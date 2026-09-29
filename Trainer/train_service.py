"""Background training service behind the viewer's "Train the AI" button.

The viewer (``Build/Watch/PersonalArenaWatch.exe``) starts this script in a hidden console::

    .venv-ml/Scripts/python.exe Trainer/train_service.py --parent-pid <viewer pid>

The service resumes the newest run (or starts ``warrior-001``), appends ML-Agents output to
``Trainer/runs/<run>.log``, keeps exporting ``latest.brain`` so the viewer hot-loads every new
checkpoint, and reports progress in ``Trainer/runs/training_service.json``.

It stops gracefully when ``Trainer/runs/training_service.stop`` appears or the viewer exits:
it sends the same Ctrl+C a terminal would, so ML-Agents saves a checkpoint before quitting.
When a run reaches ``max_steps`` it is resumed with a larger budget, so training continues
until the owner presses Stop.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass, field
import json
import os
from pathlib import Path
import re
import signal
import subprocess
import sys
import time
from typing import Callable, Iterable, TextIO

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from Trainer import arena_trainer, export_brain  # noqa: E402

STATUS_NAME = "training_service.json"
STOP_NAME = "training_service.stop"
LOCK_NAME = "training_service.lock"
HISTORY_LIMIT = 400
STATUS_INTERVAL = 2.0
EXPORT_INTERVAL = 5.0
STOP_TIMEOUT = 180.0
EXTEND_FRACTION = 0.98
QUICK_EXIT_SECONDS = 60.0

SUMMARY_PATTERN = re.compile(
    r"\[INFO\] (?P<behavior>[^.\s]+)\. Step: (?P<step>\d+)\. Time Elapsed: [\d.]+ s\."
    r"(?: Mean Reward: (?P<reward>-?\d+(?:\.\d+)?)\.)?"
)
LESSON_PATTERN = re.compile(
    r"Parameter '(?P<name>\w+)' is in lesson '(?P<lesson>[^']+)' and has value '[^']*?(?P<value>-?\d+(?:\.\d+)?)'"
)


@dataclass
class Progress:
    """Training progress parsed from ML-Agents console output."""

    behavior: str
    step: int = 0
    mean_reward: float | None = None
    zombies: float | None = None
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
        if lesson and lesson["name"] == "zombie_count":
            self.zombies = float(lesson["value"])


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


def plan_run(runs_dir: Path, behavior: str, requested: str | None = None) -> RunPlan:
    if requested:
        run_id = requested
    else:
        newest = export_brain.newest_behavior_dir(runs_dir, behavior)
        run_id = newest.parent.name if newest is not None else f"{behavior.lower()}-001"

    run_dir = runs_dir / run_id
    behavior_dir = run_dir / behavior
    found = export_brain.checkpoints(behavior_dir) if behavior_dir.is_dir() else []
    last_step = found[-1][0] if found else 0
    if (behavior_dir / "checkpoint.pt").is_file():
        mode = "resume"
    elif run_dir.exists():
        mode = "force"
    else:
        mode = "new"
    return RunPlan(run_id, mode, last_step)


def configured_max_steps(config: dict, behavior: str) -> int:
    return int(float(config["behaviors"][behavior].get("max_steps", 500000)))


def effective_config(config_path: Path, behavior: str, last_step: int, output: Path) -> tuple[Path, int]:
    """Return the config to train with and its max_steps, extending the budget when it is used up."""
    import yaml  # PyYAML ships with mlagents.

    config = yaml.safe_load(config_path.read_text(encoding="utf-8"))
    max_steps = configured_max_steps(config, behavior)
    if last_step < max_steps * EXTEND_FRACTION:
        return config_path, max_steps
    extended = last_step + max_steps
    config["behaviors"][behavior]["max_steps"] = extended
    output.write_text(yaml.safe_dump(config, sort_keys=False), encoding="utf-8")
    return output, extended


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


def acquire_lock(path: Path):
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
        self.log_file: TextIO | None = None
        self.exported: dict[Path, int] = {}

    # -- reporting -------------------------------------------------------------------------

    def log(self, message: str) -> None:
        if self.log_file is not None:
            self.log_file.write(f"[service] {time.strftime('%H:%M:%S')} {message}\n")
            self.log_file.flush()

    def status(self, state: str, message: str = "") -> dict:
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
            "session_seconds": round(self.clock() - self.started_at, 1),
            "updated_unix": self.clock(),
            "steps": self.progress.steps,
            "rewards": self.progress.rewards,
        }

    def publish(self, state: str, message: str = "") -> None:
        write_json_atomically(self.status_path, self.status(state, message))

    def export(self) -> None:
        try:
            export_brain.export_newest(self.runs_dir, self.behavior, self.exported, log=self.log)
        except Exception as error:  # Exporting must never take training down.
            self.log(f"brain export failed: {error}")

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
        elif plan.mode == "force":
            arguments.append("--force")
        parsed = arena_trainer.create_parser().parse_args(arguments)
        return arena_trainer.build_command(parsed, self.root)

    def run(self) -> int:
        self.runs_dir.mkdir(parents=True, exist_ok=True)
        lock = acquire_lock(self.runs_dir / LOCK_NAME)
        if lock is None:
            print("Another training service is already running.", file=sys.stderr)
            return 3
        try:
            return self._run_locked()
        finally:
            if self.log_file is not None:
                self.log_file.close()
            lock.close()

    def _run_locked(self) -> int:
        self.stop_path.unlink(missing_ok=True)
        plan = plan_run(self.runs_dir, self.behavior, self.args.run_id)
        self.run_id = plan.run_id
        log_path = self.runs_dir / f"{plan.run_id}.log"
        tail = LogTail(log_path)
        for line in tail.read_lines():
            self.progress.feed(line)
        if tail.partial:
            self.progress.feed(tail.partial)
        self.log_file = log_path.open("a", encoding="utf-8")
        self.publish("starting", "Loading the training arenas...")

        restarts = 0
        while True:
            config_path = arena_trainer.resolve_path(self.args.config, self.root)
            config, self.max_steps = effective_config(
                config_path, self.behavior, plan.last_step, self.runs_dir / f"{plan.run_id}.service.yaml"
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
                self.log(f"stopped by the viewer at step {self.progress.step:,} (exit {code})")
                self.publish("stopped", f"Stopped. Progress saved at step {plan.last_step:,}.")
                self.stop_path.unlink(missing_ok=True)
                return 0

            finished = code == 0 and plan.last_step >= self.max_steps * EXTEND_FRACTION
            restarts = restarts + 1 if seconds < QUICK_EXIT_SECONDS else 0
            if not finished or restarts > 2:
                message = f"Training stopped unexpectedly (exit code {code}). See Trainer/runs/{plan.run_id}.log."
                self.log(message)
                self.publish("error", message)
                return 1
            self.log(f"reached max_steps at {plan.last_step:,}; continuing with a larger budget")
            self.publish("starting", "Reached the step budget; continuing...")

    def _train(self, command: list[str], tail: LogTail) -> tuple[int, bool, float]:
        environment = dict(os.environ, PYTHONUNBUFFERED="1")
        launched = self.clock()
        live_summaries = self.progress.summaries
        process = subprocess.Popen(
            command, cwd=self.root, stdout=self.log_file, stderr=subprocess.STDOUT, env=environment
        )
        self.trainer_pid = process.pid
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


def create_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Run training for the viewer's Train the AI button.")
    parser.add_argument("--run-id", help="Run to train (default: resume the newest run).")
    parser.add_argument("--behavior", default="Warrior")
    parser.add_argument("--config", default=str(arena_trainer.DEFAULT_CONFIG))
    parser.add_argument("--results-dir", default=str(arena_trainer.DEFAULT_RESULTS_DIRECTORY))
    parser.add_argument("--num-envs", type=int, default=4)
    parser.add_argument("--base-port", type=int, default=5005)
    parser.add_argument("--parent-pid", type=int, default=0, help="Stop when this process exits.")
    return parser


def main(argv: Iterable[str] | None = None) -> int:
    args = create_parser().parse_args(list(argv) if argv is not None else None)
    # Ctrl+C is meant for ML-Agents; the service keeps running to report the result.
    signal.signal(signal.SIGINT, signal.SIG_IGN)
    return TrainingService(args).run()


if __name__ == "__main__":
    raise SystemExit(main())
