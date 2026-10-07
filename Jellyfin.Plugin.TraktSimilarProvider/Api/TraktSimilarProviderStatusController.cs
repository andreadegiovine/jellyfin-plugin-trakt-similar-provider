using System.Collections.Generic;
using System.Net.Mime;
using Jellyfin.Plugin.TraktSimilarProvider.Services;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.TraktSimilarProvider.Api;

/// <summary>
/// Espone lo stato della dipendenza dal plugin Trakt ufficiale e l'elenco degli utenti
/// Jellyfin collegati a un account Trakt, usati dalla pagina di configurazione.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("Plugins/TraktSimilarProvider")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class TraktSimilarProviderStatusController : ControllerBase
{
    private readonly ITraktPluginBridge _traktPluginBridge;
    private readonly IUserManager _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TraktSimilarProviderStatusController"/> class.
    /// </summary>
    /// <param name="traktPluginBridge">Instance of the <see cref="ITraktPluginBridge"/> interface.</param>
    /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
    public TraktSimilarProviderStatusController(ITraktPluginBridge traktPluginBridge, IUserManager userManager)
    {
        _traktPluginBridge = traktPluginBridge;
        _userManager = userManager;
    }

    /// <summary>
    /// Restituisce lo stato della dipendenza e l'elenco degli utenti collegati a Trakt.
    /// </summary>
    /// <response code="200">Stato restituito con successo.</response>
    /// <returns>Lo stato.</returns>
    [HttpGet("Status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<TraktSimilarProviderStatusDto> GetStatus()
    {
        var linkedIds = _traktPluginBridge.GetLinkedJellyfinUserIds();
        var linkedUsers = new List<LinkedUserDto>(linkedIds.Count);

        foreach (var id in linkedIds)
        {
            var user = _userManager.GetUserById(id);
            if (user is not null)
            {
                linkedUsers.Add(new LinkedUserDto { Id = user.Id, Name = user.Username });
            }
        }

        return new TraktSimilarProviderStatusDto
        {
            OfficialTraktPluginAvailable = _traktPluginBridge.IsOfficialTraktPluginAvailable(),
            LinkedUsers = linkedUsers
        };
    }
}
