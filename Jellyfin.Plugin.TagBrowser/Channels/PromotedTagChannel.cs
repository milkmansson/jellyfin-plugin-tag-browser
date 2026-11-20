// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TagBrowser.Channels;

/// <summary>
/// A spare channel that shows one promoted branch of the tree. Jellyfin fixes its channel list at startup, so a fixed
/// number of these are registered; each stays hidden until a row in the settings assigns it a branch.
/// </summary>
public sealed class PromotedTagChannel : TagBrowserChannel
{
    private readonly int _slot;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromotedTagChannel"/> class.
    /// </summary>
    /// <param name="slot">The spare channel number, starting at 1.</param>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="logger">The logger.</param>
    public PromotedTagChannel(int slot, ILibraryManager libraryManager, IUserManager userManager, ILogger<TagBrowserChannel> logger)
        : base(libraryManager, userManager, logger, false)
    {
        _slot = slot;
    }

    /// <inheritdoc />
    public override string Name => Assigned?.Name ?? $"Tag Browser spare {_slot}";

    /// <inheritdoc />
    public override string Description => Assigned is { } plan ? $"{string.Join(" > ", plan.Root)}, from the Tag Browser." : "Unused spare channel.";

    /// <inheritdoc />
    protected override IReadOnlyList<string> RootNode => Assigned?.Root ?? Array.Empty<string>();

    private PromotedPlan? Assigned
    {
        get
        {
            var config = Plugin.Instance?.Configuration;
            if (config is null)
            {
                return null;
            }

            var plans = ChannelPlan.Promoted(config, config.Separator, MainChannelName);
            return _slot <= plans.Count ? plans[_slot - 1] : null;
        }
    }

    /// <inheritdoc />
    public override bool IsEnabledFor(string userId) => Assigned is not null;
}
