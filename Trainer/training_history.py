"""Build the viewer's compact training history from TensorBoard event files."""

from __future__ import annotations

import argparse
import json
import math
import os
from pathlib import Path
import sys
import time
from typing import Iterable

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from Trainer import arena_trainer  # noqa: E402

HISTORY_NAME = "training_history.json"
EVENT_PATTERN = "events.out.tfevents.*"
EXACT_TAGS = {"Policy/Entropy", "Losses/Value Loss", "Losses/Policy Loss"}

_write_signatures: dict[Path, tuple[int, int]] = {}


def _event_files(run_dir: Path, behavior: str) -> list[Path]:
    files = list((run_dir / behavior).glob(EVENT_PATTERN))
    return sorted(files, key=lambda path: (path.stat().st_mtime_ns, path.name))


def _event_signature(files: list[Path]) -> tuple[int, int]:
    stats = [path.stat() for path in files]
    return max(stat.st_mtime_ns for stat in stats), sum(stat.st_size for stat in stats)


def _newest_run_dir(runs_dir: Path, behavior: str) -> Path | None:
    candidates = [
        path.parent.parent
        for path in runs_dir.glob(f"*/{behavior}/{EVENT_PATTERN}")
        if path.is_file()
    ]
    candidates = list(dict.fromkeys(candidates))
    current = [
        path
        for path in candidates
        if arena_trainer.run_rules_version(path) == arena_trainer.RULES_VERSION
    ]
    if current:
        candidates = current
    if not candidates:
        return None
    return max(
        candidates,
        key=lambda path: max(file.stat().st_mtime_ns for file in _event_files(path, behavior)),
    )


def _keep_tag(tag: str) -> bool:
    return tag.startswith(("Environment/", "Arena/")) or tag in EXACT_TAGS


def _finite(value: float) -> float:
    return value if math.isfinite(value) else 0.0


def _downsample(points: list[tuple[int, float]], max_points: int) -> list[tuple[int, float]]:
    if len(points) <= max_points:
        return points
    if max_points == 2:
        return [points[0], points[-1]]

    first_step = points[0][0]
    last_step = points[-1][0]
    bucket_count = max_points - 2
    buckets: list[list[tuple[int, float]]] = [[] for _ in range(bucket_count)]
    for point in points[1:-1]:
        index = (point[0] - first_step) * bucket_count // (last_step - first_step)
        buckets[min(index, bucket_count - 1)].append(point)

    sampled = [points[0]]
    for bucket in buckets:
        if bucket:
            sampled.append((
                int(round(sum(step for step, _ in bucket) / len(bucket))),
                sum(value for _, value in bucket) / len(bucket),
            ))
    sampled.append(points[-1])
    return sampled


def build_history(run_dir: Path, behavior: str = "Warrior", max_points: int = 240) -> dict | None:
    if max_points < 2:
        raise ValueError("max_points must be at least 2")
    event_files = _event_files(run_dir, behavior)
    if not event_files:
        return None

    from tensorboard.backend.event_processing.event_accumulator import EventAccumulator

    scalar_values: dict[str, dict[int, float]] = {}
    for event_file in event_files:
        accumulator = EventAccumulator(str(event_file), size_guidance={"scalars": 0})
        accumulator.Reload()
        for tag in accumulator.Tags().get("scalars", []):
            if not _keep_tag(tag):
                continue
            by_step = scalar_values.setdefault(tag, {})
            for event in accumulator.Scalars(tag):
                by_step[int(event.step)] = _finite(float(event.value))

    series = []
    last_step = 0
    for tag in sorted(scalar_values):
        raw_points = sorted(scalar_values[tag].items())
        points = _downsample(raw_points, max_points)
        if raw_points:
            last_step = max(last_step, raw_points[-1][0])
        series.append({
            "tag": tag,
            "steps": [int(step) for step, _ in points],
            "values": [float(value) for _, value in points],
        })

    return {
        "run_id": run_dir.name,
        "behavior": behavior,
        "rules_version": arena_trainer.run_rules_version(run_dir),
        "updated_unix": time.time(),
        "last_step": last_step,
        "series": series,
    }


def write_history(run_dir: Path, behavior: str = "Warrior") -> bool:
    event_files = _event_files(run_dir, behavior)
    if not event_files:
        return False
    signature = _event_signature(event_files)
    output = run_dir / HISTORY_NAME
    if output.is_file() and _write_signatures.get(output) == signature:
        return False

    history = build_history(run_dir, behavior)
    if history is None:
        return False
    temporary = output.with_name(output.name + ".tmp")
    temporary.write_text(json.dumps(history, allow_nan=False), encoding="utf-8")
    last_error: PermissionError | None = None
    for attempt in range(20):
        try:
            os.replace(temporary, output)
            _write_signatures[output] = signature
            return True
        except PermissionError as error:
            last_error = error
            time.sleep(0.05 * (attempt + 1))
    assert last_error is not None
    raise last_error


def main(argv: Iterable[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-id")
    parser.add_argument("--results-dir", type=Path, default=arena_trainer.DEFAULT_RESULTS_DIRECTORY)
    parser.add_argument("--behavior", default="Warrior")
    args = parser.parse_args(list(argv) if argv is not None else None)

    runs_dir = arena_trainer.resolve_path(str(args.results_dir), arena_trainer.repository_root())
    if args.run_id:
        run_dir = runs_dir / args.run_id
    else:
        run_dir = _newest_run_dir(runs_dir, args.behavior)
        if run_dir is None:
            print(f"No {args.behavior} runs under {runs_dir}", file=sys.stderr)
            return 1

    output = run_dir / HISTORY_NAME
    if not write_history(run_dir, args.behavior) and not output.is_file():
        print(f"No TensorBoard events under {run_dir / args.behavior}", file=sys.stderr)
        return 1
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
