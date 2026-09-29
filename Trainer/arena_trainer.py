"""Command-line wrapper for reproducible Personal Arena ML-Agents runs."""

from __future__ import annotations

import argparse
import os
from pathlib import Path
import subprocess
import sys
from typing import Sequence


DEFAULT_CONFIG = Path("Trainer/config/warrior_ppo.yaml")
CONFIG_DIRECTORY = Path("Trainer/config")
DEFAULT_ENVIRONMENT = Path("Build/Training/PersonalArenaTraining.exe")
DEFAULT_RESULTS_DIRECTORY = Path("Trainer/runs")
RULES_VERSION = 3
RULES_FILE = "rules_version.txt"


def run_rules_version(run_dir: Path) -> int:
    try:
        return int((run_dir / RULES_FILE).read_text(encoding="utf-8").strip())
    except (OSError, ValueError):
        return 1


def write_rules_version(run_dir: Path) -> None:
    run_dir.mkdir(parents=True, exist_ok=True)
    (run_dir / RULES_FILE).write_text(str(RULES_VERSION), encoding="utf-8")


def repository_root() -> Path:
    return Path(__file__).resolve().parents[1]


def create_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Launch ML-Agents PPO training for Personal Arena."
    )
    parser.add_argument(
        "--config",
        help="PPO config (default: Trainer/config/<hero class>_ppo.yaml).",
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
    return str(CONFIG_DIRECTORY / f"{hero_class}_ppo.yaml")


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
    if not learner.is_file():
        parser.error(f"mlagents-learn was not found: {learner}")
    if not config.is_file():
        parser.error(f"trainer config was not found: {config}")
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
