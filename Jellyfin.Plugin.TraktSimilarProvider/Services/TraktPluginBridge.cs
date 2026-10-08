using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TraktSimilarProvider.Services;

/// <inheritdoc />
public sealed class TraktPluginBridge : ITraktPluginBridge
{
    private const string OfficialPluginTypeName = "Trakt.Plugin";

    private readonly ILogger<TraktPluginBridge> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TraktPluginBridge"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{TraktPluginBridge}"/> interface.</param>
    public TraktPluginBridge(ILogger<TraktPluginBridge> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsOfficialTraktPluginAvailable() => TryGetTraktUsers() is not null;

    /// <inheritdoc />
    public IReadOnlyList<Guid> GetLinkedJellyfinUserIds()
    {
        var result = new List<Guid>();

        foreach (var traktUser in TryGetTraktUsers() ?? Array.Empty<object>())
        {
            var linkedId = GetPropertyValue<Guid?>(traktUser, "LinkedMbUserId");
            if (linkedId is { } id && id != Guid.Empty)
            {
                result.Add(id);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public string? GetAccessToken(Guid jellyfinUserId)
    {
        foreach (var traktUser in TryGetTraktUsers() ?? Array.Empty<object>())
        {
            var linkedId = GetPropertyValue<Guid?>(traktUser, "LinkedMbUserId");
            if (linkedId != jellyfinUserId)
            {
                continue;
            }

            var token = GetPropertyValue<string>(traktUser, "AccessToken");
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }

        return null;
    }

    /// <summary>
    /// Trova l'assembly del plugin ufficiale già caricato nel processo, risale a
    /// <c>Trakt.Plugin.Instance.PluginConfiguration.TraktUsers</c> e lo restituisce come
    /// enumerazione debolmente tipizzata. Restituisce <c>null</c> se il plugin non è
    /// installato, non è attivo, o una qualunque delle proprietà attese non è presente
    /// (ad es. a seguito di un aggiornamento del plugin ufficiale che ne cambi la forma).
    /// </summary>
    private IEnumerable? TryGetTraktUsers()
    {
        try
        {
            var pluginType = FindOfficialPluginType();
            if (pluginType is null)
            {
                return null;
            }

            var instanceProperty = pluginType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var pluginInstance = instanceProperty?.GetValue(null);
            if (pluginInstance is null)
            {
                return null;
            }

            var configuration = GetPropertyValue<object>(pluginInstance, "PluginConfiguration");
            if (configuration is null)
            {
                return null;
            }

            return GetPropertyValue<IEnumerable>(configuration, "TraktUsers");
        }
        catch (Exception ex) when (ex is MemberAccessException or TargetException or AmbiguousMatchException)
        {
            _logger.LogWarning(ex, Plugin.LogPrefix + "Could not read the official Trakt plugin configuration (unexpected shape, probably an incompatible update of that plugin)");
            return null;
        }
    }

    private static Type? FindOfficialPluginType()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? type;
            try
            {
                type = assembly.GetType(OfficialPluginTypeName, throwOnError: false);
            }
            catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
            {
                continue;
            }

            if (type is not null)
            {
                return type;
            }
        }

        return null;
    }

    private static T? GetPropertyValue<T>(object source, string propertyName)
    {
        var property = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        var value = property?.GetValue(source);
        return value is T typed ? typed : default;
    }
}
