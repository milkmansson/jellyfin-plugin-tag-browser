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

## Known issues

- **Separate watched state.** Channel items are distinct from library items. Playing from the channel does not mark the library item watched.
- **Channel registration.** Jellyfin 12.x only discovers a channel registered via `IPluginServiceRegistrator`. Otherwise the plugin shows Active but no channel appears.
- **Refresh Channels.** After install or upgrade the channel is not listed until the Refresh Channels task has run.

## License

Copyright (C) 2026 milkmansson. GPL-3.0-or-later; see [LICENSE](LICENSE).

SPDX-License-Identifier: GPL-3.0-or-later
