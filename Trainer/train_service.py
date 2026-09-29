"""Background training service behind the viewer's "Train the AI" button.

The viewer (``Build/Watch/PersonalArenaWatch.exe``) starts this script in a hidden console::

    .venv-ml/Scripts/python.exe Trainer/train_service.py --parent-pid <viewer pid>

The service resumes the newest run (or starts ``warrior-s001``), appends ML-Agents output to
``Trainer/runs/<run>.log``, keeps exporting ``latest.brain`` so the viewer hot-loads every new
checkpoint, and reports progress in ``Trainer/runs/training_service.json``.

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
import signal
import subprocess
import sys
import time
import traceback
from typing import Callable, Iterable, TextIO

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from Trainer import arena_trainer, export_brain  # noqa: E402

STATUS_NAME = "training_service.json"
STOP_NAME = "training_service.stop"
LOCK_NAME = "training_service.lock"
LOCK_WAIT = 15.0
ERROR_LOG_NAME = "training_service.err.log"
DEFAULT_ARENA_AGENTS = 16  # TrainingArenaHost's default warriors per Unity game.
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
BEHAVIORS = {name.lower(): name for name in ("Warrior", "Mage", "Archer")}

SUMMARY_PATTERN = re.compile(
    r"\[INFO\] (?P<behavior>[^.\s]+)\. Step: (?P<step>\d+)\. Time Elapsed: [\d.]+ s\."
    r"(?: Mean Reward: (?P<reward>-?\d+(?:\.\d+)?)\.)?"
)
LESSON_PATTERN = re.compile(
    r"Parameter '(?P<name>\w+)' is in lesson '(?P<lesson>[^']+)' and has value '[^']*?(?P<value>-?\d+(?:\.\d+)?)'"
)


def normalize_behavior(value: str) -> str:
    try:
        return BEHAVIORS[value.lower()]
    except KeyError as error:
        raise argparse.ArgumentTypeError(
            "behavior must be Warrior, Mage, or Archer"
        ) from error


def default_config(behavior: str) -> str:
    return str(Path("Trainer/config") / f"{behavior.lower()}_survivor_ppo.yaml")


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


def plan_run(runs_dir: Path, behavior: str, requested: str | None = None) -> RunPlan:
    if requested:
        run_id = requested
        requested_dir = runs_dir / run_id
        requested_behavior = requested_dir / behavior
        if (
            (requested_behavior / "checkpoint.pt").is_file()
            and arena_trainer.run_schema_version(requested_dir) != arena_trainer.SCHEMA_VERSION
        ):
            run_id = next_run_id(runs_dir, behavior)
    else:
        resumable = [
            path
            for path in runs_dir.glob(f"*/{behavior}")
            if path.is_dir()
            and (path / "checkpoint.pt").is_file()
            and export_brain.checkpoints(path)
            and arena_trainer.run_schema_version(path.parent)
            == arena_trainer.SCHEMA_VERSION
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
        run_id = newest.parent.name if newest is not None else next_run_id(runs_dir, behavior)

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
    return RunPlan(run_id, mode, last_step)


def next_run_id(runs_dir: Path, behavior: str) -> str:
    prefix = behavior.lower()
    pattern = re.compile(rf"^{re.escape(prefix)}-s(\d{{3}})$")
    numbers = []
    if runs_dir.is_dir():
        for path in runs_dir.iterdir():
            match = pattern.match(path.name)
            if path.is_dir() and match:
                numbers.append(int(match.group(1)))
    return f"{prefix}-s{max(numbers, default=0) + 1:03d}"


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
        }

    def publish(self, state: str, message: str = "") -> None:
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
        if self.torch_device:
            arguments.extend(("--torch-device", self.torch_device))
        arguments.extend(("--time-scale", str(self.args.time_scale)))
        if self.args.arena_agents:
            arguments.extend(("--arena-agents", str(self.args.arena_agents)))
        arguments.extend(("--hero-class", self.behavior.lower()))
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
            if self.run_id:
                self.write_history()
            if self.log_file is not None:
                self.log_file.close()
            lock.close()

    def _run_locked(self) -> int:
        self.stop_path.unlink(missing_ok=True)
        plan = plan_run(self.runs_dir, self.behavior, self.args.run_id)
        self.run_id = plan.run_id
        if plan.mode in ("new", "force"):
            arena_trainer.write_schema_version(self.runs_dir / plan.run_id)
        log_path = self.runs_dir / f"{plan.run_id}.log"
        tail = LogTail(log_path)
        for line in tail.read_lines():
            self.progress.feed(line)
        if tail.partial:
            self.progress.feed(tail.partial)
        self.log_file = log_path.open("a", encoding="utf-8")
        self.publish("starting", "Loading the training arenas...")

        restarts = 0
        crashes = 0
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
