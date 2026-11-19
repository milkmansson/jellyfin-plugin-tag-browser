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

    /// <summary>
    /// Gets or sets folder names to show first, one per line, in order.
    /// A plain name matches at any level; a full path ("move: cambre") matches only that folder.
    /// </summary>
    public string PinnedFolders { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the order of folders that are not pinned: Alphabetical, MostVideos or RecentlyAdded.
    /// </summary>
    public string FolderOrder { get; set; } = "Alphabetical";

    /// <summary>
    /// Gets or sets which video provides a folder thumbnail: Random (stable per folder), Title, PremiereDesc, PremiereAsc or DateAddedDesc.
    /// Also decides which video provides a folder's image.
    /// </summary>
    public string VideoOrder { get; set; } = "Random";

    /// <summary>
    /// Gets or sets the folder image: FirstVideo (thumbnail of the first video, falling back to the logo), Logo or None.
    /// </summary>
    public string FolderImage { get; set; } = "FirstVideo";

    /// <summary>
    /// Gets or sets the libraries to include (comma-separated library item ids). Empty = all libraries.
    /// </summary>
    public string Libraries { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the Tags branch is shown.</summary>
    public bool BranchTags { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether tags are shown at the top level, alongside the other branches.
    /// When off, tags are grouped under the Tags branch folder.
    /// </summary>
    public bool TagsAtTopLevel { get; set; } = true;

    /// <summary>Gets or sets the Tags branch name (used when tags are not at the top level).</summary>
    public string BranchTagsName { get; set; } = "Tags";

    /// <summary>Gets or sets a value indicating whether the Date branch is shown.</summary>
    public bool BranchDate { get; set; } = true;

    /// <summary>Gets or sets the Date branch name.</summary>
    public string BranchDateName { get; set; } = "Date";

    /// <summary>Gets or sets the Date branch depth: Year, Month or Day.</summary>
    public string DateDepth { get; set; } = "Day";

    /// <summary>Gets or sets a value indicating whether the library Folders branch is shown.</summary>
    public bool BranchFolders { get; set; }

    /// <summary>Gets or sets the Folders branch name.</summary>
    public string BranchFoldersName { get; set; } = "Folders";

    /// <summary>Gets or sets a value indicating whether the Genres branch is shown.</summary>
    public bool BranchGenres { get; set; }

    /// <summary>Gets or sets the Genres branch name.</summary>
    public string BranchGenresName { get; set; } = "Genres";

    /// <summary>Gets or sets a value indicating whether the Studios branch is shown.</summary>
    public bool BranchStudios { get; set; }

    /// <summary>Gets or sets the Studios branch name.</summary>
    public string BranchStudiosName { get; set; } = "Studios";

    /// <summary>Gets or sets a value indicating whether the People branch is shown.</summary>
    public bool BranchPeople { get; set; }

    /// <summary>Gets or sets the People branch name.</summary>
    public string BranchPeopleName { get; set; } = "People";

    /// <summary>Gets or sets a value indicating whether a date in the file name is used when the video has no date.</summary>
    public bool DateFromFileName { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether a date in the file name is preferred over the time recorded in the file.
    /// A date-only &lt;premiered&gt; still wins.
    /// </summary>
    public bool PreferFileNameDate { get; set; }

    /// <summary>Gets or sets the Date branch folder for videos without any date. Empty hides them.</summary>
    public string UndatedName { get; set; } = "Undated";
}
