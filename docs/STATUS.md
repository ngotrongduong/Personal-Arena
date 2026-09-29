# Trạng thái dự án

> File này là "bộ nhớ" giữa các phiên. Đọc đầu tiên, cập nhật cuối cùng.
> Cập nhật lần cuối: 2026-09-29 (phiên Claude, cloud).

## Đang ở đâu

- **Milestone:** M0 (thiết lập và spike). Chi tiết milestone ở `docs/PLAN.md`.
- **Nhánh chính:** `main`. CI (`dotnet test CoreTests` + guardrails) đã cấu hình nhưng đang bị
  GitHub chặn vì billing — xem "Vướng mắc".
- **Có trong repo:** khung thư mục, quy tắc agent, `Vec2` + 2 test, lock file môi trường ML,
  hệ thống tài liệu/agents/skills, template cấu hình PPO cho Warrior.
- **Chưa có trong repo:** phần Unity project thật (`Unity/Packages/`, `Unity/ProjectSettings/`).
  Hiện `Unity/` chỉ có `Assets/Arena/Core`.

## M0: checklist

| Việc | Trạng thái |
|---|---|
| Repo private + `.gitignore` Unity + `.gitattributes` | xong |
| Core assembly thuần C# + CoreTests (dotnet) | xong |
| Lock môi trường ML (Py 3.10.12, torch 2.2.2+cu121, mlagents 1.1.0) | xong (lock đã có; venv trên PC cần xác nhận) |
| Tài liệu bàn giao phiên, agents, skills, CI | xong |
| Đưa Unity project (Packages + ProjectSettings) lên repo, cài `com.unity.ml-agents` 4.x | **chưa** — T-000 |
| Chạy thử `mlagents-learn` trên GPU (3DBall hoặc tương tự) | **cần xác nhận** — T-000 |
| Spike: nạp ONNX lúc runtime trong bản build | **chưa** — T-006 |

## Việc tiếp theo (theo thứ tự)

1. **Owner:** T-000 — đưa Unity project lên repo và xác nhận venv + GPU (hướng dẫn trong task).
2. **Codex:** T-001 `Rng`, T-002 `ArenaConfig`, T-003 chỉ số chiến đấu (làm song song được).
3. **Claude:** review + merge T-001..T-003, rồi viết T-004 (`SimEvent` + `RewardCalculator`)
   và T-005 (`ArenaSim` tối thiểu).
4. **Claude + Owner:** T-006 spike ONNX runtime → đóng M0, mở M1.

## Vướng mắc / câu hỏi mở

- **CI chưa chạy được:** GitHub báo "recent account payments have failed or your spending
  limit needs to be increased". Owner cần vào GitHub → Settings → Billing & plans sửa thanh
  toán / spending limit cho Actions. Workflow đã sẵn sàng (`.github/workflows/ci.yml`).
- Phiên cloud không cài được .NET SDK (proxy chặn), nên cho tới khi CI chạy, test Core chỉ
  chạy được trên PC (`dotnet test CoreTests`).
- Unity MCP: chọn CoplayDev `unity-mcp` (miễn phí, MIT) hay MCP chính thức của Unity (cần
  gói AI trả phí)? Đề xuất: CoplayDev. Xem `docs/DECISIONS.md` D-008.

## Nhật ký phiên (mới nhất trên cùng)

### 2026-09-29 — Claude (cloud): dọn dẹp và dựng hệ thống bàn giao
- Kết nối repo từ cloud. Rà toàn bộ repo.
- Tách tài liệu: `AGENTS.md` (quy tắc chung), `CLAUDE.md` (bộ nhớ Claude), `docs/STATUS.md`,
  `docs/DECISIONS.md`, `docs/SETUP.md`, `docs/TRAINING.md`, `docs/RESOURCES.md`,
  `docs/tasks/` (bảng việc + task cho Codex). Rút gọn `README.md` và `docs/PLAN.md`, bỏ phần
  trùng lặp.
- Thêm 4 subagent, 4 skill Claude, 1 skill Codex, `.claude/settings.json` (toàn quyền sửa file
  + hook in STATUS đầu phiên), `.editorconfig`, CI GitHub Actions, template PPO Warrior có
  curriculum 1→16 zombie.
- Sửa mâu thuẫn: PLAN nói PR vào `develop` (không tồn tại) → dùng `main`.

### 2026-09-29 09:42 — Owner + Claude (PC): scaffold M0
- Commit đầu tiên: layout, AGENTS.md, PLAN, Core `Vec2`, CoreTests, lock ML.
