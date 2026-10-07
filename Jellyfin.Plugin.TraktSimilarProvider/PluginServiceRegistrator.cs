using System;
using Jellyfin.Plugin.TraktSimilarProvider.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.TraktSimilarProvider;

/// <summary>
/// Registra i servizi di supporto del plugin. I fornitori <c>ISimilarItemsProvider</c>
/// (<see cref="Providers.TraktSimilarItemsProvider"/>, <see cref="Providers.TraktSuggestionsProvider"/>)
/// non vanno registrati qui: Jellyfin li scopre automaticamente scansionando gli assembly
/// caricati (<c>ApplicationHost.GetExports&lt;ISimilarItemsProvider&gt;()</c>), costruendoli
/// comunque tramite il container DI — da cui la necessità di registrare qui le loro dipendenze.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddMemoryCache();

        serviceCollection.AddHttpClient(TraktDiscoveryClient.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        serviceCollection.AddSingleton<ITraktDiscoveryClient, TraktDiscoveryClient>();
        serviceCollection.AddSingleton<ITraktPluginBridge, TraktPluginBridge>();
        serviceCollection.AddSingleton<ILocalTitleResolver, LocalTitleResolver>();
    }
}
