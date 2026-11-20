<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="images/logo-white.png">
    <img src="images/logo-black.png" alt="Tag Browser logo" width="160">
  </picture>
</p>

# jellyfin-plugin-tag-browser
Plugin allowing browsing a Jellyfin library using tags/metadata.

> [!WARNING]
> **Status:** Early development. Please try this out and leave us an issue with any bugs or features.

## How it works

Adds a **Tag Browser** channel. Each tag becomes a folder, with all similar videos available under that folder.  Videos with multiple tags will appear under all viable tags.
Tags containing the separator (default `:`) are split into nested folders:

| Tag | Path in the channel |
|---|---|
| `to practise` | `to practise` |
| `move: cambre` | `move` → `cambre` |
| `a: b: c` | `a` → `b` → `c` |

A video with several tags appears in each matching folder.

Other branches can be enabled alongside Tags:

| Branch | Built from | Example path |
|---|---|---|
| Date | `<premiered>`, else the date recorded in the file, else a date in the file name | `Date` → `2022` → `2022-03 March` → `2022-03-20` |
| Folders | Library folder structure | `Folders` → `Zouk` |

### Property branches

Any metadata property can become its own top-level folder, listing every distinct value beneath it. In the settings, add a row to the **Property branches** table: the property name (not case-sensitive) and, optionally, the name shown in the channel. The table is empty by default.

| Property | Built from | Example path |
|---|---|---|
| `genre` | `<genre>` | `Genre` → `Brazilian Zouk` |
| `studio` | `<studio>` | `Studio` → `Inflow` |
| `actor`, `director`, `writer`, … | `<actor>`, `<director>`, … (any Jellyfin person role) | `Actor` → `Anna` |
| `people` | every person, any role | `People` → `Anna` |
| `year` | `<year>` | `Year` → `2022` |
| `rating` | `<mpaa>` | `Rating` → `PG` |
| `country` | `<country>` | `Country` → `Brazil` |

Only what Jellyfin imports from the NFO is available; elements Jellyfin ignores cannot be grouped by. Tags have their own branch above, so `tag` is not a property. Unknown names show nothing. A video with several values appears under each.

Tags only lists tagged videos; the other branches list all videos of the included libraries.

## Requirements

- Jellyfin **12.4.0**.
  - Plugins are tied to the server version. Pin your Jellyfin version; update the plugin when upgrading Jellyfin.
  - Tested with `lscr.io/linuxserver/jellyfin:12.4ubu2604-ls60`.

## Install

1. **Dashboard → Plugins → Repositories → +**, add:
   `https://raw.githubusercontent.com/milkmansson/jellyfin-plugin-tag-browser/main/manifest.json`
2. **Catalog → Tag Browser → Install**, then restart Jellyfin.
3. Open the plugin settings and **Save** once (this runs Refresh Channels).

## Configuration

**Dashboard → Plugins → Tag Browser.** Saving runs Refresh Channels automatically.

| Setting | Default | Notes |
|---|---|---|
| Channel name | `Tag Browser` | Renaming creates a new channel; watched state of its items resets. |
| Tag separator | `:` | Matched exactly. Empty = no nesting. |
| Libraries | All | Only videos from ticked libraries appear. |
| Tags at the top level | On | Tags appear alongside the other branches. Off: grouped under the Tags branch. |
| Branches | Tags, Date | Tick to enable; each has a name. |
| Property branches | none | Table of properties to group by (see above). |
| Date branch depth | Year › Month › Day | Or: year, year › month. |
| Date from file name | On | Used when the video has no date (`20220213_180246`, `VID-20250924-WA0005`). |
| Prefer file-name date | Off | Use the file-name date instead of the recorded time (which is UTC). |
| Undated folder | `Undated` | Videos without any date. Empty = hidden. |
| Folder image | Video thumbnail | Thumbnail of a video in the folder (logo if none). Or: logo, none. |
| Folder thumbnail taken from | Random | A random video per folder (fixed, not changing per visit). Or: first by title, newest, oldest, most recently added. |
| Tile / wide tile / background image | Built-in | Optional server path to a PNG/JPEG. |

## Known issues

- **Alphabetical only.** Jellyfin sorts channel folders and videos by name; the plugin cannot set the order.
- **No library view options.** Jellyfin shows channels without the library's grid/list switch and filters.
- **Separate watched state.** Channel items are distinct from library items; playing from the channel does not mark the library item watched. Each folder placement also has its own watched state.
- **`<premiered>` must be a date only** (`YYYY-MM-DD`). With a time, Jellyfin ignores the field.
- **Recorded times are UTC**, shown in the server's time zone; videos recorded far away can land on the neighbouring day. Use *Prefer file-name date* or set `<premiered>`.
- **Custom image paths** are read on the server. A missing file falls back to the built-in artwork.

## Differences from the plugin template

Based on [jellyfin-plugin-template](https://github.com/jellyfin/jellyfin-plugin-template) (layout, `build.yaml`, `.editorconfig`, analyzers, warnings as errors). Differences:

- **Targets Jellyfin 12.4 / .NET 10** (template: 10.11 / .NET 9).
- **Own release workflow.** Publishes a GitHub release and `manifest.json`; the template's publish workflow deploys to Jellyfin's repository and only works for official plugins.
- **Unit tests** (`Jellyfin.Plugin.TagBrowser.Tests`).
- **No Renovate or VS Code settings.**

## License

Copyright (C) 2026 milkmansson. GPL-3.0-or-later; see [LICENSE](LICENSE).

SPDX-License-Identifier: GPL-3.0-or-later
