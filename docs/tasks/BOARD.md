# Bảng việc

Cập nhật mỗi khi đổi trạng thái. Chi tiết ở từng file task. Việc đã xong trước khi có bảng
này (M0 scaffold, Unity project, Core M1) ghi ở `docs/STATUS.md`.

| Task | Tiêu đề | Owner | Trạng thái | Milestone | Song song được với |
|---|---|---|---|---|---|
| [T-001](T-001-verify-gpu-training.md) | Xác nhận venv ML + GPU (smoke test) | Owner | todo | M0 | tất cả |
| [T-002](T-002-onnx-runtime-spike.md) | Spike nạp ONNX lúc runtime trong bản build | Claude + Owner | todo | M0 | T-003..T-005 |
| [T-003](T-003-arena-view-runner.md) | `ArenaRunner`, `HeroView`, `ZombieView` | Codex | todo | M1 | T-001, T-002 |
| [T-004](T-004-keyboard-mouse-input.md) | Điều khiển bàn phím + chuột | Codex | todo | M1 | T-005 (sau T-003) |
| [T-005](T-005-arena-hud.md) | HUD máu / năng lượng / cooldown | Codex | todo | M1 | T-004 (sau T-003) |
| [T-006](T-006-manual-arena-scene.md) | Scene `ManualArena`, chơi tay → đóng M1 | Claude (PC) | todo | M1 | — (sau T-003..T-005) |
| [T-007](T-007-hero-agent-spec.md) | Spec `HeroAgent` (mở M2) | Claude | todo | M2 | T-003..T-006 |
