// SPDX-License-Identifier: GPL-3.0-or-later
using Jellyfin.Plugin.TagBrowser.Channels;
using Xunit;

namespace Jellyfin.Plugin.TagBrowser.Tests;

public class TagTreeTests
{
    [Theory]
    [InlineData("move: cambre", ":", new[] { "move", "cambre" })]
    [InlineData("a: b: c", ":", new[] { "a", "b", "c" })]
    [InlineData("to practise", ":", new[] { "to practise" })]
    [InlineData("  move :  cambre  ", ":", new[] { "move", "cambre" })]
    [InlineData("move::cambre", ":", new[] { "move", "cambre" })]
    [InlineData("move: cambre", "", new[] { "move: cambre" })]
    [InlineData("move > cambre", " > ", new[] { "move", "cambre" })]
    public void Split_SplitsTrimsAndDropsEmptySegments(string tag, string separator, string[] expected)
    {
        Assert.Equal(expected, TagTree.Split(tag, separator));
    }

    [Fact]
    public void Split_OfBlankTag_IsEmpty()
    {
        Assert.Empty(TagTree.Split("   ", ":"));
    }

    [Fact]
    public void FolderId_RoundTrips()
    {
        var path = new[] { "Date", "2022", "2022-03 March", "with: colon" };
        Assert.Equal(path, TagTree.FromFolderId(TagTree.ToFolderId(path)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-ours")]
    public void FromFolderId_OfRootOrForeignId_IsRoot(string? id)
    {
        Assert.Empty(TagTree.FromFolderId(id));
    }

    [Fact]
    public void StartsWith_IsCaseInsensitive()
    {
        Assert.True(TagTree.StartsWith(new[] { "Move", "Cambre" }, new[] { "move" }));
        Assert.True(TagTree.StartsWith(new[] { "move" }, System.Array.Empty<string>()));
        Assert.False(TagTree.StartsWith(new[] { "move" }, new[] { "move", "cambre" }));
        Assert.False(TagTree.StartsWith(new[] { "level", "x" }, new[] { "move" }));
    }
}
