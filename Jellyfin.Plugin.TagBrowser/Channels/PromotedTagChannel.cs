// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
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

    /// <summary>
    /// The channel's tile is a thumbnail of one of its videos, chosen by the folder image settings; the logo
    /// (or the custom image) when the settings say so or no video has a thumbnail.
    /// </summary>
    /// <param name="type">The image type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The image.</returns>
    public override Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
    {
        if (type is ImageType.Primary or ImageType.Thumb && TilePath() is { } path)
        {
            return Task.FromResult(new DynamicImageResponse
            {
                HasImage = true,
                Path = path,
                Protocol = MediaProtocol.File,
                Format = System.IO.Path.GetExtension(path).ToLowerInvariant() switch
                {
                    ".jpg" or ".jpeg" => ImageFormat.Jpg,
                    ".webp" => ImageFormat.Webp,
                    ".gif" => ImageFormat.Gif,
                    _ => ImageFormat.Png,
                },
            });
        }

        return base.GetChannelImage(type, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        var result = await base.GetChannelItems(query, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(query.FolderId) && TilePath() is { } path)
        {
            await SyncChannelTileAsync(path, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// The thumbnail of one of the channel's videos, or null when the settings ask for the logo or none has one.
    /// </summary>
    private string? TilePath()
    {
        var config = Plugin.Instance?.Configuration;
        if (Assigned is not { } plan || config is null)
        {
            return null;
        }

        var path = FolderImagePath(VideosUnder(plan.Root), TagTree.ToFolderId(plan.Root), config.VideoOrder, config.FolderImage);
        return path is not null && File.Exists(path) && !string.Equals(path, Plugin.Instance?.GetFolderLogoPath(), StringComparison.Ordinal)
            ? path
            : null;
    }

    /// <inheritdoc />
    public override bool IsEnabledFor(string userId) => Assigned is not null;
}
