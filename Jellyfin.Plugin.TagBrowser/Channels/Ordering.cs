// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.TagBrowser.Channels;

/// <summary>
/// Sorting of folders and videos according to the plugin settings.
/// </summary>
public static class Ordering
{
    /// <summary>
    /// Sorts videos by the configured video order.
    /// </summary>
    /// <param name="items">The videos.</param>
    /// <param name="videoOrder">Title, PremiereDesc, PremiereAsc or DateAddedDesc.</param>
    /// <returns>The sorted videos.</returns>
    public static IEnumerable<BaseItem> SortVideos(IEnumerable<BaseItem> items, string videoOrder)
    {
        var byTitle = (Func<BaseItem, string>)(i => i.SortName ?? i.Name ?? string.Empty);
        return videoOrder switch
        {
            "PremiereDesc" => items.OrderByDescending(i => i.PremiereDate ?? i.DateCreated).ThenBy(byTitle, StringComparer.OrdinalIgnoreCase),
            "PremiereAsc" => items.OrderBy(i => i.PremiereDate ?? i.DateCreated).ThenBy(byTitle, StringComparer.OrdinalIgnoreCase),
            "DateAddedDesc" => items.OrderByDescending(i => i.DateCreated).ThenBy(byTitle, StringComparer.OrdinalIgnoreCase),
            _ => items.OrderBy(byTitle, StringComparer.OrdinalIgnoreCase),
        };
    }

    /// <summary>
    /// Parses the pinned-folders setting: one entry per line, each split on the separator.
    /// </summary>
    /// <param name="pinned">The raw setting.</param>
    /// <param name="separator">The tag separator.</param>
    /// <returns>The pinned entries, in order.</returns>
    public static IReadOnlyList<string[]> ParsePinned(string? pinned, string separator)
    {
        if (string.IsNullOrWhiteSpace(pinned))
        {
            return Array.Empty<string[]>();
        }

        return pinned
            .Split('\n')
            .Select(line => TagTree.Split(line, separator))
            .Where(p => p.Length > 0)
            .ToList();
    }

    /// <summary>
    /// Returns the position of a folder in the pinned list, or null if it is not pinned.
    /// A one-segment entry matches the folder name at any level; a longer entry must match the full path.
    /// </summary>
    /// <param name="folderPath">The folder's full path.</param>
    /// <param name="pinned">The pinned entries.</param>
    /// <returns>The pinned position, or null.</returns>
    public static int? PinnedIndex(IReadOnlyList<string> folderPath, IReadOnlyList<string[]> pinned)
    {
        for (var i = 0; i < pinned.Count; i++)
        {
            var entry = pinned[i];
            var matches = entry.Length == 1
                ? string.Equals(entry[0], folderPath[^1], StringComparison.OrdinalIgnoreCase)
                : entry.Length == folderPath.Count && TagTree.StartsWith(folderPath, entry);

            if (matches)
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>
    /// Sorts folders: pinned first (in list order), then by the configured folder order.
    /// </summary>
    /// <param name="folders">Folders as (full path, videos below it).</param>
    /// <param name="pinned">The pinned entries.</param>
    /// <param name="folderOrder">Alphabetical, MostVideos or RecentlyAdded.</param>
    /// <returns>The sorted folders.</returns>
    public static IEnumerable<(string[] Path, List<BaseItem> Videos)> SortFolders(
        IEnumerable<(string[] Path, List<BaseItem> Videos)> folders,
        IReadOnlyList<string[]> pinned,
        string folderOrder)
    {
        var withPin = folders.Select(f => (Folder: f, Pin: PinnedIndex(f.Path, pinned))).ToList();

        var pinnedFirst = withPin
            .Where(f => f.Pin.HasValue)
            .OrderBy(f => f.Pin)
            .Select(f => f.Folder);

        var rest = withPin.Where(f => !f.Pin.HasValue).Select(f => f.Folder);
        var name = (Func<(string[] Path, List<BaseItem> Videos), string>)(f => f.Path[^1]);
        rest = folderOrder switch
        {
            "MostVideos" => rest.OrderByDescending(f => f.Videos.Count).ThenBy(name, StringComparer.OrdinalIgnoreCase),
            "RecentlyAdded" => rest.OrderByDescending(f => f.Videos.Max(v => v.DateCreated)).ThenBy(name, StringComparer.OrdinalIgnoreCase),
            _ => rest.OrderBy(name, StringComparer.OrdinalIgnoreCase),
        };

        return pinnedFirst.Concat(rest);
    }
}
