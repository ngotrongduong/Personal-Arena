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
| [T-023](T-023-m5-core-economy.md) | Core M5: Training Focus, modifier bậc 2–10, nguồn vàng, nhãn khán giả, dữ liệu "câu chuyện" trận, profile + luật kinh tế (mua cấp, điểm, 5 loadout, mở bậc), Farm tự động, log kinh tế | Claude subagent (Codex hết lượt) | done (review + golden bậc 1 khớp code cũ) | M5 | T-024 |
| [T-024](T-024-m5-trainer-owner-build.md) | Trainer M5: `train_service` nhận build/bậc/trọng tâm của owner, bật `own_build_share` 0,7, truyền xuống Unity qua `--env-args` | Claude (PC) | done (D-035) | M5 | T-023 |
| [T-025](T-025-m5-training-owner-glue.md) | Unity ML M5: đọc `--owner-build/--owner-tier/--training-focus`, build owner dao động nhẹ (≤ 2 điểm), trọng tâm vào `HeroAgent`, `OwnerTraining` cho nút TRAIN | Claude (PC) | done (build owner ~70% mọi trận) | M5 | T-023 |
| [T-026](T-026-m5-viewer-economy.md) | Trình xem M5: `profile.json`, ví vàng, menu Nhân vật (C) / loadout / bậc / trọng tâm, Farm tự động (F), so sánh loadout (V), nhãn khán giả, câu chuyện sau trận | Claude subagent + Claude (PC) | done (EditMode 147, ảnh chụp) | M5 | T-023, T-025 |
| T-027 | Thưởng hạ quái: `PerKill` 0,004 / `PerEliteKill` 0,1 trong `SurvivorRewardConfig` (+ theo trọng tâm), test dấu và thứ bậc | Claude (PC) | done (D-036) | M5 | |
| [T-028](T-028-m6-lineage-trainer.md) | Trainer M6: kho phiên bản não (`brain_lineage.py`: sync, lưu mỗi lần chấm, snapshot, rẽ nhánh/nhân bản, dọn), nối vào `champion` và `train_service` | Claude (PC) | done (pytest 145, review) | M6 | |
| [T-029](T-029-m6-lineage-viewer.md) | Trình xem M6: bảng **Lịch sử não** (L): nhánh, phiên bản, xem ngay, so sánh, ghim, đổi tên, rẽ nhánh; TRAIN học tiếp nhánh đang dùng | Claude subagent + Claude (PC) | done (EditMode 180, ảnh chụp, review) | M6 | T-028 |
| [T-030](T-030-m7-core.md) | Core M7: bộ đồ Mage/Archer (6 vũ khí + skill mỗi class), tiến hóa vũ khí qua rương (40–57), quái Exploder/Ghost/Necromancer, mua class, `SurvivorEval --class` | Claude subagent + Claude (PC) | done (CoreTests 235, review) | M7 | |
| [T-031](T-031-m7-trainer.md) | Trainer M7: config `mage/archer_survivor_ppo.yaml`, service train theo `--behavior`, run `<class>-sNNN`, không ghi đè run cũ | Claude subagent + Claude (PC) | done (pytest 185, review) | M7 | T-030 |
| [T-032](T-032-m7-unity.md) | Unity M7: cửa hàng class, nhân vật chọn điều khiển trình xem và TRAIN, model Mage/Rogue, hình ảnh vũ khí/skill/quái mới, thông báo TIẾN HÓA | Claude subagent + Claude (PC) | done (EditMode 238, CoreTests 236, review, ảnh chụp) | M7 | T-030, T-031 |
| [T-033](T-033-m8-audio.md) | Âm thanh M8: hiệu ứng Kenney CC0 + nhạc nền/trùm, bộ giới hạn tiếng cho đám quái, phím `M`, im khi cửa sổ ở nền và khi chạy tự động | Claude subagent + Claude (PC) | done | M8 | |
| T-034 | Bản build hoàn chỉnh: bảng Cài đặt (âm lượng, cửa sổ, chất lượng, Thoát), tên/icon/số phiên bản, `-smokeTest` chạy trọn vòng 3 class | Claude (PC) | todo | M8 | sau T-033 |
| [T-035](T-035-m8-balance.md) | Cân bằng theo dữ liệu `SurvivorEval --set`: ngọc EXP × 1,5, máu trùm × 0,4, vàng rơi 4,5% | Claude (PC) | done (CoreTests 239) | M8 | |
