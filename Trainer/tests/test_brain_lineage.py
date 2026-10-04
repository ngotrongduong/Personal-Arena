from __future__ import annotations

import json
import os
from pathlib import Path

import pytest

from Trainer import arena_trainer, brain_lineage, champion, export_brain, train_service

BEHAVIOR = "Warrior"


def evaluation(median=600.0, p10=420.0, wins=0.5) -> dict:
    summary = {
        "Runs": 100,
        "MedianSurvivedSeconds": median,
        "P10SurvivedSeconds": p10,
        "MeanSurvivedSeconds": median,
        "WinRate": wins,
        "CatastrophicCount": 0,
        "Behavior": {"Aggression": 0.4},
    }
    return {"summary": summary, "passes_m4a": median >= 600, "settings": champion.evaluation_settings()}


def make_run(runs: Path, run_id: str, steps=(1_000_000,), schema=arena_trainer.SCHEMA_VERSION) -> Path:
    behavior_dir = runs / run_id / BEHAVIOR
    behavior_dir.mkdir(parents=True)
    for step in steps:
        (behavior_dir / f"{BEHAVIOR}-{step}.pt").write_bytes(f"pt-{run_id}-{step}".encode())
    (behavior_dir / "checkpoint.pt").write_bytes(b"latest")
    arena_trainer.write_schema_version(runs / run_id, schema)
    logs = runs / run_id / "run_logs"
    logs.mkdir()
    (logs / "training_status.json").write_text(
        json.dumps({"Warrior": {"checkpoints": [{"steps": 1}], "lesson": 3}}), encoding="utf-8"
    )
    return behavior_dir


def brain(tmp_path: Path, name: str) -> Path:
    path = tmp_path / name
    path.write_bytes(name.encode())
    return path


def record(runs: Path, tmp_path: Path, run_id: str, step: int, won: bool, median=600.0) -> Path:
    line = champion._evaluation_line(
        champion._metadata(run_id, step, evaluation(median=median), arena_trainer.SCHEMA_VERSION), won
    )
    checkpoint = runs / run_id / BEHAVIOR / f"{BEHAVIOR}-{step}.pt"
    return brain_lineage.record_evaluation(
        runs, BEHAVIOR, run_id, step, brain(tmp_path, f"{run_id}-{step}.b"), checkpoint, line,
        log=lambda _: None,
    )


def version(runs: Path, identifier: str) -> dict:
    path = brain_lineage.versions_directory(runs, BEHAVIOR) / identifier / "version.json"
    return json.loads(path.read_text(encoding="utf-8"))


def test_sync_imports_champion_history_once(tmp_path: Path):
    runs = tmp_path / "runs"
    make_run(runs, "warrior-s001", steps=(2_000_000,))
    champion.apply_evaluation(
        runs, BEHAVIOR, "warrior-s001", 2_000_000, brain(tmp_path, "first.brain"), evaluation(),
        log=lambda _: None,
    )

    assert brain_lineage.sync(runs, BEHAVIOR) == 1
    assert brain_lineage.sync(runs, BEHAVIOR) == 0
    data = version(runs, "warrior-s001-2000000")
    folder = brain_lineage.versions_directory(runs, BEHAVIOR) / "warrior-s001-2000000"
    assert data["source"] == "history"
    assert data["champion"] is True
    assert data["evaluated"] is True
    assert data["has_checkpoint"] is True
    assert (folder / "brain.brain").read_bytes() == b"first.brain"
    assert (folder / "checkpoint.pt").read_bytes() == b"pt-warrior-s001-2000000"


def test_record_keeps_the_losers_brain_and_status(tmp_path: Path):
    runs = tmp_path / "runs"
    make_run(runs, "warrior-s001", steps=(4_000_000,))
    target = record(runs, tmp_path, "warrior-s001", 4_000_000, won=False, median=590)

    data = version(runs, "warrior-s001-4000000")
    assert data["source"] == "eval"
    assert data["champion"] is False
    assert data["score"] == champion.calculate_score(evaluation(median=590)["summary"])
    assert (target / "brain.brain").read_bytes() == b"warrior-s001-4000000.b"
    status = json.loads((target / "training_status.json").read_text(encoding="utf-8"))
    assert "checkpoints" not in status["Warrior"]
    assert status["Warrior"]["lesson"] == 3


def test_record_never_raises(tmp_path: Path):
    messages = []
    result = brain_lineage.record_evaluation(
        tmp_path / "runs", BEHAVIOR, "warrior-s001", 1, tmp_path / "missing.brain", None,
        {"won": False}, log=messages.append,
    )
    assert result is None
    assert messages and "could not save" in messages[0]


def test_evaluate_latest_records_a_version_for_a_losing_candidate(tmp_path: Path, monkeypatch):
    runs = tmp_path / "runs"
    make_run(runs, "warrior-s001", steps=(2_000_000,))

    def export(checkpoint, output):
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_bytes(b"exported")

    monkeypatch.setattr(export_brain, "export_checkpoint", export)

    class Runner:
        def __init__(self, median):
            self.median = median

        def run(self, brain, output, log=print):
            return evaluation(median=self.median)

    champion.evaluate_latest(runs, BEHAVIOR, "warrior-s001", runner=Runner(700), log=lambda _: None)
    (runs / "warrior-s001" / BEHAVIOR / f"{BEHAVIOR}-4000000.pt").write_bytes(b"pt-4")
    champion.evaluate_latest(runs, BEHAVIOR, "warrior-s001", runner=Runner(100), log=lambda _: None)

    assert version(runs, "warrior-s001-2000000")["champion"] is True
    loser = version(runs, "warrior-s001-4000000")
    assert loser["champion"] is False
    assert loser["has_checkpoint"] is True
    candidates = champion.champion_directory(runs, BEHAVIOR) / "candidates"
    assert not (candidates / "warrior-s001-4000000.brain").exists()


def test_fork_creates_a_resumable_run_and_never_touches_the_source(tmp_path: Path):
    runs = tmp_path / "runs"
    make_run(runs, "warrior-s001", steps=(2_000_000, 4_000_000))
    record(runs, tmp_path, "warrior-s001", 2_000_000, won=True)
    before = {path: path.read_bytes() for path in (runs / "warrior-s001").rglob("*") if path.is_file()}

    result = brain_lineage.fork(runs, BEHAVIOR, version="warrior-s001-2000000", log=lambda _: None)

    assert result == {"ok": True, "run_id": "warrior-s002", "id": "warrior-s001-2000000", "step": 2_000_000}
    new_behavior = runs / "warrior-s002" / BEHAVIOR
    assert (new_behavior / f"{BEHAVIOR}-2000000.pt").read_bytes() == b"pt-warrior-s001-2000000"
    assert (new_behavior / "checkpoint.pt").read_bytes() == b"pt-warrior-s001-2000000"
    assert arena_trainer.run_schema_version(runs / "warrior-s002") == arena_trainer.SCHEMA_VERSION
    status = json.loads((runs / "warrior-s002" / "run_logs" / "training_status.json").read_text())
    assert "checkpoints" not in status["Warrior"]
    forked = json.loads((runs / "warrior-s002" / "forked_from.json").read_text(encoding="utf-8"))
    assert forked["source_version"] == "warrior-s001-2000000"
    after = {path: path.read_bytes() for path in (runs / "warrior-s001").rglob("*") if path.is_file()}
    assert after == before

    branches = brain_lineage.load_branches(runs, BEHAVIOR)
    assert branches[0]["run_id"] == "warrior-s002"
    assert branches[0]["parent_run"] == "warrior-s001"
    assert branches[0]["parent_step"] == 2_000_000
    plan = train_service.plan_run(runs, BEHAVIOR, "warrior-s002")
    assert plan.run_id == "warrior-s002"
    assert not list(runs.glob(".*partial"))


def test_fork_refuses_versions_it_cannot_train(tmp_path: Path):
    runs = tmp_path / "runs"
    make_run(runs, "warrior-s001")
    record(runs, tmp_path, "warrior-s001", 1_000_000, won=True)
    folder = brain_lineage.versions_directory(runs, BEHAVIOR) / "warrior-s001-1000000"

    (folder / "checkpoint.pt").unlink()
    with pytest.raises(brain_lineage.LineageError, match="Only the brain is left"):
        brain_lineage.fork(runs, BEHAVIOR, version="warrior-s001-1000000")

    (folder / "checkpoint.pt").write_bytes(b"x")
    data = version(runs, "warrior-s001-1000000")
    data["schema_version"] = 3
    (folder / "version.json").write_text(json.dumps(data), encoding="utf-8")
    with pytest.raises(brain_lineage.LineageError, match="old rules"):
        brain_lineage.fork(runs, BEHAVIOR, version="warrior-s001-1000000")

    with pytest.raises(brain_lineage.LineageError):
        brain_lineage.fork(runs, BEHAVIOR, version="nope")
    assert not (runs / "warrior-s002").exists()


def test_fork_leaves_no_partial_folder_on_failure(tmp_path: Path, monkeypatch):
    runs = tmp_path / "runs"
    make_run(runs, "warrior-s001")
    record(runs, tmp_path, "warrior-s001", 1_000_000, won=True)

    def broken(*args, **kwargs):
        raise OSError("disk full")

    monkeypatch.setattr(arena_trainer, "write_schema_version", broken)
    with pytest.raises(OSError):
        brain_lineage.fork(runs, BEHAVIOR, version="warrior-s001-1000000")
    assert not (runs / "warrior-s002").exists()
    assert not list(runs.glob(".*"))
    assert brain_lineage.load_branches(runs, BEHAVIOR) == []


def test_clone_by_run_id_snapshots_the_newest_settled_checkpoint(tmp_path: Path, monkeypatch):
    runs = tmp_path / "runs"
    behavior_dir = make_run(runs, "warrior-s001", steps=(1_000_000, 2_000_000, 3_000_000))
    now = 10_000.0
    for step, age in ((1_000_000, 300), (2_000_000, 60), (3_000_000, 5)):
        path = behavior_dir / f"{BEHAVIOR}-{step}.pt"
        os.utime(path, (now - age, now - age))
    exported = []

    def export(checkpoint, output):
        exported.append(checkpoint.name)
        output.write_bytes(b"brain-" + checkpoint.name.encode())

    monkeypatch.setattr(export_brain, "export_checkpoint", export)
    result = brain_lineage.fork(runs, BEHAVIOR, run_id="warrior-s001", now=lambda: now, log=lambda _: None)

    assert exported == [f"{BEHAVIOR}-2000000.pt"]
    assert result["run_id"] == "warrior-s002"
    assert result["step"] == 2_000_000
    data = version(runs, "warrior-s001-2000000")
    assert data["source"] == "manual"
    assert data["evaluated"] is False


def test_snapshot_falls_back_when_the_newest_cannot_be_read(tmp_path: Path, monkeypatch):
    runs = tmp_path / "runs"
    make_run(runs, "warrior-s001", steps=(1_000_000, 2_000_000))

    def export(checkpoint, output):
        if "2000000" in checkpoint.name:
            raise RuntimeError("truncated")
        output.write_bytes(b"ok")

    monkeypatch.setattr(export_brain, "export_checkpoint", export)
    result = brain_lineage.snapshot(
        runs, BEHAVIOR, "warrior-s001", evaluate=False, now=lambda: 1e12, log=lambda _: None
    )
    assert result["step"] == 1_000_000


def test_snapshot_evaluates_without_touching_the_champion(tmp_path: Path, monkeypatch):
    runs = tmp_path / "runs"
    make_run(runs, "warrior-s001")
    monkeypatch.setattr(
        export_brain, "export_checkpoint", lambda checkpoint, output: output.write_bytes(b"b")
    )

    class Runner:
        def run(self, brain, output, log=print):
            return evaluation(median=800)

    brain_lineage.snapshot(
        runs, BEHAVIOR, "warrior-s001", evaluate=True, runner=Runner(), now=lambda: 1e12,
        log=lambda _: None,
    )
    data = version(runs, "warrior-s001-1000000")
    assert data["evaluated"] is True
    assert data["score"] == champion.calculate_score(evaluation(median=800)["summary"])
    directory = champion.champion_directory(runs, BEHAVIOR)
    assert not (directory / "champion.json").exists()
    assert not (directory / "evaluations.jsonl").exists()


def test_snapshot_refuses_missing_or_old_runs(tmp_path: Path):
    runs = tmp_path / "runs"
    with pytest.raises(brain_lineage.LineageError, match="not found"):
        brain_lineage.snapshot(runs, BEHAVIOR, "warrior-s009", evaluate=False)
    make_run(runs, "warrior-s001", schema=3)
    with pytest.raises(brain_lineage.LineageError, match="old rules"):
        brain_lineage.snapshot(runs, BEHAVIOR, "warrior-s001", evaluate=False)


def test_prune_keeps_protected_checkpoints_and_thins_old_versions(tmp_path: Path):
    runs = tmp_path / "runs"
    steps = [step * 2_000_000 for step in range(1, 21)]  # 2M..40M, 20 versions
    make_run(runs, "warrior-s001", steps=steps)
    labels = brain_lineage.lineage_directory(runs, BEHAVIOR) / "labels.json"
    labels.parent.mkdir(parents=True)
    labels.write_text(json.dumps({"versions": [{"id": "warrior-s001-8000000", "pinned": True}]}))
    for step in steps:  # record_evaluation prunes after every evaluation
        record(runs, tmp_path, "warrior-s001", step, won=(step == 2_000_000), median=500 + step / 1e6)
    brain_lineage.prune(runs, BEHAVIOR)

    versions = {item["id"]: item for item in brain_lineage.load_versions(runs, BEHAVIOR)}
    # Protected ones keep their files.
    assert versions["warrior-s001-2000000"]["has_checkpoint"] is True
    assert versions["warrior-s001-8000000"]["has_checkpoint"] is True
    # The 10 newest unprotected versions stay; the 3 newest of them keep their checkpoint.
    newest = [f"warrior-s001-{step}" for step in steps[-10:]]
    assert all(identifier in versions for identifier in newest)
    with_checkpoint = [
        identifier for identifier in newest
        if (versions[identifier]["_path"] / "checkpoint.pt").is_file()
    ]
    assert with_checkpoint == newest[-3:]
    assert versions[newest[0]]["has_checkpoint"] is False
    # Older unprotected versions (4M..20M, minus pinned 8M) keep only the best per 10M bucket;
    # here the score grows with the step, so the newest of each bucket wins.
    old = sorted(int(identifier.rsplit("-", 1)[1]) for identifier in versions if identifier not in newest)
    assert old == [2_000_000, 6_000_000, 8_000_000, 18_000_000, 20_000_000]


@pytest.mark.parametrize("damaged", ["labels.json", "branches.json"])
def test_prune_removes_nothing_while_pins_or_parents_cannot_be_read(tmp_path: Path, damaged: str):
    runs = tmp_path / "runs"
    steps = [step * 2_000_000 for step in range(1, 16)]
    make_run(runs, "warrior-s001", steps=steps)
    lineage = brain_lineage.lineage_directory(runs, BEHAVIOR)
    lineage.mkdir(parents=True)
    (lineage / damaged).write_text("{half-written", encoding="utf-8")
    for step in steps:
        record(runs, tmp_path, "warrior-s001", step, won=False)

    assert brain_lineage.prune(runs, BEHAVIOR)["skipped"] is True
    versions = brain_lineage.load_versions(runs, BEHAVIOR)
    assert len(versions) == len(steps)
    assert all((item["_path"] / "checkpoint.pt").is_file() for item in versions)


def test_a_failed_copy_leaves_no_half_version(tmp_path: Path, monkeypatch):
    runs = tmp_path / "runs"
    make_run(runs, "warrior-s001")
    real_copy2 = brain_lineage.shutil.copy2

    def broken(source, destination, *args, **kwargs):
        raise OSError("disk full")

    monkeypatch.setattr(brain_lineage.shutil, "copy2", broken)
    assert record(runs, tmp_path, "warrior-s001", 1_000_000, won=False) is None
    directory = brain_lineage.versions_directory(runs, BEHAVIOR)
    assert list(directory.iterdir()) == []

    monkeypatch.setattr(brain_lineage.shutil, "copy2", real_copy2)
    assert record(runs, tmp_path, "warrior-s001", 1_000_000, won=False) is not None
    assert version(runs, "warrior-s001-1000000")["has_checkpoint"] is True
    assert [path.name for path in directory.iterdir()] == ["warrior-s001-1000000"]


def test_fork_keeps_the_checkpoint_time(tmp_path: Path):
    runs = tmp_path / "runs"
    behavior_dir = make_run(runs, "warrior-s001")
    old = 1_000_000_000.0
    os.utime(behavior_dir / f"{BEHAVIOR}-1000000.pt", (old, old))
    record(runs, tmp_path, "warrior-s001", 1_000_000, won=True)

    brain_lineage.fork(runs, BEHAVIOR, version="warrior-s001-1000000", log=lambda _: None)
    copied = runs / "warrior-s002" / BEHAVIOR / f"{BEHAVIOR}-1000000.pt"
    assert abs(copied.stat().st_mtime - old) < 2


def test_fork_succeeds_with_a_warning_when_the_branch_list_is_damaged(tmp_path: Path):
    runs = tmp_path / "runs"
    make_run(runs, "warrior-s001")
    record(runs, tmp_path, "warrior-s001", 1_000_000, won=True)
    branches = brain_lineage.lineage_directory(runs, BEHAVIOR) / "branches.json"
    branches.write_text("not json", encoding="utf-8")

    result = brain_lineage.fork(runs, BEHAVIOR, version="warrior-s001-1000000", log=lambda _: None)
    assert result["ok"] is True
    assert result["run_id"] == "warrior-s002"
    assert "warning" in result
    assert (runs / "warrior-s002" / BEHAVIOR / "checkpoint.pt").is_file()
    assert branches.read_text(encoding="utf-8") == "not json"


def test_snapshot_is_ok_with_a_warning_when_evaluation_fails(tmp_path: Path, monkeypatch):
    runs = tmp_path / "runs"
    make_run(runs, "warrior-s001")
    monkeypatch.setattr(
        export_brain, "export_checkpoint", lambda checkpoint, output: output.write_bytes(b"b")
    )

    class Runner:
        def run(self, brain, output, log=print):
            raise RuntimeError("evaluator crashed")

    result = brain_lineage.snapshot(
        runs, BEHAVIOR, "warrior-s001", evaluate=True, runner=Runner(), now=lambda: 1e12,
        log=lambda _: None,
    )
    assert result["ok"] is True
    assert "warning" in result
    assert version(runs, "warrior-s001-1000000")["evaluated"] is False


@pytest.mark.parametrize("bad", ["..", "../x", "a/b", "a\\b", ".hidden", "champions", "", "x" * 65])
def test_ids_that_could_escape_the_runs_folder_are_refused(tmp_path: Path, bad: str, capsys):
    runs = tmp_path / "runs"
    with pytest.raises(brain_lineage.LineageError, match="is not valid"):
        brain_lineage.fork(runs, BEHAVIOR, version=bad)
    with pytest.raises(brain_lineage.LineageError, match="is not valid"):
        brain_lineage.snapshot(runs, BEHAVIOR, bad, evaluate=False)
    assert brain_lineage.main(["--results-dir", str(runs), "fork", "--run-id", bad]) == 1
    assert json.loads(capsys.readouterr().out.strip().splitlines()[-1])["ok"] is False
    # M7: --behavior only accepts a hero class, so argparse refuses a path-like one up front.
    with pytest.raises(SystemExit) as refused:
        brain_lineage.main(["--results-dir", str(runs), "--behavior", bad, "sync"])
    assert refused.value.code == 2
    assert not runs.exists()


def test_cli_prints_json_on_success_and_error(tmp_path: Path, capsys):
    runs = tmp_path / "runs"
    assert brain_lineage.main(["--results-dir", str(runs), "sync"]) == 0
    assert json.loads(capsys.readouterr().out.strip().splitlines()[-1]) == {"ok": True, "imported": 0}

    assert brain_lineage.main(["--results-dir", str(runs), "fork", "--version", "missing"]) == 1
    error = json.loads(capsys.readouterr().out.strip().splitlines()[-1])
    assert error["ok"] is False
    assert "not found" in error["error"]


@pytest.mark.parametrize("name", ["abc\n", "warrior-s001.", "warrior-s001 ", "../x", "Champions"])
def test_check_id_rejects_aliasing_names(name: str):
    with pytest.raises(brain_lineage.LineageError):
        brain_lineage.check_id(name, "branch")


def test_version_metadata_keeps_a_stored_score_without_scoring_the_summary():
    metadata = brain_lineage._version_metadata(
        run_id="warrior-s001",
        step=10,
        source="eval",
        evaluation={"score": 123.0, "summary": {"Behavior": {}}},
        schema_version=arena_trainer.SCHEMA_VERSION,
        champion_flag=False,
    )

    assert metadata["score"] == 123.0
