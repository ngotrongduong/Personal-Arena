from pathlib import Path

import pytest
import yaml


CONFIG_DIR = Path(__file__).resolve().parents[1] / "config"
CONFIGS = (
    "warrior_ppo.yaml",
    "warrior_ppo_randomized.yaml",
    "mage_ppo.yaml",
    "archer_ppo.yaml",
)
M3_CURRICULA = {"zombie_count", "runner_weight", "brute_weight", "spitter_weight"}


@pytest.mark.parametrize("filename", CONFIGS)
def test_m3_curricula_match_their_single_behavior_and_increase(filename: str):
    config = yaml.safe_load((CONFIG_DIR / filename).read_text(encoding="utf-8"))
    assert len(config["behaviors"]) == 1
    behavior = next(iter(config["behaviors"]))
    parameters = config["environment_parameters"]
    assert M3_CURRICULA <= parameters.keys()

    for parameter in parameters.values():
        if not isinstance(parameter, dict) or "curriculum" not in parameter:
            continue
        criteria = [
            lesson["completion_criteria"]
            for lesson in parameter["curriculum"]
            if "completion_criteria" in lesson
        ]
        assert all(item["behavior"] == behavior for item in criteria)
        thresholds = [float(item["threshold"]) for item in criteria]
        assert all(left < right for left, right in zip(thresholds, thresholds[1:]))


def test_lower_health_classes_use_eighty_percent_of_warrior_thresholds():
    warrior = yaml.safe_load((CONFIG_DIR / "warrior_ppo.yaml").read_text(encoding="utf-8"))
    warrior_parameters = warrior["environment_parameters"]
    for filename in ("mage_ppo.yaml", "archer_ppo.yaml"):
        config = yaml.safe_load((CONFIG_DIR / filename).read_text(encoding="utf-8"))
        for name in M3_CURRICULA:
            warrior_thresholds = [
                lesson["completion_criteria"]["threshold"]
                for lesson in warrior_parameters[name]["curriculum"]
                if "completion_criteria" in lesson
            ]
            class_thresholds = [
                lesson["completion_criteria"]["threshold"]
                for lesson in config["environment_parameters"][name]["curriculum"]
                if "completion_criteria" in lesson
            ]
            assert class_thresholds == [round(value * 0.8, 1) for value in warrior_thresholds]
