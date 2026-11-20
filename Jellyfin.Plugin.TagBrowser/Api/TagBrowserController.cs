// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using Jellyfin.Plugin.TagBrowser.Channels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.TagBrowser.Api;

/// <summary>
/// Endpoints used by the plugin's settings page.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("TagBrowser")]
public class TagBrowserController : ControllerBase
{
    /// <summary>
    /// Gets the property names to suggest in the settings page.
    /// </summary>
    /// <returns>The names.</returns>
    [HttpGet("Properties")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> GetProperties() => Ok(Branches.AllSuggestions());
}
