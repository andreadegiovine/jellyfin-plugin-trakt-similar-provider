using System.Collections.Generic;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.TraktSimilarProvider.Dto;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.TraktSimilarProvider.Services;

/// <summary>
/// Risolve i titoli Trakt (usati per le raccomandazioni, dove serve restituire direttamente
/// dei <see cref="BaseItem"/> risolti, non dei riferimenti) contro la libreria locale, via
/// Tmdb id o, in mancanza, Imdb id. Un titolo non posseduto viene scartato: nessun elemento
/// virtuale viene mai creato.
/// </summary>
public interface ILocalTitleResolver
{
    /// <summary>
    /// Risolve una lista di titoli Trakt in item locali esistenti, nell'ordine originale.
    /// </summary>
    /// <param name="titles">I titoli Trakt da risolvere.</param>
    /// <param name="kind">Il tipo di item da cercare (Movie o Series).</param>
    /// <param name="limit">Numero massimo di item da restituire.</param>
    /// <returns>Gli item locali trovati.</returns>
    IReadOnlyList<BaseItem> Resolve(IReadOnlyList<TraktTitleDto> titles, BaseItemKind kind, int limit);
}
