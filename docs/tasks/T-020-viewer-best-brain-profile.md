# T-020: M4B viewer — best brain, M4A result and Behavior Profile

- **Owner:** Claude (PC)
- **Status:** done
- **Milestone:** M4B
- **Parallel OK with:** T-018 (Python/Core only)
- **Depends on:** T-018 (file formats of `Trainer/runs/champions/<Behavior>/`)

## Goal

The owner only watches (D-019). After T-018 the trainer keeps a **champion** brain and
evaluates newer brains on 100 seeds. The viewer must show that without any command:

1. **Newest ↔ Best brain**: key `B` switches the watched warrior between the newest training
   brain (`runs/<run>/Warrior/latest.brain`, today's behavior) and the champion
   (`runs/champions/Warrior/champion.brain`). The info box says which one is playing. The choice
   is remembered (PlayerPrefs). If no champion exists yet, `B` shows a notice and stays on newest.
2. **AI profile screen**: key `P` (and a HUD button next to TRAINING DATA) opens a full-screen
   overlay in the style of `TrainingHistoryPanel`:
   - Best brain: run, step, M4A **PASS/FAIL** (median ≥ 600 s, P10 ≥ 420 s, no death < 180 s),
     median / P10 / win rate, when it was evaluated;
   - Last evaluation: step, won/lost against the champion, its median/P10;
   - **Behavior Profile** bars (0–100 %): Aggression, Caution, Greed, Exploration, Crowd control,
     Boss hunting, Skill discipline, plus preferred range and keep-distance in metres, and the most
     common death causes. Each bar shows the change against the previous champion
     (newest `history/*.json` that is not the current champion) when there is one.
   - Empty state when nothing has been evaluated yet ("appears after 2M training steps").
3. `BrainLocator` explicitly skips the `champions` folder when scanning runs.

## Files

- `Unity/Assets/Arena/View/ChampionInfo.cs` (new): `[Serializable]` JsonUtility models of
  `champion.json` / `latest_eval.json` + loader (pure, EditMode-testable).
- `Unity/Assets/Arena/View/BehaviorProfilePanel.cs` (new): the overlay.
- `Unity/Assets/Arena/View/BrainLocator.cs`, `Survivor/SurvivorWatchController.cs`,
  `Survivor/SurvivorHud.cs`: key `B`, key `P`, button, info text.
- EditMode tests under `Unity/Assets/Arena/View/Tests/Editor/`.

## Done when

- Unity batchmode compile + EditMode tests green; WatchBuild succeeds; a screenshot of the profile
  screen with a real or fixture champion looks right.

## Report (Claude, 2026-09-30)

- `BrainLocator`: `ChampionsDirectoryName`, `ChampionDirectory`, `FindChampionBrain`; the newest-brain
  and newest-run scans skip `runs/champions`.
- `ChampionInfo` (new): JsonUtility models of `champion.json` / `latest_eval.json` / `history/*.json`.
  Death causes are parsed with a regex because JsonUtility has no dictionaries; keys may be names or
  enum numbers, and `None` is dropped. The previous champion is the newest history file (by write
  time) that is not the current champion. Loading never throws.
- `BehaviorProfilePanel` (new): full-screen overlay, in the style of `TrainingHistoryPanel`. It has
  cards for the best brain (M4A badge, median / P10 / wins / early deaths against their targets),
  the last evaluation (won or kept the old champion) and death causes. It also shows 7 play-style
  bars with a change against the previous champion, plus range, skill use and XP/gold per minute.
  It reloads every 5 s while open and has an empty state before the first evaluation.
- HUD: the data button is split into "BIỂU ĐỒ HỌC (G)" and "HỒ SƠ AI (P)"; each closes the other.
- `SurvivorWatchController`:
  - Key `B` switches newest ↔ best brain and remembers the choice in PlayerPrefs `WatchBestBrain`.
    It shows a notice when no champion exists yet, or when `-brain` fixes the brain.
  - The info box says which brain is playing.
  - The history panel follows the champion's run.
  - New arguments: `-best` and `-showProfile` (with `-screenshot`, for checking the build).
- `WatchBuild`: an optional `-watchBuildOutput <dir>` builds a test copy while the owner has the
  viewer open.
- Extra files outside the list: `Editor/WatchBuild.cs` (above) and `Survivor/SurvivorHud.Build.cs`
  (the split button).
- Reviewer (approve) fixes:
  - The trainer copies `champion.brain` before `champion.json`, so the viewer re-reads the json on
    later polls until its step matches the loaded brain.
  - When `B` cannot load the champion (rejected or unreadable), the viewer returns to newest and
    says so. `B` flips based on what is actually playing, not the saved choice.
  - A record whose summary has no runs is rejected.
- Verification:
  - EditMode 86/86, including 8 new tests (`BrainLocatorTests` champions skip and lookup, and
    `ChampionInfoTests`).
  - The WatchBuild test copy built successfully.
  - Screenshots from the T-018 end-to-end champion folder show the profile screen and the info box
    while the best brain plays ("NÃO GIỎI NHẤT warrior-s001").
