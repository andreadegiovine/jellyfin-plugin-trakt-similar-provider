using System.Collections.Generic;

namespace Jellyfin.Plugin.TraktSimilarProvider.Api;

/// <summary>
/// Stato della dipendenza dal plugin Trakt ufficiale e utenti collegati disponibili.
/// </summary>
public sealed class TraktSimilarProviderStatusDto
{
    /// <summary>
    /// Gets a value indicating whether il plugin ufficiale jellyfin-plugin-trakt è installato e attivo.
    /// </summary>
    public required bool OfficialTraktPluginAvailable { get; init; }

    /// <summary>
    /// Gets gli utenti Jellyfin risultanti collegati a un account Trakt nel plugin ufficiale.
    /// </summary>
    public required IReadOnlyList<LinkedUserDto> LinkedUsers { get; init; }
}
