# T-034: M8 complete build — settings, identity, and a full-loop smoke test

- **Milestone:** M8 (D-039)
- **Owner:** Claude subagent (unity-integrator) + Claude (PC)
- **Branch / worktree:** `task/t034-m8-build` at `C:\PersonalArena-wt\t034` (off `develop` after T-033 is merged)
- **Depends on:** T-033 (audio settings and `SurvivorAudio`)

## Goal (owner view)

The viewer feels like a finished game: it is called "Personal Arena" (window title, exe icon), shows its
version in a corner, and has a **Cài đặt** (settings) panel — volumes, window/fullscreen, graphics quality,
mute-in-background, and a **Thoát game** button. The M8 acceptance "File exe chạy trọn vòng chơi" is
checked by the exe itself: `-smokeTest` plays the whole loop for all three classes and writes a pass/fail
report.

## Work

1. **Settings panel** (key `O`, plus a small gear button in a HUD corner; Esc closes it like other panels):
   - Âm lượng chung / Nhạc / Hiệu ứng sliders, Tắt tiếng, Im khi cửa sổ ở nền (from T-033's settings).
   - Chế độ hiển thị: Cửa sổ / Toàn màn hình (borderless), saved and applied at start.
   - Chất lượng đồ họa: Thấp / Vừa / Cao (Unity quality levels; keep the current one as "Cao" default).
   - Hiện FPS (optional small counter).
   - **Thoát game** with a confirm step; quitting while training runs keeps today's behaviour
     (`OnApplicationQuit` asks the service to stop and save).
   - Pure logic (labels, clamping, defaults, prefs round trip) EditMode-tested; settings in `PlayerPrefs`.
   - Add "O cài đặt" to the help text.
2. **Identity at build time** (`View/Editor/WatchBuild.cs`, temporary PlayerSettings that are restored after
   the build, like the existing ones — `ProjectSettings.asset` must not change in git):
   - productName is already "Personal Arena" and companyName "ngotrongduong": **keep both unchanged**, because
     `Application.persistentDataPath` (`%USERPROFILE%\AppData\LocalLow\ngotrongduong\Personal Arena`) holds the
     owner's `profile.json`.
   - Icon: a 256 px PNG made from the game's existing CC BY icons (game-icons.net, credited) or a KayKit
     render; set for Standalone.
   - `bundleVersion` = `0.8.<git commit count>` (run `git rev-list --count HEAD` from the editor; fall back
     to `0.8.0`), shown small in a screen corner as "Personal Arena v0.8.N".
3. **`-smokeTest <report.json>`** (implies `-mute`; requires `-profile <scratch>` and `-brain <file>`; refuse
   to run against the real profile):
   - For Warrior, then Mage, then Archer (buy/select via `ProfileRules` in the scratch profile, granting
     the gold needed): short runs (`-runSeconds 60`-style), speed x8, play until the run ends.
   - Check and record per class: run started, at least one level-up offer shown and picked, run ended
     with an end reason, gold booked into the profile and saved, the next run started.
   - Also open and close every panel once (C, F, V, L, G, P, O) and start a 2-run Auto Farm.
   - Any `LogType.Error`/`Exception` during the test = fail. Time limit (e.g. 6 min) = fail.
   - Write `{ "passed": bool, "classes": [...], "errors": [...], "seconds": n }` and quit with exit code
     0 when passed, 1 when not.
4. **Launcher polish:** `Trainer/watch_ai.ps1` passes nothing new; but if the viewer exits with an error
   right at start, show a Vietnamese message with the `Player.log` path instead of closing silently.

## Checks

- EditMode + CoreTests green; scratch build; `-smokeTest` report passed for all three classes; screenshot of
  the settings panel and the version label.
- The build must not move the owner's profile folder (verify `Application.persistentDataPath` before/after).
- Do not touch `Build/Watch`, `Build/Training`, `Trainer/runs/` or running training.

## Result

Done on `task/t034-m8-build` (2026-10-01).

- **Settings panel** (`View/Meta/SettingsPanel.cs`, pure logic in `View/ViewerSettings.cs`): key `O` or the gear
  button (bottom-right), Esc/X closes. Volumes (chung / nhạc / hiệu ứng, 10% steps), Tắt tiếng, Im khi cửa sổ ở
  nền, Cửa sổ / Toàn màn hình (borderless, applied at start), Chất lượng Thấp / Vừa / Cao (Unity levels 1 / 3 / 5;
  "Cao" = today's Ultra), Hiện FPS (bottom-left counter), Thoát game with a confirm step (`Application.Quit`, so
  `OnApplicationQuit` still stops and saves training). Saved in `PlayerPrefs` (`Viewer.*` keys). Automated runs
  (screenshot / smoke) never change the window mode. Help text has "O cài đặt".
- **Identity** (`View/Editor/WatchBuild.cs`, all temporary and restored after the build): `bundleVersion`
  `0.8.<git rev-list --count HEAD>` (fallback `0.8.0`), app icon `View/Art/AppIcon.png` (256 px, game-icons.net
  "spinning-sword" by Lorc + "two-coins" by Delapouite, CC BY 3.0, credited in `ThirdParty/CREDITS.md`) for the
  default and every Standalone size. productName / companyName untouched: the built exe logs
  `persistentDataPath C:/Users/ngotr/AppData/LocalLow/ngotrongduong/Personal Arena` (unchanged). The corner
  label reads "Personal Arena v0.8.102" on the check build.
- **`-smokeTest <report.json>`** (`View/SmokeTestReport.cs`, `SurvivorWatchController.Smoke.cs`): implies mute,
  refuses without `-profile` / `-brain` or with a profile inside the real profile folder (checked: exit 1 with a
  "refused" report). Starts a 2-run Auto Farm, then buys (granting gold) and selects Warrior, Mage, Archer
  (`<class>.brain` next to `-brain` is used per class), 60 s runs at x8, checks start / offer / pick / end
  reason / gold booked and re-read from disk / next run; then opens and closes C, F, V, L, G, P, O; any
  Error/Assert/Exception log or 6 min = fail. Check build: **passed** for all three classes in 312 s, exit 0,
  0 problem lines in Player.log. Pass `-runs <empty scratch folder>` so L / G / P bind.
- **Launcher:** `Trainer/watch_ai.ps1` shows a Vietnamese message with the `Player.log` path and waits for
  Enter when the viewer exits with an error within 15 s of starting.
- **Tests:** CoreTests 236 passed; EditMode 289 passed (16 new: `ViewerSettingsTests`, `SmokeTestReportTests`).
- Known limits: 60 s runs earn little gold (Warrior and Mage booked +0, Archer +4), so "gold booked" checks
  the wallet arithmetic and the saved file rather than a large amount. A Unity build writes
  `Assets/Resources/PerformanceTestRun*.json` (performance-testing package); delete it after a build, never commit.
