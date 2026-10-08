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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TraktSimilarProvider.Services;

/// <inheritdoc />
public sealed class TraktDiscoveryClient : ITraktDiscoveryClient
{
    /// <summary>
    /// Name of the named HTTP client registered in <c>PluginServiceRegistrator</c>.
    /// </summary>
    public const string HttpClientName = "TraktSimilarProviderClient";

    private const string BaseUrl = "https://api.trakt.tv";

    /// <summary>
    /// Client id of the Trakt application used by the official jellyfin-plugin-trakt (public
    /// value, copied from Trakt/Api/TraktURIs.cs in their open source repository). Trakt requires
    /// an application key on every request, even for public data.
    /// </summary>
    private const string TraktClientId = "bfdd2e032c30c35b368f97ef4ec81587b899bcb028b91a1d4ba5589a4b6a7267";

    /// <summary>
    /// Maximum length of a body (request or response) reported in the error logs.
    /// </summary>
    private const int MaxLoggedBodyLength = 2000;

    private static readonly string UserAgent = BuildUserAgent();

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<TraktDiscoveryClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TraktDiscoveryClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="memoryCache">Instance of the <see cref="IMemoryCache"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TraktDiscoveryClient}"/> interface.</param>
    public TraktDiscoveryClient(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, ILogger<TraktDiscoveryClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TraktTitleDto>> GetRelatedMoviesAsync(string imdbId, CancellationToken cancellationToken)
        => GetRelatedAsync("movies", imdbId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<TraktTitleDto>> GetRelatedShowsAsync(string imdbId, CancellationToken cancellationToken)
        => GetRelatedAsync("shows", imdbId, cancellationToken);

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
                parts.Add($"{header.Key}: {string.Join(',', header.Value)}");
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
    /// Gets the related titles of a movie or show, serving them from the shared in-memory cache
    /// when possible. Only successful responses are cached (an empty list returned by Trakt is a
    /// valid response and is cached too), so a failed request is retried on the next call.
    /// </summary>
    private async Task<IReadOnlyList<TraktTitleDto>> GetRelatedAsync(string kind, string imdbId, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(
            Plugin.Instance?.Configuration.MaxItemsPerRequest ?? PluginConfiguration.DefaultMaxItemsPerRequest,
            1,
            PluginConfiguration.DefaultMaxItemsPerRequest);

        // The limit is part of the key so that changing the setting is not served stale, shorter lists.
        var cacheKey = $"trakt-related:{kind}:{imdbId}:{limit}";
        if (_memoryCache.TryGetValue(cacheKey, out IReadOnlyList<TraktTitleDto>? cached) && cached is not null)
        {
            _logger.LogDebug(Plugin.LogPrefix + "Cache hit for related {Kind} of {ImdbId}", kind, imdbId);
            return cached;
        }

        var titles = await SendAsync($"{kind}/{Uri.EscapeDataString(imdbId)}/related?limit={limit}", cancellationToken).ConfigureAwait(false);
        if (titles is null)
        {
            return Array.Empty<TraktTitleDto>();
        }

        var hours = Math.Max(1, Plugin.Instance?.Configuration.CacheHours ?? PluginConfiguration.DefaultCacheHours);
        _memoryCache.Set(cacheKey, titles, TimeSpan.FromHours(hours));
        return titles;
    }

    /// <summary>
    /// Logs every detail useful to diagnose a failed HTTP request: method, URL, request headers
    /// and body and, when available, status, response headers and body.
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
    /// Logs the details of an HTTP request that completed successfully: method, URL, request
    /// headers and body, status, response headers and body.
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

    /// <summary>
    /// Sends a GET request to Trakt.
    /// </summary>
    /// <returns>The deserialized titles, or <c>null</c> if the request failed.</returns>
    private async Task<IReadOnlyList<TraktTitleDto>?> SendAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/{relativeUrl}");
            request.Headers.Add("trakt-api-version", "2");
            request.Headers.Add("trakt-api-key", TraktClientId);

            // Trakt is behind Cloudflare, which may reject requests without a User-Agent with a 403.
            request.Headers.UserAgent.ParseAdd(UserAgent);

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
                    return null;
                }

                await LogRequestSuccessAsync(request, response).ConfigureAwait(false);

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var result = await JsonSerializer.DeserializeAsync<List<TraktTitleDto>>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                return result ?? [];
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Cancelled by the caller (e.g. the user left the page): not an error.
                _logger.LogDebug(Plugin.LogPrefix + "Request cancelled by the caller: {Url}", request.RequestUri);
                return null;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                await LogRequestFailureAsync(request, response, ex).ConfigureAwait(false);
                return null;
            }
            finally
            {
                response?.Dispose();
            }
        }

        return null;
    }
}
