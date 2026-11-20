#!/usr/bin/env bash
# SPDX-License-Identifier: GPL-3.0-or-later
# Regenerate PNG artwork from images/logo.svg (needs podman). The SVG is the source; edit it, then run this.
#   images/logo-black.png                   512x512   black on transparent (README, light theme)
#   images/logo-white.png                   512x512   white on transparent (README, dark theme)
#   Jellyfin.Plugin.TagBrowser/Images/
#     primary.png                           512x512   white on transparent (channel tile, plugin logo)
#     thumb.png                             1280x720  white on black (wide tile)
#     backdrop.png                          1920x1080 faint logo on black (channel background)
set -euo pipefail
cd "$(dirname "$0")/.."
OUT=Jellyfin.Plugin.TagBrowser/Images
TMP=$(mktemp -d); trap 'rm -rf -- "${TMP:?}"' EXIT
BODY=$(sed '1d;$d' images/logo.svg)                 # logo without the outer <svg> element
WHITE=$(printf '%s' "$BODY" | sed 's/#000/#fff/g')  # same drawing in white

cat > "$TMP/white.svg" <<SVG
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="512" height="512">$WHITE
</svg>
SVG
cat > "$TMP/thumb.svg" <<SVG
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1280 720" width="1280" height="720">
  <rect width="1280" height="720" fill="#000"/>
  <svg x="370" y="90" width="540" height="540" viewBox="0 0 512 512">$WHITE</svg>
</svg>
SVG
cat > "$TMP/backdrop.svg" <<SVG
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1920 1080" width="1920" height="1080">
  <rect width="1920" height="1080" fill="#000"/>
  <svg x="1040" y="90" width="900" height="900" viewBox="0 0 512 512" opacity="0.16">$WHITE</svg>
</svg>
SVG

podman run --rm -v "$PWD:/w:Z" -v "$TMP:/t:Z" docker.io/library/alpine:latest sh -c "
  apk add -q --no-progress rsvg-convert >/dev/null
  rsvg-convert -w 512  -h 512  /w/images/logo.svg -o /w/images/logo-black.png
  rsvg-convert -w 512  -h 512  /t/white.svg        -o /w/images/logo-white.png
  cp /w/images/logo-white.png /w/$OUT/primary.png
  rsvg-convert -w 1280 -h 720  /t/thumb.svg        -o /w/$OUT/thumb.png
  rsvg-convert -w 1920 -h 1080 /t/backdrop.svg     -o /w/$OUT/backdrop.png"
echo "rendered: images/logo-{black,white}.png, $OUT/{primary,thumb,backdrop}.png"
