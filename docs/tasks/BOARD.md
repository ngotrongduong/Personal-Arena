# Bảng việc

Cập nhật mỗi khi đổi trạng thái. Chi tiết ở từng file task. Việc đã xong trước khi có bảng
này (M0 scaffold, Unity project, Core M1) ghi ở `docs/STATUS.md`.

| Task | Tiêu đề | Owner | Trạng thái | Milestone | Song song được với |
|---|---|---|---|---|---|
| [T-001](T-001-verify-gpu-training.md) | Xác nhận venv ML + GPU (smoke test) | Claude (PC) | done (xem STATUS) | M0 | tất cả |
| [T-002](T-002-onnx-runtime-spike.md) | Spike nạp ONNX lúc runtime trong bản build | Claude (PC) | done — thay bằng định dạng `.brain` (D-017) | M0 | tất cả |
| [T-003](T-003-arena-view-runner.md) | `ArenaRunner`, `HeroView`, `ZombieView` | Codex | done (PR #5) | M1 | |
| [T-004](T-004-keyboard-mouse-input.md) | Điều khiển bàn phím + chuột | Codex | done (PR #5) | M1 | |
| [T-005](T-005-arena-hud.md) | HUD máu / năng lượng / cooldown | Codex | done (PR #5) | M1 | |
| [T-006](T-006-manual-arena-scene.md) | Scene chơi tay (`ArenaPlay`) → đóng M1 | Claude (PC) | done (PR #5) | M1 | |
| [T-007](T-007-hero-agent-spec.md) | `HeroAgent` + env train + trainer (mở M2) | Claude + Codex | done (PR M2, xem `docs/M2-agent-notes.md`) | M2 | |
| T-008 | Training thật Warrior, chỉnh curriculum, ghi Nhật ký run | Claude (PC) | doing — run `warrior-001` đang chạy | M2 | |
| T-009 | Ghi demo chơi tay → BC/GAIL khởi động | Claude + Codex | bỏ — owner không chơi tay (D-023) | M2 | |
| T-010 | Chế độ "AI chơi": trình xem `Xem-AI.cmd` (scene `ArenaWatch`, tự nạp não mới) | Claude (PC) | done (`feature/watch-ai`) | M2 | |
| [T-011](T-011-m3-core-zombies-projectiles-classes.md) | Core M3: Runner/Brute/Spitter, đạn, skill Mage/Archer, luật v3 (quan sát 883) | Codex | done (review Claude + reviewer, D-024) | M3 | T-012 |
| [T-012](T-012-m3-trainer-classes-curriculum.md) | Trainer M3: luật v3, train theo class, curriculum trộn zombie | Codex | done (D-024) | M3 | T-011 |
| [T-013](T-013-m3-unity-integration.md) | Tích hợp Unity M3: ML (class, trộn zombie) + trình xem (look theo loại/class, đạn, phím H/M, icon skill) | Claude (PC) | done (D-024) | M3 | |
| [T-014](T-014-survivor-core-sim.md) | Core Survivor lát cắt dọc: bản đồ, lịch quái 3 loại, EXP/lên cấp, 2 vũ khí, 4 phụ kiện, skill chủ động, boss, quan sát v4 (2264, chuẩn hóa), thưởng thứ bậc, nguyên nhân chết, `SurvivorEvaluator` + tool `Tools/SurvivorEval` | Codex | done | M4A | T-015 |
| [T-015](T-015-survivor-trainer-brain-upgrade.md) | Trainer Survivor: schema version, config `warrior_survivor_ppo.yaml` + curriculum đo bằng reward, run `warrior-sNNN`, export 2264 | Codex | done | M4A | T-014 |
| T-016 | Tích hợp Unity Survivor: `HeroAgent` 3 nhánh, stat `Arena/…`, trình xem (camera theo, nghĩa địa, HUD, bảng lên cấp tô sáng lựa chọn AI, màn kết trận) | Claude (PC) | done | M4A | |
| [T-017](T-017-remove-round-arena.md) | Dọn code đấu trường tròn cũ (sàn, vực, config cũ) | Claude (PC) | done | M4A | |
| [T-018](T-018-champion-eval-behavior.md) | Champion/challenger (không ghi đè não tốt nhất), đánh giá tự động 100 seed trong dịch vụ train, telemetry hành vi (Core + SurvivorEval) | Codex | done | M4B | T-020 |
| [T-020](T-020-viewer-best-brain-profile.md) | Trình xem: mục "Não giỏi nhất" (M4A đạt/chưa, Behavior Profile so với champion trước), phím `B` đổi não mới nhất ↔ giỏi nhất | Claude (PC) | done | M4B | T-018 |
| [T-019](T-019-brain-upgrade-build-curriculum.md) | `brain_upgrade.py` + mô tả schema JSON (TRAIN tự nâng cấp não), não theo build ngẫu nhiên, curriculum ôn tập 70/20/10 | Codex | done | M4B | |
| [T-021](T-021-m4c-survivor-content.md) | Core M4C: 4 vũ khí mới (giáo, rìu xoay, hào quang, sóng chấn), 4 phụ kiện, Spitter + đạn địch (khiên chặn), nam châm, rương — giữ nguyên schema v4 | Codex + Claude | done (D-034) | M4C | |
| T-022 | Unity M4C: hình ảnh 4 vũ khí mới, Spitter + đạn, nam châm/rương, icon 8 món mới, đánh bóng HUD | Claude (PC) | done (D-034) | M4C | |
