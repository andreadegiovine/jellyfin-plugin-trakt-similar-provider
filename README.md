# Trakt Similar Provider

A Jellyfin **12** plugin that adds **Trakt** as a native provider for:

- **Similar items** – the "More like this" row on movie and show pages. "Trakt" shows up in
  *Dashboard → Libraries → (your library) → Similar item providers*, next to *TheMovieDb* and
  *Local Genre/Tag*.
- **Suggestions** – the *Suggestions* tab of your library: the "Because you watched X" and
  "Because you liked X" rows, filled with the titles Trakt relates to each of those items.

Both features use the same public Trakt data (`/movies/{id}/related` and `/shows/{id}/related`),
need no Trakt account, and share a single cache. Nothing is ever added to your library: every
title suggested by Trakt is matched against what you actually own, and titles you don't have
are simply dropped.

## Requirements

- Jellyfin **12.0 or newer** (built against 12.1).
- Nothing else: the plugin uses public Trakt data, so no Trakt account and no other plugin
  (including the official Trakt plugin) is needed.

## Installation

### From the plugin repository (recommended)

1. In Jellyfin go to **Dashboard → Plugins → Repositories** and click **+**.
2. Enter a name (e.g. `Trakt Similar Provider`) and this URL:

   ```
   https://andreadegiovine.github.io/jellyfin-plugin-trakt-similar-provider/repository.json
   ```

3. Open **Dashboard → Plugins → Catalog**, find **Trakt Similar Provider** (category *General*)
   and click **Install**.
4. **Restart Jellyfin.**

### Manual installation

1. Download the latest `trakt-similar-provider_<version>.zip` from the
   [Releases](https://github.com/andreadegiovine/jellyfin-plugin-trakt-similar-provider/releases) page.
2. Extract it into a new folder inside your Jellyfin `plugins` directory, for example
   `<jellyfin-config>/plugins/Trakt Similar Provider_0.1.0.0/`.
3. **Restart Jellyfin.**

## Setup

### 1. Configure this plugin (optional)

Open **Dashboard → Plugins → Trakt Similar Provider** (there is also a shortcut in the dashboard
sidebar). The defaults work out of the box.

| Setting | What it does |
|---|---|
| **Cache duration (hours)** | How long Trakt responses are kept in memory. The same cache serves *Similar items* and *Suggestions*, so each title is requested from Trakt at most once per interval. |
| **Maximum titles per request** | Upper limit of titles handled per request. |

### 2. Enable the provider for "More like this"

1. Go to **Dashboard → Libraries** and edit a movie or show library.
2. In **Similar item providers**, tick **Trakt**. Drag it up or down to set its priority compared
   with the other providers.
3. Save. Repeat for each library you want.

The *Suggestions* tab does not need to be enabled per library: once the plugin is installed it
serves that tab automatically (see the notes below).

## Things worth knowing

- **Trakt only understands IMDb ids here.** Trakt's per-title endpoints accept a Trakt id, a slug
  or an IMDb id, but not a TMDb id. A movie or show without an IMDb id in its metadata gets no
  "More like this" results from Trakt. Refreshing metadata from a provider that fills in IMDb ids
  usually fixes it.
- **Suggestions are built per source item.** Jellyfin passes the recently watched and the liked
  items to the plugin; for each one the plugin asks Trakt for its related titles, so every
  "Because you watched X" / "Because you liked X" row is specific to X. Titles you have already
  watched are left out.
- **Suggestions are movies only, for now.** Jellyfin currently builds the *Suggestions* tab for
  movies only. Shows are supported by the plugin (Similar items work for them today, and show
  suggestions will work as soon as Jellyfin asks for them).
- **The Suggestions tab is replaced, not extended.** Jellyfin uses a single provider for that tab,
  and this plugin takes precedence over the built-in *Local Genre/Tag* one. To get the built-in
  suggestions back, uninstall this plugin.
- **Only titles in your library are shown.**
- **Shared Trakt application.** Trakt requires an application key on every request, even for
  public data, and new Trakt applications now require a VIP subscription. This plugin therefore
  reuses the public key of the official Jellyfin Trakt plugin, so its requests share that
  application's rate limit.

## Troubleshooting

Every log line from this plugin starts with `Trakt Similar Provider:`, so you can filter the
Jellyfin log (**Dashboard → Logs**) on that text.

When a request to Trakt fails, the plugin logs a warning with everything needed to understand
it: the HTTP method, URL, request headers, request body, and the response status, headers and
body.

| Symptom | Likely cause |
|---|---|
| `HTTP request failed with status 403` | Trakt (or Cloudflare in front of it) refused the request. Check the response body and headers in the log line. |
| `HTTP request failed with status 429` | Too many requests. The plugin retries once and then gives up for that request. |
| No "More like this" results from Trakt | The item has no IMDb id, or none of the titles Trakt suggests are in your library. |
| Empty *Suggestions* tab | You have not watched or liked any movie yet, the source movies have no IMDb id, or none of the related titles are in your library (or you have already watched them all). |

If you open an issue, please include the log lines starting with `Trakt Similar Provider:`.

## Building from source

Requires the .NET 10 SDK.

```bash
dotnet restore
dotnet build -c Release
```

The output is `Jellyfin.Plugin.TraktSimilarProvider/bin/Release/net10.0/Jellyfin.Plugin.TraktSimilarProvider.dll`.

## Releasing

Publishing a GitHub Release runs `.github/workflows/publish.yaml`, which builds the plugin,
uploads the zip with its checksums to the release, and regenerates the plugin repository
manifest (`repository.json`) on the `gh-pages` branch.

## License

Apache License 2.0, see [LICENSE](LICENSE).
