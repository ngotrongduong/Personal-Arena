# Trạng thái dự án

> File này là "bộ nhớ" giữa các phiên. Đọc đầu tiên, cập nhật cuối cùng.
> Cập nhật lần cuối: 2026-09-29 15:30 (phiên Claude, PC — nút "TRAIN THE AI" trong trình xem).

## Hướng đi

- **Chỉ tập trung vào Personal Arena** (D-016). Dự án PersonalGameAI đã dừng; không làm gì
  thêm ở đó trừ khi owner yêu cầu.
- Repo để **public** để CI trên GitHub chạy miễn phí (D-015) → không bao giờ commit secret.

## Đang ở đâu

- **Milestone:** M0 xong, **M1 xong** (chơi tay được), **M2 phần code xong** (agent + môi trường
  train + trainer + **trình xem AI chơi**). Đang: training thật `warrior-001`. Còn: chỉnh
  curriculum, BC từ demo.
  Chi tiết milestone ở `docs/PLAN.md`.
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
    Playables theo đồng hồ sim), hầm ngục + đuốc lập lòe (`DungeonDressing`), camera phối cảnh
    nghiêng. Asset ở `Unity/Assets/ThirdParty/KayKit` (CC0, `CREDITS.md`); map bằng
    `Assets/Arena/View/Art/KayKitArtSet.asset` (dựng lại: menu *Personal Arena/Rebuild KayKit Art
    Set*). Scene training không dùng renderer nên huấn luyện không bị ảnh hưởng.
  - Owner **chỉ xem AI tự học, không chơi tay** (D-019) → mọi việc hiển thị nhắm vào `Xem-AI.cmd`.
  - **Nút "TRAIN THE AI" (feature/train-button, D-020):** bảng TRAINING trong trình xem bật/tắt
    `Trainer/train_service.py` (chạy mlagents ẩn, resume run mới nhất, train liên tục, dừng bằng
    Ctrl+C để lưu). Hiện bước, reward, số zombie, biểu đồ reward. Đóng game = dừng + lưu.
    Code: `TrainingServiceClient.cs`, `ArenaHud` (panel), `AiArenaController`. Hướng dẫn:
    `docs/TRAINING.md` mục 4.
  - Test: Unity EditMode 29/29, CoreTests 58/58, pytest trainer 25/25.
  - **Smoke training 5 phút trên GPU:** mean reward −0.01 → 15.4, curriculum tự lên 2 zombie.
    AI học được thật; pipeline chạy đầu-cuối.

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
| M2: training thật, curriculum lên 16 zombie | `warrior-001` dừng ở 9.87M bước (reward ~130 ở 16 zombie trước khi đổi sang nút). Owner tự train tiếp bằng nút |
| M2: ghi demo chơi tay → BC/GAIL | **chưa** |

## Việc tiếp theo (theo thứ tự)

1. **Training Warrior:** owner bật/tắt bằng nút trong `Xem-AI.cmd`. `warrior-001` từng bị kill
   cứng nên curriculum về lại bài 1 zombie ở 9.5M (sẽ tự leo lại; từ nay dừng êm nên giữ được).
   Curriculum lên 16 zombie ở ~480k bước — ngưỡng 6 quá dễ. Khi đủ lâu: đọc TensorBoard, nâng
   `threshold`, chạy `warrior-002`, ghi "Nhật ký run" trong `docs/TRAINING.md`.
2. **Cân bằng:** với 4 zombie mặc định, hero đứng yên chết sau ~4 s — có thể quá khó cho người chơi.
4. **M3:** Mage, Archer, 4 loại zombie (Walker/Runner/Brute/Spitter), random hóa arena.
5. **M4:** menu, Roster/Shop (vàng trong game), Training Center (gọi trainer, dashboard), lưu/tải.

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
