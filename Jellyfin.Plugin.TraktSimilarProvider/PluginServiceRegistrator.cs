using System;
using Jellyfin.Plugin.TraktSimilarProvider.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.TraktSimilarProvider;

/// <summary>
/// Registers the plugin's supporting services. The <c>ISimilarItemsProvider</c> implementations
/// (<see cref="Providers.TraktSimilarItemsProvider"/>, <see cref="Providers.TraktSuggestionsProvider"/>)
/// must not be registered here: Jellyfin discovers them automatically by scanning the loaded
/// assemblies (<c>ApplicationHost.GetExports&lt;ISimilarItemsProvider&gt;()</c>), but still builds
/// them through the DI container, which is why their dependencies are registered here.
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
        serviceCollection.AddSingleton<ILocalTitleResolver, LocalTitleResolver>();
    }
}
