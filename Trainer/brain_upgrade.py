"""Grow an ML-Agents checkpoint to a newer Personal Arena observation schema."""

from __future__ import annotations

import argparse
import copy
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import shutil
from typing import Iterable

import numpy as np

from Trainer import arena_trainer, export_brain


INPUT_WEIGHT = "network_body._body_endoder.seq_layers.0.weight"
BRANCH_WEIGHT = "action_model._discrete_distribution.branches.{index}.weight"
BRANCH_BIAS = "action_model._discrete_distribution.branches.{index}.bias"


class UpgradeError(RuntimeError):
    """The checkpoint or schema change cannot be upgraded without losing behavior."""


def _ordered_positions(old_names: list[str], new_names: list[str], what: str) -> list[int]:
    positions: list[int] = []
    start = 0
    for name in old_names:
        try:
            position = new_names.index(name, start)
        except ValueError:
            raise UpgradeError(f"{what} '{name}' was removed, renamed, or reordered") from None
        positions.append(position)
        start = position + 1
    return positions


def _observation(schema: dict) -> list[dict]:
    segments = schema.get("observation")
    if not isinstance(segments, list) or not segments:
        raise UpgradeError("schema has no observation segments")
    names: list[str] = []
    for segment in segments:
        if not isinstance(segment, dict):
            raise UpgradeError("observation segment is not an object")
        name = segment.get("name")
        repeat = segment.get("repeat")
        fields = segment.get("fields")
        if (
            not isinstance(name, str)
            or not name
            or name in names
            or not isinstance(repeat, int)
            or isinstance(repeat, bool)
            or repeat <= 0
            or not isinstance(fields, list)
            or not fields
            or any(not isinstance(field, str) or not field for field in fields)
            or len(set(fields)) != len(fields)
        ):
            raise UpgradeError("schema has an invalid observation segment")
        names.append(name)
    return segments


def _actions(schema: dict) -> list[dict]:
    actions = schema.get("actions")
    if not isinstance(actions, list) or not actions:
        raise UpgradeError("schema has no action branches")
    names: list[str] = []
    for action in actions:
        if not isinstance(action, dict):
            raise UpgradeError("action branch is not an object")
        name = action.get("name")
        size = action.get("size")
        if (
            not isinstance(name, str)
            or not name
            or name in names
            or not isinstance(size, int)
            or isinstance(size, bool)
            or size <= 0
        ):
            raise UpgradeError("schema has an invalid action branch")
        names.append(name)
    return actions


def column_map(old_schema: dict, new_schema: dict) -> list[int]:
    """Return the new column index for every old observation index."""
    old_segments = _observation(old_schema)
    new_segments = _observation(new_schema)
    new_names = [segment["name"] for segment in new_segments]
    segment_positions = _ordered_positions(
        [segment["name"] for segment in old_segments], new_names, "observation segment"
    )

    new_offsets: list[int] = []
    offset = 0
    for segment in new_segments:
        new_offsets.append(offset)
        offset += segment["repeat"] * len(segment["fields"])

    result: list[int] = []
    for old_segment, new_position in zip(old_segments, segment_positions):
        new_segment = new_segments[new_position]
        if new_segment["repeat"] < old_segment["repeat"]:
            raise UpgradeError(f"segment '{old_segment['name']}' repeat count shrank")
        field_positions = _ordered_positions(
            old_segment["fields"],
            new_segment["fields"],
            f"field in segment {old_segment['name']}",
        )
        new_stride = len(new_segment["fields"])
        for repeat in range(old_segment["repeat"]):
            base = new_offsets[new_position] + repeat * new_stride
            result.extend(base + field for field in field_positions)
    return result


def action_map(old_schema: dict, new_schema: dict) -> list[tuple[int, int]]:
    """Return ``(new branch index, old size)`` for every old action branch."""
    old_actions = _actions(old_schema)
    new_actions = _actions(new_schema)
    positions = _ordered_positions(
        [action["name"] for action in old_actions],
        [action["name"] for action in new_actions],
        "action branch",
    )
    result: list[tuple[int, int]] = []
    for old_action, new_position in zip(old_actions, positions):
        if new_actions[new_position]["size"] < old_action["size"]:
            raise UpgradeError(f"action branch '{old_action['name']}' shrank")
        result.append((new_position, old_action["size"]))
    return result


def _schema_observation_size(schema: dict) -> int:
    return sum(segment["repeat"] * len(segment["fields"]) for segment in _observation(schema))


def _unsupported_state(checkpoint: dict) -> list[str]:
    unsupported: list[str] = []
    markers = ("lstm", "normalizer", "normalization", "running_mean", "running_variance", "normalize_steps")
    for section_name in ("Policy", "Optimizer:critic"):
        section = checkpoint.get(section_name, {})
        if isinstance(section, dict):
            unsupported.extend(
                f"{section_name}:{key}"
                for key in section
                if any(marker in key.lower() for marker in markers)
            )
    policy = checkpoint.get("Policy", {})
    memory = policy.get("memory_size_vector") if isinstance(policy, dict) else None
    if memory is not None:
        try:
            if float(memory.detach().cpu().sum()) != 0.0:
                unsupported.append("Policy:memory_size_vector")
        except (AttributeError, TypeError, ValueError):
            unsupported.append("Policy:memory_size_vector")
    return unsupported


# Bias of every output row that did not exist before, relative to the smallest old bias of its branch.
# Strongly negative, so a brain that never saw the new skill choices does not start picking them as
# soon as the action mask opens; PPO can still raise it when the choice proves useful.
NEW_CHOICE_BIAS_MARGIN = 5.0
NEW_ROW_SCALE = 0.01


def _random_rows(reference, rows: int, columns: int, generator):
    """Small random output rows from a numpy generator (platform independent, unlike torch's CPU RNG)."""
    import numpy as np
    import torch

    values = (generator.standard_normal((rows, columns)).astype(np.float32) * np.float32(NEW_ROW_SCALE)).astype(
        np.float32
    )
    return torch.from_numpy(values).to(device=reference.device, dtype=reference.dtype)


def upgrade_state(
    checkpoint: dict, old_schema: dict, new_schema: dict, seed: int = 0
) -> dict:
    """Return a checkpoint widened to ``new_schema`` while preserving old outputs."""
    columns = column_map(old_schema, new_schema)
    actions = action_map(old_schema, new_schema)
    old_observation_size = _schema_observation_size(old_schema)
    new_observation_size = _schema_observation_size(new_schema)
    if len(columns) != old_observation_size:
        raise UpgradeError("observation column map is incomplete")
    if not isinstance(checkpoint, dict):
        raise UpgradeError("checkpoint is not a dictionary")
    unsupported = _unsupported_state(checkpoint)
    if unsupported:
        raise UpgradeError("LSTM or observation normalizer is not supported: " + ", ".join(unsupported[:4]))

    result = copy.deepcopy(checkpoint)
    for section_name in ("Policy", "Optimizer:critic"):
        section = result.get(section_name)
        if not isinstance(section, dict) or INPUT_WEIGHT not in section:
            raise UpgradeError(f"checkpoint has no {section_name} input layer")
        old_weight = section[INPUT_WEIGHT]
        if getattr(old_weight, "ndim", None) != 2 or old_weight.shape[1] != old_observation_size:
            width = old_weight.shape[1] if getattr(old_weight, "ndim", None) == 2 else "invalid"
            raise UpgradeError(
                f"{section_name} observation width {width} does not match schema size {old_observation_size}"
            )
        new_weight = old_weight.new_zeros((old_weight.shape[0], new_observation_size))
        new_weight[:, columns] = old_weight
        section[INPUT_WEIGHT] = new_weight

    policy = result["Policy"]
    old_actions = _actions(old_schema)
    new_actions = _actions(new_schema)
    old_layers: list[tuple[object, object]] = []
    for old_index, old_action in enumerate(old_actions):
        weight_key = BRANCH_WEIGHT.format(index=old_index)
        bias_key = BRANCH_BIAS.format(index=old_index)
        if weight_key not in policy or bias_key not in policy:
            raise UpgradeError(f"checkpoint has no action branch {old_index}")
        weight = policy[weight_key]
        bias = policy[bias_key]
        if (
            getattr(weight, "ndim", None) != 2
            or getattr(bias, "ndim", None) != 1
            or weight.shape[0] != old_action["size"]
            or bias.shape[0] != old_action["size"]
        ):
            raise UpgradeError(f"checkpoint action branch {old_index} does not match its old schema")
        old_layers.append((weight, bias))

    for key in list(policy):
        if key.startswith("action_model._discrete_distribution.branches."):
            del policy[key]

    generator = np.random.RandomState(seed)
    old_by_new = {new_index: old_index for old_index, (new_index, _) in enumerate(actions)}
    if not old_layers:
        raise UpgradeError("checkpoint has no action branches")
    hidden = old_layers[0][0].shape[1]
    reference_weight, reference_bias = old_layers[0]
    for new_index, new_action in enumerate(new_actions):
        size = new_action["size"]
        if new_index in old_by_new:
            old_weight, old_bias = old_layers[old_by_new[new_index]]
            if old_weight.shape[1] != hidden:
                raise UpgradeError("action branches do not share a hidden width")
            old_size = old_weight.shape[0]
            weight = old_weight.new_empty((size, hidden))
            bias = old_bias.new_empty((size,))
            weight[:old_size] = old_weight
            bias[:old_size] = old_bias
            if size > old_size:
                weight[old_size:] = _random_rows(old_weight, size - old_size, hidden, generator)
                bias[old_size:] = old_bias.min() - NEW_CHOICE_BIAS_MARGIN
        else:
            weight = _random_rows(reference_weight, size, hidden, generator)
            bias = reference_bias.new_zeros((size,))
        policy[BRANCH_WEIGHT.format(index=new_index)] = weight
        policy[BRANCH_BIAS.format(index=new_index)] = bias

    sizes = [action["size"] for action in new_actions]
    discrete_sizes = policy.get("discrete_act_size_vector")
    if discrete_sizes is None:
        raise UpgradeError("checkpoint has no discrete_act_size_vector")
    policy["discrete_act_size_vector"] = discrete_sizes.new_tensor([sizes])
    if "act_size_vector_deprecated" in policy:
        policy["act_size_vector_deprecated"] = policy["act_size_vector_deprecated"].new_tensor(
            [sum(sizes)]
        )

    result.pop("Optimizer:value_optimizer", None)
    return result


def _global_step(checkpoint: dict) -> int:
    state = checkpoint.get("global_step")
    if not isinstance(state, dict) or not state:
        raise UpgradeError("checkpoint has no global step")
    value = next(iter(state.values()))
    try:
        return int(value.detach().cpu().item())
    except (AttributeError, TypeError, ValueError, RuntimeError) as error:
        raise UpgradeError("checkpoint global step is invalid") from error


def _without_checkpoint_lists(value):
    if isinstance(value, dict):
        return {
            key: _without_checkpoint_lists(item)
            for key, item in value.items()
            if not (key == "checkpoints" and isinstance(item, list))
        }
    if isinstance(value, list):
        return [_without_checkpoint_lists(item) for item in value]
    return value


def upgrade_run(
    runs_dir: Path,
    source_run: str,
    behavior: str,
    new_run: str,
    old_version: int,
    new_version: int,
) -> Path:
    """Create ``new_run`` from the newest numbered checkpoint in ``source_run``.

    Everything is read and upgraded before anything is written, and the run is written into a
    temporary sibling folder that is renamed into place at the end. Any failure raises
    :class:`UpgradeError` and leaves no partial ``new_run`` behind.
    """
    try:
        return _upgrade_run(runs_dir, source_run, behavior, new_run, old_version, new_version)
    except UpgradeError:
        raise
    except Exception as error:  # corrupt pickle, disk full, unexpected checkpoint layout, ...
        raise UpgradeError(f"{type(error).__name__}: {error}") from error


def _upgrade_run(
    runs_dir: Path,
    source_run: str,
    behavior: str,
    new_run: str,
    old_version: int,
    new_version: int,
) -> Path:
    import torch

    source_dir = runs_dir / source_run
    source_behavior = source_dir / behavior
    target_dir = runs_dir / new_run
    if target_dir.exists():
        raise UpgradeError(f"target run already exists: {new_run}")
    checkpoints = export_brain.checkpoints(source_behavior)
    if not checkpoints:
        raise UpgradeError(f"source run {source_run} has no numbered {behavior} checkpoint")
    _, source_checkpoint = checkpoints[-1]
    try:
        checkpoint = torch.load(source_checkpoint, map_location="cpu")
    except Exception as error:
        raise UpgradeError(f"cannot load {source_checkpoint.name}: {error}") from error
    try:
        old_schema = arena_trainer.load_schema(old_version)
        new_schema = arena_trainer.load_schema(new_version)
    except ValueError as error:
        raise UpgradeError(str(error)) from error
    upgraded = upgrade_state(checkpoint, old_schema, new_schema)
    source_step = _global_step(upgraded)

    status = None
    source_status = source_dir / "run_logs" / "training_status.json"
    if source_status.is_file():
        try:
            status = json.loads(source_status.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as error:
            raise UpgradeError(f"cannot copy curriculum state: {error}") from error

    partial_dir = runs_dir / f".{new_run}.partial"
    if partial_dir.exists():
        shutil.rmtree(partial_dir)
    try:
        partial_behavior = partial_dir / behavior
        partial_behavior.mkdir(parents=True)
        numbered = partial_behavior / f"{behavior}-{source_step}.pt"
        torch.save(upgraded, numbered)
        shutil.copyfile(numbered, partial_behavior / "checkpoint.pt")
        arena_trainer.write_schema_version(partial_dir, new_version)
        metadata = {
            "source_run": source_run,
            "source_step": source_step,
            "old_schema": old_version,
            "new_schema": new_version,
            "created_at": datetime.now(timezone.utc).isoformat(),
        }
        (partial_dir / "upgraded_from.json").write_text(
            json.dumps(metadata, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
        )
        if status is not None:
            partial_logs = partial_dir / "run_logs"
            partial_logs.mkdir()
            (partial_logs / "training_status.json").write_text(
                json.dumps(_without_checkpoint_lists(status), indent=4) + "\n", encoding="utf-8"
            )
        arena_trainer.replace_directory(partial_dir, target_dir)
    except BaseException:
        shutil.rmtree(partial_dir, ignore_errors=True)
        raise
    return target_dir / behavior


def create_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--runs-dir", type=Path, default=arena_trainer.DEFAULT_RESULTS_DIRECTORY)
    parser.add_argument("--source", required=True, help="Source run id.")
    parser.add_argument("--new-run", required=True, help="New run id.")
    parser.add_argument(
        "--behavior",
        type=arena_trainer.behavior_argument,
        help="Hero behavior (default: the source run's class, e.g. Mage for mage-s001; else Warrior).",
    )
    parser.add_argument("--old-version", type=int)
    parser.add_argument("--new-version", type=int, default=arena_trainer.SCHEMA_VERSION)
    return parser


def main(argv: Iterable[str] | None = None) -> int:
    args = create_parser().parse_args(list(argv) if argv is not None else None)
    if args.behavior is None:
        args.behavior = arena_trainer.run_class(args.source) or "Warrior"
    old_version = args.old_version
    if old_version is None:
        old_version = arena_trainer.run_schema_version(args.runs_dir / args.source)
    output = upgrade_run(
        args.runs_dir,
        args.source,
        args.behavior,
        args.new_run,
        old_version,
        args.new_version,
    )
    print(f"Upgraded {args.source} schema v{old_version} -> v{args.new_version}: {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
