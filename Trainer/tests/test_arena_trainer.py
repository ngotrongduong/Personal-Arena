from pathlib import Path

from Trainer import arena_trainer


def parse(*arguments: str):
    return arena_trainer.create_parser().parse_args(arguments)


def test_build_command_resolves_defaults_and_enables_headless_mode(tmp_path: Path):
    args = parse("--run-id", "warrior-smoke")

    command = arena_trainer.build_command(args, tmp_path)

    assert command[0] == str(
        tmp_path / ".venv-ml" / "Scripts" / "mlagents-learn.exe"
    )
    assert command[1] == str(tmp_path / "Trainer" / "config" / "warrior_ppo.yaml")
    assert command[command.index("--env") + 1] == str(
        tmp_path / "Build" / "Training" / "PersonalArenaTraining.exe"
    )
    assert command[command.index("--num-envs") + 1] == "4"
    assert command[command.index("--time-scale") + 1] == "20.0"
    assert command[command.index("--results-dir") + 1] == str(
        tmp_path / "Trainer" / "runs"
    )
    assert "--no-graphics" in command


def test_build_command_adds_resume_and_custom_values(tmp_path: Path):
    args = parse(
        "--run-id",
        "continued",
        "--config",
        "custom.yaml",
        "--env",
        "env.exe",
        "--num-envs",
        "2",
        "--time-scale",
        "10",
        "--base-port",
        "6000",
        "--results-dir",
        "runs",
        "--resume",
    )

    command = arena_trainer.build_command(args, tmp_path)

    assert command[1] == str(tmp_path / "custom.yaml")
    assert command[command.index("--base-port") + 1] == "6000"
    assert command[-1] == "--resume"


def test_build_command_supports_initialization_and_graphics(tmp_path: Path):
    args = parse(
        "--run-id",
        "fine-tune",
        "--initialize-from",
        "warrior-base",
        "--graphics",
    )

    command = arena_trainer.build_command(args, tmp_path)

    assert "--no-graphics" not in command
    assert command[-2:] == ["--initialize-from", "warrior-base"]


def test_build_command_can_force_the_cpu(tmp_path: Path):
    command = arena_trainer.build_command(parse("--run-id", "warrior-001", "--torch-device", "cpu"), tmp_path)

    assert command[command.index("--torch-device") + 1] == "cpu"
    assert "--torch-device" not in arena_trainer.build_command(parse("--run-id", "warrior-001"), tmp_path)


def test_build_command_passes_the_arena_agent_count_last(tmp_path: Path):
    command = arena_trainer.build_command(
        parse("--run-id", "warrior-001", "--resume", "--arena-agents", "32"), tmp_path
    )

    assert command[-4:] == ["--resume", "--env-args", "--arena-agents", "32"]
    assert "--env-args" not in arena_trainer.build_command(parse("--run-id", "warrior-001"), tmp_path)


def test_rules_version_defaults_to_one_and_round_trips_current_version(tmp_path: Path):
    run_dir = tmp_path / "warrior-001"

    assert arena_trainer.run_rules_version(run_dir) == 1

    arena_trainer.write_rules_version(run_dir)

    assert arena_trainer.run_rules_version(run_dir) == arena_trainer.RULES_VERSION
    assert (run_dir / arena_trainer.RULES_FILE).read_text(encoding="utf-8") == "2"


def test_rules_version_treats_an_unreadable_marker_as_version_one(tmp_path: Path):
    marker = tmp_path / "warrior-001" / arena_trainer.RULES_FILE
    marker.mkdir(parents=True)

    assert arena_trainer.run_rules_version(marker.parent) == 1
