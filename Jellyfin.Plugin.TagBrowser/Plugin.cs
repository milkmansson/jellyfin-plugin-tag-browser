// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Jellyfin.Plugin.TagBrowser.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.TagBrowser;

/// <summary>
/// The Tag Browser plugin: exposes library tags as a browsable channel tree.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

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
}
