// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.TagBrowser.Configuration;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.TagBrowser.Channels;

/// <summary>
/// Computes the tree paths an item appears under, for each enabled branch.
/// </summary>
public sealed class Branches
{
    private readonly PluginConfiguration _config;
    private readonly IReadOnlyList<(string Name, string Location)> _libraryLocations;
    private readonly Func<BaseItem, IEnumerable<string>> _people;

    /// <summary>
    /// Initializes a new instance of the <see cref="Branches"/> class.
    /// </summary>
    /// <param name="config">The plugin configuration.</param>
    /// <param name="libraryLocations">Included libraries as (name, root folder) pairs.</param>
    /// <param name="people">Returns the people of an item (only called when the People branch is enabled).</param>
    public Branches(PluginConfiguration config, IReadOnlyList<(string Name, string Location)> libraryLocations, Func<BaseItem, IEnumerable<string>> people)
    {
        _config = config;
        _libraryLocations = libraryLocations;
        _people = people;
    }

    /// <summary>
    /// Gets a value indicating whether only the Tags branch is enabled; tags are then shown at the top level.
    /// </summary>
    public bool TagsOnly =>
        _config.BranchTags && !_config.BranchDate && !_config.BranchFolders
        && !_config.BranchGenres && !_config.BranchStudios && !_config.BranchPeople;

    /// <summary>
    /// Returns every tree path the item appears under. The last segment of a path is the folder that lists the item.
    /// </summary>
    /// <param name="item">The library item.</param>
    /// <returns>The paths.</returns>
    public IEnumerable<string[]> PathsFor(BaseItem item)
    {
        if (_config.BranchTags)
        {
            foreach (var tag in item.Tags)
            {
                var path = TagTree.Split(tag, _config.Separator);
                if (path.Length > 0)
                {
                    yield return TagsOnly ? path : Prefix(Name(_config.BranchTagsName, "Tags"), path);
                }
            }
        }

        if (_config.BranchDate && DatePath(item) is { } date)
        {
            yield return Prefix(Name(_config.BranchDateName, "Date"), date);
        }

        if (_config.BranchFolders && FolderPath(item) is { } folder)
        {
            yield return Prefix(Name(_config.BranchFoldersName, "Folders"), folder);
        }

        if (_config.BranchGenres)
        {
            foreach (var genre in item.Genres.Where(g => !string.IsNullOrWhiteSpace(g)))
            {
                yield return new[] { Name(_config.BranchGenresName, "Genres"), genre.Trim() };
            }
        }

        if (_config.BranchStudios)
        {
            foreach (var studio in item.Studios.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                yield return new[] { Name(_config.BranchStudiosName, "Studios"), studio.Trim() };
            }
        }

        if (_config.BranchPeople)
        {
            foreach (var person in _people(item).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                yield return new[] { Name(_config.BranchPeopleName, "People"), person.Trim() };
            }
        }
    }

    /// <summary>
    /// Date path in the server's time zone: ["2022", "2022-02 February", "2022-02-13"], cut to the configured depth.
    /// Dates without a time of day (midnight UTC, e.g. from &lt;premiered&gt;) are used as-is.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The date path, or null if the item has no date.</returns>
    public string[]? DatePath(BaseItem item)
    {
        if (item.PremiereDate is not { } date)
        {
            return item.ProductionYear is { } year ? new[] { year.ToString(CultureInfo.InvariantCulture) } : null;
        }

        if (date.TimeOfDay != TimeSpan.Zero)
        {
            var utc = date.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : date.ToUniversalTime();
            date = TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.Local);
        }

        var parts = new List<string> { date.ToString("yyyy", CultureInfo.InvariantCulture) };
        if (_config.DateDepth is "Month" or "Day")
        {
            parts.Add(date.ToString("yyyy-MM MMMM", CultureInfo.InvariantCulture));
        }

        if (_config.DateDepth is "Day")
        {
            parts.Add(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        return parts.ToArray();
    }

    /// <summary>
    /// Folder path of the item relative to its library root. Prefixed with the library name when several libraries are included.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The folder segments (empty for items at the library root), or null if the item is in no included library.</returns>
    public string[]? FolderPath(BaseItem item)
    {
        var dir = Path.GetDirectoryName(item.Path ?? string.Empty) ?? string.Empty;
        foreach (var (name, location) in _libraryLocations)
        {
            var root = location.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!(dir + Path.DirectorySeparatorChar).StartsWith(root, StringComparison.Ordinal))
            {
                continue;
            }

            var rel = dir.Length > root.Length - 1 ? dir.Substring(root.Length - 1).Trim(Path.DirectorySeparatorChar) : string.Empty;
            var segments = rel.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            var multiple = _libraryLocations.Select(l => l.Name).Distinct().Count() > 1;
            return multiple ? Prefix(name, segments) : segments;
        }

        return null;
    }

    private static string Name(string? configured, string fallback) =>
        string.IsNullOrWhiteSpace(configured) ? fallback : configured.Trim();

    private static string[] Prefix(string first, string[] rest)
    {
        var result = new string[rest.Length + 1];
        result[0] = first;
        rest.CopyTo(result, 1);
        return result;
    }
}
