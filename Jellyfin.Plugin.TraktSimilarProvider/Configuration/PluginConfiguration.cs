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
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        CacheHours = DefaultCacheHours;
        MaxItemsPerRequest = 20;
    }

    /// <summary>
    /// Gets or sets how many hours the Trakt <c>/related</c> responses are kept in memory.
    /// The same cache serves both the "Similar" carousel and the "Suggestions" tab, so each
    /// title is requested from Trakt.tv at most once per interval.
    /// </summary>
    public int CacheHours { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of items returned per request.
    /// </summary>
    public int MaxItemsPerRequest { get; set; }
}
