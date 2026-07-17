# /// script
# requires-python = ">=3.11,<3.12"
# dependencies = [
#   "numpy==2.4.2",
#   "onnx==1.22.0",
#   "onnxruntime==1.26.0",
#   "packaging==26.0",
#   "safetensors==0.7.0",
#   "timm==1.0.24",
#   "torch==2.12.1",
# ]
# ///

"""Reproduce the reviewed Marqo ViT-Tiny safety-classification ONNX artifact."""

from __future__ import annotations

import argparse
import hashlib
import json
import tempfile
import urllib.request
from pathlib import Path

import numpy as np
import onnxruntime as ort
import timm
import torch
from safetensors.torch import load_file

SOURCE_REVISION = "0c26ec22111b83f106d72a55f611ec35962bcb65"
CHECKPOINT_URL = (
    "https://huggingface.co/Marqo/nsfw-image-detection-384/resolve/"
    f"{SOURCE_REVISION}/model.safetensors"
)
CHECKPOINT_SHA256 = "6bf2e0f64a1d20169736c2836e3a787b12379fdc08ba87f7d94a7a3d58eeefce"
CONFIG_URL = (
    "https://huggingface.co/Marqo/nsfw-image-detection-384/resolve/"
    f"{SOURCE_REVISION}/config.json"
)
CONFIG_SHA256 = "ae848d1dca0aeccd38f2ebb4b1bb47219cdc7f34e3ddc50c8d6430abe74d79b4"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory(prefix="images-safety-") as directory:
        checkpoint_path = Path(directory) / "model.safetensors"
        config_path = Path(directory) / "config.json"
        urllib.request.urlretrieve(CHECKPOINT_URL, checkpoint_path)
        urllib.request.urlretrieve(CONFIG_URL, config_path)
        actual_checkpoint_hash = sha256(checkpoint_path)
        if actual_checkpoint_hash != CHECKPOINT_SHA256:
            raise RuntimeError(f"Checkpoint hash mismatch: {actual_checkpoint_hash}")
        actual_config_hash = sha256(config_path)
        if actual_config_hash != CONFIG_SHA256:
            raise RuntimeError(f"Config hash mismatch: {actual_config_hash}")
        config = json.loads(config_path.read_text(encoding="utf-8"))
        if config.get("label_names") != ["NSFW", "SFW"]:
            raise RuntimeError("Config label contract changed.")
        if config.get("pretrained_cfg", {}).get("input_size") != [3, 384, 384]:
            raise RuntimeError("Config input contract changed.")

        model = timm.create_model("vit_tiny_patch16_384", pretrained=False, num_classes=2)
        model.load_state_dict(load_file(checkpoint_path))
        model.eval()

        sample = torch.zeros(1, 3, 384, 384)
        with torch.no_grad():
            torch.onnx.export(
                model,
                sample,
                args.output,
                input_names=["input"],
                output_names=["logits"],
                opset_version=17,
                do_constant_folding=True,
                dynamo=False,
            )

        tensor = np.linspace(-1.0, 1.0, 3 * 384 * 384, dtype=np.float32).reshape(
            1, 3, 384, 384
        )
        with torch.no_grad():
            expected = model(torch.from_numpy(tensor)).numpy()
        session = ort.InferenceSession(str(args.output), providers=["CPUExecutionProvider"])
        actual = session.run(None, {"input": tensor})[0]
        maximum_delta = float(np.max(np.abs(expected - actual)))
        if maximum_delta > 1e-4:
            raise RuntimeError(f"ONNX parity failed: maximum delta {maximum_delta}")

    print(f"ONNX SHA-256: {sha256(args.output)}")
    print(f"PyTorch/ONNX maximum logit delta: {maximum_delta:.10g}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
