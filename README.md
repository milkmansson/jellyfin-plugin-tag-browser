<img src="images/logo.png" alt="Tag Browser logo" width="128">

# jellyfin-plugin-tag-browser
Plugin allowing browsing a Jellyfin library using tags/metadata.

## Status

Early development. Tags are shown as a folder tree; videos only.

## How it works

Adds a **Tag Browser** channel. Each tag becomes a folder.
Tags containing the separator (default `:`) are split into nested folders:

| Tag | Path in the channel |
|---|---|
| `to practise` | `to practise` |
| `move: cambre` | `move` → `cambre` |
| `a: b: c` | `a` → `b` → `c` |

## Configuration

**Dashboard → Plugins → Tag Browser.** Saving runs Refresh Channels automatically.

| Setting | Default | Notes |
|---|---|---|
| Channel name | `Tag Browser` | Jellyfin derives the channel id from the name. Renaming creates a new channel; items' watched state resets. |
| Tag separator | `:` | Matched exactly. Empty = no nesting. |
| Tile / wide tile / background image | built-in | Optional server path to a PNG/JPEG. |

## Requirements

- Jellyfin **12.4.0** (.NET 10). Built against `Jellyfin.Controller` 12.4.0.
- Plugins are tied to the server version. Pin the Jellyfin image; rebuild the plugin when upgrading.
- Tested with `lscr.io/linuxserver/jellyfin:12.4ubu2604-ls60`.

## Install (manual)

1. Build (below).
2. Copy `Jellyfin.Plugin.TagBrowser.dll` to `<jellyfin config>/data/plugins/Tag Browser_<version>/`.
3. Restart Jellyfin.
4. Run **Dashboard → Scheduled Tasks → Refresh Channels**.

## Build

Requires podman. Builds in `mcr.microsoft.com/dotnet/sdk:10.0`.

```bash
./deploy.sh --build   # build only → artifacts/Jellyfin.Plugin.TagBrowser/
./deploy.sh           # build, install into a local podman Jellyfin, restart it
```

Defaults (override via env): `JELLYFIN_PLUGINS=/var/lib/service/containers/jellyfin/config/data/plugins`, `JELLYFIN_CONTAINER=jellyfin`.

## Artwork

`images/logo.svg` is the source (single colour, editable). `images/render.sh` regenerates all PNGs (needs podman).

## Known issues

- **Separate watched state.** Channel items are distinct from library items. Playing from the channel does not mark the library item watched.
- **Channel registration.** Jellyfin 12.x only discovers a channel registered via `IPluginServiceRegistrator`. Otherwise the plugin shows Active but no channel appears.
- **Refresh Channels.** After install or upgrade the channel is not listed until the Refresh Channels task has run.
- **Custom image paths** are read on the server. A missing file falls back to the built-in artwork (logged as a warning).

## License

Copyright (C) 2026 milkmansson. GPL-3.0-or-later; see [LICENSE](LICENSE).

SPDX-License-Identifier: GPL-3.0-or-later
