using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TraktSimilarProvider.Dto;

namespace Jellyfin.Plugin.TraktSimilarProvider.Services;

/// <summary>
/// Client per gli endpoint pubblici e personalizzati di Trakt.tv necessari a questo plugin.
/// </summary>
/// <remarks>
/// Gli endpoint <c>/movies|shows/{id}/...</c> dell'API Trakt v2 accettano solo Trakt ID, slug
/// Trakt o IMDb ID come <c>{id}</c> — non l'id TMDb. Per questo le firme qui sotto usano
/// l'IMDb id (stringa, es. "tt1375666") come chiave, essendo il più affidabile tra quelli che
/// Jellyfin popola di norma insieme al TMDb id.
/// </remarks>
public interface ITraktDiscoveryClient
{
    /// <summary>
    /// Recupera i titoli correlati a un film (<c>GET /movies/{imdbId}/related</c>). Dato
    /// pubblico, non richiede un account Trakt collegato.
    /// </summary>
    /// <param name="imdbId">L'id IMDb del film sorgente (es. "tt1375666").</param>
    /// <param name="cancellationToken">Token di cancellazione.</param>
    /// <returns>I titoli correlati, o lista vuota in caso di errore.</returns>
    Task<IReadOnlyList<TraktTitleDto>> GetRelatedMoviesAsync(string imdbId, CancellationToken cancellationToken);

    /// <summary>
    /// Recupera i titoli correlati a una serie (<c>GET /shows/{imdbId}/related</c>). Dato
    /// pubblico, non richiede un account Trakt collegato.
    /// </summary>
    /// <param name="imdbId">L'id IMDb della serie sorgente.</param>
    /// <param name="cancellationToken">Token di cancellazione.</param>
    /// <returns>I titoli correlati, o lista vuota in caso di errore.</returns>
    Task<IReadOnlyList<TraktTitleDto>> GetRelatedShowsAsync(string imdbId, CancellationToken cancellationToken);

    /// <summary>
    /// Recupera le raccomandazioni film personalizzate (<c>GET /recommendations/movies</c>) per
    /// l'utente il cui token è fornito. Richiede un account Trakt collegato e valido.
    /// </summary>
    /// <param name="accessToken">Il token di accesso Trakt dell'utente.</param>
    /// <param name="cancellationToken">Token di cancellazione.</param>
    /// <returns>Le raccomandazioni, o lista vuota in caso di errore/token non valido.</returns>
    Task<IReadOnlyList<TraktTitleDto>> GetRecommendedMoviesAsync(string accessToken, CancellationToken cancellationToken);

    /// <summary>
    /// Recupera le raccomandazioni serie personalizzate (<c>GET /recommendations/shows</c>) per
    /// l'utente il cui token è fornito. Richiede un account Trakt collegato e valido.
    /// </summary>
    /// <param name="accessToken">Il token di accesso Trakt dell'utente.</param>
    /// <param name="cancellationToken">Token di cancellazione.</param>
    /// <returns>Le raccomandazioni, o lista vuota in caso di errore/token non valido.</returns>
    Task<IReadOnlyList<TraktTitleDto>> GetRecommendedShowsAsync(string accessToken, CancellationToken cancellationToken);
}
