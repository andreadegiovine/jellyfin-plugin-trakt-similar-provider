using System;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TraktSimilarProvider.Configuration;

/// <summary>
/// Configurazione persistente del plugin.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        SelectedJellyfinUserId = Guid.Empty;
        WatchNowFilter = WatchNowFilter.Favorites;
        SimilarItemsCacheDays = 3;
        RecommendationsCacheHours = 6;
        MaxItemsPerRequest = 20;
    }

    /// <summary>
    /// Gets or sets l'id dell'utente Jellyfin il cui account Trakt collegato (tramite il
    /// plugin ufficiale "Trakt") viene usato per le raccomandazioni personalizzate
    /// (<c>/recommendations/movies</c> e <c>/recommendations/shows</c>) e per escludere dai
    /// risultati "Simili" i titoli già segnati come visti su Trakt. <see cref="Guid.Empty"/>
    /// se non configurato: in tal caso le raccomandazioni personalizzate non sono disponibili
    /// (il fornitore "Simili" via /related continua a funzionare comunque, essendo dati pubblici).
    /// </summary>
    public Guid SelectedJellyfinUserId { get; set; }

    /// <summary>
    /// Gets or sets il filtro <c>watchnow</c> applicato alle richieste di raccomandazioni.
    /// </summary>
    public WatchNowFilter WatchNowFilter { get; set; }

    /// <summary>
    /// Gets or sets per quanti giorni Jellyfin mantiene in cache i risultati del fornitore
    /// "Simili" per singolo item (letto da <c>ISimilarItemsProvider.CacheDuration</c>,
    /// stesso meccanismo usato dal fornitore nativo TheMovieDb).
    /// </summary>
    public int SimilarItemsCacheDays { get; set; }

    /// <summary>
    /// Gets or sets per quante ore questo plugin mantiene in cache internamente le
    /// raccomandazioni personalizzate, per evitare di richiamare Trakt.tv ad ogni apertura
    /// della tab Suggerimenti.
    /// </summary>
    public int RecommendationsCacheHours { get; set; }

    /// <summary>
    /// Gets or sets il numero massimo di elementi restituiti per richiesta.
    /// </summary>
    public int MaxItemsPerRequest { get; set; }
}
