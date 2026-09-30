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
        "own_build_share": 0.0,
        "review_share": 0.2,
    }.items():
        constant = options.environment_parameters[name].curriculum[0]
        assert constant.value.value == expected

    build_lessons = options.environment_parameters["build_level_max"].curriculum
    assert [lesson.name for lesson in build_lessons] == ["blank", "light", "medium", "full"]
    assert [lesson.value.value for lesson in build_lessons] == [0.0, 10.0, 25.0, 50.0]
    assert [lesson.completion_criteria.threshold for lesson in build_lessons[:-1]] == [
        0.22, 0.30, 0.40
    ]

    tier_lessons = options.environment_parameters["tier_max"].curriculum
    assert [lesson.name for lesson in tier_lessons] == ["tier-1", "tier-3", "tier-6", "tier-10"]
    assert [lesson.value.value for lesson in tier_lessons] == [1.0, 3.0, 6.0, 10.0]
    assert [lesson.completion_criteria.threshold for lesson in tier_lessons[:-1]] == [
        0.45, 0.60, 0.75
    ]

    hard_lessons = options.environment_parameters["hard_share"].curriculum
    assert [lesson.name for lesson in hard_lessons] == ["hard-off", "hard-on"]
    assert [lesson.value.value for lesson in hard_lessons] == [0.0, 0.1]
    assert hard_lessons[0].completion_criteria.threshold == 0.30

    for curriculum in (build_lessons, tier_lessons, hard_lessons):
        assert all(
            lesson.completion_criteria is not None
            and lesson.completion_criteria.measure
            == CompletionCriteriaSettings.MeasureType.PROGRESS
            and lesson.completion_criteria.min_lesson_length == 200
            and lesson.completion_criteria.behavior == "Warrior"
            for lesson in curriculum[:-1]
        )
        assert curriculum[-1].completion_criteria is None
