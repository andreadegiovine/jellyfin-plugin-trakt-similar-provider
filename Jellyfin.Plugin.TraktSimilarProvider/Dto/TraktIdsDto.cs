using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TraktSimilarProvider.Dto;

/// <summary>
/// External identifiers of a Trakt title.
/// </summary>
public class TraktIdsDto
{
    /// <summary>
    /// Gets or sets the IMDb id (e.g. "tt1375666").
    /// </summary>
    [JsonPropertyName("imdb")]
    public string? Imdb { get; set; }

    /// <summary>
    /// Gets or sets the TMDb id.
    /// </summary>
    [JsonPropertyName("tmdb")]
    public int? Tmdb { get; set; }

    /// <summary>
    /// Gets or sets the Trakt id.
    /// </summary>
    [JsonPropertyName("trakt")]
    public int? Trakt { get; set; }
}
