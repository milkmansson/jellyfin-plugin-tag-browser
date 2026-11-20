// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.TagBrowser.Channels;
using Jellyfin.Plugin.TagBrowser.Configuration;
using Xunit;

namespace Jellyfin.Plugin.TagBrowser.Tests;

public class ChannelPlanTests
{
    private static PluginConfiguration Config(params (string Path, string Name)[] rows)
        => new() { PromotedChannels = rows.Select(r => new PromotedChannel { Path = r.Path, Name = r.Name }).ToArray() };

    private static List<string> Names(PluginConfiguration c, string main = "Tag Browser")
        => ChannelPlan.Promoted(c, ":", main).Select(p => p.Name).ToList();

    [Fact]
    public void NothingPromoted_ByDefault()
    {
        Assert.Empty(ChannelPlan.Promoted(new PluginConfiguration(), ":", "Tag Browser"));
        Assert.True(new PluginConfiguration().MainChannelEnabled);
    }

    [Fact]
    public void Row_SplitsThePathOnTheSeparator_AndNameDefaultsToTheLastPart()
    {
        var plan = ChannelPlan.Promoted(Config(("dance: zouk", string.Empty), ("studio", "Studios")), ":", "Tag Browser");
        Assert.Equal(new[] { "zouk", "Studios" }, plan.Select(p => p.Name));
        Assert.Equal(new[] { "dance", "zouk" }, plan[0].Root);
        Assert.Equal(new[] { "studio" }, plan[1].Root);
    }

    [Fact]
    public void EmptyPaths_DuplicateNames_DuplicatePaths_AndTheMainChannelsName_AreIgnored()
    {
        var c = Config(
            (string.Empty, "Nothing"),
            ("dance", "Dance"),
            ("other", "dance"),
            ("DANCE", "Again"),
            ("x", "Tag Browser"),
            ("y", "Y"));
        Assert.Equal(new[] { "Dance", "Y" }, Names(c));
    }

    [Fact]
    public void AtMostTenChannels()
    {
        var c = Config(Enumerable.Range(1, 15).Select(i => ("t" + i, "C" + i)).ToArray());
        Assert.Equal(ChannelPlan.MaxPromoted, Names(c).Count);
        Assert.Equal("C1", Names(c)[0]);
    }

    private static readonly IReadOnlyList<string> Dance = new[] { "dance" };
    private static readonly IReadOnlyList<string> DanceZouk = new[] { "dance", "zouk" };
    private static readonly IReadOnlyList<string> None = Array.Empty<string>();

    [Fact]
    public void PromotedBranch_MovesOutOfTheMainChannel_NotOutOfItsOwn()
    {
        var promoted = new[] { Dance };
        Assert.True(ChannelPlan.IsMovedOut(new[] { "dance", "zouk" }, None, promoted));
        Assert.True(ChannelPlan.IsMovedOut(new[] { "dance" }, None, promoted));
        Assert.False(ChannelPlan.IsMovedOut(new[] { "Studios", "FOX" }, None, promoted));
        Assert.False(ChannelPlan.IsMovedOut(new[] { "dance", "zouk" }, Dance, promoted));
    }

    [Fact]
    public void MatchingIgnoresCase()
    {
        Assert.True(ChannelPlan.IsMovedOut(new[] { "Dance", "x" }, None, new[] { Dance }));
    }

    [Fact]
    public void BranchPromotedInsideAnotherPromotedBranch_LeavesTheOuterOneToo()
    {
        var promoted = new[] { Dance, DanceZouk };
        Assert.True(ChannelPlan.IsMovedOut(new[] { "dance", "zouk", "a" }, Dance, promoted));
        Assert.False(ChannelPlan.IsMovedOut(new[] { "dance", "wcs" }, Dance, promoted));
        Assert.False(ChannelPlan.IsMovedOut(new[] { "dance", "zouk", "a" }, DanceZouk, promoted));
        Assert.True(ChannelPlan.IsMovedOut(new[] { "dance", "zouk", "a" }, None, promoted));
    }
}
