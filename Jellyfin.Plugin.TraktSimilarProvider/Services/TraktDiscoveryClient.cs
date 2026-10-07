using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TraktSimilarProvider.Configuration;
using Jellyfin.Plugin.TraktSimilarProvider.Dto;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TraktSimilarProvider.Services;

/// <inheritdoc />
public sealed class TraktDiscoveryClient : ITraktDiscoveryClient
{
    /// <summary>
    /// Nome del client HTTP nominato registrato in <c>PluginServiceRegistrator</c>.
    /// </summary>
    public const string HttpClientName = "TraktSimilarProviderClient";

    private const string BaseUrl = "https://api.trakt.tv";

    /// <summary>
    /// Client id dell'applicazione Trakt del plugin ufficiale jellyfin-plugin-trakt
    /// (valore pubblico, copiato da Trakt/Api/TraktURIs.cs nel loro repository open source,
    /// come concordato con l'utente: vedi la cronologia di sviluppo per il contesto).
    /// </summary>
    private const string TraktClientId = "bfdd2e032c30c35b368f97ef4ec81587b899bcb028b91a1d4ba5589a4b6a7267";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TraktDiscoveryClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TraktDiscoveryClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TraktDiscoveryClient}"/> interface.</param>
    public TraktDiscoveryClient(IHttpClientFactory httpClientFactory, ILogger<TraktDiscoveryClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TraktTitleDto>> GetRelatedMoviesAsync(string imdbId, CancellationToken cancellationToken)
        => SendAsync($"movies/{Uri.EscapeDataString(imdbId)}/related", accessToken: null, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<TraktTitleDto>> GetRelatedShowsAsync(string imdbId, CancellationToken cancellationToken)
        => SendAsync($"shows/{Uri.EscapeDataString(imdbId)}/related", accessToken: null, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<TraktTitleDto>> GetRecommendedMoviesAsync(string accessToken, CancellationToken cancellationToken)
        => SendAsync($"recommendations/movies?{BuildRecommendationsQuery()}", accessToken, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<TraktTitleDto>> GetRecommendedShowsAsync(string accessToken, CancellationToken cancellationToken)
        => SendAsync($"recommendations/shows?{BuildRecommendationsQuery()}", accessToken, cancellationToken);

    private static string BuildRecommendationsQuery()
    {
        // Parametri fissi/noti, nessun input esterno da incapsulare: concatenazione diretta sicura.
        var parameters = new List<string> { "ignore_watched=true" };

        var watchNow = Plugin.Instance?.Configuration.WatchNowFilter ?? WatchNowFilter.None;
        var watchNowValue = watchNow switch
        {
            WatchNowFilter.Favorites => "favorites",
            WatchNowFilter.Any => "any",
            WatchNowFilter.AnyAll => "any_all",
            WatchNowFilter.Free => "free",
            _ => null
        };

        if (watchNowValue is not null)
        {
            parameters.Add($"watchnow={watchNowValue}");
        }

        return string.Join('&', parameters);
    }

    private async Task<IReadOnlyList<TraktTitleDto>> SendAsync(string relativeUrl, string? accessToken, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/{relativeUrl}");
            request.Headers.Add("trakt-api-version", "2");
            request.Headers.Add("trakt-api-key", TraktClientId);
            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            try
            {
                using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt == 0)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2);
                    await Task.Delay(retryAfter, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Trakt ha risposto {StatusCode} per {Url}", (int)response.StatusCode, relativeUrl);
                    return Array.Empty<TraktTitleDto>();
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var result = await JsonSerializer.DeserializeAsync<List<TraktTitleDto>>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                return result ?? [];
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning(ex, "Chiamata a Trakt fallita per {Url}", relativeUrl);
                return Array.Empty<TraktTitleDto>();
            }
        }

        return Array.Empty<TraktTitleDto>();
    }
}
