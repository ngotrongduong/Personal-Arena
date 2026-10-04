from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path

import numpy as np
import pytest

from Trainer import arena_trainer, brain_upgrade, export_brain


PINNED_FAKE_WEIGHT_SHA = "c4a11cc2bb42b9bef66042e29dee16de2b39a654002ea07d8b891563cc716e4e"


def schemas() -> tuple[dict, dict]:
    old = {
        "schema_version": 1,
        "observation": [
            {"name": "self", "repeat": 1, "fields": ["a", "b", "c", "d"]},
            {"name": "items", "repeat": 2, "fields": ["x", "y"]},
            {"name": "rays", "repeat": 1, "fields": ["p", "q", "r", "s"]},
        ],
        "actions": [{"name": "move", "size": 3}, {"name": "skill", "size": 2}],
    }
    new = {
        "schema_version": 2,
        "observation": [
            {"name": "self", "repeat": 1, "fields": ["a", "b", "inserted", "c", "d"]},
            {"name": "items", "repeat": 3, "fields": ["x", "y"]},
            {"name": "rays", "repeat": 1, "fields": ["p", "q", "r", "s"]},
            {"name": "extra", "repeat": 1, "fields": ["u", "v"]},
        ],
        "actions": [
            {"name": "move", "size": 5},
            {"name": "skill", "size": 2},
            {"name": "bonus", "size": 4},
        ],
    }
    return old, new


def fake_checkpoint(
    seed: int = 19,
    step: int = 123,
    in_size: int = 12,
    branches: tuple[int, ...] = (3, 2),
    hidden: int = 8,
) -> dict:
    """A tiny ML-Agents-shaped checkpoint.

    Weights come from ``numpy.random.RandomState`` with explicit float32 casts, never from torch's RNG,
    so the bytes (and the committed cross-language fixtures built from them) are identical on every
    platform.
    """
    import torch

    random = np.random.RandomState(seed)

    def layer(out_size: int, in_width: int) -> tuple[object, object]:
        weight = (random.standard_normal((out_size, in_width)).astype(np.float32) * np.float32(0.1)).astype(np.float32)
        bias = (random.standard_normal(out_size).astype(np.float32) * np.float32(0.1)).astype(np.float32)
        return torch.from_numpy(weight), torch.from_numpy(bias)

    policy: dict = {
        "version_number": torch.tensor([3.0]),
        "is_continuous_int_deprecated": torch.tensor([0.0]),
        "continuous_act_size_vector": torch.tensor([0.0]),
        "discrete_act_size_vector": torch.tensor([[float(size) for size in branches]]),
        "act_size_vector_deprecated": torch.tensor([float(sum(branches))]),
        "memory_size_vector": torch.tensor([0.0]),
    }
    critic: dict = {}
    width = in_size
    for index in (0, 2, 4):
        weight, bias = layer(hidden, width)
        policy[f"network_body._body_endoder.seq_layers.{index}.weight"] = weight
        policy[f"network_body._body_endoder.seq_layers.{index}.bias"] = bias
        critic[f"network_body._body_endoder.seq_layers.{index}.weight"] = (weight.clone() * 0.7).float()
        critic[f"network_body._body_endoder.seq_layers.{index}.bias"] = (bias.clone() * 0.7).float()
        width = hidden
    for index, size in enumerate(branches):
        weight, bias = layer(size, hidden)
        policy[f"action_model._discrete_distribution.branches.{index}.weight"] = weight
        policy[f"action_model._discrete_distribution.branches.{index}.bias"] = bias
    value_weight, value_bias = layer(1, hidden)
    critic["value_heads.value_heads.extrinsic.weight"] = value_weight
    critic["value_heads.value_heads.extrinsic.bias"] = value_bias
    return {
        "Policy": policy,
        "global_step": {"_GlobalSteps__global_step": torch.tensor(step)},
        "Optimizer:value_optimizer": {"state": {1: "old adam"}, "param_groups": []},
        "Optimizer:critic": critic,
    }


def body_output(state: dict, observation: np.ndarray) -> np.ndarray:
    hidden = observation.astype(np.float32)
    for index in (0, 2, 4):
        weight = state[f"network_body._body_endoder.seq_layers.{index}.weight"].numpy()
        bias = state[f"network_body._body_endoder.seq_layers.{index}.bias"].numpy()
        pre = weight @ hidden + bias
        hidden = (pre / (1.0 + np.exp(-pre))).astype(np.float32)
    return hidden


def test_upgrade_preserves_actor_logits_and_critic_body():
    old_schema, new_schema = schemas()
    checkpoint = fake_checkpoint()
    upgraded = brain_upgrade.upgrade_state(checkpoint, old_schema, new_schema, seed=77)
    columns = brain_upgrade.column_map(old_schema, new_schema)
    action_mapping = brain_upgrade.action_map(old_schema, new_schema)
    old_body, old_branches = export_brain.extract_actor_layers(checkpoint["Policy"])
    new_body, new_branches = export_brain.extract_actor_layers(upgraded["Policy"])
    rng = np.random.default_rng(9019)

    for _ in range(50):
        old_observation = rng.uniform(-1, 1, 12).astype(np.float32)
        new_observation = np.zeros(17, dtype=np.float32)
        new_observation[columns] = old_observation
        old_logits = export_brain.forward(old_body, old_branches, old_observation)
        new_logits = export_brain.forward(new_body, new_branches, new_observation)
        old_offset = 0
        new_offsets = np.cumsum([0] + [branch[0].shape[0] for branch in new_branches])
        for new_branch, old_size in action_mapping:
            np.testing.assert_allclose(
                new_logits[new_offsets[new_branch] : new_offsets[new_branch] + old_size],
                old_logits[old_offset : old_offset + old_size],
                rtol=0,
                atol=1e-6,
            )
            old_offset += old_size
        np.testing.assert_allclose(
            body_output(upgraded["Optimizer:critic"], new_observation),
            body_output(checkpoint["Optimizer:critic"], old_observation),
            rtol=0,
            atol=1e-6,
        )

    assert "Optimizer:value_optimizer" not in upgraded
    assert "Optimizer:value_optimizer" in checkpoint
    assert upgraded["global_step"]["_GlobalSteps__global_step"].item() == 123
    assert upgraded["Policy"]["discrete_act_size_vector"].tolist() == [[5.0, 2.0, 4.0]]
    np.testing.assert_allclose(
        new_branches[0][1][3:], old_branches[0][1].min() - brain_upgrade.NEW_CHOICE_BIAS_MARGIN, rtol=0, atol=0
    )
    np.testing.assert_allclose(new_branches[2][1], 0, rtol=0, atol=0)


def test_upgrade_refuses_non_growth_and_unsupported_checkpoints():
    old_schema, new_schema = schemas()

    renamed = json.loads(json.dumps(new_schema))
    renamed["observation"][0]["fields"][-1] = "renamed"
    with pytest.raises(brain_upgrade.UpgradeError):
        brain_upgrade.column_map(old_schema, renamed)

    shrunk = json.loads(json.dumps(new_schema))
    shrunk["actions"][0]["size"] = 2
    with pytest.raises(brain_upgrade.UpgradeError):
        brain_upgrade.action_map(old_schema, shrunk)

    wrong_width = fake_checkpoint()
    wrong_width["Policy"][brain_upgrade.INPUT_WEIGHT] = wrong_width["Policy"][brain_upgrade.INPUT_WEIGHT][:, :-1]
    with pytest.raises(brain_upgrade.UpgradeError, match="width"):
        brain_upgrade.upgrade_state(wrong_width, old_schema, new_schema)

    recurrent = fake_checkpoint()
    recurrent["Policy"]["network_body.lstm.weight"] = recurrent["Policy"][brain_upgrade.INPUT_WEIGHT]
    with pytest.raises(brain_upgrade.UpgradeError, match="LSTM"):
        brain_upgrade.upgrade_state(recurrent, old_schema, new_schema)

    normalized = fake_checkpoint()
    normalized["Policy"]["network_body.normalize_steps"] = normalized["Policy"]["version_number"]
    with pytest.raises(brain_upgrade.UpgradeError, match="normalizer"):
        brain_upgrade.upgrade_state(normalized, old_schema, new_schema)


def write_schema(path: Path, schema: dict) -> None:
    path.write_text(json.dumps(schema), encoding="utf-8")


def hashes(path: Path) -> dict[str, str]:
    return {
        str(item.relative_to(path)): hashlib.sha256(item.read_bytes()).hexdigest()
        for item in path.rglob("*")
        if item.is_file()
    }


def test_upgrade_run_copies_curriculum_without_touching_source(tmp_path: Path, monkeypatch):
    import torch

    old_schema, new_schema = schemas()
    schema_dir = tmp_path / "schemas"
    schema_dir.mkdir()
    write_schema(schema_dir / "survivor_v1.json", old_schema)
    write_schema(schema_dir / "survivor_v2.json", new_schema)
    monkeypatch.setattr(arena_trainer, "SCHEMAS_DIR", schema_dir)
    runs = tmp_path / "runs"
    source = runs / "warrior-s001"
    behavior = source / "Warrior"
    behavior.mkdir(parents=True)
    torch.save(fake_checkpoint(step=500), behavior / "Warrior-500.pt")
    torch.save(fake_checkpoint(step=500), behavior / "checkpoint.pt")
    arena_trainer.write_schema_version(source, 1)
    status = {
        "run_seconds": {"lesson_num": 2},
        "build_level_max": {"lesson_num": 1},
        "Warrior": {"checkpoints": [{"file_path": "source.pt"}], "other": 7},
        "Other": {"checkpoints": []},
        "metadata": {"mlagents_version": "1.1.0"},
    }
    (source / "run_logs").mkdir()
    (source / "run_logs" / "training_status.json").write_text(json.dumps(status), encoding="utf-8")
    before = hashes(source)

    output = brain_upgrade.upgrade_run(runs, "warrior-s001", "Warrior", "warrior-s002", 1, 2)

    target = runs / "warrior-s002"
    assert output == target / "Warrior"
    assert (output / "checkpoint.pt").read_bytes() == (output / "Warrior-500.pt").read_bytes()
    assert (target / arena_trainer.SCHEMA_FILE).read_text(encoding="utf-8") == "2"
    metadata = json.loads((target / "upgraded_from.json").read_text(encoding="utf-8"))
    assert metadata | {"created_at": "ignored"} == {
        "source_run": "warrior-s001",
        "source_step": 500,
        "old_schema": 1,
        "new_schema": 2,
        "created_at": "ignored",
    }
    copied = json.loads((target / "run_logs" / "training_status.json").read_text(encoding="utf-8"))
    assert copied["run_seconds"]["lesson_num"] == 2
    assert copied["build_level_max"]["lesson_num"] == 1
    assert copied["metadata"] == status["metadata"]
    assert "checkpoints" not in copied["Warrior"]
    assert "checkpoints" not in copied["Other"]
    assert copied["Warrior"]["other"] == 7
    assert hashes(source) == before

    with pytest.raises(brain_upgrade.UpgradeError, match="already exists"):
        brain_upgrade.upgrade_run(runs, "warrior-s001", "Warrior", "warrior-s002", 1, 2)


@pytest.mark.parametrize("damage", ["status", "checkpoint"])
def test_failed_upgrade_run_leaves_no_partial_target(tmp_path: Path, monkeypatch, damage: str):
    import torch

    old_schema, new_schema = schemas()
    schema_dir = tmp_path / "schemas"
    schema_dir.mkdir()
    write_schema(schema_dir / "survivor_v1.json", old_schema)
    write_schema(schema_dir / "survivor_v2.json", new_schema)
    monkeypatch.setattr(arena_trainer, "SCHEMAS_DIR", schema_dir)
    runs = tmp_path / "runs"
    source = runs / "warrior-s001"
    behavior = source / "Warrior"
    behavior.mkdir(parents=True)
    if damage == "checkpoint":
        (behavior / "Warrior-500.pt").write_bytes(b"not a pickle")
    else:
        torch.save(fake_checkpoint(step=500), behavior / "Warrior-500.pt")
    (behavior / "checkpoint.pt").write_bytes((behavior / "Warrior-500.pt").read_bytes())
    arena_trainer.write_schema_version(source, 1)
    (source / "run_logs").mkdir()
    # A training_status.json cut off by a killed trainer.
    (source / "run_logs" / "training_status.json").write_text('{"run_seconds": {"les', encoding="utf-8")

    with pytest.raises(brain_upgrade.UpgradeError):
        brain_upgrade.upgrade_run(runs, "warrior-s001", "Warrior", "warrior-s002", 1, 2)

    assert sorted(path.name for path in runs.iterdir()) == ["warrior-s001"]


def test_current_schema_file_is_valid_and_matches_the_expected_size():
    schema = arena_trainer.load_schema(arena_trainer.SCHEMA_VERSION)

    assert arena_trainer.schema_path(arena_trainer.SCHEMA_VERSION).is_file()
    assert arena_trainer.SCHEMA_VERSION == 5
    assert schema["schema_version"] == 5
    assert arena_trainer.observation_size(schema) == 2592
    assert [action["size"] for action in schema["actions"]] == [9, 7, 5]


def test_load_schema_rejects_duplicate_names_and_inconsistent_totals(tmp_path: Path, monkeypatch):
    old_schema, _ = schemas()
    monkeypatch.setattr(arena_trainer, "SCHEMAS_DIR", tmp_path)
    duplicate = json.loads(json.dumps(old_schema))
    duplicate["observation"][0]["fields"][1] = "a"
    write_schema(tmp_path / "survivor_v1.json", duplicate)
    with pytest.raises(ValueError, match="unique"):
        arena_trainer.load_schema(1)

    inconsistent = json.loads(json.dumps(old_schema))
    inconsistent["observation_size"] = 999
    write_schema(tmp_path / "survivor_v1.json", inconsistent)
    with pytest.raises(ValueError, match="inconsistent"):
        arena_trainer.load_schema(1)


FIXTURE_HIDDEN = 8


def fixture_payloads() -> tuple[bytes, bytes, dict]:
    """Real v4 -> v5 upgrade of a small (hidden width 8) deterministic numpy-built checkpoint."""
    old_schema = arena_trainer.load_schema(4)
    new_schema = arena_trainer.load_schema(5)
    old_state = fake_checkpoint(
        seed=1919, step=19019, in_size=2264, branches=(9, 5, 5), hidden=FIXTURE_HIDDEN
    )
    new_state = brain_upgrade.upgrade_state(old_state, old_schema, new_schema, seed=20)
    old_body, old_branches = export_brain.extract_actor_layers(old_state["Policy"])
    new_body, new_branches = export_brain.extract_actor_layers(new_state["Policy"])
    old_brain = export_brain.encode_brain("Warrior", 19019, old_body, old_branches)
    new_brain = export_brain.encode_brain("Warrior", 19019, new_body, new_branches)
    mapping = {
        "columns": brain_upgrade.column_map(old_schema, new_schema),
        "actions": [list(item) for item in brain_upgrade.action_map(old_schema, new_schema)],
    }
    return old_brain, new_brain, mapping


def test_cross_language_fixtures_are_current():
    fixtures = Path(__file__).resolve().parents[2] / "CoreTests" / "Fixtures" / "brain_upgrade"
    old_brain, new_brain, mapping = fixture_payloads()
    expected_map = json.dumps(mapping, indent=2) + "\n"
    if os.environ.get("PA_REGEN_FIXTURES") == "1":
        fixtures.mkdir(parents=True, exist_ok=True)
        (fixtures / "old.brain").write_bytes(old_brain)
        (fixtures / "new.brain").write_bytes(new_brain)
        (fixtures / "map.json").write_text(expected_map, encoding="utf-8")
    assert (fixtures / "old.brain").read_bytes() == old_brain
    assert (fixtures / "new.brain").read_bytes() == new_brain
    assert json.loads((fixtures / "map.json").read_text(encoding="utf-8")) == mapping


def test_v4_to_v5_maps_every_block_as_documented():
    old_schema = arena_trainer.load_schema(4)
    new_schema = arena_trainer.load_schema(5)
    columns = brain_upgrade.column_map(old_schema, new_schema)

    assert len(columns) == 2264
    assert len(set(columns)) == 2264
    assert columns[0:64] == list(range(0, 64))  # self block keeps its positions
    assert columns[64:128] == list(range(72, 136))  # inventory slot i -> 72 + i
    for slot in range(4):
        old_base = 128 + 66 * slot
        new_base = 200 + 130 * slot
        assert columns[old_base : old_base + 64] == list(range(new_base, new_base + 64))
        assert columns[old_base + 64] == new_base + 128  # next level
        assert columns[old_base + 65] == new_base + 129  # present flag
    assert columns[392:2264] == list(range(720, 2592))  # rays and density shift by 328
    assert brain_upgrade.action_map(old_schema, new_schema) == [(0, 9), (1, 5), (2, 5)]
    new_only = set(range(2592)) - set(columns)
    assert len(new_only) == 2592 - 2264
    assert set(range(64, 72)) <= new_only


def test_v5_schema_json_extends_v4_names():
    old = {segment["name"]: segment for segment in arena_trainer.load_schema(4)["observation"]}
    new = {segment["name"]: segment for segment in arena_trainer.load_schema(5)["observation"]}

    assert list(old) == list(new)
    assert new["self"]["fields"][:64] == old["self"]["fields"]
    assert new["self"]["fields"][64:] == [
        "skill_cooldown_4", "skill_cooldown_5", "skill_allowed_4", "skill_allowed_5",
        "last_skill_5", "last_skill_6", "reserved_70", "reserved_71",
    ]
    assert new["inventory"]["repeat"] == 128
    assert len(new["offers"]["fields"]) == 130
    assert new["offers"]["fields"][:64] == old["offers"]["fields"][:64]
    assert new["offers"]["fields"][-2:] == ["next_level", "present"]
    assert new["rays"] == old["rays"] and new["density"] == old["density"]
    assert arena_trainer.load_schema(5)["action_size"] == 21


def test_v4_to_v5_upgrade_treats_weights_and_biases():
    old_schema = arena_trainer.load_schema(4)
    new_schema = arena_trainer.load_schema(5)
    old = fake_checkpoint(seed=5, in_size=2264, branches=(9, 5, 5), hidden=6)
    new = brain_upgrade.upgrade_state(old, old_schema, new_schema, seed=3)
    columns = brain_upgrade.column_map(old_schema, new_schema)
    new_only = sorted(set(range(2592)) - set(columns))

    for section in ("Policy", "Optimizer:critic"):
        old_weight = old[section][brain_upgrade.INPUT_WEIGHT].numpy()
        new_weight = new[section][brain_upgrade.INPUT_WEIGHT].numpy()
        assert new_weight.shape == (6, 2592)
        np.testing.assert_array_equal(new_weight[:, columns], old_weight)  # old weights unchanged
        assert not new_weight[:, new_only].any()  # new input columns are exactly zero

    old_policy, new_policy = old["Policy"], new["Policy"]
    assert new_policy["discrete_act_size_vector"].tolist() == [[9.0, 7.0, 5.0]]
    assert new_policy["act_size_vector_deprecated"].tolist() == [21.0]
    for branch, old_size in ((0, 9), (2, 5)):
        for kind in ("weight", "bias"):
            key = f"action_model._discrete_distribution.branches.{branch}.{kind}"
            np.testing.assert_array_equal(new_policy[key].numpy(), old_policy[key].numpy())
    weight_key = "action_model._discrete_distribution.branches.1.weight"
    bias_key = "action_model._discrete_distribution.branches.1.bias"
    np.testing.assert_array_equal(new_policy[weight_key].numpy()[:5], old_policy[weight_key].numpy())
    np.testing.assert_array_equal(new_policy[bias_key].numpy()[:5], old_policy[bias_key].numpy())
    new_bias = new_policy[bias_key].numpy()
    assert new_bias.shape == (7,)
    expected = old_policy[bias_key].numpy().min() - brain_upgrade.NEW_CHOICE_BIAS_MARGIN
    np.testing.assert_array_equal(new_bias[5:], np.float32([expected, expected]))
    assert new_bias[5:].max() < new_bias[:5].min() - 4.0  # new skills are clearly less likely
    assert np.abs(new_policy[weight_key].numpy()[5:]).max() < 0.1  # small random rows, not copies


def test_v4_to_v5_upgrade_preserves_old_logits_on_real_layout():
    old_schema = arena_trainer.load_schema(4)
    new_schema = arena_trainer.load_schema(5)
    old = fake_checkpoint(seed=6, in_size=2264, branches=(9, 5, 5), hidden=16)
    new = brain_upgrade.upgrade_state(old, old_schema, new_schema, seed=1)
    columns = brain_upgrade.column_map(old_schema, new_schema)
    old_body, old_branches = export_brain.extract_actor_layers(old["Policy"])
    new_body, new_branches = export_brain.extract_actor_layers(new["Policy"])
    rng = np.random.default_rng(3)
    for _ in range(5):
        old_obs = rng.uniform(-1, 1, 2264).astype(np.float32)
        new_obs = np.zeros(2592, dtype=np.float32)
        new_obs[columns] = old_obs
        old_logits = export_brain.forward(old_body, old_branches, old_obs)
        new_logits = export_brain.forward(new_body, new_branches, new_obs)
        np.testing.assert_allclose(new_logits[:9], old_logits[:9], rtol=0, atol=1e-5)
        np.testing.assert_allclose(new_logits[9:14], old_logits[9:14], rtol=0, atol=1e-5)
        np.testing.assert_allclose(new_logits[16:], old_logits[14:], rtol=0, atol=1e-5)
        # New skill logits stay below every old skill logit by roughly the bias margin.
        assert new_logits[14:16].max() < old_logits[9:14].min() - 2.0


def test_upgrade_is_deterministic_and_independent_of_torch_rng():
    import torch

    old_schema = arena_trainer.load_schema(4)
    new_schema = arena_trainer.load_schema(5)
    old = fake_checkpoint(seed=7, in_size=2264, branches=(9, 5, 5), hidden=4)
    torch.manual_seed(1)
    first = brain_upgrade.upgrade_state(old, old_schema, new_schema, seed=9)
    torch.manual_seed(2)
    second = brain_upgrade.upgrade_state(old, old_schema, new_schema, seed=9)
    key = "action_model._discrete_distribution.branches.1.weight"
    assert torch.equal(first["Policy"][key], second["Policy"][key])


def test_fake_checkpoint_bytes_are_pinned_across_platforms():
    state = fake_checkpoint(seed=1, in_size=3, branches=(2,), hidden=2)
    weight = state["Policy"][brain_upgrade.INPUT_WEIGHT].numpy()
    # numpy's RandomState (MT19937) stream is frozen by numpy's compatibility guarantee.
    assert hashlib.sha256(weight.astype("<f4").tobytes()).hexdigest() == PINNED_FAKE_WEIGHT_SHA


def make_mlagents(obs_size: int, branches: tuple[int, ...], model_path: Path, load: bool = False):
    from mlagents.plugins.trainer_type import register_trainer_plugins
    from mlagents.trainers.model_saver.torch_model_saver import TorchModelSaver
    from mlagents.trainers.policy.torch_policy import TorchPolicy
    from mlagents.trainers.ppo.optimizer_torch import TorchPPOOptimizer
    from mlagents.trainers.settings import NetworkSettings, TrainerSettings
    from mlagents.trainers.torch_entities.networks import SimpleActor
    from mlagents_envs.base_env import (
        ActionSpec,
        BehaviorSpec,
        DimensionProperty,
        ObservationSpec,
        ObservationType,
    )

    register_trainer_plugins()
    network = NetworkSettings(normalize=False, hidden_units=8, num_layers=3)
    settings = TrainerSettings(network_settings=network, keep_checkpoints=2, max_steps=1000)
    observation = ObservationSpec(
        shape=(obs_size,),
        dimension_property=(DimensionProperty.UNSPECIFIED,),
        observation_type=ObservationType.DEFAULT,
        name="vector",
    )
    spec = BehaviorSpec([observation], ActionSpec.create_discrete(branches))
    policy = TorchPolicy(
        41,
        spec,
        network,
        SimpleActor,
        {"conditional_sigma": False, "tanh_squash": False},
    )
    optimizer = TorchPPOOptimizer(policy, settings)
    saver = TorchModelSaver(settings, str(model_path), load=load)
    saver.register(policy)
    saver.register(optimizer)
    saver.export = lambda *_args, **_kwargs: None
    return policy, optimizer, saver


def test_mlagents_saver_upgrade_loader_and_update_round_trip(tmp_path: Path):
    import torch
    from mlagents.trainers.buffer import AgentBuffer, BufferKey, RewardSignalUtil
    from mlagents.trainers.trajectory import ObsUtil

    old_schema, new_schema = schemas()
    old_dir = tmp_path / "old"
    old_policy, old_optimizer, old_saver = make_mlagents(12, (3, 2), old_dir)
    old_policy.set_step(321)
    old_saver.save_checkpoint("Warrior", 321)
    source = torch.load(old_dir / "Warrior-321.pt", map_location="cpu")
    assert "Optimizer:value_optimizer" in source
    upgraded = brain_upgrade.upgrade_state(source, old_schema, new_schema, seed=8)
    assert "Optimizer:value_optimizer" not in upgraded

    new_dir = tmp_path / "new"
    new_dir.mkdir()
    torch.save(upgraded, new_dir / "checkpoint.pt")
    # Key check on a separate network, so it cannot hide a failure of mlagents' own loader below.
    check_policy, check_optimizer, _ = make_mlagents(17, (5, 2, 4), tmp_path / "check")
    policy_result = check_policy.actor.load_state_dict(upgraded["Policy"], strict=False)
    critic_result = check_optimizer.critic.load_state_dict(upgraded["Optimizer:critic"], strict=False)
    assert policy_result.missing_keys == [] and policy_result.unexpected_keys == []
    assert critic_result.missing_keys == [] and critic_result.unexpected_keys == []

    new_policy, new_optimizer, new_saver = make_mlagents(17, (5, 2, 4), new_dir, load=True)
    new_saver.initialize_or_load()
    assert new_policy.get_current_step() == 321
    loaded_critic = new_optimizer.critic.state_dict()
    for key, value in upgraded["Optimizer:critic"].items():
        assert torch.equal(loaded_critic[key].cpu(), value.cpu()), key
    assert len(new_optimizer.optimizer.state) == 0

    columns = brain_upgrade.column_map(old_schema, new_schema)
    old_body, old_branches = export_brain.extract_actor_layers(source["Policy"])
    loaded_body, loaded_branches = export_brain.extract_actor_layers(new_policy.actor.state_dict())
    rng = np.random.default_rng(44)
    for _ in range(10):
        old_observation = rng.normal(size=12).astype(np.float32)
        new_observation = np.zeros(17, dtype=np.float32)
        new_observation[columns] = old_observation
        old_logits = export_brain.forward(old_body, old_branches, old_observation)
        new_logits = export_brain.forward(loaded_body, loaded_branches, new_observation)
        np.testing.assert_allclose(new_logits[:3], old_logits[:3], rtol=0, atol=1e-6)
        np.testing.assert_allclose(new_logits[5:7], old_logits[3:5], rtol=0, atol=1e-6)

    batch = AgentBuffer()
    for _ in range(4):
        batch[ObsUtil.get_name_at(0)].append(np.zeros(17, dtype=np.float32))
        batch[BufferKey.ACTION_MASK].append(np.zeros(11, dtype=np.float32))
        batch[BufferKey.DISCRETE_ACTION].append(np.array([0, 0, 0], dtype=np.int32))
        batch[BufferKey.DISCRETE_LOG_PROBS].append(np.zeros(3, dtype=np.float32))
        batch[BufferKey.MASKS].append(np.array(1, dtype=np.float32))
        batch[BufferKey.MEMORY].append(np.zeros(0, dtype=np.float32))
        batch[BufferKey.CRITIC_MEMORY].append(np.zeros(0, dtype=np.float32))
        batch[BufferKey.ADVANTAGES].append(np.array(0.1, dtype=np.float32))
        batch[RewardSignalUtil.returns_key("extrinsic")].append(np.array(0.2, dtype=np.float32))
    stats = new_optimizer.update(batch, num_sequences=4)
    assert np.isfinite(stats["Losses/Policy Loss"])
    assert np.isfinite(stats["Losses/Value Loss"])
