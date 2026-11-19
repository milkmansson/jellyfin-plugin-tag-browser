// SPDX-License-Identifier: GPL-3.0-or-later
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TagBrowser.Configuration;

/// <summary>
/// Plugin settings.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// The default channel name.
    /// </summary>
    public const string DefaultChannelName = "Tag Browser";

    /// <summary>
    /// Gets or sets the channel name shown in Jellyfin.
    /// Changing it creates a new channel (Jellyfin derives the channel id from its name).
    /// </summary>
    public string ChannelName { get; set; } = DefaultChannelName;

    /// <summary>
    /// Gets or sets the separator that splits a tag into tree levels, e.g. "move: cambre".
    /// Empty means tags are not split.
    /// </summary>
    public string Separator { get; set; } = ":";

    /// <summary>
    /// Gets or sets an optional server path to a custom primary (tile) image.
    /// </summary>
    public string PrimaryImagePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional server path to a custom thumb (wide tile) image.
    /// </summary>
    public string ThumbImagePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional server path to a custom backdrop (background) image.
    /// </summary>
    public string BackdropImagePath { get; set; } = string.Empty;
}
