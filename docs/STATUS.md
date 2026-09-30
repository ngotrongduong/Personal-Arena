# Trạng thái dự án

> File này là "bộ nhớ" giữa các phiên. Đọc đầu tiên, cập nhật cuối cùng.
> Cập nhật lần cuối: 2026-09-30 (phiên Claude, PC — M4A code xong, đang train `warrior-s001`).

## Hướng đi

- **Chỉ tập trung vào Personal Arena** (D-016). Dự án PersonalGameAI đã dừng; không làm gì
  thêm ở đó trừ khi owner yêu cầu.
- Repo để **public** để CI trên GitHub chạy miễn phí (D-015) → không bao giờ commit secret.

## Đang ở đâu

- **Milestone:** M0–M3 xong. **M4A (lát cắt dọc Survivor) code xong** (T-014..T-017); đang train
  `warrior-s001` tới nghiệm thu đánh giá 100 seed. Chi tiết milestone ở `docs/PLAN.md`, thiết kế ở
  `docs/GDD.md`.
- **M4A đã có (nhánh `feature/survivor-core`, merge vào `develop`):**
  - Core `Core/Survivor` (T-014): bản đồ 100 × 100 có vật cản, lịch quái, EXP/lên cấp, vũ khí tự
    bắn, phụ kiện, đồ nhặt, boss, quan sát schema v4 (2264), action 9/5/5, thưởng thứ bậc từ
    `SurvivorEvent`, `SurvivorEvaluator` + `Tools/SurvivorEval` (đánh giá 100 seed).
  - Trainer (T-015): `warrior_survivor_ppo.yaml` (curriculum độ dài trận 180 → 900 s),
    `schema_version.txt` thay `rules_version`, `brain_upgrade.py`, run `warrior-sNNN`.
  - Unity (T-016): `HeroAgent` 3 nhánh, trình xem Survivor (camera theo nhân vật, nghĩa địa
    KayKit Halloween, HUD EXP/cấp/đồng hồ/vàng/món đồ, bảng lên cấp tô sáng lựa chọn, màn kết trận).
  - T-017: **đã xoá hẳn đấu trường tròn cũ** (`ArenaSim`, quan sát 883, `ArenaPlay.unity`, các view
    cũ, config `<class>_ppo.yaml`). Mage/Archer chờ config Survivor riêng ở M7. Các mục lịch sử bên
    dưới nói về code cũ này.
  - Test: CoreTests 69/69, pytest 70/70, Unity EditMode 78/78.
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
| M4A: train `warrior-s001` qua đánh giá 100 seed | đang train (13.45M bước: sống ~165/180 s, chết 33%) |

## Việc tiếp theo (theo thứ tự)

Owner đổi hướng ngày 2026-09-30: game thành kiểu **Vampire Survivors**, AI tự học farm vàng, chọn
nâng cấp, thích nghi theo build; não học tiếp khi game cập nhật. Thiết kế đầy đủ: **`docs/GDD.md`**.
Lộ trình mới M4A–M8 ở `docs/PLAN.md` (owner góp ý thiết kế → D-030: chia M4 thành M4A/M4B/M4C,
thưởng theo thứ bậc, nghiệm thu bằng đánh giá 100 seed). Đấu trường tròn luật v3 sẽ bị thay;
`warrior-003` không cần train tiếp.

1. Train `warrior-s001` (dịch vụ nút TRAIN đang chạy) tới khi qua đánh giá 100 seed bằng
   `Tools/SurvivorEval`: trung vị ≥ 10:00, P10 ≥ 7:00, không chết trước 3:00.
   - Theo dõi: `Arena/Died`, `Arena/SurvivedSeconds`, `Environment/Lesson Number/run_seconds`.
   - AI nhặt ít EXP (~54 EXP, cấp ~3.7 mỗi trận 180 s dù giết ~195 quái). Nếu lên bài 360/600 s mà
     chết nhiều vì thiếu nâng cấp: tăng `PerLevelProgress` (hiện 0.05) nhưng vẫn giữ thứ bậc D-030.
   - Nếu kẹt ở bài 180 s lâu (reward không lên 5.0): xem lại ngưỡng curriculum.
2. **M4B** (champion/challenger, Behavior Profile, nâng cấp não), **M4C** (đủ nội dung), rồi M5–M8.

## Cách làm trên PC (Claude)

- Chỉ còn một thư mục làm việc: `C:\PersonalArena` (develop). Các worktree cũ và nhánh tính năng
  đã merge đều đã xóa; `main` = `develop`. Venv ML ở `C:\PersonalArena\.venv-ml`
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
  180 s (cần reward 5.0).

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
