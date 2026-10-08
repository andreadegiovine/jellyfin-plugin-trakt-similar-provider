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
/// Plugin principale: registra "Trakt" come fornitore di elementi simili (film e serie) e
/// di suggerimenti, basandosi sul plugin ufficiale "Trakt" per le credenziali dell'utente.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// GUID del plugin ufficiale "Trakt" (jellyfin/jellyfin-plugin-trakt), da cui dipendiamo
    /// per le credenziali dell'account collegato. Verificato dal sorgente ufficiale: Trakt/Plugin.cs.
    /// </summary>
    public static readonly Guid OfficialTraktPluginId = new("4fe3201e-d6ae-4f2e-8917-e12bda571281");

    /// <summary>
    /// Prefisso anteposto a ogni messaggio di log di questo plugin, per riconoscerlo nei log del server.
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
                EmbeddedResourcePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.Configuration.configPage.html",
                    GetType().Namespace)
            }
        ];
    }
}
