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
                    yield return _config.TagsAtTopLevel ? path : Prefix(Name(_config.BranchTagsName, "Tags"), path);
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
    /// Date path: ["2022", "2022-02 February", "2022-02-13"], cut to the configured depth.
    /// Order: date-only value (e.g. &lt;premiered&gt;) as written; then the time recorded in the file, in the
    /// server's time zone (or the file-name date if preferred); then the file-name date; then the year;
    /// otherwise the "Undated" folder.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The date path, or null if the item has no date.</returns>
    public string[]? DatePath(BaseItem item)
    {
        var fromName = _config.DateFromFileName || _config.PreferFileNameDate ? DateFromFileName(item.Path) : null;

        DateTime? date = null;
        if (item.PremiereDate is { } premiere)
        {
            if (premiere.TimeOfDay == TimeSpan.Zero)
            {
                // Date only (e.g. <premiered>): use as written, never shifted.
                date = premiere.Date;
            }
            else if (_config.PreferFileNameDate && fromName is not null)
            {
                // Time recorded in the file is UTC; the file name holds the camera's local date.
                date = fromName;
            }
            else
            {
                var utc = premiere.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(premiere, DateTimeKind.Utc) : premiere.ToUniversalTime();
                date = TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.Local);
            }
        }
        else if (_config.DateFromFileName && fromName is not null)
        {
            date = fromName;
        }

        if (date is not { } d)
        {
            if (item.ProductionYear is { } year)
            {
                return new[] { year.ToString(CultureInfo.InvariantCulture) };
            }

            return string.IsNullOrWhiteSpace(_config.UndatedName) ? null : new[] { _config.UndatedName.Trim() };
        }

        var parts = new List<string> { d.ToString("yyyy", CultureInfo.InvariantCulture) };
        if (_config.DateDepth is "Month" or "Day")
        {
            parts.Add(d.ToString("yyyy-MM MMMM", CultureInfo.InvariantCulture));
        }

        if (_config.DateDepth is "Day")
        {
            parts.Add(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        return parts.ToArray();
    }

    /// <summary>
    /// Finds a date in a file name, e.g. 20220213_180246, VID_20251203_210934, VID-20250924-WA0005, 2022-03-20.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The date, or null if the name contains no valid date.</returns>
    public static DateTime? DateFromFileName(string? path)
    {
        var name = Path.GetFileNameWithoutExtension(path ?? string.Empty);
        foreach (System.Text.RegularExpressions.Match m in FileNameDate.Matches(name))
        {
            if (DateTime.TryParseExact(
                    m.Groups["y"].Value + m.Groups["m"].Value + m.Groups["d"].Value,
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date)
                && date.Year >= 1990 && date <= DateTime.Today.AddDays(1))
            {
                return date;
            }
        }

        return null;
    }

    private static readonly System.Text.RegularExpressions.Regex FileNameDate =
        new(@"(?<!\d)(?<y>(19|20)\d{2})[-_.]?(?<m>\d{2})[-_.]?(?<d>\d{2})(?!\d)", System.Text.RegularExpressions.RegexOptions.Compiled);

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
