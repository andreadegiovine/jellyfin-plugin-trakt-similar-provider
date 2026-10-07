using System;

namespace Jellyfin.Plugin.TraktSimilarProvider.Api;

/// <summary>
/// Un utente Jellyfin collegato a un account Trakt.
/// </summary>
public sealed class LinkedUserDto
{
    /// <summary>
    /// Gets l'id dell'utente Jellyfin.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets il nome dell'utente Jellyfin.
    /// </summary>
    public required string Name { get; init; }
}
