// SPDX-License-Identifier: GPL-3.0-or-later
namespace Jellyfin.Plugin.TagBrowser.Configuration;

/// <summary>
/// One configured property branch.
/// </summary>
public class PropertyBranch
{
    /// <summary>Gets or sets the property to group by, e.g. studio, actor, year (case-insensitive).</summary>
    public string Property { get; set; } = string.Empty;

    /// <summary>Gets or sets the folder name shown in the channel. Empty = the property name.</summary>
    public string DisplayName { get; set; } = string.Empty;
}
