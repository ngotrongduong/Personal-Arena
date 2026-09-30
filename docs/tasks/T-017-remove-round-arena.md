# T-017 — Dọn code đấu trường tròn cũ

- **Owner:** Claude (PC) + subagent `unity-integrator`
- **Trạng thái:** doing
- **Milestone:** M4A (D-026: chế độ Survivor thay hẳn đấu trường tròn)
- **Phụ thuộc:** T-014, T-015, T-016 (đã xong trên `feature/survivor-core`)

## Mục tiêu

Sau T-016, huấn luyện và trình xem chỉ còn dùng `SurvivorSim`. Code của đấu trường tròn cũ
(`ArenaSim`, sàn tròn, vực, quan sát 883, luật v3) không còn ai dùng. Task này xoá phần đó để repo
chỉ còn một chế độ chơi.

## Phạm vi (agent tự quyết chi tiết bằng cách biên dịch và chạy test)

1. **Core** (`Unity/Assets/Arena/Core`, ngoài thư mục `Survivor/`):
   - Xoá: `ArenaSim` và các file chỉ nó dùng (dự kiến `ArenaConfig`, `ObservationBuilder`,
     `RaySensor`, `RewardCalculator`, `RewardConfig`, `BrainPilot`, `HeroActionMask`, `HeroInput`).
   - Giữ: file mà Survivor, `PolicyBrain` hoặc `Tools/SurvivorEval` vẫn dùng (`Rng`, `Vec2`,
     `PolicyBrain`, `Defs`, `Entities`, `Projectiles`, `SimEvent`...).
   - Tách phần còn dùng khỏi file sắp xoá nếu cần.
   - Xoá test cũ tương ứng trong `CoreTests`.
2. **View** (`Unity/Assets/Arena/View`):
   - Xoá: `AiArenaController`, `KeyboardArenaController`, `ArenaRenderer`, `ArenaHud`,
     `ArenaStage`, `TopDownCamera`.
   - Xoá thêm những file chỉ các file trên dùng (`ArenaSpace`, `ArenaStats`, `InputLatch`...) và test
     tương ứng.
   - Giữ những gì trình xem Survivor dùng (`ArenaEffects`, `ArenaArtSet`, `CharacterAnimator`,
     `SkillIconFactory`, `TrainingHistory*`, `TrainingServiceClient`, `UiSprites`, `FxAssets`...).
3. **Scene và editor:**
   - Xoá scene chơi tay `ArenaPlay.unity`: owner không chơi tay (D-019 / D-023).
   - Bỏ các builder cũ trong `PlaySceneBuilder`, chỉ giữ builder Survivor.
4. **ML** (`Unity/Assets/Arena/ML`): bỏ phần còn trỏ tới luật cũ, nếu có.
5. **Trainer:**
   - Xoá config luật v3 (`warrior_ppo.yaml`, `warrior_ppo_randomized.yaml`, `mage_ppo.yaml`,
     `archer_ppo.yaml`). Mage/Archer sẽ có config Survivor riêng ở M6.
   - `train_service` và test phải chạy đúng khi chỉ còn `warrior_survivor_ppo.yaml`.
   - Mage/Archer: host huấn luyện đã tự chuyển về Warrior. Service xử lý behavior Mage/Archer rõ
     ràng, không crash.
   - Bỏ mọi chỗ còn đọc `rules_version.txt`.
6. **Không đụng tới:**
   - `Trainer/runs/`, `Build/Training/`: dịch vụ huấn luyện đang chạy.
   - `ProjectSettings/ProjectSettings.asset`.
   - Các file `docs/` khác ngoài phần Report của task này. Claude tự cập nhật AGENTS.md, STATUS,
     BOARD và DECISIONS.

## Nghiệm thu

- `dotnet test CoreTests` xanh.
- `Tools/SurvivorEval` build được.
- pytest `Trainer` xanh.
- Unity EditMode xanh.
- `WatchBuild.BuildWindows` thành công.
- Grep không còn `ArenaSim`, `RulesVersion`, `rules_version` trong `Unity/Assets`, `CoreTests`,
  `Tools` và `Trainer` (trừ `Trainer/runs`).
- Không build lại bản huấn luyện: Claude tự làm sau khi dừng dịch vụ.

## Report

- Đã xóa (kèm `.meta`):
  - Core: ArenaConfig, ArenaSim, BrainPilot, HeroActionMask, HeroInput, ObservationBuilder,
    RaySensor, RewardCalculator, RewardConfig, Entities, Projectiles, SimEvent.
  - View: AiArenaController, KeyboardArenaController, ArenaRenderer, ArenaHud, ArenaStage,
    TopDownCamera, ArenaStats, InputLatch và test của chúng.
  - Scene `ArenaPlay.unity` cùng menu "Build Play Scene".
  - 10 file test cũ trong CoreTests và 4 config yaml cũ (warrior/randomized/mage/archer).
- Giữ lại / di chuyển:
  - `Defs.cs` chỉ còn `SkillKind` + `SkillDef` (icon kỹ năng dùng).
  - `ArenaSpace` giữ lại vì SurvivorRenderer dùng `ToWorld` và `YawDegrees`.
  - Hàm `Bucket` chuyển từ ArenaHud sang `TrainingHistory`.
- Entities, Projectiles, SimEvent cũng bị xóa (spec định giữ) vì không còn code nào dùng.
- Bỏ `rules_version`: run không có `schema_version.txt` được coi là schema 1.
  `history.json` ghi `schema_version`.
- Dịch vụ huấn luyện: Mage/Archer báo lỗi rõ "arrives in M7" và không tạo thư mục run.
  PLAN.md ghi M7, spec ghi M6; code theo M7.
- EditorBuildSettings giờ trỏ tới `ArenaWatch.unity`.
