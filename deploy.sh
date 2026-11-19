#!/usr/bin/env bash
# SPDX-License-Identifier: GPL-3.0-or-later
#
# Build the plugin in the official .NET SDK container and install it into a
# local (podman) Jellyfin, then restart Jellyfin so it loads the new build.
#
#   ./deploy.sh            build + install + restart
#   ./deploy.sh --build    build only
#
# Override paths with env vars if your setup differs:
#   JELLYFIN_PLUGINS   Jellyfin's plugins dir on the host
#   JELLYFIN_CONTAINER container name to restart
#   JELLYFIN_PLUGINS_IN_CONTAINER the same plugins dir as seen inside the container
set -euo pipefail

REPO="$(cd "$(dirname "$0")" && pwd)"
PROJECT="Jellyfin.Plugin.TagBrowser"
SDK_IMAGE="mcr.microsoft.com/dotnet/sdk:10.0"
JELLYFIN_PLUGINS="${JELLYFIN_PLUGINS:-/var/lib/service/containers/jellyfin/config/data/plugins}"
JELLYFIN_CONTAINER="${JELLYFIN_CONTAINER:-jellyfin}"
JELLYFIN_PLUGINS_IN_CONTAINER="${JELLYFIN_PLUGINS_IN_CONTAINER:-/config/data/plugins}"

NAME="$(sed -nE 's/^name: *"?([^"]+)"?$/\1/p' "$REPO/build.yaml")"
VERSION="$(sed -nE 's/^version: *"?([^"]+)"?$/\1/p' "$REPO/build.yaml")"
GUID="$(sed -nE 's/^guid: *"?([^"]+)"?$/\1/p' "$REPO/build.yaml")"
ABI="$(sed -nE 's/^targetAbi: *"?([^"]+)"?$/\1/p' "$REPO/build.yaml")"
OUT="$REPO/artifacts/$PROJECT"

echo ">> building $NAME $VERSION"
podman run --rm \
  -v "$REPO:/src:Z" -v tagbrowser-nuget:/root/.nuget/packages \
  -w /src "$SDK_IMAGE" \
  dotnet publish "$PROJECT/$PROJECT.csproj" -c Release -o "/src/artifacts/$PROJECT" -p:Version="$VERSION" --nologo -v quiet

[ "${1:-}" = "--build" ] && { echo ">> built: $OUT/$PROJECT.dll"; exit 0; }

DEST="$JELLYFIN_PLUGINS/${NAME}_${VERSION}"
echo ">> installing to $DEST"
find "$JELLYFIN_PLUGINS" -maxdepth 1 -type d -name "${NAME}_*" ! -path "$DEST" -exec rm -rf {} +
mkdir -p "$DEST"
cp "$OUT/$PROJECT.dll" "$DEST/"
cp "$REPO/$PROJECT/Images/primary.png" "$DEST/logo.png"
cat > "$DEST/meta.json" <<JSON
{
  "category": "General",
  "changelog": "local build",
  "description": "Browse a library by its tags, as a tree.",
  "guid": "$GUID",
  "name": "$NAME",
  "overview": "Browse a library by its tags, as a tree",
  "owner": "milkmansson",
  "targetAbi": "$ABI",
  "timestamp": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "version": "$VERSION",
  "status": "Active",
  "autoUpdate": false,
  "imagePath": "$JELLYFIN_PLUGINS_IN_CONTAINER/${NAME}_${VERSION}/logo.png",
  "assemblies": []
}
JSON

echo ">> restarting $JELLYFIN_CONTAINER"
podman restart "$JELLYFIN_CONTAINER" >/dev/null
echo ">> done"
