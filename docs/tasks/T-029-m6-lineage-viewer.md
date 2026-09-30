# T-029: M6 brain lineage — viewer panel "Lịch sử não"

- **Owner:** Claude subagent (unity-integrator)
- **Status:** done (verified by the orchestrator 2026-10-01)
- **Milestone:** M6 (D-037)
- **Depends on:** T-028 (store layout and `Trainer/brain_lineage.py` CLI, written in parallel)

> Claude (the orchestrator) has full decision authority; never ask the user anything; work until
> the task is done. Follow `AGENTS.md`. Do not run git commands. Do not stop, restart or touch the
> running training (`C:\PersonalArena\Trainer\runs\`, `Build\Training`, python processes). Work
> only in the worktree `C:\PersonalArena-wt\t028`.

## Goal

GDD §5 "Lịch sử não": the owner sees every saved brain version, compares two, watches any of them
right away, renames and pins them, and starts a new branch from one (or clones a branch) to train
it in another direction. M6 acceptance: "Quay lại một não cũ và chơi được ngay".

## Store (read from disk; written by T-028 except `labels.json`)

Read `docs/tasks/T-028-m6-lineage-trainer.md` for the exact JSON. Summary:
`<runs>/champions/<Behavior>/lineage/versions/<id>/{brain.brain, checkpoint.pt?, version.json}`,
`lineage/branches.json` (parents; Python writes), `lineage/labels.json` (names and pins; **only
the viewer writes it**, atomically: tmp file then replace, like `ProfileStore`). `version.json`
uses the same `summary` and `behavior` objects as `champion.json`, so reuse `ChampionSummary`,
`ChampionBehavior` and the death-cause parser from `View/ChampionInfo.cs`.

A **branch** is a run folder `<runs>/<run_id>/` with the current schema (`BrainLocator
.RunSchemaVersion == CurrentSchemaVersion`) and a `<Behavior>` folder; `champions` and folders
starting with `.` are never branches. A version's `checkpoint.pt` must exist on disk for "Rẽ
nhánh" (do not trust `has_checkpoint` alone).

## Files

- New `View/Lineage/` (with `.meta` files, fresh guids):
  - `LineageStore.cs` — pure reader/writer: `LineageIndex Load(runsDirectory, behavior)` (branches,
    versions, labels, parents; never throws; skips folders without `version.json` or
    `brain.brain`), label save (rename version/branch, pin/unpin), default names (branch
    `warrior-s001` → "Nhánh 1"; version → "Bước 97,0 tr"), sorting (versions newest first), and
    `TrainRunId(runsDirectory, behavior, profileRunId)`: the profile's run only when
    `<run>/<Behavior>/checkpoint.pt` exists and the schema is current, else null.
  - `LineageCommand.cs` — runs `"<repo>/.venv-ml/Scripts/python.exe" "<repo>/Trainer/brain_lineage.py"
    --results-dir "<runs>" --behavior <B> <args>` hidden (no window), one at a time, async; parses
    the last stdout line as JSON (`ok`, `error`, `run_id`, `id`); a timeout of 15 min for a
    snapshot with evaluation, 2 min otherwise; exposes state for the panel. Uses the same paths
    as `TrainingServiceClient`.
  - `BrainLineagePanel.cs` — full-screen panel (the `MetaPanel` pattern from T-026: backdrop, card,
    X, Esc closes, blocks hotkeys while an `InputField` is focused, only one full-screen panel
    open at a time).
- Edit: `View/TrainingServiceClient.cs` (`Start` takes an optional run id → ` --run-id <id>`),
  `View/Survivor/SurvivorWatchController.cs`, `View/Survivor/SurvivorHud*.cs`,
  `View/BrainLocator.cs` if a helper is needed.
- EditMode tests under `View/Tests/Editor/`.
- This file's Report.

## Panel (key **L**, and a "LỊCH SỬ NÃO" button next to the other panel buttons)

On open, start `LineageCommand sync` in the background (imports the champion history the first
time) and reload the index when it finishes; show what is on disk meanwhile.

- **Left: Nhánh.** One row per branch: name, `run_id`, current step (newest numbered checkpoint
  name or `latest.brain` step), "rẽ từ <parent name> · bước N" when it has a parent, badges
  "ĐANG DÙNG" (profile `BrainRunId`) and "ĐANG HỌC" (training status `run_id` while training is
  active). Buttons: "Dùng nhánh này" (sets the profile's `BrainRunId`, saves the profile; the next
  TRAIN continues this branch; if training runs another branch, say "Áp dụng ở lần HUẤN LUYỆN
  sau"), "Đổi tên" (InputField, max 24 chars), "Nhân bản" (`fork --run-id <run>`; on success name
  the new branch "<old name> (bản sao)", make it the active branch), "Lưu phiên bản hiện tại"
  (`snapshot --run-id <run>`; shows "Đang lưu và chấm điểm... (vài phút)").
- **Right: Phiên bản** of the selected branch (the active branch at open), newest first, with a
  scroll view. Row: name, step ("Bước 97,0 tr"), date, median survival (mm:ss), win %, gold/min,
  mean level, badges ★ "GIỎI NHẤT" (it is the current `champion.json` brain) / "từng giỏi nhất"
  (`champion` true) / 📌 ghim / "chỉ xem" (no `checkpoint.pt`). Unevaluated rows show "chưa chấm".
  Buttons per row: "Xem ngay", "So sánh", "Ghim"/"Bỏ ghim", "Đổi tên", "Rẽ nhánh" (disabled with
  the reason when there is no `checkpoint.pt`: "Bản này chỉ còn não để xem, không học tiếp được").
  "Rẽ nhánh" → `fork --version <id>`; on success name the branch "Nhánh từ <version name>", make it
  active and select it.
- **Compare (bottom or overlay):** pick A and B with "So sánh" (third click replaces the older
  pick). Two columns with the better value highlighted: Sống (trung vị), Sống (10% tệ nhất), Tỉ lệ
  thắng, Vàng/phút, EXP/phút, Cấp TB, Máu mất/phút (lower is better), Sát thương trùm, Điểm; then
  behavior bars A vs B using the Vietnamese trait names already used by the Behavior Profile panel
  (P). "chưa chấm" when a side has no evaluation.
- Errors from the CLI are shown in the panel in Vietnamese (the CLI already returns Vietnamese).

## Watching a version ("Xem ngay")

- The controller gets a "watched version" override: `PollBrain` loads that version's
  `brain.brain` (same validation as today) and starts a fresh run immediately (`RestartNow`). The
  info line shows "Não: <version name> (<branch name>)". A notice: "Đang xem phiên bản <name> —
  bấm B để về não mới nhất".
- **B** while a version is watched → back to the newest brain (clears the override). Otherwise B
  keeps toggling newest ↔ best as now. `-brain` still wins over everything.
- The override is not saved across restarts.

## Newest brain follows the active branch

"Não mới nhất" = `<runs>/<BrainRunId>/<Behavior>/latest.brain` when that file exists with the
current schema, else today's `BrainLocator.FindNewestBrain`. Auto Farm keeps using the bytes of
whatever brain is loaded (unchanged).

## TRAIN continues the active branch

`StartTraining` passes `LineageStore.TrainRunId(runs, behavior, profile.BrainRunId)` to
`TrainingServiceClient.Start` (null → no `--run-id`, the service picks as today).

## Help

Help text gains "L lịch sử não". No hotkey acts while an InputField is focused.

## Tests (EditMode)

LineageStore: load a temp tree (branches with/without parents, versions with/without `.pt`, a
folder without `version.json` ignored, labels applied, champion marker), default names, label
save round trip (atomic, keeps other entries), `TrainRunId` rules (missing run, old schema,
no `checkpoint.pt` → null); TrainingServiceClient argument with and without a run id;
LineageCommand JSON parsing (ok, error, garbage). Compare "better" rules (lower is better for
damage taken).

## Done when

- EditMode tests green in batchmode (the orchestrator runs them if your shell cannot).
- The orchestrator builds `Build/WatchNext` and takes screenshots with a scratch `-runs` tree and
  `-profile`; add `-openPanel lineage` to the existing debug flag so the panel can be captured.

## Report

Written without a Unity editor (no compile or test run in the subagent shell); the orchestrator
compiles, runs EditMode tests and builds.

**New files** (all under `Unity/Assets/Arena/View/`, each with a `.meta`):
- `Lineage/` folder.
- `Lineage/LineageStore.cs`: DTOs for `version.json`, `branches.json`, `labels.json`; the
  `LineageIndex` model (branches, versions newest first, labels); `Load` (never throws; a
  version needs `version.json` with id and run_id plus its brain file; `HasCheckpoint` = its
  `checkpoint.pt` exists; `IsCurrentChampion` = champion.json run_id and step match;
  branches = current-schema run folders with a `<Behavior>` folder, plus version-only runs with
  `OnDisk = false`; parents from `branches.json`); default names ("Nhánh 1", "Bước 97,0 tr");
  `TrainRunId` (run exists, current schema, `<Behavior>/checkpoint.pt`, else null);
  `BranchLatestBrain`; label writes (`RenameVersion`, `SetPinned`, `RenameBranch`) that re-read
  `labels.json`, keep other entries and save atomically (`.tmp` then `File.Replace`). Python's
  `NaN` / `Infinity` are read as 0.
- `Lineage/LineageCommand.cs`: runs `Trainer/brain_lineage.py` (sync / snapshot / fork) in a
  hidden process, one at a time, with async stdout capture and a timeout (2 min; 15 min for
  snapshot, which evaluates). `ParseResult` takes the last JSON line that has "ok".
- `Lineage/LineageCompare.cs`: the compare rules: 9 metrics, labels, formatting, and `Better`
  (lower is better only for damage taken; an unevaluated side never wins).
- `Lineage/BrainLineagePanel.cs` (a `MetaPanel`, 1800×1000 on the 1920×1080 reference canvas,
  so it fits both 1920×1080 and 1280×720):
  - **Status line:** shows busy / message / hint.
  - **Left, "NHÁNH":** a scroll list of branches.
    - Badges: ĐANG DÙNG, ĐANG HỌC, ĐANG CHỌN, "chỉ còn phiên bản đã lưu".
    - Info: parent ("rẽ từ X · bước N") and step count.
    - Buttons: Dùng nhánh này / Đổi tên / Nhân bản / Lưu phiên bản hiện tại.
    - A rename bar sits below the list (max 24 characters; Enter saves, Esc cancels, empty restores the default name).
  - **Right, versions of the selected branch:** a scroll list, newest first.
    - Badges: GIỎI NHẤT, "từng giỏi nhất", ĐÃ GHIM, "chỉ xem", SO SÁNH A/B.
    - Stats line: survival, win rate, gold/min, level, or "chưa chấm".
    - Buttons: Xem ngay / So sánh / Ghim–Bỏ ghim / Đổi tên / Rẽ nhánh.
    - "Rẽ nhánh" is disabled without `.pt`, with the reason "Bản này chỉ còn não để xem, không học tiếp được".
    - Old-schema versions cannot be watched or forked.
  - **Bottom right, compare card:** 9 metrics A/B, with the better cell in gold and "chưa chấm" for unevaluated versions, plus 7 play-style bars for A (blue) and B (orange). A third pick replaces the older one.
  - **On open:** runs `sync` in the background.
  - **Fork / clone:** gives the new branch a name ("Nhánh từ …" / "… (bản sao)") and makes it the active branch.
  - **Snapshot:** shows the result.
  - **Command buttons are disabled** while a command runs or when Python or the script is missing (then the panel is view-only).

**Changed files:**
- `Survivor/SurvivorHud.cs` and `Survivor/SurvivorHud.Build.cs`:
  - Build the lineage panel.
  - Add a "LỊCH SỬ NÃO (L)" button in the training panel (`TrainingHeight` 506).
  - `ToggleLineagePanel()` opens only when the panel is bound to a runs folder.
  - Escape and one-panel-at-a-time handling.
- `Survivor/SurvivorWatchController.cs`:
  - **Keys and help:** the L key and the help line "L lịch sử não".
  - **Screenshots:** `-openPanel lineage`.
  - **Brain priority:** a watched-version override. The order is `-brain` > version > champion (B) > newest.
  - **"Xem ngay":** loads the version brain and starts a fresh run. On rejection it falls back and shows why.
  - **B:** B leaves the version and goes back to the newest brain.
  - **Info box:** shows "Não: <version> (<branch>) … Phiên bản đã lưu".
  - **Newest brain:** now the active branch's `latest.brain` when valid, else the newest run as before.
  - **TRAIN:** passes `LineageStore.TrainRunId(...)` as `--run-id`.
- `TrainingServiceClient.cs`:
  - `ServiceArguments(..., runId)` appends ` --run-id <id>` only for safe ids (letters, digits, `-_.`, ≤ 64 characters, not starting with `.` or `-`).
  - Adds `IsSafeRunId` and `RunIdArgument`.
- `BehaviorProfilePanel.cs`: public `TraitCount` / `TraitName` / `TraitColor` / `TraitValue` (null-safe) for the compare bars.

**Tests** (`Tests/Editor/`):
- New: `LineageStoreTests.cs`, `LineageCommandTests.cs`, `LineageCompareTests.cs`.
- Added three run-id tests to `TrainingServiceClientTests.cs`.

**Not compiled yet (please check first):**
- The lookbehind regex for NaN in `LineageStore`.
- `ProcessStartInfo.StandardOutputEncoding` / `StandardErrorEncoding`.
- `File.Replace(temp, path, null, true)`.
- `InputField.wasCanceled` / `SetTextWithoutNotify`.

**UX notes:**
- Rename also commits when the owner clicks elsewhere, as uGUI end-edit does.
- Badges are colored text (no ★/📌 glyphs, because the legacy font lacks them).
- The lineage button is in the training panel, like the G/P buttons.

## Orchestrator verification (2026-10-01)

- **Compile and tests:** everything compiled. Unity EditMode: 180/180 passed. Trainer pytest: 145/145 passed.
- **Build:** the WatchBuild of the worktree succeeded.
- **Screenshots** (scratch `-runs` tree and profile):
  - the lineage panel at 1920×1080 and at 1280×720, with no text overflowing or overlapping;
  - `-watchVersion warrior-s001-49999825`, a new debug flag that starts on a saved version as "Xem ngay" does. The info box shows "Não: Bước 50,0 tr (Nhánh 1) … Phiên bản đã lưu".
- **Added by the orchestrator:**
  - While open, the panel re-reads the store every 15 s, so versions saved by training show up.
  - `-watchVersion <id>`.
- **Reviewer fixes:**
  - The viewer refuses to edit `labels.json` while it exists but cannot be read, so other pins are not dropped.
  - The fallback when `File.Replace` is refused now goes through `labels.json.bak`, not delete + move.
  - Temp names are unique.
  - A command timeout stops the whole process tree (`taskkill /T /F`), so the evaluator child stops too.
  - A `warning` from the CLI (for example, saved but not scored) is shown in the panel.
- **Matching fixes on the Python side** (T-028): see that file's report.
