# T-043: M9 active skills 5 and 6 for Warrior, Archer and Mage (schema v5 slots 4 and 5)

- **Milestone:** M9 (D-041; ideas in `docs/CONTENT-PROPOSAL.md` section 4)
- **Owner:** Claude subagent (`core-sim-engineer`), Claude reviews and does git
- **Branch:** `claude/serene-sagan-3a70q5` (do NOT run any git command)
- **Depends on:** T-039 (six skill slots, `none` in slots 4–5, action branch 9 / 7 / 5, observation already has the fields),
  T-036..T-042 (patterns, pools, `OldRules`/`OldConfig`). Read their Reports. **Do not change the schema.**

## Goal (owner view)

Each class gets two more active skills (the chosen skill index 5 and 6 on the action branch), so fights have more
tactical options. The numbers are starting points.

## Skills (put them in `ActiveSkills[4]` and `[5]` of each class kit; `SkillDef` fields as the existing skills)

| Class | Slot | Id | Name | Behaviour | Numbers |
|---|---|---|---|---|---|
| Warrior | 4 | `leap-slam` | Nhảy đập đất | New `SkillKind.Leap`: the hero leaps up to 5 m toward the nearest enemy within 7 m (else along the move/facing direction) at speed 20 (reuse the Dash movement), and on landing blasts a circle (stun, knockback) | blast radius 2.8 m, 40 dmg, stun 0.6 s, knockback 2.5, cooldown 8 s, energy 30 |
| Warrior | 5 | `whirlwind` | Xoáy kiếm | New `SkillKind.Whirlwind`: for 1.5 s the hero spins; every 0.15 s it damages every enemy within 2.4 m (small knockback) and moves at ×0.7 speed | 12 dmg per tick, knockback 0.5, cooldown 9 s, energy 30 |
| Archer | 4 | `caltrop-trap` | Bẫy gai | New `SkillKind.Trap`: drops a trap at the hero's position: radius 2 m, lasts 6 s, hurts enemies inside every 0.5 s and **slows** them to ×0.6 speed while inside. Max 2 traps alive (a new one replaces the oldest) | 6 dmg per tick, cooldown 10 s, energy 20 |
| Archer | 5 | `arrow-barrage` | Mưa tên | New `SkillKind.Barrage`: 9 arrows in a 50° cone toward the nearest enemy within 14 m (else the facing), each pierces 1 enemy | 18 dmg per arrow, speed 16, cooldown 7 s, energy 25 |
| Mage | 4 | `fire-wall` | Tường lửa | New `SkillKind.Wall`: a flame wall 6 m wide × 1.2 m deep, centred 3 m in front of the hero, perpendicular to the facing, lasts 4 s, hurts enemies inside every 0.4 s. Max 2 walls alive (replace the oldest) | 8 dmg per tick, cooldown 12 s, energy 35 |
| Mage | 5 | `chain-lightning` | Lôi liên hoàn | New `SkillKind.Chain`: strikes the nearest enemy within 10 m, then jumps to the nearest enemy not yet hit within 5 m, up to 6 targets; each jump deals ×0.85 of the previous; short stun | 35 dmg, stun 0.3 s, cooldown 6 s, energy 30 |

Implementation notes:
- `SkillKind` lives in `Unity/Assets/Arena/Core/Defs.cs` (shared enum): append new members at the END only (do not renumber).
  Skill execution is in `SurvivorSim.cs` (the `skill.Kind == …` chain), the mask in `SurvivorActionMask.cs`; read both.
- Traps and walls need their own pooled state (fixed arrays 2 each; never `new` in `Step`). **Slow**: add a minimal
  enemy slow (a remaining-time field and a speed multiplier applied in enemy movement) that is exactly neutral when
  no slow is active (multiply by 1f only when the flag is clear so old-rule runs stay bit-identical).
- Whirlwind: the hero keeps full control of movement; the spin state ends after 1.5 s or on death. Leap: invulnerable
  to contact hits while airborne is NOT required; keep it simple. The mask must keep working for these (energy,
  cooldown, no use while stunned or dead); a skill in progress must not block using a different one, except Leap
  (no other skill while leaping).
- Events: `SkillUsed` as for existing skills (slot and hits), plus `StrikeLanded` (id = −1 for skills, value = radius,
  as the Mage frost burst does) for each blast/tick so the viewer can draw them; chain lightning emits one
  `StrikeLanded` per target (value 0.6).
- Evaluator and tracker: `SurvivorEvaluator.SkillUses` and the behaviour tracker already have 6 counters; check that
  `SurvivorBehaviorTracker` does not assume 4 skills.
- Old rules: the `OldRules`/`OldConfig` helpers must set slots 4–5 to `none` for ALL classes (they currently only
  assume the Warrior's slot 3). Linux goldens (`Tier1_ScriptedRun…`, `Tier1_EvaluatorRun…`) must pass unchanged;
  do not re-record any constant.

## Hard rules
- Pure C# 9, deterministic (only `rng` from the sim, never when the skill is unused), allocation-free
  `Step`/`Write`/`WriteMask`, `TreatWarningsAsErrors`.
- Only edit `Unity/Assets/Arena/Core`, `CoreTests`, and this task file (new `.cs` need `.meta`). Do not edit Unity ML/View
  and do not touch `Trainer/`. No git commands. `export PATH=$PATH:/root/.dotnet; dotnet test CoreTests -c Release`.
- Tests in a new file `CoreTests/Survivor/SurvivorM9SkillsTests.cs` for every skill: it does what the table says
  (leap lands and blasts, whirlwind ticks for 1.5 s, trap slows and replaces the oldest, barrage cone and pierce,
  wall rectangle and replacement, chain jump count/decay/no repeats), mask and cooldown/energy, observation fields for
  skills 5–6 (`skill_cooldown_4/5`, `skill_allowed_4/5`, `last_skill`), determinism with all three classes using their
  new skills, zero allocation with a run that uses all six skills, and a brain upgrade is NOT needed (check that the
  action mask still blocks the new choices when the skill is `none`).

## Report (fill in when done)

**Status: review.** `dotnet test CoreTests -c Release`: 446 passed, 0 failed, 0 skipped (395 before; 51 new test cases in `CoreTests/Survivor/SurvivorM9SkillsTests.cs`). Linux goldens pass unchanged, no constant re-recorded. Unity View/ML and `Trainer/` untouched.

### What changed, per file
- `Unity/Assets/Arena/Core/Defs.cs`: `SkillKind` + `Leap, Whirlwind, Trap, Barrage, Wall, Chain` (appended, Teleport stays 7). `SkillDef` + `Count, MaxAlive, Duration, TickSeconds, Width, Depth, JumpRange, Falloff` (all 0 for old skills). Existing `SlowFactor`/`SlowSeconds` are now used (trap: enemy speed x, linger; whirlwind: hero speed x).
- `Survivor/SurvivorDefs.cs`: kit slots 4-5 for Warrior (leap-slam, whirlwind), Mage (fire-wall, chain-lightning), Archer (caltrop-trap, arrow-barrage) with the table numbers. Archer slot 3 stays `none`.
- `Survivor/SurvivorSim.Skills.cs` (new, + `.meta`): `StartLeap/LandLeap`, `StartWhirlwind/UpdateWhirlwind`, `DropTrap/UpdateTraps`, `FireBarrage`, `RaiseWall/UpdateWalls/InsideWall`, `ChainLightning`; public `Whirling`, `WhirlRemaining`.
- `Survivor/SurvivorSim.cs`: pools `traps`/`walls` (2 each, `TrapCapacity`/`WallCapacity`, public `Traps`, `Walls`), `ApplySkill` dispatches the 6 kinds, `MoveHero` calls `LandLeap` when a leap ends and scales the target speed by `SlowFactor` only while the spin is on, `Step` calls `UpdateWhirlwind/UpdateTraps/UpdateWalls` after `UpdateZones`, pools cleared in `ClearPools`.
- `Survivor/SurvivorEntities.cs`: `SurvivorTrap`, `SurvivorWall` classes; `SurvivorEnemy.SlowRemaining/SlowMultiplier`.
- `Survivor/SurvivorSim.Enemies.cs`: spawn resets the slow fields; `UpdateEnemies` multiplies speed only when `SlowRemaining > 0` (exactly neutral otherwise).
- `Survivor/SurvivorSim.Content.cs`: `ResetContentState` clears whirl state and the hazard order counter.
- `Survivor/SurvivorActionMask.cs`: no change needed (generic; `Dashing` already blocks every skill, so no other skill while leaping).
- Tests: `SurvivorM9SkillsTests.cs` (new); `SurvivorTestHelpers.OldRules` sets slots 4-5 to `none` for all classes; `SurvivorSchemaV5Tests` updated (the two tests that assumed "no real skill in slots 4-5" now use `OldRules`).
- `SurvivorBehaviorTracker`, `SurvivorEvaluator`: checked, no change (tracker looks skills up by kind, evaluator already has 6 counters).

### Decisions on unspecified details
- Leap: target within 7 m = nearest enemy (by `NearestEnemy`); travel = min(5, centre distance - hero radius - enemy radius), so it stops where bodies touch; no target = move direction, else facing, full 5 m. Moving through `Hero.Dashing`, so body separation is skipped in flight and the mask blocks all skills until landing. The blast (`StrikeLanded` Id -1, Value 2.8 x area) comes in `MoveHero` on the landing tick; `SkillUsed` is emitted at cast with hits 0 (landing hits are not known then). A stunned hero mid-leap pauses it (same as dash).
- Whirlwind: first tick 0.15 s after the cast, 10 ticks, ends on the 90th tick (cast tick counts). Hero keeps full steering at x0.7 (`SlowFactor`). `Whirling` is false once the hero is dead. Recasting restarts it (impossible with cooldown 9 s).
- Trap: lasts and ticks from the cast; first damage after 0.5 s; slow is refreshed every tick for enemies inside and lingers `SlowSeconds` = 0.1 s after they leave; several traps take the stronger (lower) multiplier. Slows bosses too (only stun excludes the boss). Trap damage has no knockback. `StrikeLanded` (Value = radius) per damage tick.
- Barrage: "pierces 1 enemy" = existing `Pierce` meaning (1 extra enemy, so an arrow hits 2); arrow radius 0.25, knockback 0.3, flight range = Range x 1.2 (as the other skill projectiles), `SkillUsed` hits = arrows launched (stops early if the 128 projectile pool is full). Source index of the arrows is `-1 - slot` = -5 / -6.
- Wall: centred 3 m ahead along the facing at cast, width across and depth along; circle-vs-rectangle test; first tick 0.4 s after the cast. `StrikeLanded` per tick: Value = half width, Extra = half depth, Point = centre (the viewer reads orientation from `sim.Walls[i].Direction`). No knockback.
- Chain: first target = nearest within 10 m (radius included), jumps measured centre-to-centre from the previous target (<= 5 m), up to 6, no repeats, x0.85 each, stun 0.3 s (boss immune as usual). The chain keeps going after a target dies. With no target in range the skill still spends energy and cooldown (same as the existing fireball/war-cry with nobody near). `SkillUsed` Extra = targets hit.
- `DurationMul` (duration charm, omni box) and `AreaMul` also apply to whirlwind/trap/wall durations and radii/size (leap, whirlwind, trap, wall), like orbit and poison pool; chain and barrage ignore both. Might and crit apply as for any skill damage (the tests turn crit off).
- Hazard order counter (`hazardOrder`) decides "oldest" (shared by traps and walls, reset per run).
- Caps: chain buffer holds at most 8 targets (`Count` is clamped to it).

### What the Unity viewer needs (nothing edited)
- HUD: skill bar of 6 slots (skip `none`; Archer has `none` in slot 3, so its bar is slots 1,2,3,5,6 with a gap), key bindings for skill values 5 and 6 (`SurvivorWatchController` currently maps 1-4), cooldown sweep from `Hero.SkillCooldowns[4/5]`.
- Icons: 6 new skill icons (leap-slam, whirlwind, caltrop-trap, arrow-barrage, fire-wall, chain-lightning). `SoundCueMap` / `SurvivorRenderer` switch on `SkillKind` and have no case for the 6 new kinds; add cues (jump + ground slam, whirl, trap click, volley, fire whoosh, lightning crack).
- Effects: leap = hero arc/shadow while `Hero.Dashing` with the Leap skill + shockwave ring on `StrikeLanded` Id -1 Value 2.8; whirlwind = spinning blades ring radius 2.4 while `sim.Whirling` (`WhirlRemaining`), ring flash per `StrikeLanded`; trap = ground spikes disc from `sim.Traps` (`Position`, `Radius`, `Remaining/Duration`), slowed enemies tint (`SlowRemaining > 0`); barrage = nine arrow projectiles (`SourceIndex` -6) in the existing projectile renderer; wall = fire strip from `sim.Walls` (`Position`, `Direction`, `Width`, `Depth`, `Remaining/Duration`); chain = lightning bolt between consecutive `StrikeLanded` points of the same tick (Value 0.6), starting at the hero.
- Skill-projectile sprites are looked up by `SourceIndex` (-1 - slot); slot 5 (-6) needs an entry.
- Unity tests that iterate `ActiveSkills` (`SoundCueMapTests`, `SurvivorViewLogicTests`) now meet real skills in slots 4-5 and may need cues for the new kinds.
- Brains: no schema bump; old brains never choose branch values 5/6 with `none`; they will start using the new skills only after retraining.

### Not verified / weak tests
- Windows-only goldens and Unity EditMode tests not run. `Step` time (0.05 ms budget) not measured; new loops are O(enemies) per active trap/wall/spin per tick (<= 2 + 2 + 1), the chain is O(6 x enemies) per cast.
- Weak: the "ready again after the cooldown" check in `NewSkill_MaskCooldownEnergyAndStun` is skipped when the hero is still mid-leap (warrior slot 4), so the leap's cooldown-ready case is covered only by `Leap_BlocksEverySkillWhileAirborne...`. The allocation test uses cooldown/energy resets from the test to force frequent casts and proves "no per-tick allocation", not that every code path ran (it asserts both new slots were used in the measured window). The determinism test compares hashes of two identical runs (and a different seed), not exact values. Barrage pierce is tested with a clean 3-enemy line, not crowds. Trap slow-stacking is only checked for "second trap keeps the slow alive", not for the min-multiplier rule. Balance numbers are the task's starting points, untuned.
