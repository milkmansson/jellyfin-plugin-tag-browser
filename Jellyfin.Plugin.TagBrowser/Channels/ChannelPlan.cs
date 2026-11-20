// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.TagBrowser.Configuration;

namespace Jellyfin.Plugin.TagBrowser.Channels;

/// <summary>
/// Works out which branches become channels of their own, and which paths therefore move out of other channels.
/// </summary>
public static class ChannelPlan
{
    /// <summary>
    /// The number of spare channels registered with Jellyfin, and so the most branches that can be promoted.
    /// </summary>
    public const int MaxPromoted = 10;

    /// <summary>
    /// The promoted channels in the settings, in order. Rows without a path, with a name already used (including
    /// the main channel's) or repeating a path are ignored; at most <see cref="MaxPromoted"/> are kept.
    /// </summary>
    /// <param name="config">The plugin configuration.</param>
    /// <param name="separator">The tag level separator.</param>
    /// <param name="mainName">The main channel's name.</param>
    /// <returns>The promoted channels.</returns>
    public static IReadOnlyList<PromotedPlan> Promoted(PluginConfiguration config, string separator, string mainName)
    {
        var result = new List<PromotedPlan>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { mainName };
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in config.PromotedChannels ?? Array.Empty<PromotedChannel>())
        {
            var root = TagTree.Split(row?.Path ?? string.Empty, separator);
            if (root.Length == 0)
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(row!.Name) ? root[^1] : row.Name.Trim();
            if (!names.Add(name) || !roots.Add(TagTree.ToFolderId(root)))
            {
                continue;
            }

            result.Add(new PromotedPlan(name, root));
            if (result.Count == MaxPromoted)
            {
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Whether a path belongs to a promoted channel other than the one opening at <paramref name="root"/>, and so is
    /// not listed there. The main channel has an empty root. A promoted branch is moved, not copied: it only appears
    /// in its own channel, and a branch promoted inside another one leaves that one too.
    /// </summary>
    /// <param name="path">The tree path of a video placement.</param>
    /// <param name="root">The root of the channel being listed.</param>
    /// <param name="promotedRoots">The roots of all promoted channels.</param>
    /// <returns>True if the path is moved out of this channel.</returns>
    public static bool IsMovedOut(IReadOnlyList<string> path, IReadOnlyList<string> root, IEnumerable<IReadOnlyList<string>> promotedRoots)
        => promotedRoots.Any(p => !(p.Count == root.Count && TagTree.StartsWith(p, root))
            && TagTree.StartsWith(path, p)
            && !TagTree.StartsWith(root, p));
}
