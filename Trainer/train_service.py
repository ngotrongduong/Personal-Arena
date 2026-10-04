"""Background training service behind the viewer's "Train the AI" button.

The viewer (``Build/Watch/PersonalArenaWatch.exe``) starts this script in a hidden console::

    .venv-ml/Scripts/python.exe Trainer/train_service.py --parent-pid <viewer pid>

``--behavior Warrior|Mage|Archer`` picks the hero class (M7); everything below is per class.
The service resumes the class's newest run (or starts ``<class>-s001``, e.g. ``mage-s001``),
appends ML-Agents output to ``Trainer/runs/<run>.log``, keeps exporting ``latest.brain`` so the
viewer hot-loads every new checkpoint, and reports progress in ``Trainer/runs/training_service.json``.

It stops gracefully when ``Trainer/runs/training_service.stop`` appears or the viewer exits:
it sends the same Ctrl+C a terminal would, so ML-Agents saves a checkpoint before quitting.
When a run reaches ``max_steps`` it is resumed with a larger budget, so training continues
until the owner presses Stop. When ML-Agents crashes (for example inside the GPU driver) the
service resumes it from the last checkpoint; after repeated crashes it trains on the CPU.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass, field
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys
import threading
import time
import traceback
from typing import Callable, Iterable, TextIO

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from Trainer import arena_trainer, brain_lineage, brain_upgrade, export_brain  # noqa: E402
from Trainer import champion  # noqa: E402

STATUS_NAME = "training_service.json"
STOP_NAME = "training_service.stop"
LOCK_NAME = "training_service.lock"
LOCK_WAIT = 15.0
ERROR_LOG_NAME = "training_service.err.log"
DEFAULT_ARENA_AGENTS = 16  # TrainingArenaHost's default heroes per Unity game.
HISTORY_LIMIT = 400
STATUS_INTERVAL = 2.0
EXPORT_INTERVAL = 5.0
STOP_TIMEOUT = 180.0
EXTEND_FRACTION = 0.98
QUICK_EXIT_SECONDS = 60.0
# ML-Agents crashes are retried from the last checkpoint; a run this long resets the count.
CRASH_RETRY_LIMIT = 5
CPU_FALLBACK_AFTER = 2
STABLE_RUN_SECONDS = 600.0
RETRY_DELAY = 5.0
BEHAVIORS = {name.lower(): name for name in arena_trainer.HERO_BEHAVIORS}

SUMMARY_PATTERN = re.compile(
    r"\[INFO\] (?P<behavior>[^.\s]+)\. Step: (?P<step>\d+)\. Time Elapsed: [\d.]+ s\."
    r"(?: Mean Reward: (?P<reward>-?\d+(?:\.\d+)?)\.)?"
)
LESSON_PATTERN = re.compile(
    r"Parameter '(?P<name>\w+)' is in lesson '(?P<lesson>[^']+)' and has value '[^']*?(?P<value>-?\d+(?:\.\d+)?)'"
)


def normalize_behavior(value: str) -> str:
    return arena_trainer.behavior_argument(value)


def default_config(behavior: str) -> str:
    return str(arena_trainer.CONFIG_DIRECTORY / f"{behavior.lower()}_survivor_ppo.yaml")


def missing_config_message(behavior: str, config_path: Path) -> str:
    """What the viewer shows when the trainer config is missing."""
    return f"Trainer config not found: {config_path.name}."


def config_problem(behavior: str, config_path: Path) -> str:
    """Why ``config_path`` cannot train ``behavior`` ("" when it can)."""
    if not config_path.is_file():
        return missing_config_message(behavior, config_path)
    trained = arena_trainer.config_behaviors(config_path)
    if behavior not in trained:
        return f"Trainer config {config_path.name} has no {behavior} behavior."
    return ""


@dataclass
class Progress:
    """Training progress parsed from ML-Agents console output."""

    behavior: str
    step: int = 0
    mean_reward: float | None = None
    zombies: float | None = None
    runner_weight: float = 0.0
    brute_weight: float = 0.0
    spitter_weight: float = 0.0
    steps: list[int] = field(default_factory=list)
    rewards: list[float] = field(default_factory=list)
    summaries: int = 0

    def feed(self, line: str) -> None:
        summary = SUMMARY_PATTERN.search(line)
        if summary and summary["behavior"] == self.behavior:
            step = int(summary["step"])
            # A resumed run restarts from its last checkpoint: drop points past it.
            while self.steps and self.steps[-1] >= step:
                self.steps.pop()
                self.rewards.pop()
            self.step = step
            self.summaries += 1
            if summary["reward"] is not None:
                self.mean_reward = float(summary["reward"])
                self.steps.append(step)
                self.rewards.append(self.mean_reward)
                if len(self.steps) > HISTORY_LIMIT:
                    start = (len(self.steps) - 1) % 2
                    self.steps = self.steps[start::2]
                    self.rewards = self.rewards[start::2]
            return

        lesson = LESSON_PATTERN.search(line)
        if lesson:
            value = float(lesson["value"])
            if lesson["name"] == "zombie_count":
                self.zombies = value
            elif lesson["name"] in ("runner_weight", "brute_weight", "spitter_weight"):
                setattr(self, lesson["name"], value)


class LogTail:
    """Reads lines appended to a growing text file."""

    def __init__(self, path: Path):
        self.path = path
        self.offset = 0
        self.partial = ""

    def read_lines(self) -> list[str]:
        try:
            with self.path.open("rb") as handle:
                handle.seek(0, os.SEEK_END)
                size = handle.tell()
                if size < self.offset:
                    self.offset = 0
                    self.partial = ""
                handle.seek(self.offset)
                data = handle.read()
                self.offset = handle.tell()
        except FileNotFoundError:
            return []
        text = self.partial + data.decode("utf-8", errors="replace")
        lines = text.split("\n")
        self.partial = lines.pop()
        return [line.rstrip("\r") for line in lines]


@dataclass
class RunPlan:
    run_id: str
    mode: str  # "resume", "force" or "new"
    last_step: int
    message: str = ""


def foreign_run(run_dir: Path, behavior: str) -> bool:
    """True when ``run_dir`` belongs to another hero class: its name says so (``warrior-s001`` for
    a Mage) or it holds another class's brain. Training it with ``--force`` would wreck that run."""
    owner = arena_trainer.run_class(run_dir.name)
    if owner is not None and owner != behavior:
        return True
    return any(
        (run_dir / other).is_dir() for other in arena_trainer.HERO_BEHAVIORS if other != behavior
    )


def class_checkpoint_dirs(runs_dir: Path, behavior: str) -> list[Path]:
    """``<run>/<Behavior>`` folders with a resumable checkpoint, only from this class's runs
    (``mage-...`` for the Mage): a Mage never resumes or upgrades a Warrior run, and vice versa."""
    return [
        path
        for path in runs_dir.glob(f"*/{behavior}")
        if path.is_dir()
        and path.parent.name != champion.CHAMPIONS_DIR
        and not path.parent.name.startswith(".")
        and arena_trainer.run_belongs_to(path.parent.name, behavior)
        and (path / "checkpoint.pt").is_file()
        and export_brain.checkpoints(path)
    ]


def plan_run(runs_dir: Path, behavior: str, requested: str | None = None) -> RunPlan:
    message = ""
    if requested and requested != champion.CHAMPIONS_DIR:
        try:
            brain_lineage.check_id(requested, "nhánh")
        except brain_lineage.LineageError:
            # Never let a run name leave the runs folder: train this class's own newest run instead.
            message = f"Tên nhánh {requested} không hợp lệ; học tiếp não {behavior} mới nhất."
            requested = None
    if (
        requested
        and requested != champion.CHAMPIONS_DIR
        and foreign_run(runs_dir / requested, behavior)
    ):
        # The viewer asked for another class's branch: train this class's own newest run instead.
        message = f"Nhánh {requested} không phải não {behavior}; học tiếp não {behavior} mới nhất."
        requested = None
    if (
        requested
        and requested != champion.CHAMPIONS_DIR
        and (runs_dir / requested / arena_trainer.SCHEMA_FILE).is_file()
        and any(
            entry.name != arena_trainer.SCHEMA_FILE for entry in (runs_dir / requested).iterdir()
        )
        and arena_trainer.run_schema_version(runs_dir / requested) < arena_trainer.SCHEMA_VERSION
    ):
        # The viewer still names the run from before the schema change (the profile keeps its run id).
        # Plan as if nothing was requested: resume the class's current-schema run, or upgrade the older
        # brain into the next run. Never start from zero while an upgradable brain exists.
        requested = None
    if requested:
        run_id = next_run_id(runs_dir, behavior) if requested == champion.CHAMPIONS_DIR else requested
        requested_dir = runs_dir / run_id
        # An existing run of an older schema (pre-Survivor mage-001, even without a checkpoint) is never
        # trained with --force: that would overwrite it. Start this class's next run instead.
        if (
            requested_dir.is_dir()
            and any(entry.name != arena_trainer.SCHEMA_FILE for entry in requested_dir.iterdir())
            and arena_trainer.run_schema_version(requested_dir) != arena_trainer.SCHEMA_VERSION
        ):
            run_id = next_run_id(runs_dir, behavior)
    else:
        candidates = class_checkpoint_dirs(runs_dir, behavior)
        resumable = [
            path
            for path in candidates
            if arena_trainer.run_schema_version(path.parent) == arena_trainer.SCHEMA_VERSION
        ]
        newest = (
            max(
                resumable,
                key=lambda path: max(
                    checkpoint.stat().st_mtime
                    for _, checkpoint in export_brain.checkpoints(path)
                ),
            )
            if resumable
            else None
        )
        if newest is not None:
            run_id = newest.parent.name
        else:
            run_id = next_run_id(runs_dir, behavior)
            # Runs without a schema marker are pre-Survivor arena brains (mage-001, warrior-003):
            # they can never be upgraded, so a class's first Survivor run starts quietly from scratch.
            older = [
                path
                for path in candidates
                if (path.parent / arena_trainer.SCHEMA_FILE).is_file()
                and arena_trainer.run_schema_version(path.parent) < arena_trainer.SCHEMA_VERSION
            ]
            older.sort(
                key=lambda path: max(
                    checkpoint.stat().st_mtime
                    for _, checkpoint in export_brain.checkpoints(path)
                ),
                reverse=True,
            )
            current_schema = arena_trainer.schema_path(arena_trainer.SCHEMA_VERSION)
            upgrade_source = next(
                (
                    path
                    for path in older
                    if current_schema.is_file()
                    and arena_trainer.schema_path(
                        arena_trainer.run_schema_version(path.parent)
                    ).is_file()
                ),
                None,
            )
            if upgrade_source is not None:
                old_version = arena_trainer.run_schema_version(upgrade_source.parent)
                source_step = export_brain.checkpoints(upgrade_source)[-1][0]
                try:
                    brain_upgrade.upgrade_run(
                        runs_dir,
                        upgrade_source.parent.name,
                        behavior,
                        run_id,
                        old_version,
                        arena_trainer.SCHEMA_VERSION,
                    )
                except Exception as error:  # upgrade_run wraps its own errors; stay safe anyway
                    message = f"Không thể nâng cấp não AI ({error}); bắt đầu học lại từ đầu."
                else:
                    message = (
                        "Não AI được nâng cấp lên luật mới "
                        f"(schema v{old_version} → v{arena_trainer.SCHEMA_VERSION}) "
                        f"và học tiếp từ bước {source_step}."
                    )
            elif older:
                old_version = arena_trainer.run_schema_version(older[0].parent)
                missing = []
                if not arena_trainer.schema_path(old_version).is_file():
                    missing.append(f"schema v{old_version}")
                if not current_schema.is_file():
                    missing.append(f"schema v{arena_trainer.SCHEMA_VERSION}")
                message = (
                    "Không thể nâng cấp não AI vì thiếu " + " và ".join(missing)
                    + "; bắt đầu học lại từ đầu."
                )

    run_dir = runs_dir / run_id
    behavior_dir = run_dir / behavior
    found = export_brain.checkpoints(behavior_dir) if behavior_dir.is_dir() else []
    last_step = found[-1][0] if found else 0
    if (
        (behavior_dir / "checkpoint.pt").is_file()
        and arena_trainer.run_schema_version(run_dir) == arena_trainer.SCHEMA_VERSION
    ):
        mode = "resume"
    elif run_dir.exists():
        mode = "force"
    else:
        mode = "new"
    return RunPlan(run_id, mode, last_step, message)


def install_staged_build(root: Path) -> str:
    """Swap ``Build/TrainingNext`` (built while training was running) into ``Build/Training``.

    The training player is locked while ML-Agents runs, so a new build is staged next to it
    and installed here, before the trainer starts. Returns a log message ("" when nothing was
    staged). On any error the current build stays in place.
    """
    build_dir = root / "Build"
    staged = build_dir / "TrainingNext"
    if not (staged / arena_trainer.DEFAULT_ENVIRONMENT.name).is_file():
        return ""
    target = build_dir / "Training"
    previous = build_dir / ".Training.old"
    try:
        if previous.exists():
            shutil.rmtree(previous)
        if target.exists():
            target.rename(previous)
        try:
            staged.rename(target)
        except OSError:
            if previous.exists() and not target.exists():
                previous.rename(target)
            raise
    except OSError as error:
        return f"could not install the staged training build ({error}); using the current one"
    shutil.rmtree(previous, ignore_errors=True)
    return "installed the new training build from Build/TrainingNext"


def next_run_id(runs_dir: Path, behavior: str) -> str:
    prefix = behavior.lower()
    pattern = re.compile(rf"^{re.escape(prefix)}-s(\d{{3}})$")
    numbers = []
    if runs_dir.is_dir():
        for path in runs_dir.iterdir():
            match = pattern.match(path.name)
            if path.is_dir() and path.name != champion.CHAMPIONS_DIR and match:
                numbers.append(int(match.group(1)))
    return f"{prefix}-s{max(numbers, default=0) + 1:03d}"


def configured_max_steps(config: dict, behavior: str) -> int:
    return int(float(config["behaviors"][behavior].get("max_steps", 500000)))


def environment_overrides(args: argparse.Namespace) -> dict[str, float]:
    """Constant environment parameters the owner's choices set (M5): train on their build."""
    if getattr(args, "owner_build", None):
        return {"own_build_share": arena_trainer.OWNER_BUILD_SHARE}
    return {}


def effective_config(config_path: Path, behavior: str, last_step: int, output: Path,
                     overrides: dict[str, float] | None = None) -> tuple[Path, int]:
    """Return the config to train with and its max_steps, extending the budget when it is used up
    and applying the owner's constant environment parameters."""
    import yaml  # PyYAML ships with mlagents.

    config = yaml.safe_load(config_path.read_text(encoding="utf-8"))
    max_steps = configured_max_steps(config, behavior)
    changed = False
    if last_step >= max_steps * EXTEND_FRACTION:
        max_steps = last_step + max_steps
        config["behaviors"][behavior]["max_steps"] = max_steps
        changed = True
    for name, value in (overrides or {}).items():
        parameters = config.setdefault("environment_parameters", {}) or {}
        config["environment_parameters"] = parameters
        if isinstance(parameters.get(name), dict):
            # A curriculum here would be replaced by a constant, and --resume would then restore a
            # lesson number that no longer exists.
            raise ValueError(f"environment parameter {name!r} is a curriculum; it cannot be overridden")
        if parameters.get(name) != value:
            parameters[name] = value
            changed = True
    if not changed:
        return config_path, max_steps
    config["behaviors"][behavior]["max_steps"] = max_steps  # "3.0e7" in YAML is a string.
    output.write_text(yaml.safe_dump(config, sort_keys=False), encoding="utf-8")
    return output, max_steps


def process_alive(pid: int) -> bool:
    if pid <= 0:
        return False
    if sys.platform != "win32":
        try:
            os.kill(pid, 0)
        except ProcessLookupError:
            return False
        except PermissionError:
            return True
        return True

    import ctypes

    kernel32 = ctypes.windll.kernel32
    synchronize = 0x00100000
    handle = kernel32.OpenProcess(synchronize, False, pid)
    if not handle:
        return kernel32.GetLastError() == 5  # Access denied: it exists.
    try:
        return kernel32.WaitForSingleObject(handle, 0) == 0x102  # WAIT_TIMEOUT: still running.
    finally:
        kernel32.CloseHandle(handle)


def send_ctrl_c(process: subprocess.Popen) -> None:
    """Interrupt ML-Agents like a terminal Ctrl+C so it saves a checkpoint."""
    if sys.platform == "win32":
        import ctypes

        # CTRL_C_EVENT to every process on this (hidden) console; the service ignores SIGINT.
        ctypes.windll.kernel32.GenerateConsoleCtrlEvent(0, 0)
    else:
        process.send_signal(signal.SIGINT)


def kill_tree(process: subprocess.Popen) -> None:
    if sys.platform == "win32":
        subprocess.run(["taskkill", "/T", "/F", "/PID", str(process.pid)], capture_output=True, check=False)
    else:
        process.kill()


def acquire_lock(path: Path, wait_seconds: float = 0.0):
    """Returns the open lock file, or None if another service still holds it after the wait."""
    deadline = time.monotonic() + wait_seconds
    while True:
        handle = try_lock(path)
        if handle is not None or time.monotonic() >= deadline:
            return handle
        time.sleep(0.25)


def try_lock(path: Path):
    handle = path.open("a+")
    try:
        if sys.platform == "win32":
            import msvcrt

            handle.seek(0)
            msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
        else:
            import fcntl

            fcntl.flock(handle.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
    except OSError:
        handle.close()
        return None
    return handle


def write_json_atomically(path: Path, payload: dict) -> None:
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_text(json.dumps(payload), encoding="utf-8")
    for attempt in range(20):
        try:
            os.replace(temporary, path)
            return
        except PermissionError:  # The viewer may be reading the old file.
            time.sleep(0.05 * (attempt + 1))


class TrainingService:
    def __init__(self, args: argparse.Namespace, clock: Callable[[], float] = time.time):
        self.args = args
        self.clock = clock
        self.root = arena_trainer.repository_root()
        self.runs_dir = arena_trainer.resolve_path(args.results_dir, self.root)
        self.status_path = self.runs_dir / STATUS_NAME
        self.stop_path = self.runs_dir / STOP_NAME
        self.behavior = args.behavior
        self.progress = Progress(args.behavior)
        self.started_at = clock()
        self.run_id = ""
        self.trainer_pid = 0
        self.max_steps = 0
        self.torch_device: str | None = getattr(args, "torch_device", None)
        self.log_file: TextIO | None = None
        self.exported: dict[Path, int] = {}
        self.evaluation_thread: threading.Thread | None = None
        self.evaluation_runner: champion.EvaluationRunner | None = None
        self.evaluation_attempt_step = 0
        self.last_eval_step = 0
        self.last_eval_won: bool | None = None
        self.evaluation_message = ""
        self._evaluation_lock = threading.Lock()
        self._status_lock = threading.Lock()

    # -- reporting -------------------------------------------------------------------------

    def log(self, message: str) -> None:
        if self.log_file is not None:
            self.log_file.write(f"[service] {time.strftime('%H:%M:%S')} {message}\n")
            self.log_file.flush()

    def status(self, state: str, message: str = "") -> dict:
        current = champion.load_champion(self.runs_dir, self.behavior)
        summary = current.get("summary", {}) if current else {}
        with self._evaluation_lock:
            evaluating = self.evaluation_thread is not None and self.evaluation_thread.is_alive()
            last_eval_step = self.last_eval_step
            last_eval_won = self.last_eval_won
            evaluation_message = self.evaluation_message
        return {
            "state": state,
            "message": message,
            "run_id": self.run_id,
            "behavior": self.behavior,
            "pid": os.getpid(),
            "trainer_pid": self.trainer_pid,
            "step": self.progress.step,
            "max_steps": self.max_steps,
            "has_reward": self.progress.mean_reward is not None,
            "mean_reward": self.progress.mean_reward if self.progress.mean_reward is not None else 0.0,
            "zombies": self.progress.zombies if self.progress.zombies is not None else 0.0,
            "zombie_mix": {
                "walker": 1.0,
                "runner": self.progress.runner_weight,
                "brute": self.progress.brute_weight,
                "spitter": self.progress.spitter_weight,
            },
            "session_seconds": round(self.clock() - self.started_at, 1),
            "num_envs": getattr(self.args, "num_envs", 0),
            "arena_agents": getattr(self.args, "arena_agents", None) or DEFAULT_ARENA_AGENTS,
            "cpu": self.torch_device == "cpu",
            "updated_unix": self.clock(),
            "steps": self.progress.steps,
            "rewards": self.progress.rewards,
            "champion_step": current.get("step") if current else None,
            "champion_run": current.get("run_id") if current else None,
            "champion_median": summary.get("MedianSurvivedSeconds") if current else None,
            "champion_p10": summary.get("P10SurvivedSeconds") if current else None,
            "champion_passes_m4a": current.get("passes_m4a") if current else None,
            "last_eval_step": last_eval_step or None,
            "last_eval_won": last_eval_won,
            "evaluating": evaluating,
            "evaluation_message": evaluation_message,
            "training_focus": getattr(self.args, "training_focus", None) or "balanced",
            "owner_build": bool(getattr(self.args, "owner_build", None)),
            # The tier only reaches Unity together with a build (0 = no owner build).
            "owner_tier": (getattr(self.args, "owner_tier", None) or 1)
            if getattr(self.args, "owner_build", None) else 0,
        }

    def publish(self, state: str, message: str = "") -> None:
        with self._status_lock:
            write_json_atomically(self.status_path, self.status(state, message))

    def export(self) -> None:
        try:
            export_brain.export_newest(self.runs_dir, self.behavior, self.exported, log=self.log)
        except Exception as error:  # Exporting must never take training down.
            self.log(f"brain export failed: {error}")

    def write_history(self) -> None:
        try:
            from Trainer import training_history

            training_history.write_history(self.runs_dir / self.run_id, self.behavior)
        except Exception as error:  # History must never hide the training result.
            self.log(f"training history export failed: {error}")

    def initialize_evaluation_state(self) -> None:
        self.last_eval_step = champion.last_evaluated_step(
            self.runs_dir, self.behavior, self.run_id
        )
        latest = champion.latest_evaluation(self.runs_dir, self.behavior)
        if latest is not None and latest.get("run_id") == self.run_id:
            self.last_eval_won = bool(latest.get("won", False))
        self.evaluation_attempt_step = self.last_eval_step

    def maybe_start_evaluation(self) -> bool:
        with self._evaluation_lock:
            if self.evaluation_thread is not None and self.evaluation_thread.is_alive():
                return False
            behavior_dir = self.runs_dir / self.run_id / self.behavior
            found = export_brain.checkpoints(behavior_dir) if behavior_dir.is_dir() else []
            if not found:
                return False
            step = found[-1][0]
            threshold = self.last_eval_step + champion.EVALUATION_INTERVAL
            if step < threshold or step <= self.evaluation_attempt_step:
                return False
            self.evaluation_attempt_step = step
            runner = champion.EvaluationRunner()
            thread = threading.Thread(
                target=self._evaluate_checkpoint,
                args=(step, runner),
                name=f"champion-eval-{self.behavior}",
                daemon=True,
            )
            self.evaluation_runner = runner
            self.evaluation_thread = thread
            self.evaluation_message = ""
            thread.start()
            return True

    def _evaluate_checkpoint(self, step: int, runner: champion.EvaluationRunner) -> None:
        try:
            result = champion.evaluate_latest(
                self.runs_dir, self.behavior, self.run_id, runner=runner, log=self.log
            )
            with self._evaluation_lock:
                if not result.get("cancelled") and not result.get("skipped"):
                    self.last_eval_step = int(result.get("step", step))
                    self.last_eval_won = bool(result.get("won", False))
                if result.get("skipped"):
                    self.evaluation_message = str(result.get("message", "Evaluation skipped."))
        except Exception as error:
            message = f"Champion evaluation failed: {error}"
            self.log(message)
            with self._evaluation_lock:
                self.evaluation_message = message
        finally:
            with self._evaluation_lock:
                if self.evaluation_runner is runner:
                    self.evaluation_runner = None

    def cancel_evaluation(self) -> None:
        with self._evaluation_lock:
            runner = self.evaluation_runner
            thread = self.evaluation_thread
        if runner is not None:
            runner.cancel()
        if thread is not None and thread.is_alive():
            thread.join(timeout=10.0)

    # -- control -------------------------------------------------------------------------

    def stop_requested(self) -> bool:
        if self.stop_path.exists():
            return True
        return bool(self.args.parent_pid) and not process_alive(self.args.parent_pid)

    def trainer_command(self, plan: RunPlan, config: Path) -> list[str]:
        arguments = [
            "--run-id",
            plan.run_id,
            "--config",
            str(config),
            "--base-port",
            str(self.args.base_port),
            "--num-envs",
            str(self.args.num_envs),
            "--results-dir",
            str(self.runs_dir),
        ]
        if plan.mode == "resume":
            arguments.append("--resume")
        else:
            # A new run's folder already exists (the schema marker is written first), so
            # ML-Agents would refuse it without --force.
            arguments.append("--force")
        if self.torch_device:
            arguments.extend(("--torch-device", self.torch_device))
        arguments.extend(("--time-scale", str(self.args.time_scale)))
        if self.args.arena_agents:
            arguments.extend(("--arena-agents", str(self.args.arena_agents)))
        arguments.extend(("--hero-class", self.behavior.lower()))
        if getattr(self.args, "owner_build", None):
            arguments.extend(("--owner-build", self.args.owner_build))
            arguments.extend(("--owner-tier", str(getattr(self.args, "owner_tier", None) or 1)))
        if getattr(self.args, "training_focus", None):
            arguments.extend(("--training-focus", self.args.training_focus))
        parsed = arena_trainer.create_parser().parse_args(arguments)
        return arena_trainer.build_command(parsed, self.root)

    def wait_unless_stopped(self, seconds: float, message: str) -> bool:
        """Wait before a retry; True when the owner pressed Stop meanwhile."""
        deadline = self.clock() + seconds
        while self.clock() < deadline:
            if self.stop_requested():
                return True
            self.publish("starting", message)
            time.sleep(0.5)
        return self.stop_requested()

    def fail(self, details: str) -> None:
        """Record an unexpected service error where the owner (and the viewer) can find it."""
        last_line = details.strip().splitlines()[-1] if details.strip() else "unknown error"
        try:
            (self.runs_dir / ERROR_LOG_NAME).write_text(details, encoding="utf-8")
        except OSError:
            pass
        try:
            self.log("service error:\n" + details.rstrip())
        except (OSError, ValueError):
            pass
        try:
            self.publish("error", f"The training service hit an error ({last_line}). "
                                  f"See Trainer/runs/{ERROR_LOG_NAME}.")
        except OSError:
            pass

    def run(self) -> int:
        self.runs_dir.mkdir(parents=True, exist_ok=True)
        # A service that just saved and stopped (e.g. for a power change) may still be exiting.
        lock = acquire_lock(self.runs_dir / LOCK_NAME, LOCK_WAIT)
        if lock is None:
            print("Another training service is already running.", file=sys.stderr)
            return 3
        try:
            return self._run_locked()
        except Exception:
            self.fail(traceback.format_exc())
            return 1
        finally:
            self.cancel_evaluation()
            if self.run_id:
                self.write_history()
            if self.log_file is not None:
                self.log_file.close()
            lock.close()

    def _run_locked(self) -> int:
        self.stop_path.unlink(missing_ok=True)
        # Check the config before planning, so an untrainable behavior never creates a run folder.
        config_path = arena_trainer.resolve_path(self.args.config, self.root)
        message = config_problem(self.behavior, config_path)
        if message:
            print(message, file=sys.stderr)
            self.publish("error", message)
            return 2

        plan = plan_run(self.runs_dir, self.behavior, self.args.run_id)
        self.run_id = plan.run_id
        self.initialize_evaluation_state()
        if plan.mode in ("new", "force"):
            arena_trainer.write_schema_version(self.runs_dir / plan.run_id)
        log_path = self.runs_dir / f"{plan.run_id}.log"
        tail = LogTail(log_path)
        for line in tail.read_lines():
            self.progress.feed(line)
        if tail.partial:
            self.progress.feed(tail.partial)
        self.log_file = log_path.open("a", encoding="utf-8")
        build_message = install_staged_build(self.root)
        if build_message:
            self.log(build_message)
        starting_message = plan.message or "Loading the training arenas..."
        if plan.message:
            self.log(plan.message)
        if getattr(self.args, "owner_build", None) or getattr(self.args, "training_focus", None):
            self.log(f"owner settings: build {getattr(self.args, 'owner_build', None) or 'random'}, "
                     f"tier {getattr(self.args, 'owner_tier', None) or 1}, "
                     f"focus {getattr(self.args, 'training_focus', None) or 'balanced'}")
        try:
            from Trainer import brain_lineage  # late import, like champion's evaluator hook

            imported = brain_lineage.sync(self.runs_dir, self.behavior)
            if imported:
                self.log(f"lineage: imported {imported} champion history versions")
        except Exception as error:  # noqa: BLE001 - lineage must never stop training
            self.log(f"lineage sync failed: {error}")
        self.publish("starting", starting_message)

        restarts = 0
        crashes = 0
        while True:
            config_path = arena_trainer.resolve_path(self.args.config, self.root)
            config, self.max_steps = effective_config(
                config_path, self.behavior, plan.last_step, self.runs_dir / f"{plan.run_id}.service.yaml",
                environment_overrides(self.args),
            )
            command = self.trainer_command(plan, config)
            missing = [path for path in (command[0], command[command.index("--env") + 1]) if not Path(path).is_file()]
            if missing:
                message = "Missing " + ", ".join(Path(path).name for path in missing) + " (build it first)."
                self.log(message)
                self.publish("error", message)
                return 2

            self.log(f"starting {plan.run_id} ({plan.mode}, max_steps {self.max_steps:,}): "
                     + arena_trainer.format_command(command))
            code, stopped, seconds = self._train(command, tail)
            self.export()
            plan = plan_run(self.runs_dir, self.behavior, plan.run_id)
            if stopped:
                return self._stopped(plan, f"(exit {code})")

            finished = code == 0 and plan.last_step >= self.max_steps * EXTEND_FRACTION
            if finished:
                restarts = restarts + 1 if seconds < QUICK_EXIT_SECONDS else 0
                if restarts > 2:
                    message = f"Training keeps finishing right away. See Trainer/runs/{plan.run_id}.log."
                    self.log(message)
                    self.publish("error", message)
                    return 1
                self.log(f"reached max_steps at {plan.last_step:,}; continuing with a larger budget")
                self.publish("starting", "Reached the step budget; continuing...")
                continue

            # ML-Agents crashed or quit early: resume from the last checkpoint.
            crashes = crashes + 1 if seconds < STABLE_RUN_SECONDS else 1
            exit_text = f"exit code {code} / 0x{code & 0xFFFFFFFF:08X}"
            if crashes > CRASH_RETRY_LIMIT:
                message = (f"Training keeps crashing ({exit_text}) after {CRASH_RETRY_LIMIT} restarts. "
                           f"See Trainer/runs/{plan.run_id}.log.")
                self.log(message)
                self.publish("error", message)
                return 1
            if crashes >= CPU_FALLBACK_AFTER and self.torch_device != "cpu":
                self.torch_device = "cpu"
                self.log("repeated crashes: training on the CPU from now on")
            self.log(f"ML-Agents stopped unexpectedly after {seconds:.0f} s ({exit_text}); "
                     f"restarting from the last checkpoint (attempt {crashes} of {CRASH_RETRY_LIMIT})")
            message = f"The trainer crashed; restarting it (attempt {crashes} of {CRASH_RETRY_LIMIT})..."
            if self.wait_unless_stopped(RETRY_DELAY, message):
                return self._stopped(plan, "while restarting")

    def _stopped(self, plan: RunPlan, detail: str) -> int:
        self.log(f"stopped by the viewer at step {self.progress.step:,} {detail}")
        self.publish("stopped", f"Stopped. Progress saved at step {plan.last_step:,}.")
        self.stop_path.unlink(missing_ok=True)
        return 0

    def _train(self, command: list[str], tail: LogTail) -> tuple[int, bool, float]:
        environment = dict(os.environ, PYTHONUNBUFFERED="1")
        launched = self.clock()
        live_summaries = self.progress.summaries
        process = subprocess.Popen(
            command, cwd=self.root, stdout=self.log_file, stderr=subprocess.STDOUT, env=environment
        )
        self.trainer_pid = process.pid
        try:
            return self._watch(process, tail, launched, live_summaries)
        except BaseException:
            # Never leave ML-Agents training with nobody able to stop it.
            if process.poll() is None:
                send_ctrl_c(process)
                try:
                    process.wait(STOP_TIMEOUT)
                except subprocess.TimeoutExpired:
                    kill_tree(process)
            raise

    def _watch(self, process: subprocess.Popen, tail: LogTail, launched: float,
               live_summaries: int) -> tuple[int, bool, float]:
        stopping_since: float | None = None
        next_status = 0.0
        next_export = self.clock() + EXPORT_INTERVAL
        while True:
            code = process.poll()
            for line in tail.read_lines():
                self.progress.feed(line)

            now = self.clock()
            if stopping_since is None and self.stop_requested():
                stopping_since = now
                self.log("stop requested; asking ML-Agents to save and quit")
                self.cancel_evaluation()
                send_ctrl_c(process)
                next_status = 0.0
            if stopping_since is not None and code is None and now - stopping_since > STOP_TIMEOUT:
                self.log("ML-Agents did not quit in time; killing it")
                kill_tree(process)

            if code is not None:
                self.trainer_pid = 0
                return code, stopping_since is not None, now - launched

            if now >= next_export and stopping_since is None:
                self.export()
                self.write_history()
                self.maybe_start_evaluation()
                next_export = now + EXPORT_INTERVAL
            if now >= next_status:
                if stopping_since is not None:
                    self.publish("stopping", "Saving the AI's progress...")
                elif self.progress.summaries > live_summaries:
                    self.publish("training", "The AI is training.")
                else:
                    self.publish("starting", "Loading the training arenas...")
                next_status = now + STATUS_INTERVAL
            time.sleep(1.0)


class TrainingArgumentParser(argparse.ArgumentParser):
    def parse_args(self, args=None, namespace=None):
        parsed = super().parse_args(args, namespace)
        if parsed.config is None:
            parsed.config = default_config(parsed.behavior)
        return parsed


def create_parser() -> argparse.ArgumentParser:
    parser = TrainingArgumentParser(description="Run training for the viewer's Train the AI button.")
    parser.add_argument("--run-id", help="Run to train (default: resume the newest run).")
    parser.add_argument("--behavior", type=normalize_behavior, default="Warrior")
    parser.add_argument("--config")
    parser.add_argument("--results-dir", default=str(arena_trainer.DEFAULT_RESULTS_DIRECTORY))
    parser.add_argument("--num-envs", type=int, default=4, help="Unity games running side by side.")
    parser.add_argument("--arena-agents", type=int, help="Heroes per game (default: the build's 16).")
    parser.add_argument("--time-scale", type=float, default=20.0)
    parser.add_argument("--base-port", type=int, default=5005)
    parser.add_argument("--parent-pid", type=int, default=0, help="Stop when this process exits.")
    parser.add_argument("--torch-device", choices=("cpu", "cuda"), help="Force the PyTorch device.")
    arena_trainer.add_owner_arguments(parser)
    return parser


def main(argv: Iterable[str] | None = None) -> int:
    args = create_parser().parse_args(list(argv) if argv is not None else None)
    # Ctrl+C is meant for ML-Agents; the service keeps running to report the result.
    signal.signal(signal.SIGINT, signal.SIG_IGN)
    service = TrainingService(args)
    try:
        return service.run()
    except Exception:  # The viewer hides the console, so leave a trace it can show.
        service.fail(traceback.format_exc())
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
