# Release support policy

How long any given Images release is supported, what "supported" means in practice, and how breaking-change windows are signalled.

## What is supported

For any version published to [Releases](https://github.com/SysAdminDoc/Images/releases):

- **Latest minor** — receives bug fixes, security fixes, and feature work. The current `0.1.x` line is on this footing.
- **Previous minor** — receives security fixes only, until the next minor lands. Once `0.2.0` ships, `0.1.x` drops to "no further work" status.
- **All older versions** — best-effort; no fixes back-ported, no compatibility guarantees.

There is no LTS line yet; one will be considered when Images leaves `0.1.x`.

## Servicing surface

- **NuGet packages**: monitored by Dependabot weekly and the [Security workflow](../.github/workflows/security.yml) daily. Vulnerabilities flagged by `dotnet list package --vulnerable --include-transitive` block the next release.
- **Bundled native runtimes** (Magick.NET native pack, optional Ghostscript): tracked by version + SHA-256 in the runtime provenance surface. The SHA-256 of `gsdll64.dll` is shown in About → Runtime provenance and `Images.exe --system-info`. Replacement only after a license + CVE review.
- **.NET runtime**: pinned to `net9.0-windows`. Move to a newer LTS will be staged behind a roadmap decision record (see ROADMAP item 84).

## Breaking change policy

- Settings/cache schema migrations are **forward-only and hop-by-hop** (see `SettingsService` and ROADMAP SCH-04). A v1→v5 user upgrades through v1→v2→v3→v4→v5 with integrity checks at each hop.
- Local caches (`thumbs/`, `Logs/`, `update-check.json`) are **disposable**. Any release may rebuild them; deleting them is always a safe recovery.
- File formats produced by export are not silently changed. Quality/encoding regressions are documented in CHANGELOG.

## Reporting

- Bugs / feature requests: <https://github.com/SysAdminDoc/Images/issues>
- Security advisories: open a private vulnerability report through GitHub. Please do not file public issues for live security defects.

## Distribution channels

The official release surface is [GitHub Releases](https://github.com/SysAdminDoc/Images/releases). Both the portable ZIP and the Inno Setup installer are uploaded to the same release; both are byte-for-byte identical builds. Distributions outside that surface (mirrors, package-manager-of-the-week) are unsupported until they appear in this list.
