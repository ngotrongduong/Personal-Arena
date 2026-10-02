# T-039: M9 wave 2 infrastructure — schema v5 in Core (catalog 128, six active skills)

- **Milestone:** M9 (content wave 2; plan in `docs/CONTENT-PROPOSAL.md`, decision D-041)
- **Owner:** Claude subagent (`core-sim-engineer`), Claude reviews and does git
- **Branch:** `claude/serene-sagan-3a70q5` (do NOT run any git command)
- **Depends on:** T-036, T-038. **Next:** T-040 (Trainer: `survivor_v5.json`, brain upgrade v4 → v5), T-041 (Unity, needs the PC).

## Goal (owner view)

Make room for the many weapons, passives, evolutions and skills of the real game. Wave 1 used up the
catalog (only ids 38–39 are free). This task only builds the room; content comes in later tasks. The owner accepts
that brains must be upgraded or retrained.

## What changes (observation/action schema v5)

1. `SurvivorObservation.SchemaVersion = 5`.
2. **Catalog 64 → 128.** `SurvivorCatalog.CatalogSize = 128`. **Existing catalog indices NEVER move** (profiles,
   brains and tests refer to them). Reserve ranges in code comments: 64–95 new weapons, 96–111 new passives,
   112–127 evolutions of the new weapons; old free ids 38–39 and the old fillers 62/63 stay as they are.
   Inventory observation = 128 slots; offers = 4 slots × (catalog size + 2) as they are now.
3. **Six active skills.** `SurvivorClassDef.ActiveSkills` has 6 slots; `SurvivorInput.SkillBranchSize = 7`
   (0 = none, 1..6). Action layout becomes 9 / 7 / 5. All three classes keep their current 4 skills in slots
   0–3 and get `none` in slots 4–5 for now (new skills come later). Update the action mask, `SurvivorInput`,
   `SurvivorPilot`, `SurvivorActionMask`, `SurvivorSim` skill handling and cooldown arrays.
4. **Self block 64 → 72 values**: add `skill_cooldown_4/5`, `skill_allowed_4/5`, `last_skill_5/6`
   (existing `last_skill_0..4` = none + 4 skills; with 6 skills it is none + 6 = 7 values) and use the rest as
   reserved. Keep the order of every existing self field and append the new ones so brain upgrades can map them.
   Rays (72 × 25) and density (3 × 8 × 3) are unchanged.
5. Recompute every offset constant (`InventoryOffset`, `OffersOffset`, `RaysOffset`, `DensityOffset`, `Size`) and the
   doc comment at the top of `SurvivorObservation.cs`. Expected total: 72 + 128 + 4·130 + 1800 + 72 = **2592**
   (verify, don't trust this number).
6. Everything that hardcodes the old sizes: `Tools/SurvivorEval/Program.cs` and any other C# in Core/Tools.
   Also edit (without being able to run them) the Unity EditMode tests that assert 2264 or branch sizes:
   `Unity/Assets/Arena/ML/Tests/Editor/BehaviorSetupTests.cs`, `SurvivorActionMapperTests.cs`,
   `Unity/Assets/Arena/View/Tests/Editor/AutoFarmRunnerTests.cs` — use the constants
   (`SurvivorObservation.Size`, `SurvivorInput.*BranchSize`) instead of literals, change nothing else in
   `Unity/Assets/Arena/ML` or `View`, and list in the Report every Unity place that depends on the skill count 4
   (the viewer skill bar etc.) so T-041 can handle it. Do NOT touch `Trainer/`.

## Hard rules
- Pure C# 9, deterministic, no allocation in `Step` / `Write` / `WriteMask`, `TreatWarningsAsErrors`.
- Gameplay with the existing content must be bit-identical to before (the schema change only widens the
  observation and the skill branch). Goldens: the Linux-running ones (`Tier1_ScriptedRun…`, `Tier1_EvaluatorRun…`)
  must pass unchanged; the Windows-only ones are not run here but must not need re-recording. If a golden
  truly depends on the observation layout (e.g. via the test brain's input size), say so in the Report and make
  the test brain adapt rather than re-recording any constant.
- Tests: new schema size and offsets, inventory 128 slots, a skill in slot 5 works end to end (mask, input, cooldown,
  observation), `none` in slots 4–5 is masked out, determinism, zero allocations, `Write` is finite and in [-1, 1].
- Files outside `Unity/Assets/Arena/Core`, `CoreTests`, `Tools/SurvivorEval` and the three Unity EditMode tests
  above are off limits. Add `.meta` for new `.cs` files inside Unity.

## Report (fill in when done)
