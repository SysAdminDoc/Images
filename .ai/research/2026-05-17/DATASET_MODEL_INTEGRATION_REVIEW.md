# Dataset, Model, And Integration Review

Date: 2026-05-17

## Relevance

This project has a meaningful model/integration angle. Current and planned features include OCR, local semantic search, inpaint/content-aware repair, background removal, super-resolution, duplicate/similarity, and metadata/catalog workflows. The review is therefore not thin.

## Current State

Shipped or documented:

- OCR uses Windows.Media.Ocr and installed Windows OCR capabilities.
- Duplicate cleanup includes exact SHA-256 grouping and perceptual similarity.
- Gallery smart filters use local metadata/sidecar signals.
- `docs/inpaint-runtime-decision.md` chooses an opt-in local LaMa ONNX direction for future content-aware repair.
- `docs/design-product-differentiators.md` scopes semantic search to local indexing, an embedding provider abstraction, SQLite records, and no automatic model download.
- `ModelManagerService` and `ModelManagerWindow` now provide the V7-30 shared model manager foundation: approved local model definitions, app-local grouped storage, manual ONNX import/delete/reveal controls, pinned SHA-256 validation, runtime readiness copy, and diagnostics provenance rows.
- `SemanticSearchService` and `SemanticSearchWindow` now provide the first V7-31 foundation: selected-folder indexing, app-local `semantic-index.db`, `ISemanticEmbeddingProvider`, deterministic offline metadata embeddings, exact cosine search, folder filtering, cancellation, result open/reveal, and delete-index controls.

Not present yet:

- Approved ONNX image/text embedding provider.
- Face/person recognition.
- Background removal.
- Super-resolution.
- Bundled AI model artifacts.
- Model inference package/runtime execution.

## Model Manager Requirement

V7-30 implemented the shared model/runtime registry foundation with:

- Model ID and display name.
- Source URL.
- License summary.
- Version, revision, or exact artifact filename where available.
- SHA-256.
- Size.
- Runtime: Windows ML, ONNX Runtime DirectML, CPU fallback, or external executable.
- Storage path under app-local data.
- Download/import status.
- Hardware/runtime compatibility status.
- Last validation/import result.
- Delete/rebuild controls.
- No automatic network call without explicit user action.

The current manager feeds About diagnostics, CLI reports, and future feature enablement. Settings integration, user-initiated download logging, license/readme retention, and real runtime validation remain future work.

## Candidate Feature Paths

### Semantic Search

Candidate models/sources:

- OpenCLIP: https://github.com/mlfoundations/open_clip
- SigLIP docs: https://huggingface.co/docs/transformers/model_doc/siglip
- Qdrant CLIP ViT-B/32 text ONNX candidate: https://huggingface.co/Qdrant/clip-ViT-B-32-text
- Qdrant CLIP ViT-B/32 vision ONNX candidate: https://huggingface.co/Qdrant/clip-ViT-B-32-vision

Architecture:

- Build catalog schema first. Done under V7-12.
- Add embedding provider interface. Done under the V7-31 foundation.
- Store file fingerprint, model ID, embedding version, dimensions, and indexed timestamp.
- Start with exact cosine search for small libraries. Done for the deterministic local provider.
- Evaluate sqlite-vec only after embedding shape and catalog update rules are stable.
- Model manager now pins Qdrant text ONNX SHA-256 `4dbe762b11e36488304471e439cde89da053ad7acaddbf9e096745d142ec8d8b` and vision ONNX SHA-256 `c68d3d9a200ddd2a8c8a5510b576d4c94d1ae383bf8b36dd8c084f94e1fb4d63`; tokenizer/preprocessor sidecar handling is still required before runtime enablement.

User controls:

- Select folders to index.
- Pause/cancel/rebuild index.
- Delete embeddings.
- Show model/runtime status.
- Explain that embeddings are local derived data.

### Inpaint / Content-Aware Repair

Existing decision:

- Primary candidate: https://huggingface.co/opencv/inpainting_lama
- Approved primary file: `inpainting_lama_2025jan.onnx`, SHA-256 `7df918ac3921d3daf0aae1d219776cf0dc4e4935f035af81841b40adcf74fdf2`
- Fallback validation candidate: https://huggingface.co/Carve/LaMa-ONNX
- Approved fallback file: `lama_fp32.onnx`, SHA-256 `1faef5301d78db7dda502fe59966957ec4b79dd64e16f03ed96913c7a4eb68d6`
- Original project: https://github.com/advimman/lama

Implementation constraints:

- Windows ML first, ONNX Runtime DirectML fallback.
- No bundled model by default.
- No automatic download.
- Store model hash in the XMP edit operation or patch provenance.
- Output should be non-destructive first.

### Background Removal

Candidate sources:

- BiRefNet: https://github.com/ZhengPeng7/BiRefNet
- rembg: https://github.com/danielgatis/rembg
- U-2-Net: https://github.com/xuebinqin/U-2-Net

Recommendation:

- Treat as an export/edit operation after model manager and catalog are stable.
- Avoid Python/runtime bundling. Prefer ONNX-compatible local inference if implemented.
- Provide mask preview and manual correction before export.

### Super-Resolution

Candidate sources:

- OpenModelDB: https://openmodeldb.info/
- Real-ESRGAN: https://github.com/xinntao/Real-ESRGAN
- Upscayl workflow reference: https://github.com/upscayl/upscayl

Recommendation:

- Defer until model manager and batch/export UX are mature.
- Require output size/time estimate and GPU/CPU fallback messaging.
- Keep outputs as copies by default.

### Face Recognition

External references:

- Immich face recognition: https://docs.immich.app/features/facial-recognition
- PhotoPrism face recognition: https://docs.photoprism.app/user-guide/ai/face-recognition/
- digiKam features: https://www.digikam.org/about/features/

Recommendation:

- Defer. Face data is sensitive derived user data and requires explicit consent, delete controls, clustering review, false-positive handling, and export/import policy.
- If implemented later, separate detection, embedding, clustering, and labeling so each can be rebuilt and audited.

## Dataset And Evaluation Strategy

Use generated and opt-in test corpora rather than checked-in personal media:

- Generated codec corpus for PNG/JPEG/WebP/TIFF/GIF/APNG/SVG/PDF-like fixtures where feasible.
- Synthetic duplicate/similarity sets with known transforms.
- Synthetic OCR text images with known boxes and expected text.
- Small public-domain images only when license and source are recorded.
- No personal photo corpora in the repo.

For model features:

- Keep a tiny deterministic test seam with fake providers.
- Validate real model execution only in optional/manual integration tests.
- Record model hash, runtime, device, elapsed time, and output dimensions.
- Add golden-output tolerances rather than exact pixel matches when runtime kernels differ.

## Integration Policy

Every new external integration should pass the existing `docs/integration-policy.md` template:

- Name/version.
- Source URL.
- License.
- Redistribution permission.
- CVE/advisory search.
- File access boundary.
- Network behavior.
- Failure mode.
- Test gate.
- Removal/disable path.

For models, add:

- Model card URL.
- Dataset/training limitations if known.
- Input/output tensor shape.
- Hardware requirements.
- Derived-data storage and delete controls.

## Recommended Sequence

1. Catalog schema v1. Done under V7-12.
2. Model manager/runtime registry. Registry/import/status foundation done under V7-30; runtime execution still pending.
3. Fake-provider tests and CLI diagnostics for embedding providers. Service tests and UI exist; CLI diagnostics remain optional.
4. Local embedding index MVP. Deterministic local foundation exists; approved ONNX embeddings remain open.
5. Semantic search UI. Foundation window exists.
6. Inpaint/background/upscale workbenches only after the shared runtime execution foundation is stable.
