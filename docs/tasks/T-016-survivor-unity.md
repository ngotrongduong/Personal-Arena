# T-016 — Tích hợp Unity cho chế độ Survivor (ML + trình xem)

- **Owner:** Claude (PC) + subagent `unity-integrator` cho trình xem
- **Trạng thái:** in progress
- **Milestone:** M4A (D-026, D-030)
- **Phụ thuộc:** T-014 (Core Survivor), T-015 (Trainer)

## Phần ML (`Assets/Arena/ML`) — Claude

- `HeroAgent` chạy `SurvivorSim`:
  - quan sát v4 có 2264 số;
  - 3 nhánh hành động rời rạc 9 / 5 / 5: di chuyển, skill, chọn nâng cấp.
- Mỗi trận đọc các tham số môi trường:
  - `run_seconds`;
  - `tier_min` / `tier_max`;
  - `build_level_max`;
  - `own_build_share`.
- Khi game đang chờ AI chọn nâng cấp, agent gọi `RequestDecision()` để chọn ngay ở bước kế tiếp.
- Chỉ số TensorBoard `Arena/*` lấy từ `SurvivorEpisodeStats`:
  - thời gian sống, cấp, vàng, số quái giết;
  - nguyên nhân chết;
  - số lần dùng mỗi skill;
  - cấp cuối của từng món đồ.
- `SurvivorActionMapper`, `SurvivorEnvFactory`, `BehaviorSetup` và `ClassRegistry` được viết lại cho
  Survivor. Mage và Archer chưa có bộ Survivor (sẽ có ở M7), nên host huấn luyện tự chuyển về Warrior.
- Test EditMode:
  - mapper và mask;
  - factory tạo build;
  - `BehaviorSetup` đúng 2264 số và 9 / 5 / 5.

## Trình xem (`Assets/Arena/View`) — subagent `unity-integrator`

Mục tiêu: owner mở `Xem-AI.cmd` và thấy AI Warrior chơi Survivor 15 phút, kèm đầy đủ thông tin.

1. **Bộ điều khiển xem** (`SurvivorWatchController`, thay `AiArenaController` trong scene Play):
   - tạo `SurvivorSim` và chạy `SurvivorPilot` ở tốc độ thật; tốc độ xem x1 / x2 / x4 / x8;
   - khi trận kết thúc, hiện màn kết trận vài giây rồi tự sang trận mới (seed + 1);
   - nạp não nóng như trước, nhưng kiểm bằng `SurvivorPilot.Validate` cùng self-test;
   - `BrainLocator` dùng `schema_version.txt` (bỏ `rules_version.txt` / `ArenaSim.RulesVersion`);
     chỉ nạp run có schema bằng `SurvivorObservation.SchemaVersion`;
   - giữ nguyên nút TRAIN THE AI, nút Power và bảng TRAINING DATA (phím G). Behavior luôn là
     `Warrior`, nên bỏ các nút đổi class và kiểu trộn zombie.
2. **Hiển thị thế giới** (`SurvivorRenderer`):
   - camera đi theo nhân vật, góc nhìn chéo từ trên xuống; cuộn chuột để zoom;
   - nền bản đồ 100 × 100 m kiểu nghĩa địa, có hàng rào bao quanh;
   - vật cản `Obstacles` vẽ bằng mộ, cây, đá lấy từ KayKit Halloween (đã tải ở
     `ThirdParty/KayKit/Halloween`), chọn mẫu theo chỉ số của vật cản;
   - quái dùng lại model KayKit M3 theo `TypeIndex`: walker, runner, brute, và boss là skeleton to;
     quái tinh anh to hơn và có viền màu;
   - dùng object pool theo sức chứa của Core (400 quái, 128 đạn, 600 đồ nhặt); không Instantiate
     trong vòng lặp mỗi frame;
   - đồ nhặt: ngọc EXP 3 cỡ và 3 màu, xu vàng, thịt, rương, nam châm;
   - hiệu ứng vũ khí lấy từ sự kiện `WeaponFired`, `DamageDealt` và `EnemyKilled`: vệt kiếm, búa bay,
     số sát thương nổi lên (chí mạng màu khác);
   - nội suy vị trí giữa các tick như `ArenaRenderer`.
3. **HUD** (`SurvivorHud`, có thể dùng lại helper UI trong `ArenaHud` và `UiSprites`):
   - trên cùng: thanh EXP dài toàn màn hình kèm "Lv N";
   - giữa trên: đồng hồ mm:ss đếm tới 15:00; khi boss xuất hiện, thêm thanh máu boss;
   - trái trên: máu và năng lượng, vàng, số quái đã giết;
   - hàng icon vũ khí và phụ kiện kèm cấp, và 3 ô skill chủ động có hồi chiêu;
   - **bảng lên cấp**: khi `IsAwaitingPick`, hiện các lựa chọn (tên món, cấp kế tiếp, mô tả ngắn).
     Sau khi AI chọn, **tô sáng món AI chọn** khoảng 1,2 giây trước khi game chạy tiếp; trong lúc
     đó trình xem tạm dừng mô phỏng, còn Core thì không cần đổi;
   - **màn kết trận**: thắng / hết giờ / chết (kèm nguyên nhân), thời gian sống, cấp, vàng, số quái
     giết, món đồ cuối trận;
   - bảng thông tin AI như cũ: run, số bước, lần nạp não gần nhất, trung bình 10 trận gần nhất
     (sống bao lâu, cấp, vàng).
4. **Icon món đồ:** vẽ bằng `SkillIconFactory` (SDF), hoặc dùng icon game-icons.net (CC BY 3.0). Nếu
   dùng icon ngoài thì ghi vào `ThirdParty/CREDITS.md`.
5. **Test EditMode:**
   - `BrainLocator` theo schema;
   - định dạng đồng hồ;
   - chọn model vật cản ổn định theo chỉ số;
   - bảng lên cấp tô đúng món đã chọn (logic thuần, không cần scene).
6. **Build:**
   - `PlaySceneBuilder` dựng lại scene Play với bộ điều khiển mới;
   - `WatchBuild.BuildWindows` và `TrainingBuild.BuildWindows` phải chạy thành công.

Code của đấu trường cũ (`ArenaRenderer`, `ArenaStage`, `AiArenaController` và các phần liên quan)
được giữ tới T-017 rồi mới xóa. Không sửa gì trong `Assets/Arena/Core`.

## Kiểm tra

- Unity EditMode xanh (batchmode).
- Build training và build xem đều thành công.
- Chụp ảnh bản build xem (`-screenshot`) thấy được:
  - đám quái;
  - ngọc EXP;
  - HUD;
  - bảng lên cấp tô sáng món AI chọn.
