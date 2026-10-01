"""M7 (T-031): Mage and Archer train their own Survivor brains exactly like the Warrior."""

from __future__ import annotations

import json
import os
from pathlib import Path

import pytest
import yaml

from Trainer import arena_trainer, brain_lineage, brain_upgrade, champion, train_service

CONFIG_DIR = Path(__file__).resolve().parents[1] / "config"
TRAINER_DIR = Path(__file__).resolve().parents[1]
CLASSES = ("Warrior", "Mage", "Archer")


@pytest.fixture(autouse=True)
def no_real_build_swap(monkeypatch):
    monkeypatch.setattr(train_service, "install_staged_build", lambda root: "")


def make_checkpoint(runs: Path, run_id: str, behavior: str, step: int,
                    schema: int | None = arena_trainer.SCHEMA_VERSION, mtime: float | None = None) -> Path:
    behavior_dir = runs / run_id / behavior
    behavior_dir.mkdir(parents=True, exist_ok=True)
    numbered = behavior_dir / f"{behavior}-{step}.pt"
    numbered.write_bytes(f"pt-{run_id}-{step}".encode())
    (behavior_dir / "checkpoint.pt").write_bytes(b"latest")
    if schema is not None:
        arena_trainer.write_schema_version(runs / run_id, schema)
    if mtime is not None:
        os.utime(numbered, (mtime, mtime))
    return behavior_dir


def snapshot_files(folder: Path) -> dict[Path, bytes]:
    return {path: path.read_bytes() for path in folder.rglob("*") if path.is_file()}


def evaluation(median: float = 600.0) -> dict:
    summary = {
        "Runs": 100, "MedianSurvivedSeconds": median, "P10SurvivedSeconds": 420.0,
        "MeanSurvivedSeconds": median, "WinRate": 0.5, "CatastrophicCount": 0,
        "Behavior": {"Aggression": 0.4},
    }
    return {"summary": summary, "passes_m4a": True, "settings": champion.evaluation_settings()}


# -- configs ------------------------------------------------------------------------------------


def renamed(data, old: str, new: str):
    """``data`` with every behavior reference renamed (keys and ``behavior:`` values)."""
    if isinstance(data, dict):
        return {
            (new if key == old else key): (new if key == "behavior" and value == old else renamed(value, old, new))
            for key, value in data.items()
        }
    if isinstance(data, list):
        return [renamed(item, old, new) for item in data]
    return data


@pytest.mark.parametrize("behavior", ["Mage", "Archer"])
def test_class_config_is_the_warrior_config_with_its_own_behavior_name(behavior: str):
    warrior = yaml.safe_load((CONFIG_DIR / "warrior_survivor_ppo.yaml").read_text(encoding="utf-8"))
    config = yaml.safe_load((CONFIG_DIR / f"{behavior.lower()}_survivor_ppo.yaml").read_text(encoding="utf-8"))

    assert list(config["behaviors"]) == [behavior]
    assert config == renamed(warrior, "Warrior", behavior)
    text = (CONFIG_DIR / f"{behavior.lower()}_survivor_ppo.yaml").read_text(encoding="utf-8")
    assert "Warrior" not in text


@pytest.mark.parametrize("behavior", CLASSES)
def test_default_config_of_each_class_trains_that_behavior(behavior: str):
    root = arena_trainer.repository_root()
    path = root / train_service.default_config(behavior)

    assert path.is_file()
    assert arena_trainer.config_behaviors(path) == [behavior]
    assert train_service.config_problem(behavior, path) == ""


# -- run ids and newest-run selection -----------------------------------------------------------


@pytest.mark.parametrize("behavior", CLASSES)
def test_each_class_starts_its_own_s001(tmp_path: Path, behavior: str):
    plan = train_service.plan_run(tmp_path, behavior)

    assert (plan.run_id, plan.mode, plan.last_step, plan.message) == (f"{behavior.lower()}-s001", "new", 0, "")


def test_run_numbers_are_counted_per_class(tmp_path: Path):
    for run_id in ("warrior-s001", "warrior-s002", "warrior-s003", "mage-s001"):
        (tmp_path / run_id).mkdir()

    assert train_service.next_run_id(tmp_path, "Warrior") == "warrior-s004"
    assert train_service.next_run_id(tmp_path, "Mage") == "mage-s002"
    assert train_service.next_run_id(tmp_path, "Archer") == "archer-s001"


def test_newest_run_selection_is_isolated_per_class(tmp_path: Path):
    make_checkpoint(tmp_path, "mage-s001", "Mage", 3_000_000, mtime=100)
    make_checkpoint(tmp_path, "warrior-s001", "Warrior", 106_000_000, mtime=300)
    make_checkpoint(tmp_path, "archer-s002", "Archer", 5_000_000, mtime=200)

    warrior = train_service.plan_run(tmp_path, "Warrior")
    mage = train_service.plan_run(tmp_path, "Mage")
    archer = train_service.plan_run(tmp_path, "Archer")

    assert (warrior.run_id, warrior.mode, warrior.last_step) == ("warrior-s001", "resume", 106_000_000)
    assert (mage.run_id, mage.mode, mage.last_step) == ("mage-s001", "resume", 3_000_000)
    assert (archer.run_id, archer.mode, archer.last_step) == ("archer-s002", "resume", 5_000_000)


def test_a_class_never_resumes_a_run_named_for_another_class(tmp_path: Path):
    # Even if a Mage folder somehow ended up inside a Warrior run, the Mage must not train it.
    make_checkpoint(tmp_path, "warrior-s001", "Warrior", 1_000_000, mtime=100)
    make_checkpoint(tmp_path, "warrior-s001", "Mage", 2_000_000, mtime=200)
    before = snapshot_files(tmp_path / "warrior-s001")

    plan = train_service.plan_run(tmp_path, "Mage")

    assert (plan.run_id, plan.mode) == ("mage-s001", "new")
    assert snapshot_files(tmp_path / "warrior-s001") == before


def test_a_fresh_mage_ignores_the_old_arena_mage_run_quietly(tmp_path: Path):
    # The owner's runs folder has mage-001 from the round arena (no schema marker).
    make_checkpoint(tmp_path, "mage-001", "Mage", 657_335, schema=None)
    (tmp_path / "mage-001" / "rules_version.txt").write_text("3", encoding="utf-8")
    make_checkpoint(tmp_path, "warrior-s001", "Warrior", 106_000_000)

    plan = train_service.plan_run(tmp_path, "Mage")

    assert (plan.run_id, plan.mode, plan.last_step, plan.message) == ("mage-s001", "new", 0, "")


def write_fake_schema(directory: Path, version: int) -> None:
    schema = {
        "schema_version": version,
        "observation": [{"name": "self", "repeat": 1, "fields": ["value"]}],
        "actions": [{"name": "move", "size": 1}],
    }
    (directory / f"survivor_v{version}.json").write_text(json.dumps(schema), encoding="utf-8")


def test_a_schema_upgrade_never_turns_a_warrior_brain_into_a_mage(tmp_path: Path, monkeypatch):
    schema_dir = tmp_path / "schemas"
    schema_dir.mkdir()
    write_fake_schema(schema_dir, 4)
    write_fake_schema(schema_dir, 5)
    monkeypatch.setattr(arena_trainer, "SCHEMAS_DIR", schema_dir)
    monkeypatch.setattr(arena_trainer, "SCHEMA_VERSION", 5)
    runs = tmp_path / "runs"
    make_checkpoint(runs, "warrior-s001", "Warrior", 800, schema=4)
    calls = []
    monkeypatch.setattr(train_service.brain_upgrade, "upgrade_run", lambda *args: calls.append(args))

    plan = train_service.plan_run(runs, "Mage")

    assert calls == []
    assert (plan.run_id, plan.mode, plan.message) == ("mage-s001", "new", "")


def test_a_requested_run_of_another_class_is_never_forced(tmp_path: Path):
    make_checkpoint(tmp_path, "warrior-s001", "Warrior", 1_000_000)
    make_checkpoint(tmp_path, "mage-s001", "Mage", 2_000_000)
    before = snapshot_files(tmp_path / "warrior-s001")

    plan = train_service.plan_run(tmp_path, "Mage", "warrior-s001")

    assert (plan.run_id, plan.mode, plan.last_step) == ("mage-s001", "resume", 2_000_000)
    assert "warrior-s001" in plan.message
    assert snapshot_files(tmp_path / "warrior-s001") == before


def test_a_requested_unprefixed_run_holding_another_class_is_never_forced(tmp_path: Path):
    make_checkpoint(tmp_path, "custom", "Warrior", 1_000_000)

    plan = train_service.plan_run(tmp_path, "Archer", "custom")

    assert (plan.run_id, plan.mode) == ("archer-s001", "new")


def test_a_requested_own_branch_is_still_resumed(tmp_path: Path):
    make_checkpoint(tmp_path, "mage-s001", "Mage", 2_000_000, mtime=200)
    make_checkpoint(tmp_path, "mage-s002", "Mage", 1_000_000, mtime=100)

    plan = train_service.plan_run(tmp_path, "Mage", "mage-s002")

    assert (plan.run_id, plan.mode, plan.last_step, plan.message) == ("mage-s002", "resume", 1_000_000, "")


@pytest.mark.parametrize(("run_id", "expected"), [
    ("warrior-s001", "Warrior"), ("mage-s001", "Mage"), ("Archer-s010", "Archer"),
    ("mage-001", "Mage"), ("champions", None), ("broken", None), ("mage", None),
])
def test_run_class_reads_the_prefix(run_id: str, expected: str | None):
    assert arena_trainer.run_class(run_id) == expected


# -- the service trains Mage and Archer for real ------------------------------------------------


@pytest.mark.parametrize("behavior", ["Mage", "Archer"])
def test_service_trains_a_fresh_class_from_scratch(tmp_path: Path, behavior: str):
    runs = tmp_path / "runs"
    make_checkpoint(runs, "warrior-s001", "Warrior", 106_000_000)
    warrior_before = snapshot_files(runs / "warrior-s001")
    fake_exe = tmp_path / "fake.exe"
    fake_exe.write_bytes(b"")
    args = train_service.create_parser().parse_args(
        ["--behavior", behavior.lower(), "--results-dir", str(runs)]
    )
    service = train_service.TrainingService(args)
    commands: list[list[str]] = []
    real_command = service.trainer_command

    def capture(plan, config):
        commands.append(real_command(plan, config))
        return [str(fake_exe), "--env", str(fake_exe)]

    service.trainer_command = capture
    service._train = lambda command, tail: (0, True, 1.0)
    service.export = lambda: None
    service.write_history = lambda: None

    assert service.run() == 0

    run_id = f"{behavior.lower()}-s001"
    command = commands[0]
    assert command[1].endswith(f"{behavior.lower()}_survivor_ppo.yaml")
    assert command[command.index("--run-id") + 1] == run_id
    assert "--force" in command and "--resume" not in command
    assert "--initialize-from" not in command
    assert command[command.index("--env-args"):] == ["--env-args", "--hero-class", behavior.lower()]
    assert arena_trainer.run_schema_version(runs / run_id) == arena_trainer.SCHEMA_VERSION
    status = json.loads((runs / train_service.STATUS_NAME).read_text(encoding="utf-8"))
    assert (status["behavior"], status["run_id"], status["state"]) == (behavior, run_id, "stopped")
    assert "M7" not in status["message"]
    assert snapshot_files(runs / "warrior-s001") == warrior_before
    assert not (runs / "champions" / "Warrior").exists()


def test_a_config_for_another_class_is_refused_before_any_run_folder(tmp_path: Path):
    root = arena_trainer.repository_root()
    args = train_service.create_parser().parse_args([
        "--behavior", "Mage", "--results-dir", str(tmp_path),
        "--config", str(root / "Trainer" / "config" / "warrior_survivor_ppo.yaml"),
    ])

    assert train_service.TrainingService(args).run() == 2

    status = json.loads((tmp_path / train_service.STATUS_NAME).read_text(encoding="utf-8"))
    assert status["state"] == "error"
    assert status["message"] == "Trainer config warrior_survivor_ppo.yaml has no Mage behavior."
    assert not (tmp_path / "mage-s001").exists()


def test_the_old_m7_gate_is_gone():
    assert not hasattr(train_service, "FUTURE_BEHAVIORS")
    for path in (TRAINER_DIR / "train_service.py", TRAINER_DIR / "arena_trainer.py"):
        text = path.read_text(encoding="utf-8")
        assert "arrives in M7" not in text
        assert "Only the Warrior can train" not in text


# -- champion, lineage and upgrade are per behavior ---------------------------------------------


@pytest.mark.parametrize("behavior", ["Mage", "Archer"])
def test_champion_and_lineage_live_under_the_class_folder(tmp_path: Path, behavior: str):
    runs = tmp_path / "runs"
    run_id = f"{behavior.lower()}-s001"
    make_checkpoint(runs, run_id, behavior, 2_000_000)
    candidate = tmp_path / "candidate.brain"
    candidate.write_bytes(b"brain")

    line = champion.apply_evaluation(runs, behavior, run_id, 2_000_000, candidate, evaluation(),
                                     log=lambda _: None)
    brain_lineage.record_evaluation(
        runs, behavior, run_id, 2_000_000, candidate,
        runs / run_id / behavior / f"{behavior}-2000000.pt", line, log=lambda _: None,
    )

    directory = runs / "champions" / behavior
    assert champion.champion_directory(runs, behavior) == directory
    assert (directory / "champion.brain").read_bytes() == b"brain"
    assert champion.load_champion(runs, behavior)["run_id"] == run_id
    assert champion.load_champion(runs, "Warrior") is None
    assert champion.last_evaluated_step(runs, behavior, run_id) == 2_000_000
    assert champion.last_evaluated_step(runs, "Warrior", run_id) == 0
    assert brain_lineage.lineage_directory(runs, behavior) == directory / "lineage"
    version = directory / "lineage" / "versions" / f"{run_id}-2000000"
    assert (version / "checkpoint.pt").read_bytes() == f"pt-{run_id}-2000000".encode()
    assert not (runs / "champions" / "Warrior").exists()


def test_lineage_fork_of_a_mage_version_creates_a_mage_run(tmp_path: Path):
    runs = tmp_path / "runs"
    make_checkpoint(runs, "warrior-s001", "Warrior", 1_000_000)
    make_checkpoint(runs, "warrior-s002", "Warrior", 1_000_000)
    make_checkpoint(runs, "mage-s001", "Mage", 4_000_000)
    candidate = tmp_path / "candidate.brain"
    candidate.write_bytes(b"brain")
    line = champion.apply_evaluation(runs, "Mage", "mage-s001", 4_000_000, candidate, evaluation(),
                                     log=lambda _: None)
    brain_lineage.record_evaluation(
        runs, "Mage", "mage-s001", 4_000_000, candidate,
        runs / "mage-s001" / "Mage" / "Mage-4000000.pt", line, log=lambda _: None,
    )

    result = brain_lineage.fork(runs, "Mage", version="mage-s001-4000000", log=lambda _: None)

    assert result["run_id"] == "mage-s002"
    assert (runs / "mage-s002" / "Mage" / "Mage-4000000.pt").is_file()
    assert brain_lineage.load_branches(runs, "Mage")[0]["run_id"] == "mage-s002"
    assert brain_lineage.load_branches(runs, "Warrior") == []


def test_lineage_cannot_snapshot_another_class_run(tmp_path: Path):
    runs = tmp_path / "runs"
    make_checkpoint(runs, "warrior-s001", "Warrior", 1_000_000)

    with pytest.raises(brain_lineage.LineageError):
        brain_lineage.snapshot(runs, "Mage", "warrior-s001", evaluate=False, now=lambda: 1e12)


def test_cli_behavior_names_are_normalized(tmp_path: Path, monkeypatch, capsys):
    seen = []
    monkeypatch.setattr(champion, "load_champion", lambda results_dir, behavior: seen.append(behavior))
    monkeypatch.setattr(champion, "latest_evaluation", lambda results_dir, behavior: seen.append(behavior))

    assert champion.main(["--results-dir", str(tmp_path), "--behavior", "mage", "status"]) == 0
    assert seen == ["Mage", "Mage"]
    assert brain_lineage.create_parser().parse_args(["--behavior", "ARCHER", "sync"]).behavior == "Archer"
    with pytest.raises(SystemExit):
        champion.main(["--behavior", "Necromancer", "status"])
    capsys.readouterr()


def test_brain_upgrade_cli_takes_the_class_from_the_source_run(tmp_path: Path, monkeypatch):
    calls = []
    monkeypatch.setattr(brain_upgrade, "upgrade_run", lambda *args: calls.append(args) or tmp_path)

    assert brain_upgrade.main([
        "--runs-dir", str(tmp_path), "--source", "archer-s001", "--new-run", "archer-s002",
        "--old-version", "4", "--new-version", "5",
    ]) == 0

    assert calls == [(tmp_path, "archer-s001", "Archer", "archer-s002", 4, 5)]


# -- arena_trainer --------------------------------------------------------------------------------


@pytest.mark.parametrize("hero_class", ["mage", "archer"])
def test_arena_trainer_dry_run_trains_the_class(capsys, hero_class: str):
    assert arena_trainer.main(["--run-id", f"{hero_class}-smoke", "--hero-class", hero_class, "--dry-run"]) == 0

    output = capsys.readouterr().out
    assert f"{hero_class}_survivor_ppo.yaml" in output
    assert output.rstrip().endswith(f"--env-args --hero-class {hero_class}")


def test_arena_trainer_infers_the_class_from_an_explicit_config(capsys):
    assert arena_trainer.main([
        "--run-id", "archer-smoke", "--config", "Trainer/config/archer_survivor_ppo.yaml", "--dry-run",
    ]) == 0

    assert capsys.readouterr().out.rstrip().endswith("--env-args --hero-class archer")


def test_arena_trainer_refuses_a_config_for_another_class(capsys):
    with pytest.raises(SystemExit):
        arena_trainer.main([
            "--run-id", "mage-smoke", "--hero-class", "mage",
            "--config", "Trainer/config/warrior_survivor_ppo.yaml", "--dry-run",
        ])

    assert "trains Warrior, not Mage" in capsys.readouterr().err
