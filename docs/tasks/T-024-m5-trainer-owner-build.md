# T-024: M5 trainer — train on the owner's build, tier and Training Focus

- **Owner:** Claude (PC)
- **Status:** done
- **Milestone:** M5
- **Parallel OK with:** T-023 (Core only)
- **Depends on:** nothing (T-025 wires the Unity side)

## Goal

D-029: about 70 % of the character brain's training episodes use the owner's own build (with a
slight jitter), the rest stay random. The viewer's TRAIN button passes the owner's choices to the
training service, which passes them to Unity.

## Changes

- `Trainer/arena_trainer.py`
  - `--owner-build` (16 comma-separated stat points, each 0..20, normalised), `--owner-tier`
    (1..10, default 1 when a build is given) and `--training-focus`
    (`balanced|survival|gold|boss|offense`, case-insensitive).
  - `build_command` forwards them after `--env-args` (still one final marker).
  - Constants `STAT_SLOTS`, `MAX_STAT_POINTS`, `TRAINING_FOCUSES`, `OWNER_BUILD_SHARE = 0.7`.
- `Trainer/train_service.py`
  - Same three arguments; `trainer_command` forwards them.
  - `environment_overrides(args)` returns `{own_build_share: 0.7}` when an owner build is given.
  - `effective_config(..., overrides)` writes `<run>.service.yaml` when it extends `max_steps`
    **or** applies an override (and always writes `max_steps` as an integer).
  - `status()` adds `training_focus`, `owner_build` (bool) and `owner_tier`; the start of each
    service run logs the owner settings.
- Without the new arguments everything behaves exactly as before (the running `warrior-s001`
  service is unaffected).

- `effective_config` refuses (ValueError) to override a parameter that is a curriculum, because
  `--resume` would then restore a lesson number that no longer exists.
- `status()["owner_tier"]` is 0 when no owner build is given (the tier only reaches Unity with a
  build).

## Which run learns the owner's build (D-035)

The reviewer pointed out that the service resumes the newest run, so the owner's build and focus
train `warrior-s001`, which is also the class's base brain. D-029 imagined a separate character
brain started with `--initialize-from`. Decision for M5: **the owner's Warrior brain is
`warrior-s001` itself** (`CharacterProfile.BrainRunId`). There is one owner and one Warrior on
this machine, and `--initialize-from` would restart the step counter and the `run_seconds`
curriculum at 3-minute runs for no visible gain. The 30 % random-build episodes keep the brain
general, so a later build change still needs only a little extra learning. A separate character
run (with its own champion folder) is added when a second character of the same class or a
shipped base brain exists (M6+). The curriculum never goes back a lesson, so a focus that lowers
the reward scale can only slow the next lesson, not undo one.

## Tests

`Trainer/tests/test_arena_trainer.py` and `test_train_service.py`: pass-through order, default tier
(parser and service), focus without a build, invalid values rejected, override written without
extending, override already set (no file written), curriculum override refused, override plus
extension, no override without a build, status fields. `pytest Trainer/tests`: 118 passed.

## Report

Done by Claude. `test_install_staged_build_swaps_in_new_build` failed once in a full run and passed
on the rerun and in isolation (file timing on Windows); unrelated to this task.
