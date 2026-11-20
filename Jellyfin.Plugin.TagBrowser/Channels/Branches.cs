// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.TagBrowser.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.TagBrowser.Channels;

/// <summary>
/// Computes the tree paths an item appears under, for each enabled branch.
/// </summary>
public sealed class Branches
{
    private static readonly System.Text.RegularExpressions.Regex FileNameDate =
        new(@"(?<!\d)(?<y>(19|20)\d{2})[-_.]?(?<m>\d{2})[-_.]?(?<d>\d{2})(?!\d)", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Property names listed first in the settings page suggestions (any other readable item property also works).
    /// </summary>
    public static readonly IReadOnlyList<string> SuggestedProperties = new[]
    {
        "genres", "studios", "productionYear", "officialRating", "productionLocations",
        "person.actor", "person.director", "person.writer", "person.producer", "person.composer", "person.guestStar",
        "originalTitle", "container", "videoType", "width", "height", "communityRating", "criticRating",
        "preferredMetadataLanguage", "preferredMetadataCountryCode", "isHD", "is3D", "isLocked", "dateCreated", "premiereDate",
        "video.codec", "video.height", "video.width", "audio.codec", "audio.language", "audio.channels", "subtitle.language",
    };

    private static readonly HashSet<string> PersonKinds = new(Enum.GetNames<Jellyfin.Data.Enums.PersonKind>().Select(n => n.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);

    private readonly PluginConfiguration _config;
    private readonly IReadOnlyList<(string Name, string Location)> _libraryLocations;
    private readonly Func<BaseItem, IEnumerable<(string Name, string Kind)>> _people;
    private readonly Func<BaseItem, IEnumerable<BaseItem>> _parents;
    private readonly Func<BaseItem, IReadOnlyList<MediaStream>> _streams;
    private readonly Func<BaseItem, string, Func<string[]>, string[]> _memo;

    /// <summary>
    /// Initializes a new instance of the <see cref="Branches"/> class.
    /// </summary>
    /// <param name="config">The plugin configuration.</param>
    /// <param name="libraryLocations">Included libraries as (name, root folder) pairs.</param>
    /// <param name="parents">Returns the items an item inherits metadata from (an episode's series); none by default.</param>
    /// <param name="streams">Returns the media streams (video, audio, subtitles) of an item; none by default.</param>
    /// <param name="memo">Remembers a computed stream value list per item and name (the lookup is costly); computes every time by default.</param>
    /// <param name="people">Returns the people of an item as (name, role) pairs (only called when a person property is configured).</param>
    public Branches(
        PluginConfiguration config,
        IReadOnlyList<(string Name, string Location)> libraryLocations,
        Func<BaseItem, IEnumerable<(string Name, string Kind)>> people,
        Func<BaseItem, IEnumerable<BaseItem>>? parents = null,
        Func<BaseItem, IReadOnlyList<MediaStream>>? streams = null,
        Func<BaseItem, string, Func<string[]>, string[]>? memo = null)
    {
        _config = config;
        _libraryLocations = libraryLocations;
        _people = people;
        _parents = parents ?? (_ => Array.Empty<BaseItem>());
        _streams = streams ?? (_ => Array.Empty<MediaStream>());
        _memo = memo ?? ((_, _, compute) => compute());
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

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var branch in _config.PropertyBranches ?? Array.Empty<PropertyBranch>())
        {
            var property = branch?.Property?.Trim();
            if (string.IsNullOrEmpty(property) || !seen.Add(property))
            {
                continue;
            }

            var values = PropertyValues(item, property);
            if (values is null)
            {
                continue;
            }

            var name = Name(branch!.DisplayName, char.ToUpperInvariant(property[0]) + property.Substring(1));
            var distinct = values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var value in distinct)
            {
                yield return new[] { name, value };
            }

            if (distinct.Count == 0 && !string.IsNullOrWhiteSpace(_config.UnknownName))
            {
                yield return new[] { name, _config.UnknownName.Trim() };
            }
        }
    }

    /// <summary>
    /// Values of a named property, or null when the name is unknown. Names are case-insensitive and are Jellyfin's own:
    /// an item property (Genres, Studios, ProductionYear, ...), person.&lt;role&gt; (person.actor, person.director, ...) or
    /// video./audio./subtitle.&lt;stream property&gt;.
    /// An episode also gets its series' values (Jellyfin keeps genres, studios, countries and people on the series),
    /// except ProductionYear and OfficialRating, which come from the series only when the episode has none.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="property">The property name.</param>
    /// <returns>The values (possibly none), or null for an unknown property.</returns>
    public IEnumerable<string>? PropertyValues(BaseItem item, string property)
    {
        var key = property.Trim().ToLowerInvariant();
        var own = OwnValues(item, key);
        if (own is null)
        {
            return null;
        }

        var fallbackOnly = key is "productionyear" or "officialrating";
        var values = own.ToList();
        if (fallbackOnly && values.Count > 0)
        {
            return values;
        }

        foreach (var parent in _parents(item))
        {
            values.AddRange(OwnValues(parent, key) ?? Array.Empty<string>());
            if (fallbackOnly && values.Count > 0)
            {
                break;
            }
        }

        return values;
    }

    private IEnumerable<string>? OwnValues(BaseItem item, string key)
    {
        if (StreamName(key) is var (streamKind, streamProp))
        {
            return StreamValues(item, streamKind, streamProp);
        }

        if (key.StartsWith("person.", StringComparison.Ordinal))
        {
            var role = key.Substring("person.".Length);
            return PersonKinds.Contains(role)
                ? _people(item).Where(p => string.Equals(p.Kind, role, StringComparison.OrdinalIgnoreCase)).Select(p => p.Name)
                : null;
        }

        return key is "tag" or "tags" ? null : ReflectedValues(item, key);
    }

    /// <summary>
    /// Values of any other readable property of the item, found by name (case-insensitive): text, numbers,
    /// yes/no, dates, enums and lists of those. Null when the item has no such property or it is not of such a type.
    /// </summary>
    private static IEnumerable<string>? ReflectedValues(BaseItem item, string key)
        => FindProperty(item.GetType(), key) is { } info ? ValuesOf(info, item) : null;

    private static IEnumerable<string> ValuesOf(System.Reflection.PropertyInfo info, object target)
    {
        object? value;
        try
        {
            value = info.GetValue(target);
        }
        catch (Exception ex) when (ex is System.Reflection.TargetInvocationException or InvalidOperationException or NullReferenceException)
        {
            return Array.Empty<string>();
        }

        if (value is null)
        {
            return Array.Empty<string>();
        }

        if (value is string text)
        {
            return new[] { text };
        }

        if (value is System.Collections.IEnumerable list)
        {
            return list.Cast<object?>().Select(ScalarText).OfType<string>().ToList();
        }

        return ScalarText(value) is { } single ? new[] { single } : Array.Empty<string>();
    }

    /// <summary>
    /// Splits a stream property name of the form video.&lt;property&gt;, audio.&lt;property&gt; or subtitle.&lt;property&gt;
    /// into (stream kind, property).
    /// </summary>
    private static (string Kind, string Prop)? StreamName(string key)
    {
        var dot = key.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0 || dot == key.Length - 1)
        {
            return null;
        }

        var kind = key.Substring(0, dot);
        return kind is "video" or "audio" or "subtitle" ? (kind, key.Substring(dot + 1)) : null;
    }

    private IEnumerable<string>? StreamValues(BaseItem item, string kind, string prop)
    {
        var type = kind switch { "video" => MediaStreamType.Video, "audio" => MediaStreamType.Audio, _ => MediaStreamType.Subtitle };
        if (FindProperty(typeof(MediaStream), prop) is not { } info)
        {
            return null;
        }

        // Memoised: reading an item's streams is a database lookup, so keep only the short values.
        return _memo(item, kind + "." + info.Name.ToLowerInvariant(), () => _streams(item)
            .Where(m => m.Type == type)
            .SelectMany(m => ValuesOf(info, m))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToArray());
    }

    private static System.Reflection.PropertyInfo? FindProperty(Type type, string name)
    {
        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase;
        System.Reflection.PropertyInfo? info;
        try
        {
            info = type.GetProperty(name, flags);
        }
        catch (System.Reflection.AmbiguousMatchException)
        {
            // A property hidden by a derived class: take the most derived one.
            info = type.GetProperty(name, flags | System.Reflection.BindingFlags.DeclaredOnly);
        }

        if (info is null || !info.CanRead || info.GetIndexParameters().Length > 0)
        {
            return null;
        }

        var t = Nullable.GetUnderlyingType(info.PropertyType) ?? info.PropertyType;
        var element = t.IsArray ? t.GetElementType()! : t.IsGenericType ? t.GetGenericArguments()[0] : t;
        element = Nullable.GetUnderlyingType(element) ?? element;
        return IsScalar(t) || (t != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(t) && IsScalar(element)) ? info : null;
    }

    /// <summary>
    /// Every property name that can be used: the suggested ones first, then the person roles, then each other readable
    /// property of videos and episodes that holds text, numbers, yes/no, dates or lists of those (minus tags and internal ids/paths).
    /// </summary>
    /// <returns>The names.</returns>
    public static IReadOnlyList<string> AllSuggestions()
    {
        var names = new List<string>(SuggestedProperties);
        names.AddRange(Enum.GetNames<Jellyfin.Data.Enums.PersonKind>().Select(n => "person." + char.ToLowerInvariant(n[0]) + n.Substring(1)));
        var reflected = new[] { typeof(Video), typeof(MediaBrowser.Controller.Entities.TV.Episode) }
            .SelectMany(t => t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            .Select(p => p.Name)
            .Where(n => FindProperty(typeof(MediaBrowser.Controller.Entities.TV.Episode), n) is not null || FindProperty(typeof(Video), n) is not null)
            .Where(n => !n.EndsWith("Id", StringComparison.Ordinal) && !n.EndsWith("Ids", StringComparison.Ordinal)
                && !n.EndsWith("Path", StringComparison.Ordinal) && !n.EndsWith("Paths", StringComparison.Ordinal)
                && !n.EndsWith("Key", StringComparison.Ordinal) && !n.EndsWith("Keys", StringComparison.Ordinal)
                && !n.StartsWith("Supports", StringComparison.Ordinal) && !n.StartsWith("Enable", StringComparison.Ordinal)
                && !n.StartsWith("Inherited", StringComparison.Ordinal)
                && !string.Equals(n, "Tags", StringComparison.Ordinal))
            .Select(n => char.ToLowerInvariant(n[0]) + n.Substring(1))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
        names.AddRange(reflected);
        string[] skip = { "Localized", "Delivery", "Supports", "Dv", "Rpu", "Pl", "Bl", "El", "TimeBase", "CodecTimeBase", "NalLength", "Path", "Score", "Index", "IsExternalUrl", "Comment" };
        foreach (var kind in new[] { "video", "audio", "subtitle" })
        {
            names.AddRange(typeof(MediaStream).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Select(p => p.Name)
                .Where(n => FindProperty(typeof(MediaStream), n) is not null && !skip.Any(k => n.StartsWith(k, StringComparison.Ordinal)))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Select(n => kind + "." + char.ToLowerInvariant(n[0]) + n.Substring(1)));
        }

        return names.Distinct(StringComparer.OrdinalIgnoreCase).Where(n => !n.Equals("unknown", StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static bool IsScalar(Type t) =>
        t == typeof(string) || t.IsPrimitive || t.IsEnum || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(DateTimeOffset);

    private static string? ScalarText(object? value) => value switch
    {
        null => null,
        string s => s,
        DateTime d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable f when value.GetType().IsPrimitive || value is decimal => f.ToString(null, CultureInfo.InvariantCulture),
        Enum e => e.ToString(),
        bool b => b ? "Yes" : "No",
        _ => null,
    };

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
