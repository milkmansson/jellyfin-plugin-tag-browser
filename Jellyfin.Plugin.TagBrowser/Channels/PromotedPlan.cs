// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;

namespace Jellyfin.Plugin.TagBrowser.Channels;

/// <summary>
/// A branch promoted to its own channel.
/// </summary>
/// <param name="Name">The channel name.</param>
/// <param name="Root">The tree path the channel opens at.</param>
public sealed record PromotedPlan(string Name, IReadOnlyList<string> Root);
