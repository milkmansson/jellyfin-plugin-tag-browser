// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TagBrowser.Channels;

/// <summary>
/// A channel that presents library tags as a folder tree.
/// </summary>
public class TagBrowserChannel : IChannel
{
    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly ILogger<TagBrowserChannel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TagBrowserChannel"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="logger">The logger.</param>
    public TagBrowserChannel(ILibraryManager libraryManager, IUserManager userManager, ILogger<TagBrowserChannel> logger)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name
    {
        get
        {
            var name = Plugin.Instance?.Configuration.ChannelName;
            return string.IsNullOrWhiteSpace(name) ? Configuration.PluginConfiguration.DefaultChannelName : name.Trim();
        }
    }

    /// <inheritdoc />
    public string Description => "Browse your library by tag.";

    /// <inheritdoc />
    public string DataVersion => "3";

    /// <inheritdoc />
    public string HomePageUrl => "https://github.com/milkmansson/jellyfin-plugin-tag-browser";

    /// <inheritdoc />
    public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

    private static string Separator => Plugin.Instance?.Configuration.Separator ?? ":";

    /// <inheritdoc />
    public InternalChannelFeatures GetChannelFeatures()
    {
        return new InternalChannelFeatures
        {
            ContentTypes = new List<ChannelMediaContentType> { ChannelMediaContentType.Clip },
            MediaTypes = new List<ChannelMediaType> { ChannelMediaType.Video },
        };
    }

    /// <inheritdoc />
    public bool IsEnabledFor(string userId) => true;

    /// <inheritdoc />
    public Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        var node = TagTree.FromFolderId(query.FolderId);
        var separator = Separator;

        var folders = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase); // key -> display name
        var media = new List<BaseItem>();

        foreach (var item in GetTaggedVideos(query.UserId))
        {
            var placedHere = false;
            foreach (var tag in item.Tags)
            {
                var path = TagTree.Split(tag, separator);
                if (path.Length == 0 || !TagTree.StartsWith(path, node))
                {
                    continue;
                }

                if (path.Length == node.Length)
                {
                    placedHere = true;
                }
                else
                {
                    var child = path[node.Length];
                    folders.TryAdd(child, child);
                }
            }

            if (placedHere)
            {
                media.Add(item);
            }
        }

        var items = new List<ChannelItemInfo>();
        foreach (var name in folders.Values)
        {
            items.Add(new ChannelItemInfo
            {
                Id = TagTree.ToFolderId(node.Append(name).ToArray()),
                Name = name,
                Type = ChannelItemType.Folder,
                FolderType = ChannelFolderType.Container,
            });
        }

        items.AddRange(media
            .OrderBy(i => i.SortName ?? i.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ToChannelItem));

        _logger.LogDebug("Tag Browser: node [{Node}] -> {Folders} folders, {Media} items", string.Join(" > ", node), folders.Count, media.Count);

        return Task.FromResult(new ChannelItemResult
        {
            Items = items,
            TotalRecordCount = items.Count,
        });
    }

    /// <inheritdoc />
    public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        var (customPath, resource) = type switch
        {
            ImageType.Primary => (config?.PrimaryImagePath, "primary.png"),
            ImageType.Thumb => (config?.ThumbImagePath, "thumb.png"),
            ImageType.Backdrop => (config?.BackdropImagePath, "backdrop.png"),
            _ => (null, null),
        };

        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
        {
            return Task.FromResult(new DynamicImageResponse
            {
                HasImage = true,
                Path = customPath,
                Protocol = MediaProtocol.File,
                Format = ImageFormatFromPath(customPath),
            });
        }

        if (!string.IsNullOrWhiteSpace(customPath))
        {
            _logger.LogWarning("Tag Browser: custom {Type} image not found at {Path}; using built-in artwork", type, customPath);
        }

        var stream = resource is null
            ? null
            : GetType().Assembly.GetManifestResourceStream($"{typeof(Plugin).Namespace}.Images.{resource}");

        return Task.FromResult(stream is null
            ? new DynamicImageResponse { HasImage = false }
            : new DynamicImageResponse { HasImage = true, Stream = stream, Format = ImageFormat.Png });
    }

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedChannelImages() => new[] { ImageType.Primary, ImageType.Thumb, ImageType.Backdrop };

    private static ImageFormat ImageFormatFromPath(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ImageFormat.Jpg,
            ".webp" => ImageFormat.Webp,
            ".gif" => ImageFormat.Gif,
            _ => ImageFormat.Png,
        };

    private IEnumerable<BaseItem> GetTaggedVideos(Guid userId)
    {
        var user = userId == Guid.Empty ? null : _userManager.GetUserById(userId);

        var itemsQuery = new InternalItemsQuery(user)
        {
            Recursive = true,
            IsFolder = false,
            MediaTypes = new[] { MediaType.Video },
        };

        return _libraryManager.GetItemList(itemsQuery).Where(i => i.Tags is { Length: > 0 });
    }

    private static ChannelItemInfo ToChannelItem(BaseItem item)
    {
        var info = new ChannelItemInfo
        {
            Id = item.Id.ToString("N"),
            Name = item.Name,
            OriginalTitle = item.OriginalTitle,
            Overview = item.Overview,
            Type = ChannelItemType.Media,
            ContentType = ChannelMediaContentType.Clip,
            MediaType = ChannelMediaType.Video,
            RunTimeTicks = item.RunTimeTicks,
            PremiereDate = item.PremiereDate,
            ProductionYear = item.ProductionYear,
            CommunityRating = item.CommunityRating,
            DateCreated = item.DateCreated,
            Genres = item.Genres.ToList(),
            Tags = item.Tags.ToList(),
            Studios = item.Studios.ToList(),
            ImageUrl = item.GetImagePath(ImageType.Primary),
        };

        if (item is IHasMediaSources hasSources)
        {
            info.MediaSources = hasSources.GetMediaSources(false).ToList();
        }

        return info;
    }
}
