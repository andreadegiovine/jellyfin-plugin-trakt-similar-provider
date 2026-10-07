using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.TraktSimilarProvider.Services;

/// <summary>
/// Legge, senza riferimento a tempo di compilazione (il plugin ufficiale non è distribuito
/// come pacchetto NuGet), lo stato e le credenziali del plugin ufficiale "Trakt" già
/// caricato nello stesso processo server, tramite riflessione sui suoi tipi pubblici.
/// </summary>
public interface ITraktPluginBridge
{
    /// <summary>
    /// Verifica se il plugin ufficiale Trakt è installato e attivo su questo server.
    /// </summary>
    /// <returns><c>true</c> se il plugin è presente e la sua istanza è stata caricata.</returns>
    bool IsOfficialTraktPluginAvailable();

    /// <summary>
    /// Elenca gli id degli utenti Jellyfin che risultano collegati a un account Trakt nel
    /// plugin ufficiale (indipendentemente dalla validità corrente del token).
    /// </summary>
    /// <returns>Gli id utente Jellyfin collegati.</returns>
    IReadOnlyList<Guid> GetLinkedJellyfinUserIds();

    /// <summary>
    /// Recupera l'access token Trakt attualmente salvato per l'utente Jellyfin indicato.
    /// </summary>
    /// <param name="jellyfinUserId">L'id dell'utente Jellyfin.</param>
    /// <returns>Il token, o <c>null</c> se l'utente non è collegato o il token non è valido/presente.</returns>
    string? GetAccessToken(Guid jellyfinUserId);
}
