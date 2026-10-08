using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using Jellyfin.Plugin.TraktSimilarProvider.Dto;
using Jellyfin.Plugin.TraktSimilarProvider.Services;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using BaseItem = MediaBrowser.Controller.Entities.BaseItem;

namespace Jellyfin.Plugin.TraktSimilarProvider.Providers;

/// <summary>
/// Fornitore nativo "Trakt" per il carosello "Simili", per film e serie, basato
/// sugli endpoint pubblici <c>/movies|shows/{imdbId}/related</c> di Trakt.tv.
/// Non richiede un account Trakt collegato: sono dati pubblici, uguali per tutti.
/// </summary>
public sealed class TraktSimilarItemsProvider : IRemoteSimilarItemsProvider<Movie>, IRemoteSimilarItemsProvider<Series>
{
    private readonly ITraktDiscoveryClient _discoveryClient;
    private readonly ILogger<TraktSimilarItemsProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TraktSimilarItemsProvider"/> class.
    /// </summary>
    /// <param name="discoveryClient">Instance of the <see cref="ITraktDiscoveryClient"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TraktSimilarItemsProvider}"/> interface.</param>
    public TraktSimilarItemsProvider(ITraktDiscoveryClient discoveryClient, ILogger<TraktSimilarItemsProvider> logger)
    {
        _discoveryClient = discoveryClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => "Trakt";

    /// <inheritdoc/>
    public MetadataPluginType Type => MetadataPluginType.SimilarityProvider;

    /// <inheritdoc/>
    public TimeSpan? CacheDuration
    {
        get
        {
            var days = Plugin.Instance?.Configuration.SimilarItemsCacheDays ?? 0;
            return days > 0 ? TimeSpan.FromDays(days) : null;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<SimilarItemReference> GetSimilarItemsAsync(
        Movie item,
        SimilarItemsQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!item.TryGetProviderId(MetadataProvider.Imdb, out var imdbId))
        {
            yield break;
        }

        IReadOnlyList<TraktTitleDto> related;
        try
        {
            related = await _discoveryClient.GetRelatedMoviesAsync(imdbId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, Plugin.LogPrefix + "Failed to fetch related titles from Trakt for movie {ImdbId}", imdbId);
            yield break;
        }

        foreach (var reference in ToReferences(related))
        {
            yield return reference;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<SimilarItemReference> GetSimilarItemsAsync(
        Series item,
        SimilarItemsQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!item.TryGetProviderId(MetadataProvider.Imdb, out var imdbId))
        {
            yield break;
        }

        IReadOnlyList<TraktTitleDto> related;
        try
        {
            related = await _discoveryClient.GetRelatedShowsAsync(imdbId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, Plugin.LogPrefix + "Failed to fetch related titles from Trakt for show {ImdbId}", imdbId);
            yield break;
        }

        foreach (var reference in ToReferences(related))
        {
            yield return reference;
        }
    }

    bool IRemoteSimilarItemsProvider.Supports(Type itemType)
        => typeof(Movie).IsAssignableFrom(itemType) || typeof(Series).IsAssignableFrom(itemType);

    IAsyncEnumerable<SimilarItemReference> IRemoteSimilarItemsProvider.GetSimilarItemsAsync(
        BaseItem item,
        SimilarItemsQuery query,
        CancellationToken cancellationToken)
        => item switch
        {
            Movie movie => GetSimilarItemsAsync(movie, query, cancellationToken),
            Series series => GetSimilarItemsAsync(series, query, cancellationToken),
            _ => throw new ArgumentException($"Unsupported item type {item.GetType()}", nameof(item))
        };

    private static IEnumerable<SimilarItemReference> ToReferences(IReadOnlyList<TraktTitleDto> titles)
    {
        foreach (var title in titles)
        {
            if (title.Ids?.Tmdb is { } tmdbId)
            {
                yield return new SimilarItemReference
                {
                    ProviderName = MetadataProvider.Tmdb.ToString(),
                    ProviderId = tmdbId.ToString(CultureInfo.InvariantCulture)
                };
            }
            else if (!string.IsNullOrWhiteSpace(title.Ids?.Imdb))
            {
                yield return new SimilarItemReference
                {
                    ProviderName = MetadataProvider.Imdb.ToString(),
                    ProviderId = title.Ids!.Imdb!
                };
            }
        }
    }
}
