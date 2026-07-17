# /// script
# requires-python = ">=3.11,<3.12"
# dependencies = [
#   "numpy==1.26.4",
#   "onnxruntime==1.23.2",
#   "tensorflow==2.15.1",
#   "tf2onnx==1.16.1",
# ]
# ///

"""Reproduce the reviewed NIMA MobileNet ONNX artifact from idealo's checkpoint."""

from __future__ import annotations

import argparse
import hashlib
import os
import tempfile
import urllib.request
from pathlib import Path

os.environ.setdefault("TF_CPP_MIN_LOG_LEVEL", "2")

import numpy as np
import onnxruntime as ort
import tensorflow as tf
import tf2onnx

SOURCE_REVISION = "dceaf7c2d218bc6e80b21d6e147e3b56a21b7f31"
WEIGHTS_URL = (
    "https://raw.githubusercontent.com/idealo/image-quality-assessment/"
    f"{SOURCE_REVISION}/models/MobileNet/weights_mobilenet_aesthetic_0.07.hdf5"
)
WEIGHTS_SHA256 = "e563ad91b3d47410e45f7238f07ab8f6abd1bd0c4b18a4b0af9c681a21a91cb2"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def build_model(weights: Path) -> tf.keras.Model:
    base = tf.keras.applications.MobileNet(
        input_shape=(224, 224, 3), weights=None, include_top=False, pooling="avg"
    )
    head = tf.keras.layers.Dropout(0.75)(base.output)
    output = tf.keras.layers.Dense(10, activation="softmax")(head)
    model = tf.keras.Model(base.inputs, output)
    model.load_weights(weights)
    return model


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory(prefix="images-nima-") as directory:
        weights = Path(directory) / "weights.hdf5"
        urllib.request.urlretrieve(WEIGHTS_URL, weights)
        actual_weights_hash = sha256(weights)
        if actual_weights_hash != WEIGHTS_SHA256:
            raise RuntimeError(f"Checkpoint hash mismatch: {actual_weights_hash}")

        model = build_model(weights)
        signature = (tf.TensorSpec((1, 224, 224, 3), tf.float32, name="input"),)
        tf2onnx.convert.from_keras(
            model,
            input_signature=signature,
            opset=17,
            output_path=str(args.output),
        )

        # A deterministic tensor proves conversion parity without depending on a fixture file.
        tensor = np.linspace(-1.0, 1.0, 224 * 224 * 3, dtype=np.float32).reshape(
            1, 224, 224, 3
        )
        expected = model(tensor, training=False).numpy()
        session = ort.InferenceSession(str(args.output), providers=["CPUExecutionProvider"])
        actual = session.run(None, {"input": tensor})[0]
        maximum_delta = float(np.max(np.abs(expected - actual)))
        if maximum_delta > 1e-5:
            raise RuntimeError(f"ONNX parity failed: maximum delta {maximum_delta}")

    print(f"ONNX SHA-256: {sha256(args.output)}")
    print(f"TensorFlow/ONNX maximum probability delta: {maximum_delta:.10g}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
