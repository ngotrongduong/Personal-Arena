# T-025: M5 Unity training glue — owner build, tier and Training Focus reach the agents

- **Owner:** Claude
- **Status:** done
- **Milestone:** M5
- **Parallel OK with:** T-026 only after T-023 is merged (T-026 uses `TrainingServiceClient` too;
  T-025 lands first)
- **Depends on:** T-023 (Core: `TrainingFocus`, `SurvivorRewardConfig.ForFocus`), T-024 (trainer
  forwards `--owner-build`, `--owner-tier`, `--training-focus` after `--env-args`)

> Claude (the orchestrator) has full decision authority; never ask the user anything; work until
> the task is done.

## Goal

D-029 / D-035: when the owner presses TRAIN, about 70 % of the Warrior's training episodes use the
owner's active loadout at the owner's tier (with a slight jitter), and the reward weights follow
the chosen Training Focus. Observation schema v4 and the action space do not change.

## Changes

### Training build (`Unity/Assets/Arena/ML`)

- `TrainingArenaHost` parses three optional command-line arguments (Unity receives them after
  `--env-args`):
  - `--owner-build a,b,...` — 16 integers; a wrong count or a non-integer ignores the whole
    argument with a warning; each value is clamped to `0..StatInfo.Cap(stat)` (slots ≥ 13 → 0);
  - `--owner-tier N` — clamped to 1..10, default 1; only used with a build;
  - `--training-focus id` — `TrainingFocusInfo.TryParse`; unknown → Balanced with a warning.
  - Parsing lives in a small static `OwnerTrainingArgs` class (pure, EditMode-testable) with
    `static OwnerTrainingArgs Parse(string[] args)` → `CharacterBuild OwnBuild` (null when absent)
    and `TrainingFocus Focus`. The host logs one line with what it applied.
- `HeroAgent.Configure(int agentIndex, string heroClassId, CharacterBuild ownBuild = null,
  TrainingFocus focus = TrainingFocus.Balanced)`. `Initialize` builds
  `Config.Rewards = SurvivorRewardConfig.ForFocus(focus)`; `OnEpisodeBegin` rebuilds the reward
  calculator if the focus changed after `Initialize` (the agent object is inactive while it is
  configured, so this is only a guard).
- `SurvivorEnvFactory.CreateBuild`: the owner's build branch returns a clone with its own tier and
  a **jitter**: `moves = rng.NextInt(3)` (0..2); each move takes one point from a random used stat
  that has points and gives it to a different random used stat below its cap (skipped when no
  such pair exists). The total and every cap stay valid. With `ownBuild == null` the factory
  draws exactly the same random numbers as before (existing determinism kept).

### Viewer service client (`Unity/Assets/Arena/View/TrainingServiceClient.cs`)

- `TrainingStatus` gains `training_focus` (string), `owner_build` (bool), `owner_tier` (int).
- `Start(parentPid, power, behavior, OwnerTraining owner = null)` where `OwnerTraining` holds
  `int[] Points` (16), `int Tier` and `string FocusId`. It appends
  `--owner-build a,b,... --owner-tier N` when `Points` is not null, and `--training-focus id` when
  `FocusId` is not null. A pure `static string Arguments(...)` builds the argument string so it is
  testable.
- The viewer (`SurvivorWatchController`) passes nothing yet; T-026 adds the profile and passes the
  owner's choices.

## Tests (EditMode)

- `OwnerTrainingArgsTests`: valid build/tier/focus; wrong count; clamping to caps and reserved
  slots; tier clamp; focus case-insensitive; unknown focus → Balanced; no arguments → null build,
  Balanced.
- `SurvivorEnvFactoryTests`: owner jitter keeps the total, caps and tier, moves at most 2 points,
  is deterministic per seed; a zero-point owner build stays zero; null owner build keeps the
  previous sequence.
- `TrainingServiceClientTests`: argument string with and without owner/focus; status parse of the
  three new fields (and old files without them).
- `HeroAgent` focus: a configured Gold focus gives `PerGold` = the Gold value.

## Done when

- `dotnet test CoreTests -c Release` still green; Unity EditMode tests green in batchmode.
- Training build written to `Build/TrainingNext` (the service installs it on the next start; the
  running `warrior-s001` is not touched).

## Report

Done (Claude, 2026-09-30).

- `OwnerTrainingArgs.Parse` reads the three arguments exactly as `arena_trainer.py` emits them
  (16 comma-separated ints, tier only with a build, lowercase focus id); never throws, clamps with
  warnings. `TrainingArenaHost` logs one line with what it applied.
- **Owner share is rolled first** in `SurvivorEnvFactory.CreateEpisode`: `own_build_share` 0.7 means
  ~70 % of *all* episodes use the owner's jittered build; the remaining ~30 % are split by the
  review / hard shares as before (with review 0.2, hard 0.1: 6 % review, 3 % hard, 21 % random).
  The reviewer caught that rolling it inside the New slice gave only 49–56 %. Without an owner build
  no extra random number is drawn, so the random-build sequence is unchanged.
- Jitter: 0–2 moves of one point between used stats, total / caps / tier kept, reserved slots 0,
  deterministic per episode seed.
- Focus: `HeroAgent.Configure(..., focus)` rebuilds the reward config right away (`ApplyFocus`)
  instead of an `OnEpisodeBegin` guard — same result, covered by `HeroAgentFocusTests` (Gold, then
  back to Balanced). `ForFocus(Balanced)` equals the old defaults.
- `OwnerTraining.Arguments` only sends a 16-value build (clamped to caps, reserved slots 0) and a
  known focus id, so a bad caller can never make `train_service.py` exit with code 2.
- New stat `Arena/Episode/SurvivedOwn`. Observation (2264) and actions (9/5/5) unchanged.
- Unity EditMode tests green in batchmode; `Build/TrainingNext` rebuilt (installed by the service on
  its next start; the running `warrior-s001` was not touched).
