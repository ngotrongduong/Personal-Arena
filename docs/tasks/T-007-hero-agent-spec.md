# T-007: Spec for `HeroAgent` (start of M2)

- **Owner:** Claude (spec) → then split into Codex tasks
- **Status:** todo
- **Milestone:** M2
- **Depends on:** T-003 (runner pattern), T-001 (GPU confirmed)

## Known facts to build on (from `develop`)

- Observations come from Core, not Unity sensors: `ObservationBuilder.Size` =
  16 hero values + 72 rays × (4 zombie types + 6) = **736** floats. `HeroAgent` writes the
  buffer with `VectorSensor.AddObservation` (or a custom `ISensor` for zero-alloc).
- Actions: 3 discrete branches 9 / 3 / 5 (`HeroInput.MoveBranchSize` etc.).
- Rewards: `RewardCalculator.Compute(sim.Events, dt)` after each `Step`, summed over the 5 ticks
  of a decision; `EndEpisode` when `sim.Done`.
- Unity fixed timestep is 0.02 s today. ML-Agents steps the Academy in `FixedUpdate`, so M2
  sets `Fixed Timestep = 1/60` and uses `DecisionRequester` period 5 — or drives the sim
  manually per Academy step. Decide in this spec and record in DECISIONS.
- Environment parameters from `Trainer/config/warrior_ppo.yaml`: `zombie_count`,
  `arena_size`, `hp_mult`, `damage_mult`, `speed_mult` → map onto `ArenaConfig` on
  `OnEpisodeBegin`.

## Output

- Final spec in this file, then task files for: `HeroAgent` (Codex), multi-arena training scene
  (Claude), `Heuristic()` via `KeyboardMouseInput` mapping (Codex), first training run (Owner/Claude).
