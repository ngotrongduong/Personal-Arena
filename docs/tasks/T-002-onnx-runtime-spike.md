# T-002: Spike — load an ONNX model at runtime in a player build

- **Owner:** Claude (on the linked PC) + Owner
- **Status:** done (2026-09-29) — runtime ONNX import is Editor-only; replaced by the `.brain`
  format + `Core/PolicyBrain.cs` (see D-017 in `docs/DECISIONS.md`)
- **Milestone:** M0
- **Parallel OK with:** T-003..T-005
- **Depends on:** T-001 (GPU/venv confirmed). Packages already installed: ml-agents 4.1.0, ai.inference 2.6.1

## Question

Can a Windows player build load a `.onnx` file from disk (not imported as an asset) with
Inference Engine and hand it to `Agent.SetModel`? The Training Center (M4) needs this.

## Steps

1. Train any tiny behaviour for ~50k steps (or use an ML-Agents example model) → `.onnx`.
2. In a throwaway scene `Unity/Assets/Spikes/OnnxRuntime/`: a script that
   `ModelLoader.Load(path)` / builds a `ModelAsset` from bytes, then `agent.SetModel(...)`.
3. Build Windows player, copy `.onnx` next to the exe, run, check the agent acts.
4. If runtime ONNX load is not possible: try an Editor batchmode step that converts ONNX →
   `.sentis` (`ModelWriter`) and load that at runtime instead.

## Result (fill in)

- Works? yes / no / with conversion
- Code snippet that worked:
- Record the outcome as a new decision in `docs/DECISIONS.md`.
