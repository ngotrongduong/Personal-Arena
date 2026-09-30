import argparse
import json
import threading
from pathlib import Path

import pytest

from Trainer import arena_trainer, train_service

_install_staged_build = train_service.install_staged_build


@pytest.fixture(autouse=True)
def no_real_build_swap(monkeypatch):
    # Service tests run against the real repository root; never touch its Build folder.
    monkeypatch.setattr(train_service, "install_staged_build", lambda root: "")


def summary(step: int, reward: float | None) -> str:
    if reward is None:
        return (f"[INFO] Warrior. Step: {step}. Time Elapsed: 10.000 s. "
                "No episode was completed since last summary. Training.")
    return (f"[INFO] Warrior. Step: {step}. Time Elapsed: 4690.605 s. "
            f"Mean Reward: {reward:.3f}. Std of Reward: 46.988. Training.")


def test_progress_reads_summaries_and_curriculum():
    progress = train_service.Progress("Warrior")

    for line in [
        "[INFO] Parameter 'zombie_count' is in lesson 'OneZombie' and has value 'Float: value=1.0'.",
        "[INFO] Parameter 'arena_size' is in lesson 'arena_size' and has value 'Float: value=20.0'.",
        "[INFO] Parameter 'runner_weight' is in lesson 'Runners' and has value 'Float: value=0.5'.",
        "[INFO] Parameter 'brute_weight' is in lesson 'Brutes' and has value 'Float: value=0.35'.",
        "[INFO] Parameter 'spitter_weight' is in lesson 'Spitters' and has value 'Float: value=0.25'.",
        summary(30000, None),
        summary(60000, -0.5),
        "[INFO] Parameter 'zombie_count' is in lesson 'SixteenZombies' and has value 'Float: value=16.0'.",
        summary(90000, 12.25),
        "[INFO] Other. Step: 120000. Time Elapsed: 1.0 s. Mean Reward: 99.000. Std of Reward: 1.0. Training.",
    ]:
        progress.feed(line)

    assert progress.step == 90000
    assert progress.mean_reward == 12.25
    assert progress.zombies == 16.0
    assert (progress.runner_weight, progress.brute_weight, progress.spitter_weight) == (0.5, 0.35, 0.25)
    assert progress.steps == [60000, 90000]
    assert progress.rewards == [-0.5, 12.25]
    assert progress.summaries == 3


def test_progress_forgets_points_after_a_resume_rewinds():
    progress = train_service.Progress("Warrior")
    for step in (30000, 60000, 90000):
        progress.feed(summary(step, step / 1000))

    progress.feed(summary(60000, 1.0))

    assert progress.steps == [30000, 60000]
    assert progress.rewards == [30.0, 1.0]


def test_progress_history_is_thinned_but_keeps_the_newest_point():
    progress = train_service.Progress("Warrior")
    for index in range(1, train_service.HISTORY_LIMIT + 2):
        progress.feed(summary(index * 1000, float(index)))

    assert len(progress.steps) <= train_service.HISTORY_LIMIT
    assert progress.steps[-1] == (train_service.HISTORY_LIMIT + 1) * 1000


def test_log_tail_returns_complete_lines_only(tmp_path: Path):
    log = tmp_path / "run.log"
    tail = train_service.LogTail(log)
    assert tail.read_lines() == []

    log.write_bytes(b"first\r\nsec")
    assert tail.read_lines() == ["first"]
    with log.open("ab") as handle:
        handle.write(b"ond\nthird\n")
    assert tail.read_lines() == ["second", "third"]


def make_checkpoint(
    runs: Path,
    run_id: str,
    step: int,
    resumable: bool = True,
    schema_version: int | None = arena_trainer.SCHEMA_VERSION,
    behavior: str = "Warrior",
) -> None:
    behavior_dir = runs / run_id / behavior
    behavior_dir.mkdir(parents=True, exist_ok=True)
    (behavior_dir / f"{behavior}-{step}.pt").write_bytes(b"x")
    if resumable:
        (behavior_dir / "checkpoint.pt").write_bytes(b"x")
    if schema_version is not None:
        (behavior_dir.parent / arena_trainer.SCHEMA_FILE).write_text(
            str(schema_version), encoding="utf-8"
        )


def test_plan_run_starts_warrior_s001_when_nothing_exists(tmp_path: Path):
    plan = train_service.plan_run(tmp_path, "Warrior")

    assert (plan.run_id, plan.mode, plan.last_step) == ("warrior-s001", "new", 0)


def test_plan_run_starts_mage_s001_when_nothing_exists(tmp_path: Path):
    plan = train_service.plan_run(tmp_path, "Mage")

    assert (plan.run_id, plan.mode, plan.last_step) == ("mage-s001", "new", 0)


def test_plan_run_resumes_the_newest_run(tmp_path: Path):
    make_checkpoint(tmp_path, "warrior-s001", 7499948)

    plan = train_service.plan_run(tmp_path, "Warrior")

    assert (plan.run_id, plan.mode, plan.last_step) == ("warrior-s001", "resume", 7499948)


def test_plan_run_numbers_only_survivor_runs(tmp_path: Path):
    make_checkpoint(tmp_path, "warrior-s001", 100, resumable=False)
    (tmp_path / "warrior-s002").mkdir()
    (tmp_path / "warrior-011").mkdir()
    (tmp_path / "warrior-s1000").mkdir()
    (tmp_path / "warrior-other").mkdir()

    plan = train_service.plan_run(tmp_path, "Warrior")

    assert (plan.run_id, plan.mode, plan.last_step) == ("warrior-s003", "new", 0)


def test_plan_run_ignores_champions_directory(tmp_path: Path):
    fake = tmp_path / "champions" / "Warrior"
    fake.mkdir(parents=True)
    (fake / "checkpoint.pt").write_bytes(b"x")
    (fake / "Warrior-999.pt").write_bytes(b"x")
    arena_trainer.write_schema_version(fake.parent)

    assert train_service.plan_run(tmp_path, "Warrior").run_id == "warrior-s001"


@pytest.mark.parametrize("schema_version", [None, 3])
def test_plan_run_does_not_resume_an_unmarked_or_older_schema_run(
    tmp_path: Path, schema_version: int | None
):
    make_checkpoint(tmp_path, "warrior-011", 100, schema_version=schema_version)

    plan = train_service.plan_run(tmp_path, "Warrior")

    assert arena_trainer.SCHEMA_VERSION == 4
    assert (plan.run_id, plan.mode, plan.last_step) == ("warrior-s001", "new", 0)


def test_plan_run_uses_the_newest_current_schema_run(tmp_path: Path):
    make_checkpoint(tmp_path, "warrior-s001", 200, schema_version=3)
    make_checkpoint(tmp_path, "warrior-s002", 100)
    import os

    os.utime(tmp_path / "warrior-s002" / "Warrior" / "Warrior-100.pt", (1, 1))
    os.utime(tmp_path / "warrior-s001" / "Warrior" / "Warrior-200.pt", (2, 2))

    plan = train_service.plan_run(tmp_path, "Warrior")

    assert (plan.run_id, plan.mode, plan.last_step) == ("warrior-s002", "resume", 100)


def write_fake_schema(directory: Path, version: int) -> None:
    schema = {
        "schema_version": version,
        "observation": [{"name": "self", "repeat": 1, "fields": ["value"]}],
        "actions": [{"name": "move", "size": 1}],
    }
    (directory / f"survivor_v{version}.json").write_text(json.dumps(schema), encoding="utf-8")


def test_plan_run_upgrades_the_newest_older_schema(tmp_path: Path, monkeypatch):
    schema_dir = tmp_path / "schemas"
    schema_dir.mkdir()
    write_fake_schema(schema_dir, 4)
    write_fake_schema(schema_dir, 5)
    monkeypatch.setattr(arena_trainer, "SCHEMAS_DIR", schema_dir)
    monkeypatch.setattr(arena_trainer, "SCHEMA_VERSION", 5)
    make_checkpoint(tmp_path, "warrior-s001", 800, schema_version=4)
    calls = []

    def fake_upgrade(runs_dir, source_run, behavior, new_run, old_version, new_version):
        calls.append((source_run, behavior, new_run, old_version, new_version))
        make_checkpoint(runs_dir, new_run, 800, schema_version=5, behavior=behavior)
        return runs_dir / new_run / behavior

    monkeypatch.setattr(train_service.brain_upgrade, "upgrade_run", fake_upgrade)

    plan = train_service.plan_run(tmp_path, "Warrior")

    assert calls == [("warrior-s001", "Warrior", "warrior-s002", 4, 5)]
    assert (plan.run_id, plan.mode, plan.last_step) == ("warrior-s002", "resume", 800)
    assert plan.message == (
        "Não AI được nâng cấp lên luật mới (schema v4 → v5) "
        "và học tiếp từ bước 800."
    )


def test_plan_run_missing_old_schema_falls_back_to_new(tmp_path: Path, monkeypatch):
    schema_dir = tmp_path / "schemas"
    schema_dir.mkdir()
    write_fake_schema(schema_dir, 5)
    monkeypatch.setattr(arena_trainer, "SCHEMAS_DIR", schema_dir)
    monkeypatch.setattr(arena_trainer, "SCHEMA_VERSION", 5)
    make_checkpoint(tmp_path, "warrior-s001", 800, schema_version=4)

    plan = train_service.plan_run(tmp_path, "Warrior")

    assert (plan.run_id, plan.mode, plan.last_step) == ("warrior-s002", "new", 0)
    assert "thiếu schema v4" in plan.message
    assert "học lại từ đầu" in plan.message


def test_plan_run_failed_upgrade_falls_back_to_new(tmp_path: Path, monkeypatch):
    schema_dir = tmp_path / "schemas"
    schema_dir.mkdir()
    write_fake_schema(schema_dir, 4)
    write_fake_schema(schema_dir, 5)
    monkeypatch.setattr(arena_trainer, "SCHEMAS_DIR", schema_dir)
    monkeypatch.setattr(arena_trainer, "SCHEMA_VERSION", 5)
    make_checkpoint(tmp_path, "warrior-s001", 800, schema_version=4)

    def failing_upgrade(*_args, **_kwargs):
        raise OSError("disk full")

    monkeypatch.setattr(train_service.brain_upgrade, "upgrade_run", failing_upgrade)

    plan = train_service.plan_run(tmp_path, "Warrior")

    assert (plan.run_id, plan.mode, plan.last_step) == ("warrior-s002", "new", 0)
    assert "Không thể nâng cấp" in plan.message
    assert "disk full" in plan.message


def test_plan_run_ignores_hidden_partial_runs(tmp_path: Path):
    make_checkpoint(tmp_path, ".warrior-s002.partial", 900)

    plan = train_service.plan_run(tmp_path, "Warrior")

    assert (plan.run_id, plan.mode, plan.last_step) == ("warrior-s001", "new", 0)


def test_plan_run_current_schema_wins_without_upgrade(tmp_path: Path, monkeypatch):
    schema_dir = tmp_path / "schemas"
    schema_dir.mkdir()
    write_fake_schema(schema_dir, 4)
    write_fake_schema(schema_dir, 5)
    monkeypatch.setattr(arena_trainer, "SCHEMAS_DIR", schema_dir)
    monkeypatch.setattr(arena_trainer, "SCHEMA_VERSION", 5)
    make_checkpoint(tmp_path, "warrior-s001", 900, schema_version=4)
    make_checkpoint(tmp_path, "warrior-s002", 100, schema_version=5)

    def unexpected(*_args, **_kwargs):
        raise AssertionError("upgrade should not run")

    monkeypatch.setattr(train_service.brain_upgrade, "upgrade_run", unexpected)

    plan = train_service.plan_run(tmp_path, "Warrior")

    assert (plan.run_id, plan.mode, plan.last_step) == ("warrior-s002", "resume", 100)
    assert plan.message == ""


def test_plan_run_never_upgrades_from_champions(tmp_path: Path, monkeypatch):
    schema_dir = tmp_path / "schemas"
    schema_dir.mkdir()
    write_fake_schema(schema_dir, 4)
    write_fake_schema(schema_dir, 5)
    monkeypatch.setattr(arena_trainer, "SCHEMAS_DIR", schema_dir)
    monkeypatch.setattr(arena_trainer, "SCHEMA_VERSION", 5)
    fake = tmp_path / "champions" / "Warrior"
    fake.mkdir(parents=True)
    (fake / "checkpoint.pt").write_bytes(b"x")
    (fake / "Warrior-999.pt").write_bytes(b"x")
    arena_trainer.write_schema_version(fake.parent, 4)

    def unexpected(*_args, **_kwargs):
        raise AssertionError("champion must not be an upgrade source")

    monkeypatch.setattr(train_service.brain_upgrade, "upgrade_run", unexpected)

    plan = train_service.plan_run(tmp_path, "Warrior")

    assert (plan.run_id, plan.mode, plan.last_step) == ("warrior-s001", "new", 0)


def test_plan_run_does_not_resume_an_explicit_incompatible_run(tmp_path: Path):
    make_checkpoint(tmp_path, "warrior-011", 100, schema_version=3)

    plan = train_service.plan_run(tmp_path, "Warrior", "warrior-011")

    assert (plan.run_id, plan.mode, plan.last_step) == ("warrior-s001", "new", 0)


def test_plan_run_forces_a_folder_without_checkpoint(tmp_path: Path):
    (tmp_path / "broken").mkdir()

    plan = train_service.plan_run(tmp_path, "Warrior", "broken")

    assert (plan.run_id, plan.mode) == ("broken", "force")


def write_config(path: Path) -> None:
    path.write_text(
        "behaviors:\n  Warrior:\n    trainer_type: ppo\n    max_steps: 3.0e7\n"
        "environment_parameters:\n  arena_size: 20.0\n",
        encoding="utf-8",
    )


@pytest.mark.parametrize(
    ("mode", "initial_marker", "expected_marker"),
    [("new", None, "4"), ("force", "1", "4"), ("resume", "4", "4")],
)
def test_service_marks_new_and_forced_runs_without_overwriting_resumed_runs(
    tmp_path: Path, mode: str, initial_marker: str | None, expected_marker: str
):
    config = tmp_path / "warrior.yaml"
    write_config(config)
    runs = tmp_path / "runs"
    run_id = f"warrior-s{mode}"
    run_dir = runs / run_id
    if mode == "force":
        run_dir.mkdir(parents=True)
    elif mode == "resume":
        make_checkpoint(runs, run_id, 100)
    if initial_marker is not None:
        run_dir.mkdir(parents=True, exist_ok=True)
        (run_dir / arena_trainer.SCHEMA_FILE).write_text(initial_marker, encoding="utf-8")

    fake_exe = tmp_path / "fake.exe"
    fake_exe.write_bytes(b"")
    args = train_service.create_parser().parse_args([
        "--run-id", run_id, "--results-dir", str(runs), "--config", str(config)
    ])
    service = train_service.TrainingService(args)
    seen_markers: list[str] = []
    service.trainer_command = lambda plan, path: [str(fake_exe), "--env", str(fake_exe)]
    service._train = lambda command, tail: (
        seen_markers.append((run_dir / arena_trainer.SCHEMA_FILE).read_text(encoding="utf-8")) or 0,
        True,
        1.0,
    )
    service.export = lambda: None
    service.write_history = lambda: None

    assert service.run() == 0

    assert seen_markers == [expected_marker]
    assert (run_dir / arena_trainer.SCHEMA_FILE).read_text(encoding="utf-8") == expected_marker


def test_effective_config_keeps_the_original_budget(tmp_path: Path):
    config = tmp_path / "warrior.yaml"
    write_config(config)

    path, max_steps = train_service.effective_config(config, "Warrior", 1_000_000, tmp_path / "out.yaml")

    assert path == config
    assert max_steps == 30_000_000
    assert not (tmp_path / "out.yaml").exists()


def test_effective_config_extends_a_used_up_budget(tmp_path: Path):
    import yaml

    config = tmp_path / "warrior.yaml"
    write_config(config)

    path, max_steps = train_service.effective_config(config, "Warrior", 29_990_000, tmp_path / "out.yaml")

    assert path == tmp_path / "out.yaml"
    assert max_steps == 59_990_000
    written = yaml.safe_load(path.read_text(encoding="utf-8"))
    assert written["behaviors"]["Warrior"]["max_steps"] == 59_990_000
    assert written["environment_parameters"]["arena_size"] == 20.0


OWNER_BUILD = "5,0,2,0,3,0,0,0,0,0,0,0,0,0,0,20"


def test_effective_config_trains_on_the_owner_build_without_extending(tmp_path: Path):
    import yaml

    config = tmp_path / "warrior.yaml"
    write_config(config)
    args = train_service.create_parser().parse_args(["--results-dir", str(tmp_path), "--owner-build", OWNER_BUILD])

    path, max_steps = train_service.effective_config(
        config, "Warrior", 1_000_000, tmp_path / "out.yaml", train_service.environment_overrides(args)
    )

    assert path == tmp_path / "out.yaml"
    assert max_steps == 30_000_000
    written = yaml.safe_load(path.read_text(encoding="utf-8"))
    assert written["environment_parameters"]["own_build_share"] == arena_trainer.OWNER_BUILD_SHARE
    assert written["environment_parameters"]["arena_size"] == 20.0
    assert written["behaviors"]["Warrior"]["max_steps"] == 30_000_000


def test_effective_config_overrides_and_extends_together(tmp_path: Path):
    import yaml

    config = tmp_path / "warrior.yaml"
    write_config(config)

    path, max_steps = train_service.effective_config(
        config, "Warrior", 29_990_000, tmp_path / "out.yaml", {"own_build_share": 0.7}
    )

    written = yaml.safe_load(path.read_text(encoding="utf-8"))
    assert max_steps == 59_990_000
    assert written["behaviors"]["Warrior"]["max_steps"] == 59_990_000
    assert written["environment_parameters"]["own_build_share"] == 0.7


def test_no_owner_build_means_no_environment_overrides(tmp_path: Path):
    args = train_service.create_parser().parse_args(["--results-dir", str(tmp_path), "--training-focus", "boss"])

    assert train_service.environment_overrides(args) == {}


def test_trainer_command_passes_the_owner_settings(tmp_path: Path):
    args = train_service.create_parser().parse_args([
        "--results-dir", str(tmp_path), "--owner-build", OWNER_BUILD, "--owner-tier", "3",
        "--training-focus", "survival",
    ])
    service = train_service.TrainingService(args)

    command = service.trainer_command(train_service.RunPlan("warrior-s001", "resume", 5), tmp_path / "c.yaml")

    assert command[-9:] == [
        "--env-args", "--hero-class", "warrior", "--owner-build", OWNER_BUILD, "--owner-tier", "3",
        "--training-focus", "survival",
    ]
    status = service.status("training")
    assert (status["owner_build"], status["owner_tier"], status["training_focus"]) == (True, 3, "survival")


def test_status_defaults_to_a_balanced_focus_without_an_owner_build(tmp_path: Path):
    args = train_service.create_parser().parse_args(["--results-dir", str(tmp_path)])
    status = train_service.TrainingService(args).status("training")

    assert (status["owner_build"], status["owner_tier"], status["training_focus"]) == (False, 0, "balanced")


def test_trainer_command_defaults_the_owner_tier_to_one(tmp_path: Path):
    args = train_service.create_parser().parse_args(["--results-dir", str(tmp_path), "--owner-build", OWNER_BUILD])
    service = train_service.TrainingService(args)

    command = service.trainer_command(train_service.RunPlan("warrior-s001", "resume", 5), tmp_path / "c.yaml")

    assert command[-4:] == ["--owner-build", OWNER_BUILD, "--owner-tier", "1"]


def test_trainer_command_passes_a_focus_without_a_build(tmp_path: Path):
    args = train_service.create_parser().parse_args(["--results-dir", str(tmp_path), "--training-focus", "gold"])
    service = train_service.TrainingService(args)

    command = service.trainer_command(train_service.RunPlan("warrior-s001", "resume", 5), tmp_path / "c.yaml")

    assert command[-5:] == ["--env-args", "--hero-class", "warrior", "--training-focus", "gold"]
    assert "--owner-build" not in command


def test_effective_config_skips_writing_an_override_that_is_already_set(tmp_path: Path):
    import yaml

    config = tmp_path / "warrior.yaml"
    write_config(config)
    data = yaml.safe_load(config.read_text(encoding="utf-8"))
    data["environment_parameters"]["own_build_share"] = 0.7
    config.write_text(yaml.safe_dump(data, sort_keys=False), encoding="utf-8")

    path, _ = train_service.effective_config(
        config, "Warrior", 1_000_000, tmp_path / "out.yaml", {"own_build_share": 0.7}
    )

    assert path == config
    assert not (tmp_path / "out.yaml").exists()


def test_effective_config_refuses_to_override_a_curriculum(tmp_path: Path):
    import yaml

    config = tmp_path / "warrior.yaml"
    write_config(config)
    data = yaml.safe_load(config.read_text(encoding="utf-8"))
    data["environment_parameters"]["own_build_share"] = {"curriculum": [{"name": "a", "value": 0.0}]}
    config.write_text(yaml.safe_dump(data, sort_keys=False), encoding="utf-8")

    with pytest.raises(ValueError):
        train_service.effective_config(config, "Warrior", 1_000_000, tmp_path / "out.yaml", {"own_build_share": 0.7})


def test_trainer_command_resumes_with_service_settings(tmp_path: Path):
    args = train_service.create_parser().parse_args(["--results-dir", str(tmp_path), "--base-port", "5105"])
    service = train_service.TrainingService(args)

    command = service.trainer_command(train_service.RunPlan("warrior-s001", "resume", 5), tmp_path / "c.yaml")

    assert command[1] == str((tmp_path / "c.yaml").resolve())
    assert command[command.index("--run-id") + 1] == "warrior-s001"
    assert command[command.index("--base-port") + 1] == "5105"
    assert command[command.index("--results-dir") + 1] == str(tmp_path.resolve())
    assert command[command.index("--time-scale") + 1] == "20.0"
    assert "--resume" in command
    assert command[-3:] == ["--env-args", "--hero-class", "warrior"]


def test_trainer_command_forces_new_runs_because_the_schema_marker_made_the_folder(tmp_path: Path):
    args = train_service.create_parser().parse_args(["--results-dir", str(tmp_path)])
    service = train_service.TrainingService(args)

    command = service.trainer_command(train_service.RunPlan("warrior-s001", "new", 0), tmp_path / "c.yaml")

    assert "--force" in command
    assert "--resume" not in command


def test_trainer_command_uses_the_viewer_power_setting(tmp_path: Path):
    args = train_service.create_parser().parse_args(
        ["--results-dir", str(tmp_path), "--num-envs", "8", "--arena-agents", "32", "--time-scale", "30"]
    )
    service = train_service.TrainingService(args)

    command = service.trainer_command(train_service.RunPlan("warrior-s001", "resume", 5), tmp_path / "c.yaml")

    assert command[command.index("--num-envs") + 1] == "8"
    assert command[command.index("--time-scale") + 1] == "30.0"
    assert command[-5:] == [
        "--env-args", "--arena-agents", "32", "--hero-class", "warrior"
    ]
    status = service.status("training")
    assert (status["num_envs"], status["arena_agents"]) == (8, 32)


def test_status_payload_matches_the_viewer_fields(tmp_path: Path):
    args = train_service.create_parser().parse_args(["--results-dir", str(tmp_path)])
    service = train_service.TrainingService(args, clock=lambda: 100.0)
    service.run_id = "warrior-s001"
    service.progress.feed(summary(60000, 4.5))

    service.publish("training", "The AI is training.")

    status = json.loads((tmp_path / train_service.STATUS_NAME).read_text(encoding="utf-8"))
    assert status["state"] == "training"
    assert status["run_id"] == "warrior-s001"
    assert status["step"] == 60000
    assert status["has_reward"] is True
    assert status["mean_reward"] == 4.5
    assert status["behavior"] == "Warrior"
    assert status["zombie_mix"] == {
        "walker": 1.0, "runner": 0.0, "brute": 0.0, "spitter": 0.0
    }
    assert status["steps"] == [60000]
    assert status["updated_unix"] == 100.0
    assert status["champion_step"] is None
    assert status["last_eval_step"] is None
    assert status["last_eval_won"] is None
    assert status["evaluating"] is False


def test_service_evaluation_trigger_is_two_million_and_one_at_a_time(tmp_path: Path, monkeypatch):
    args = train_service.create_parser().parse_args(["--results-dir", str(tmp_path)])
    service = train_service.TrainingService(args)
    service.run_id = "warrior-s001"
    make_checkpoint(tmp_path, service.run_id, 1_999_999)
    assert not service.maybe_start_evaluation()

    make_checkpoint(tmp_path, service.run_id, 2_000_000)
    started = threading.Event()
    release = threading.Event()

    def fake_evaluate(*args, **kwargs):
        started.set()
        release.wait(2)
        return {"run_id": service.run_id, "step": 2_000_000, "won": True}

    monkeypatch.setattr(train_service.champion, "evaluate_latest", fake_evaluate)
    assert service.maybe_start_evaluation()
    assert started.wait(2)
    assert not service.maybe_start_evaluation()
    release.set()
    service.evaluation_thread.join(2)
    assert service.last_eval_step == 2_000_000
    assert service.last_eval_won is True


def test_service_cancels_running_evaluation(tmp_path: Path):
    args = train_service.create_parser().parse_args(["--results-dir", str(tmp_path)])
    service = train_service.TrainingService(args)

    class FakeRunner:
        def __init__(self):
            self.cancelled = False

        def cancel(self):
            self.cancelled = True

    runner = FakeRunner()
    service.evaluation_runner = runner
    service.cancel_evaluation()
    assert runner.cancelled


@pytest.mark.parametrize(
    ("behavior", "expected", "config"),
    [
        ("warrior", "Warrior", "warrior_survivor_ppo.yaml"),
        ("MAGE", "Mage", "mage_survivor_ppo.yaml"),
        ("Archer", "Archer", "archer_survivor_ppo.yaml"),
    ],
)
def test_parser_normalizes_behavior_and_chooses_its_default_config(
    behavior: str, expected: str, config: str
):
    args = train_service.create_parser().parse_args(["--behavior", behavior])

    assert args.behavior == expected
    assert Path(args.config) == Path("Trainer/config") / config


def test_only_the_warrior_has_a_survivor_config_for_now():
    root = arena_trainer.repository_root()

    assert (root / train_service.default_config("Warrior")).is_file()
    for behavior in train_service.FUTURE_BEHAVIORS:
        assert not (root / train_service.default_config(behavior)).exists()


@pytest.mark.parametrize("behavior", ["Mage", "Archer"])
def test_mage_and_archer_report_a_clear_error_without_a_run_folder(
    tmp_path: Path, capsys, behavior: str
):
    args = train_service.create_parser().parse_args(
        ["--behavior", behavior, "--results-dir", str(tmp_path)]
    )

    assert train_service.TrainingService(args).run() == 2

    status = read_status(tmp_path)
    assert status["state"] == "error"
    assert status["behavior"] == behavior
    assert "arrives in M7" in status["message"]
    assert "Only the Warrior can train" in status["message"]
    assert "arrives in M7" in capsys.readouterr().err
    assert not (tmp_path / train_service.ERROR_LOG_NAME).exists()
    assert not (tmp_path / f"{behavior.lower()}-s001").exists()


def test_a_missing_custom_config_is_reported_by_name(tmp_path: Path):
    args = train_service.create_parser().parse_args(
        ["--results-dir", str(tmp_path), "--config", str(tmp_path / "gone.yaml")]
    )

    assert train_service.TrainingService(args).run() == 2

    status = read_status(tmp_path)
    assert status["state"] == "error"
    assert status["message"] == "Trainer config not found: gone.yaml."
    assert list(tmp_path.glob("warrior-s*")) == []


def test_a_second_service_cannot_take_the_lock(tmp_path: Path):
    first = train_service.acquire_lock(tmp_path / "lock")
    try:
        assert first is not None
        assert train_service.acquire_lock(tmp_path / "lock") is None
    finally:
        first.close()
    second = train_service.acquire_lock(tmp_path / "lock")
    assert second is not None
    second.close()


def test_a_new_service_waits_for_the_old_one_to_exit(tmp_path: Path):
    first = train_service.acquire_lock(tmp_path / "lock")
    timer = threading.Timer(0.5, first.close)
    timer.start()
    try:
        second = train_service.acquire_lock(tmp_path / "lock", wait_seconds=5.0)
        assert second is not None
        second.close()
    finally:
        timer.cancel()
        first.close()


def test_stop_file_and_missing_parent_request_a_stop(tmp_path: Path):
    args = argparse.Namespace(
        results_dir=str(tmp_path), behavior="Warrior", parent_pid=0, run_id=None,
        config="c.yaml", num_envs=1, base_port=5005,
    )
    service = train_service.TrainingService(args)
    assert not service.stop_requested()

    (tmp_path / train_service.STOP_NAME).write_text("")
    assert service.stop_requested()

    (tmp_path / train_service.STOP_NAME).unlink()
    args.parent_pid = 2**31 - 2  # No such process.
    assert service.stop_requested()


def test_process_alive_sees_this_process():
    import os

    assert train_service.process_alive(os.getpid())
    assert not train_service.process_alive(0)


CRASH = 0xC0000409  # What the GPU driver crash looked like on the owner's PC.


def scripted_service(tmp_path: Path, monkeypatch, outcomes: list[tuple[int, bool, float]]):
    """A service whose ML-Agents runs return the scripted (exit code, stopped, seconds)."""
    config = tmp_path / "warrior.yaml"
    write_config(config)
    runs = tmp_path / "runs"
    make_checkpoint(runs, "warrior-s001", 100)
    fake_exe = tmp_path / "fake.exe"
    fake_exe.write_bytes(b"")
    monkeypatch.setattr(train_service, "RETRY_DELAY", 0.0)

    args = train_service.create_parser().parse_args(["--results-dir", str(runs), "--config", str(config)])
    service = train_service.TrainingService(args)
    devices: list[str | None] = []

    def fake_command(plan, config_path):
        devices.append(service.torch_device)
        return [str(fake_exe), "--env", str(fake_exe)]

    def fake_train(command, tail):
        outcome = outcomes.pop(0) if outcomes else (CRASH, False, 5.0)
        if outcome[1]:
            (runs / train_service.STOP_NAME).write_text("")
        return outcome

    service.trainer_command = fake_command
    service._train = fake_train
    service.export = lambda: None
    return service, runs, devices


def read_status(runs: Path) -> dict:
    return json.loads((runs / train_service.STATUS_NAME).read_text(encoding="utf-8"))


def test_a_crashed_trainer_is_resumed_and_falls_back_to_the_cpu(tmp_path: Path, monkeypatch):
    service, runs, devices = scripted_service(
        tmp_path, monkeypatch, [(CRASH, False, 8.0), (CRASH, False, 8.0), (0, True, 30.0)]
    )

    assert service.run() == 0

    assert devices == [None, None, "cpu"]
    assert read_status(runs)["state"] == "stopped"
    log = (runs / "warrior-s001.log").read_text(encoding="utf-8")
    assert "0xC0000409" in log
    assert "restarting from the last checkpoint (attempt 1 of 5)" in log
    assert "training on the CPU" in log


def test_a_trainer_that_keeps_crashing_ends_in_an_error(tmp_path: Path, monkeypatch):
    service, runs, devices = scripted_service(tmp_path, monkeypatch, [])

    assert service.run() == 1

    assert len(devices) == train_service.CRASH_RETRY_LIMIT + 1
    status = read_status(runs)
    assert status["state"] == "error"
    assert "keeps crashing" in status["message"]


def test_a_long_healthy_run_resets_the_crash_count(tmp_path: Path, monkeypatch):
    long_run = train_service.STABLE_RUN_SECONDS + 1
    service, runs, devices = scripted_service(
        tmp_path, monkeypatch, [(CRASH, False, 8.0), (CRASH, False, long_run), (0, True, 30.0)]
    )

    assert service.run() == 0

    assert devices == [None, None, None]


def test_service_writes_history_once_when_the_session_ends(tmp_path: Path, monkeypatch):
    service, runs, _ = scripted_service(tmp_path, monkeypatch, [(0, True, 30.0)])
    written: list[Path] = []
    service.write_history = lambda: written.append(runs / service.run_id)

    assert service.run() == 0

    assert written == [runs / "warrior-s001"]


def test_stop_during_a_restart_wait_saves_and_stops(tmp_path: Path, monkeypatch):
    service, runs, devices = scripted_service(tmp_path, monkeypatch, [(CRASH, False, 8.0)])
    monkeypatch.setattr(train_service, "RETRY_DELAY", 5.0)
    real_train = service._train

    def crash_after_stop_file(command, tail):
        (runs / train_service.STOP_NAME).write_text("")
        return real_train(command, tail)

    service._train = crash_after_stop_file

    assert service.run() == 0

    assert len(devices) == 1
    assert read_status(runs)["state"] == "stopped"
    assert not (runs / train_service.STOP_NAME).exists()


def test_a_service_error_is_written_where_the_viewer_can_show_it(tmp_path: Path, monkeypatch):
    def broken_plan(*_args, **_kwargs):
        raise RuntimeError("disk on fire")

    monkeypatch.setattr(train_service, "plan_run", broken_plan)
    args = train_service.create_parser().parse_args(["--results-dir", str(tmp_path)])

    assert train_service.TrainingService(args).run() == 1

    status = read_status(tmp_path)
    assert status["state"] == "error"
    assert "disk on fire" in status["message"]
    assert "disk on fire" in (tmp_path / train_service.ERROR_LOG_NAME).read_text(encoding="utf-8")


def _write_build(folder: Path, marker: str) -> None:
    folder.mkdir(parents=True)
    (folder / "PersonalArenaTraining.exe").write_text(marker, encoding="utf-8")


def test_install_staged_build_swaps_in_new_build(tmp_path: Path):
    _write_build(tmp_path / "Build" / "Training", "old")
    _write_build(tmp_path / "Build" / "TrainingNext", "new")

    message = _install_staged_build(tmp_path)

    assert "installed" in message
    assert (tmp_path / "Build" / "Training" / "PersonalArenaTraining.exe").read_text(encoding="utf-8") == "new"
    assert not (tmp_path / "Build" / "TrainingNext").exists()
    assert not (tmp_path / "Build" / ".Training.old").exists()


def test_install_staged_build_without_staged_build_does_nothing(tmp_path: Path):
    _write_build(tmp_path / "Build" / "Training", "old")

    assert _install_staged_build(tmp_path) == ""
    assert (tmp_path / "Build" / "Training" / "PersonalArenaTraining.exe").read_text(encoding="utf-8") == "old"


def test_install_staged_build_keeps_current_build_on_error(tmp_path: Path, monkeypatch):
    _write_build(tmp_path / "Build" / "Training", "old")
    _write_build(tmp_path / "Build" / "TrainingNext", "new")
    real_rename = Path.rename

    def rename(self, target):
        if self.name == "TrainingNext":
            raise PermissionError("locked")
        return real_rename(self, target)

    monkeypatch.setattr(Path, "rename", rename)

    message = _install_staged_build(tmp_path)

    assert "could not install" in message
    assert (tmp_path / "Build" / "Training" / "PersonalArenaTraining.exe").read_text(encoding="utf-8") == "old"
    assert (tmp_path / "Build" / "TrainingNext" / "PersonalArenaTraining.exe").is_file()
