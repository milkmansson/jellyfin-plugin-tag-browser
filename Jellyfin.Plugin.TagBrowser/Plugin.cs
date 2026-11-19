// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Jellyfin.Plugin.TagBrowser.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TagBrowser;

/// <summary>
/// The Tag Browser plugin: exposes library tags as a browsable channel tree.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private readonly ITaskManager _taskManager;
    private readonly ILogger<Plugin> _logger;
    private long _configurationVersion;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    /// <param name="taskManager">Instance of the <see cref="ITaskManager"/> interface.</param>
    /// <param name="logger">The logger.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ITaskManager taskManager, ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        _taskManager = taskManager;
        _logger = logger;
        ConfigurationChanged += (_, _) =>
        {
            Interlocked.Increment(ref _configurationVersion);
            RefreshChannels();
        };
    }

    /// <summary>
    /// Gets a counter that increases on every settings save (used in the channel cache key).
    /// </summary>
    public long ConfigurationVersion => Interlocked.Read(ref _configurationVersion);

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Tag Browser";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("209c23d2-2b52-4e78-a4d7-3107e770e326");

    /// <inheritdoc />
    public override string Description => "Browse a library by its tags, as a tree. Tags like \"move: cambre\" are split on the separator into nested folders.";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace),
        };
    }

    /// <summary>
    /// Returns a file path to the built-in logo, used as a folder image fallback.
    /// The embedded image is written to the plugin's data folder on first use.
    /// </summary>
    /// <returns>The logo path, or null if it could not be written.</returns>
    public string? GetFolderLogoPath()
    {
        var path = Path.Combine(DataFolderPath, "folder-logo.png");
        if (File.Exists(path))
        {
            return path;
        }

        try
        {
            Directory.CreateDirectory(DataFolderPath);
            using var resource = GetType().Assembly.GetManifestResourceStream($"{GetType().Namespace}.Images.primary.png");
            if (resource is null)
            {
                return null;
            }

            using var file = File.Create(path);
            resource.CopyTo(file);
            return path;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Tag Browser: could not write folder logo to {Path}", path);
            return null;
        }
    }

    /// <summary>
    /// Queues Jellyfin's "Refresh Channels" task so name and image changes show up.
    /// </summary>
    private void RefreshChannels()
    {
        var worker = _taskManager.ScheduledTasks.FirstOrDefault(t =>
            string.Equals(t.ScheduledTask.Key, "RefreshChannels", StringComparison.Ordinal)
            || string.Equals(t.Name, "Refresh Channels", StringComparison.Ordinal));

        if (worker is null)
        {
            _logger.LogWarning("Tag Browser: Refresh Channels task not found; run it manually to apply changes");
            return;
        }

        _taskManager.Execute(worker, new TaskOptions());
    }
}
