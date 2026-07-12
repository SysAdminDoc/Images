# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, GUI/visual validation, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

The feature set already rivals ImageGlass and nomacs. What's missing for a `1.0` label is release infrastructure and quality gates, not features. These items define the boundary:

1. Code signing (D-05 in Roadmap_Blocked.md — needs Azure Artifact Signing credentials)
2. WinGet + Scoop publication (D-02 in Roadmap_Blocked.md — needs first manual submission)

Promote to `1.0.0` when those are unblocked.

## Research-Driven Additions

Net-new, evidence-grounded, code-ready items from the 2026-07-12 (pass 2) research. The feature backlog is otherwise drained; larger bets remain in `Roadmap_Blocked.md`.

### P2 — reliability / distribution

- [ ] P2 — Serialize timing-sensitive tests to stop parallel-load flakes
  Why: `CodecRuntimeTests.RunVersionProbe_DrainsStderrWhileWaiting` and `UpdateCheckServiceTests.CheckAsync_WhenContentLengthIsUnknown_RecordsActualBytesRead` intermittently fail only under full-suite CPU saturation (process-spawn stdout/stderr drain timing and HTTP stream byte-count timing); they pass isolated. A green build can flip red on unrelated PRs.
  Evidence: first-hand test runs this session (2 failures on a full run, 0 on a clean re-run / isolated); xUnit docs (config-xunit-runner-json, running-tests-in-parallel); `tests/Images.Tests/CodecRuntimeTests.cs:44`, `tests/Images.Tests/UpdateCheckServiceTests.cs:49`.
  Touches: new `[CollectionDefinition("Timing-Sensitive", DisableParallelization = true)]` + `[Collection("Timing-Sensitive")]` on the process-spawn/stream-timing classes (CodecRuntimeTests, UpdateCheckServiceTests, and any other child-process/stream-timing class); optionally add `tests/Images.Tests/xunit.runner.json` (`parallelAlgorithm: "conservative"`, a `maxParallelThreads` cap) with `CopyToOutputDirectory=PreserveNewest` in the csproj.
  Acceptance: the full `dotnet test Images.sln -c Release` suite passes green across 10 consecutive runs with no intermittent timing failures.
  Complexity: S

- [ ] P2 — Cut the v0.2.26 release from the accumulated Unreleased CHANGELOG
  Why: ~29 lines of shipped user-facing features (opt-in color management, loupe, live pixel readout, zoom-lock, transparency checkerboard, zoom-to-selection, session restore, stop-at-ends, metadata-preserving Save-a-copy, Magick.NET 14.15 security bump) sit unreleased since v0.2.25 with no version cut. The unsigned ZIP/installer path is not gated on signing.
  Evidence: `CHANGELOG.md` `## Unreleased` section; `scripts/Test-ReleaseReadiness.ps1` / local release scripts; git log since `45494cb` (release Images 0.2.25).
  Touches: version strings (`src/Images/Images.csproj`, `app.manifest`, installer defaults, README badge), `CHANGELOG.md` (promote Unreleased → `v0.2.26 - <date>`), run release-readiness gates (version sync, tests, vulnerable/deprecated scan, localization parity, provenance docs, package-manifest hashes, WinGet/Scoop manifest validation), tag + GitHub Release with the unsigned artifacts.
  Acceptance: all version strings match 0.2.26, release-readiness script passes, a GitHub Release v0.2.26 exists with the portable ZIP + installer attached and downloadable.
  Complexity: M

### P3 — dependency currency

- [ ] P3 — Bump Serilog 4.3.1 → 4.4.0
  Why: One routine minor behind (4.4.0 released 2026-07-10); keeps the logging dependency current. No security advisory — low priority, but a clean quick win alongside the release cut.
  Evidence: https://www.nuget.org/packages/Serilog/ ; `src/Images/Images.csproj` pins `Serilog` 4.3.1.
  Touches: `src/Images/Images.csproj` (Serilog PackageReference); verify build + `dotnet list --deprecated/--vulnerable` clean; Serilog.Extensions.Logging/Sinks.File stay on their current pins unless a resolution conflict appears.
  Acceptance: Serilog resolves to 4.4.0, solution builds, full suite green, vulnerable/deprecated scans clean.
  Complexity: S
