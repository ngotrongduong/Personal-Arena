# T-013 — Tích hợp Unity M3 (ML + trình xem)

- **Owner:** Claude (PC)
- **Trạng thái:** done
- **Milestone:** M3 (D-024)
- **Phụ thuộc:** T-011 (Core), T-012 (Trainer)

## Đã làm

**ML (`Assets/Arena/ML`)**
- `ClassRegistry`: `warrior`/`mage`/`archer` → behavior `Warrior`/`Mage`/`Archer`.
- `TrainingArenaHost` đọc `--hero-class`; `HeroAgent` đọc env param `runner_weight`,
  `brute_weight`, `spitter_weight` (Walker luôn 1) và dựng lại sim khi trộn zombie đổi.
- `EnvConfigFactory` dựng `ZombieSpawns` từ các trọng số; test EditMode cho việc này.

**Trình xem (`Assets/Arena/View`)**
- `AiArenaController`: phím **H** đổi class, phím **M** đổi kiểu trộn zombie (Chỉ Walker / Tất cả /
  Nhiều Runner / Nhiều Brute / Nhiều Spitter), cả hai cũng là nút ở góc phải trên và nhớ qua
  PlayerPrefs; tham số dòng lệnh `-class` / `-mix` để chụp ảnh. Đổi class thì nạp não mới nhất của
  behavior đó (`BrainPilot.ClearBrain`) và nút TRAIN train đúng class đang xem.
- `ArenaArtSet` + `KayKitArtSetBuilder`: look theo class (Knight / Mage + gậy / Ranger + cung) và
  theo loại zombie (Minion / Rogue / Warrior to + rìu + khiên / Skeleton Mage + gậy).
- `ArenaRenderer`: đổi model khi zombie hồi sinh thành loại khác; vẽ đạn (cầu lửa, tên, đạn độc)
  và hiệu ứng nổ, vòng băng, dịch chuyển.
- `ArenaHud`: tên và mô tả skill theo class; **icon skill tự vẽ** (`SkillIconFactory`: SDF trên
  CPU, 128 px, cache theo id skill, 12 icon); khi xem AI không hiện phím tắt (chỉ bản chơi tay
  gọi `ShowSkillKeys(true)`). Menu *Personal Arena/Export Skill Icon Sheet* xuất bảng icon ra
  `docs/images/skill-icons.png`.

**Sửa theo review của T-011/T-012**
- Spitter bị hất lùi thì không tự đi (không tự bước xuống vực để "ăn" thưởng hất vực).
- Đạn ở tick cuối chỉ bay phần tầm còn lại.
- `arena_trainer.py --hero-class mage` tự dùng `mage_ppo.yaml` khi không có `--config`.
- `conftest.py` chỉ đổi thư mục temp khi có biến môi trường `PA_PYTEST_TEMP`.
- Thêm test: Spitter lùi mà không rơi vực, Spitter không đi khi bị hất lùi, khiên mana chặn đạn
  sau lưng, test tất định có kiểm có đạn bắn/trúng.

## Kiểm tra

- CoreTests 97/97, pytest 62/62, Unity EditMode 47/47.
- Build `TrainingBuild.BuildWindows` và `WatchBuild.BuildWindows` headless thành công.
