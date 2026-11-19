// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.TagBrowser.Channels;

/// <summary>
/// Turns flat tags into tree paths, and encodes/decodes tree paths as channel folder ids.
/// </summary>
public static class TagTree
{
    private const string FolderPrefix = "tag:";
    private const char PathSeparator = '\u001f';

    /// <summary>
    /// Splits a tag into trimmed, non-empty path segments.
    /// "move: cambre" with separator ":" gives ["move", "cambre"].
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <param name="separator">The level separator; empty means no splitting.</param>
    /// <returns>The path segments.</returns>
    public static string[] Split(string tag, string separator)
    {
        var parts = string.IsNullOrEmpty(separator)
            ? new[] { tag }
            : tag.Split(separator, StringSplitOptions.None);

        return parts.Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
    }

    /// <summary>
    /// Encodes a tree path as a channel folder id.
    /// </summary>
    /// <param name="path">The path segments.</param>
    /// <returns>The folder id.</returns>
    public static string ToFolderId(IReadOnlyList<string> path) => FolderPrefix + string.Join(PathSeparator, path);

    /// <summary>
    /// Decodes a channel folder id into a tree path. Null or empty means the root.
    /// </summary>
    /// <param name="folderId">The folder id.</param>
    /// <returns>The path segments.</returns>
    public static string[] FromFolderId(string? folderId)
    {
        if (string.IsNullOrEmpty(folderId) || !folderId.StartsWith(FolderPrefix, StringComparison.Ordinal))
        {
            return Array.Empty<string>();
        }

        var body = folderId.Substring(FolderPrefix.Length);
        return body.Length == 0 ? Array.Empty<string>() : body.Split(PathSeparator);
    }

    /// <summary>
    /// Returns true if <paramref name="path"/> starts with <paramref name="prefix"/> (case-insensitive).
    /// </summary>
    /// <param name="path">The full path.</param>
    /// <param name="prefix">The prefix path.</param>
    /// <returns>Whether the path starts with the prefix.</returns>
    public static bool StartsWith(IReadOnlyList<string> path, IReadOnlyList<string> prefix)
    {
        if (path.Count < prefix.Count)
        {
            return false;
        }

        for (var i = 0; i < prefix.Count; i++)
        {
            if (!string.Equals(path[i], prefix[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
