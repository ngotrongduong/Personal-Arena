"""Brain lineage (M6, D-037): saved brain versions, branches and forks.

A *version* is a saved brain of one run at one step, kept under
``runs/champions/<Behavior>/lineage/versions/<run>-<step>/`` (``brain.brain``, ``version.json``
and, while it is kept, the ML-Agents ``checkpoint.pt`` needed to train on from it). A *branch* is
a training run of that class (``warrior-sNNN``, ``mage-sNNN``, ``archer-sNNN``). Every behavior
has its own lineage folder, so classes never share versions or branches. Forking copies a
version's checkpoint into a new run; existing runs are never changed or deleted.

File ownership: this module writes ``versions/`` and ``branches.json``; the viewer writes
``labels.json`` (names and pins), which this module only reads.
"""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
import shutil
import sys
import time
from typing import Callable, Iterable
import uuid

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from Trainer import arena_trainer, champion, export_brain  # noqa: E402

LINEAGE_DIR = "lineage"
VERSIONS_DIR = "versions"
BRANCHES_FILE = "branches.json"
LABELS_FILE = "labels.json"
VERSION_FILE = "version.json"
BRAIN_FILE = "brain.brain"
CHECKPOINT_FILE = "checkpoint.pt"
STATUS_FILE = "training_status.json"

KEEP_CHECKPOINTS = 3
KEEP_RECENT_VERSIONS = 10
THIN_BUCKET_STEPS = 10_000_000
SETTLED_SECONDS = 20.0
_ID_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_.-]{0,63}$")


class LineageError(RuntimeError):
    """A lineage operation cannot be done; the message is shown to the owner (Vietnamese)."""


def check_id(value: str | None, what: str) -> str | None:
    """Reject names that could leave the runs folder (``..``, slashes) or hit reserved folders."""
    if value is None:
        return None
    if not _ID_PATTERN.match(value) or value.lower() == champion.CHAMPIONS_DIR.lower():
        raise LineageError(f"Tên {what} không hợp lệ: {value}")
    return value


def lineage_directory(results_dir: Path, behavior: str) -> Path:
    return champion.champion_directory(Path(results_dir), behavior) / LINEAGE_DIR


def versions_directory(results_dir: Path, behavior: str) -> Path:
    return lineage_directory(results_dir, behavior) / VERSIONS_DIR


def version_id(run_id: str, step: int) -> str:
    return f"{run_id}-{int(step)}"


def load_versions(results_dir: Path, behavior: str) -> list[dict]:
    """Every complete version (a folder with ``version.json`` and ``brain.brain``), oldest first."""
    directory = versions_directory(results_dir, behavior)
    found: list[dict] = []
    if not directory.is_dir():
        return found
    for folder in directory.iterdir():
        if not folder.is_dir() or folder.name.startswith("."):
            continue
        data = _read_json(folder / VERSION_FILE)
        if data is None or not (folder / BRAIN_FILE).is_file():
            continue
        data["_path"] = folder
        found.append(data)
    found.sort(key=lambda item: (str(item.get("run_id", "")), int(item.get("step", 0))))
    return found


def load_branches(results_dir: Path, behavior: str) -> list[dict]:
    data = _read_json(lineage_directory(results_dir, behavior) / BRANCHES_FILE) or {}
    branches = data.get("branches")
    return [item for item in branches if isinstance(item, dict)] if isinstance(branches, list) else []


def pinned_versions(results_dir: Path, behavior: str) -> set[str]:
    data = _read_json(lineage_directory(results_dir, behavior) / LABELS_FILE) or {}
    versions = data.get("versions")
    if not isinstance(versions, list):
        return set()
    return {
        str(item["id"])
        for item in versions
        if isinstance(item, dict) and item.get("id") and item.get("pinned") is True
    }


def sync(results_dir: Path, behavior: str = "Warrior") -> int:
    """Import champion history entries that are not versions yet. Returns how many were added."""
    results_dir = Path(results_dir)
    history = champion.champion_directory(results_dir, behavior) / "history"
    if not history.is_dir():
        return 0
    added = 0
    for metadata_path in sorted(history.glob("*.json")):
        brain = metadata_path.with_suffix(".brain")
        metadata = _read_json(metadata_path)
        if metadata is None or not brain.is_file():
            continue
        try:
            run_id = str(metadata["run_id"])
            step = int(metadata["step"])
        except (KeyError, TypeError, ValueError):
            continue
        target = versions_directory(results_dir, behavior) / version_id(run_id, step)
        if (target / VERSION_FILE).is_file():
            continue
        checkpoint = results_dir / run_id / behavior / f"{behavior}-{step}.pt"
        _write_version(
            target,
            brain,
            checkpoint if checkpoint.is_file() else None,
            None,
            _version_metadata(
                run_id, step, "history", metadata,
                schema_version=int(metadata.get("schema_version", arena_trainer.SCHEMA_VERSION)),
                champion_flag=True,
            ),
        )
        added += 1
    return added


def record_evaluation(
    results_dir: Path,
    behavior: str,
    run_id: str,
    step: int,
    brain: Path,
    checkpoint: Path | None,
    line: dict,
    log: Callable[[str], None] = print,
) -> Path | None:
    """Save an evaluated checkpoint as a version. Never raises (lineage must not break training)."""
    try:
        results_dir = Path(results_dir)
        target = versions_directory(results_dir, behavior) / version_id(run_id, step)
        if not (target / VERSION_FILE).is_file():
            _write_version(
                target,
                Path(brain),
                Path(checkpoint) if checkpoint is not None and Path(checkpoint).is_file() else None,
                results_dir / run_id / "run_logs" / STATUS_FILE,
                _version_metadata(
                    run_id, step, "eval", line,
                    schema_version=arena_trainer.SCHEMA_VERSION,
                    champion_flag=bool(line.get("won")),
                ),
            )
        sync(results_dir, behavior)
        prune(results_dir, behavior)
        return target
    except Exception as error:  # noqa: BLE001 - the evaluator must keep working
        log(f"lineage: could not save version {run_id} step {step}: {error}")
        return None


def snapshot(
    results_dir: Path,
    behavior: str,
    run_id: str,
    evaluate: bool = True,
    runner: champion.EvaluationRunner | None = None,
    now: Callable[[], float] = time.time,
    log: Callable[[str], None] = print,
) -> dict:
    """Save the newest settled checkpoint of ``run_id`` as a manual version."""
    check_id(run_id, "nhánh")
    results_dir = Path(results_dir)
    behavior_dir = results_dir / run_id / behavior
    if not behavior_dir.is_dir():
        raise LineageError(f"Không tìm thấy nhánh {run_id}.")
    if arena_trainer.run_schema_version(results_dir / run_id) != arena_trainer.SCHEMA_VERSION:
        raise LineageError(f"Nhánh {run_id} dùng luật cũ nên không lưu được.")
    found = export_brain.checkpoints(behavior_dir)
    settled = [
        (step, path) for step, path in found
        if now() - _modified(path) >= SETTLED_SECONDS
    ]
    if not settled:
        raise LineageError(f"Nhánh {run_id} chưa có bản lưu huấn luyện nào.")

    for step, checkpoint in reversed(settled):
        target = versions_directory(results_dir, behavior) / version_id(run_id, step)
        if (target / VERSION_FILE).is_file():
            existing = _read_json(target / VERSION_FILE) or {}
            if existing.get("source") != "manual":
                existing["source"] = "manual"
                existing.pop("_path", None)
                _write_json_atomic(target / VERSION_FILE, existing)
            result = {"ok": True, "id": target.name, "run_id": run_id, "step": step, "existing": True}
            if evaluate and not existing.get("evaluated"):
                _evaluate_into(result, target, runner, log)
            return result
        staging = target.parent / f".{target.name}.brain"
        staging.parent.mkdir(parents=True, exist_ok=True)
        try:
            export_brain.export_checkpoint(checkpoint, staging)
        except Exception as error:  # noqa: BLE001 - a checkpoint still being written; try the previous one
            staging.unlink(missing_ok=True)
            log(f"lineage: cannot export {checkpoint.name}: {error}")
            continue
        try:
            _write_version(
                target, staging, checkpoint, results_dir / run_id / "run_logs" / STATUS_FILE,
                _version_metadata(
                    run_id, step, "manual", None,
                    schema_version=arena_trainer.SCHEMA_VERSION, champion_flag=False,
                ),
            )
        finally:
            staging.unlink(missing_ok=True)
        result = {"ok": True, "id": target.name, "run_id": run_id, "step": step}
        if evaluate:
            _evaluate_into(result, target, runner, log)
        return result
    raise LineageError(f"Không đọc được bản lưu huấn luyện nào của nhánh {run_id}.")


def fork(
    results_dir: Path,
    behavior: str = "Warrior",
    version: str | None = None,
    run_id: str | None = None,
    now: Callable[[], float] = time.time,
    log: Callable[[str], None] = print,
) -> dict:
    """Start a new branch (run) from a version, or clone the newest state of branch ``run_id``."""
    results_dir = Path(results_dir)
    check_id(version, "phiên bản")
    check_id(run_id, "nhánh")
    if (version is None) == (run_id is None):
        raise LineageError("Cần chọn đúng một phiên bản hoặc một nhánh để rẽ nhánh.")
    if run_id is not None:
        version = snapshot(results_dir, behavior, run_id, evaluate=False, now=now, log=log)["id"]

    source = versions_directory(results_dir, behavior) / str(version)
    metadata = _read_json(source / VERSION_FILE)
    if metadata is None or not (source / BRAIN_FILE).is_file():
        raise LineageError(f"Không tìm thấy phiên bản {version}.")
    checkpoint = source / CHECKPOINT_FILE
    if not checkpoint.is_file():
        raise LineageError("Bản này chỉ còn não để xem, không học tiếp được.")
    if int(metadata.get("schema_version", -1)) != arena_trainer.SCHEMA_VERSION:
        raise LineageError("Phiên bản này dùng luật cũ nên không rẽ nhánh được.")

    from Trainer import train_service  # late import: train_service imports this module

    step = int(metadata["step"])
    new_run = train_service.next_run_id(results_dir, behavior)
    target = results_dir / new_run
    partial = results_dir / f".{new_run}.partial"
    if partial.exists():
        shutil.rmtree(partial)
    try:
        partial_behavior = partial / behavior
        partial_behavior.mkdir(parents=True)
        # copy2 keeps the old file times, so a fork never looks like the newest training.
        shutil.copy2(checkpoint, partial_behavior / f"{behavior}-{step}.pt")
        shutil.copy2(checkpoint, partial_behavior / "checkpoint.pt")
        arena_trainer.write_schema_version(partial, arena_trainer.SCHEMA_VERSION)
        status = _read_json(source / STATUS_FILE)
        if status is not None:
            (partial / "run_logs").mkdir()
            (partial / "run_logs" / STATUS_FILE).write_text(
                json.dumps(_without_checkpoint_lists(status), indent=4) + "\n", encoding="utf-8"
            )
        forked = {
            "source_run": metadata.get("run_id"),
            "source_step": step,
            "source_version": source.name,
            "created_at": _now(),
        }
        (partial / "forked_from.json").write_text(
            json.dumps(forked, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
        )
        if target.exists():
            raise LineageError(f"Nhánh {new_run} đã tồn tại.")
        arena_trainer.replace_directory(partial, target)
    except BaseException:
        shutil.rmtree(partial, ignore_errors=True)
        raise

    result = {"ok": True, "run_id": new_run, "id": source.name, "step": step}
    lineage = lineage_directory(results_dir, behavior)
    try:
        if _unreadable(lineage / BRANCHES_FILE):
            raise OSError("branches.json is damaged")
        branches = load_branches(results_dir, behavior)
        branches.append({
            "run_id": new_run,
            "parent_run": metadata.get("run_id"),
            "parent_step": step,
            "parent_version": source.name,
            "created_at": forked["created_at"],
        })
        _write_json_atomic(lineage / BRANCHES_FILE, {"branches": branches})
    except Exception as error:  # noqa: BLE001 - the new run exists and trains; only its tree link is lost
        log(f"lineage: branch {new_run} created but not listed: {error}")
        result["warning"] = f"Đã tạo nhánh {new_run} nhưng chưa ghi được cây nhánh."
    log(f"lineage: new branch {new_run} from {source.name}")
    return result


def prune(results_dir: Path, behavior: str = "Warrior") -> dict:
    """Apply the D-037 retention rules. Only version copies are removed, never a run's own files."""
    results_dir = Path(results_dir)
    lineage = lineage_directory(results_dir, behavior)
    if _unreadable(lineage / LABELS_FILE) or _unreadable(lineage / BRANCHES_FILE):
        # Pins or fork parents are unknown right now (file half-written or damaged): remove nothing.
        return {"removed_checkpoints": 0, "removed_versions": 0, "skipped": True}
    versions = load_versions(results_dir, behavior)
    pinned = pinned_versions(results_dir, behavior)
    parents = {str(item.get("parent_version")) for item in load_branches(results_dir, behavior)}

    def protected(item: dict) -> bool:
        return (
            bool(item.get("champion"))
            or item.get("source") == "manual"
            or str(item.get("id")) in pinned
            or str(item.get("id")) in parents
        )

    removed_checkpoints = 0
    removed_versions = 0
    by_run: dict[str, list[dict]] = {}
    for item in versions:
        by_run.setdefault(str(item.get("run_id")), []).append(item)

    for items in by_run.values():
        newest_first = sorted(items, key=lambda item: int(item.get("step", 0)), reverse=True)
        unprotected = [item for item in newest_first if not protected(item)]

        # Thin old automatic versions: one per bucket, the best score (then the newest step).
        survivors = unprotected[:KEEP_RECENT_VERSIONS]
        best_in_bucket: dict[int, dict] = {}
        for item in unprotected[KEEP_RECENT_VERSIONS:]:
            bucket = int(item.get("step", 0)) // THIN_BUCKET_STEPS
            current = best_in_bucket.get(bucket)
            if current is None or _rank(item) > _rank(current):
                best_in_bucket[bucket] = item
        keep = {id(item) for item in survivors} | {id(item) for item in best_in_bucket.values()}
        for item in unprotected:
            if id(item) not in keep:
                shutil.rmtree(item["_path"], ignore_errors=True)
                removed_versions += 1

        # Checkpoints: protected versions keep theirs, plus the newest few others.
        kept_others = 0
        for item in newest_first:
            if id(item) not in keep and not protected(item):
                continue
            checkpoint = item["_path"] / CHECKPOINT_FILE
            if not checkpoint.is_file() or protected(item):
                continue
            if kept_others < KEEP_CHECKPOINTS:
                kept_others += 1
                continue
            checkpoint.unlink(missing_ok=True)
            data = {key: value for key, value in item.items() if key != "_path"}
            data["has_checkpoint"] = False
            _write_json_atomic(item["_path"] / VERSION_FILE, data)
            removed_checkpoints += 1
    return {"removed_checkpoints": removed_checkpoints, "removed_versions": removed_versions}


def _rank(item: dict) -> tuple[float, int]:
    return (float(item.get("score", 0.0) or 0.0), int(item.get("step", 0)))


def _evaluate_into(result: dict, target: Path, runner: champion.EvaluationRunner | None, log) -> None:
    """Score a saved version; a failed evaluation leaves it saved (unscored) with a warning."""
    try:
        _evaluate_version(target, runner, log)
    except Exception as error:  # noqa: BLE001 - the version itself is already saved
        log(f"lineage: evaluation of {target.name} failed: {error}")
        result["warning"] = "Đã lưu phiên bản nhưng chưa chấm điểm được."


def _evaluate_version(target: Path, runner: champion.EvaluationRunner | None, log) -> None:
    evaluator = runner or champion.EvaluationRunner()
    output = target / "eval.json"
    raw = evaluator.run(target / BRAIN_FILE, output, log=log)
    output.unlink(missing_ok=True)
    if raw.get("skipped") or raw.get("cancelled"):
        return
    data = _read_json(target / VERSION_FILE) or {}
    data.update(_evaluated_fields(raw))
    _write_json_atomic(target / VERSION_FILE, data)


def _evaluated_fields(raw: dict) -> dict:
    summary = raw["summary"]
    return {
        "evaluated": True,
        "score": champion.calculate_score(summary),
        "passes_m4a": bool(raw.get("passes_m4a", False)),
        "summary": summary,
        "behavior": summary.get("Behavior", summary.get("behavior", {})),
        "settings": raw.get("settings", champion.evaluation_settings()),
        "evaluated_at": _now(),
    }


def _version_metadata(
    run_id: str,
    step: int,
    source: str,
    evaluation: dict | None,
    schema_version: int,
    champion_flag: bool,
) -> dict:
    data = {
        "id": version_id(run_id, step),
        "run_id": run_id,
        "step": int(step),
        "schema_version": int(schema_version),
        "source": source,
        "created_at": _now(),
        "evaluated": False,
        "score": 0.0,
        "champion": bool(champion_flag),
        "passes_m4a": False,
        "brain_file": BRAIN_FILE,
    }
    if evaluation is not None and isinstance(evaluation.get("summary"), dict):
        data.update({
            "evaluated": True,
            "score": float(evaluation.get("score", champion.calculate_score(evaluation["summary"]))),
            "passes_m4a": bool(evaluation.get("passes_m4a", False)),
            "summary": evaluation["summary"],
            "behavior": evaluation.get("behavior") or evaluation["summary"].get("Behavior", {}),
            "settings": evaluation.get("settings"),
            "evaluated_at": evaluation.get("evaluated_at") or evaluation.get("time") or _now(),
        })
    return data


def _write_version(
    target: Path,
    brain: Path,
    checkpoint: Path | None,
    status: Path | None,
    metadata: dict,
) -> None:
    """Build the version in a hidden ``.<id>.partial`` folder, then move it into place whole."""
    staging = target.parent / f".{target.name}.partial"
    if staging.exists():
        shutil.rmtree(staging)
    try:
        staging.mkdir(parents=True)
        shutil.copyfile(brain, staging / BRAIN_FILE)
        if checkpoint is not None:
            shutil.copy2(checkpoint, staging / CHECKPOINT_FILE)  # keep the training time
        if status is not None and status.is_file():
            data = _read_json(status)
            if data is not None:
                _write_json_atomic(staging / STATUS_FILE, _without_checkpoint_lists(data))
        metadata["has_checkpoint"] = (staging / CHECKPOINT_FILE).is_file()
        _write_json_atomic(staging / VERSION_FILE, metadata)
        if target.exists():  # an orphan left by an older build (no version.json)
            shutil.rmtree(target)
        arena_trainer.replace_directory(staging, target)
    except BaseException:
        shutil.rmtree(staging, ignore_errors=True)
        raise


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



def _modified(path: Path) -> float:
    try:
        return path.stat().st_mtime
    except OSError:
        return float("inf")


def _write_json_atomic(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    # A name of its own per writer: the trainer and a viewer command may write at the same time.
    temporary = path.with_name(f".{path.name}.{os.getpid()}.{uuid.uuid4().hex[:8]}.tmp")
    try:
        temporary.write_text(json.dumps(payload, indent=2, ensure_ascii=False), encoding="utf-8")
        os.replace(temporary, path)
    except BaseException:
        temporary.unlink(missing_ok=True)
        raise


def _unreadable(path: Path) -> bool:
    """True when ``path`` exists but is not a readable JSON object."""
    return path.exists() and _read_json(path) is None


def _read_json(path: Path) -> dict | None:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError, UnicodeDecodeError):
        return None
    return data if isinstance(data, dict) else None


def _now() -> str:
    return datetime.now(timezone.utc).isoformat()


def create_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results-dir", type=Path, default=arena_trainer.DEFAULT_RESULTS_DIRECTORY)
    parser.add_argument("--behavior", type=arena_trainer.behavior_argument, default="Warrior")
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("sync")
    commands.add_parser("prune")
    snapshot_parser = commands.add_parser("snapshot")
    snapshot_parser.add_argument("--run-id", required=True)
    snapshot_parser.add_argument("--no-evaluate", action="store_true")
    fork_parser = commands.add_parser("fork")
    source = fork_parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--version")
    source.add_argument("--run-id")
    return parser


def main(argv: Iterable[str] | None = None) -> int:
    args = create_parser().parse_args(list(argv) if argv is not None else None)
    results_dir = arena_trainer.resolve_path(str(args.results_dir), arena_trainer.repository_root())

    def log(message: str) -> None:
        print(message, file=sys.stderr)

    try:
        check_id(args.behavior, "hành vi")
        check_id(getattr(args, "run_id", None), "nhánh")
        check_id(getattr(args, "version", None), "phiên bản")
        if args.command == "sync":
            result = {"ok": True, "imported": sync(results_dir, args.behavior)}
        elif args.command == "prune":
            result = {"ok": True, **prune(results_dir, args.behavior)}
        elif args.command == "snapshot":
            result = snapshot(results_dir, args.behavior, args.run_id, not args.no_evaluate, log=log)
            try:
                prune(results_dir, args.behavior)
            except Exception as error:  # noqa: BLE001 - the snapshot is saved; tidying can wait
                log(f"lineage: prune after snapshot failed: {error}")
        else:
            result = fork(results_dir, args.behavior, version=args.version, run_id=args.run_id, log=log)
    except LineageError as error:
        print(json.dumps({"ok": False, "error": str(error)}, ensure_ascii=False))
        return 1
    except Exception as error:  # noqa: BLE001 - the viewer shows every failure to the owner
        print(json.dumps({"ok": False, "error": f"Lỗi: {type(error).__name__}: {error}"}, ensure_ascii=False))
        return 1
    print(json.dumps(result, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    raise SystemExit(main())
