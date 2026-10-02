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
