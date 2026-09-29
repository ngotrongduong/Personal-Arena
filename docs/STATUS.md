# Trạng thái dự án

> File này là "bộ nhớ" giữa các phiên. Đọc đầu tiên, cập nhật cuối cùng.
> Cập nhật lần cuối: 2026-09-29 (phiên Claude, cloud).

## Đang ở đâu

- **Milestone:** M0 gần xong, **M1 phần Core xong**, M1 phần Unity (xem và chơi tay) chưa làm.
  Chi tiết milestone ở `docs/PLAN.md`.
- **Nhánh:** làm việc trên `develop` (nhánh tính năng → PR/merge vào `develop`); `main` được
  fast-forward theo `develop` khi ổn định. Nhánh mặc định trên GitHub là `main`.
- **Đã có trên `develop`:**
  - Unity 6000.3.2f1 project đầy đủ, ML-Agents **4.1.0** + Inference **2.6.1**, Input System,
    uGUI, Test Framework (PR #3).
  - Core M1 (PR #2): `ArenaSim` 60 Hz deterministic (Warrior vs Walker: đâm, đá choáng, khiên +
    parry, lao, backstab), `Rng` PCG32, `ArenaConfig`, `Defs`, `RaySensor` 72 tia,
    `ObservationBuilder` (736 số), `RewardCalculator` + test dấu từng reward. Ghi chú:
    `docs/M1-core-notes.md`.
  - `HeroInput` helpers dùng chung cho bàn phím và heuristic (PR #4). Tổng 50 test NUnit.
  - Hệ thống tài liệu, agents, skills, CI (phiên này).

## Checklist

| Việc | Trạng thái |
|---|---|
| M0: repo, `.gitignore`, Core assembly, CoreTests, lock ML | xong |
| M0: Unity project + ML-Agents 4.1.0 trong repo | xong |
| M0: venv ML chạy trên GPU | **cần xác nhận** — T-001 |
| M0: spike nạp ONNX lúc runtime | **chưa** — T-002 |
| M1: Core combat + test | xong |
| M1: view, input, HUD, scene chơi tay | **chưa** — T-003..T-006 |

## Việc tiếp theo (theo thứ tự)

1. **Codex:** T-003 (`ArenaRunner` + view), rồi T-004 và T-005 song song.
2. **Owner:** T-001 (5 phút: kiểm tra CUDA). Sửa billing GitHub để CI chạy (xem Vướng mắc).
3. **Claude (khi PC được liên kết):** review + merge T-003..T-005, làm T-006 scene → owner chơi thử
   → đóng M1, fast-forward `main`.
4. **Claude:** T-002 spike ONNX, T-007 spec `HeroAgent` → bắt đầu M2.

## Vướng mắc / câu hỏi mở

- **CI chưa chạy được:** GitHub báo "recent account payments have failed or your spending
  limit needs to be increased". Owner vào GitHub → Settings → Billing & plans, sửa thanh toán
  hoặc spending limit cho Actions. Workflow đã sẵn sàng (`.github/workflows/ci.yml`).
- Phiên cloud không cài được .NET SDK (proxy chặn): trước khi CI chạy, test Core chỉ chạy trên PC.
- Unity MCP: đề xuất CoplayDev `unity-mcp` (D-008), chờ owner đồng ý và cài.
- Core dùng 72 tia (`RaySensorConfig.RayCount`), video dùng ~92. Giữ 72 cho tới khi training
  cho thấy cần hơn.
- Unity Fixed Timestep đang là 0.02 s (mặc định), không phải 1/60. Runner (T-003) tự đếm tick;
  M2 phải quyết định (ghi trong T-007).

## Nhật ký phiên (mới nhất trên cùng)

### 2026-09-29 11:30 — Claude (cloud): dọn dẹp và dựng hệ thống bàn giao
- Kết nối repo từ cloud; rà cả `main` và `develop`.
- Tách tài liệu: `AGENTS.md` (quy tắc chung), `CLAUDE.md`, `docs/STATUS.md`, `DECISIONS.md`,
  `SETUP.md`, `TRAINING.md`, `RESOURCES.md`, `docs/tasks/` (bảng việc + task T-001..T-007).
  Rút gọn README và PLAN, bỏ phần trùng.
- Thêm 4 subagent, 4 skill Claude, 2 skill Codex, `.claude/settings.json` (toàn quyền sửa file,
  hook in STATUS đầu phiên), `.editorconfig`, CI, template PPO Warrior có curriculum 1→16.
- Lỡ merge tài liệu vào `main` trước khi thấy `develop` (clone ban đầu chỉ có `main`); đã
  merge `main` vào `develop` và fast-forward `main` = `develop` để hai nhánh khớp.

### 2026-09-29 09:42–10:06 — Owner + Claude (PC)
- Scaffold M0; PR #2 Core M1; PR #3 Unity project + ML-Agents 4.1.0; PR #4 `HeroInput` helpers.
