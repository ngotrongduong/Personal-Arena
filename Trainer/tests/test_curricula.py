from pathlib import Path


CONFIG_DIR = Path(__file__).resolve().parents[1] / "config"


def test_only_survivor_configs_remain():
    assert sorted(path.name for path in CONFIG_DIR.glob("*.yaml")) == ["warrior_survivor_ppo.yaml"]


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
