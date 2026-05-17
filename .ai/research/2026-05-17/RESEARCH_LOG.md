# Research Log

Date: 2026-05-17

## Tools Used

- `rtk git log -10` / `git log -10` for current commit sequence.
- `git status --short --branch`, `git tag`, `git show`, and `gh release view` for release state.
- `rg --files`, `Select-String`, and targeted PowerShell reads for local source discovery.
- `dotnet list package --vulnerable --include-transitive` for advisory state.
- `dotnet list package --outdated` for update opportunities.
- Web search and targeted source opening for current external ecosystem, security, runtime, and model references.

## Local Reconnaissance Passes

1. Instruction pass:
   - Read root `AGENTS.md`.
   - Read root `CLAUDE.md`.
   - Read global Claude and user project instructions.
   - Loaded relevant C# stack memory.

2. Repo state pass:
   - Checked branch and worktree state.
   - Inspected recent commits.
   - Inspected current release tag and GitHub release assets.
   - Confirmed `assets/banner.png.xmp` was pre-existing unrelated untracked work.

3. Project file pass:
   - Read `README.md`, `CHANGELOG.md`, `ROADMAP.md`, and key docs.
   - Searched for alternate AI/instruction/memory files.
   - Mapped source/test/doc directories and high-level file counts.

4. Dependency pass:
   - Ran vulnerable package scan.
   - Identified SharpCompress advisory.
   - Searched local source for affected `WriteToDirectory()` usage.
   - Upgraded SharpCompress to 0.48.1 and updated local docs.
   - Re-ran vulnerable package scan and confirmed clean result.

## External Research Passes

### Direct OSS Viewer Search

Representative queries:

- `ImageGlass Windows image viewer features GitHub ImageGlass`
- `ImageGlass supported formats official ImageMagick Ghostscript`
- `PicView Windows image viewer features GitHub PicView`
- `nomacs image viewer synchronization features GitHub nomacs`
- `QuickLook Windows preview GitHub QuickLook`
- `qView image viewer minimal official GitHub qView features`

Findings:

- Broad-format viewers increasingly publish explicit format capability tables.
- Linked pan/zoom and opacity overlay are established comparison patterns in nomacs.
- Quick preview and Explorer-adjacent flows are a separate lightweight category worth supporting through command-line/shell helpers rather than bloating the main viewer.
- Minimal viewers compete on chrome discipline; Images should keep this as a constraint even as it adds power features.

### Commercial And DAM Search

Representative queries:

- `XnView MP features image viewer batch convert ratings catalog metadata official`
- `FastStone Image Viewer features batch convert compare crop resize official`
- `ACDSee Photo Studio Ultimate features AI face recognition duplicate finder official`
- `Eagle app digital asset management features image organization official`

Findings:

- Mature commercial apps cluster around management, search, batch conversion, metadata, duplicate finding, and AI assistance.
- ACDSee 2026 presents AI keywords and face recognition as organization accelerators, not standalone novelty features.
- FastStone's quality/file-size Save As comparison is a strong fit for Images' future converter/export work.
- Eagle-like library systems show the tradeoff between powerful organization and user distrust when apps copy or lock away source files.

### Adjacent Local Photo Platform Search

Representative queries:

- `digiKam features face recognition similarity search official`
- `Immich features facial recognition semantic search smart search official docs`
- `PhotoPrism features face recognition search private AI official`
- `Czkawka duplicate image finder perceptual hash official GitHub`

Findings:

- Similarity, face recognition, OCR, and semantic search require durable indexing and model lifecycle controls.
- Immich and PhotoPrism separate model inference, embeddings, clustering, and indexed search; Images should not bolt semantic search directly into the viewer.
- digiKam's database split and similarity database support the need for a deliberate catalog schema.
- Duplicate/similar-image tools show the value of explaining confidence and keeping destructive cleanup reversible.

### Runtime, Security, And Dependency Search

Representative queries:

- `SharpCompress GHSA-6c8g-7p36-r338 CVE-2026-44788 0.48.1`
- `.NET 9 support policy end of support official Microsoft`
- `Ghostscript 10.07.0 release CVE security official`
- `libjpeg-turbo 3.1.4.1 vc x64 jpegtran SHA-256`
- `NuGet Magick.NET-Q16-AnyCPU 14.13.0 release`
- `Serilog 4.3.1 release notes GitHub`

Findings:

- SharpCompress 0.47.4 is not acceptable for the vulnerability gate even though Images avoids the affected API.
- .NET 9 remains supported until 2026-11-10; .NET 10 is LTS and should be planned but not rushed.
- Ghostscript 10.07.0 is current as of the release page inspected and should stay on a monitored CVE gate.
- libjpeg-turbo 3.1.4.1 has a GitHub release asset digest for `libjpeg-turbo-3.1.4.1-vc-x64.exe`; local extraction verified `bin\jpegtran.exe` SHA-256 before staging policy was updated.
- Magick.NET 14.13.0 is current in this repo and recent enough to avoid the older vulnerable floors shown by NuGet.

### Models, Datasets, And Integrations Search

Representative queries:

- `OpenCLIP local image search embeddings`
- `SigLIP image text embeddings Hugging Face docs`
- `OpenModelDB image upscaling models`
- `Real-ESRGAN GitHub super resolution`
- `BiRefNet background removal GitHub`
- `LaMa ONNX inpainting Hugging Face`
- `sqlite-vec vector search SQLite extension`

Findings:

- Model-backed features need a common model registry: ID, version, source URL, license, SHA-256, size, runtime, downloaded/imported state, and delete controls.
- Semantic search should come after catalog schema and background job infrastructure.
- Inpaint/upscale/background removal should use opt-in local runtimes and avoid automatic downloads.
- Embeddings and face data are derived user data and need explicit delete/rebuild controls.

## Failed Or Thin Searches

- Exact "latest" activity signals for every viewer were not normalized into a single star/release table. That would require a deeper GitHub API pass and is lower value than feature and architecture extraction.
- Some community search results were noisy or low-trust. They were used only as weak signal and not as primary roadmap evidence.
- Commercial product pages are marketing-heavy. Features were used only when the page itself clearly claimed a concrete capability.

## Saturation Test

The research reached saturation when additional sources repeated the same opportunity clusters:

- Format capability transparency.
- Linked compare/overlay.
- Local catalog and metadata workflows.
- Duplicate/similarity cleanup with reversible actions.
- Batch/export quality comparison.
- Model lifecycle and opt-in local inference.
- Color management and large-image handling.
- Distribution trust, signing, and runtime provenance.

The final roadmap prioritizes these repeated clusters only when they fit the current Images architecture and shipped state.
