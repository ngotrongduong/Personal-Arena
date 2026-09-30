# T-023: M5 Core — economy, loadouts, tier modifiers, Training Focus, spectator labels, run story

- **Owner:** Codex
- **Status:** doing
- **Milestone:** M5
- **Parallel OK with:** T-024 (Trainer glue, different files)
- **Depends on:** T-021 (merged)

> Claude (the orchestrator) has full decision authority; never ask the user anything; work until
> the task is done. Follow `AGENTS.md`. Do not run git commands. Do not touch files outside the
> list below. When a detail is not specified, pick the simplest deterministic option, and write
> it in the Report.

## Goal

M5 is the out-of-run loop (design: `docs/GDD.md` §3.1–3.5, §5 "M5" items): a wallet of gold,
buying character levels, spending stat points in 5 named loadouts, choosing a difficulty tier,
Auto Farm, a Training Focus for TRAIN, and two viewer features computed honestly from sim data:
the spectator label and the post-run story. This task builds **all the rules in pure C#** so the
Unity UI (T-025, Claude) only draws them and saves the profile.

**Hard rule: observation schema v4 does not change** (size 2264, every offset keeps its meaning,
`Trainer/schemas/survivor_v4.json` untouched, `SurvivorObservationSchemaTests` green without
edits). Action space 9/5/5 unchanged. The default `SurvivorRewardConfig` (= Balanced focus)
keeps its current numbers. This is what lets `warrior-s001` keep learning.

## Files

- Edit under `Unity/Assets/Arena/Core/Survivor/` (namespace `PersonalArena.Core.Survivor`):
  `SurvivorConfig.cs` (tuning numbers), `SurvivorRewardConfig.cs`, `SurvivorSim*.cs`,
  `SurvivorEvaluator.cs`, `SurvivorEntities.cs`, `SurvivorDefs.cs` only where needed.
- New files (each with a `.meta`, copy a neighbour's format, fresh random 32-hex guid):
  - `Core/Survivor/TrainingFocus.cs`
  - `Core/Survivor/TierModifiers.cs`
  - `Core/Survivor/SpectatorLabeler.cs`
  - `Core/Survivor/RunChronicle.cs`
  - `Core/Meta/` folder (with its folder `.meta`), namespace `PersonalArena.Core.Meta`:
    `PlayerProfile.cs`, `ProfileRules.cs`, `FarmSession.cs`, `EconomyLog.cs`.
    Check that `CoreTests/CoreTests.csproj` compiles files from the new folder (it probably
    globs `Core/**`; if it lists folders, add this one).
- Tests: new `CoreTests/Survivor/SurvivorM5*.cs` and `CoreTests/Meta/*.cs`.
- This file's Report section.
- Nothing under `Unity/Assets/Arena/ML`, `View`, `Editor`, `Trainer/`, `Tools/` or other `docs/`.

Core must stay free of `UnityEngine`. Profile classes must be serialisable by **Unity
`JsonUtility`**: `[Serializable]` classes with **public fields** only (no properties, no
dictionaries, no nullable, no polymorphism); arrays and `List<T>` are fine; `long` is fine.

## Spec

Keep every tunable number in `SurvivorTuning` or a `const`/static readonly in the owning class,
not as literals inside logic.

### 1. Training Focus (`TrainingFocus.cs`, `SurvivorRewardConfig.cs`)

- `public enum TrainingFocus { Balanced, Survival, Gold, Boss, Offense }`.
- `public static class TrainingFocusInfo`:
  - `string Id(TrainingFocus)` → `"balanced" | "survival" | "gold" | "boss" | "offense"`;
  - `bool TryParse(string id, out TrainingFocus focus)` (case-insensitive, trims; null/empty →
    false);
  - `string DisplayName(TrainingFocus)` → `"Cân bằng" | "Sống sót" | "Vàng" | "Boss" | "Tấn công"`.
- `SurvivorRewardConfig.ForFocus(TrainingFocus focus)` returns a **new** config. Only the fields
  below differ from the defaults:

| Focus | Changes |
|---|---|
| Balanced | none (exactly `new SurvivorRewardConfig()`) |
| Survival | `HpLostPerMaxHp = 1.6`, `Death = 6`, `PerGold = 0.0005` |
| Gold | `PerGold = 0.003` |
| Boss | `BossDamage = 5` |
| Offense | `PerLevelProgress = 0.1`, `HpLostPerMaxHp = 0.7` |

- Every focus config passes `Validate()` and keeps the GDD §4.3 hierarchy. Tests per focus:
  - every term keeps its sign (same style as the existing reward sign tests);
  - `Win` > `Death` > the reward of 900 s of survival (`900 · SurvivePerSecond`) > 0 still holds
    for every focus **except** it is enough to check `Win > 900·SurvivePerSecond` and
    `Death > 0` (write exactly what you test in the Report);
  - "outcome dominates": a run that dies at 300 s after collecting 400 gold and 5 levels of
    progress scores lower than the same run that survives to 900 s (TimeUp), for every focus;
  - Balanced equals the default config field by field.

### 2. Tier modifiers (`TierModifiers.cs` + sim)

GDD §3.5 table, cumulative: tier N has every modifier with a tier ≤ N.

- `public enum TierModifier { DenserSpawns = 2, EarlyElite = 3, FastRunners = 4, LessMeat = 5,
  EarlyBrutes = 6, DoubleElites = 7, EnemyRegen = 8, BossSummonsFaster = 9, Nightmare = 10 }`.
- `public static class TierModifiers`: `bool Has(int tier, TierModifier m)` (= `tier >= (int)m`),
  `string DisplayName(TierModifier m)` (Vietnamese, from the GDD table; Nightmare text below),
  `IEnumerable<TierModifier> For(int tier)` (ascending).
- Effects (numbers go in `SurvivorTuning`):
  - **DenserSpawns:** max alive and spawns/second × 1.10 (on top of `SpawnPerTier`).
  - **EarlyElite:** one extra elite at 90 s (same elite rules as the others).
  - **FastRunners:** Runner (enemy type 1) move speed × 1.15.
  - **LessMeat:** meat drop chance × 0.5.
  - **EarlyBrutes:** in spawn phases that start at or after 60 s but before the phase where Brute
    (type 2) normally appears, Brute weight is at least 1. Phase 0–1 min is unchanged.
  - **DoubleElites:** at every scheduled elite time (including the tier-3 one) spawn 2 elites
    instead of 1 (each drops its own chest and gold).
  - **EnemyRegen:** a normal or elite enemy (not the boss) that has not been damaged for 3 s
    regains 2% of its max HP per second, up to max. Per-enemy internal field for the last hit
    time, reset on spawn, no allocations.
  - **BossSummonsFaster:** boss summon interval × 0.5.
  - **Nightmare (tier 10):** everything above, plus every enemy moves 10% faster and elites
    have +50% HP. (The GDD's "poison puddles" are replaced by this, because a puddle would need
    a new observation kind; Claude records the GDD change.) `DisplayName`: "Ác mộng: tất cả
    modifier trên, quái nhanh hơn 10%, tinh anh thêm 50% máu".
- Tier 1 behaviour must be **bit-identical** to today (existing determinism/golden tests stay
  green unchanged).

### 3. Gold sources (sim + evaluator)

- `public enum GoldSource { Normal, Elite, Boss, Chest, Filler }` and `GoldSourceCount = 5`.
- The sim keeps per-source totals: `float GetGold(GoldSource s)`; the sum always equals `Gold`
  (test). Reset with the run. No allocations in `Step`.
- `SurvivorRunStats` gains `float[] GoldBySource` (length 5) filled by `RunOne`; `Summarize`
  unchanged except it must not break.

### 4. Spectator label (`SpectatorLabeler.cs`)

Honest labels from sim data (GDD §5): `public enum SpectatorLabel { None, Kiting, Looting,
Charging, Escaping }` with Vietnamese display names `"" | "THẢ DIỀU" | "NHẶT VÀNG" | "LAO VÀO" |
"THOÁT VÂY"` (`SpectatorLabels.DisplayName`).

`public sealed class SpectatorLabeler` — call `Observe(SurvivorSim sim, float dt)` after each
`Step`; read `Current` (the shown label) and `float SecondsIn(SpectatorLabel)` (time share of the
**shown** label since `Reset()`).

Raw label each tick (hero speed `v`, unit move direction `u` when `|v| > 0.3 · max speed`, else
the raw label is `None` unless Escaping applies); `C` = centroid of active enemies within 10 m,
`n10` their count, `n3` count within 3 m; `w` = unit vector from the hero to `C`:

1. **Escaping** — (`n3 ≥ 4`, or hero HP < 35% and the nearest enemy < 4 m) and the hero moves
   with `dot(u, w) < 0`.
2. **Looting** — the nearest gold / chest / magnet / meat pickup within 6 m, and the angle between
   `u` and the direction to it is ≤ 45°.
3. **Charging** — `n10 ≥ 3` and `dot(u, w) > 0.5`.
4. **Kiting** — `n10 ≥ 3`, `dot(u, w) < −0.3`, and the hero dealt damage (`DamageDealt` event) in
   the last 1.0 s.
5. otherwise **None**.

Hysteresis: the shown label changes only after the raw label has been the same for 0.5 s of sim
time, and a shown label (other than None) stays at least 1.0 s. Constants public. Reading the
sim uses the existing public pools; allocation-free after construction.

### 5. Run story data (`RunChronicle.cs`)

`public sealed class RunChronicleRecorder` — `Reset()`, `Observe(SurvivorSim sim, SpectatorLabel
shownLabel)` after each `Step` (reads `sim.Events`), `Finish(SurvivorSim sim)` at run end, and
`RunChronicle Result`. `RunChronicle` holds `List<ChronicleEntry> Entries` in time order plus
the end summary (end reason, death cause, survived seconds, level, kills, gold, boss damage
fraction, min HP ratio and its time).

`ChronicleEntry` (struct or class): `float Time; ChronicleKind Kind; int ItemIndex; int Level;
float Value; SpectatorLabel Label; DeathCause Cause;`. Kinds and when they are added:

- `NewItem` — an item picked for the first time (from level-up or chest); `ItemIndex`, `Level=1`.
- `ItemMaxed` — an item reaches its `MaxLevel`.
- `StyleChange` — every full 60 s of run time, the label with the most shown time in that minute
  (ignoring None); added only when it differs from the previous minute's style and holds ≥ 40%
  of that minute. `Label` set.
- `NearDeath` — HP ratio drops below 0.2; `Value` = the lowest ratio reached in that dip; at most
  one per 60 s; the dip ends when HP is back above 0.4. `Cause` = the `DeathCause`-style reason
  that would apply now (surrounded when ≥ `SurroundedCount` enemies touch, else the type of the
  last enemy that hit: Brute/Boss/Projectile/Contact).
- `EliteKilled`, `ChestOpened` (`ItemIndex`, `Level` = new level, `Value` = gold),
  `BossSpawned`, `BossKilled`.
- `End` — added by `Finish` (`Value` = survived seconds, `Cause` = death cause).

Cap `Entries` at 64 (drop further `StyleChange` first, then further `NewItem`). The recorder may
allocate when adding entries (it runs only in the viewer and Auto Farm, not in training), but
not per tick otherwise. The View formats it to Vietnamese text; Core only gives data.

### 6. Profile model (`Core/Meta/PlayerProfile.cs`)

```
[Serializable] public sealed class PlayerProfile
  public int Version = 1;
  public long Gold;
  public int UnlockedTier = 1;       // 1..10
  public int SelectedTier = 1;       // 1..UnlockedTier
  public string SelectedClassId = "warrior";
  public string TrainingFocus = "balanced";
  public List<CharacterProfile> Characters = new List<CharacterProfile>();
  public ProfileStats Stats = new ProfileStats();

[Serializable] public sealed class CharacterProfile
  public string ClassId;             // "warrior"
  public int Level;                  // character level = total stat points owned
  public int ActiveLoadout;          // 0..4
  public Loadout[] Loadouts;         // always length 5
  public string BrainRunId;          // e.g. "warrior-s001"

[Serializable] public sealed class Loadout
  public string Name;                // default "Bộ 1".."Bộ 5"
  public int[] Points;               // length StatInfo.SlotCount (16); slots >= UsedCount stay 0
  public LoadoutRecord Record = new LoadoutRecord();

[Serializable] public sealed class LoadoutRecord
  public int Runs; public int Wins; public long TotalGold; public float TotalMinutes;
  public float BestSeconds; public List<float> RecentSeconds = new List<float>(); // last 20

[Serializable] public sealed class ProfileStats
  public int Runs; public int Wins; public long GoldEarned; public float BestSeconds;
  public int FarmSessions; public int FarmRuns;
```

### 7. Rules (`Core/Meta/ProfileRules.cs`, static)

- `const int LoadoutCount = 5`, `const int RecentCap = 20`, `const int MaxTier = 10`,
  `int MaxCharacterLevel` = sum of `StatInfo.Cap` over the 13 used stats (190).
- `PlayerProfile NewProfile()` — 0 gold, tier 1, one Warrior (`BrainRunId = "warrior-s001"`,
  level 0, 5 empty loadouts named "Bộ 1".."Bộ 5").
- `bool Sanitize(PlayerProfile p)` — repairs any loaded file so the rest never crashes; returns
  true when it changed something. Nulls → defaults; gold < 0 → 0; tiers clamped (1..10,
  selected ≤ unlocked); unknown focus id → "balanced"; missing Warrior → added; loadouts array
  fixed to length 5; `Points` fixed to length 16, negatives → 0, each stat ≤ its cap, slots ≥ 13
  → 0; if a loadout spends more points than `Level`, remove points from the highest stat index
  down until it fits; `ActiveLoadout` clamped; `RecentSeconds` trimmed to the last 20;
  `Level` clamped to 0..MaxCharacterLevel.
- `long LevelCost(int currentLevel)` = `floor(100 · 1.25^currentLevel)` (double math, then
  `(long)Math.Floor`). Check: 0→100, 9→745, 19→6938 or 6939 — assert whatever the exact double
  formula gives and note the GDD rounding in the Report.
- `bool TryBuyLevel(PlayerProfile p, CharacterProfile c)` — fails at max level or not enough
  gold; otherwise gold −= cost, level += 1.
- `int SpentPoints(Loadout l)`, `int UnspentPoints(CharacterProfile c, int loadout)`.
- `bool TryAddPoint(CharacterProfile c, int loadout, StatId stat)` — needs an unspent point, a
  used stat (< 13) and the stat below its cap. `bool TryRemovePoint(...)`. `void ResetPoints(c,
  loadout)` (free).
- `bool TrySetActiveLoadout(c, index)`, `bool TryRename(c, index, name)` (trim, 1..16 chars).
- `CharacterBuild ToBuild(CharacterProfile c, int tier)` — the active loadout's points + tier;
  result passes `Validate()`.
- `public sealed class RunResult { float SurvivedSeconds; EndReason End; float Gold; int Tier;
  bool Farm; }` (public fields or init properties, your choice).
- `RunReward RecordRun(PlayerProfile p, CharacterProfile c, int loadout, RunResult r)` —
  wallet += `floor(r.Gold)`; loadout record (runs, wins when `End == Won`, total gold, total
  minutes, best, recent list capped at 20); profile stats (farm runs counted separately in
  `FarmRuns`, still in `Runs`); a **win at the highest unlocked tier** unlocks the next tier
  (max 10). `RunReward { long GoldAdded; bool TierUnlocked; int UnlockedTier; }`.
- `LoadoutSummary Summarize(Loadout l)` — runs, wins, win rate, median of `RecentSeconds` (0 when
  empty), gold per minute (`TotalGold / TotalMinutes`, 0 when no minutes), best seconds.
- Class prices for the shop screen: `long ClassPrice(string classId)` → warrior 0, mage 1500,
  archer 3000, unknown −1; `bool IsClassPlayable(string classId)` → only "warrior" in M5 (the UI
  shows the others as "sắp có"). No buy method yet (M6).

### 8. Auto Farm (`Core/Meta/FarmSession.cs`)

`public sealed class FarmSession` — `FarmSession(PolicyBrain brain, CharacterBuild build, int
runCount, int baseSeed)` (1 ≤ runCount ≤ 100). Each run uses `new SurvivorConfig { Build =
build.Clone() }` and seed `baseSeed + i`, run through `SurvivorEvaluator.RunOne(brain, config,
seed, deterministic: true)`.

- `bool RunNext()` runs one match, appends its `SurvivorRunStats` to `Results`, returns false when
  done or cancelled.
- Thread-friendly progress read from another thread: `int Completed`, `float TotalGold`,
  `bool IsDone`, `void Cancel()` (volatile / `Interlocked`; `Results` is only read after
  `IsDone`). One `FarmSession` is used by one worker thread; document that.
- A test runs 2 short matches with a tiny random `PolicyBrain` (build one the way existing
  evaluator tests do) and checks counts, gold sum and cancel. Use `RunSeconds` via a config hook
  if needed to keep the test fast (add an optional `Func<SurvivorConfig>` or `runSeconds`
  parameter; your choice, write it in the Report).

### 9. Economy log (`Core/Meta/EconomyLog.cs`)

GDD §3.1 "measure first": `static string Header` and `static string Line(DateTime utc, string
mode, string classId, int tier, string loadoutName, CharacterBuild build, SurvivorRunStats s)`
→ one CSV line, invariant culture, fields: time ISO-8601, mode (`watch`/`farm`), class, tier,
loadout name (commas replaced by spaces), survived s, end reason, death cause, level, kills,
gold total, gold per source ×5, total XP, gold per minute, then the 13 stat points joined by `;`.
The View appends it to a file; Core only formats.

### 10. Determinism, allocations, performance

- Same seed + same inputs → identical run at every tier (test tier 1, 5, 10).
- `Step` and `SurvivorObservation.Write` stay allocation-free (existing tests stay green; add one
  at tier 10 with all modifiers active).
- The existing performance test must pass in **Release**. Do not relax budgets.

## Tests (CoreTests)

At least one focused test per behaviour:

- Focus: ids/parse/display names; field values per focus; Balanced == default; the sign and
  "outcome dominates" tests above.
- Tier modifiers: `Has`/`For`; each effect measurable in a short sim (denser spawns count, extra
  elite at 90 s at tier 3 not tier 2, runner speed at 4, meat chance at 5 (statistical or via the
  tuning value used), brute weight at 60 s at 6, two elites at 7, regen after 3 s idle at 8 not
  7, summon interval at 9, Nightmare speed/HP at 10); tier-1 golden/determinism unchanged.
- Gold sources: sum equals `Gold` after a real run; chest gold goes to `Chest`, level-up filler to
  `Filler`, boss gold to `Boss`.
- Labeler: construct scenes with `SpawnEnemyForTests` / `SpawnPickupForTests` and set hero
  velocity through inputs: surrounded + moving out → Escaping; gold ahead within 6 m → Looting;
  moving into a group → Charging; moving away from a group while hitting → Kiting; hysteresis
  (a raw flip for < 0.5 s does not change `Current`).
- Chronicle: NewItem on first pick, ItemMaxed, NearDeath once per dip with the lowest ratio,
  StyleChange per minute rule, End entry, cap at 64.
- Profile rules: NewProfile shape; Sanitize repairs each broken case listed; LevelCost values;
  TryBuyLevel gold/level; add/remove/reset points with caps and unspent limit; ToBuild validates;
  RecordRun wallet/record/stats/tier unlock (only when winning at the highest unlocked tier, and
  never above 10); Summarize median and gold/min; ClassPrice.
- FarmSession and EconomyLog as above (CSV field count = header field count).

## Done when

- From the worktree root: `dotnet test CoreTests -c Release` is green (use `--no-restore` if the
  sandbox blocks NuGet; say so in the Report). Debug build must at least compile.
- `Trainer/schemas/survivor_v4.json` is untouched and its test passes.
- The Report lists what changed, test counts, and every decision taken on an unspecified detail.

## Report

(Codex fills this in.)
