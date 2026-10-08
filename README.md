# Trakt Similar Provider

A Jellyfin **12** plugin that adds **Trakt** as a native provider for:

- **Similar items** – the "More like this" row on movie and show pages. "Trakt" shows up in
  *Dashboard → Libraries → (your library) → Similar item providers*, next to *TheMovieDb* and
  *Local Genre/Tag*.
- **Suggestions** – the *Suggestions* tab of your library, filled with Trakt's personalised
  recommendations for the Trakt account you pick.

Nothing is ever added to your library: every title suggested by Trakt is matched against what
you actually own, and titles you don't have are simply dropped.

## Requirements

- Jellyfin **12.0 or newer** (built against 12.1).
- For **Similar items**: nothing else. It uses public Trakt data and does not need a Trakt account.
- For **Suggestions**: the official
  [Trakt plugin](https://github.com/jellyfin/jellyfin-plugin-trakt) installed, with at least one
  Jellyfin user linked to a Trakt account. This plugin reuses the link and the access token stored by
  that plugin; it never asks you for Trakt credentials.

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

### 1. Link your Trakt account (needed for Suggestions only)

Install the official Trakt plugin, open its settings and link the Jellyfin user to a Trakt account.

### 2. Configure this plugin

Open **Dashboard → Plugins → Trakt Similar Provider**.

| Setting | What it does |
|---|---|
| **Trakt user for personalised Suggestions** | The Jellyfin user whose linked Trakt account is used for the *Suggestions* tab. The list only contains users linked in the official Trakt plugin. Leave it on *None* to turn Suggestions off. |
| **Streaming availability filter (watchnow)** | Asks Trakt to return only titles available on streaming services: your favourite services, any service in your country, any service in any country, or free ones. *None* disables the filter. |
| **"Similar" cache (days)** | How long Jellyfin keeps the "More like this" results for each item. `0` disables caching. |
| **Suggestions cache (hours)** | How long this plugin keeps Trakt recommendations in memory before asking Trakt again. |

The page also shows whether the official Trakt plugin was detected.

### 3. Enable the provider for "More like this"

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
- **Suggestions repeat the same list.** Trakt has no "similar to this title" recommendation, only
  one personalised list per user. Every row of the *Suggestions* tab therefore shows the same titles.
- **The Suggestions tab is replaced, not extended.** Jellyfin uses a single provider for that tab,
  and this plugin takes precedence over the built-in *Local Genre/Tag* one. If no Trakt user is
  selected, the official Trakt plugin is missing, or the token is not valid, the tab will be empty
  until you fix it. To get the built-in suggestions back, uninstall this plugin.
- **Only titles in your library are shown.** For Suggestions the plugin also asks Trakt to leave
  out titles you have already watched.
- **Shared Trakt application.** Trakt requires an application key on every request, even for
  public data, and new Trakt applications now require a VIP subscription. This plugin therefore
  reuses the public key of the official Jellyfin Trakt plugin, so its requests share that
  application's rate limit.
- **Compatibility with the official Trakt plugin.** The link between Jellyfin users and Trakt
  accounts is read from the official plugin while Jellyfin is running. If a future update of that
  plugin changes its internals, Suggestions will stop working until this plugin is updated;
  "More like this" keeps working. A warning is written to the log.

## Troubleshooting

Every log line from this plugin starts with `Trakt Similar Provider:`, so you can filter the
Jellyfin log (**Dashboard → Logs**) on that text.

When a request to Trakt fails, the plugin logs a warning with everything needed to understand
it: the HTTP method, URL, request headers, request body, and the response status, headers and
body. Your Trakt access token is never written to the log (the `Authorization` header is shown
as `<redacted>`).

| Symptom | Likely cause |
|---|---|
| `HTTP request failed with status 403` | Trakt (or Cloudflare in front of it) refused the request. Check the response body and headers in the log line. |
| `HTTP request failed with status 401` | The Trakt token of the selected user is expired or revoked. Open the official Trakt plugin and re-link the account. |
| `HTTP request failed with status 429` | Too many requests. The plugin retries once and then gives up for that request. |
| No "More like this" results from Trakt | The item has no IMDb id, or none of the titles Trakt suggests are in your library. |
| Empty *Suggestions* tab | No Trakt user selected, official Trakt plugin missing or not linked, or an expired token. |
| `Could not read the official Trakt plugin configuration` | The official Trakt plugin changed in a way this plugin does not understand yet. |

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
