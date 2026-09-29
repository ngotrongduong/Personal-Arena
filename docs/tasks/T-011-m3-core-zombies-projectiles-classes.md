# T-011: M3 Core — new zombies, projectiles, Mage and Archer skills (rules v3)

- **Owner:** Codex
- **Status:** review
- **Milestone:** M3
- **Parallel OK with:** T-012 (Python only, no shared files)
- **Depends on:** none

> Claude (the orchestrator) has full decision authority; never ask the user anything; work until
> the task is done. Follow `AGENTS.md`. Do not run git commands. Do not touch files outside the
> list below. When a detail is not specified, pick the simplest deterministic option, and write
> it in the Report.

## Goal

After this task, the pure C# sim (`Unity/Assets/Arena/Core`) has:
- three new zombie types (Runner, Brute, Spitter), including a ranged zombie AI;
- a pooled projectile system used by both zombies and the hero;
- the missing hero skill kinds (`Projectile`, `AreaBurst`, `Teleport`);
- two new hero classes, Mage and Archer;
- an observation layout of 883 floats, with `ArenaSim.RulesVersion = 3`.

Nothing under `Unity/Assets/Arena/ML` or `Unity/Assets/Arena/View` changes in this task. Claude
integrates those afterwards.

## Files

- Edit: `Unity/Assets/Arena/Core/Defs.cs`, `ArenaSim.cs`, `Entities.cs`, `SimEvent.cs`,
  `RaySensor.cs`, `ObservationBuilder.cs` (only if its XML doc or size comment mentions 811/11),
  `RewardConfig.cs`, `RewardCalculator.cs`, `ArenaConfig.cs` (only if validation needs it).
- Create, only if it keeps `ArenaSim.cs` readable: `Unity/Assets/Arena/Core/Projectiles.cs`
  (for example `ProjectileState` and helpers). A new `.cs` file under `Assets/` needs a `.meta`.
  Copy the format of a neighbouring `.meta` and use a fresh random 32-hex-char guid.
- Edit and create tests under `CoreTests/` (the existing test project).
- Do not touch anything else. In particular, leave `Unity/Assets/Arena/ML`, `View`, `Editor`,
  `Trainer/` and `docs/` alone.

## Spec

### 1. Rules version

`ArenaSim.RulesVersion = 3`. Update its doc comment: v3 = new zombie types, projectiles,
observation 883.

### 2. Definitions (`Defs.cs`)

Add `public enum ZombieBehavior { Melee, Ranged }`.

**`ZombieTypeDef` new fields:**
- `Behavior` (default `Melee`)
- `KnockbackResist` (0..1, default 0). Every push distance applied to this zombie is multiplied by
  `(1 - KnockbackResist)`. This covers kick, parry pushback, block stagger pushback, area burst
  and projectile knockback.
- `PreferredDistance` (ranged only)
- `ProjectileSpeed`, `ProjectileRadius`, `ProjectileRange` (ranged only)

**`SkillDef` new fields:**
- `bool Pierce`: a projectile passes through zombies and hits each zombie at most once.
- `bool DashBackward`: a `Dash` goes in the direction of `-facing` and ignores the move input.
- `bool BlockAllDirections`: a `Block` covers attacks from every angle, not only the front 90°.

**New `DefaultDefs` zombies** (all numbers are starting balance and may be tuned later):

| Id | TypeIndex | Behavior | MaxHp | MoveSpeed | Turn | Radius | Damage | AttackRange | Arc | Windup | Cooldown | Other |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| walker (unchanged) | 0 | Melee | 100 | 2.2 | 200 | 0.45 | 20 | 1.3 | 60 | 0.5 | 1.2 | |
| runner | 1 | Melee | 50 | 4.2 | 360 | 0.4 | 12 | 1.2 | 60 | 0.3 | 0.8 | |
| brute | 2 | Melee | 300 | 1.6 | 120 | 0.7 | 45 | 1.8 | 90 | 0.9 | 1.8 | KnockbackResist 0.6 |
| spitter | 3 | Ranged | 70 | 2.4 | 240 | 0.45 | 15 | 10 (start windup at or under this distance) | 20 (must face the hero within ±10°) | 0.6 | 2.5 | PreferredDistance 7, ProjectileSpeed 9, ProjectileRadius 0.3, ProjectileRange 12 |

Add `DefaultDefs.Runner()`, `Brute()`, `Spitter()`, and `DefaultDefs.ZombieTypes()`. The last one
returns the four types in TypeIndex order as a new array on each call.

**New `DefaultDefs` hero classes.** Keep `Warrior()` unchanged, except that `DashBackward`,
`BlockAllDirections` and `Pierce` stay false. Each skill has a readable `Id`; the HUD will show it.

`Mage()`:
- **Class stats:** Id "mage", MaxHp 80, MoveSpeed 4.3, Acceleration as Warrior, Turn as Warrior,
  Radius as Warrior, MaxEnergy 120, EnergyRegenPerSec 18.
- **Slot 0 "fireball":** Projectile. Damage 40, ProjectileSpeed 14, ProjectileRadius 0.35,
  Range 16, Cooldown 0.8, EnergyCost 12, AreaRadius 1.5 (splash).
- **Slot 1 "frost-nova":** AreaBurst. AreaRadius 4, Damage 10, SlowFactor 0.4, SlowSeconds 3,
  Knockback 1.5, Cooldown 6, EnergyCost 30.
- **Slot 2 "mana-shield":** Block. EnergyCost 10, EnergyPerSecond 25, BlockMoveMultiplier 0.6,
  BlockDamageMultiplier 0.25, BlockAllDirections true, ParryWindowSeconds 0,
  BlockStaggerSeconds 0, BlockPushback 0.
- **Slot 3 "blink":** Teleport. DashDistance 6, Cooldown 5, EnergyCost 25.

`Archer()`:
- **Class stats:** Id "archer", MaxHp 85, MoveSpeed 5.0, Acceleration as Warrior,
  TurnSpeedDegPerSec 600, Radius 0.42, MaxEnergy 100, EnergyRegenPerSec 16.
- **Slot 0 "arrow":** Projectile. Damage 22, ProjectileSpeed 24, ProjectileRadius 0.15, Range 20,
  Cooldown 0.4, EnergyCost 0.
- **Slot 1 "piercing-arrow":** Projectile. Pierce true, Damage 45, ProjectileSpeed 30,
  ProjectileRadius 0.15, Range 22, Cooldown 3, EnergyCost 20.
- **Slot 2 "leap-back":** Dash. DashBackward true, DashDistance 4, DashSpeed 16, Cooldown 3,
  EnergyCost 15.
- **Slot 3 "concussive-arrow":** Projectile. Damage 10, ProjectileSpeed 20, ProjectileRadius 0.2,
  Range 18, Knockback 4, StunSeconds 1.0, Cooldown 5, EnergyCost 20.

Add `DefaultDefs.HeroClass(string id)`. It returns a new def for "warrior", "mage" or "archer",
ignores case, and throws `ArgumentException` for anything else.

Slot layout note: the action mask and the ML side treat slots generically. Do not assume slot
2 is Block anywhere in Core.

### 3. Projectiles

`ProjectileState` (class, preallocated pool) has these fields:
- `Active`, `Id` (monotonic per episode), `FromHero` (bool), `SourceZombieId` (−1 for the hero)
- `Position`, `Velocity`, `Radius`, `Damage`, `RemainingSeconds`
- `Pierce`, `ulong HitMask` (bit = zombie Id; `ArenaConfig` already caps ZombieCount at 64)
- `Knockback`, `StunSeconds`, `SlowFactor`, `SlowSeconds`, `AreaRadius`
- `SkillSlot` (hero slot index 0..3, or −1 for a zombie; the View uses this to pick the look)

Pool rules:
- The pool has 128 slots and is allocated in the constructor. There are no per-step allocations.
- `Reset` deactivates every slot.
- Public accessor: `public IReadOnlyList<ProjectileState> Projectiles`. The doc comment says that
  only `Active` entries are in flight.
- If the pool is full, the spawn is skipped (deterministic). The hero's energy and cooldown are
  still spent.

Spawning:
- Direction: the hero's facing, or for a zombie, from the zombie to the hero's position at the
  moment of firing (no lead).
- Start position: `owner position + dir * (ownerRadius + projectileRadius)`.
- `RemainingSeconds = range / speed`.
- Zombie projectiles use speed `ProjectileSpeed * Config.SpeedMultiplier` and damage
  `AttackDamage * Config.DamageMultiplier`. The lifetime is computed so that the travelled
  distance stays `ProjectileRange`.
- Emit `ProjectileFired` with Value = projectile Id and ZombieId = source zombie id (−1 for the
  hero).

Update: add a new step phase `UpdateProjectiles()`, called in `Step` after the zombie loop and
before `ResolveAllOverlaps()`. For each active projectile, in index order:
- Move by `Velocity * dt`.
- Test the swept segment from the old to the new position against circles. Use the combined
  radius (projectile + target) so fast arrows cannot tunnel. For several candidates, take the
  earliest hit along the segment; tie-break by lower zombie Id.
- **Hero projectile** hits alive zombies whose bit is not in `HitMask`:
  - Apply `DamageZombie(Damage)`.
  - Emit `ProjectileHit` with Value = damage removed and the zombie id.
  - If still alive: apply knockback along the projectile direction (distance `Knockback`, scaled
    by KnockbackResist). Apply stun the way `PerformKick` does, including resetting the attack
    phase. Apply slow.
  - Splash (`AreaRadius > 0`): every other alive zombie within `AreaRadius + its radius` of the
    impact point takes `Damage * 0.5`.
  - Non-pierce projectiles deactivate on the first hit. Pierce projectiles set the bit and
    continue.
  - Kills keep using the existing `DamageZombie` → `KillZombie`/potion path.
  - Projectile hits are not backstabs.
- **Zombie projectile** only hits the alive hero:
  - If the hero is blocking and the block covers the direction (frontal ≤90° like melee, or
    `BlockAllDirections`), the projectile is destroyed, emits `ProjectileBlocked`, and does no
    damage. It never parries or staggers.
  - Otherwise the hero takes the flat damage: emit `HeroDamaged` with the source zombie id and
    kill the hero at 0 HP, as melee does. There is no from-behind multiplier and no
    `HeroDamagedFromBehind` for projectiles.
  - The projectile deactivates either way.
- Deactivate when `RemainingSeconds <= 0`. Projectiles fly over the abyss freely; only the
  lifetime ends them.

### 4. Zombie AI (`UpdateZombie`)

The knockback, stun, slow and windup code stays shared. `Behavior` only changes the movement and
attack-start part:
- **Melee:** exactly as today.
- **Ranged:**
  - Face the hero with the usual turn speed.
  - Let `d` be the distance to the hero.
    - If `d > PreferredDistance + 1`: move toward the hero.
    - If `d < PreferredDistance - 1.5`: move directly away from the hero. The existing
      `KeepOnPlatform` pass stops it at the rim. It must never walk off by itself.
    - Otherwise: stand still.
    - All movement uses `MoveSpeed * SpeedMultiplier * slow`.
  - When `AttackPhase == Idle`, `d <= AttackRange`, and the facing is within
    `AttackArcDegrees / 2` of the hero direction: enter `Windup` for `AttackWindupSeconds`.
    Standing still during the windup is the telegraph the hero learns to read.
  - At the end of the windup: fire a zombie projectile (instead of calling `ResolveZombieAttack`),
    then `Recover` for `AttackCooldown`. Ranged zombies may move while recovering, like melee.

### 5. Respawn re-roll

In the respawn branch of `UpdateZombie`, assign `zombie.Def = ChooseZombieType()` before calling
`ResetZombie(zombie, true)`, so the mix stays at the configured weights over a long episode.

### 6. Hero skills

`UseSkill` no longer throws for any kind.
- **Projectile:** spawn a hero projectile from the skill fields above, with `SkillSlot = slot`.
- **AreaBurst:** every alive zombie within `AreaRadius + zombie radius` of the hero:
  - takes `DamageZombie(Damage)`;
  - if still alive: slow (`SlowFactor` / `SlowSeconds`, keeping the stronger remaining slow),
    pushed away from the hero by `Knockback` (scaled by KnockbackResist), and stunned if
    `StunSeconds > 0`.
  - The existing `SkillUsed` event is enough; no new event.
- **Teleport:**
  - Direction: the move direction if it is non-zero, else the facing.
  - `safe = Config.Radius - HeroDef.Radius - 0.3`.
  - Distance `t = min(DashDistance, RaySensor.EdgeDistance(pos, dir, Center, safe))`. This is 0
    if the hero is already outside `safe`.
  - Set `Position += dir * t` and cancel any dash.
  - Emit `HeroTeleported` with Value = t.
  - A teleport never ends over the abyss.
- **Dash with `DashBackward`:** direction = `-Vec2.FromAngle(Hero.Facing)`, ignoring the move
  input. `MoveDashing` must use the speed of the dash skill that started the dash. Store it, for
  example in a `HeroState.DashSpeed` field, instead of calling `ActiveSkill(SkillKind.Dash)`,
  which is fine today but fragile.
- **Block** (`ResolveZombieAttack`):
  - `covered = IsBlocking && (block.BlockAllDirections || frontal ≤ 90°)`.
  - A parry only happens when `block.ParryWindowSeconds > 0`. Today a zero window still parries on
    the first tick; fix that.
  - Mana-shield: damage × 0.25 from any side, with no parry or stagger (already expressible).
  - Use the block skill the hero is actually holding. Do not use a different block def by mistake.

### 7. Events (`SimEvent.cs`)

Append (do not reorder existing values): `ProjectileFired`, `ProjectileHit`, `ProjectileBlocked`,
`HeroTeleported`. Make sure the events list capacity still avoids per-step allocation in normal
play: pre-size it for zombies plus projectiles.

### 8. Rewards

`RewardConfig.KnockOffBonus = 0.5f`, applied on `ZombieFell`.
- This is on top of the `Kill` reward the fall already gives through `ZombieKilled`.
- Only knockbacks, which are always hero-caused, move zombies off the platform.
- All other new events give no reward in this task.

### 9. Observation

`RaySensor` gets one new category, "enemy projectile", placed last:
- Layout: `[none, edge, zombie0..N-1, potion, enemyProjectile]`, so
  `CategoryCount = ZombieTypeCount + 4`.
- `SizePerRay = CategoryCount + 4 = 12` with 4 zombie types.
- Total observation: 19 + 72 × 12 = **883**.
- Expose `EnemyProjectileCategory`.
- Only active zombie projectiles are sensed. Use hit radius `max(projectile.Radius, 0.4)`.
- The three zombie flags are 0 for a projectile hit.
- Update the XML doc comment.

The hero features (19) stay the same. They must not depend on the class; check that nothing in
`ObservationBuilder` assumes Warrior (for example "slot 2 is block"). If something does, make it
generic by `SkillKind` and note it in the Report.

### 10. Invariants (AGENTS.md)

The code must:
- stay deterministic for the same seed, config and inputs;
- use no `UnityEngine`;
- make no allocations in `Step` after construction (pool and lists preallocated);
- keep the existing Warrior behaviour for existing tests, except where this spec changes it
  (observation size, respawn re-roll).

## Tests (must add, in CoreTests)

- `Runner_IsFasterThanWalker`: after 1 s of chasing, the runner has covered more ground.
- `Brute_ResistsKnockback`: a kick moves a brute about 40% of the distance it moves a walker
  (±15%).
- `Spitter_KeepsDistance`: in a 1-zombie spitter arena, after 5 s the spitter's distance to an
  idle hero is between 5 and 9 m, and the spitter never falls.
- `Spitter_ProjectileHitsHero`: an idle hero gets `HeroDamaged` from a spitter projectile.
- `Spitter_ProjectileBlockedByFrontalShield`: a Warrior holding block toward the spitter gets
  `ProjectileBlocked` and loses no HP.
- `Spitter_ProjectileHitsWhenBlockingAway`: blocking with the back to the spitter still takes
  damage.
- `Respawn_RerollsZombieType`: with a 50/50 walker/runner mix and respawn on, over many kills
  both types appear after respawns.
- `Mage_FireballDamagesAndSplashes`: the direct target takes 40; a neighbour within the splash
  takes 20.
- `Mage_FrostNovaSlowsAndPushes`: nearby zombies are slowed and pushed away; far zombies are
  untouched.
- `Mage_BlinkStaysOnPlatform`: blinking toward the rim from near the edge ends inside the
  platform, and `HeroTeleported` is emitted.
- `Mage_ManaShieldReducesFromBehindAndNeverParries`: a hit from behind while shielding does
  0.25× damage and emits no `Parry`.
- `Archer_ArrowHitsDistantZombie`: an arrow kills or damages a zombie 15 m away within its
  lifetime.
- `Archer_PiercingArrowHitsTwoInLine`: two zombies in a line both take damage, and each is hit
  only once.
- `Archer_LeapBackMovesOppositeFacing`.
- `Archer_ConcussiveArrowStunsAndPushes`.
- `FastProjectile_DoesNotTunnel`: a 30 m/s arrow against a small zombie still hits.
- `Observation_SizeIs883` (update the existing 811 test), and a test that an enemy projectile on
  a ray sets `EnemyProjectileCategory`.
- `Reward_KnockOffBonus_IsPositive`: a `ZombieFell` event gives +KnockOffBonus. Add it to the
  reward-sign tests.
- `Determinism_MixedZombiesAndProjectiles`: two sims with the same seed, all 4 zombie types,
  Mage or Archer, and the same scripted inputs produce identical states over 600 steps.
- `NoAllocations_StepWithProjectiles` if the project already has an allocation test pattern;
  otherwise skip it and note that in the Report.
- `DefaultDefs_HeroClass_ById` (valid ids, case-insensitive, unknown throws).

Update existing tests whose expectations legitimately change: observation size, and any test
that asserted `NotSupportedException` for these skill kinds.

## Done when

- [ ] `dotnet test CoreTests` passes with no warnings. Run it with
  `dotnet test C:\PersonalArena\CoreTests` (a single plain command).
- [ ] AGENTS.md §6 invariants hold (determinism, no UnityEngine in Core, reward signs tested).
- [ ] The Report below is filled in.

## Report (filled by the implementer)

- Changed files:
  - `Unity/Assets/Arena/Core/Defs.cs`
  - `Unity/Assets/Arena/Core/ArenaSim.cs`
  - `Unity/Assets/Arena/Core/Entities.cs`
  - `Unity/Assets/Arena/Core/Projectiles.cs` and `Projectiles.cs.meta`
  - `Unity/Assets/Arena/Core/SimEvent.cs`
  - `Unity/Assets/Arena/Core/RaySensor.cs`
  - `Unity/Assets/Arena/Core/RewardConfig.cs`
  - `Unity/Assets/Arena/Core/RewardCalculator.cs`
  - `Unity/Assets/Arena/Core/ArenaConfig.cs`
  - `CoreTests/M3CoreTests.cs`
  - `CoreTests/SensorAndObservationTests.cs`
  - `CoreTests/RewardTests.cs`
  - `CoreTests/ConfigAndDeterminismTests.cs`
- Test result: `dotnet test C:\PersonalArena\CoreTests` - 94 passed, 0 failed, 0 skipped; no warnings.
- Notes / open questions:
  - Added the 128-slot preallocated projectile pool in its own Core file to keep `ArenaSim.cs`
    navigable. A full pool deterministically skips spawning after energy/cooldown are spent.
  - Slow effects retain the lower (stronger) factor; equal-strength applications extend to the
    longer remaining duration, while weaker effects do not replace stronger ones.
  - `ObservationBuilder` already treated all four slots generically, so it required no change.
  - No allocation-test pattern exists in `CoreTests` (the existing performance test measures only
    elapsed time), so `NoAllocations_StepWithProjectiles` was skipped as directed. Projectile
    objects and event capacity are preallocated; `Step` adds no normal-path heap allocations.
  - No open questions.
