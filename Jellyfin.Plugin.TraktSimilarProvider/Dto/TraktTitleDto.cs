using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TraktSimilarProvider.Dto;

/// <summary>
/// A movie or show returned by Trakt.tv (shape of <c>/movies|shows/{id}/related</c>).
/// </summary>
public class TraktTitleDto
{
    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the release year.
    /// </summary>
    [JsonPropertyName("year")]
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets the external identifiers of the title.
    /// </summary>
    [JsonPropertyName("ids")]
    public TraktIdsDto? Ids { get; set; }
}
