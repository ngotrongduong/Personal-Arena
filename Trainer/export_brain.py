"""Export ML-Agents PPO checkpoints to the Personal Arena ``.brain`` format.

The Unity player cannot load ONNX at runtime (the Inference Engine importer is
Editor-only), so the "Watch AI" viewer runs the policy in plain C#
(``Unity/Assets/Arena/Core/PolicyBrain.cs``). This script converts the actor of
a ``<Behavior>-<step>.pt`` checkpoint into that little-endian binary layout::

    "PABR" | int32 version | int32 nameLen | utf8 name | int64 step | int32 obsSize
    int32 bodyLayers  { int32 in | int32 out | float32[out*in] W | float32[out] b }   (Swish after each)
    int32 branches    { int32 in | int32 out | float32[out*in] W | float32[out] b }   (raw logits)
    int32 selfTests (0/1) { float32[obsSize] obs | float32[sum(out)] expected logits }

``--watch`` keeps re-exporting the newest checkpoint of the newest run to
``<run>/<Behavior>/latest.brain`` so the viewer can hot-reload it while training runs.
"""

from __future__ import annotations

import argparse
import os
from pathlib import Path
import re
import struct
import sys
import time
from typing import Iterable, Sequence

import numpy as np

FORMAT_VERSION = 1
MAGIC = b"PABR"
BODY_PATTERN = re.compile(r"^network_body\._body_endoder\.seq_layers\.(\d+)\.weight$")
BRANCH_PATTERN = re.compile(r"^action_model\._discrete_distribution\.branches\.(\d+)\.weight$")
CHECKPOINT_PATTERN = re.compile(r"^(?P<behavior>.+)-(?P<step>\d+)\.pt$")
LATEST_NAME = "latest.brain"

Layer = tuple[np.ndarray, np.ndarray]


class ExportError(RuntimeError):
    """The checkpoint cannot be represented by the C# PolicyBrain."""


def repository_root() -> Path:
    return Path(__file__).resolve().parents[1]


def extract_actor_layers(state: dict) -> tuple[list[Layer], list[Layer]]:
    """Return (body, branches) as float32 (weight[out, in], bias[out]) pairs."""
    unsupported = [
        key
        for key in state
        if any(marker in key.lower() for marker in ("normalizer", "lstm", "hypernet", "_continuous_distribution"))
    ]
    memory = state.get("memory_size_vector")
    if memory is not None and float(_array(memory).sum()) != 0.0:
        unsupported.append("memory_size_vector")
    if unsupported:
        raise ExportError(
            "Checkpoint uses features the C# brain does not implement: " + ", ".join(sorted(unsupported)[:5])
        )

    body_indices = sorted(int(m.group(1)) for key in state if (m := BODY_PATTERN.match(key)))
    branch_indices = sorted(int(m.group(1)) for key in state if (m := BRANCH_PATTERN.match(key)))
    if not body_indices or not branch_indices:
        raise ExportError("Checkpoint has no SimpleActor body/branch weights (keys: %s)" % sorted(state)[:8])
    if branch_indices != list(range(len(branch_indices))):
        raise ExportError(f"Branch indices are not contiguous: {branch_indices}")

    body = [
        _layer(state, f"network_body._body_endoder.seq_layers.{index}") for index in body_indices
    ]
    branches = [
        _layer(state, f"action_model._discrete_distribution.branches.{index}") for index in branch_indices
    ]

    width = body[0][0].shape[1]
    for weight, _ in body:
        if weight.shape[1] != width:
            raise ExportError(f"Body layer expects {weight.shape[1]} inputs, previous width is {width}")
        width = weight.shape[0]
    for weight, _ in branches:
        if weight.shape[1] != width:
            raise ExportError(f"Branch expects {weight.shape[1]} inputs, body width is {width}")
    return body, branches


def _layer(state: dict, prefix: str) -> Layer:
    weight = _array(state[prefix + ".weight"])
    bias = _array(state[prefix + ".bias"])
    if weight.ndim != 2 or bias.shape != (weight.shape[0],):
        raise ExportError(f"{prefix} has unexpected shapes {weight.shape} / {bias.shape}")
    return weight, bias


def _array(value) -> np.ndarray:
    if hasattr(value, "detach"):
        value = value.detach().cpu().numpy()
    array = np.asarray(value, dtype=np.float32)
    if not np.all(np.isfinite(array)):
        raise ExportError("Checkpoint contains non-finite weights")
    return array


def forward(body: Sequence[Layer], branches: Sequence[Layer], observation: np.ndarray) -> np.ndarray:
    """Reference forward pass (float32), identical in structure to PolicyBrain.Evaluate."""
    hidden = observation.astype(np.float32)
    for weight, bias in body:
        pre = weight @ hidden + bias
        hidden = (pre / (1.0 + np.exp(-pre))).astype(np.float32)
    return np.concatenate([weight @ hidden + bias for weight, bias in branches]).astype(np.float32)


def encode_brain(
    behavior: str,
    step: int,
    body: Sequence[Layer],
    branches: Sequence[Layer],
    self_test_observation: np.ndarray | None = None,
) -> bytes:
    observation_size = body[0][0].shape[1]
    name = behavior.encode("utf-8")
    parts = [MAGIC, struct.pack("<ii", FORMAT_VERSION, len(name)), name, struct.pack("<qi", step, observation_size)]
    for group in (body, branches):
        parts.append(struct.pack("<i", len(group)))
        for weight, bias in group:
            parts.append(struct.pack("<ii", weight.shape[1], weight.shape[0]))
            parts.append(np.ascontiguousarray(weight, dtype="<f4").tobytes())
            parts.append(np.ascontiguousarray(bias, dtype="<f4").tobytes())
    if self_test_observation is None:
        parts.append(struct.pack("<i", 0))
    else:
        observation = np.asarray(self_test_observation, dtype=np.float32)
        if observation.shape != (observation_size,):
            raise ExportError("Self-test observation has the wrong size")
        parts.append(struct.pack("<i", 1))
        parts.append(observation.astype("<f4").tobytes())
        parts.append(forward(body, branches, observation).astype("<f4").tobytes())
    return b"".join(parts)


def load_actor_state(checkpoint: Path) -> dict:
    import torch  # Imported lazily so format tests do not need torch.

    data = torch.load(str(checkpoint), map_location="cpu")
    if not isinstance(data, dict) or "Policy" not in data:
        raise ExportError(f"{checkpoint.name} has no 'Policy' module (found {list(data)[:6]})")
    return data["Policy"]


def export_checkpoint(checkpoint: Path, output: Path, seed: int = 0) -> int:
    match = CHECKPOINT_PATTERN.match(checkpoint.name)
    if not match:
        raise ExportError(f"Not a '<Behavior>-<step>.pt' checkpoint: {checkpoint.name}")
    body, branches = extract_actor_layers(load_actor_state(checkpoint))
    rng = np.random.default_rng(seed)
    observation = rng.uniform(-1.0, 1.0, size=body[0][0].shape[1]).astype(np.float32)
    payload = encode_brain(match["behavior"], int(match["step"]), body, branches, observation)
    write_atomically(output, payload)
    return int(match["step"])


def write_atomically(path: Path, payload: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_bytes(payload)
    for attempt in range(20):
        try:
            os.replace(temporary, path)
            return
        except PermissionError:  # The viewer may be reading the old file on Windows.
            time.sleep(0.1 * (attempt + 1))
    raise ExportError(f"Could not replace {path}")


def checkpoints(behavior_dir: Path) -> list[tuple[int, Path]]:
    found = []
    for path in behavior_dir.glob("*.pt"):
        match = CHECKPOINT_PATTERN.match(path.name)
        if match:
            found.append((int(match["step"]), path))
    return sorted(found)


def newest_behavior_dir(runs_dir: Path, behavior: str) -> Path | None:
    candidates = [path for path in runs_dir.glob(f"*/{behavior}") if path.is_dir() and checkpoints(path)]
    if not candidates:
        return None
    return max(candidates, key=lambda path: max(p.stat().st_mtime for _, p in checkpoints(path)))


def export_newest(runs_dir: Path, behavior: str, exported: dict[Path, int], log=print) -> Path | None:
    behavior_dir = newest_behavior_dir(runs_dir, behavior)
    if behavior_dir is None:
        return None
    step, checkpoint = checkpoints(behavior_dir)[-1]
    output = behavior_dir / LATEST_NAME
    if exported.get(output) == step and output.exists():
        return output
    try:
        export_checkpoint(checkpoint, output)
    except (OSError, RuntimeError, EOFError) as error:  # A checkpoint may still be mid-write.
        if isinstance(error, ExportError):
            raise
        log(f"[export_brain] skipped {checkpoint.name} for now: {error}")
        return None
    exported[output] = step
    log(f"[export_brain] {behavior_dir.parent.name}: step {step:,} -> {output}")
    return output


def main(argv: Iterable[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--checkpoint", type=Path, help="Export one checkpoint and exit.")
    parser.add_argument("--output", type=Path, help="Output path for --checkpoint.")
    parser.add_argument("--runs-dir", type=Path, default=repository_root() / "Trainer" / "runs")
    parser.add_argument("--behavior", default="Warrior")
    parser.add_argument("--watch", action="store_true", help="Keep exporting the newest checkpoint.")
    parser.add_argument("--interval", type=float, default=5.0)
    args = parser.parse_args(list(argv) if argv is not None else None)

    if args.checkpoint:
        output = args.output or args.checkpoint.with_suffix(".brain")
        step = export_checkpoint(args.checkpoint, output)
        print(f"[export_brain] step {step:,} -> {output}")
        return 0

    exported: dict[Path, int] = {}
    while True:
        result = export_newest(args.runs_dir, args.behavior, exported)
        if not args.watch:
            if result is None:
                print(f"[export_brain] no {args.behavior} checkpoints under {args.runs_dir}", file=sys.stderr)
                return 1
            return 0
        time.sleep(args.interval)


if __name__ == "__main__":
    raise SystemExit(main())
