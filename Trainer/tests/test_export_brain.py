import struct
from pathlib import Path

import numpy as np
import pytest

from Trainer import export_brain


def actor_state(obs: int = 6, hidden: int = 4, branches=(9, 3, 5), seed: int = 0) -> dict:
    rng = np.random.default_rng(seed)
    state = {
        "version_number": np.array([3.0]),
        "memory_size_vector": np.array([0.0]),
        "discrete_act_size_vector": np.array([list(branches)], dtype=np.float32),
        "network_body._body_endoder.seq_layers.0.weight": rng.normal(size=(hidden, obs)),
        "network_body._body_endoder.seq_layers.0.bias": rng.normal(size=hidden),
        "network_body._body_endoder.seq_layers.2.weight": rng.normal(size=(hidden, hidden)),
        "network_body._body_endoder.seq_layers.2.bias": rng.normal(size=hidden),
    }
    for index, size in enumerate(branches):
        prefix = f"action_model._discrete_distribution.branches.{index}"
        state[prefix + ".weight"] = rng.normal(size=(size, hidden))
        state[prefix + ".bias"] = rng.normal(size=size)
    return state


def test_extract_orders_layers_and_checks_widths():
    body, branches = export_brain.extract_actor_layers(actor_state())

    assert [w.shape for w, _ in body] == [(4, 6), (4, 4)]
    assert [w.shape[0] for w, _ in branches] == [9, 3, 5]
    assert all(w.dtype == np.float32 for w, _ in body + branches)


@pytest.mark.parametrize(
    "extra_key",
    [
        "network_body.observation_encoder.processors.0.normalizer.running_mean",
        "network_body.lstm.lstm.weight_ih_l0",
        "action_model._continuous_distribution.mu.weight",
    ],
)
def test_extract_rejects_features_the_runtime_lacks(extra_key: str):
    state = actor_state()
    state[extra_key] = np.zeros(3)

    with pytest.raises(export_brain.ExportError):
        export_brain.extract_actor_layers(state)


def test_extract_rejects_recurrent_memory():
    state = actor_state()
    state["memory_size_vector"] = np.array([128.0])

    with pytest.raises(export_brain.ExportError):
        export_brain.extract_actor_layers(state)


def test_forward_applies_swish_to_body_only():
    weight = np.array([[1.0, 0.0], [0.0, -2.0]], dtype=np.float32)
    bias = np.array([0.0, 0.5], dtype=np.float32)
    head = np.eye(2, dtype=np.float32)
    logits = export_brain.forward([(weight, bias)], [(head, np.array([0.0, 1.0], dtype=np.float32))], np.array([2.0, 1.0]))

    swish = lambda x: x / (1 + np.exp(-x))  # noqa: E731
    np.testing.assert_allclose(logits, [swish(2.0), swish(-1.5) + 1.0], rtol=1e-6)


def test_encode_brain_layout_matches_policy_brain_reader():
    body, branches = export_brain.extract_actor_layers(actor_state())
    observation = np.linspace(-1, 1, 6, dtype=np.float32)
    payload = export_brain.encode_brain("Warrior", 499975, body, branches, observation)

    assert payload[:4] == b"PABR"
    version, name_length = struct.unpack_from("<ii", payload, 4)
    assert (version, name_length) == (export_brain.FORMAT_VERSION, 7)
    assert payload[12:19] == b"Warrior"
    step, obs_size = struct.unpack_from("<qi", payload, 19)
    assert (step, obs_size) == (499975, 6)

    offset = 31
    for group in (body, branches):
        (count,) = struct.unpack_from("<i", payload, offset)
        offset += 4
        assert count == len(group)
        for weight, bias in group:
            inputs, outputs = struct.unpack_from("<ii", payload, offset)
            offset += 8
            assert (inputs, outputs) == (weight.shape[1], weight.shape[0])
            stored = np.frombuffer(payload, "<f4", inputs * outputs, offset).reshape(outputs, inputs)
            np.testing.assert_array_equal(stored, weight)
            offset += 4 * (inputs * outputs + outputs)

    (self_tests,) = struct.unpack_from("<i", payload, offset)
    assert self_tests == 1
    offset += 4 + 4 * obs_size
    expected = np.frombuffer(payload, "<f4", 17, offset)
    np.testing.assert_allclose(expected, export_brain.forward(body, branches, observation), rtol=1e-6)
    assert offset + 4 * 17 == len(payload)


def test_newest_behavior_dir_and_checkpoint_order(tmp_path: Path):
    old = tmp_path / "run-a" / "Warrior"
    new = tmp_path / "run-b" / "Warrior"
    for folder in (old, new):
        folder.mkdir(parents=True)
    (old / "Warrior-500.pt").write_bytes(b"x")
    (new / "Warrior-1000.pt").write_bytes(b"x")
    (new / "Warrior-20000.pt").write_bytes(b"x")
    (new / "checkpoint.pt").write_bytes(b"x")
    import os

    os.utime(old / "Warrior-500.pt", (1, 1))

    assert export_brain.newest_behavior_dir(tmp_path, "Warrior") == new
    assert [step for step, _ in export_brain.checkpoints(new)] == [1000, 20000]
    assert export_brain.newest_behavior_dir(tmp_path, "Mage") is None
