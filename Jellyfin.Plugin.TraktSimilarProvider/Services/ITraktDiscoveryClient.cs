using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TraktSimilarProvider.Dto;

namespace Jellyfin.Plugin.TraktSimilarProvider.Services;

/// <summary>
/// Client for the public Trakt.tv <c>related</c> endpoints used by this plugin. Responses are
/// cached in memory, so every caller (the "Similar" provider and the "Suggestions" provider)
/// shares the same cache.
/// </summary>
/// <remarks>
/// The <c>/movies|shows/{id}/...</c> endpoints of the Trakt v2 API only accept a Trakt id, a Trakt
/// slug or an IMDb id as <c>{id}</c> — not the TMDb id. For this reason the signatures below use
/// the IMDb id (string, e.g. "tt1375666") as the key, being the most reliable one among those
/// that Jellyfin usually populates together with the TMDb id.
/// </remarks>
public interface ITraktDiscoveryClient
{
    /// <summary>
    /// Gets the titles related to a movie (<c>GET /movies/{imdbId}/related</c>). Public data,
    /// no Trakt account required.
    /// </summary>
    /// <param name="imdbId">The IMDb id of the source movie (e.g. "tt1375666").</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The related titles, or an empty list on error.</returns>
    Task<IReadOnlyList<TraktTitleDto>> GetRelatedMoviesAsync(string imdbId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the titles related to a show (<c>GET /shows/{imdbId}/related</c>). Public data,
    /// no Trakt account required.
    /// </summary>
    /// <param name="imdbId">The IMDb id of the source show.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The related titles, or an empty list on error.</returns>
    Task<IReadOnlyList<TraktTitleDto>> GetRelatedShowsAsync(string imdbId, CancellationToken cancellationToken);
}
