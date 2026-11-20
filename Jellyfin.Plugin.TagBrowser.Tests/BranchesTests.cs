// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.TagBrowser.Channels;
using Jellyfin.Plugin.TagBrowser.Configuration;
using MediaBrowser.Controller.Entities;
using Xunit;

namespace Jellyfin.Plugin.TagBrowser.Tests;

public class BranchesTests
{
    private static readonly (string Name, string Location)[] OneLibrary = { ("Dance", "/media/Dance") };

    private static Branches Make(PluginConfiguration config, (string Name, string Location)[]? libraries = null, params (string Name, string Kind)[] people)
        => new(config, libraries ?? OneLibrary, _ => people);

    private static Video Item(string path, string[]? tags = null, DateTime? premiere = null, int? year = null)
        => new() { Path = path, Tags = tags ?? Array.Empty<string>(), PremiereDate = premiere, ProductionYear = year };

    private static PluginConfiguration TagsOnly() => new() { BranchDate = false };

    [Theory]
    [InlineData("/x/20220213_180246.mp4", 2022, 2, 13)]
    [InlineData("/x/VID_20251203_210934.mp4", 2025, 12, 3)]
    [InlineData("/x/VID-20250924-WA0005.mp4", 2025, 9, 24)]
    [InlineData("/x/2022-03-20 lesson.mp4", 2022, 3, 20)]
    [InlineData("/x/IMG_20210101.jpg", 2021, 1, 1)]
    public void DateFromFileName_FindsDates(string path, int y, int m, int d)
    {
        Assert.Equal(new DateTime(y, m, d), Branches.DateFromFileName(path));
    }

    [Theory]
    [InlineData("/x/received_1299897487208591.mp4")]
    [InlineData("/x/lesson.mp4")]
    [InlineData("/x/20221345_000000.mp4")]
    [InlineData("/x/99991231.mp4")]
    [InlineData(null)]
    public void DateFromFileName_RejectsNonDates(string? path)
    {
        Assert.Null(Branches.DateFromFileName(path));
    }

    [Fact]
    public void Default_TagsAtTopLevel_AlongsideDate()
    {
        var item = Item("/media/Dance/a.mp4", new[] { "move: cambre" }, new DateTime(2022, 3, 20, 0, 0, 0, DateTimeKind.Utc));
        var paths = Make(new PluginConfiguration()).PathsFor(item).Select(p => string.Join("/", p)).ToList();
        Assert.Equal(new[] { "move/cambre", "Date/2022/2022-03 March/2022-03-20" }, paths);
    }

    [Fact]
    public void TagsOnly_TagsAreAtTopLevel()
    {
        var paths = Make(TagsOnly()).PathsFor(Item("/media/Dance/a.mp4", new[] { "move: cambre", "to practise" })).ToList();
        Assert.Equal(new[] { new[] { "move", "cambre" }, new[] { "to practise" } }, paths);
    }

    [Fact]
    public void SeveralBranches_PrefixWithBranchNames()
    {
        var config = new PluginConfiguration { TagsAtTopLevel = false, PropertyBranches = new[] { new PropertyBranch { Property = "Genres" }, new PropertyBranch { Property = "studios" } } };
        var item = Item("/media/Dance/a.mp4", new[] { "move: cambre" }, new DateTime(2022, 3, 20, 0, 0, 0, DateTimeKind.Utc));
        item.Genres = new[] { "Zouk" };
        item.Studios = new[] { "Inflow" };

        var paths = Make(config).PathsFor(item).Select(p => string.Join("/", p)).ToList();

        Assert.Contains("Tags/move/cambre", paths);
        Assert.Contains("Date/2022/2022-03 March/2022-03-20", paths);
        Assert.Contains("Genres/Zouk", paths);
        Assert.Contains("Studios/Inflow", paths);
    }

    [Fact]
    public void BranchNames_AreConfigurable()
    {
        var config = new PluginConfiguration { TagsAtTopLevel = false, BranchTagsName = "Etiketter", BranchDateName = " Datum " };
        var item = Item("/media/Dance/a.mp4", new[] { "x" }, new DateTime(2022, 3, 20, 0, 0, 0, DateTimeKind.Utc));
        var firsts = Make(config).PathsFor(item).Select(p => p[0]).ToList();
        Assert.Equal(new[] { "Etiketter", "Datum" }, firsts);
    }

    private static PluginConfiguration Props(params string[] properties)
        => new() { TagsAtTopLevel = false, BranchTags = false, BranchDate = false, UnknownName = string.Empty, PropertyBranches = properties.Select(p => new PropertyBranch { Property = p }).ToArray() };

    private static List<string> PathStrings(Branches b, BaseItem item) => b.PathsFor(item).Select(p => string.Join("/", p)).ToList();

    [Fact]
    public void NoPropertyBranches_ByDefault()
    {
        var item = Item("/media/Dance/a.mp4");
        item.Genres = new[] { "Zouk" };
        Assert.DoesNotContain(PathStrings(Make(new PluginConfiguration()), item), p => p.StartsWith("Genre", StringComparison.Ordinal));
    }

    [Fact]
    public void Property_IsCaseInsensitive_AndDefaultsDisplayNameToCapitalisedName()
    {
        var item = Item("/media/Dance/a.mp4");
        item.Studios = new[] { "Inflow", "inflow", " Other " };
        Assert.Equal(new[] { "Studios/Inflow", "Studios/Other" }, PathStrings(Make(Props("studios")), item));
        Assert.Equal(new[] { "STUDIOS/Inflow", "STUDIOS/Other" }, PathStrings(Make(Props("STUDIOS")), item));
    }

    [Fact]
    public void DisplayName_OverridesPropertyName()
    {
        var config = new PluginConfiguration { BranchTags = false, BranchDate = false, UnknownName = string.Empty, PropertyBranches = new[] { new PropertyBranch { Property = "studios", DisplayName = "Labels" } } };
        var item = Item("/media/Dance/a.mp4");
        item.Studios = new[] { "Inflow" };
        Assert.Equal(new[] { "Labels/Inflow" }, PathStrings(Make(config), item));
    }

    [Fact]
    public void Year_Rating_Country()
    {
        var item = Item("/media/Dance/a.mp4", year: 2021);
        item.OfficialRating = "PG";
        item.ProductionLocations = new[] { "Brazil" };
        Assert.Equal(new[] { "ProductionYear/2021", "OfficialRating/PG", "ProductionLocations/Brazil" }, PathStrings(Make(Props("productionYear", "officialRating", "productionLocations")), item));
    }

    [Fact]
    public void PersonRoles_FilterByKind()
    {
        var item = Item("/media/Dance/a.mp4");
        var b = Make(Props("person.actor", "person.director", "PERSON.GuestStar"), null, ("Anna", "Actor"), ("anna", "Actor"), ("Bruno", "Director"), ("Cleo", "GuestStar"));
        Assert.Equal(new[] { "Person.actor/Anna", "Person.director/Bruno", "PERSON.GuestStar/Cleo" }, PathStrings(b, item));
    }

    [Fact]
    public void UnknownProperty_AndDuplicateRows_AreIgnored()
    {
        var item = Item("/media/Dance/a.mp4");
        item.Genres = new[] { "Zouk" };
        Assert.Equal(new[] { "Genres/Zouk" }, PathStrings(Make(Props("genres", "GENRES", "nonsense", "person.nonsense", "genre", "")), item));
    }

    [Fact]
    public void DateOnly_IsUsedAsWritten()
    {
        var path = Make(new PluginConfiguration()).DatePath(Item("/x/a.mp4", premiere: new DateTime(2025, 12, 3, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(new[] { "2025", "2025-12 December", "2025-12-03" }, path);
    }

    [Fact]
    public void RecordedTime_IsConvertedToServerTimeZone()
    {
        var utc = new DateTime(2022, 3, 20, 22, 43, 41, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.Local);
        var path = Make(new PluginConfiguration()).DatePath(Item("/x/clip.mp4", premiere: utc));
        Assert.Equal(local.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), path![^1]);
    }

    [Fact]
    public void PreferFileNameDate_OverridesRecordedTime_ButNotDateOnly()
    {
        var config = new PluginConfiguration { PreferFileNameDate = true };
        var recorded = Item("/x/20220321_114341.mp4", premiere: new DateTime(2022, 3, 20, 22, 43, 41, DateTimeKind.Utc));
        Assert.Equal("2022-03-21", Make(config).DatePath(recorded)![^1]);

        var premiered = Item("/x/20220321_114341.mp4", premiere: new DateTime(2022, 3, 25, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal("2022-03-25", Make(config).DatePath(premiered)![^1]);
    }

    [Fact]
    public void FileNameDate_IsFallback_WhenEnabled()
    {
        var item = Item("/x/VID-20250924-WA0005.mp4");
        Assert.Equal("2025-09-24", Make(new PluginConfiguration()).DatePath(item)![^1]);
        Assert.Equal(new[] { "Undated" }, Make(new PluginConfiguration { DateFromFileName = false }).DatePath(item));
    }

    [Fact]
    public void Undated_FallsBackToYear_ThenUndatedFolder_OrHidden()
    {
        Assert.Equal(new[] { "2021" }, Make(new PluginConfiguration()).DatePath(Item("/x/received_1.mp4", year: 2021)));
        Assert.Equal(new[] { "Undated" }, Make(new PluginConfiguration()).DatePath(Item("/x/received_1.mp4")));
        Assert.Null(Make(new PluginConfiguration { UndatedName = "" }).DatePath(Item("/x/received_1.mp4")));
    }

    [Theory]
    [InlineData("Year", new[] { "2022" })]
    [InlineData("Month", new[] { "2022", "2022-03 March" })]
    [InlineData("Day", new[] { "2022", "2022-03 March", "2022-03-20" })]
    public void DateDepth_CutsThePath(string depth, string[] expected)
    {
        var item = Item("/x/a.mp4", premiere: new DateTime(2022, 3, 20, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(expected, Make(new PluginConfiguration { DateDepth = depth }).DatePath(item));
    }

    [Fact]
    public void FolderPath_IsRelativeToLibrary()
    {
        var b = Make(new PluginConfiguration());
        Assert.Equal(new[] { "Zouk", "2022" }, b.FolderPath(Item("/media/Dance/Zouk/2022/a.mp4")));
        Assert.Empty(b.FolderPath(Item("/media/Dance/a.mp4"))!);
        Assert.Null(b.FolderPath(Item("/media/DanceOther/a.mp4")));
    }

    [Fact]
    public void FolderPath_PrefixesLibraryName_WhenSeveralLibraries()
    {
        var libs = new[] { ("Dance", "/media/Dance"), ("Films", "/media/Films") };
        Assert.Equal(new[] { "Films", "Sci-Fi" }, Make(new PluginConfiguration(), libs).FolderPath(Item("/media/Films/Sci-Fi/a.mkv")));
    }

    [Fact]
    public void Episode_InheritsSeriesValues_YearAndRatingOnlyAsFallback()
    {
        var series = Item("/media/Shows/S");
        series.Studios = new[] { "FOX" };
        series.Genres = new[] { "Comedy" };
        series.ProductionYear = 2011;
        series.OfficialRating = "TV-14";

        var episode = Item("/media/Shows/S/e.mkv", year: 2013);
        episode.Genres = new[] { "Romance" };

                var b = new Branches(Props("studios", "genres", "productionYear", "officialRating", "person.actor"), OneLibrary, i => ReferenceEquals(i, series) ? new[] { ("Zooey", "Actor") } : new[] { ("Guest", "GuestStar") }, i => ReferenceEquals(i, episode) ? new[] { series } : Array.Empty<BaseItem>());

        Assert.Equal(
            new[] { "Studios/FOX", "Genres/Romance", "Genres/Comedy", "ProductionYear/2013", "OfficialRating/TV-14", "Person.actor/Zooey" },
            PathStrings(b, episode));
    }

    [Fact]
    public void AnyReadableProperty_CanBeUsed_ByName()
    {
        var item = Item("/media/Dance/a.mp4");
        item.OriginalTitle = "Orig";
        item.CommunityRating = 7.5f;
        var b = Make(Props("ORIGINALTITLE", "communityRating", "isLocked", "dateCreated", "nonsense", "path"));
        var paths = PathStrings(b, item);
        Assert.Contains("ORIGINALTITLE/Orig", paths);
        Assert.Contains("CommunityRating/7.5", paths);
        Assert.Contains("IsLocked/No", paths);
        Assert.Contains("Path//media/Dance/a.mp4", paths);
        Assert.DoesNotContain(paths, p => p.StartsWith("Nonsense", StringComparison.Ordinal));
    }

    [Fact]
    public void Tag_IsNotAProperty()
    {
        var item = Item("/media/Dance/a.mp4", new[] { "x" });
        Assert.DoesNotContain(PathStrings(Make(Props("tag", "tags")), item), p => p.StartsWith("Tag", StringComparison.Ordinal));
    }

    [Fact]
    public void EverySuggestedProperty_Resolves()
    {
        var b = Make(new PluginConfiguration());
        var item = Item("/media/Dance/a.mp4");
        foreach (var name in Branches.SuggestedProperties)
        {
            Assert.NotNull(b.PropertyValues(item, name));
        }
    }

    [Fact]
    public void StreamProperties_UseTheItemsMediaStreams()
    {
        var item = Item("/media/Dance/a.mp4");
        var streams = new List<MediaBrowser.Model.Entities.MediaStream>
        {
            new() { Type = MediaBrowser.Model.Entities.MediaStreamType.Video, Codec = "h264", Height = 1080 },
            new() { Type = MediaBrowser.Model.Entities.MediaStreamType.Audio, Codec = "aac", Language = "eng", Channels = 2 },
            new() { Type = MediaBrowser.Model.Entities.MediaStreamType.Audio, Codec = "ac3", Language = "por", Channels = 6 },
            new() { Type = MediaBrowser.Model.Entities.MediaStreamType.Subtitle, Language = "swe" },
        };
        var b = new Branches(Props("video.codec", "audio.codec", "audio.language", "subtitle.language", "video.height", "audio.channels"), OneLibrary, _ => Array.Empty<(string, string)>(), null, _ => streams);

        Assert.Equal(
            new[]
            {
                "Video.codec/h264", "Audio.codec/aac", "Audio.codec/ac3", "Audio.language/eng", "Audio.language/por",
                "Subtitle.language/swe", "Video.height/1080", "Audio.channels/2", "Audio.channels/6",
            },
            PathStrings(b, item));
    }

    [Theory]
    [InlineData("codec")]
    [InlineData("videoCodec")]
    [InlineData("audioLanguage")]
    [InlineData("resolution")]
    [InlineData("hdr")]
    [InlineData("genre")]
    [InlineData("studio")]
    [InlineData("year")]
    [InlineData("rating")]
    [InlineData("country")]
    [InlineData("people")]
    [InlineData("actor")]
    public void Shortcuts_DoNotExist(string name)
    {
        var item = Item("/media/Dance/a.mp4");
        var streams = new List<MediaBrowser.Model.Entities.MediaStream> { new() { Type = MediaBrowser.Model.Entities.MediaStreamType.Video, Codec = "h264" } };
        var b = new Branches(Props(name), OneLibrary, _ => Array.Empty<(string, string)>(), null, _ => streams);
        Assert.Empty(PathStrings(b, item));
    }

    [Fact]
    public void AllSuggestions_StartWithTheCuratedOnes_HaveNoDuplicatesOrTags_AndAllResolve()
    {
        var all = Branches.AllSuggestions();
        Assert.Equal(Branches.SuggestedProperties, all.Take(Branches.SuggestedProperties.Count));
        Assert.Equal(all.Count, all.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.DoesNotContain(all, n => n.Equals("tags", StringComparison.OrdinalIgnoreCase));
        var b = Make(new PluginConfiguration());
        var video = Item("/media/Dance/a.mp4");
        var episode = new MediaBrowser.Controller.Entities.TV.Episode { Path = "/media/Shows/e.mkv" };
        foreach (var name in all)
        {
            Assert.True(b.PropertyValues(video, name) is not null || b.PropertyValues(episode, name) is not null, name);
        }
    }

    [Fact]
    public void GeneralStreamNames_ReadAnyStreamProperty_AndUnknownOnesShowNothing()
    {
        var item = Item("/media/Dance/a.mp4");
        var streams = new List<MediaBrowser.Model.Entities.MediaStream>
        {
            new() { Type = MediaBrowser.Model.Entities.MediaStreamType.Video, BitDepth = 10, Profile = "High" },
            new() { Type = MediaBrowser.Model.Entities.MediaStreamType.Audio, ChannelLayout = "5.1" },
        };
        var b = new Branches(Props("video.bitDepth", "VIDEO.profile", "audio.channelLayout", "audio.bitDepth", "video.nonsense", "stream.codec"), OneLibrary, _ => Array.Empty<(string, string)>(), null, _ => streams);
        Assert.Equal(new[] { "Video.bitDepth/10", "VIDEO.profile/High", "Audio.channelLayout/5.1" }, PathStrings(b, item));
    }

    [Fact]
    public void StreamValues_AreMemoisedPerItemAndName()
    {
        var item = Item("/media/Dance/a.mp4");
        var lookups = 0;
        var streams = new List<MediaBrowser.Model.Entities.MediaStream> { new() { Type = MediaBrowser.Model.Entities.MediaStreamType.Video, Codec = "h264" } };
        var cache = new Dictionary<string, string[]>();
        Func<BaseItem, string, Func<string[]>, string[]> memo = (_, key, compute) => cache.TryGetValue(key, out var v) ? v : cache[key] = compute();
        var b = new Branches(Props("video.codec"), OneLibrary, _ => Array.Empty<(string, string)>(), null, _ => { lookups++; return streams; }, memo);
        PathStrings(b, item);
        PathStrings(b, item);
        Assert.Equal(1, lookups);
    }

    [Fact]
    public void Unknown_FolderForVideosWithoutAValue_ButNotForUnknownProperties()
    {
        var config = Props("person.director", "studios", "nonsense");
        config.UnknownName = " Unknown ";
        var item = Item("/media/Dance/a.mp4");
        item.Studios = new[] { "Inflow" };
        var b = Make(config, null, ("Anna", "Actor"));
        Assert.Equal(new[] { "Person.director/Unknown", "Studios/Inflow" }, PathStrings(b, item));
    }

    [Fact]
    public void Unknown_CanBeHidden()
    {
        var config = Props("person.director");
        config.UnknownName = string.Empty;
        Assert.Empty(PathStrings(Make(config), Item("/media/Dance/a.mp4")));
    }

    [Fact]
    public void Unknown_DefaultIsUnknown()
    {
        Assert.Equal("Unknown", new PluginConfiguration().UnknownName);
    }

    [Fact]
    public void Episode_WithNoValueOfItsOwnOrItsSeries_IsUnknown()
    {
        var series = Item("/media/Shows/S");
        var episode = Item("/media/Shows/S/e.mkv");
        var config = Props("studios");
        config.UnknownName = "Unknown";
        var b = new Branches(config, OneLibrary, _ => Array.Empty<(string, string)>(), i => ReferenceEquals(i, episode) ? new[] { series } : Array.Empty<BaseItem>());
        Assert.Equal(new[] { "Studios/Unknown" }, PathStrings(b, episode));
        series.Studios = new[] { "FOX" };
        Assert.Equal(new[] { "Studios/FOX" }, PathStrings(b, episode));
    }
}
