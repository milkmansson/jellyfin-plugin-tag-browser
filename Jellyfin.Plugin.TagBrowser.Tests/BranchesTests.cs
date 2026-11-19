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

    private static Branches Make(PluginConfiguration config, (string Name, string Location)[]? libraries = null, params string[] people)
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
        var config = new PluginConfiguration { TagsAtTopLevel = false, BranchGenres = true, BranchStudios = true };
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

    [Fact]
    public void People_AreDistinctAndOnlyWhenEnabled()
    {
        var item = Item("/media/Dance/a.mp4");
        Assert.DoesNotContain(Make(new PluginConfiguration(), null, "Anna").PathsFor(item), p => p[0] == "People");

        var paths = Make(new PluginConfiguration { BranchPeople = true }, null, "Anna", "anna", "Bruno").PathsFor(item)
            .Where(p => p[0] == "People").Select(p => p[1]).ToList();
        Assert.Equal(new[] { "Anna", "Bruno" }, paths);
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
}
