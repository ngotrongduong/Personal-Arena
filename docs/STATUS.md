# Trạng thái dự án

> File này là "bộ nhớ" giữa các phiên. Đọc đầu tiên, cập nhật cuối cùng.
> Cập nhật lần cuối: 2026-10-04 (Codex PC: T-041, trình xem và build schema v5).

## Hướng đi

- **Chỉ tập trung vào Personal Arena** (D-016). Dự án PersonalGameAI đã dừng; không làm gì
  thêm ở đó trừ khi owner yêu cầu.
- Repo để **public** để CI trên GitHub chạy miễn phí (D-015) → không bao giờ commit secret.

## Đang ở đâu

- **M9 / T-041:** HUD dùng 6 ô skill, 6 ô vũ khí + 6 ô phụ kiện theo Core; Archer có 5 skill thật, bỏ ô `none` và giữ đúng cooldown. `Build/TrainingNext` và `Build/WatchNext` đã build thành công; CoreTests Release 627/627, smoke test 3/3 class + 7 bảng + Farm đạt. Chi tiết EditMode và kiểm tra bố cục trong Report T-041.
- Core hiện là **schema v5: 2592 quan sát, action 9/7/5, danh mục 128**. Các mốc schema v4 bên dưới là lịch sử. Não nâng cấp để kiểm thử nằm trong TEMP; không sửa `Trainer/runs/` và không dừng training của owner.

- **Milestone:** M0–M3 xong. **M4A (lát cắt dọc Survivor) code xong** (T-014..T-017); đang train
  `warrior-s001` tới nghiệm thu đánh giá 100 seed. Chi tiết milestone ở `docs/PLAN.md`, thiết kế ở
  `docs/GDD.md`.
- **M4A đã có (nhánh `feature/survivor-core`, merge vào `develop`):**
  - Core `Core/Survivor` (T-014): bản đồ 100 × 100 có vật cản, lịch quái, EXP/lên cấp, vũ khí tự
    bắn, phụ kiện, đồ nhặt, boss, quan sát schema v4 (2264), action 9/5/5, thưởng thứ bậc từ
    `SurvivorEvent`, `SurvivorEvaluator` + `Tools/SurvivorEval` (đánh giá 100 seed).
  - Trainer (T-015): `warrior_survivor_ppo.yaml` (curriculum độ dài trận 180 → 900 s),
    `schema_version.txt` thay `rules_version`, run `warrior-sNNN` (`brain_upgrade.py` để T-019).
  - Unity (T-016): `HeroAgent` 3 nhánh, trình xem Survivor (camera theo nhân vật, nghĩa địa
    KayKit Halloween, HUD EXP/cấp/đồng hồ/vàng/món đồ, bảng lên cấp tô sáng lựa chọn, màn kết trận).
  - T-017: **đã xoá hẳn đấu trường tròn cũ** (`ArenaSim`, quan sát 883, `ArenaPlay.unity`, các view
    cũ, config `<class>_ppo.yaml`). Mage/Archer có config Survivor riêng từ M7. Các mục lịch sử bên
    dưới nói về code cũ này.
  - Test: CoreTests 69/69, pytest 70/70, Unity EditMode 78/78.
- **M4B code xong (D-032, D-033):**
  - T-018 (Codex, Claude review): `Trainer/champion.py` chấm checkpoint trên 100 seed, giữ não giỏi
    nhất ở `runs/champions/Warrior/`. Dịch vụ train tự chấm mỗi 2M bước. `SurvivorBehaviorTracker`
    đo phong cách đánh.
  - T-020 (Claude): trình xem có phím `B` (não mới nhất ↔ giỏi nhất) và phím/nút `P` (**Hồ sơ AI**).
  - T-019 (Codex, Claude review): `Trainer/schemas/survivor_v4.json` + `brain_upgrade.py` (schema
    lớn hơn → tự nâng não sang run mới khi bấm TRAIN, lỗi thì học lại từ đầu kèm thông báo);
    curriculum theo tiến độ cho build ngẫu nhiên (`build_level_max`), bậc (`tier_max`) và trận khó
    (`hard_share`, bật sau 30%); 20% trận ôn tập.
  - Bản build train mới (có T-019) nằm ở `Build/TrainingNext`, tự được tráo vào khi bấm TRAIN lần
    tới (dịch vụ đang chạy là bản cũ).
  - Test: CoreTests 85/85, pytest 100/100, EditMode 90/90.
- **M4C code xong (D-034):**
  - T-021 (Codex, Claude làm nốt + review): giáo đâm, rìu xoay, hào quang, sóng chấn động; 4 phụ
    kiện mới; Spitter + đạn địch; nam châm; rương từ tinh anh. Schema v4 không đổi → não học tiếp.
  - T-022 (Claude): hình ảnh 4 vũ khí mới (rìu KayKit xoay quanh người), đạn nhổ xanh, hiệu ứng
    rương/nam châm, Spitter có look riêng, 8 icon game-icons.net mới trong HUD và bảng lên cấp.
  - `Build/Watch` đã build lại. Bản train mới (có M4C) ở `Build/TrainingNext`, tự tráo vào khi owner
    bấm TRAIN lần tới.
  - Test: CoreTests 112/112, EditMode 90/90.
- **Lịch sử M0–M3:** BC từ demo đã bỏ (D-023: owner không chơi tay).
- **Nhánh:** làm việc trên `develop` (nhánh tính năng → PR merge commit vào `develop`); `main`
  fast-forward theo `develop` khi ổn định.
- **Đã có trên `develop`:**
  - Unity 6000.3.2f1, ML-Agents 4.1.0, Inference 2.6.1, Input System, uGUI, Test Framework (PR #3).
  - Core M1 (PR #2) + `HeroInput` helpers (PR #4): sim 60 Hz deterministic, Warrior vs Walker.
  - **M1 view (PR #5):** scene `Assets/Scenes/ArenaPlay.unity` (tự dựng bằng
    `PersonalArena.View.Editor.PlaySceneBuilder.Build`), `ArenaRenderer`, bàn phím + chuột, HUD,
    bảng kết quả. Bản build Windows chạy, đã xem ảnh chụp: arena, warrior, zombie, HUD đúng.
    Shader `Standard` được ép vào "Always Included Shaders" (nếu không bản build bị đen).
  - **M2 agent (PR M2):** `Assets/Arena/ML/` — `HeroAgent` (1 tick Core mỗi action, quyết định
    12 Hz, action 9/3/5, mask skill, thống kê `Arena/*`), `TrainingArenaHost` (16 arena/process),
    `TrainingSceneBuilder`, `TrainingBuild` → `Build/Training/PersonalArenaTraining.exe`.
    `Trainer/arena_trainer.py` + 2 config PPO (curriculum 1→2→4→8→16). Hướng dẫn: `docs/TRAINING.md`.
  - **Trình xem AI (feature/watch-ai):** `Xem-AI.cmd` → exporter `.pt`→`.brain` chạy ngầm +
    `Build/Watch/PersonalArenaWatch.exe` (scene `ArenaWatch`, `AiArenaController`), tự nạp não
    mới khi training lưu. Não chạy bằng `Core/PolicyBrain.cs` + `BrainPilot.cs` (D-017, không
    ONNX). Hướng dẫn: `docs/TRAINING.md` mục 4.
  - **Đồ họa KayKit (feature/kaykit-art, D-018):** hiệp sĩ kiếm + khiên, bộ xương làm zombie
    (3 kiểu), hoạt ảnh chạy/chém/đá/đỡ/lướt/trúng đòn/chết/trồi lên (`CharacterAnimator`,
    Playables theo đồng hồ sim), camera phối cảnh nghiêng. (Hầm ngục `DungeonDressing` đã bỏ ở
    luật v2, thay bằng `ArenaStage`.) Asset ở `Unity/Assets/ThirdParty/KayKit` (CC0, `CREDITS.md`); map bằng
    `Assets/Arena/View/Art/KayKitArtSet.asset` (dựng lại: menu *Personal Arena/Rebuild KayKit Art
    Set*). Scene training không dùng renderer nên huấn luyện không bị ảnh hưởng.
  - Owner **chỉ xem AI tự học, không chơi tay** (D-019) → mọi việc hiển thị nhắm vào `Xem-AI.cmd`.
  - **Nút "TRAIN THE AI" (feature/train-button, D-020):** bảng TRAINING trong trình xem bật/tắt
    `Trainer/train_service.py` (chạy mlagents ẩn, resume run mới nhất, train liên tục, dừng bằng
    Ctrl+C để lưu). Hiện bước, reward, số zombie, biểu đồ reward. Đóng game = dừng + lưu.
    Code: `TrainingServiceClient.cs`, `ArenaHud` (panel), `AiArenaController`. Hướng dẫn:
    `docs/TRAINING.md` mục 4.
  - **Train bền + nút Power (feature/train-power, D-021):** mlagents crash thì service tự chạy lại
    từ checkpoint (2 lần crash nhanh → CPU); lỗi service ghi `training_service.err.log` và hiện
    lên bảng. Nút Power LIGHT/NORMAL/FAST/MAX = 32/64/128/256 arena cùng lúc (mặc định FAST,
    MAX ~2 lần nhanh hơn mức cũ). Bảng đo ở `docs/TRAINING.md` mục 4.
  - **Luật v2 + đồ họa mới (feature/arena-v2, D-022):** Core: sàn tròn bán kính 14 (arena 28),
    rơi vực = chết (`HeroFell`/`ZombieFell`), kick knockback, block stagger, dash 18 m/s, gia tốc,
    bình máu; quan sát 811; `ArenaSim.RulesVersion = 2` ghi vào `rules_version.txt` của run.
    View: `ArenaStage` (sàn đá tròn, vực, đảo đá bay, đuốc), `ArenaEffects` (vệt chém, sóng kick,
    khiên block + sao choáng, bóng mờ dash, bình máu, hạt bụi), di chuyển nội suy mượt, HUD bo góc
    có hiệu ứng, `TopDownCamera` xoay/zoom/dời + 3 chế độ, màn hình **TRAINING DATA** (nút hoặc
    phím `G`, 6 đồ thị từ `training_history.json` do `Trainer/training_history.py` ghi).
  - **M3 (feature/m3-zombies, D-024, T-011..T-013):** Core luật v3: Runner (nhanh, yếu), Brute
    (to, chậm, kháng hất lùi), Spitter (giữ khoảng cách, nhổ đạn độc, khiên chặn được); đạn chung
    cho hero và zombie; zombie hồi sinh bốc loại mới; quan sát **883** (thêm kênh "đạn địch");
    thưởng hất zombie xuống vực. Mage (cầu lửa, vòng băng, khiên mana, dịch chuyển) và Archer (bắn
    tên, tên xuyên, nhảy lùi, tên chấn động). Trainer: `--hero-class`, config
    `<class>_ppo.yaml`, curriculum thêm dần loại zombie sau 16 zombie. Trình xem: phím/nút `H` đổi
    class, `M` đổi kiểu trộn zombie; model KayKit theo class và loại zombie; hiệu ứng đạn;
    **thanh skill có icon tự vẽ** (`SkillIconFactory`, 12 icon, bảng ở `docs/images/skill-icons.png`),
    bỏ chữ phím tắt, cooldown quét vòng trên icon.
  - Test lúc hết M3: CoreTests 97/97, pytest 62/62, Unity EditMode 47/47.

## Checklist

| Việc | Trạng thái |
|---|---|
| M0: repo, Unity project, ML-Agents, venv ML trên GPU | xong |
| M0: spike nạp ONNX lúc runtime | xong — không được, thay bằng `.brain` (D-017) |
| M1: Core + view + input + HUD + scene chơi tay | xong (PR #5) |
| M2: HeroAgent + env train + trainer wrapper | xong (PR M2) |
| M2: trình xem AI chơi (T-010) | xong (`Xem-AI.cmd`) |
| Đồ họa KayKit + hoạt ảnh cho trình xem AI | xong (D-018) |
| Nút train trong trình xem AI | xong (D-020) — đã thử bật, dừng, đóng game: đều lưu êm |
| Train tự hồi phục khi crash + nút Power | xong (D-021) — đã thử đổi FAST→MAX lúc đang train: lưu + chạy lại trong 1 s |
| Luật v2: sàn tròn + vực, kick/block/dash thật, bình máu, hiệu ứng, camera, TRAINING DATA | xong (D-022) — đã chụp bản build: sàn tròn, HUD, 6 đồ thị đúng |
| M2: training thật, curriculum lên 16 zombie | xong — `warrior-002` (luật v2) dừng êm ở 48.9M bước, reward ~300; không train tiếp (luật v3) |
| M2: ghi demo chơi tay → BC/GAIL | bỏ (D-023) |
| M3: 4 loại zombie + đạn + Mage/Archer + icon skill | xong (D-024); đã thay bằng Survivor |
| **Hướng mới: chế độ Survivor (kiểu Vampire Survivors)** | thiết kế xong (`docs/GDD.md`, D-026..D-030) |
| M4A: Core, Trainer, Unity Survivor + dọn code cũ (T-014..T-017) | xong — test xanh, build xem và build train chạy |
| M4A: train `warrior-s001` qua đánh giá 100 seed | đang làm — 23.1M bước, bài 360 s. Đánh giá 100 seed thử ở 17M: trung vị 10:32, P10 5:17 (cần 7:00), 1 lần chết sớm |
| M4B: champion/challenger + Hồ sơ AI (T-018, T-020) | xong |
| M4B: nâng cấp não + build ngẫu nhiên + ôn tập (T-019) | xong — có hiệu lực từ lần bấm TRAIN kế tiếp (tự tráo `Build/TrainingNext`) |
| M4C: đủ nội dung (6 vũ khí, 8 phụ kiện, đồ nhặt, rương, spitter, HUD) | code xong (T-021, T-022) — chờ train trên bản mới và qua đánh giá 100 seed |
| M5: kinh tế và build (T-023..T-026) + thưởng hạ quái (T-027, D-036) | code xong — chờ nghiệm thu: đổi build → TRAIN → AI đổi lối đánh |
| M6: Lịch sử não (T-028, T-029, D-037) | code xong — chờ nghiệm thu: mở bảng `L`, chọn não cũ, bấm "Xem ngay" |
| M7: class mua được, tiến hóa vũ khí, quái mới (T-030..T-032, D-038) | xong — owner đã mua Pháp sư + Cung thủ, chạy tốt (não nền Mage/Archer vẫn đang học) |
| M8: đánh bóng — âm thanh, cài đặt + phiên bản + smoke test, cân bằng (T-033..T-035, D-039) | code xong — chờ owner nghiệm thu bản build mới |

## Việc tiếp theo (theo thứ tự)

**Ưu tiên hiện tại:** Claude review T-041; tiếp theo mới làm T-037 (icon/hiệu ứng/âm thanh M9). Khi dùng build schema v5, não schema v4 cần đi qua cơ chế `brain_upgrade`; các ghi chú “schema không đổi” của M4–M8 bên dưới chỉ áp dụng cho các phiên cũ.

Owner đổi hướng ngày 2026-09-30: game thành kiểu **Vampire Survivors**, AI tự học farm vàng, chọn
nâng cấp, thích nghi theo build; não học tiếp khi game cập nhật. Thiết kế đầy đủ: **`docs/GDD.md`**.
Lộ trình mới M4A–M8 ở `docs/PLAN.md` (owner góp ý thiết kế → D-030: chia M4 thành M4A/M4B/M4C,
thưởng theo thứ bậc, nghiệm thu bằng đánh giá 100 seed). Đấu trường tròn luật v3 sẽ bị thay;
`warrior-003` không cần train tiếp.

0. Lần tới owner dừng rồi bấm TRAIN, dịch vụ mới sẽ tự tráo `Build/TrainingNext` vào (có cả T-019
   lẫn nội dung M4C), chạy curriculum T-019 và chấm não mỗi 2M bước. Kiểm tra log run có dòng
   "installed the new training build". Schema không đổi nên `warrior-s001` học tiếp, không mở run mới.
   Sau đó theo dõi: AI có chọn vũ khí mới không, tỉ lệ chết vì `Projectile` (Spitter) từ phút 5.
1. Train `warrior-s001` (nút TRAIN THE AI) tới khi qua đánh giá 100 seed bằng
   `Tools/SurvivorEval`: trung vị ≥ 10:00, P10 ≥ 7:00, không chết trước 3:00.
   - Theo dõi: `Arena/Died`, `Arena/SurvivedSeconds`, `Environment/Lesson Number/run_seconds`.
   - AI nhặt ít EXP (~54 EXP, cấp ~3.7 mỗi trận 180 s dù giết ~195 quái). Nếu lên bài 360/600 s mà
     chết nhiều vì thiếu nâng cấp: tăng `PerLevelProgress` (hiện 0.05) nhưng vẫn giữ thứ bậc D-030.
   - Nếu kẹt ở bài 180 s lâu (reward không lên 5.0): xem lại ngưỡng curriculum.
2. Nghiệm thu M4C = qua đánh giá 100 seed trên bản có nội dung mới. Rồi M5 (kinh tế và build).
   Nợ nhỏ còn lại của T-021 ghi ở phần Report của `docs/tasks/T-021-m4c-survivor-content.md`
   (N2, N3, N5, N6 — rương có thể mất khi pool đồ nhặt đầy).
3. M5 đang làm song song khi owner train (2026-09-30):
   - T-024 (Trainer) xong: dịch vụ train nhận `--owner-build/--owner-tier/--training-focus`.
     Quyết định D-035: não của nhân vật chính là `warrior-s001`.
   - T-023 (Core) xong: Training Focus, modifier bậc 2–10, nguồn vàng, nhãn khán giả, câu chuyện
     trận, profile + luật kinh tế, Farm tự động (`FarmSession`), log kinh tế. Bậc 1 giữ y hệt code cũ
     (golden test).
   - T-025 (Unity ML) xong: bản train đọc build/bậc/trọng tâm của owner; ~70% trận dùng build owner
     (lệch ≤ 2 điểm). `Build/TrainingNext` đã build — dịch vụ tự cài ở lần TRAIN sau.
   - T-027 xong (D-036): owner thấy AI lùi và bỏ chạy thay vì tiến lên giết quái → thêm thưởng hạ
     quái trực tiếp (`PerKill` 0,004, `PerEliteKill` 0,1), vẫn giữ thứ bậc D-030.
   - T-026 xong: trình xem có ví vàng, menu Nhân vật (`C`: mua cấp, 5 bộ điểm, bậc, trọng tâm),
     Farm tự động (`F`), so sánh build (`V`), nhãn khán giả trên đầu nhân vật, câu chuyện trận ở màn
     kết. Profile ở `%USERPROFILE%\AppData\LocalLow\ngotrongduong\Personal Arena\profile.json`
     (`Application.persistentDataPath`, có `.bak` và `economy_log.csv` cạnh bên).
   - Bản xem mới được build ra `Build/WatchNext` khi owner đang mở trình xem; `Xem-AI.cmd`
     (`Trainer/watch_ai.ps1`) tự tráo nó vào `Build/Watch` ở lần mở sau.
   - Nghiệm thu M5: owner cộng điểm/đổi trọng tâm → bấm TRAIN → Hồ sơ AI (`P`) cho thấy lối đánh
     đổi, điểm hồi phục nhanh. Theo dõi `economy_log.csv` để cân bằng giá cấp và vàng.
4. M6 Lịch sử não (D-037) code xong 2026-10-01:
   - T-028 (Trainer): `Trainer/brain_lineage.py` giữ phiên bản não ở
     `runs/champions/Warrior/lineage/` (mỗi lần chấm, lưu tay, lịch sử champion), rẽ nhánh/nhân bản
     thành run mới, dọn `.pt` cũ. `champion.py` ghi mỗi lần chấm; `train_service` chạy `sync` lúc bắt đầu.
   - T-029 (trình xem): bảng **Lịch sử não** (phím/nút `L`): cây nhánh, danh sách phiên bản, Xem ngay,
     So sánh, Ghim, Đổi tên, Rẽ nhánh, Nhân bản, Lưu phiên bản hiện tại, Dùng nhánh này (TRAIN học
     tiếp nhánh đó qua `BrainRunId` trong profile). Cờ `-watchVersion <id>` cho bản build.
   - Dịch vụ train đang chạy vẫn dùng `champion.py` cũ → phiên bản theo từng lần chấm (có `.pt`, rẽ
     nhánh được) bắt đầu có từ lần bấm TRAIN kế tiếp. 8 não champion cũ chỉ xem được (`.pt` đã bị
     ML-Agents dọn). Lưu phiên bản hiện tại và Nhân bản dùng được ngay.
   - Nghiệm thu M6: mở `L`, chọn một não cũ, bấm Xem ngay → trình xem chơi bằng não đó ngay.
5. M7 class mua được (D-038) code xong 2026-10-01:
   - T-030 (Core): bộ đồ Pháp sư (vũ khí 14–19, cầu lửa, khiên phép, dịch chuyển, nổ băng) và Cung thủ
     (20–25, bắn mạnh, lộn ra sau, đá); 18 tiến hóa (40–57) khi mở rương có vũ khí cấp tối đa + phụ
     kiện đôi; quái mới từ phút 5: Bom xác (nổ), Hồn ma (đi xuyên vật cản), Pháp sư xác (bắn xa, gọi 3
     zombie). Schema v4 giữ nguyên → `warrior-s001` học tiếp.
   - T-031 (Trainer): `mage_survivor_ppo.yaml`, `archer_survivor_ppo.yaml`, `train_service --behavior`,
     run `mage-sNNN`/`archer-sNNN`, champion/lineage và `SurvivorEval` theo class.
   - T-032 (trình xem): cửa hàng class trong menu `C` (Mua 1.500 / 3.000 vàng, Chọn), class đang chọn
     quyết định não được xem, nút TRAIN huấn luyện class đó (đang train class khác → lưu rồi chuyển),
     hình nhân vật/vũ khí/hiệu ứng/quái mới, thông báo "TIẾN HÓA", 10 icon mới (CC BY).
   - Bản xem ở `Build/WatchNext`, bản train ở `Build/TrainingNext` — tự tráo vào ở lần mở / lần TRAIN sau.
   - Nghiệm thu M7: owner mua Pháp sư hoặc Cung thủ, chọn, bấm TRAIN; khi não nền mới qua
     `SurvivorEval` (trung vị ≥ 10:00, P10 ≥ 7:00) là xong.
6. M8 đánh bóng (D-039) code xong 2026-10-01:
   - T-033: âm thanh Kenney CC0 + nhạc CC0, phím `M`, im khi cửa sổ ở nền / chạy tự động.
   - T-034: bảng Cài đặt (`O` / nút bánh răng: âm lượng, cửa sổ, chất lượng, FPS, Thoát), nhãn
     `Personal Arena v0.8.<số commit>` + icon app, `-smokeTest <report.json>` (bắt buộc `-profile`
     tạm) chạy trọn vòng 3 class; `watch_ai.ps1` báo lỗi tiếng Việt kèm đường dẫn `Player.log`.
   - T-035: cân bằng theo `SurvivorEval --set`: ngọc EXP × 1,5 (`XpMul`), máu trùm × 0,4
     (`BossHpMul`, 6.000 → 2.400), vàng rơi 3% → 4,5%. Luật đổi nhưng schema không đổi → não học tiếp.
   - Theo dõi sau khi owner bấm TRAIN trên bản mới: Mage P10 (395 s ở bản cân bằng mới, cần tập),
     AI có bắt đầu đánh trùm không (sát thương trùm trung bình mới ~1%).
   - Ý tưởng milestone sau: nới chỗ chừa cho quái (8 → 16 loại) và skill (4 → 6) bằng một lần
     `brain_upgrade` để thêm nhiều quái/skill mới mà não vẫn học tiếp.

## Cách làm trên PC (Claude)

- `C:\PersonalArena` đang ở nhánh `feature/survivor-core` (fast-forward theo `develop`); `develop`
  checkout ở worktree `C:\PersonalArena-wt\develop` để merge. Worktree task của Codex tạo dưới
  `C:\PersonalArena-wt\` và xoá sau khi merge; `main` = `develop`. Venv ML ở `C:\PersonalArena\.venv-ml`
  (Python 3.10.12, mlagents 1.1.0, torch 2.2.2+cu121, có pytest).
- Unity headless: `Unity.exe -batchmode -nographics -quit -projectPath <Unity> -executeMethod X
  -logFile L`. **Luôn có `-quit`**, nếu không Unity treo mãi.
- Test EditMode: `-runTests -testPlatform EditMode -testResults <xml>` (không dùng `-quit`).
- Chụp màn hình bản build: chạy exe cửa sổ (`-screen-fullscreen 0`) rồi `PrintWindow(h, dc, 2)`;
  `CopyFromScreen` bị khóa foreground.
- Tránh hộp "Allow": gom lệnh nhiều bước vào file `.ps1` rồi chạy một lệnh đơn.
- `arena_trainer.py` tìm `.venv-ml` ở gốc repo → chạy từ `C:\PersonalArena`, không từ worktree.

## Vướng mắc / câu hỏi mở

- CI (`.github/workflows/ci.yml`) **đã chạy xanh** trên mọi lần push từ 2026-09-29 (repo public
  nên miễn phí). Không còn vướng thanh toán.
- Unity MCP: đề xuất CoplayDev `unity-mcp` (D-008), chưa cài.
- Core dùng 72 tia, video ~92. Giữ 72 cho tới khi training cho thấy cần hơn.
- Package Inference tự thêm define `SENTIS_ANALYTICS_ENABLED` (analytics phía Editor). Xem lại
  trước khi phát hành để đảm bảo game không gọi mạng.

## Nhật ký phiên (mới nhất trên cùng)

### 2026-10-04 — Codex (PC): T-041 Unity schema v5
- HUD trình xem nay theo hằng số Core: 6 skill, 6 vũ khí + 6 phụ kiện. Warrior/Mage hiện đủ 6 skill;
  Archer ẩn slot `none` và dồn 5 skill thật liền nhau, cooldown vẫn theo đúng slot Core.
- Bổ sung tên/mô tả tiếng Việt cho 7 skill M9, fallback âm thanh an toàn và test lịch sử não theo schema hiện tại.
- Kiểm: CoreTests Release 627/627; Unity EditMode 296/296; smoke test bản build đạt 3/3 class, 7 bảng và Farm,
  0 lỗi. Render HUD ở 1920×1080 và 1280×720 cho cả ba class không tràn hoặc chồng nhau.
- Build mới đã tạo ở `Build/TrainingNext` và `Build/WatchNext`. Não schema v5 dùng cho smoke chỉ là bản sao trong
  TEMP; không đổi `Trainer/runs/`, không dừng training. T-041 chuyển `review`; việc Codex tiếp theo là T-037 sau review.

### 2026-10-02 (sau) — Claude (cloud): M9 đợt 1a, nội dung Warrior (T-036)
- Owner chốt: chia 2 đợt, ô mang theo 6 + 6 như Vampire Survivors, bắt đầu với Warrior, không ngại AI học lại.
  Đề xuất đầy đủ ở `docs/CONTENT-PROPOSAL.md`.
- T-036 (subagent `core-sim-engineer`, Claude review): ô 6 + 6 (`SurvivorTuning.MaxWeaponSlots/MaxPassiveSlots`),
  5 vũ khí Warrior (Phun lửa, Kiếm liên hoàn, Búa nặng, Bom, Phản đòn: ô 26–30), 4 phụ kiện cho cả 3 class
  (Hồi phục, May mắn, Tham lam, Vương miện: 58–61), skill Tiếng thét ở ô skill thứ 4 của Warrior. Schema v4 (2264)
  và hành động 9/5/5 không đổi. Golden cũ chạy bằng `OldRules` (4 + 4, pool cũ), không ghi lại giá trị.
- CoreTests 286/286 (Linux). Hai golden chỉ-Windows do job Windows của CI kiểm.
- **Chưa có hình ảnh/HUD trong trình xem** (T-037, cần PC có Unity): HUD 6 + 6 ô, icon, hiệu ứng, âm thanh.
  Não Warrior cũ chỉ dùng skill thứ 4 sau khi train lại. Số cân bằng là số khởi điểm, chưa chỉnh.
- **Đợt 1b (T-038)**: Vòng bảo hộ (31), Boomerang (32), Bình độc (33) cho Warrior; phụ kiện Bùa thời gian (34),
  Bộ nhân đôi (35, tối đa cấp 2), Giáp phản (36), Hộp tổng hợp (37) cho cả 3 class. Hai chỉ số mới `Duration`,
  `Amount` dùng chỗ `Reserved13/14`. CoreTests 342/342. Danh mục còn trống: 38–39. Chưa có hình ảnh/HUD (T-037).
- **Đợt 2, T-039 (Core) xong**: schema **v5** = 2592 giá trị (self 0..71, túi đồ 72..199 gồm 128 ô, đề nghị 200..719
  = 4 × 130, tia 720..2519, mật độ 2520..2591), hành động 9/7/5 (6 skill chủ động, 2 ô mới tạm là `none`),
  danh mục 128 (chỉ số cũ không dời). CoreTests 360/360. **T-040 (Trainer) xong**: `survivor_v5.json`, `SCHEMA_VERSION = 5`,
  `brain_upgrade` v4 → v5 giữ nguyên đầu ra cho input cũ (cột mới = 0; hai lựa chọn skill mới có bias thấp hơn
  bias nhỏ nhất cũ 5,0, chưa thử trong train thật), fixture nhị phân sinh bằng numpy nên giống nhau mọi nền tảng;
  pytest 203/203, CoreTests 361/361. **Chưa xong**: T-041 (Unity, cần PC: thanh
  skill 6 ô ở `SurvivorHud`, build lại, chạy EditMode). Hiện **bản xem/bản train cũ không chạy được với Core mới**
  (khác schema): đừng build hay train từ nhánh này trước khi T-040 và T-041 xong.
- **T-042 (nhóm vũ khí C) xong** trên schema v5: Đạn nảy (64; cả 3 class), Bóng tốc (65; Archer), Đồng hồ băng (66),
  Thanh tẩy (67), Mưa bom vòng (68) (66–68 cho Mage). CoreTests 395/395. Danh mục còn trống từ 69.
- **T-043 (skill thứ 5 và 6) xong**: Warrior Nhảy đập đất + Xoáy kiếm, Archer Bẫy gai + Mưa tên, Mage Tường lửa +
  Lôi liên hoàn (6 `SkillKind` mới thêm cuối enum; quái có thể bị làm chậm, trung tính khi không có bẫy).
  CoreTests 446/446. Cần Unity: thanh skill 6 ô, phím cho skill 5–6, icon, hiệu ứng.
- **T-044 xong**: Mage thêm Phun lửa, Vòng bảo hộ, Bình độc + 4 vũ khí mới; Archer thêm Bom, Boomerang, Bình độc;
  vũ khí mới 69 Hỏa cầu nổ (Mage), 70 Vòng tay ba mũi (Mage, Archer), 71 Bắn bốn hướng (Archer), 72 Đá tụ lực
  (Mage). CoreTests 495/495. Danh mục còn trống từ 73.
- **T-045 xong**: 16 tiến hóa cho vũ khí mới (ô 112–127; Thanh tẩy không có), cơ chế rương/ghép phụ kiện dùng chung với
  18 tiến hóa cũ (40–57). CoreTests 614/614. Lưu ý: tiến hóa Bóng ma tốc độ có hệ số sàn 0,4 nên sát thương lúc đứng
  yên là ×0,88 (task ghi 0,8; đổi `MomentumFloor` ≈ 0,333 nếu muốn 0,8). Đạn xuyên giờ nhớ tối đa 8 quái.
- **Đo hiệu năng `Step` với nội dung M9** (máy cloud, Release, 250 quái + 400 ngọc, test mới
  `SurvivorM9PerformanceTests`): bộ đồ M9 nặng nhất Warrior ≈ 0,058–0,061 ms, Archer ≈ 0,057–0,082 ms (dao động giữa
  các lần chạy), Mage ≈ 0,024–0,033 ms; bộ 6 vũ khí cũ cấp 5 trên cùng máy ≈ 0,046–0,049 ms. Đo từng vũ khí riêng: chỉ
  đám quái đã tốn ≈ 0,050 ms, mỗi vũ khí M9 chỉ cộng 0–0,015 ms (Đồng hồ băng còn làm nhanh hơn vì quái đứng yên) →
  chi phí chính là mô phỏng quái, không phải vũ khí mới. Mục tiêu 0,05 ms đặt cho PC của owner; CI giữ ngưỡng 0,2 ms.
  Nếu cần nhanh hơn: tối ưu cập nhật/tách quái, không phải vũ khí.
- **T-046 xong (chỉ di chuyển code)**: vũ khí chia theo cơ chế (`SurvivorSim.Weapons{,.Melee,.AroundHero,.Projectiles,
  .Area,.Defense}.cs`, skill gom vào `.Skills.cs`), danh mục tách thành `SurvivorDefs.cs` (kiểu), `SurvivorCatalog.cs`
  (logic), `SurvivorCatalog.Items.cs` (mọi dòng dữ liệu + 2 bảng tiến hóa, **phải ở cùng file theo thứ tự phụ thuộc**),
  `SurvivorClassKits.cs`, `SurvivorEnemyDefs.cs`; dispatch thành `switch`. Bỏ `Content/Wave1b/GroupC`. Hash 18 trận dài
  trước/sau khớp từng bit; CoreTests 622/622. Vũ khí mới đặt vào file theo cơ chế của nó.
  - Dọn tiếp (sau T-046): Đá không choáng trùm nữa (D-042); Quạt tên (`FireFan`) không tốn hồi chiêu khi pool đạn đầy;
    `ThrustSpears` dùng `NearestEnemy`; mảng đệm dùng `MaxVolleyCount`; `hammerTargetIds` đổi tên `volleyTargetIds`.
    Dấu vân tay 18 trận vẫn khớp từng bit; CoreTests 625/625.
- Chưa làm:
  toàn bộ phần hiển thị Unity (T-041 + icon/hiệu ứng/âm thanh), cân bằng bằng `SurvivorEval`.

### 2026-10-02 — Claude (cloud): CI xanh lại, review M5–M8
- CI `core-tests` đỏ từ M5: 2 golden băm từng bit float (ghi trên Windows) lệch trên Linux. Nay 2 test đó chỉ
  chạy trên Windows (`Assume`) và CI có thêm job `windows-latest` (PR #7, xanh cả hai).
- Cloud đã dựng được `dotnet test` (.NET 10 ở `/root/.dotnet`) và pytest (Python 3.10 + bản pin trong
  `Trainer/requirements-ml.lock.txt`, torch CPU). pytest 196 qua, 1 lỗi: fixture `old.brain` lệch 1 byte
  (RNG torch khác nền tảng; CI không chạy pytest, test C# đọc đúng file đã commit).
- Review M5–M8 (2 reviewer, chỉ đọc). Đã sửa: `--run-id` thoát khỏi thư mục runs, `check_id` quá lỏng, thư mục
  tạm trùng khi ghi phiên bản, điểm mặc định tính sớm, `RecordRun` nhận giây vô hạn. Phần View (Unity UI) chưa review.
- **Chưa sửa, cần quyết định/thiết kế:**
  - Rẽ nhánh từ phiên bản lịch sử (champion cũ) không có `training_status.json` → curriculum có thể về lesson 0.
  - ~~Thưởng hạ quái lớn hơn thưởng sống sót~~ — **owner quyết định giữ nguyên** (D-040). Lưu ý: "Pháp sư xác"
    (Necromancer) là **quái địch** M7 (D-038), không phải class của người chơi; nó gọi 3 zombie mỗi 6 s. AI có
    thể học đứng gần để hạ walker lấy thưởng; owner coi đó là một chiến thuật AI tự đánh giá.
  - ~~`DoubleElites`~~ — **giữ nguyên** (D-040): bậc ≥ 7 gấp đôi mọi tinh anh, kể cả tinh anh sớm 90 s của bậc 3.
  - Walker triệu hồi có thể nằm ngoài bản đồ 1 tick; sự kiện `EnemySummoned` báo thừa khi pool đầy. Sửa sẽ đổi
    golden M7 (ghi trên Windows) nên chưa làm.
  - `brain_lineage`: khoá khi rẽ nhánh/dọn song song, ghi file trên Windows không retry, `prune` có thể xoá
    phiên bản đang được sao chép (đọc code, chưa chạy thử).
- Việc cần PC: grep `SENTIS_ANALYTICS_ENABLED` trong `Library/PackageCache/com.unity.ai.inference*` để biết bản
  build có gửi gì không (cấu hình Analytics/Ads/Purchasing đã tắt, code game không gọi mạng).

### 2026-10-01 đêm — Claude (PC): M8 đánh bóng (T-033..T-035)
- Owner nghiệm thu M7 (mua Pháp sư + Cung thủ, chạy tốt).
- T-033 âm thanh, T-034 bản build hoàn chỉnh (subagent viết, reviewer soát, Claude sửa `watch_ai.ps1`
  về ASCII + nới giới hạn smoke test), T-035 cân bằng (Claude, đánh giá 100 seed).
- T-034b: smoke test bản cuối từng trượt ở Cung thủ (trận 60 s chưa lên cấp vì bắn xa, ngọc EXP nằm
  xa). Nay mỗi trận thử dài 120 s và mỗi class dùng não champion của chính nó.
- `Build/WatchNext` + `Build/TrainingNext` có M8, tự tráo vào ở lần mở trình xem / bấm TRAIN sau.
- Test: CoreTests 239/239, EditMode 289/289 (T-034).

### 2026-10-01 tối — Claude (PC): M7 class mua được (T-030..T-032, D-038)
- T-030 (Core), T-031 (Trainer), T-032 (trình xem) xong; subagent viết, Claude kiểm, reviewer duyệt.
- `warrior-s001` vẫn học tiếp (schema v4 không đổi). Lần chấm gần nhất 149,0M: 1241,6 điểm, qua mốc
  M4A, trung vị 1020 s, lần đầu thắng trận (1/100); champion vẫn là não 97,0M (1530 điểm).
- `Build/WatchNext` + `Build/TrainingNext` có M7, tự tráo vào ở lần mở trình xem / bấm TRAIN sau.
- Test: CoreTests 236/236, pytest 185/185, EditMode 238/238.

### 2026-10-01 — Claude (PC): M6 Lịch sử não (T-028, T-029)
- Owner bấm TRAIN lại: `warrior-s001` khỏe ở ~106.6M bước; champion (96,999,889) qua M4A.
- T-028 (Claude): `brain_lineage.py` + nối vào `champion`/`train_service`. Smoke test trên bản sao run
  thật: sync 8 não lịch sử, snapshot 106,093,272, nhân bản ra `warrior-s002` học tiếp được.
- T-029 (subagent viết, Claude kiểm): bảng `L`, `LineageStore`/`LineageCommand`/`LineageCompare`,
  cờ `-watchVersion`. Ảnh chụp 1080p/720p và xem não cũ đều đúng.
- Sửa sau review: `prune` không xoá gì khi `labels.json`/`branches.json` hỏng; kiểm tên run/phiên
  bản; phiên bản ghi vào thư mục tạm rồi mới đổi tên; lỗi chấm/ghi cây nhánh thành cảnh báo; trình
  xem không ghi đè `labels.json` hỏng; `taskkill /T` khi huỷ lệnh. `brain_upgrade` dùng chung
  `replace_directory` (hết lỗi `PermissionError` hiếm gặp trong test).
- Test: pytest 145/145, EditMode 180/180.

### 2026-09-30 khuya — Claude (PC): M5 T-023..T-027
- T-023 (Core kinh tế), T-024 (Trainer build owner, D-035), T-025 (Unity ML đọc build owner) đã merge.
- T-027 (D-036): owner báo AI chỉ lùi và chạy → thêm thưởng hạ quái; `Build/TrainingNext` đã build lại.
- T-026 (subagent viết, Claude kiểm): ví, menu C/F/V, nhãn khán giả, câu chuyện trận. Sửa 1 lỗi biên
  dịch test (`Is.AnyOf`). Ảnh chụp bản build: nhãn "THẢ DIỀU", màn kết trận có câu chuyện, 3 nút C/F/V.
- Test: CoreTests 201/201, EditMode 147/147.

### 2026-09-30 tối — Claude (PC): M4C T-021 + T-022
- **T-021** (Codex viết tới lúc hết hạn mức, Claude làm nốt + subagent reviewer duyệt): nội dung M4C
  trong Core, giữ nguyên schema v4.
  - Sửa sau review: tính vị trí rìu một lần mỗi tick; thêm test tất định với trận thật, test
    hiệu năng/không cấp phát với đủ 6 vũ khí, test pool và lựa chọn thay thế khi đầy ô.
- **T-022** (Claude): hình ảnh vũ khí/đạn/rương/nam châm, look Spitter, 8 icon mới (CC BY, đã ghi
  `CREDITS.md`).
- Build lại `Build/Watch` (ảnh chụp: HUD có icon mới, hào quang quanh Warrior, bảng lên cấp hiện
  phụ kiện mới) và `Build/TrainingNext`. Owner đang train nên chưa tráo bản train.
- Test: CoreTests 112, EditMode 90 — xanh.

### 2026-09-30 chiều muộn — Claude (PC): M4B T-019, xong M4B
- **T-019** (Codex viết, Claude review): schema JSON, `brain_upgrade.py`, 3 loại trận
  mới/ôn tập/khó (`SurvivorEnvFactory.CreateEpisode`), thống kê `Arena/Episode/Survived<Kind>`,
  curriculum theo tiến độ.
  - Sửa sau review: nâng cấp ghi vào thư mục tạm rồi mới đổi tên (lỗi giữa chừng không để lại run
    hỏng); mọi lỗi nâng cấp → học lại từ đầu kèm thông báo; service bỏ qua thư mục ẩn; trận khó chỉ
    bật sau 30% tiến độ.
- Build train mới ra `Build/TrainingNext` vì owner đang train (exe bị khóa). Service tự tráo vào khi
  bấm TRAIN lần tới.
- Test: CoreTests 85, pytest 100, EditMode 90 — xanh. `warrior-s001` đang ở 23.1M bước (bài 360 s).

### 2026-09-30 chiều — Claude (PC): M4B T-018 + T-020
- **T-018** (Codex viết, Claude review): champion/challenger, chấm 100 seed tự động mỗi 2M bước,
  telemetry phong cách đánh.
  - Sửa sau review: lỗi chấm không còn che thông báo train (tách ra `evaluation_message`).
  - Chạy thử đầu-cuối trên bản sao checkpoint thật, mỗi lần chấm khoảng 7,6 phút:
    - 16M: 682,9 điểm, thành não giỏi nhất đầu tiên;
    - 17M: 730,2 điểm, thắng và thay não giỏi nhất.
- **T-020** (Claude): phím `B` đổi não mới nhất ↔ giỏi nhất; phím/nút `P` mở màn **Hồ sơ AI**.
  - Đã kiểm: EditMode 86/86; build thử ra thư mục scratch (owner đang mở trình xem nên không ghi đè
    `Build/Watch`); đã xem ảnh chụp.
- `warrior-s001` đang ở 19M bước: đã lên bài 360 s, sống ~268 s. Reward giảm xuống ~−2 là do trận
  dài hơn, không phải AI kém đi.

### 2026-09-30 — Claude (PC): M4A code xong, train `warrior-s001`
- T-014 (Core Survivor) và T-015 (Trainer) do Codex làm; Claude review, sửa lỗi review, merge vào
  `feature/survivor-core`.
- T-016 (Claude + subagent): `HeroAgent` 3 nhánh; quyết định lại ngay sau khi chọn nâng cấp. Trình
  xem Survivor dùng asset KayKit Halloween (CC0) và icon game-icons.net (CC BY).
- Service huấn luyện: thêm `--force` khi tạo run mới; `warrior-s001` chạy trên bản build mới.
- T-017: xoá đấu trường tròn cũ (Core, View, scene `ArenaPlay`, config cũ). `BrainLocator` chỉ đọc
  `schema_version.txt`. Service báo lỗi rõ khi được yêu cầu train Mage/Archer (M7). Màn TRAINING DATA
  đổi 3 đồ thị cũ (rơi vực, hất vực, số zombie) thành: chết trước giờ, cấp đạt được, độ dài trận.
- Cập nhật AGENTS.md §6, `docs/TRAINING.md`, RESOURCES, agent `core-sim-engineer` theo Survivor.
- Test: CoreTests 69, pytest 70, EditMode 78 — xanh. `WatchBuild` và `SurvivorEval` build được.
- `warrior-s001` ở 13.45M bước: reward −5.5 → 2.3, chết 100% → 33%, sống 44 → 165/180 s, vẫn bài
  180 s (cần reward 5.0). Tới 14.55M reward lên ~4.1; owner dừng êm từ trình xem lúc 11:52.
- CI: CoreTests chạy bản Release (bản Debug chậm hơn ngân sách hiệu năng). `develop` và `main` đã
  gồm M4A; các worktree/nhánh Codex đã merge đều đã xoá.

### 2026-09-30 — Claude (PC): hướng mới Survivor
- Owner muốn game thành kiểu Vampire Survivors: bắt đầu với 1 Warrior, AI tự học đánh, nhặt EXP, chọn
  nâng cấp, farm vàng; vàng mua cấp/điểm chỉ số, class, độ khó cao rớt nhiều vàng; AI thích nghi theo
  build và học tiếp khi game cập nhật. Owner chọn: vũ khí tự bắn + AI điều khiển, bản đồ rộng có giới
  hạn, trận 15 phút, thay hẳn đấu trường cũ.
- Nghiên cứu VS (wiki): PowerUps, đường EXP, lên cấp 3–4 lựa chọn, 6+6 món, rương tinh anh, Greed/Curse.
- Viết `docs/GDD.md`, D-026..D-029, lộ trình M4–M7.
- Owner gửi bản góp ý thiết kế dài → nhận hết (D-030): học phải "cảm nhận được" (Behavior Profile),
  thưởng thứ bậc (kết quả > sống > máu > tiến độ > vàng, bỏ thưởng hạ quái), quan sát chuẩn hóa +
  đặc trưng chuyển động (2264), bỏ Thần chết (hết giờ 17:00), champion/challenger, curriculum ôn tập,
  đánh giá 100 seed; chia M4A/M4B/M4C; M5 thêm loadout, Training Focus, modifier độ khó, lớp phủ
  khán giả, báo cáo sau trận; M6 Brain Lineage. Viết lại GDD, PLAN, T-014, T-015; Codex chạy lại.

### 2026-09-30 01:00 — Claude (PC): icon skill từ game-icons.net
- Owner duyệt dùng icon miễn phí và đặt hướng mới: **ưu tiên asset có sẵn / free** (D-025, thay D-011;
  cho phép CC BY nếu ghi công trong `ThirdParty/CREDITS.md`).
- 12 icon của Lorc/Delapouite (CC BY 3.0) ở `ThirdParty/GameIcons/Resources/SkillIcons/<skill-id>.png`;
  `SkillIconFactory` giữ nền màu, thay hình tự vẽ bằng icon này; `SkillIconImporter` giữ ảnh đọc được lúc chạy.
- EditMode 48/48, build lại `Build/Watch`, ảnh chụp Archer thấy icon mới. warrior-003 vẫn train (3.18M, reward 70.9, 4 zombie).

### 2026-09-29 23:00 – 2026-09-30 00:45 — Claude (PC): M3 vào game, icon skill
- M3 tích hợp đủ: 4 loại zombie (Walker/Runner/Brute/Spitter), 3 class (Warrior/Mage/Archer), luật v3,
  883 quan sát. Trình xem: phím H đổi class, M đổi kiểu zombie, nút TRAIN train class đang chọn.
- warrior-002 dừng êm ở 48.9M (luật v2). Smoke train luật v3 (4 env × 64, MAX): mage-001 reward 0 → 10.9
  ở 600k; archer-001 32.9 ở 1.08M (lên 2 zombie); warrior-003 là run mới, **vẫn đang train** (1.4M, 19.5).
- Owner yêu cầu: bỏ gợi ý phím tắt ở thanh skill (owner không điều khiển) + artwork cho skill. Vẽ 12 icon
  bằng code (4/class, `docs/images/skill-icons.png`), không tải gói ngoài. Hồi chiêu = quét kim đồng hồ
  chỉ trên icon.
- Lỗi: icon luôn tối dù skill sẵn sàng. Nguyên nhân: Image `Filled` không có sprite → Unity bỏ qua
  `fillAmount`, phủ cả khung. Gán sprite + test `ArenaHudSkillBarTests`.
- Test: CoreTests 97, pytest 62, EditMode 48 — tất cả xanh. Build lại `Build/Watch`, ảnh chụp đúng.

### 2026-09-29 22:45 — Claude (PC): sửa lỗi hình (chấm trắng, vệt đen)
- Chấm trắng trên đầu mọi nhân vật: `CreateStunStars` gọi `SetVisible(false)` nhưng `Visible` mặc định đã
  false nên hàm return sớm → 3 sao choáng nằm phẳng trên đầu suốt trận. Giờ tắt trực tiếp khi tạo.
- Sao choáng làm lại: sao vàng 5 cánh viền cam đậm (alpha-blend, `FxAssets.StunStarMaterial`), to hơn,
  quay rộng hơn; zombie bị choáng còn lắc lư chóng mặt (`BodyRoot.localRotation`).
- Vệt đen quanh warrior: mép khiên Block. `Sin(PI)` float ra -8.7e-8, `Pow(âm, 0.8)` = NaN → mép đen.
  Kẹp về 0 (`BuildShieldBand`); cũng kẹp độ mờ của nhát chém.
- Build lại `Build/Watch`, kiểm tra ảnh chụp: hết chấm trắng, khiên không còn viền đen. warrior-002 vẫn train (~40.7M).

### 2026-09-29 16:30–19:30 — Claude (PC): luật v2, đồ họa sống động, TRAINING DATA
- Owner muốn: đấu trường tròn giữa vực (rơi là chết, cả zombie), rộng hơn; di chuyển mượt, dash
  không như teleport; hiệu ứng cho strike/kick/block/dash; kick hất lùi, block làm choáng thấy
  được; bình máu rớt từ zombie; camera xoay/zoom; màn hình đồ thị học tập; nút/HUD đẹp hơn.
- Làm theo D-022 (Claude + Codex; Claude review, tích hợp, build, kiểm trên bản build). Bỏ
  `DungeonDressing`; thêm `ArenaStage`, `ArenaEffects`, `UiSprites`, `TrainingHistoryPanel`,
  `TrainingHistory.cs`, `Trainer/training_history.py`. Trình xem chỉ nạp não đúng `RulesVersion`.
- Train `warrior-002` từ đầu ở MAX: 24M bước sau ~2.5 giờ, reward ~290, ~143 zombie/ván ở bài 16.
- Lỗi đã sửa: trùng tên class `Slash` trong `ArenaEffects` (CS0102) → `SlashFx`. Click giả lập
  vào nút TRAINING DATA không ăn (cửa sổ đổi kích thước khi focus) → thêm phím `G`.
- Kiểm: CoreTests 73/73, pytest 47/47, EditMode 38/38, build watch OK, ảnh chụp sàn tròn + 6 đồ thị.

### 2026-09-29 15:30–16:30 — Claude (PC): train "tự tắt", nút Power
- Owner báo bấm TRAIN thì vài giây sau tự tắt. Nguyên nhân tìm được: mlagents crash `0xC0000409`
  trong `nvcuda64.dll` ~7 s sau khi khởi động (1 lần trong 10 ngày log, chạy lại thì ổn), và giao
  diện chỉ hiện "Stopped" khi service thoát nhanh (lỗi bị che bởi status cũ). Các lần STOP bằng
  script trong lúc Claude test cũng trông như "tự tắt".
- Sửa: service tự chạy lại từ checkpoint (tối đa 5, CPU sau 2 crash nhanh), ghi lỗi vào
  `training_service.err.log`, viewer hiện lỗi khi service thoát sớm (`LaunchFailure`). Service
  mới chờ tối đa 15 s cho service cũ nhả lock (đổi Power = dừng rồi chạy lại ngay).
- Owner muốn train nhiều con cùng lúc → đo 13 cấu hình (bảng ở TRAINING.md), thêm nút Power.
  "View speed" chỉ là tốc độ xem.
- Kiểm trên bản build: TRAIN ở FAST (128 arena) chạy; đổi sang MAX khi đang train → lưu 10,220,014
  và chạy lại 256 arena sau 1 s. pytest 34/34, EditMode 32/32. Để `warrior-001` train tiếp ở MAX.
- Lưu ý khi lái viewer bằng script: cú click đầu vào cửa sổ chưa focus có thể bị nuốt → click lại.

### 2026-09-29 15:00–15:30 — Claude (PC): nút "TRAIN THE AI"
- Owner không muốn phải nhờ Claude mỗi lần train → thêm service train chạy ngầm + bảng TRAINING
  trong trình xem (D-020). Giao tiếp qua file trong `Trainer/runs/`; dừng bằng Ctrl+C (đã thử
  riêng: console ẩn của .NET nhận được `GenerateConsoleCtrlEvent`).
- Chuyển `warrior-001` từ terminal sang nút ngay sau checkpoint 9.5M (kill cứng → mất tiến độ
  curriculum, xem mục 1 "Việc tiếp theo").
- Kiểm đầu-cuối trên bản build: bấm TRAIN → STOP TRAINING, bước + biểu đồ hiện đúng; bấm STOP →
  "Progress saved at step 9,727,471", có `training_status.json`; đóng cửa sổ khi đang train →
  dừng êm, lưu 9,867,350, không sót process. pytest 25/25, EditMode 29/29.

### 2026-09-29 14:00–15:00 — Claude (PC): đồ họa KayKit
- Owner muốn game đẹp hơn, chọn bộ KayKit (CC0). Tải 4 pack free vào `ThirdParty/KayKit`.
- Thêm `ArenaArtSet`, `CharacterAnimator`, `DungeonDressing`, `KayKitArtSetBuilder`; viết lại
  `ArenaRenderer` (giữ đường hình khối cũ làm dự phòng); `TopDownCamera` thành phối cảnh nghiêng,
  tự tìm khoảng cách vừa khung arena; ánh sáng tối + sương mù + đuốc.
- Lỗi đã sửa: gán art set trước `NewScene` làm scene lưu `artSet: {fileID: 0}` → nạp lại sau.
- Kiểm: build watch OK, ảnh chụp đúng, Player.log không lỗi, EditMode 20/20.

### 2026-09-29 13:20–13:50 — Claude (PC): training warrior-001, trình xem AI
- Bắt đầu `warrior-001` (4 env × 16 arena). 1M bước sau ~15 phút; curriculum đã lên 16 zombie.
- Owner muốn xem AI train trực tiếp → phát hiện bản build không nạp được ONNX (importer chỉ có
  trong Editor) → tự viết định dạng `.brain` + MLP C# (D-017), khớp mlagents sai số ~5e-7.
- Thêm `export_brain.py --watch`, scene `ArenaWatch`, `AiArenaController` (tự nạp não mới,
  đổi số zombie/tốc độ), `WatchBuild`, `Xem-AI.cmd`. Đã chạy thử: não 1M bước giết 8 zombie/ván.

### 2026-09-29 tối — Claude (PC): rà soát, thống nhất bản chính thức
- Kiểm tra GitHub: không còn PR/nhánh nào của phiên cloud chưa merge.
- Sửa lỗi: `TrainingBuild` để lại cài đặt player 640x360/windowed cho cả game → nay lưu và trả lại
  trong `finally`. Đã kiểm: build env OK, `ProjectSettings.asset` không đổi, EditMode 18/18, pytest 3/3.
- `main` fast-forward = `develop`; xóa worktree `-m1view`, `-m2agent` và các nhánh tính năng đã merge.
- Owner chốt hướng: dừng PersonalGameAI, chỉ làm Personal Arena (D-016); repo public để CI miễn
  phí (D-015, thay D-002). CI đã xanh. Cập nhật PLAN, AGENTS, DECISIONS cho khớp.
- Lưu ý: chạy test EditMode làm package Inference bỏ define `SENTIS_ANALYTICS_ENABLED` trong
  `ProjectSettings.asset` — đừng commit thay đổi đó cho tới khi quyết định chuyện analytics.

### 2026-09-29 chiều — Claude (PC): đóng M1, code M2
- Sửa bản build đen (shader Standard), chụp màn hình xác nhận; merge PR #5 (M1 view).
- M2: Codex viết `HeroAgent`, env train, trainer; Claude chạy Unity headless (scene, test 9/9,
  build env), pytest 3/3, đổi tên tham số `dmg_mult` → `damage_mult` cho khớp config; merge PR M2.
- Ghi quy tắc "toàn quyền, không hỏi owner chuyện kỹ thuật" vào `AGENTS.md`.

### 2026-09-29 11:30 — Claude (cloud): dọn dẹp và dựng hệ thống bàn giao
- Tách tài liệu: `AGENTS.md`, `CLAUDE.md`, `docs/STATUS.md`, `DECISIONS.md`, `SETUP.md`,
  `TRAINING.md`, `RESOURCES.md`, `docs/tasks/` (bảng việc + T-001..T-007).
- Thêm subagent, skill, `.claude/settings.json`, `.editorconfig`, CI, template PPO Warrior.

### 2026-09-29 09:42–10:06 — Owner + Claude (PC)
- Scaffold M0; PR #2 Core M1; PR #3 Unity project + ML-Agents 4.1.0; PR #4 `HeroInput` helpers.
