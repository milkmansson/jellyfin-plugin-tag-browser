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
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TagBrowser.Channels;

/// <summary>
/// A channel that presents library tags as a folder tree.
/// </summary>
public class TagBrowserChannel : IChannel, IHasCacheKey
{
    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly ILogger<TagBrowserChannel> _logger;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Guid Id, string Key), (DateTime Modified, string[] Values)> StreamCache = new();
    private static readonly long StartedAt = DateTime.UtcNow.Ticks;
    private static long _libraryVersion;

    /// <summary>
    /// Initializes a new instance of the <see cref="TagBrowserChannel"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="logger">The logger.</param>
    public TagBrowserChannel(ILibraryManager libraryManager, IUserManager userManager, ILogger<TagBrowserChannel> logger)
        : this(libraryManager, userManager, logger, true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TagBrowserChannel"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="watchLibrary">Whether this instance bumps the shared cache version on library changes (once per server is enough).</param>
    protected TagBrowserChannel(ILibraryManager libraryManager, IUserManager userManager, ILogger<TagBrowserChannel> logger, bool watchLibrary)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
        _logger = logger;

        if (watchLibrary)
        {
            // Any library change may change tags; bump the version so Jellyfin re-queries the channels.
            // Channel items (including ours) are ignored, otherwise every listing would trigger another.
            _libraryManager.ItemAdded += OnLibraryChanged;
            _libraryManager.ItemUpdated += OnLibraryChanged;
            _libraryManager.ItemRemoved += OnLibraryChanged;
        }
    }

    /// <summary>
    /// Gets the main channel's name from the settings.
    /// </summary>
    protected static string MainChannelName
    {
        get
        {
            var name = Plugin.Instance?.Configuration.ChannelName;
            return string.IsNullOrWhiteSpace(name) ? Configuration.PluginConfiguration.DefaultChannelName : name.Trim();
        }
    }

    /// <inheritdoc />
    public virtual string Name => MainChannelName;

    /// <inheritdoc />
    public virtual string Description => "Browse your library by tag.";

    /// <inheritdoc />
    public string DataVersion => "3";

    /// <inheritdoc />
    public string HomePageUrl => "https://github.com/milkmansson/jellyfin-plugin-tag-browser";

    /// <inheritdoc />
    public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

    /// <summary>
    /// Gets the tree path this channel opens at; empty for the main channel.
    /// </summary>
    protected virtual IReadOnlyList<string> RootNode => Array.Empty<string>();

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
    public virtual bool IsEnabledFor(string userId) => Plugin.Instance?.Configuration.MainChannelEnabled ?? true;

    private void OnLibraryChanged(object? sender, ItemChangeEventArgs e)
    {
        if (e.Item is not null && e.Item.ChannelId.Equals(Guid.Empty))
        {
            Interlocked.Increment(ref _libraryVersion);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Jellyfin caches listings per cache key for hours. The key changes on every library change and every
    /// settings save (never reusing an old key, which would bring back stale listings), and per process start.
    /// </remarks>
    public string GetCacheKey(string? userId)
        => $"{StartedAt}-{Interlocked.Read(ref _libraryVersion)}-{Plugin.Instance?.ConfigurationVersion ?? 0}";

    /// <inheritdoc />
    public virtual async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (!IsEnabledFor(string.Empty))
        {
            return new ChannelItemResult();
        }

        var node = TagTree.FromFolderId(query.FolderId);
        if (node.Length == 0)
        {
            node = RootNode.ToArray();
        }

        var root = RootNode;
        var promotedRoots = ChannelPlan.Promoted(config ?? new Configuration.PluginConfiguration(), Separator, MainChannelName).Select(p => p.Root).ToList();
        var separator = Separator;
        var videoOrder = config?.VideoOrder ?? "Title";

        // child folder name (case-insensitive) -> (display name, distinct videos anywhere below it)
        var folders = new Dictionary<string, (string Name, Dictionary<Guid, BaseItem> Videos)>(StringComparer.OrdinalIgnoreCase);
        var media = new List<BaseItem>();

        var branches = new Branches(config ?? new Configuration.PluginConfiguration(), GetLibraryLocations(), PeopleOf, ParentsOf, StreamsOf, StreamMemo);

        var timer = System.Diagnostics.Stopwatch.StartNew();
        var scanned = 0;
        foreach (var item in GetSourceVideos(query.UserId))
        {
            scanned++;
            var placedHere = false;
            foreach (var path in branches.PathsFor(item))
            {
                if (path.Length == 0 || !TagTree.StartsWith(path, node) || ChannelPlan.IsMovedOut(path, root, promotedRoots))
                {
                    continue;
                }

                if (path.Length == node.Length)
                {
                    placedHere = true;
                    continue;
                }

                var child = path[node.Length];
                if (!folders.TryGetValue(child, out var entry))
                {
                    entry = (child, new Dictionary<Guid, BaseItem>());
                    folders[child] = entry;
                }

                entry.Videos.TryAdd(item.Id, item);
            }

            if (placedHere)
            {
                media.Add(item);
            }
        }

        timer.Stop();
        _logger.Log(
            timer.ElapsedMilliseconds > 500 ? LogLevel.Information : LogLevel.Debug,
            "Tag Browser: scanned {Videos} videos for [{Node}] in {Ms} ms",
            scanned,
            string.Join(" > ", node),
            timer.ElapsedMilliseconds);

        var pinned = Ordering.ParsePinned(config?.PinnedFolders, separator);
        var sortedFolders = Ordering.SortFolders(
            folders.Values.Select(f => (Path: node.Append(f.Name).ToArray(), Videos: f.Videos.Values.ToList())),
            pinned,
            config?.FolderOrder ?? "Alphabetical");

        var items = new List<ChannelItemInfo>();
        foreach (var folder in sortedFolders)
        {
            items.Add(new ChannelItemInfo
            {
                Id = TagTree.ToFolderId(folder.Path),
                Name = folder.Path[^1],
                Type = ChannelItemType.Folder,
                FolderType = ChannelFolderType.Container,
                ImageUrl = FolderImagePath(folder.Videos, TagTree.ToFolderId(folder.Path), videoOrder, config?.FolderImage ?? "FirstVideo"),
            });
        }

        // Jellyfin keys channel items on their id alone and gives each a single parent, so a video
        // placed in several folders needs a distinct id per placement (folder path + library id).
        var folderKey = TagTree.ToFolderId(node);
        items.AddRange(Ordering.SortVideos(media, videoOrder).Select(v => ToChannelItem(v, folderKey)));

        _logger.LogDebug("Tag Browser: node [{Node}] -> {Folders} folders, {Media} items", string.Join(" > ", node), folders.Count, media.Count);

        // Jellyfin only applies a channel item's image when the item has none yet, so changed
        // thumbnail settings would never reach existing folders. Update those ourselves.
        foreach (var folder in items.Where(i => i.Type == ChannelItemType.Folder && !string.IsNullOrEmpty(i.ImageUrl)))
        {
            await SyncFolderImageAsync(folder, cancellationToken).ConfigureAwait(false);
        }

        return new ChannelItemResult
        {
            Items = items,
            TotalRecordCount = items.Count,
        };
    }

    /// <inheritdoc />
    public virtual Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
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

    /// <summary>
    /// Sets the image of an existing channel folder if it differs from the wanted one.
    /// The id formula mirrors Jellyfin's ChannelManager (externalId + channel name + "16"); see README pinning note.
    /// </summary>
    private async Task SyncFolderImageAsync(ChannelItemInfo info, CancellationToken cancellationToken)
    {
        var id = _libraryManager.GetNewItemId(info.Id + Name + "16", typeof(Folder));
        if (_libraryManager.GetItemById(id) is not { } existing
            || string.Equals(existing.GetImagePath(ImageType.Primary, 0), info.ImageUrl, StringComparison.Ordinal))
        {
            return;
        }

        existing.SetImagePath(ImageType.Primary, info.ImageUrl);
        await _libraryManager.UpdateItemAsync(existing, existing.GetParent(), ItemUpdateType.ImageUpdate, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The image for a folder (or a promoted channel): a thumbnail of one of its videos, or the logo, per the settings.
    /// </summary>
    /// <param name="videos">The videos under the folder.</param>
    /// <param name="folderId">The folder id (keeps a random pick stable).</param>
    /// <param name="videoOrder">Which video provides the thumbnail.</param>
    /// <param name="folderImage">FirstVideo, Logo or None.</param>
    /// <returns>The image path, or null for none.</returns>
    protected static string? FolderImagePath(IReadOnlyList<BaseItem> videos, string folderId, string videoOrder, string folderImage)
    {
        if (string.Equals(folderImage, "None", StringComparison.Ordinal))
        {
            return null;
        }

        if (string.Equals(folderImage, "FirstVideo", StringComparison.Ordinal))
        {
            var candidates = string.Equals(videoOrder, "Random", StringComparison.Ordinal)
                ? Ordering.StableShuffle(videos, folderId)
                : Ordering.SortVideos(videos, videoOrder);
            var thumb = candidates
                .Select(v => v.GetImagePath(ImageType.Primary))
                .FirstOrDefault(path => !string.IsNullOrEmpty(path) && File.Exists(path));
            if (thumb is not null)
            {
                return thumb;
            }
        }

        return Plugin.Instance?.GetFolderLogoPath();
    }

    /// <summary>
    /// Jellyfin stores a channel's images once, when it first creates the channel, and never asks again. Set this
    /// channel's tile images to <paramref name="path"/> if they differ, so they follow the videos' thumbnails.
    /// </summary>
    /// <param name="path">The image path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the images are up to date.</returns>
    protected async Task SyncChannelTileAsync(string path, CancellationToken cancellationToken)
    {
        var channels = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Channel },
            Name = Name,
        });

        foreach (var channel in channels.Where(c => string.Equals(c.Name, Name, StringComparison.Ordinal)))
        {
            var changed = false;
            foreach (var type in new[] { ImageType.Primary, ImageType.Thumb })
            {
                if (!string.Equals(channel.GetImagePath(type, 0), path, StringComparison.Ordinal))
                {
                    channel.SetImagePath(type, path);
                    changed = true;
                }
            }

            if (changed)
            {
                await _libraryManager.UpdateItemAsync(channel, channel.GetParent(), ItemUpdateType.ImageUpdate, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// The videos listed under a tree path in this channel (after promoted branches have moved out).
    /// </summary>
    /// <param name="root">The tree path.</param>
    /// <returns>The videos.</returns>
    protected IReadOnlyList<BaseItem> VideosUnder(IReadOnlyList<string> root)
    {
        var config = Plugin.Instance?.Configuration ?? new Configuration.PluginConfiguration();
        var branches = new Branches(config, GetLibraryLocations(), PeopleOf, ParentsOf, StreamsOf, StreamMemo);
        var promotedRoots = ChannelPlan.Promoted(config, Separator, MainChannelName).Select(p => p.Root).ToList();
        return GetSourceVideos(Guid.Empty)
            .Where(i => branches.PathsFor(i).Any(p => p.Length > 0 && TagTree.StartsWith(p, root) && !ChannelPlan.IsMovedOut(p, root, promotedRoots)))
            .ToList();
    }

    private IEnumerable<BaseItem> GetSourceVideos(Guid userId)
    {
        var user = userId == Guid.Empty ? null : _userManager.GetUserById(userId);

        var itemsQuery = new InternalItemsQuery(user)
        {
            Recursive = true,
            IsFolder = false,
            MediaTypes = new[] { MediaType.Video },
        };

        var roots = GetLibraryLocations().Select(l => l.Location.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar).ToList();

        // Channel items (including this channel's own copies) are stored as videos too; only use real library items
        // from the included libraries.
        return _libraryManager.GetItemList(itemsQuery)
            .Where(i => i.ChannelId.Equals(Guid.Empty)
                && !string.IsNullOrEmpty(i.Path)
                && roots.Any(r => i.Path.StartsWith(r, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Included libraries as (name, root folder) pairs. Empty "Libraries" setting means all libraries.
    /// </summary>
    private IReadOnlyList<(string Name, string Location)> GetLibraryLocations()
    {
        var selected = (Plugin.Instance?.Configuration.Libraries ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _libraryManager.GetVirtualFolders()
            .Where(v => v.CollectionType is not CollectionTypeOptions.boxsets)
            .Where(v => selected.Count == 0 || selected.Contains(v.ItemId))
            .SelectMany(v => v.Locations.Select(l => (v.Name, l)))
            .ToList();
    }

    private static string[] StreamMemo(BaseItem item, string key, Func<string[]> compute)
    {
        if (StreamCache.TryGetValue((item.Id, key), out var hit) && hit.Modified == item.DateModified)
        {
            return hit.Values;
        }

        if (StreamCache.Count > 250_000)
        {
            StreamCache.Clear();
        }

        var values = compute();
        StreamCache[(item.Id, key)] = (item.DateModified, values);
        return values;
    }

    private static IReadOnlyList<MediaStream> StreamsOf(BaseItem item) => item.GetMediaStreams();

    private static IEnumerable<BaseItem> ParentsOf(BaseItem item)
        => item is MediaBrowser.Controller.Entities.TV.Episode { Series: { } series } ? new BaseItem[] { series } : Array.Empty<BaseItem>();

    private IEnumerable<(string Name, string Kind)> PeopleOf(BaseItem item) => _libraryManager.GetPeople(item).Select(p => (p.Name, p.Type.ToString()));

    private static ChannelItemInfo ToChannelItem(BaseItem item, string folderKey)
    {
        var info = new ChannelItemInfo
        {
            Id = folderKey + "#" + item.Id.ToString("N"),
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
