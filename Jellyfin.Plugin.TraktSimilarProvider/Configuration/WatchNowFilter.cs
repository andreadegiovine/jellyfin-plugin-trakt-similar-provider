namespace Jellyfin.Plugin.TraktSimilarProvider.Configuration;

/// <summary>
/// Valore del parametro <c>watchnow</c> inviato agli endpoint Trakt
/// <c>/recommendations/movies</c> e <c>/recommendations/shows</c>.
/// </summary>
public enum WatchNowFilter
{
    /// <summary>
    /// Nessun filtro watchnow.
    /// </summary>
    None = 0,

    /// <summary>
    /// Disponibile in streaming su un servizio preferito dell'utente (<c>favorites</c>).
    /// </summary>
    Favorites = 1,

    /// <summary>
    /// Disponibile in streaming su un qualsiasi servizio nel paese dell'utente (<c>any</c>).
    /// </summary>
    Any = 2,

    /// <summary>
    /// Disponibile in streaming su un qualsiasi servizio in qualsiasi paese (<c>any_all</c>).
    /// </summary>
    AnyAll = 3,

    /// <summary>
    /// Disponibile gratuitamente in streaming nel paese dell'utente (<c>free</c>).
    /// </summary>
    Free = 4
}
