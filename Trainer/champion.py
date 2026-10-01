"""Evaluate Survivor checkpoints and safely maintain champion brains."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import glob
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import threading
from typing import Callable, Iterable

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from Trainer import arena_trainer  # noqa: E402

CHAMPIONS_DIR = "champions"
EVALUATION_INTERVAL = 2_000_000
EVALUATION_SEEDS = 100
EVALUATION_SEED_START = 1_000_000
EVALUATION_TIER = 1
EVALUATION_RUN_SECONDS = 900.0
PROMOTION_MARGIN = 5.0

_build_lock = threading.Lock()
_built_dll: Path | None = None


def evaluation_settings() -> dict:
    return {
        "seeds": EVALUATION_SEEDS,
        "seed_start": EVALUATION_SEED_START,
        "tier": EVALUATION_TIER,
        "run_seconds": EVALUATION_RUN_SECONDS,
        "deterministic": True,
    }


def calculate_score(summary: dict) -> float:
    return (
        float(summary["MedianSurvivedSeconds"])
        + 0.5 * float(summary["P10SurvivedSeconds"])
        + 300.0 * float(summary["WinRate"])
        - 60.0 * int(summary["CatastrophicCount"])
    )


score = calculate_score


def should_promote(challenger: dict, current: dict | None, schema_version: int) -> bool:
    if current is None or int(current.get("schema_version", -1)) != schema_version:
        return True
    return (
        float(challenger["score"]) > float(current["score"]) + PROMOTION_MARGIN
        and _catastrophic_count(challenger) <= _catastrophic_count(current)
    )


def champion_directory(results_dir: Path, behavior: str) -> Path:
    return results_dir / CHAMPIONS_DIR / behavior


def load_champion(results_dir: Path, behavior: str) -> dict | None:
    return _read_json(champion_directory(results_dir, behavior) / "champion.json")


def latest_evaluation(results_dir: Path, behavior: str) -> dict | None:
    return _read_json(champion_directory(results_dir, behavior) / "latest_eval.json")


def last_evaluated_step(results_dir: Path, behavior: str, run_id: str) -> int:
    path = champion_directory(results_dir, behavior) / "evaluations.jsonl"
    latest = 0
    try:
        for line in path.read_text(encoding="utf-8").splitlines():
            try:
                item = json.loads(line)
            except json.JSONDecodeError:
                continue
            if item.get("run_id") == run_id and not item.get("restored"):
                latest = max(latest, int(item.get("step", 0)))
    except OSError:
        pass
    return latest


class EvaluationRunner:
    """Runs the evaluator while exposing cancellation to the training service."""

    def __init__(self) -> None:
        self.process: subprocess.Popen | None = None
        self._cancelled = threading.Event()
        self._lock = threading.Lock()

    def cancel(self) -> None:
        self._cancelled.set()
        with self._lock:
            process = self.process
        if process is not None and process.poll() is None:
            process.kill()

    def run(self, brain: Path, output: Path, log: Callable[[str], None] = print) -> dict:
        dotnet = shutil.which("dotnet")
        if dotnet is None:
            message = "evaluation skipped: dotnet not found"
            log(message)
            return {"skipped": True, "message": message}
        if self._cancelled.is_set():
            return {"cancelled": True, "message": "evaluation cancelled"}
        dll = _build_evaluator(dotnet)
        settings = evaluation_settings()
        command = [
            dotnet, str(dll), "--brain", str(brain), "--out", str(output),
            "--seeds", str(settings["seeds"]), "--seed-start", str(settings["seed_start"]),
            "--tier", str(settings["tier"]), "--run-seconds", str(settings["run_seconds"]),
            "--threads", str(max(1, (os.cpu_count() or 1) // 4)), "--deterministic",
        ]
        output.parent.mkdir(parents=True, exist_ok=True)
        with self._lock:
            if self._cancelled.is_set():
                return {"cancelled": True, "message": "evaluation cancelled"}
            self.process = subprocess.Popen(
                command,
                cwd=arena_trainer.repository_root(),
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                creationflags=_creation_flags(),
            )
            process = self.process
        stdout, _ = process.communicate()
        with self._lock:
            self.process = None
        if self._cancelled.is_set():
            return {"cancelled": True, "message": "evaluation cancelled"}
        if process.returncode != 0:
            raise RuntimeError(f"SurvivorEval failed ({process.returncode}): {stdout.strip()}")
        return json.loads(output.read_text(encoding="utf-8"))


def evaluate_latest(
    results_dir: Path,
    behavior: str = "Warrior",
    run_id: str | None = None,
    runner: EvaluationRunner | None = None,
    log: Callable[[str], None] = print,
) -> dict:
    from Trainer import export_brain

    results_dir = Path(results_dir)
    behavior_dir = (
        results_dir / run_id / behavior
        if run_id is not None
        else export_brain.newest_behavior_dir(results_dir, behavior, arena_trainer.SCHEMA_VERSION)
    )
    checkpoints = export_brain.checkpoints(behavior_dir) if behavior_dir.is_dir() else []
    if not checkpoints:
        raise FileNotFoundError(f"No {behavior} checkpoint found under {results_dir}")
    step, checkpoint = checkpoints[-1]
    actual_run_id = behavior_dir.parent.name
    directory = champion_directory(results_dir, behavior)
    candidate_dir = directory / "candidates"
    candidate_brain = candidate_dir / f"{actual_run_id}-{step}.brain"
    candidate_eval = candidate_dir / f"{actual_run_id}-{step}.eval.json"
    evaluator = runner or EvaluationRunner()
    if type(evaluator) is EvaluationRunner and shutil.which("dotnet") is None:
        message = "evaluation skipped: dotnet not found"
        log(message)
        return {"skipped": True, "message": message}
    export_brain.export_checkpoint(checkpoint, candidate_brain)
    raw = evaluator.run(candidate_brain, candidate_eval, log=log)
    if raw.get("skipped") or raw.get("cancelled"):
        candidate_brain.unlink(missing_ok=True)
        candidate_eval.unlink(missing_ok=True)
        return raw
    result = apply_evaluation(
        results_dir, behavior, actual_run_id, step, candidate_brain, raw,
        runner=evaluator, log=log,
    )
    try:
        from Trainer import brain_lineage  # late import: brain_lineage imports this module

        if "won" in result:
            brain_lineage.record_evaluation(
                results_dir, behavior, actual_run_id, step, candidate_brain, checkpoint, result,
                log=log,
            )
    except Exception as error:  # noqa: BLE001 - lineage must never break the evaluator
        log(f"lineage: {error}")
    if not result["won"]:
        candidate_brain.unlink(missing_ok=True)
        candidate_eval.unlink(missing_ok=True)
    return result


def apply_evaluation(
    results_dir: Path,
    behavior: str,
    run_id: str,
    step: int,
    candidate_brain: Path,
    raw: dict,
    runner: EvaluationRunner | None = None,
    log: Callable[[str], None] = print,
) -> dict:
    directory = champion_directory(Path(results_dir), behavior)
    directory.mkdir(parents=True, exist_ok=True)
    current = load_champion(Path(results_dir), behavior)
    current_settings = raw.get("settings", evaluation_settings())
    if current is not None and current.get("settings") != current_settings:
        champion_brain = directory / "champion.brain"
        reevaluation_file = directory / "candidates" / "champion-reeval.json"
        reevaluator = runner or EvaluationRunner()
        reevaluated = reevaluator.run(champion_brain, reevaluation_file, log=log)
        if reevaluated.get("skipped") or reevaluated.get("cancelled"):
            return reevaluated
        current = _metadata(
            str(current["run_id"]), int(current["step"]), reevaluated,
            int(current.get("schema_version", arena_trainer.SCHEMA_VERSION)),
        )
        _write_json_atomic(directory / "champion.json", current)
        _record_evaluation(directory, _evaluation_line(current, won=False))

    challenger = _metadata(run_id, step, raw, arena_trainer.SCHEMA_VERSION)
    won = should_promote(challenger, current, arena_trainer.SCHEMA_VERSION)
    line = _evaluation_line(challenger, won=won)
    _record_evaluation(directory, line)
    if won:
        history = directory / "history"
        history.mkdir(parents=True, exist_ok=True)
        stem = f"{run_id}-{step}"
        history_brain = history / f"{stem}.brain"
        history_json = history / f"{stem}.json"
        if not history_brain.exists():
            _copy_atomic(candidate_brain, history_brain)
        _write_json_atomic(history_json, challenger)
        _copy_atomic(candidate_brain, directory / "champion.brain")
        _write_json_atomic(directory / "champion.json", challenger)
        log(f"champion promoted: {run_id} step {step:,} score {challenger['score']:.1f}")
    return line


def restore(results_dir: Path, behavior: str, identifier: str) -> dict:
    directory = champion_directory(Path(results_dir), behavior)
    history_brain = directory / "history" / f"{identifier}.brain"
    history_json = directory / "history" / f"{identifier}.json"
    if not history_brain.is_file() or not history_json.is_file():
        raise FileNotFoundError(f"Champion history entry not found: {identifier}")
    metadata = json.loads(history_json.read_text(encoding="utf-8"))
    _copy_atomic(history_brain, directory / "champion.brain")
    _write_json_atomic(directory / "champion.json", metadata)
    line = _evaluation_line(metadata, won=True)
    line["restored"] = True
    _record_evaluation(directory, line)
    return line


def _metadata(run_id: str, step: int, raw: dict, schema_version: int) -> dict:
    summary = raw["summary"]
    behavior = summary.get("Behavior", summary.get("behavior", {}))
    return {
        "run_id": run_id,
        "step": int(step),
        "schema_version": int(schema_version),
        "score": calculate_score(summary),
        "passes_m4a": bool(raw.get("passes_m4a", False)),
        "summary": summary,
        "behavior": behavior,
        "settings": raw.get("settings", evaluation_settings()),
        "evaluated_at": _now(),
        "brain_file": "champion.brain",
    }


def _evaluation_line(metadata: dict, won: bool) -> dict:
    return {
        "run_id": metadata["run_id"], "step": metadata["step"],
        "score": metadata["score"], "passes_m4a": metadata["passes_m4a"], "won": won,
        "summary": metadata["summary"], "behavior": metadata["behavior"],
        "settings": metadata["settings"], "time": metadata.get("evaluated_at", _now()),
    }


def _record_evaluation(directory: Path, line: dict) -> None:
    directory.mkdir(parents=True, exist_ok=True)
    with (directory / "evaluations.jsonl").open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(line, separators=(",", ":")) + "\n")
        handle.flush()
    _write_json_atomic(directory / "latest_eval.json", line, pretty=True)


def _catastrophic_count(item: dict) -> int:
    summary = item.get("summary", item)
    return int(summary["CatastrophicCount"])


def _build_evaluator(dotnet: str) -> Path:
    global _built_dll
    with _build_lock:
        if _built_dll is not None and _built_dll.is_file():
            return _built_dll
        command = [dotnet, "build", "Tools/SurvivorEval", "-c", "Release"]
        completed = subprocess.run(
            command, cwd=arena_trainer.repository_root(), capture_output=True, text=True,
            creationflags=_creation_flags(), check=False,
        )
        if completed.returncode != 0:
            raise RuntimeError("SurvivorEval build failed: " + (completed.stdout + completed.stderr).strip())
        pattern = str(arena_trainer.repository_root() / "Tools" / "SurvivorEval" / "bin" / "Release" / "*" / "SurvivorEval.dll")
        matches = [Path(path) for path in glob.glob(pattern)]
        if not matches:
            raise RuntimeError("SurvivorEval build succeeded but SurvivorEval.dll was not found")
        _built_dll = max(matches, key=lambda path: path.stat().st_mtime_ns)
        return _built_dll


def _creation_flags() -> int:
    return int(getattr(subprocess, "CREATE_NO_WINDOW", 0)) if sys.platform == "win32" else 0


def _copy_atomic(source: Path, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_name(destination.name + ".tmp")
    shutil.copyfile(source, temporary)
    os.replace(temporary, destination)


def _write_json_atomic(path: Path, payload: dict, pretty: bool = True) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_text(json.dumps(payload, indent=2 if pretty else None), encoding="utf-8")
    os.replace(temporary, path)


def _read_json(path: Path) -> dict | None:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None


def _now() -> str:
    return datetime.now(timezone.utc).isoformat()


def main(argv: Iterable[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results-dir", type=Path, default=arena_trainer.DEFAULT_RESULTS_DIRECTORY)
    parser.add_argument("--behavior", type=arena_trainer.behavior_argument, default="Warrior")
    subparsers = parser.add_subparsers(dest="command", required=True)
    status_parser = subparsers.add_parser("status")
    status_parser.set_defaults(command="status")
    evaluate_parser = subparsers.add_parser("evaluate")
    evaluate_parser.add_argument("--run-id")
    restore_parser = subparsers.add_parser("restore")
    restore_parser.add_argument("identifier")
    args = parser.parse_args(list(argv) if argv is not None else None)
    results_dir = arena_trainer.resolve_path(str(args.results_dir), arena_trainer.repository_root())
    try:
        if args.command == "status":
            print(json.dumps({"champion": load_champion(results_dir, args.behavior), "last_evaluation": latest_evaluation(results_dir, args.behavior)}, indent=2))
        elif args.command == "evaluate":
            print(json.dumps(evaluate_latest(results_dir, args.behavior, args.run_id), indent=2))
        else:
            print(json.dumps(restore(results_dir, args.behavior, args.identifier), indent=2))
        return 0
    except (OSError, RuntimeError, ValueError, KeyError) as error:
        print(f"champion: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
