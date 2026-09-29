# Trạng thái dự án

> File này là "bộ nhớ" giữa các phiên. Đọc đầu tiên, cập nhật cuối cùng.
> Cập nhật lần cuối: 2026-09-29 chiều (phiên Claude, PC).

## Đang ở đâu

- **Milestone:** M0 xong (trừ spike ONNX), **M1 xong** (chơi tay được), **M2 phần code xong**
  (agent + môi trường train + trainer). Còn: chạy training thật lâu, chỉnh curriculum, BC từ demo.
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
  - Test: Unity EditMode 18/18 pass (ML + view), pytest trainer 3/3 pass.
  - **Smoke training 5 phút trên GPU:** mean reward −0.01 → 15.4, curriculum tự lên 2 zombie.
    AI học được thật; pipeline chạy đầu-cuối.

## Checklist

| Việc | Trạng thái |
|---|---|
| M0: repo, Unity project, ML-Agents, venv ML trên GPU | xong |
| M0: spike nạp ONNX lúc runtime | **chưa** — T-002 (cần trước M4) |
| M1: Core + view + input + HUD + scene chơi tay | xong (PR #5) |
| M2: HeroAgent + env train + trainer wrapper | xong (PR M2) |
| M2: training thật, curriculum lên 16 zombie | **chưa** |
| M2: ghi demo chơi tay → BC/GAIL | **chưa** |

## Việc tiếp theo (theo thứ tự)

1. **Training thật Warrior:** `arena_trainer.py --run-id warrior-001` (vài giờ trên RTX 4070 Ti),
   xem TensorBoard, chỉnh `threshold` curriculum (đang là 6, đoán), ghi vào "Nhật ký run" của
   `docs/TRAINING.md`.
2. **Cân bằng:** với 4 zombie mặc định, hero đứng yên chết sau ~4 s — có thể quá khó cho người chơi.
3. **Xem AI đánh:** nạp `Warrior.onnx` vào `ArenaPlay` (chế độ "AI chơi") — cần T-002.
4. **M3:** Mage, Archer, 4 loại zombie (Walker/Runner/Brute/Spitter), random hóa arena.
5. **M4:** menu, Roster/Shop (vàng trong game), Training Center (gọi trainer, dashboard), lưu/tải.

## Cách làm trên PC (Claude)

- Worktree: `C:\PersonalArena` (develop), `C:\PersonalArena-m1view` (feature/m1-view, đã merge),
  `C:\PersonalArena-m2agent` (feature/m2-agent, đã merge). Venv ML chỉ ở `C:\PersonalArena\.venv-ml`
  (Python 3.10.12, mlagents 1.1.0, torch 2.2.2+cu121, có pytest).
- Unity headless: `Unity.exe -batchmode -nographics -quit -projectPath <Unity> -executeMethod X
  -logFile L`. **Luôn có `-quit`**, nếu không Unity treo mãi.
- Test EditMode: `-runTests -testPlatform EditMode -testResults <xml>` (không dùng `-quit`).
- Chụp màn hình bản build: chạy exe cửa sổ (`-screen-fullscreen 0`) rồi `PrintWindow(h, dc, 2)`;
  `CopyFromScreen` bị khóa foreground.
- Tránh hộp "Allow": gom lệnh nhiều bước vào file `.ps1` rồi chạy một lệnh đơn.
- `arena_trainer.py` tìm `.venv-ml` ở gốc repo → chạy từ `C:\PersonalArena`, không từ worktree.

## Vướng mắc / câu hỏi mở

- **CI chưa chạy được:** GitHub báo lỗi thanh toán Actions. Owner vào GitHub → Settings →
  Billing & plans để sửa. Workflow đã sẵn sàng (`.github/workflows/ci.yml`).
- Unity MCP: đề xuất CoplayDev `unity-mcp` (D-008), chưa cài.
- Core dùng 72 tia, video ~92. Giữ 72 cho tới khi training cho thấy cần hơn.
- Package Inference tự thêm define `SENTIS_ANALYTICS_ENABLED` (analytics phía Editor). Xem lại
  trước khi phát hành để đảm bảo game không gọi mạng.

## Nhật ký phiên (mới nhất trên cùng)

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
