# Content-aware inpaint runtime decision

Status: implemented for V60-08; approved local model import, hash validation, LaMa inference, and the shared Windows ML / DirectML / CPU runtime are shipped.
Date: 2026-05-17

## Decision

Images implements content-aware repair as an opt-in, local-only LaMa ONNX workflow. The approved targets are the Apache-2.0 OpenCV `opencv/inpainting_lama` package and Carve's `LaMa-ONNX` `lama_fp32.onnx` validation candidate.

The feature remains opt-in and local-only: it is enabled only after the user imports a model whose SHA-256 matches the approved registry.

## Runtime choice

1. Primary runtime: self-contained `Microsoft.Windows.AI.MachineLearning` 2.1.74. On Windows 11 24H2+, Images registers only certified providers already marked ready by the Windows ML catalog, selects an NPU before a GPU, and never invokes provider acquisition automatically.
2. Fallback runtime: the same Windows ML package supplies bundled DirectML and ONNX Runtime CPU paths. Session creation retries per model, so an incompatible NPU/GPU provider cannot strand CLIP, background removal, LaMa, or super-resolution.
3. CPU fallback: allowed only as an explicit slow-path fallback with visible status and cancellation. It must not run silently on large masks.
4. Rejected for Images v0.x: Stable Diffusion inpainting as the default repair tool. It is too large, too slow, and too generative for a fast local viewer/editor workflow.

## Model choice

Primary candidate:

- Model: `opencv/inpainting_lama`
- Source: https://huggingface.co/opencv/inpainting_lama
- Approved file: `inpainting_lama_2025jan.onnx`
- SHA-256: `7df918ac3921d3daf0aae1d219776cf0dc4e4935f035af81841b40adcf74fdf2`
- License: Apache License, per the model card.
- Notes: ONNX source, OpenCV 5 sample code, approx 93 MB model package.

Fallback validation candidate:

- Model: `Carve/LaMa-ONNX` `lama_fp32.onnx`
- Source: https://huggingface.co/Carve/LaMa-ONNX
- Approved file: `lama_fp32.onnx`
- SHA-256: `1faef5301d78db7dda502fe59966957ec4b79dd64e16f03ed96913c7a4eb68d6`
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
- Model files live under `%LOCALAPPDATA%\Images\models\inpaint\<approved-model-id>\`.
- The diagnostics surfaces must show model ID, source, path, size, SHA-256, runtime backend, and last validation result.
- Output must flow through the existing non-destructive edit stack first. Original overwrite needs the same confirmation and rollback rules as crop/JPEG writeback.
- The UI copy must label the feature "Generative erase" or "Content-aware repair" only when a validated local model is available; otherwise it remains disabled with a setup/status explanation.

## Implementation shape

- `ModelManagerService`: shipped shared registry/storage foundation for approved model IDs, expected hashes, source URLs, app-local grouped folders, SHA-256 verification, import/delete/reveal actions, and runtime status copy.
- `InpaintModelRegistry`: shipped tensor-shape, preprocessing, mask, and fixed-input contract layered over the shared model manager.
- `ModelStorage`: app-data model folder and SHA-256 verification are shipped; future user-initiated download and license/readme retention remain unimplemented.
- `InpaintRuntime`: shipped through the shared `OnnxRuntimeService`, with Windows ML NPU/GPU selection and per-model DirectML/CPU fallback plus truthful hardware labels.
- `InpaintPlanner`: converts a pixel selection or mask into 512 x 512 tiles with mask dilation and overlap.
- `InpaintOperation`: XMP edit-stack operation with model ID/hash, mask bounds, tile grid, and renderer version. Store raster patch sidecars if exact replay is not stable across runtime updates.
- `InpaintRenderer`: deterministic apply path used by preview, Save a copy, and future original overwrite flows.

## Integration review fields

| Field | Decision |
| --- | --- |
| Name and version | LaMa ONNX; approved artifacts are `inpainting_lama_2025jan.onnx` and `lama_fp32.onnx` with pinned SHA-256 values above. |
| Source URL | Primary: `https://huggingface.co/opencv/inpainting_lama`; fallback: `https://huggingface.co/Carve/LaMa-ONNX`. |
| License | Apache-2.0 / Apache License per model cards and original repository. |
| Redistribution permission | Do not bundle by default; user-initiated download or local import only until exact artifact hashes and license files are recorded. |
| Source-use boundary | Reference only. Runtime consumes ONNX model files; no OpenCV sample code copied into Images. |
| Update cadence | Pin approved model IDs and hashes; manual review before adding or replacing a model. |
| CVE/advisory tracking | Windows ML / Windows App SDK ML, ONNX Runtime DirectML, and model-source repository advisories. |
| Binary provenance | SHA-256 is required for every approved model file before the setup UI marks it ready. |
| Process boundary | Reviewed in-process Windows ML/ONNX runtime; no Python or OpenCV native dependency. |
| File access boundary | Current image pixels, mask/selection, model folder, and temp files under app data. |
| Network behavior | No automatic download. User-initiated download only, logged and checksum-verified. |
| Failure mode | Disabled action with setup copy when missing; cancellable progress; no source overwrite on failure. |
| Test corpus | Generated masks over local raster fixtures; deterministic tiling, mask dilation, model registry, storage verification, and edit-stack serialization tests. |
| Release impact | No installer size increase until a future optional model bundle is explicitly approved. |

## Deferrals

- No bundled model or silent model/provider acquisition.
- No Stable Diffusion, SUPIR, Flux, or other large diffusion model path in the viewer/editor.
- No OpenCvSharp dependency just to run the OpenCV sample. Revisit only if pure ONNX preprocessing is insufficient.
- No automatic model marketplace. Model sources are project-approved and disabled until verified.
