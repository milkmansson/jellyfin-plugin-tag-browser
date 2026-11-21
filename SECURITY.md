# Security policy

## Scope

Tag Browser is a plugin that runs inside the Jellyfin server. This policy covers vulnerabilities **in the plugin's own code**, for example:

- the plugin reading or exposing files, library items or settings it should not (including through the custom image path settings);
- the plugin showing a user videos, folders or channels that Jellyfin's own permissions and library access would hide from them;
- unsafe handling of settings or metadata values (injection, path traversal, crashes triggered by crafted tags, NFO content or property names);
- a flaw in the release process, such as the build workflow, the release ZIP or `manifest.json`.

**Out of scope here:**

- Vulnerabilities in Jellyfin itself, its web client, its authentication, other plugins or the operating system. These go to the relevant project. Jellyfin's own process is at <https://github.com/jellyfin/jellyfin/security/policy>.
- Problems that need an attacker who already has Jellyfin administrator access. Administrators can configure plugins, install other plugins and read the server's files.
- Issues caused by running an unsupported or mismatched Jellyfin version (see below).
- The known limitations listed in the README, such as separate watched state for channel items.

If it is unclear whether a problem is in the plugin or in Jellyfin, a report here is still welcome. It will be passed on if it belongs upstream. Reproducing it with the plugin disabled helps to tell the two apart.

## Supported versions

Only the **latest release** is supported, running on the Jellyfin version stated in the README (currently **12.4.0**). Plugins are tied to the server version, so fixes are not backported to older releases.

## Reporting a vulnerability

Please **do not open a public issue** for a security problem.

Report it privately through GitHub: on the repository, open **Security** → **Report a vulnerability**
(<https://github.com/milkmansson/jellyfin-plugin-tag-browser/security/advisories/new>).

Useful details:

- the plugin version and the Jellyfin version;
- the relevant plugin settings (branches, property rows, separate channels, image paths);
- the user role needed to trigger it (anonymous, regular user or administrator);
- the steps to reproduce, and what was expected versus what happened;
- the impact as understood, and any suggested fix.

Please keep real library contents, credentials and API keys out of reports. A minimal example is enough.

## What to expect

This is a small, volunteer-maintained project. Reports are handled on a best-effort basis:

- an acknowledgement within about a week;
- a fix or a decision on a confirmed issue as soon as practical, followed by a new release and a published advisory;
- credit in the advisory, unless the reporter prefers to stay anonymous.

Please allow time for a fix before disclosing the issue publicly.
