# T-020: M4B viewer — best brain, M4A result and Behavior Profile

- **Owner:** Claude (PC)
- **Status:** todo
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
