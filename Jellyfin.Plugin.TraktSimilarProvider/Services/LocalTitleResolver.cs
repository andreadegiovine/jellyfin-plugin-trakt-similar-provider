using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.TraktSimilarProvider.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.TraktSimilarProvider.Services;

/// <inheritdoc />
public sealed class LocalTitleResolver : ILocalTitleResolver
{
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalTitleResolver"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    public LocalTitleResolver(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    /// <inheritdoc />
    public IReadOnlyList<BaseItem> Resolve(IReadOnlyList<TraktTitleDto> titles, BaseItemKind kind, int limit)
    {
        var results = new List<BaseItem>(Math.Min(limit, titles.Count));

        foreach (var title in titles)
        {
            if (results.Count >= limit)
            {
                break;
            }

            var providerIds = BuildProviderIdSet(title);
            if (providerIds is null)
            {
                continue;
            }

            var query = new InternalItemsQuery
            {
                IncludeItemTypes = [kind],
                HasAnyProviderId = providerIds,
                Recursive = true,
                Limit = 1
            };

            var matches = _libraryManager.GetItemList(query);
            if (matches.Count > 0)
            {
                results.Add(matches[0]);
            }
        }

        return results;
    }

    private static Dictionary<string, string>? BuildProviderIdSet(TraktTitleDto title)
    {
        if (title.Ids is null || (title.Ids.Tmdb is null && string.IsNullOrWhiteSpace(title.Ids.Imdb)))
        {
            return null;
        }

        var set = new Dictionary<string, string>(2, StringComparer.OrdinalIgnoreCase);

        if (title.Ids.Tmdb is { } tmdbId)
        {
            set[MetadataProvider.Tmdb.ToString()] = tmdbId.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(title.Ids.Imdb))
        {
            set[MetadataProvider.Imdb.ToString()] = title.Ids.Imdb;
        }

        return set;
    }
}
