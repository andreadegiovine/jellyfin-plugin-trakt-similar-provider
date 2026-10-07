using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TraktSimilarProvider.Dto;

/// <summary>
/// Un film o una serie restituiti da Trakt.tv (shape comune a <c>/movies|shows/{id}/related</c>
/// e <c>/recommendations/movies|shows</c>).
/// </summary>
public class TraktTitleDto
{
    /// <summary>
    /// Gets or sets il titolo.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets l'anno di uscita.
    /// </summary>
    [JsonPropertyName("year")]
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets gli identificatori esterni del titolo.
    /// </summary>
    [JsonPropertyName("ids")]
    public TraktIdsDto? Ids { get; set; }
}
