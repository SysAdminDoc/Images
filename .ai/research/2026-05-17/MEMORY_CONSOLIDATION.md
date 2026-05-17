# Memory Consolidation

Date: 2026-05-17

## Files And Memory Sources Inspected

Repository-local:

- `AGENTS.md`
- `CLAUDE.md`
- `README.md`
- `CHANGELOG.md`
- `ROADMAP.md`
- `docs/improvement-plan.md`
- `docs/design-product-differentiators.md`
- `docs/inpaint-runtime-decision.md`
- `docs/archive-runtime-review.md`
- `docs/integration-policy.md`
- `.factory/state.json`
- `.claude-octopus/*` directory presence

Shared/global instructions:

- `C:\Users\--\.claude\CLAUDE.md`
- `C:\Users\--\CLAUDE.md`
- `C:\Users\--\.claude\projects\c--Users----repos\memory\MEMORY.md`
- `C:\Users\--\.claude\projects\c--Users----repos\memory\projects\images-viewer.md`
- `C:\Users\--\.claude\projects\c--Users----repos\memory\stack-csharp.md`
- `C:\Users\--\.codex\memories\MEMORY.md`

Agent-file search covered:

- `AGENTS.md`
- `CLAUDE.md`
- `.claude/**`
- `.claude-instructions`
- `.cursor/rules/**`
- `.cursorrules`
- `.windsurfrules`
- `GEMINI.md`
- `COPILOT_INSTRUCTIONS.md`
- `.github/copilot-instructions.md`
- `.ai/**`
- `memory*.md`
- `context*.md`
- `project*.md`
- `notes*.md`
- `TODO*`
- `ROADMAP*`
- `CHANGELOG*`
- `ARCHITECTURE*`
- `CONTRIBUTING*`

Only root `AGENTS.md`, root `CLAUDE.md`, root `CHANGELOG.md`, and root `ROADMAP.md` matched in the tracked tree before this run.

## Durable Project Facts Extracted

- Images is a Windows-only WPF/.NET application, currently targeting `net9.0-windows10.0.22621.0`.
- The app uses Catppuccin Mocha styling and a classic Windows Photo Viewer-inspired image-first layout.
- Image decode is WIC-first with Magick.NET fallback.
- Optional document previews use Ghostscript, currently bundled in release artifacts as Ghostscript 10.07.0.
- Settings/cache/logs are local and disposable under app storage.
- The repo uses SQLite for local app state.
- Release artifacts include a portable zip and an Inno Setup installer.
- Release policy requires version sync and vulnerable package gates.
- The project has broad shipped functionality beyond the older viewer-only roadmap: archive books, gallery, cleanup, file health, batch, macro, import inbox, OCR, edit stack, and flat-raster crop/writeback.

These facts are now consolidated in root `PROJECT_CONTEXT.md`.

## Plans Extracted Into The Roadmap

The new `ROADMAP.md` v7 section emphasizes:

- Roadmap/status hygiene.
- Dependency/runtime provenance.
- Settings/accessibility IA.
- Approved jpegtran artifact staging.
- Compare/overlay mode.
- Catalog schema v1.
- Local model/runtime foundation before semantic search or generative tools.
- Color management and large-image/deep-zoom architecture.
- Distribution trust and package-manager expansion.

## Resolved Or Reframed Conflicts

| Conflict | Resolution |
| --- | --- |
| `ROADMAP.md` v6 says no editor, organizer, or batch processor. | Reframed as stale. Current `README.md`, `CHANGELOG.md`, and `docs/improvement-plan.md` show many editor, organizer, cleanup, gallery, and batch workflows already shipped. |
| Shared `.claude` memory describes older Images state around `v0.1.2`. | Treated as historical. Live repo, git log, README, changelog, and current docs are canonical. |
| Global instruction discourages adding agent/meta references to repo docs. | The user explicitly required `.ai/research` artifacts, project memory consolidation, and future-session handoff files for this run. The user-specific task takes priority. |
| Global recipe says tests are not needed unless explicitly requested. | This task explicitly required self-audit and evidence-backed completion; build/test/security verification is appropriate. |
| `AGENTS.md` points to `CLAUDE.md`, while both are local/tool-specific guidance in this checkout. | Preserved the tool-specific files as local guidance and put the committed canonical context in `PROJECT_CONTEXT.md`. |

## Open Conflicts

- `CHANGELOG.md` has `v0.1.8` and `v0.1.9` dated 2026-06-02, after the current date 2026-05-17. This should be corrected only after checking tags, commits, and release history.
- The project continues to use `.NET 9` while `.NET 10` is now LTS according to the official .NET support policy. This is not a defect because .NET 9 remains supported until 2026-11-10, but a migration decision should be scheduled.
- The roadmap includes historical source IDs from prior research. They are useful as context, but future sessions should refresh source freshness before making new "latest" claims.

## Canonical Memory Outcome

Root `PROJECT_CONTEXT.md` is now the short, durable memory file for future sessions. Root `ROADMAP.md` v7 is the current planning artifact. The older roadmap and docs remain useful as source archives and should not be deleted.
