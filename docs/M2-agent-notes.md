# M2 HeroAgent and training notes

## Files

- `Unity/Assets/Arena/ML/`: ML-Agents adapter, action mapping/masking, environment config factory,
  behavior setup, parallel training host, and assembly definition.
- `Unity/Assets/Arena/ML/Editor/`: training scene generator and explicit-scene Windows build command.
- `Unity/Assets/Arena/ML/Tests/Editor/`: EditMode coverage for branch mapping, skill masks, and
  environment-parameter clamping.
- `Trainer/config/`: fixed Warrior PPO curriculum and a final-lesson domain-randomized variant.
- `Trainer/arena_trainer.py` and `Trainer/tests/`: stdlib launcher and command construction tests.
- `docs/TRAINING.md`: build, train, TensorBoard, resume, fine-tune, and ONNX locations.

No `.meta` files were written. Unity will generate them on import.

## Design decisions

- Each `HeroAgent` owns its Core objects and observation buffer. The simulation advances once per
  `OnActionReceived`; `DecisionRequester` repeats the last action between 12 Hz decisions so Core
  still receives exactly 60 steps per simulated second.
- Episode seeds are deterministic: `agentIndex * 1,000,003 + episodeCounter`.
- Every episode gets a newly validated `ArenaConfig`. The existing `ArenaSim` is reset and reused
  when width, height, and zombie count are unchanged; a new sim is required when those shape values
  change because Core owns its zombie list and immutable config reference.
- Skill action 0 is always enabled. Null/None, cooldown, and insufficient-energy slots are masked;
  an already-held Block remains enabled at low energy so the policy can continue holding it.
- Runtime code has no mutable static state and performs no LINQ or intentional per-tick allocations.
- The randomized trainer config uses synchronized five-lesson curricula for all randomized
  parameters, because ML-Agents environment parameters each own their own curriculum.

## Claude checks

1. Import with Unity 6000.3.2f1 and let Unity generate metadata.
2. Run EditMode tests, especially `PersonalArena.ML.Tests`, then run `dotnet test CoreTests`.
3. Generate `Assets/Scenes/Training.unity`; confirm it contains one host plus camera and light and
   that `EditorBuildSettings` is unchanged.
4. Build Windows batchmode and launch a short `--dry-run`, followed by a real trainer smoke run.
5. Verify in a live run that `OnActionReceived` occurs every fixed tick with repeated actions and
   TensorBoard receives the `Arena/*` statistics.
6. Tune the curriculum reward threshold (currently 6) from actual reward distributions before the
   long GPU run.
