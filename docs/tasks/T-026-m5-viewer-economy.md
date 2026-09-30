# T-026: M5 viewer — wallet, character build, tiers, Training Focus, Auto Farm, build comparison

- **Owner:** Claude subagent (unity-integrator)
- **Status:** done (orchestrator verified: EditMode 147/147, watch build, screenshots)
- **Milestone:** M5
- **Depends on:** T-023 (Core rules, merged), T-025 (training glue and `OwnerTraining`, merged)

> Claude (the orchestrator) has full decision authority; never ask the user anything; work until
> the task is done. Follow `AGENTS.md`. Do not run git commands. Do not stop, restart or touch the
> running training (`Trainer/runs/`, `Build/Training`, python processes).

## Goal

The owner never plays; they decide outside the run (GDD §3, D-019, D-028). M5 gives the "Xem AI"
viewer the whole out-of-run loop, drawn from the pure Core rules in `Core/Meta` and
`Core/Survivor` (T-023): the AI earns gold for the owner, the owner spends it on levels and stat
points, picks a loadout, a difficulty tier and a Training Focus, then presses TRAIN and watches
the AI adapt. All numbers and rules come from Core; the View only draws, saves and calls.

## Files

- New, under `Unity/Assets/Arena/View/Meta/` (with `.meta` files, fresh guids):
  - `ProfileStore.cs` — load/save `profile.json`, economy CSV.
  - `CharacterPanel.cs` — full-screen character/build panel.
  - `LoadoutComparePanel.cs` — build comparison table.
  - `AutoFarmPanel.cs` + `AutoFarmRunner.cs` — background farming.
  - `ChronicleText.cs` — Vietnamese text for `RunChronicle`.
- Edit: `View/Survivor/SurvivorWatchController.cs`, `View/Survivor/SurvivorHud.cs`,
  `View/Survivor/SurvivorHud.Build.cs`, `View/Survivor/SurvivorRenderer*.cs` only for the label
  anchor if needed, `View/Editor/WatchBuild.cs` only if a new asset must be included.
- EditMode tests under `View/Tests/Editor/`.
- This file's Report.

## Spec

### 1. Profile storage (`ProfileStore`)

- Path: `Application.persistentDataPath/profile.json`; `-profile <path>` on the command line
  overrides it (screenshot/test runs pass a scratch path so they never touch the real wallet).
- `Load()`: missing file → `ProfileRules.NewProfile()`; unreadable/corrupt JSON → keep the bad
  file as `profile.corrupt-<yyyyMMdd-HHmmss>.json`, start from `profile.bak.json` if it loads,
  else a new profile; always `ProfileRules.Sanitize`. `JsonUtility` only.
- `Save()`: atomic — write `profile.json.tmp`, then replace (`File.Replace` with
  `profile.bak.json` as backup, or move when there is no old file). Saves are cheap and happen
  after every change (buy, point, loadout, tier, focus, run recorded, farm finished).
- `AppendEconomyLine(string line)`: `economy_log.csv` beside the profile, header written first
  (`EconomyLog.Header`); IO errors are logged once and never crash the viewer.
- Pure helpers stay testable without Unity paths (constructor takes the directory).

### 2. Watch runs use the owner's build and earn gold

- The watched run uses `ProfileRules.ToBuild(warrior, profile.SelectedTier)`. The sim config is
  updated **only when a new run starts** (never mid-run); a change made in a panel shows
  "Áp dụng từ trận sau".
- `RecordResultOnce`: `ProfileRules.RecordRun(profile, warrior, activeLoadout, new RunResult
  { SurvivedSeconds, End, Gold = sim.Gold, Tier, Farm = false })`, then save and append an economy
  line (`mode = "watch"`). A run counts only when a brain played it (not when the hero stood
  waiting without a brain). The end screen shows "+N vàng vào ví" and, when a tier unlocks,
  "Mở khóa bậc N!".
- HUD top-left info gains a wallet line: "Ví: N vàng   Bậc N   Bộ: <name>   Trọng tâm: <focus>".

### 3. Spectator label and run story

- `SpectatorLabeler.Observe(sim, dt)` after every sim step (the pick step too); the current label
  is drawn as a small floating tag above the hero (world→screen, hidden when `None`), colour per
  label, fades in/out ~0.2 s.
- `RunChronicleRecorder` observes every step with the shown label; `Finish` at run end. The end
  panel gets a "Câu chuyện trận đấu" column: up to ~10 lines from `ChronicleText.Format`, e.g.
  "02:10  Nhặt Rìu xoay", "05:00  Chuyển sang THẢ DIỀU", "07:42  Suýt chết (còn 12% máu, bị vây)",
  "15:00  Trùm xuất hiện", "15:48  Hạ trùm!". Pick the most important entries when there are more
  (End, BossKilled, BossSpawned, NearDeath, ItemMaxed first). Pure formatting → EditMode tests.
- The end screen stays 5 s as now; widen or re-layout the end panel so both columns fit at
  1920×1080 and at 1280×720.

### 4. Character panel (key **C**, and a "NHÂN VẬT" button)

Full-screen overlay using the `BehaviorProfilePanel` pattern (backdrop, card, X, Esc closes,
`ConsumedEscapeThisFrame`, only one full-screen panel open at a time — extend the existing
toggle logic in `SurvivorHud`). Sections:

- **Header:** "CHIẾN BINH  Cấp N", wallet, unspent points.
- **Mua cấp:** button "Mua cấp N+1 — X vàng" (`TryBuyLevel`); disabled with the reason when gold is
  short or the level is max.
- **5 loadout tabs:** click selects the active loadout (`TrySetActiveLoadout`); a rename field
  (uGUI `InputField`, max 16 chars, `TryRename` on submit). Hotkeys must not fire while the
  field has focus.
- **13 stat rows:** Vietnamese name, short effect per point (from `StatInfo.PerPoint`, e.g.
  "+5% máu tối đa / điểm"), points / cap bar, − and + buttons (`TryRemovePoint`/`TryAddPoint`).
  "Tẩy điểm (miễn phí)" button (`ResetPoints`).
- **Bậc độ khó:** "< Bậc N >" limited to `UnlockedTier`; list of the tier's rule changes
  (`TierModifiers.For(tier)` + `DisplayName`) and the gold multiplier "Vàng ×(1 + 0,5·(N−1))".
- **Trọng tâm huấn luyện:** 5 buttons (`TrainingFocusInfo.DisplayName`) with one line each on what
  changes (e.g. Vàng: "AI được thưởng nhiều hơn khi nhặt vàng"), saved in
  `profile.TrainingFocus`.
- **Cửa hàng class:** Warrior "Đang dùng", Mage 1500 and Archer 3000 greyed with "Sắp có (M6)"
  (`ClassPrice`, `IsClassPlayable`); no buying in M5.
- Footer: "Thay đổi áp dụng từ trận xem sau. AI học theo build này ở lần HUẤN LUYỆN sau."

### 5. TRAIN uses the owner's choices

- `StartTraining` passes `OwnerTraining` (T-025): the active loadout's points and the selected
  tier **only when the Warrior has level ≥ 1 or the selected tier > 1** (a level-0 tier-1 owner
  keeps the pure random-build curriculum); the focus id always.
- The training panel shows one line from the status file while training:
  "Học theo build của bạn (bậc N) · Trọng tâm: X" or "Học build ngẫu nhiên · Trọng tâm: X". If the
  owner's current choices differ from what the running service reported, add
  "Thay đổi build/trọng tâm sẽ áp dụng ở lần HUẤN LUYỆN sau".

### 6. Auto Farm (key **F**, and a "FARM VÀNG" button)

- Panel with run count choices 10 / 25 / 50 / 100, "Bắt đầu" / "Dừng", a progress bar,
  completed/total, gold so far (`TotalGold`), estimated time left, and on finish a summary
  (runs, wins, total gold, average survival).
- Runs on **one background thread** with a `FarmSession` built from a **separate `PolicyBrain`
  instance** loaded from the same brain file bytes the viewer currently uses (never share the
  viewer pilot's brain across threads), the owner's `ToBuild(active, selectedTier)`, seeds from
  a profile-independent base (e.g. `Environment.TickCount`). The thread runs at below-normal
  priority. No Unity API on the thread.
- When the session finishes or is stopped: on the main thread, `RecordRun` for each completed
  result with `Farm = true` (gold to the wallet, loadout record), `Stats.FarmSessions++`, save,
  one economy line per run (`mode = "farm"`). Quitting the app cancels, waits ≤ 2 s for the
  thread, then records whatever finished.
- Farming needs a loaded brain; without one the start button says why. Farming keeps the
  current brain even if a newer one is hot-loaded during the session.

### 7. Build comparison (key **V**, and a "SO SÁNH BUILD" button)

- Table of the 5 loadouts (`ProfileRules.Summarize`): name, points per main stat (compact, e.g.
  "Máu 10 · Mạnh 5 · Chí mạng 5"), runs, win rate, median survival, gold/minute, best time. The
  best value per column is highlighted; the active loadout is marked. Empty loadouts show
  "chưa có trận".

### 8. Hotkeys and help

- New keys: **C** character, **F** farm, **V** compare. Existing R/Esc/Space/T/G/P/B unchanged.
  Help text updated. No key acts while an `InputField` is focused.

## Tests (EditMode)

ProfileStore (round trip, corrupt file recovery with backup, atomic save leaves a valid file,
CSV header once), ChronicleText (formatting, priority trimming), watch reward flow (a helper that
turns a finished sim into `RunResult`), OwnerTraining selection rule (level 0 tier 1 → no build;
level ≥ 1 → build), AutoFarmRunner (2 very short runs with a tiny random brain finish, record
and cancel; use the `FarmSession` test hook from T-023).

## Done when

- EditMode tests green in batchmode; `dotnet test CoreTests -c Release` untouched and green.
- Watch build to a scratch folder (`Build/WatchNext` or similar — the owner may have the viewer
  open), and screenshots with `-profile <scratch>`: character panel, compare panel, farm panel
  running, end screen with story, hero label visible. Report the paths.

## Report

**Status: done.** The subagent's shell returned no output, so the orchestrator did the
verification:

- EditMode passes 147/147. The only fix needed was replacing `Is.AnyOf` (not in this NUnit) in
  `ProfileStoreTests`.
- The watch build succeeds, and the screenshots look right.
- The Offense focus hint now mentions the kill reward (T-027, D-036).

What was built (View only; Core, observation schema v4 (2264 floats) and the 9/5/5 action space
are unchanged):

- `View/Meta/ProfileStore.cs`: loads and saves `profile.json` atomically (tmp file, then
  replace, keeping a `.bak`). A corrupt file falls back to the backup and keeps a
  `profile.corrupt-*.json` copy; with no backup it starts a new profile. Also appends
  `economy_log.csv` (header once). `-profile <folder or file>` overrides the location.
- `View/Meta/MetaViewLogic.cs`: pure helpers.
  - Sim to `RunResult` and `RunStats`.
  - `OwnerTrainingFor`: the build is sent only when level ≥ 1 or tier > 1; the focus is always
    sent.
  - Build line, "choices differ" check, `SameBuild`, wallet text, reward text, number formats.
- `View/Meta/ChronicleText.cs`: formats the Core chronicle into the Vietnamese "Câu chuyện trận
  đấu" column, trimming by priority (the End line is always kept).
- `View/Meta/AutoFarmRunner.cs`, `AutoFarmPanel.cs`:
  - Auto Farm (F) runs its own brain instance on a below-normal thread. Runs are recorded
    with Farm=true.
  - Errors are shown in the panel.
  - On quit it cancels, waits up to 2 s, then records the runs that finished.
- `View/Meta/MetaPanel.cs`, `CharacterPanel.cs` (C), `LoadoutComparePanel.cs` (V): shared panel
  base (blocks hotkeys while an InputField is focused), character/shop/loadout/tier/focus
  panel, and the build compare screen.
- `View/Survivor/SurvivorHud*.cs`:
  - A wallet line and the hero spectator label, which fades in about 0.2 s.
  - The end screen shows "+N vàng vào ví" and "Mở khóa bậc N!", plus the story column.
  - Help text updated. Esc closes the open panel first.
- `View/Survivor/SurvivorWatchController.cs`:
  - Watch runs use `ToBuild(warrior, SelectedTier)`, applied at run start. Changes made
    mid-run show "Thay đổi build: áp dụng từ trận sau".
  - `SpectatorLabeler.Observe` runs after every step, including the pick step.
    `SpectatorLabeler.Reset` and the chronicle reset run on every sim reset.
  - `RecordRun` (Farm=false) runs once per run, only if the brain played it. It then saves
    and writes an economy line with mode `watch`.
  - TRAIN passes `OwnerTraining`. The training panel shows the build line and the note that
    changes apply to the next training.
  - Debug flags for screenshots: `-openPanel character|compare|farm`, `-farmRuns N`,
    `-endShot`, `-labelShot`. `-runSeconds 60..900` works only together with `-profile`.

EditMode tests added (30, in `View/Tests/Editor`): ProfileStoreTests 7, ChronicleTextTests 7,
MetaViewLogicTests 11, AutoFarmRunnerTests 5. Results: all green (147/147 in the
project).

Verification scripts (subagent scratchpad `scratchpad\t026\`): `run-editmode.ps1`,
`run-coretests.ps1`, `build-watch.ps1` (to `Build\WatchNext`), `shots.ps1`. `shots.ps1` uses a
scratch profile and a copied brain, and writes `shots\watch_label.png`, `watch_end.png`,
`watch720_end.png`, `character.png`, `compare.png` and `farm.png`.

Known issues:

- The HUD wallet uses its own line in the info block rather than being merged into the existing
  top line.
- If a farm run is still running when the 2 s quit timeout expires, it is dropped, not recorded.
  Only finished runs are booked.
- The screenshot flags are debug-only and not documented in the in-game help.
