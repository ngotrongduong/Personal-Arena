# T-018: M4B — behavior telemetry, champion/challenger and automatic 100-seed evaluation

- **Owner:** Codex
- **Status:** done
- **Milestone:** M4B
- **Parallel OK with:** T-020 (Unity viewer; no shared files)
- **Depends on:** T-014, T-015 (done)

> Claude (the orchestrator) has full decision authority; never ask the user anything; work until
> the task is done. Follow `AGENTS.md`. Do not run git commands. Only touch the files listed
> below. When a detail is not specified, pick the simplest option and write it in the Report.

## Goal

`docs/GDD.md` §4.6–4.7 asks for three things that do not exist yet:

1. A **Behavior Profile** measured during evaluation runs (how aggressive / cautious / greedy the
   brain plays), so the owner can see *how* the AI plays, not only how long it survives.
2. **Champion/challenger**: the best brain so far is kept safely and never overwritten; a newer
   brain (challenger) only replaces it after it wins the same 100-seed evaluation. Every previous
   champion stays on disk (rollback is always possible).
3. The training service runs that evaluation **automatically in the background** while training,
   so the owner never has to run a command.

The M4A acceptance numbers (median ≥ 600 s, P10 ≥ 420 s, no death before 180 s) come from the
existing `SurvivorEvaluator.PassesM4A` and must keep working unchanged.

## Files

- Create: `Unity/Assets/Arena/Core/Survivor/SurvivorBehaviorTracker.cs` (+ `.meta`, copy the GUID
  style of neighbouring `.meta` files with a new random 32-hex GUID).
- Edit: `Unity/Assets/Arena/Core/Survivor/SurvivorEvaluator.cs`, `Tools/SurvivorEval/Program.cs`.
- Create: `CoreTests/Survivor/SurvivorBehaviorTrackerTests.cs`; edit
  `CoreTests/Survivor/SurvivorEvaluatorTests.cs` if needed.
- Create: `Trainer/champion.py`, `Trainer/tests/test_champion.py`.
- Edit: `Trainer/train_service.py`, `Trainer/export_brain.py`, `Trainer/training_history.py`,
  `Trainer/README.md`, and tests in `Trainer/tests/`.
- Edit this file's **Report** section only.
- Do **not** touch: `Trainer/runs/` (real training data — never read-modify-write or delete
  anything there; tests use `tmp_path`), `SurvivorSim*.cs` observation/reward/step logic,
  `SurvivorObservation.cs`, `SurvivorRewardConfig.cs`, anything else under `Unity/`, `docs/`
  (except this Report), `.github/`.

## Spec

### 1. Core: `SurvivorBehaviorTracker` (pure C#, namespace `PersonalArena.Core.Survivor`)

A read-only observer of a `SurvivorSim`. It must not change the sim in any way (no calls that
mutate state, no RNG use) so evaluation results stay identical to today's for the same brain and
seed. No allocations after `Reset` (preallocate arrays), no LINQ, C# 9 compatible (Unity 6 +
`net10.0` tool), follow the style of the surrounding Core files.

API:

```csharp
public sealed class SurvivorBehaviorTracker
{
    public void Reset(SurvivorSim sim);          // after sim.Reset(...)
    public void Observe(SurvivorSim sim);        // after EVERY sim.Step(...)
    public SurvivorBehaviorStats Finish(SurvivorSim sim);
}

public sealed class SurvivorBehaviorStats   // public properties (get; set;) so System.Text.Json serializes them
{
    float Aggression, Caution, Greed, Exploration, CrowdControl, BossHunting,
          SkillDiscipline, PreferredRange, KeepDistance;
    int KickUses, BlockUses, DashUses, EffectiveKicks, EffectiveBlocks, EffectiveDashes;
}
```

`-1` means "no data" for every float metric. Rules:

- `Observe` reads the sim's per-step event list (it is cleared at the start of each `Step`, so it
  must be called after every step). Positional samples are taken on every 5th **simulated** tick
  (ticks with `sim.LastStepSeconds > 0`; level-up pause ticks do not count).
- **Aggression** (0..1): over position samples where the hero moved (`LastMove != 0`) and at least
  one alive enemy is within 12 m: share where cos(move direction, direction to the centroid of the
  alive enemies within 12 m) > 0.5.
- **Caution** (0..1): over position samples where hero HP < 35 % of max and the nearest alive enemy
  is within 6 m: share where the hero moved and cos(move direction, direction to that nearest
  enemy) < −0.5 (retreating).
- **KeepDistance** (m): mean distance to the nearest alive enemy over samples with any alive enemy.
- **PreferredRange** (m): median of the same nearest-enemy distances, computed from a fixed
  48-bin histogram over 0–12 m (distances ≥ 12 m go to the last bin); report the bin centre.
- **Greed** (0..1): `sim.DropsCollected / sim.DropsSpawned` (gold + meat), −1 if none spawned.
- **Exploration** (0..1): distinct 5 m × 5 m cells of the map the hero entered at sample time /
  total cells (`(2·MapHalfSize/5)²`, i.e. 400 for the default 100 m map).
- **Skill effectiveness** (from `SkillUsed` events; `Value` = slot, `Extra` = hits for the kick;
  slots are `SurvivorSim.BlockSlot` / `SurvivorSim.DashSlot`, the remaining active slot is the
  kick — look the kinds up via `sim.Config.ClassDef.ActiveSkills[slot].Kind`, do not hardcode):
  - kick effective if hits ≥ 1;
  - block effective if at least one `Blocked` or `Parry` event happens between that block start
    and the moment `Hero.Blocking` becomes false;
  - dash effective if the count of alive enemies within 3 m of the hero is 0.5 s (30 simulated
    ticks) after the dash start **strictly lower** than at dash start (a dash with nobody near is
    not effective). If the run ends before the 0.5 s check, ignore that dash (count neither use
    nor effect).
  - `SkillDiscipline` = total effective / total counted uses, −1 if no uses.
- **CrowdControl** (0..1): share of kicks that hit ≥ 3 enemies, −1 if no kicks.
- **BossHunting** (0..1): over position samples while the boss is alive and within 15 m and the
  hero moved: share where cos(move direction, direction to boss) > 0.5; −1 if never sampled.

Move direction: reuse whatever the sim uses to turn a move index into a direction (make an
existing private helper `internal`/`public static` if needed, without changing its behavior), or
use `Hero.Velocity` normalized when its length > 0.01 — pick one and state it in the Report.

Performance: Observe must be cheap; iterating the enemy pool once per sample (every 5 ticks) is
fine. The existing Core perf test budget must still pass in Release.

### 2. Evaluator and summary

- `SurvivorRunStats` gets `SurvivorBehaviorStats Behavior`. `SurvivorEvaluator.RunOne` creates
  one tracker per run (or reuses a per-evaluator one), calls it after each step, fills `Behavior`.
  All other run stats must be bit-identical to before for the same brain/seed/deterministic flag
  (add a test comparing against a run without the tracker if that is practical, otherwise assert
  determinism twice with the tracker).
- `SurvivorEvalSummary` gets `SurvivorBehaviorStats Behavior`: per metric the mean over runs,
  ignoring −1 values (−1 when every run is −1); counters are summed.
- `PassesM4A` is unchanged.

### 3. `Tools/SurvivorEval`

- JSON output gains: `"behavior"` inside `summary` and inside each run (via the new properties),
  plus top-level `"brain": {"name": ..., "step": ...}` (from `PolicyBrain` metadata — use whatever
  the loaded brain exposes; the header stores a behavior name and a step) and
  `"settings": {"seeds", "seed_start", "tier", "run_seconds", "deterministic"}`.
- The existing summary/run objects use public properties, serialized with their PascalCase
  names (e.g. `MedianSurvivedSeconds`). Keep existing key names stable; Python reads them. Document the exact keys in `Trainer/README.md`.
- Console print gains one line with the main behavior metrics.

### 4. `Trainer/champion.py` (Python 3.10, type hints, stdlib only + existing modules)

Storage (all under the results dir, default `Trainer/runs/`):

```
runs/champions/<Behavior>/
  champion.brain            current champion (atomic replace only when a challenger wins)
  champion.json             {run_id, step, schema_version, score, passes_m4a, summary, behavior,
                             settings, evaluated_at, brain_file}
  history/<run_id>-<step>.brain / .json   every brain that was ever champion — never deleted
  evaluations.jsonl         one line per evaluation (champion re-evals included): run_id, step,
                            score, passes_m4a, won (bool), summary, behavior, settings, time
  latest_eval.json          the newest evaluation line, pretty printed (for the viewer)
  candidates/               temporary; a losing candidate's .brain/.json may be deleted
```

Score: `score = median + 0.5 * p10 + 300 * win_rate - 60 * catastrophic_count` (seconds-based).

Decision rule for a challenger:
- no champion yet, or champion's `schema_version` ≠ current `arena_trainer.SCHEMA_VERSION` →
  challenger wins;
- if the champion's stored `settings` differ from the current evaluation settings → re-evaluate
  the champion brain first with the current settings (update `champion.json` in place, keep the
  brain), then compare;
- challenger wins if `score > champion.score + 5` **and**
  `challenger catastrophic_count <= champion catastrophic_count`.
- On a win: copy challenger into `history/`, then atomically replace `champion.brain` and
  `champion.json`. The previous champion is already in `history/` (it was copied there when it
  won). Never delete anything in `history/`.

Evaluation settings (constants): 100 seeds, seed start 1 000 000, tier 1, 900 s, deterministic,
threads `max(1, os.cpu_count() // 4)`.

Running the evaluator:
- Locate `dotnet` with `shutil.which`. If missing: log it, return a result that says
  "evaluation skipped: dotnet not found" and do nothing else (the service must keep training).
- Build once per process: `dotnet build Tools/SurvivorEval -c Release` (capture output, no
  console window on Windows: `creationflags=subprocess.CREATE_NO_WINDOW` when on Windows), then
  run `dotnet <built dll> --brain ... --out <candidates/x.eval.json> ...`. Find the dll path from
  the build output directory (`Tools/SurvivorEval/bin/Release/<tfm>/SurvivorEval.dll`) without
  hardcoding the TFM (glob).
- The evaluation subprocess must be killable (return the `Popen` or accept a cancel callback) so
  the service can stop it.
- Candidate brain: export the newest checkpoint of the run directly with
  `export_brain.export_checkpoint(...)` into `candidates/<run_id>-<step>.brain` (do not rely on
  `latest.brain`, the service overwrites it).

CLI (`python Trainer/champion.py ...`, `--results-dir` default like the other tools,
`--behavior` default `Warrior`, the behavior name in
`Trainer/config/warrior_survivor_ppo.yaml`):
- `status` — print champion + last evaluation;
- `evaluate [--run-id R]` — evaluate the newest checkpoint of the newest (or given) run and apply
  the decision rule;
- `restore <run_id>-<step>` — make a `history/` entry the champion again (rollback; logs an
  evaluations.jsonl line with `"restored": true`).

### 5. Training service integration (`train_service.py`)

- While training, in the `_watch` loop: when the newest checkpoint step of the current run is
  ≥ last evaluated step + **2 000 000** (and no evaluation is running), start an evaluation in a
  background **thread** (one at a time). The first evaluation happens at the first checkpoint
  ≥ 2M above the last one recorded in `evaluations.jsonl` for this run (0 if none).
- On stop (stop file / shutdown): kill a running evaluation subprocess, do not start new ones,
  never leave a half-written `champion.*` (atomic writes only).
- Evaluation errors are logged to the service log and published as a message; they never stop
  training.
- Status JSON (`training_service.json`) gains: `champion_step`, `champion_run`,
  `champion_median`, `champion_p10`, `champion_passes_m4a`, `last_eval_step`, `last_eval_won`,
  `evaluating` (bool). Missing values → `null`.

### 6. Scanners must ignore `runs/champions`

`runs/champions/<Behavior>/` matches globs like `runs_dir.glob(f"*/{behavior}")`. Make
`export_brain` (`discover_behaviors`, `newest_behavior_dir`, …), `training_history`
(`_newest_run_dir`, `discover_behaviors`) and `train_service` (`plan_run`, `next_run_id`) skip the
`champions` directory explicitly (one shared constant, e.g. `CHAMPIONS_DIR = "champions"` in
`champion.py` or `arena_trainer.py`). Add a test for each.

## Tests

- CoreTests (`dotnet test CoreTests -c Release` must be green, including the perf test):
  - each metric on hand-built sims/scenarios (e.g. hero moving toward a pack → Aggression 1;
    hurt hero moving away → Caution 1; no enemies → −1 values);
  - kick with hits ≥ 3 counts toward CrowdControl; block with a Blocked event is effective;
    dash effectiveness check;
  - tracker does not change the sim: same seed + brain, with and without tracker → identical
    `SurvivedSeconds`, `Kills`, `Gold`, `EndReason`;
  - summary averages ignore −1;
  - `Tools/SurvivorEval --self-test --out <tmp>` writes JSON with `summary.behavior`, `brain`,
    `settings` (run it manually and describe in the Report; a CoreTests test is not required).
- pytest (`C:\PersonalArena\.venv-ml\Scripts\python.exe -m pytest Trainer` must be green):
  - score formula and every branch of the decision rule (no champion, schema change, settings
    change → re-eval, win, lose by margin, lose by catastrophic);
  - history is never deleted; champion files replaced atomically; restore works;
  - evaluator runner with a fake `dotnet` (monkeypatch `shutil.which`/`subprocess`) including the
    "dotnet not found" skip;
  - service: triggers at +2M only, one at a time, kills the evaluation on stop, status fields;
  - scanners ignore `champions`.

## Done when

- Both test suites are green; `dotnet build Tools/SurvivorEval -c Release` succeeds with
  `TreatWarningsAsErrors`.
- `Trainer/README.md` documents `champion.py` (commands, storage layout, score rule) in English.
- Report below is filled in.

## Report

Implemented by Codex, reviewed and verified by Claude.

- `SurvivorBehaviorTracker` collects the 9 behavior metrics and 6 skill counters without changing
  the sim or its RNG; `Observe` does not allocate after `Reset`. Movement direction is
  `Hero.Velocity.Normalized()` (only when `LastMove != 0`). A dash is judged 30 simulated ticks
  after use; a dash that has not reached 0.5 s when the run ends is ignored. A block still open at
  the end of a run counts as one use, effective if a `Blocked`/`Parry` happened during it.
- `SurvivorEvaluator` attaches the profile to every run and summarizes it as the mean ignoring
  `-1`; counters are summed. `PassesM4A` is unchanged. `SurvivorEval` JSON gains `brain`,
  `settings` and lowercase `behavior`, plus a console line with the profile.
- `Trainer/champion.py`: 100-seed evaluation, score rule, every champion/challenger branch,
  champion re-evaluation when settings change, never-deleted history, atomic replacement, restore,
  CLI `status/evaluate/restore`, cancellable evaluator. The training service starts one background
  evaluation per +2M steps, cancels it on Stop, reports failures without stopping training, and
  publishes the champion/evaluation fields in the status JSON.
- The scanners in `export_brain`, `training_history` and `train_service` share `CHAMPIONS_DIR` and
  skip `runs/champions`. `Trainer/README.md` documents the CLI, storage layout, score rule and JSON
  keys.
- Claude's review change: evaluation errors go to a separate `evaluation_message` status field
  instead of replacing the training `message`.
- Verification (Claude, normal restore, no sandbox flags):
  - CoreTests Release: 80/80 pass.
  - Trainer pytest: 82/82 pass (7 known ML-Agents deprecation warnings).
  - `dotnet build Tools/SurvivorEval -c Release`: 0 warnings, 0 errors.
  - End-to-end: `champion.py` on copies of real `warrior-s001` checkpoints in a scratch runs folder,
    100 seeds each, ~7.6 min per evaluation:
    - 15,999,787 steps: median 646 s, P10 314 s, 2 catastrophic, score 682.9 → first champion.
    - 16,999,928 steps: median 632 s, P10 317 s, 1 catastrophic, score 730.2 → beat the champion
      (+47.3, fewer catastrophic deaths) and replaced it; the old one moved to `history/`.
    - Neither passes M4A yet (P10 is below 420 s).
