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


def test_survivor_config_parses_with_mlagents_and_has_reward_curriculum():
    from mlagents.plugins.trainer_type import register_trainer_plugins
    from mlagents.trainers.cli_utils import load_config
    from mlagents.trainers.settings import CompletionCriteriaSettings, RunOptions

    path = CONFIG_DIR / "warrior_survivor_ppo.yaml"
    register_trainer_plugins()
    options = RunOptions.from_dict(load_config(str(path)))
    trainer = options.behaviors["Warrior"]

    assert trainer.hyperparameters.batch_size == 4096
    assert trainer.hyperparameters.buffer_size == 81920
    assert trainer.network_settings.normalize is False
    assert trainer.network_settings.hidden_units == 512
    assert trainer.network_settings.num_layers == 3
    assert trainer.time_horizon == 256
    assert trainer.max_steps == 100_000_000

    assert options.environment_parameters is not None
    lessons = options.environment_parameters["run_seconds"].curriculum
    assert [lesson.name for lesson in lessons] == [
        "survive-3", "survive-6", "survive-10", "full-run"
    ]
    assert [lesson.value.value for lesson in lessons] == [180.0, 360.0, 600.0, 900.0]
    criteria = [lesson.completion_criteria for lesson in lessons[:-1]]
    assert all(item is not None for item in criteria)
    assert [item.threshold for item in criteria if item is not None] == [5.0, 6.5, 8.5]
    assert all(
        item is not None
        and item.measure == CompletionCriteriaSettings.MeasureType.REWARD
        and item.signal_smoothing
        and item.min_lesson_length == 200
        and item.behavior == "Warrior"
        for item in criteria
    )
    assert lessons[-1].completion_criteria is None

    for name, expected in {
        "tier_min": 1.0,
        "tier_max": 1.0,
        "build_level_max": 0.0,
        "own_build_share": 0.0,
    }.items():
        constant = options.environment_parameters[name].curriculum[0]
        assert constant.value.value == expected
