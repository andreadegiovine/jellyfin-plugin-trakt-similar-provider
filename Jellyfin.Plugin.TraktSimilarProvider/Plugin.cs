using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.TraktSimilarProvider.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.TraktSimilarProvider;

/// <summary>
/// Main plugin class: registers "Trakt" as a provider of similar items (movies and shows) and of
/// suggestions, both based on the public Trakt.tv <c>/related</c> endpoints.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Prefix prepended to every log message of this plugin, to make it easy to find in the server log.
    /// </summary>
    public const string LogPrefix = "Trakt Similar Provider: ";

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Trakt Similar Provider";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("a3f1c9d4-7e2b-4a6f-9c1d-5b8e2f0a6c3d");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                DisplayName = Name,
                EmbeddedResourcePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.Configuration.configPage.html",
                    GetType().Namespace),
                EnableInMainMenu = true,
                MenuSection = "server",
                MenuIcon = "recommend"
            }
        ];
    }
}
