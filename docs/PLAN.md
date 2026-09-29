# Kế hoạch "Personal Arena": game Unity arena có AI tự học

## Bối cảnh

Bạn xem video Pezzza's Work *"AI Gladiator learns to fight Zombies"*. Trong đó
một agent PPO tự học đánh zombie:
- mắt là 92 raycast;
- quyết định 12 lần/giây;
- dùng curriculum 1→16 zombie.

Bạn muốn một **game Unity hoàn chỉnh**:
- chọn class (Chiến binh, Pháp sư, Cung thủ…), mỗi class có đặc điểm riêng;
- mua nhân vật và **huấn luyện riêng từng con**;
- arena tùy chỉnh được kích thước, độ khó, số lượng và loại zombie;
- AI tự học cách đối phó;
- phần game và phần AI tích hợp trong cùng một sản phẩm;
- giao diện vừa phải.

Ưu điểm lớn: game do mình làm nên AI đọc thẳng trạng thái game. Không cần chụp
màn hình, không lo anti-cheat, và chạy nhanh hơn thời gian thực nhiều lần.

Các lựa chọn đã chốt nằm ở `docs/DECISIONS.md`; máy và môi trường ở `docs/SETUP.md`;
tiến độ hiện tại ở `docs/STATUS.md`.

## Công nghệ

- **Unity 6.3** + **ML-Agents** (`com.unity.ml-agents` 4.x, Apache 2.0). Có sẵn PPO,
  `RayPerceptionSensor3D`, curriculum qua environment parameters, ghi demo
  (`DemonstrationRecorder`) cho BC/GAIL, và chạy model trong game bằng Inference Engine.
- **Python trainer:** `mlagents==1.1.0`, torch cu121, Python 3.10.12 (xem `docs/SETUP.md`).

## Kiến trúc

```
PersonalArena/ (repo public ngotrongduong/Personal-Arena, D-015)
  Unity/                      Unity project
    Assets/Arena/Core/        logic thuần C# (không MonoBehaviour), test bằng dotnet (CoreTests/)
      Combat (máu, năng lượng, cooldown, stun, backstab, parry), SkillSystem, ArenaRules
    Assets/Arena/Data/        ScriptableObjects: HeroClassDef, SkillDef, ZombieTypeDef, ArenaPreset
    Assets/Arena/Actors/      HeroController, ZombieController (AI thường, không học), Projectile
    Assets/Arena/ML/          HeroAgent : Agent, reward shaping, EnvParams → ArenaConfig
    Assets/Arena/Game/        Menu, Roster/Shop (tiền trong game), Training Center, Battle, Save
  Trainer/                    Python: arena_trainer (bọc mlagents-learn), sinh YAML, đọc tiến độ
  docs/                       PLAN, GDD ngắn, hướng dẫn huấn luyện
```

### Thiết kế cốt lõi (đảm bảo mở rộng được thành game hoàn chỉnh)

**Một agent chung cho mọi class.** `HeroAgent` dùng giao diện hành động chung:
- nhánh 1: di chuyển (9 hướng);
- nhánh 2: xoay hướng nhìn (trái, phải, giữ);
- nhánh 3: skill slot 0–4 (không làm gì, hoặc 4 skill).

Class chỉ khác ở `HeroClassDef` (chỉ số và 4 `SkillDef`):
- **Chiến binh:** đâm, đá choáng, khiên và parry, lao tới.
- **Pháp sư:** cầu lửa (projectile), vòng băng làm chậm (AoE), dịch chuyển, khiên mana.
- **Cung thủ:** bắn tên, bắn xuyên, lùi nhảy, bẫy.

**Observation** (tính trong Core, D-013):
- `RaySensor` 72 tia 360° (video dùng ~92), mỗi tia nhận loại zombie, tường và khoảng cách;
- 16 số của hero: máu, năng lượng, cooldown 4 slot, trạng thái stun/block, tường gần…;
- tổng 736 số, `HeroAgent` chỉ chép sang ML-Agents.

Kích thước observation không phụ thuộc số zombie.

**Mỗi class là một behavior riêng** (`Warrior`, `Mage`, `Archer`), nên có model
nền riêng.
- **Mỗi nhân vật đã mua** có "não" riêng: fine-tune từ model nền của class bằng
  `--initialize-from`, lưu kèm cấp độ và thống kê trong save.
- Đây là ý "mua và tự huấn luyện riêng biệt".

**Zombie types** (data-driven, AI thường theo luật):
- Walker (chậm);
- Runner (nhanh, máu ít);
- Brute (trâu, đánh mạnh, phải né);
- Spitter (bắn từ xa).

**Tùy chỉnh arena và độ khó.** `ArenaConfig` gồm: kích thước, số zombie, tỉ lệ
từng loại, hệ số máu/sát thương, tốc độ spawn.
- Khi huấn luyện, các tham số này được **random hóa quanh curriculum**, nên AI
  quen mọi cấu hình người chơi chọn.

**Tốc độ:**
- AI quyết định ở 12 Hz (`DecisionRequester` period 5 ở fixed 60 Hz);
- khi train chạy `--no-graphics`, `time-scale` 20, **nhiều arena song song**
  trong một scene và nhiều env (`--num-envs`).

**Reward** (rút kinh nghiệm video): thưởng sống sót nhỏ, thưởng gây sát thương
và hạ zombie, thưởng backstab/parry, phạt khi mất máu và khi chết.
- Cấu hình được trong `HeroClassDef`.
- Có test kiểm tra **dấu** của từng reward, tránh lỗi "đâm giáo bị phạt" như
  trong video.

**Chơi tay:** `Heuristic()` đọc bàn phím.
- Bạn chơi được từng class.
- Bật ghi demo để AI khởi động bằng cách bắt chước bạn (BC/GAIL), rồi tự học vượt lên.

### Tích hợp AI vào game (Training Center)

- Game gọi `Trainer/arena_trainer.py` qua subprocess, truyền nhân vật, class,
  preset arena và số phút.
- Trainer sinh YAML, chạy `mlagents-learn` với **chính bản build game ở chế độ
  headless** (`--env`), và ghi `progress.json` (điểm TB, entropy, thời gian sống,
  cấp curriculum).
- Game đọc file đó để vẽ dashboard giống video.
- Xong thì model `.onnx` được chép vào thư mục save của nhân vật. Game nạp lúc
  runtime bằng Inference Engine rồi gọi `Agent.SetModel`.
- **Rủi ro kỹ thuật cần spike ở M0:** Inference Engine có nạp ONNX lúc runtime
  trong bản build không.
  - Nếu không: thêm bước chuyển ONNX sang `.sentis` bằng Unity Editor batchmode
    (`ModelWriter`) trong trainer.

## Các milestone

| # | Milestone | Kết quả nghiệm thu |
|---|---|---|
| M0 | **Thiết lập và spike**: repo GitHub, Unity project, `.gitignore` Unity, cài uv + Py 3.10.12 + torch cu121 + mlagents 1.1.0, package ML-Agents 4.x; chạy thử 3DBall/`mlagents-learn` trên GPU; spike nạp ONNX runtime trong build | Train mẫu chạy; biết chắc cách nạp model runtime |
| M1 | **Arena chơi tay**: Warrior + Walker, combat giống video (đâm, đá choáng, khiên/parry, backstab), camera top-down, HUD máu/năng lượng; logic Core có test EditMode | Bạn chơi được 1 đấu N zombie |
| M2 | **Warrior tự học**: `HeroAgent`, reward, nhiều arena song song, curriculum 1→2→4→8→16, xem model trong Editor; demo bạn chơi → BC | Agent hạ ổn định 1 zombie rồi lên 16; dashboard TensorBoard |
| M3 | **Nội dung data-driven**: Mage, Archer, 4 loại zombie, `ArenaConfig` (kích thước, độ khó, trộn loại) + random hóa khi train; model nền cho 3 class | 3 class tự học; cấu hình arena bất kỳ trong khoảng cho phép vẫn đánh được |
| M4 | **Survivor lõi (Warrior)** — thay M4/M5 cũ (D-026): bản đồ 100×100 m, lịch quái 15 phút, EXP và lên cấp, 6 vũ khí tự bắn, 8 phụ kiện, 3 skill chủ động, đồ nhặt, tinh anh, rương, boss; quan sát v4 chừa chỗ, nhánh chọn nâng cấp, build ngẫu nhiên khi train, `brain_upgrade.py` (D-027); trình xem: camera theo nhân vật, nghĩa địa, HUD, bảng lên cấp tô sáng lựa chọn của AI, màn kết trận | Test xanh; `warrior-s001` sống ≥ 10 phút ở bậc 1; owner xem được |
| M5 | **Kinh tế và build** (D-028, D-029): `profile.json`, vàng về ví, mua cấp, cộng/tẩy điểm, bậc độ khó, Farm tự động, não riêng theo build, màn so sánh build | Đổi build → TRAIN → AI đổi lối đánh, điểm hồi phục nhanh |
| M6 | **Class mua được và nội dung**: Mage, Archer, tiến hóa vũ khí, thêm quái vào chỗ trống | Thêm nội dung mà não học tiếp, không học lại từ đầu |
| M7 | **Đánh bóng**: âm thanh miễn phí, cân bằng, bản build hoàn chỉnh | File exe chạy trọn vòng chơi |

> Từ 2026-09-30, thiết kế game chi tiết (số liệu, danh mục vũ khí, phụ kiện, chỉ số, quan sát v4) nằm ở
> **`docs/GDD.md`**. Các mục kiến trúc phía trên mô tả đấu trường M1–M3; phần nào khác GDD thì GDD thắng.

## Cách làm việc

Phân công Claude / Codex / owner, quy tắc git và lệnh chạy test: xem `AGENTS.md`.
Bảng việc: `docs/tasks/BOARD.md`.

## Kiểm tra

- **Test dotnet (`CoreTests`)** cho Core: sát thương, cooldown, stun, backstab, parry,
  năng lượng khiên, dấu reward, `ArenaConfig` hợp lệ. CI chạy mỗi lần push.
- **Test EditMode** (từ M1) cho phần Unity: prefab, component, `HeroAgent` quan sát đúng kích thước.
- **pytest** cho `Trainer/` (sinh YAML, đọc tiến độ).
- **Training thật:**
  - M2: điểm TB tăng và thời gian sống/kill đạt ngưỡng, curriculum lên đủ cấp.
  - M3: mỗi class vượt ngưỡng ở preset Easy/Normal/Hard.
- **Kiểm tra bằng mắt:** chơi tay, xem AI đánh, luồng mua → train → đấu trong
  bản build.
