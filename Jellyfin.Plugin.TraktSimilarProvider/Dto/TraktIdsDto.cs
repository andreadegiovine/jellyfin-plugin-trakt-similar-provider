using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TraktSimilarProvider.Dto;

/// <summary>
/// Identificatori esterni di un titolo Trakt.
/// </summary>
public class TraktIdsDto
{
    /// <summary>
    /// Gets or sets l'id IMDb (es. "tt1375666").
    /// </summary>
    [JsonPropertyName("imdb")]
    public string? Imdb { get; set; }

    /// <summary>
    /// Gets or sets l'id TMDb.
    /// </summary>
    [JsonPropertyName("tmdb")]
    public int? Tmdb { get; set; }

    /// <summary>
    /// Gets or sets l'id Trakt.
    /// </summary>
    [JsonPropertyName("trakt")]
    public int? Trakt { get; set; }
}
