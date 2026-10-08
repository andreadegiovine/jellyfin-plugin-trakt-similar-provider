using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.TraktSimilarProvider.Dto;
using Jellyfin.Plugin.TraktSimilarProvider.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
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
    /// Maximum number of concurrent requests to Trakt for a single batch.
    /// </summary>
    private const int MaxParallelRequests = 4;

    private readonly ITraktDiscoveryClient _discoveryClient;
    private readonly ILocalTitleResolver _titleResolver;
    private readonly ILogger<TraktSuggestionsProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TraktSuggestionsProvider"/> class.
    /// </summary>
    /// <param name="discoveryClient">Instance of the <see cref="ITraktDiscoveryClient"/> interface.</param>
    /// <param name="titleResolver">Instance of the <see cref="ILocalTitleResolver"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TraktSuggestionsProvider}"/> interface.</param>
    public TraktSuggestionsProvider(
        ITraktDiscoveryClient discoveryClient,
        ILocalTitleResolver titleResolver,
        ILogger<TraktSuggestionsProvider> logger)
    {
        _discoveryClient = discoveryClient;
        _titleResolver = titleResolver;
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

        using var gate = new SemaphoreSlim(MaxParallelRequests);

        var tasks = new List<Task<IReadOnlyList<BaseItem>>>(sourceItems.Count);
        foreach (var source in sourceItems)
        {
            tasks.Add(GetSuggestionsForSourceAsync(source, query, limit, gate, cancellationToken));
        }

        var lists = await Task.WhenAll(tasks).ConfigureAwait(false);

        var result = new Dictionary<Guid, IReadOnlyList<BaseItem>>(sourceItems.Count);
        for (var i = 0; i < sourceItems.Count; i++)
        {
            result[sourceItems[i].Id] = lists[i];
        }

        return result;
    }

    private async Task<IReadOnlyList<BaseItem>> GetSuggestionsForSourceAsync(
        BaseItem source,
        SimilarItemsQuery query,
        int limit,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        if (source is not (Movie or Series)
            || !source.TryGetProviderId(MetadataProvider.Imdb, out var imdbId)
            || string.IsNullOrWhiteSpace(imdbId))
        {
            return Array.Empty<BaseItem>();
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
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
            gate.Release();
        }
    }
}
