using System;
using System.Collections.Generic;
using System.IO;
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

    /// <summary>
    /// Lunghezza massima del body (richiesta o risposta) riportata nei log di errore.
    /// </summary>
    private const int MaxLoggedBodyLength = 2000;

    private static readonly string UserAgent = BuildUserAgent();

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

    private static string BuildUserAgent()
    {
        var version = typeof(TraktDiscoveryClient).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        return $"Jellyfin-Trakt-Similar-Provider/{version}";
    }

    private static string FormatHeaders(params HttpHeaders?[] headerSets)
    {
        var parts = new List<string>();
        foreach (var headers in headerSets)
        {
            if (headers is null)
            {
                continue;
            }

            foreach (var header in headers)
            {
                // Il token di accesso dell'utente non deve mai finire nei log.
                var value = header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                    ? "<redacted>"
                    : string.Join(',', header.Value);
                parts.Add($"{header.Key}: {value}");
            }
        }

        return parts.Count == 0 ? "<none>" : string.Join("; ", parts);
    }

    private static async Task<string> FormatBodyAsync(HttpContent? content)
    {
        if (content is null)
        {
            return "<none>";
        }

        string body;
        try
        {
            body = await content.ReadAsStringAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or ObjectDisposedException)
        {
            return $"<unreadable: {ex.Message}>";
        }

        if (body.Length == 0)
        {
            return "<empty>";
        }

        return body.Length > MaxLoggedBodyLength
            ? string.Concat(body.AsSpan(0, MaxLoggedBodyLength), "... (truncated)")
            : body;
    }

    /// <summary>
    /// Registra nei log tutti i dettagli utili a diagnosticare una richiesta HTTP fallita:
    /// metodo, URL, header e body della richiesta e, se disponibile, stato, header e body della risposta.
    /// </summary>
    private async Task LogRequestFailureAsync(HttpRequestMessage request, HttpResponseMessage? response, Exception? exception)
    {
        var method = request.Method.Method;
        var url = request.RequestUri?.ToString() ?? "<unknown>";
        var requestHeaders = FormatHeaders(request.Headers, request.Content?.Headers);
        var requestBody = await FormatBodyAsync(request.Content).ConfigureAwait(false);

        if (response is null)
        {
            _logger.LogWarning(
                exception,
                Plugin.LogPrefix + "HTTP request failed. Method: {Method}; URL: {Url}; Request headers: {RequestHeaders}; Request body: {RequestBody}",
                method,
                url,
                requestHeaders,
                requestBody);
            return;
        }

        var responseHeaders = FormatHeaders(response.Headers, response.Content.Headers);
        var responseBody = await FormatBodyAsync(response.Content).ConfigureAwait(false);

        _logger.LogWarning(
            exception,
            Plugin.LogPrefix + "HTTP request failed with status {StatusCode} {ReasonPhrase}. Method: {Method}; URL: {Url}; Request headers: {RequestHeaders}; Request body: {RequestBody}; Response headers: {ResponseHeaders}; Response body: {ResponseBody}",
            (int)response.StatusCode,
            response.ReasonPhrase,
            method,
            url,
            requestHeaders,
            requestBody,
            responseHeaders,
            responseBody);
    }

    /// <summary>
    /// Registra nei log i dettagli di una richiesta HTTP completata con successo:
    /// metodo, URL, header e body della richiesta e stato, header e body della risposta.
    /// </summary>
    private async Task LogRequestSuccessAsync(HttpRequestMessage request, HttpResponseMessage response)
    {
        var method = request.Method.Method;
        var url = request.RequestUri?.ToString() ?? "<unknown>";
        var requestHeaders = FormatHeaders(request.Headers, request.Content?.Headers);
        var requestBody = await FormatBodyAsync(request.Content).ConfigureAwait(false);
        var responseHeaders = FormatHeaders(response.Headers, response.Content.Headers);
        var responseBody = await FormatBodyAsync(response.Content).ConfigureAwait(false);

        _logger.LogInformation(
            Plugin.LogPrefix + "HTTP request completed with status {StatusCode} {ReasonPhrase}. Method: {Method}; URL: {Url}; Request headers: {RequestHeaders}; Request body: {RequestBody}; Response headers: {ResponseHeaders}; Response body: {ResponseBody}",
            (int)response.StatusCode,
            response.ReasonPhrase,
            method,
            url,
            requestHeaders,
            requestBody,
            responseHeaders,
            responseBody);
    }

    private async Task<IReadOnlyList<TraktTitleDto>> SendAsync(string relativeUrl, string? accessToken, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/{relativeUrl}");
            request.Headers.Add("trakt-api-version", "2");
            request.Headers.Add("trakt-api-key", TraktClientId);

            // Trakt sta dietro Cloudflare, che puo' rifiutare con 403 le richieste senza User-Agent.
            request.Headers.UserAgent.ParseAdd(UserAgent);

            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            HttpResponseMessage? response = null;
            try
            {
                response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt == 0)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2);
                    await Task.Delay(retryAfter, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    await LogRequestFailureAsync(request, response, exception: null).ConfigureAwait(false);
                    return Array.Empty<TraktTitleDto>();
                }

                await LogRequestSuccessAsync(request, response).ConfigureAwait(false);

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var result = await JsonSerializer.DeserializeAsync<List<TraktTitleDto>>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                return result ?? [];
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Annullata dal chiamante (es. l'utente ha lasciato la pagina): non e' un errore.
                _logger.LogDebug(Plugin.LogPrefix + "Request cancelled by the caller: {Url}", request.RequestUri);
                return Array.Empty<TraktTitleDto>();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                await LogRequestFailureAsync(request, response, ex).ConfigureAwait(false);
                return Array.Empty<TraktTitleDto>();
            }
            finally
            {
                response?.Dispose();
            }
        }

        return Array.Empty<TraktTitleDto>();
    }
}
