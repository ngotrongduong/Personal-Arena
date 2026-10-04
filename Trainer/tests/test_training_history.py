from __future__ import annotations

import json
import os
from pathlib import Path

from torch.utils.tensorboard import SummaryWriter

from Trainer import arena_trainer, training_history


def write_events(
    behavior_dir: Path, suffix: str, scalars: list[tuple[str, float, int]]
) -> Path:
    writer = SummaryWriter(log_dir=str(behavior_dir), filename_suffix=suffix)
    for tag, value, step in scalars:
        writer.add_scalar(tag, value, step)
    writer.close()
    return next(behavior_dir.glob(f"events.out.tfevents.*{suffix}"))


def by_tag(history: dict) -> dict[str, dict]:
    return {series["tag"]: series for series in history["series"]}


def test_build_history_merges_filters_deduplicates_and_downsamples(tmp_path: Path):
    run_dir = tmp_path / "warrior-s002"
    behavior_dir = run_dir / "Warrior"
    behavior_dir.mkdir(parents=True)
    arena_trainer.write_schema_version(run_dir)
    first = write_events(behavior_dir, ".first", [
        *(('Environment/Cumulative Reward', float(step), step) for step in (0, 5, 10, 15, 20)),
        ("Arena/Damage", float("nan"), 2),
        ("Arena/Damage", float("inf"), 3),
        ("Policy/Entropy", 0.75, 5),
        ("Losses/Value Loss", 1.5, 5),
        ("Losses/Policy Loss", -0.25, 5),
        ("Policy/Learning Rate", 0.001, 5),
    ])
    second = write_events(behavior_dir, ".second", [
        ("Environment/Cumulative Reward", 100.0, 10),
        ("Environment/Cumulative Reward", 25.0, 25),
        ("Arena/Kills", 4.0, 25),
        ("Arena/SurvivedSeconds", 180.0, 25),
        ("Arena/Level", 12.0, 25),
        ("Arena/Gold", 350.0, 25),
        ("Arena/DeathCause/Surrounded", 1.0, 25),
    ])
    os.utime(first, ns=(1_000_000_000, 1_000_000_000))
    os.utime(second, ns=(2_000_000_000, 2_000_000_000))

    history = training_history.build_history(run_dir, "Warrior")

    assert history is not None
    assert set(history) == {
        "run_id", "behavior", "schema_version", "updated_unix", "last_step", "series"
    }
    assert history["run_id"] == "warrior-s002"
    assert history["behavior"] == "Warrior"
    assert history["schema_version"] == 5
    assert history["last_step"] == 25
    series = by_tag(history)
    assert set(series) == {
        "Arena/Damage", "Arena/DeathCause/Surrounded", "Arena/Gold", "Arena/Kills",
        "Arena/Level", "Arena/SurvivedSeconds", "Environment/Cumulative Reward",
        "Losses/Policy Loss", "Losses/Value Loss", "Policy/Entropy",
    }
    reward = series["Environment/Cumulative Reward"]
    assert reward["steps"] == [0, 5, 10, 15, 20, 25]
    assert reward["values"] == [0.0, 5.0, 100.0, 15.0, 20.0, 25.0]
    assert series["Arena/Damage"]["values"] == [0.0, 0.0]
    assert all(isinstance(step, int) for item in history["series"] for step in item["steps"])
    assert all(isinstance(value, float) for item in history["series"] for value in item["values"])
    assert all(set(item) == {"tag", "steps", "values"} for item in history["series"])

    downsampled = training_history.build_history(run_dir, "Warrior", max_points=4)

    assert downsampled is not None
    reward = by_tag(downsampled)["Environment/Cumulative Reward"]
    assert len(reward["steps"]) <= 4
    assert (reward["steps"][0], reward["values"][0]) == (0, 0.0)
    assert (reward["steps"][-1], reward["values"][-1]) == (25, 25.0)


def test_write_history_is_atomic_json_and_skips_unchanged_events(tmp_path: Path):
    run_dir = tmp_path / "warrior-s002"
    behavior_dir = run_dir / "Warrior"
    behavior_dir.mkdir(parents=True)
    write_events(behavior_dir, ".first", [("Environment/Episode Length", 10.0, 1)])

    assert training_history.write_history(run_dir, "Warrior")
    output = run_dir / training_history.HISTORY_NAME
    first_mtime = output.stat().st_mtime_ns
    assert not training_history.write_history(run_dir, "Warrior")
    assert output.stat().st_mtime_ns == first_mtime
    assert not output.with_name(output.name + ".tmp").exists()
    assert json.loads(output.read_text(encoding="utf-8"))["last_step"] == 1

    write_events(behavior_dir, ".second", [("Arena/Kills", 2.0, 2)])

    assert training_history.write_history(run_dir, "Warrior")
    assert json.loads(output.read_text(encoding="utf-8"))["last_step"] == 2


def test_history_returns_nothing_without_event_files(tmp_path: Path):
    run_dir = tmp_path / "warrior-s001"

    assert training_history.build_history(run_dir, "Warrior") is None
    assert not training_history.write_history(run_dir, "Warrior")


def test_cli_prefers_current_schema_events_without_requiring_a_checkpoint(
    tmp_path: Path, capsys
):
    old_dir = tmp_path / "warrior-001"
    current_dir = tmp_path / "warrior-s001"
    old_event = write_events(
        old_dir / "Warrior", ".old", [("Environment/Cumulative Reward", 1.0, 1)]
    )
    current_event = write_events(
        current_dir / "Warrior", ".current", [("Environment/Cumulative Reward", 2.0, 2)]
    )
    arena_trainer.write_schema_version(current_dir)
    os.utime(current_event, ns=(1_000_000_000, 1_000_000_000))
    os.utime(old_event, ns=(2_000_000_000, 2_000_000_000))

    assert training_history.main(["--results-dir", str(tmp_path)]) == 0

    output = current_dir / training_history.HISTORY_NAME
    assert capsys.readouterr().out.strip() == str(output)
    assert output.is_file()
    assert not (old_dir / training_history.HISTORY_NAME).exists()


def test_history_scanners_ignore_champions_directory(tmp_path: Path):
    fake_run = tmp_path / "champions"
    write_events(fake_run / "Warrior", ".fake", [("Arena/Kills", 1.0, 1)])
    arena_trainer.write_schema_version(fake_run)

    assert training_history.discover_behaviors(tmp_path) == []
    assert training_history._newest_run_dir(tmp_path, "Warrior") is None
