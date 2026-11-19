#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
"""Add a release to manifest.json (Jellyfin plugin repository format). Plugin metadata comes from build.yaml."""
import argparse
import datetime
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[2]
IMAGE_URL = "https://raw.githubusercontent.com/milkmansson/jellyfin-plugin-tag-browser/main/Jellyfin.Plugin.TagBrowser/Images/primary.png"


def read_build_yaml():
    """Read the simple top-level 'key: value' entries from build.yaml (no YAML library needed)."""
    meta = {}
    text = (ROOT / "build.yaml").read_text(encoding="utf-8")
    for key in ("name", "guid", "targetAbi", "owner", "overview", "category"):
        m = re.search(rf'^{key}:\s*"?([^"\n]+)"?\s*$', text, re.M)
        if m:
            meta[key] = m.group(1).strip()
    desc = re.search(r"^description:\s*>\s*\n((?:\s{2,}.*\n?)+)", text, re.M)
    meta["description"] = " ".join(line.strip() for line in desc.group(1).splitlines()) if desc else meta.get("overview", "")
    return meta


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--version", required=True)
    ap.add_argument("--checksum", required=True)
    ap.add_argument("--url", required=True)
    ap.add_argument("--changelog", default="")
    args = ap.parse_args()

    meta = read_build_yaml()
    path = ROOT / "manifest.json"
    manifest = json.loads(path.read_text(encoding="utf-8")) if path.exists() else []

    plugin = next((p for p in manifest if p.get("guid") == meta["guid"]), None)
    if plugin is None:
        plugin = {"guid": meta["guid"], "versions": []}
        manifest.append(plugin)

    plugin.update({
        "name": meta["name"],
        "description": meta["description"],
        "overview": meta["overview"],
        "owner": meta["owner"],
        "category": meta["category"],
        "imageUrl": IMAGE_URL,
    })

    plugin["versions"] = [v for v in plugin["versions"] if v.get("version") != args.version]
    plugin["versions"].insert(0, {
        "version": args.version,
        "changelog": args.changelog,
        "targetAbi": meta["targetAbi"],
        "sourceUrl": args.url,
        "checksum": args.checksum,
        "timestamp": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    })

    path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"manifest.json: {meta['name']} {args.version} ({len(plugin['versions'])} version(s))")


if __name__ == "__main__":
    main()
