# Content-aware inpaint runtime decision

Status: decision scoped for E12 / V60-08; no model bundled and no runtime enabled yet.
Date: 2026-05-14

## Decision

Images will implement future content-aware repair as an opt-in, local-only LaMa ONNX workflow. The first approved target is an Apache-2.0 LaMa ONNX model, with OpenCV's `opencv/inpainting_lama` package as the primary reference implementation and Carve's `LaMa-ONNX` `lama_fp32.onnx` as the fallback validation candidate.

The feature must not ship inside the core viewer until the V60 AI runtime foundation exists. E12 only records the model/runtime choice so the editor roadmap does not drift into an unbounded generative-image stack.

## Runtime choice

1. Primary runtime: Windows ML through `Microsoft.Windows.AI.MachineLearning` / Windows App SDK ML on supported Windows 11 24H2+ systems. Windows ML is the preferred path because Windows manages the shared ONNX Runtime and can install/update hardware execution providers without Images bundling vendor SDKs.
2. Fallback runtime: `Microsoft.ML.OnnxRuntime.DirectML` for supported older Windows systems. DirectML remains supported for ONNX Runtime on Windows and gives broad DirectX 12 GPU coverage, but new Windows ML work is the forward path.
3. CPU fallback: allowed only as an explicit slow-path fallback with visible status and cancellation. It must not run silently on large masks.
4. Rejected for Images v0.x: Stable Diffusion inpainting as the default repair tool. It is too large, too slow, and too generative for a fast local viewer/editor workflow.

## Model choice

Primary candidate:

- Model: `opencv/inpainting_lama`
- Source: https://huggingface.co/opencv/inpainting_lama
- License: Apache License, per the model card.
- Notes: ONNX source, OpenCV 5 sample code, approx 93 MB model package.

Fallback validation candidate:

- Model: `Carve/LaMa-ONNX` `lama_fp32.onnx`
- Source: https://huggingface.co/Carve/LaMa-ONNX
- License: Apache-2.0, per the model card.
- Notes: fixed 512 x 512 input shape, opset 17, recommended over the alternate `lama.onnx` export.

Original research reference:

- Project: `advimman/lama`
- Source: https://github.com/advimman/lama
- License: Apache-2.0.
- Paper: "Resolution-robust Large Mask Inpainting with Fourier Convolutions."

## Product contract

- No model is bundled in the installer or committed to source control.
- First use must show an explicit local-model setup flow: download from an approved URL or choose a local model file.
- Every download must be user initiated, logged through the network-egress surface, and verified by SHA-256 before use.
- Model files live under `%LOCALAPPDATA%\Images\models\inpaint\`.
- The diagnostics surfaces must show model ID, source, path, size, SHA-256, runtime backend, and last validation result.
- Output must flow through the existing non-destructive edit stack first. Original overwrite needs the same confirmation and rollback rules as crop/JPEG writeback.
- The UI copy must label the feature "Generative erase" or "Content-aware repair" only when a validated local model is available; otherwise it remains disabled with a setup/status explanation.

## Implementation shape

- `InpaintModelRegistry`: approved model IDs, expected hashes, source URLs, license text paths, input size, opset, and preprocessing contract.
- `ModelStorage`: app-data model folder, temp download, SHA-256 verification, license/readme retention, and delete/reset action.
- `InpaintRuntime`: detects Windows ML first, falls back to ONNX Runtime DirectML, reports CPU fallback explicitly.
- `InpaintPlanner`: converts a pixel selection or mask into 512 x 512 tiles with mask dilation and overlap.
- `InpaintOperation`: XMP edit-stack operation with model ID/hash, mask bounds, tile grid, and renderer version. Store raster patch sidecars if exact replay is not stable across runtime updates.
- `InpaintRenderer`: deterministic apply path used by preview, Save a copy, and future original overwrite flows.

## Integration review fields

| Field | Decision |
| --- | --- |
| Name and version | LaMa ONNX; exact artifact version/hash pending model-manager implementation. |
| Source URL | Primary: `https://huggingface.co/opencv/inpainting_lama`; fallback: `https://huggingface.co/Carve/LaMa-ONNX`. |
| License | Apache-2.0 / Apache License per model cards and original repository. |
| Redistribution permission | Do not bundle by default; user-initiated download or local import only until exact artifact hashes and license files are recorded. |
| Source-use boundary | Reference only. Runtime consumes ONNX model files; no OpenCV sample code copied into Images. |
| Update cadence | Pin approved model IDs and hashes; manual review before adding or replacing a model. |
| CVE/advisory tracking | Windows ML / Windows App SDK ML, ONNX Runtime DirectML, and model-source repository advisories. |
| Binary provenance | SHA-256 required for every approved model file before the setup UI enables it. |
| Process boundary | In-process managed runtime only after V60-01 review; no Python, no OpenCV native dependency in the first Images implementation. |
| File access boundary | Current image pixels, mask/selection, model folder, and temp files under app data. |
| Network behavior | No automatic download. User-initiated download only, logged and checksum-verified. |
| Failure mode | Disabled action with setup copy when missing; cancellable progress; no source overwrite on failure. |
| Test corpus | Generated masks over local raster fixtures; deterministic tiling, mask dilation, model registry, storage verification, and edit-stack serialization tests. |
| Release impact | No installer size increase until a future optional model bundle is explicitly approved. |

## Deferrals

- No implementation in the current E12 crop/selection slice.
- No Stable Diffusion, SUPIR, Flux, or other large diffusion model path in the viewer/editor.
- No OpenCvSharp dependency just to run the OpenCV sample. Revisit only if pure ONNX preprocessing is insufficient.
- No automatic model marketplace. Model sources are project-approved and disabled until verified.
