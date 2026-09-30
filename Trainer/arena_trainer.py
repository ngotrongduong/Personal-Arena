"""Command-line wrapper for reproducible Personal Arena ML-Agents runs."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import time
from typing import Sequence


DEFAULT_CONFIG = Path("Trainer/config/warrior_survivor_ppo.yaml")
CONFIG_DIRECTORY = Path("Trainer/config")
DEFAULT_ENVIRONMENT = Path("Build/Training/PersonalArenaTraining.exe")
DEFAULT_RESULTS_DIRECTORY = Path("Trainer/runs")
SCHEMA_VERSION = 4
SCHEMA_FILE = "schema_version.txt"
SCHEMAS_DIR = Path(__file__).resolve().parent / "schemas"

# M5: the owner's build and Training Focus, passed through to the Unity environment.
STAT_SLOTS = 16
MAX_STAT_POINTS = 20
TRAINING_FOCUSES = ("balanced", "survival", "gold", "boss", "offense")
OWNER_BUILD_SHARE = 0.7


def owner_build(value: str) -> str:
    """argparse type: 16 comma-separated stat point counts (0..20), returned normalised."""
    parts = [part.strip() for part in value.split(",")]
    if len(parts) != STAT_SLOTS:
        raise argparse.ArgumentTypeError(f"owner build needs {STAT_SLOTS} values, got {len(parts)}")
    try:
        points = [int(part) for part in parts]
    except ValueError:
        raise argparse.ArgumentTypeError("owner build values must be integers") from None
    if any(point < 0 or point > MAX_STAT_POINTS for point in points):
        raise argparse.ArgumentTypeError(f"owner build values must be within 0..{MAX_STAT_POINTS}")
    return ",".join(str(point) for point in points)


def owner_tier(value: str) -> int:
    try:
        tier = int(value)
    except ValueError:
        raise argparse.ArgumentTypeError("owner tier must be an integer") from None
    if tier < 1 or tier > 10:
        raise argparse.ArgumentTypeError("owner tier must be within 1..10")
    return tier


def training_focus(value: str) -> str:
    focus = value.strip().lower()
    if focus not in TRAINING_FOCUSES:
        raise argparse.ArgumentTypeError(f"training focus must be one of {', '.join(TRAINING_FOCUSES)}")
    return focus


def add_owner_arguments(parser: argparse.ArgumentParser) -> None:
    parser.add_argument(
        "--owner-build",
        type=owner_build,
        help="The owner's stat points (16 comma-separated counts); mixed into 70%% of episodes.",
    )
    parser.add_argument("--owner-tier", type=owner_tier, help="Difficulty tier of the owner's build (1..10).")
    parser.add_argument("--training-focus", type=training_focus, help="Reward weighting (default: balanced).")


def run_schema_version(run_dir: Path) -> int:
    """Observation schema recorded for a run, or 1 when the marker is missing or unreadable."""
    try:
        return int((run_dir / SCHEMA_FILE).read_text(encoding="utf-8").strip())
    except (OSError, ValueError):
        return 1


def write_schema_version(run_dir: Path, version: int = SCHEMA_VERSION) -> None:
    run_dir.mkdir(parents=True, exist_ok=True)
    (run_dir / SCHEMA_FILE).write_text(str(version), encoding="utf-8")


def replace_directory(source: Path, destination: Path, attempts: int = 10) -> None:
    """``os.replace`` a freshly built folder; Windows scanners can hold it open for a moment."""
    for attempt in range(attempts):
        try:
            os.replace(source, destination)
            return
        except PermissionError:
            if attempt == attempts - 1:
                raise
            time.sleep(0.2 * (attempt + 1))


def schema_path(version: int) -> Path:
    return SCHEMAS_DIR / f"survivor_v{version}.json"


def observation_size(schema: dict) -> int:
    try:
        return sum(
            segment["repeat"] * len(segment["fields"])
            for segment in schema["observation"]
        )
    except (KeyError, TypeError):
        raise ValueError("schema observation has an invalid shape") from None


def load_schema(version: int) -> dict:
    path = schema_path(version)
    try:
        schema = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError(f"cannot load schema v{version}: {error}") from error

    if schema.get("schema_version") != version:
        raise ValueError(f"schema version does not match {path.name}")
    observation = schema.get("observation")
    actions = schema.get("actions")
    if not isinstance(observation, list) or not observation:
        raise ValueError("schema needs observation segments")
    if not isinstance(actions, list) or not actions:
        raise ValueError("schema needs action branches")

    segment_names: set[str] = set()
    for segment in observation:
        name = segment.get("name") if isinstance(segment, dict) else None
        repeat = segment.get("repeat") if isinstance(segment, dict) else None
        fields = segment.get("fields") if isinstance(segment, dict) else None
        if not isinstance(name, str) or not name or name in segment_names:
            raise ValueError("observation segment names must be unique and non-empty")
        if not isinstance(repeat, int) or isinstance(repeat, bool) or repeat <= 0:
            raise ValueError(f"segment {name} has a non-positive repeat")
        if (
            not isinstance(fields, list)
            or not fields
            or any(not isinstance(field, str) or not field for field in fields)
            or len(set(fields)) != len(fields)
        ):
            raise ValueError(f"segment {name} fields must be unique and non-empty")
        segment_names.add(name)

    action_names: set[str] = set()
    for action in actions:
        name = action.get("name") if isinstance(action, dict) else None
        size = action.get("size") if isinstance(action, dict) else None
        if not isinstance(name, str) or not name or name in action_names:
            raise ValueError("action branch names must be unique and non-empty")
        if not isinstance(size, int) or isinstance(size, bool) or size <= 0:
            raise ValueError(f"action branch {name} has a non-positive size")
        action_names.add(name)

    total = observation_size(schema)
    if total <= 0 or schema.get("observation_size", total) != total:
        raise ValueError("schema observation total is inconsistent")
    action_total = sum(action["size"] for action in actions)
    if schema.get("action_size", action_total) != action_total:
        raise ValueError("schema action total is inconsistent")
    return schema


def repository_root() -> Path:
    return Path(__file__).resolve().parents[1]


def create_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Launch ML-Agents PPO training for Personal Arena."
    )
    parser.add_argument(
        "--config",
        help="PPO config (default: Trainer/config/<hero class>_survivor_ppo.yaml).",
    )
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--env", default=str(DEFAULT_ENVIRONMENT))
    parser.add_argument("--num-envs", type=int, default=4)
    parser.add_argument("--time-scale", type=float, default=20.0)
    parser.add_argument("--base-port", type=int, default=5005)
    parser.add_argument("--results-dir", default=str(DEFAULT_RESULTS_DIRECTORY))
    parser.add_argument(
        "--arena-agents",
        type=int,
        help="Heroes trained side by side inside each Unity game (default: the build's 16).",
    )
    parser.add_argument(
        "--hero-class",
        choices=("warrior", "mage", "archer"),
        help="Hero class trained by the Unity environment.",
    )
    parser.add_argument(
        "--torch-device",
        choices=("cpu", "cuda"),
        help="Force the PyTorch device (default: ML-Agents picks the GPU when it can).",
    )
    add_owner_arguments(parser)

    graphics = parser.add_mutually_exclusive_group()
    graphics.add_argument(
        "--no-graphics",
        dest="no_graphics",
        action="store_true",
        default=True,
        help="Run Unity without graphics (the default).",
    )
    graphics.add_argument(
        "--graphics",
        dest="no_graphics",
        action="store_false",
        help="Enable graphics for diagnostics.",
    )

    checkpoint = parser.add_mutually_exclusive_group()
    checkpoint.add_argument("--resume", action="store_true")
    checkpoint.add_argument("--initialize-from", metavar="RUN_ID")
    checkpoint.add_argument(
        "--force",
        action="store_true",
        help="Overwrite a run folder that exists but has no checkpoint to resume.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print the resolved command without starting training.",
    )
    return parser


def resolve_path(value: str, root: Path) -> Path:
    path = Path(value).expanduser()
    return path.resolve() if path.is_absolute() else (root / path).resolve()


def config_for(args: argparse.Namespace) -> str:
    """The explicit --config, else the config whose behavior matches the hero class."""
    if args.config:
        return args.config
    hero_class = getattr(args, "hero_class", None) or "warrior"
    return str(CONFIG_DIRECTORY / f"{hero_class}_survivor_ppo.yaml")


def build_command(args: argparse.Namespace, root: Path | None = None) -> list[str]:
    root = repository_root() if root is None else root.resolve()
    learner = root / ".venv-ml" / "Scripts" / "mlagents-learn.exe"
    config = resolve_path(config_for(args), root)
    environment = resolve_path(args.env, root)
    results_directory = resolve_path(args.results_dir, root)

    command = [
        str(learner),
        str(config),
        "--run-id",
        args.run_id,
        "--env",
        str(environment),
        "--num-envs",
        str(args.num_envs),
        "--time-scale",
        str(args.time_scale),
        "--base-port",
        str(args.base_port),
        "--results-dir",
        str(results_directory),
    ]
    if args.no_graphics:
        command.append("--no-graphics")
    if getattr(args, "torch_device", None):
        command.extend(("--torch-device", args.torch_device))
    if args.resume:
        command.append("--resume")
    elif args.initialize_from:
        command.extend(("--initialize-from", args.initialize_from))
    elif args.force:
        command.append("--force")
    environment_arguments = []
    if getattr(args, "arena_agents", None):
        environment_arguments.extend(("--arena-agents", str(args.arena_agents)))
    if getattr(args, "hero_class", None):
        environment_arguments.extend(("--hero-class", args.hero_class))
    if getattr(args, "owner_build", None):
        environment_arguments.extend(("--owner-build", args.owner_build))
        environment_arguments.extend(("--owner-tier", str(getattr(args, "owner_tier", None) or 1)))
    if getattr(args, "training_focus", None):
        environment_arguments.extend(("--training-focus", args.training_focus))
    if environment_arguments:
        # --env-args takes the rest of the command line, so it must come last.
        command.append("--env-args")
        command.extend(environment_arguments)
    return command


def format_command(command: Sequence[str]) -> str:
    return subprocess.list2cmdline(list(command))


def main(argv: Sequence[str] | None = None) -> int:
    parser = create_parser()
    args = parser.parse_args(argv)
    root = repository_root()
    command = build_command(args, root)

    if args.dry_run:
        print(format_command(command))
        return 0

    learner = Path(command[0])
    config = Path(command[1])
    environment = Path(command[command.index("--env") + 1])
    if not config.is_file():
        if args.config is None and args.hero_class in ("mage", "archer"):
            parser.error("Mage/Archer Survivor brains arrive in M7")
        parser.error(f"trainer config was not found: {config}")
    if not learner.is_file():
        parser.error(f"mlagents-learn was not found: {learner}")
    if not environment.is_file():
        parser.error(f"training environment was not found: {environment}")
    if args.num_envs < 1:
        parser.error("--num-envs must be at least 1")
    if args.time_scale <= 0:
        parser.error("--time-scale must be greater than 0")
    if not 1 <= args.base_port <= 65535:
        parser.error("--base-port must be between 1 and 65535")

    os.makedirs(resolve_path(args.results_dir, root), exist_ok=True)
    completed = subprocess.run(command, cwd=root, check=False)
    return completed.returncode


if __name__ == "__main__":
    sys.exit(main())
