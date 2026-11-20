// SPDX-License-Identifier: GPL-3.0-or-later
using Jellyfin.Plugin.TagBrowser.Channels;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.TagBrowser;

/// <summary>
/// Registers the plugin's services with Jellyfin's dependency injection container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IChannel, TagBrowserChannel>();

        // Jellyfin fixes its channel list at startup, so register spare channels now; each stays hidden until the
        // settings assign it a branch to show.
        for (var i = 1; i <= ChannelPlan.MaxPromoted; i++)
        {
            var slot = i;
            serviceCollection.AddSingleton<IChannel>(sp => ActivatorUtilities.CreateInstance<PromotedTagChannel>(sp, slot));
        }
    }
}
