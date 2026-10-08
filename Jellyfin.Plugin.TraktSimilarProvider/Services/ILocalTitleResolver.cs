using System;
using System.Collections.Generic;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.TraktSimilarProvider.Dto;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.TraktSimilarProvider.Services;

/// <summary>
/// Resolves Trakt titles (used by the Suggestions provider, which must return resolved
/// <see cref="BaseItem"/> instances rather than references) against the local library, by Tmdb id
/// or, failing that, Imdb id. A title that is not owned is discarded: no virtual item is ever created.
/// </summary>
public interface ILocalTitleResolver
{
    /// <summary>
    /// Resolves a list of Trakt titles into existing local items, preserving the original order.
    /// </summary>
    /// <param name="titles">The Trakt titles to resolve.</param>
    /// <param name="kind">The kind of item to look for (Movie or Series).</param>
    /// <param name="limit">Maximum number of items to return.</param>
    /// <param name="user">The user the suggestions are for, if any. When set, items the user has already played are skipped.</param>
    /// <param name="excludeItemIds">Ids of items that must not be returned (e.g. the source item).</param>
    /// <returns>The local items found.</returns>
    IReadOnlyList<BaseItem> Resolve(IReadOnlyList<TraktTitleDto> titles, BaseItemKind kind, int limit, User? user, IReadOnlyCollection<Guid> excludeItemIds);
}
