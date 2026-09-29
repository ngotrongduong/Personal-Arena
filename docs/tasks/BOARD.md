# Bảng việc

Cập nhật mỗi khi đổi trạng thái. Chi tiết ở từng file task.

| Task | Tiêu đề | Owner | Trạng thái | Milestone | Song song được với |
|---|---|---|---|---|---|
| [T-000](T-000-unity-project-into-repo.md) | Đưa Unity project lên repo, xác nhận venv + GPU | Owner | todo | M0 | tất cả |
| [T-001](T-001-rng.md) | `Rng` deterministic (PCG32) | Codex | todo | M0 | T-002, T-003 |
| [T-002](T-002-arena-config.md) | `ArenaConfig` + kiểm tra hợp lệ + preset | Codex | todo | M0 | T-001, T-003 |
| [T-003](T-003-combat-stats.md) | `SimConstants`, `Health`, `Energy`, `Cooldown`, `StunTimer` | Codex | todo | M0 | T-001, T-002 |
| [T-004](T-004-sim-events-rewards.md) | `SimEvent` + `RewardCalculator` + test dấu | Claude (spec) → Codex | todo | M1 | — (sau T-003) |
| [T-005](T-005-arena-sim-minimal.md) | `ArenaSim` tối thiểu: 1 hero, N walker, đòn đánh cơ bản | Claude (spec) → Codex | todo | M1 | — (sau T-001..T-004) |
| [T-006](T-006-onnx-runtime-spike.md) | Spike nạp ONNX lúc runtime trong bản build | Claude + Owner | todo | M0 | T-001..T-003 |
