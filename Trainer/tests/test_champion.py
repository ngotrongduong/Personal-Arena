from __future__ import annotations

import json
from pathlib import Path

from Trainer import arena_trainer, champion


def summary(median=600.0, p10=420.0, wins=0.5, catastrophic=0) -> dict:
    return {
        "Runs": 100,
        "MedianSurvivedSeconds": median,
        "P10SurvivedSeconds": p10,
        "MeanSurvivedSeconds": median,
        "WinRate": wins,
        "CatastrophicCount": catastrophic,
        "Behavior": {"Aggression": 0.4},
    }


def evaluation(**kwargs) -> dict:
    data = summary(**kwargs)
    return {
        "summary": data,
        "passes_m4a": data["MedianSurvivedSeconds"] >= 600
        and data["P10SurvivedSeconds"] >= 420
        and data["CatastrophicCount"] == 0,
        "settings": champion.evaluation_settings(),
    }


def candidate(tmp_path: Path, name: str = "candidate.brain") -> Path:
    path = tmp_path / name
    path.write_bytes(name.encode())
    return path


def test_score_formula_and_decision_branches():
    data = summary(median=600, p10=400, wins=0.5, catastrophic=2)
    assert champion.calculate_score(data) == 830
    challenger = {"score": 106, "summary": {"CatastrophicCount": 1}}
    current = {"score": 100, "schema_version": 5, "summary": {"CatastrophicCount": 1}}
    assert champion.should_promote(challenger, None, 5)
    assert champion.should_promote(challenger, {**current, "schema_version": 4}, 5)
    assert champion.should_promote(challenger, current, 5)
    assert not champion.should_promote({**challenger, "score": 105}, current, 5)
    assert not champion.should_promote(
        {"score": 200, "summary": {"CatastrophicCount": 2}}, current, 5
    )


def test_promote_lose_restore_and_history_is_never_deleted(tmp_path: Path):
    first = champion.apply_evaluation(
        tmp_path, "Warrior", "warrior-s001", 2_000_000,
        candidate(tmp_path, "first.brain"), evaluation(),
    )
    assert first["won"]
    directory = champion.champion_directory(tmp_path, "Warrior")
    first_history = directory / "history" / "warrior-s001-2000000.brain"
    assert first_history.read_bytes() == b"first.brain"
    assert not (directory / "champion.brain.tmp").exists()
    assert not (directory / "champion.json.tmp").exists()

    loser = champion.apply_evaluation(
        tmp_path, "Warrior", "warrior-s001", 4_000_000,
        candidate(tmp_path, "loser.brain"), evaluation(median=590, p10=410, wins=0.5),
    )
    assert not loser["won"]
    assert first_history.exists()
    assert json.loads((directory / "champion.json").read_text())["step"] == 2_000_000

    second = champion.apply_evaluation(
        tmp_path, "Warrior", "warrior-s002", 6_000_000,
        candidate(tmp_path, "second.brain"), evaluation(median=700, p10=500, wins=0.8),
    )
    assert second["won"]
    assert first_history.exists()
    assert (directory / "history" / "warrior-s002-6000000.brain").exists()

    restored = champion.restore(tmp_path, "Warrior", "warrior-s001-2000000")
    assert restored["restored"] is True
    assert (directory / "champion.brain").read_bytes() == b"first.brain"
    assert first_history.exists()


def test_schema_change_promotes_even_with_a_lower_score(tmp_path: Path):
    directory = champion.champion_directory(tmp_path, "Warrior")
    directory.mkdir(parents=True)
    old = champion._metadata("old", 1, evaluation(median=900, p10=900, wins=1), 3)
    champion._write_json_atomic(directory / "champion.json", old)
    (directory / "champion.brain").write_bytes(b"old")

    result = champion.apply_evaluation(
        tmp_path, "Warrior", "new", 2, candidate(tmp_path),
        evaluation(median=100, p10=100, wins=0),
    )

    assert result["won"]


def test_settings_change_reevaluates_champion_before_comparing(tmp_path: Path):
    directory = champion.champion_directory(tmp_path, "Warrior")
    directory.mkdir(parents=True)
    old_raw = evaluation()
    old_raw["settings"] = {**champion.evaluation_settings(), "tier": 2}
    old = champion._metadata("old", 10, old_raw, arena_trainer.SCHEMA_VERSION)
    champion._write_json_atomic(directory / "champion.json", old)
    (directory / "champion.brain").write_bytes(b"old")

    class FakeRunner:
        def __init__(self):
            self.calls = 0

        def run(self, brain, output, log=print):
            self.calls += 1
            return evaluation(median=100, p10=100, wins=0)

    runner = FakeRunner()
    result = champion.apply_evaluation(
        tmp_path, "Warrior", "new", 20, candidate(tmp_path),
        evaluation(median=200, p10=200, wins=0), runner=runner,
    )

    assert runner.calls == 1
    assert result["won"]
    lines = (directory / "evaluations.jsonl").read_text().splitlines()
    assert len(lines) == 2


def test_dotnet_not_found_skips_without_creating_output(tmp_path: Path, monkeypatch):
    monkeypatch.setattr(champion.shutil, "which", lambda name: None)
    output = tmp_path / "result.json"

    result = champion.EvaluationRunner().run(tmp_path / "brain", output, log=lambda _: None)

    assert result["skipped"]
    assert "dotnet not found" in result["message"]
    assert not output.exists()


def test_evaluator_runner_uses_dotnet_and_reads_json(tmp_path: Path, monkeypatch):
    dll = tmp_path / "SurvivorEval.dll"
    dll.write_bytes(b"")
    monkeypatch.setattr(champion.shutil, "which", lambda name: "dotnet")
    monkeypatch.setattr(champion, "_build_evaluator", lambda dotnet: dll)

    class FakeProcess:
        returncode = 0

        def __init__(self, command, **kwargs):
            self.command = command
            output = Path(command[command.index("--out") + 1])
            output.write_text(json.dumps(evaluation()), encoding="utf-8")

        def communicate(self):
            return "ok", None

        def poll(self):
            return self.returncode

        def kill(self):
            self.returncode = -1

    monkeypatch.setattr(champion.subprocess, "Popen", FakeProcess)

    result = champion.EvaluationRunner().run(tmp_path / "brain", tmp_path / "out.json")

    assert result["summary"]["Runs"] == 100


def test_last_evaluated_step_is_per_run(tmp_path: Path):
    directory = champion.champion_directory(tmp_path, "Warrior")
    directory.mkdir(parents=True)
    records = [
        {"run_id": "a", "step": 2_000_000},
        {"run_id": "b", "step": 9_000_000},
        {"run_id": "a", "step": 4_000_000, "restored": True},
    ]
    (directory / "evaluations.jsonl").write_text(
        "".join(json.dumps(record) + "\n" for record in records), encoding="utf-8"
    )

    assert champion.last_evaluated_step(tmp_path, "Warrior", "a") == 2_000_000
