# /// script
# requires-python = ">=3.11,<3.12"
# dependencies = [
#   "numpy==2.4.2",
#   "onnx==1.22.0",
#   "onnxruntime==1.26.0",
#   "torch==2.12.1",
# ]
# ///

"""Reproduce the reviewed Places365 ResNet-18 ONNX artifact."""

from __future__ import annotations

import argparse
import hashlib
import os
import tempfile
import urllib.request
from pathlib import Path

import numpy as np
import onnxruntime as ort
import torch
from torch import nn

SOURCE_REVISION = "8a953ed56438726dc98bdef3796d042e7f1f171e"
CHECKPOINT_URL = "http://places2.csail.mit.edu/models_places365/resnet18_places365.pth.tar"
CHECKPOINT_SHA256 = "2f4759217d470da2b803f8f66cd4488a066406b555a5fb95ee9a4663f9f05588"


class BasicBlock(nn.Module):
    def __init__(self, inplanes: int, planes: int, stride: int = 1) -> None:
        super().__init__()
        self.conv1 = nn.Conv2d(inplanes, planes, 3, stride, 1, bias=False)
        self.bn1 = nn.BatchNorm2d(planes)
        self.relu = nn.ReLU(inplace=True)
        self.conv2 = nn.Conv2d(planes, planes, 3, 1, 1, bias=False)
        self.bn2 = nn.BatchNorm2d(planes)
        self.downsample: nn.Module | None = None
        if stride != 1 or inplanes != planes:
            self.downsample = nn.Sequential(
                nn.Conv2d(inplanes, planes, 1, stride, bias=False),
                nn.BatchNorm2d(planes),
            )

    def forward(self, value: torch.Tensor) -> torch.Tensor:
        identity = value
        result = self.relu(self.bn1(self.conv1(value)))
        result = self.bn2(self.conv2(result))
        if self.downsample is not None:
            identity = self.downsample(value)
        return self.relu(result + identity)


class ResNet18(nn.Module):
    def __init__(self) -> None:
        super().__init__()
        self.inplanes = 64
        self.conv1 = nn.Conv2d(3, 64, 7, 2, 3, bias=False)
        self.bn1 = nn.BatchNorm2d(64)
        self.relu = nn.ReLU(inplace=True)
        self.maxpool = nn.MaxPool2d(3, 2, 1)
        self.layer1 = self._layer(64, 2)
        self.layer2 = self._layer(128, 2, 2)
        self.layer3 = self._layer(256, 2, 2)
        self.layer4 = self._layer(512, 2, 2)
        self.avgpool = nn.AdaptiveAvgPool2d((1, 1))
        self.fc = nn.Linear(512, 365)

    def _layer(self, planes: int, blocks: int, stride: int = 1) -> nn.Sequential:
        layers = [BasicBlock(self.inplanes, planes, stride)]
        self.inplanes = planes
        layers.extend(BasicBlock(self.inplanes, planes) for _ in range(1, blocks))
        return nn.Sequential(*layers)

    def forward(self, value: torch.Tensor) -> torch.Tensor:
        value = self.maxpool(self.relu(self.bn1(self.conv1(value))))
        value = self.layer1(value)
        value = self.layer2(value)
        value = self.layer3(value)
        value = self.layer4(value)
        value = self.avgpool(value)
        return self.fc(torch.flatten(value, 1))


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

    with tempfile.TemporaryDirectory(prefix="images-places365-") as directory:
        checkpoint_path = Path(directory) / "resnet18_places365.pth.tar"
        urllib.request.urlretrieve(CHECKPOINT_URL, checkpoint_path)
        actual_checkpoint_hash = sha256(checkpoint_path)
        if actual_checkpoint_hash != CHECKPOINT_SHA256:
            raise RuntimeError(f"Checkpoint hash mismatch: {actual_checkpoint_hash}")

        checkpoint = torch.load(
            checkpoint_path,
            map_location="cpu",
            weights_only=False,
            encoding="latin1",
        )
        state = {
            key.removeprefix("module."): value
            for key, value in checkpoint["state_dict"].items()
        }
        model = ResNet18()
        model.load_state_dict(state)
        model.eval()

        sample = torch.zeros(1, 3, 224, 224)
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

        tensor = np.linspace(-2.0, 2.0, 3 * 224 * 224, dtype=np.float32).reshape(
            1, 3, 224, 224
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
