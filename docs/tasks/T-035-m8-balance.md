# T-035: M8 balance — data-driven tuning pass

- **Milestone:** M8 (D-039)
- **Owner:** Claude (PC)
- **Branch / worktree:** `task/t035-m8-balance` at `C:\PersonalArena-wt\t035` (off `develop` c2c686b)
- **Depends on:** M7 (T-030..T-032); the Mage/Archer brains having trained a while

## Goal (owner view)

A run feels like Vampire Survivors: the hero levels up often enough to see real builds (8 levels in
15 minutes was far too slow), gold comes in at a rate that makes buying levels and classes reachable,
and the minute-15 boss is beatable, not a wall the AI learns to run from.

## Method

`Tools/SurvivorEval` gained `--set Name=Value` (repeatable): it overrides any `SurvivorTuning` field for
every run, then validates the tuning (unknown field or bad value: exit with an error). Candidate tunings
were compared on the current Warrior and Mage champion brains (`Trainer/runs/champions/<Class>`), tier 1, 900 s,
`--deterministic`, same seeds for every candidate.

### Candidates (60 seeds each)

| Config | Class | Median s | P10 s | Wins | Level | Boss dmg % | Gold/run | Kills |
|---|---|---|---|---|---|---|---|---|
| base (pre-M8) | Warrior | 1020 | 611 | 0 | 8.0 | 0.2 | 203 | 300 |
| base (pre-M8) | Mage | 546 | 327 | 0 | 8.7 | 0.0 | 86 | 550 |
| XpMul 1.5 | Warrior | 1020 | 1020 | 0 | 10.6 | 0.6 | 235 | 382 |
| XpMul 1.5 | Mage | 618 | 341 | 0 | 12.2 | 0.2 | 124 | 663 |
| XpMul 2 | Warrior | 1020 | 606 | 1 | 14.4 | 3.0 | 268 | 591 |
| XpMul 2 | Mage | 522 | 366 | 0 | 13.2 | 0.1 | 83 | 689 |
| XpMul 1.5 + BossHpMul 0.4 | Warrior | as XpMul 1.5 | | | | 1.5 | | |
| XpMul 1.5 + BossHpMul 0.4 | Mage | as XpMul 1.5 | | | | 0.6 | | |
| XpMul 2 + BossHpMul 0.3 | Warrior | as XpMul 2 | | | | 6.0 | | |
| GoldChance 0.06 | Warrior | 1020 | 730 | 0 | 8.3 | 0.5 | 240 | 336 |
| GoldChance 0.06 | Mage | 533 | 392 | 0 | 9.7 | 0.2 | 112 | 625 |

"Boss dmg %" is the evaluator's mean boss damage fraction (share of the boss's HP removed). Warrior runs end
mostly *Expired* at 1020 s: the brain survives the boss phase by kiting and never commits to it.

## Decisions

| Number | Before | After | Why |
|---|---|---|---|
| `XpMul` (new) — every gem from a kill × this | 1 | **1.5** | +2.6 levels (Warrior) / +3.5 (Mage) per run; survival up for both (Warrior P10 611 → 1020, Mage median 546 → 618). ×2 levelled more but the Mage got worse (more pick screens, more spread builds) |
| `BossHpMul` (new) — boss HP × this; tier scaling still applies | 1 | **0.4** (6 000 → 2 400 HP) | The boss is a behaviour problem (the AI avoids it), but 6 000 HP made the reward for fighting unreachable; at 2 400 the AI's current chip damage triples and training can find the win |
| `GoldChance` | 0.03 | **0.045** | ~+15–30% gold per run on top of the extra kills, so a 1 500-gold Mage is ~6–8 good runs instead of ~10 |

No change to enemy HP/damage curves, tiers, items or the reward signal: the brains keep learning on the
same schema (D-027), only the numbers of the world move.

### Final check (100 seeds, new defaults vs pre-M8)

| Tuning | Class | Median s | P10 s | Level | Boss dmg % | Gold/run | Kills |
|---|---|---|---|---|---|---|---|
| pre-M8 | Warrior | 1020 | 613 | 8.0 | 0.5 | 209 | 294 |
| **M8** | Warrior | 1020 | **760** | **10.3** | **1.4** | **240** | 348 |
| pre-M8 | Mage | 595 | 422 | 9.4 | 0.1 | 108 | 606 |
| **M8** | Mage | 604 | 395 | **12.0** | **0.7** | **124** | 670 |

Warrior: safer, +2.3 levels, +15% gold. Mage: +2.6 levels, +15% gold, median flat, P10 −27 s (more
pick screens for a brain that has trained less; expected to recover with training on the new rules).

## Tests

- New `CoreTests/Survivor/SurvivorM8BalanceTests.cs`: `BossHpMul` scales only the boss; `XpMul` scales
  the gem of a normal and an elite kill; `Validate` rejects `BossHpMul = 0` and a negative `XpMul`.
- `SurvivorM5GoldenTests` pin the pre-M8 tuning (`PreM8`) so every other rule stays bit-identical; the
  Spitter gem test multiplies by `XpMul`.
- CoreTests 239/239 green.

## Result

- Defaults changed in `SurvivorConfig.cs`; GDD boss line and pickups table updated.
- Takes effect for training from the next TRAIN press (new `Build/TrainingNext`) and in the viewer from
  the next `Xem-AI.cmd` start (new `Build/WatchNext`).
