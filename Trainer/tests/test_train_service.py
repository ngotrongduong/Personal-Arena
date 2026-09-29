import argparse
import json
import threading
from pathlib import Path

from Trainer import train_service


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


def make_checkpoint(runs: Path, run_id: str, step: int, resumable: bool = True) -> None:
    behavior = runs / run_id / "Warrior"
    behavior.mkdir(parents=True, exist_ok=True)
    (behavior / f"Warrior-{step}.pt").write_bytes(b"x")
    if resumable:
        (behavior / "checkpoint.pt").write_bytes(b"x")


def test_plan_run_starts_warrior_001_when_nothing_exists(tmp_path: Path):
    plan = train_service.plan_run(tmp_path, "Warrior")

    assert (plan.run_id, plan.mode, plan.last_step) == ("warrior-001", "new", 0)


def test_plan_run_resumes_the_newest_run(tmp_path: Path):
    make_checkpoint(tmp_path, "warrior-001", 7499948)

    plan = train_service.plan_run(tmp_path, "Warrior")

    assert (plan.run_id, plan.mode, plan.last_step) == ("warrior-001", "resume", 7499948)


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


def test_trainer_command_resumes_with_service_settings(tmp_path: Path):
    args = train_service.create_parser().parse_args(["--results-dir", str(tmp_path), "--base-port", "5105"])
    service = train_service.TrainingService(args)

    command = service.trainer_command(train_service.RunPlan("warrior-001", "resume", 5), tmp_path / "c.yaml")

    assert command[1] == str((tmp_path / "c.yaml").resolve())
    assert command[command.index("--run-id") + 1] == "warrior-001"
    assert command[command.index("--base-port") + 1] == "5105"
    assert command[command.index("--results-dir") + 1] == str(tmp_path.resolve())
    assert command[command.index("--time-scale") + 1] == "20.0"
    assert command[-1] == "--resume"


def test_trainer_command_uses_the_viewer_power_setting(tmp_path: Path):
    args = train_service.create_parser().parse_args(
        ["--results-dir", str(tmp_path), "--num-envs", "8", "--arena-agents", "32", "--time-scale", "30"]
    )
    service = train_service.TrainingService(args)

    command = service.trainer_command(train_service.RunPlan("warrior-001", "resume", 5), tmp_path / "c.yaml")

    assert command[command.index("--num-envs") + 1] == "8"
    assert command[command.index("--time-scale") + 1] == "30.0"
    assert command[-3:] == ["--env-args", "--arena-agents", "32"]
    status = service.status("training")
    assert (status["num_envs"], status["arena_agents"]) == (8, 32)


def test_status_payload_matches_the_viewer_fields(tmp_path: Path):
    args = train_service.create_parser().parse_args(["--results-dir", str(tmp_path)])
    service = train_service.TrainingService(args, clock=lambda: 100.0)
    service.run_id = "warrior-001"
    service.progress.feed(summary(60000, 4.5))

    service.publish("training", "The AI is training.")

    status = json.loads((tmp_path / train_service.STATUS_NAME).read_text(encoding="utf-8"))
    assert status["state"] == "training"
    assert status["run_id"] == "warrior-001"
    assert status["step"] == 60000
    assert status["has_reward"] is True
    assert status["mean_reward"] == 4.5
    assert status["steps"] == [60000]
    assert status["updated_unix"] == 100.0


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
    make_checkpoint(runs, "warrior-001", 100)
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
    log = (runs / "warrior-001.log").read_text(encoding="utf-8")
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
