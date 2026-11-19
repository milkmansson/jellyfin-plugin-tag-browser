// SPDX-License-Identifier: GPL-3.0-or-later
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TagBrowser.Configuration;

/// <summary>
/// Plugin settings.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the separator that splits a tag into tree levels, e.g. "move: cambre".
    /// </summary>
    public string Separator { get; set; } = ":";
}
