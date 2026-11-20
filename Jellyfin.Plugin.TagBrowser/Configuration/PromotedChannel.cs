// SPDX-License-Identifier: GPL-3.0-or-later
namespace Jellyfin.Plugin.TagBrowser.Configuration;

/// <summary>
/// One branch of the tree promoted to a channel of its own.
/// </summary>
public class PromotedChannel
{
    /// <summary>Gets or sets the branch to promote, as a path using the tag separator, e.g. "dance" or "dance: zouk".</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Gets or sets the channel name. Empty = the last part of the path.</summary>
    public string Name { get; set; } = string.Empty;
}
