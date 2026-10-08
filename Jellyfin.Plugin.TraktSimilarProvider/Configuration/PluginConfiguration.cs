using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TraktSimilarProvider.Configuration;

/// <summary>
/// Persistent plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Default value of <see cref="CacheHours"/>.
    /// </summary>
    public const int DefaultCacheHours = 72;

    /// <summary>
    /// Default value of <see cref="MaxItemsPerRequest"/>, which is also the highest value accepted.
    /// </summary>
    public const int DefaultMaxItemsPerRequest = 50;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        CacheHours = DefaultCacheHours;
        MaxItemsPerRequest = DefaultMaxItemsPerRequest;
    }

    /// <summary>
    /// Gets or sets how many hours the Trakt <c>/related</c> responses are kept in memory.
    /// The same cache serves both the "Similar" carousel and the "Suggestions" tab, so each
    /// title is requested from Trakt.tv at most once per interval.
    /// </summary>
    public int CacheHours { get; set; }

    /// <summary>
    /// Gets or sets how many related titles are requested from Trakt.tv for each movie or show
    /// (the <c>limit</c> query parameter of <c>/related</c>). Values outside 1-50 are clamped.
    /// </summary>
    public int MaxItemsPerRequest { get; set; }
}
