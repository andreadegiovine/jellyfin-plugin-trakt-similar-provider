using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.TraktSimilarProvider.Dto;
using Jellyfin.Plugin.TraktSimilarProvider.Services;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;
using BaseItem = MediaBrowser.Controller.Entities.BaseItem;

namespace Jellyfin.Plugin.TraktSimilarProvider.Providers;

/// <summary>
/// Native "Trakt" provider for the Suggestions tab, based on the public
/// <c>/movies|shows/{imdbId}/related</c> endpoints of Trakt.tv.
/// </summary>
/// <remarks>
/// <para>
/// Jellyfin builds the "Because you watched X" (<c>SimilarToRecentlyPlayed</c>) and
/// "Because you liked X" (<c>SimilarToLikedItem</c>) rows by passing the recently played and the
/// liked items as the <em>source items</em> of a batch request, and by expecting a separate list
/// of similar items for each of them. This provider therefore asks Trakt for the titles related to
/// each source item, so every row is specific to its source and no Trakt account is needed.
/// The responses are cached by <see cref="ITraktDiscoveryClient"/>, the same cache used by
/// <see cref="TraktSimilarItemsProvider"/>.
/// </para>
/// <para>
/// Jellyfin currently builds Suggestions for movies only, so in practice only movie source items
/// are received; show source items are handled as well in case the server starts sending them.
/// Titles already watched by the requesting user are skipped, and only titles present in the
/// local library are returned.
/// </para>
/// <para>
/// Jellyfin uses a single batch provider for the Suggestions tab (the first one found), so when
/// this plugin is installed it takes the place of the built-in "Local Genre/Tag" one.
/// </para>
/// </remarks>
public sealed class TraktSuggestionsProvider : IBatchLocalSimilarItemsProvider
{
    /// <summary>
    /// Maximum number of concurrent requests to Trakt. The gate is static (and never disposed)
    /// so that it is shared by all batches and outlives every task that uses it.
    /// </summary>
    private const int MaxParallelRequests = 4;

    private static readonly SemaphoreSlim RequestGate = new(MaxParallelRequests);

    private readonly ITraktDiscoveryClient _discoveryClient;
    private readonly ILocalTitleResolver _titleResolver;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<TraktSuggestionsProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TraktSuggestionsProvider"/> class.
    /// </summary>
    /// <param name="discoveryClient">Instance of the <see cref="ITraktDiscoveryClient"/> interface.</param>
    /// <param name="titleResolver">Instance of the <see cref="ILocalTitleResolver"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TraktSuggestionsProvider}"/> interface.</param>
    public TraktSuggestionsProvider(
        ITraktDiscoveryClient discoveryClient,
        ILocalTitleResolver titleResolver,
        ILibraryManager libraryManager,
        ILogger<TraktSuggestionsProvider> logger)
    {
        _discoveryClient = discoveryClient;
        _titleResolver = titleResolver;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => "Trakt";

    /// <inheritdoc/>
    public MetadataPluginType Type => MetadataPluginType.LocalSimilarityProvider;

    /// <inheritdoc/>
    public async Task<Dictionary<Guid, IReadOnlyList<BaseItem>>> GetBatchSimilarItemsAsync(
        IReadOnlyList<BaseItem> sourceItems,
        SimilarItemsQuery query,
        CancellationToken cancellationToken)
    {
        var limit = query.Limit ?? 10;

        _logger.LogInformation(Plugin.LogPrefix + "Suggestions requested for {Count} source items (limit {Limit})", sourceItems.Count, limit);

        var imdbIds = GetImdbIds(sourceItems);

        var tasks = sourceItems
            .Select(source => GetSuggestionsForSourceAsync(source, imdbIds.GetValueOrDefault(source.Id), query, limit, cancellationToken))
            .ToList();
        var lists = await Task.WhenAll(tasks).ConfigureAwait(false);

        var result = new Dictionary<Guid, IReadOnlyList<BaseItem>>(sourceItems.Count);
        for (var i = 0; i < sourceItems.Count; i++)
        {
            result[sourceItems[i].Id] = lists[i];
        }

        return result;
    }

    /// <summary>
    /// Gets the IMDb id of every source item that has one. Jellyfin only loads the provider ids of
    /// an item when the request asks for the <c>ProviderIds</c> field, and the Suggestions request
    /// made by the web client does not, so the source items usually arrive without any provider id.
    /// Those items are reloaded here, in a single query, asking for the field explicitly.
    /// </summary>
    private Dictionary<Guid, string> GetImdbIds(IReadOnlyList<BaseItem> sourceItems)
    {
        var imdbIds = new Dictionary<Guid, string>(sourceItems.Count);
        var missingIds = new List<Guid>();

        foreach (var source in sourceItems)
        {
            if (source.TryGetProviderId(MetadataProvider.Imdb, out var imdbId) && !string.IsNullOrWhiteSpace(imdbId))
            {
                imdbIds[source.Id] = imdbId;
            }
            else
            {
                missingIds.Add(source.Id);
            }
        }

        if (missingIds.Count == 0)
        {
            return imdbIds;
        }

        var reloaded = _libraryManager.GetItemList(new InternalItemsQuery
        {
            ItemIds = [.. missingIds],
            Recursive = true,
            EnableTotalRecordCount = false,
            DtoOptions = new DtoOptions
            {
                Fields = [ItemFields.ProviderIds],
                EnableImages = false,
                EnableUserData = false
            }
        });

        foreach (var item in reloaded)
        {
            if (item.TryGetProviderId(MetadataProvider.Imdb, out var imdbId) && !string.IsNullOrWhiteSpace(imdbId))
            {
                imdbIds[item.Id] = imdbId;
            }
        }

        _logger.LogDebug(
            Plugin.LogPrefix + "Reloaded {Requested} source items to read their provider ids, {Found} have an IMDb id",
            missingIds.Count,
            missingIds.Count(id => imdbIds.ContainsKey(id)));

        return imdbIds;
    }

    private async Task<IReadOnlyList<BaseItem>> GetSuggestionsForSourceAsync(
        BaseItem source,
        string? imdbId,
        SimilarItemsQuery query,
        int limit,
        CancellationToken cancellationToken)
    {
        if (source is not (Movie or Series))
        {
            _logger.LogInformation(Plugin.LogPrefix + "Skipping source item {Name} ({Type}): only movies and shows are supported", source.Name, source.GetType().Name);
            return Array.Empty<BaseItem>();
        }

        if (string.IsNullOrWhiteSpace(imdbId))
        {
            _logger.LogInformation(Plugin.LogPrefix + "Skipping source item {Name}: it has no IMDb id", source.Name);
            return Array.Empty<BaseItem>();
        }

        await RequestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var isMovie = source is Movie;
            IReadOnlyList<TraktTitleDto> titles = isMovie
                ? await _discoveryClient.GetRelatedMoviesAsync(imdbId, cancellationToken).ConfigureAwait(false)
                : await _discoveryClient.GetRelatedShowsAsync(imdbId, cancellationToken).ConfigureAwait(false);

            var kind = isMovie ? BaseItemKind.Movie : BaseItemKind.Series;
            var resolved = _titleResolver.Resolve(titles, kind, limit, query.User, [.. query.ExcludeItemIds, source.Id]);

            _logger.LogInformation(
                Plugin.LogPrefix + "Trakt returned {TraktCount} related {Kind} titles for {ImdbId}, {LocalCount} found in the local library",
                titles.Count,
                kind,
                imdbId,
                resolved.Count);
            return resolved;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, Plugin.LogPrefix + "Failed to build Trakt suggestions for {ImdbId}", imdbId);
            return Array.Empty<BaseItem>();
        }
        finally
        {
            RequestGate.Release();
        }
    }
}
