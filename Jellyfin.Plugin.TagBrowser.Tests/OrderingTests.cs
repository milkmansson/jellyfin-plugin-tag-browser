// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Jellyfin.Plugin.TagBrowser.Channels;
using MediaBrowser.Controller.Entities;
using Xunit;

namespace Jellyfin.Plugin.TagBrowser.Tests;

public class OrderingTests
{
    // ForcedSortName: BaseItem.SortName otherwise needs server configuration.
    private static readonly Video A = new() { Name = "b-title", ForcedSortName = "b-title", PremiereDate = new DateTime(2022, 1, 1), DateCreated = new DateTime(2024, 1, 1) };
    private static readonly Video B = new() { Name = "a-title", ForcedSortName = "a-title", PremiereDate = new DateTime(2023, 1, 1), DateCreated = new DateTime(2023, 1, 1) };
    private static readonly Video C = new() { Name = "c-title", ForcedSortName = "c-title", PremiereDate = new DateTime(2021, 1, 1), DateCreated = new DateTime(2025, 1, 1) };

    [Theory]
    [InlineData("Title", "a-title,b-title,c-title")]
    [InlineData("PremiereDesc", "a-title,b-title,c-title")]
    [InlineData("PremiereAsc", "c-title,b-title,a-title")]
    [InlineData("DateAddedDesc", "c-title,b-title,a-title")]
    public void SortVideos_FollowsSetting(string order, string expected)
    {
        Assert.Equal(expected, string.Join(",", Ordering.SortVideos(new[] { A, B, C }, order).Select(v => v.Name)));
    }
}
