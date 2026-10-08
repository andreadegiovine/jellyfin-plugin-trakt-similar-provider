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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TraktSimilarProvider.Providers;

/// <summary>
/// Fornitore nativo "Trakt" per la tab Suggerimenti (film e serie), basato sugli endpoint
/// personalizzati <c>/recommendations/movies|shows</c> di Trakt.tv.
/// </summary>
/// <remarks>
/// <para>
/// A differenza del fornitore "Simili" (<see cref="TraktSimilarItemsProvider"/>), Trakt non
/// offre un concetto di "raccomandazioni simili a QUESTO film visto di recente": la sua API di
/// raccomandazioni è un'unica lista personalizzata per utente, senza un titolo seme. Questo
/// fornitore restituisce quindi la stessa lista per ciascun item sorgente del batch: le diverse
/// categorie della tab Suggerimenti mostreranno gli stessi titoli. È un limite noto e accettato,
/// non un errore.
/// </para>
/// <para>
/// Se nessun utente Jellyfin è stato selezionato in configurazione, o il suo account Trakt non
/// risulta collegato/valido, questo fornitore restituisce un risultato vuoto per ogni item
/// sorgente: non c'è alcun fallback automatico al fornitore nativo "Local Genre/Tag", perché il
/// core usa un solo fornitore batch alla volta (il primo trovato). In tal caso la tab Suggerimenti
/// risulterà vuota finché la configurazione non viene completata.
/// </para>
/// </remarks>
public sealed class TraktSuggestionsProvider : IBatchLocalSimilarItemsProvider
{
    private const string MoviesCacheKey = "trakt-suggestions:movies";
    private const string ShowsCacheKey = "trakt-suggestions:shows";

    private readonly ITraktDiscoveryClient _discoveryClient;
    private readonly ITraktPluginBridge _traktPluginBridge;
    private readonly ILocalTitleResolver _titleResolver;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<TraktSuggestionsProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TraktSuggestionsProvider"/> class.
    /// </summary>
    /// <param name="discoveryClient">Instance of the <see cref="ITraktDiscoveryClient"/> interface.</param>
    /// <param name="traktPluginBridge">Instance of the <see cref="ITraktPluginBridge"/> interface.</param>
    /// <param name="titleResolver">Instance of the <see cref="ILocalTitleResolver"/> interface.</param>
    /// <param name="memoryCache">Instance of the <see cref="IMemoryCache"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TraktSuggestionsProvider}"/> interface.</param>
    public TraktSuggestionsProvider(
        ITraktDiscoveryClient discoveryClient,
        ITraktPluginBridge traktPluginBridge,
        ILocalTitleResolver titleResolver,
        IMemoryCache memoryCache,
        ILogger<TraktSuggestionsProvider> logger)
    {
        _discoveryClient = discoveryClient;
        _traktPluginBridge = traktPluginBridge;
        _titleResolver = titleResolver;
        _memoryCache = memoryCache;
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
        var result = new Dictionary<Guid, IReadOnlyList<BaseItem>>(sourceItems.Count);

        var movieIds = sourceItems.Where(i => i is Movie).Select(i => i.Id).ToList();
        var seriesIds = sourceItems.Where(i => i is Series).Select(i => i.Id).ToList();

        var limit = query.Limit ?? 10;

        _logger.LogInformation(Plugin.LogPrefix + "Suggestions requested for {Count} source items (limit {Limit})", sourceItems.Count, limit);

        if (movieIds.Count > 0)
        {
            var movies = await GetSuggestedMoviesAsync(limit, cancellationToken).ConfigureAwait(false);
            foreach (var id in movieIds)
            {
                result[id] = movies;
            }
        }

        if (seriesIds.Count > 0)
        {
            var shows = await GetSuggestedShowsAsync(limit, cancellationToken).ConfigureAwait(false);
            foreach (var id in seriesIds)
            {
                result[id] = shows;
            }
        }

        foreach (var item in sourceItems)
        {
            result.TryAdd(item.Id, Array.Empty<BaseItem>());
        }

        return result;
    }

    private async Task<IReadOnlyList<BaseItem>> GetSuggestedMoviesAsync(int limit, CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue(MoviesCacheKey, out IReadOnlyList<BaseItem>? cached) && cached is not null)
        {
            return cached;
        }

        var resolved = await FetchAndResolveAsync(
            BaseItemKind.Movie,
            limit,
            (token, ct) => _discoveryClient.GetRecommendedMoviesAsync(token, ct),
            cancellationToken).ConfigureAwait(false);

        if (resolved.Count > 0)
        {
            CacheResult(MoviesCacheKey, resolved);
        }
        
        return resolved;
    }

    private async Task<IReadOnlyList<BaseItem>> GetSuggestedShowsAsync(int limit, CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue(ShowsCacheKey, out IReadOnlyList<BaseItem>? cached) && cached is not null)
        {
            return cached;
        }

        var resolved = await FetchAndResolveAsync(
            BaseItemKind.Series,
            limit,
            (token, ct) => _discoveryClient.GetRecommendedShowsAsync(token, ct),
            cancellationToken).ConfigureAwait(false);

        if (resolved.Count > 0)
        {
            CacheResult(ShowsCacheKey, resolved);
        }
        
        return resolved;
    }

    private async Task<IReadOnlyList<BaseItem>> FetchAndResolveAsync(
        BaseItemKind kind,
        int limit,
        Func<string, CancellationToken, Task<IReadOnlyList<TraktTitleDto>>> fetch,
        CancellationToken cancellationToken)
    {
        var userId = Plugin.Instance?.Configuration.SelectedJellyfinUserId ?? Guid.Empty;
        if (userId == Guid.Empty)
        {
            _logger.LogWarning(Plugin.LogPrefix + "No Jellyfin user selected in the plugin configuration: Trakt suggestions are disabled");
            return Array.Empty<BaseItem>();
        }

        var accessToken = _traktPluginBridge.GetAccessToken(userId);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _logger.LogWarning(Plugin.LogPrefix + "No valid Trakt token for the configured Jellyfin user {UserId}", userId);
            return Array.Empty<BaseItem>();
        }

        IReadOnlyList<TraktTitleDto> titles;
        try
        {
            titles = await fetch(accessToken, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, Plugin.LogPrefix + "Failed to fetch Trakt recommendations");
            return Array.Empty<BaseItem>();
        }

        var resolved = _titleResolver.Resolve(titles, kind, limit);
        _logger.LogInformation(
            Plugin.LogPrefix + "Trakt returned {TraktCount} {Kind} recommendations, {LocalCount} found in the local library",
            titles.Count,
            kind,
            resolved.Count);
        return resolved;
    }

    private void CacheResult(string key, IReadOnlyList<BaseItem> value)
    {
        var hours = Math.Max(1, Plugin.Instance?.Configuration.RecommendationsCacheHours ?? 6);
        _memoryCache.Set(key, value, TimeSpan.FromHours(hours));
    }
}
