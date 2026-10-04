# Bảng việc

Cập nhật mỗi khi đổi trạng thái. Chi tiết ở từng file task. Task T-001..T-035 (M0–M8, đã xong) ở
`docs/archive/BOARD-DONE.md`.

| Task | Tiêu đề | Owner | Trạng thái | Milestone | Song song được với |
|---|---|---|---|---|---|
| [T-036](T-036-m9-warrior-content-wave1a.md) | M9 đợt 1a: ô mang theo 6 + 6, 5 vũ khí Warrior (Phun lửa, Kiếm liên hoàn, Búa nặng, Bom, Phản đòn), 4 phụ kiện (Hồi phục, May mắn, Tham lam, Vương miện), skill Tiếng thét; không đổi schema | Claude subagent (`core-sim-engineer`) | done (CoreTests 286; phần hiển thị Unity chờ PC) | M9 | |
| [T-037](T-037-m9-unity-visuals.md) | M9 Unity: icon, hiệu ứng, âm thanh cho vũ khí/phụ kiện/tiến hóa/skill mới (ô 26–37, 58–61, 64–72, 112–127, 7 skill) | Codex (PC) | doing | M9 | |
| [T-038](T-038-m9-warrior-content-wave1b.md) | M9 đợt 1b: Vòng bảo hộ, Boomerang, Bình độc (Warrior) + 4 phụ kiện chỉ số (Bùa thời gian, Bộ nhân đôi, Giáp phản, Hộp tổng hợp); không đổi schema | Claude subagent (`core-sim-engineer`) | done (CoreTests 342; hiển thị Unity chờ PC) | M9 | |
| [T-039](T-039-m9-schema-v5-core.md) | M9 đợt 2, Core: schema v5 (danh mục 128, 6 skill, khối self 72, quan sát ≈ 2592), cập nhật hằng số và test Unity EditMode theo hằng số | Claude subagent (`core-sim-engineer`) | done (CoreTests 360, SurvivorEval build 0 lỗi; test Unity chưa chạy) | M9 | |
| [T-040](T-040-m9-schema-v5-trainer.md) | M9 đợt 2, Trainer: `Trainer/schemas/survivor_v5.json`, `brain_upgrade` v4 → v5 (ánh xạ cột, nhánh hành động 5 → 7), `export_brain`, test pytest, fixture C# | Claude subagent (`rl-trainer`) | done (pytest 203, CoreTests 361; chưa thử với não thật) | M9 | |
| [T-041](T-041-m9-unity-schema-v5.md) | M9 Unity: schema v5 trong trình xem (thanh skill tới 6 ô, 6 + 6 món, phím 5–6), EditMode xanh, build `Build/TrainingNext` + `Build/WatchNext` | Codex (PC) | review | M9 | |
| [T-042](T-042-m9-weapons-group-c.md) | M9 nhóm vũ khí C trên schema v5: Đạn nảy, Bóng tốc, Đồng hồ băng, Thanh tẩy, Mưa bom vòng (ô 64–68), vào pool Warrior/Mage/Archer | Claude subagent (`core-sim-engineer`) | done (CoreTests 395; hiển thị Unity chờ PC) | M9 | |
| [T-043](T-043-m9-skills-5-6.md) | M9 skill chủ động thứ 5 và 6 cho cả 3 class (Warrior: Nhảy đập đất, Xoáy kiếm; Archer: Bẫy gai, Mưa tên; Mage: Tường lửa, Lôi liên hoàn); không đổi schema | Claude subagent (`core-sim-engineer`) | done (CoreTests 446; hiển thị Unity chờ PC) | M9 | |
| [T-044](T-044-m9-weapons-ab-mage-archer.md) | M9 vũ khí nhóm A/B cho Mage và Archer: thêm vào pool (Phun lửa, Vòng bảo hộ, Bình độc cho Mage; Bom, Boomerang, Bình độc cho Archer) và 4 vũ khí mới ô 69–72 (Hỏa cầu nổ, Vòng tay ba mũi, Bắn bốn hướng, Đá tụ lực) | Claude subagent (`core-sim-engineer`) | done (CoreTests 495) | M9 | |
| [T-045](T-045-m9-evolutions-new-weapons.md) | M9 tiến hóa cho 16 vũ khí mới (ô 112–127, ghép với phụ kiện cũ và mới; Thanh tẩy không có tiến hóa) | Claude subagent (`core-sim-engineer`) | done (CoreTests 614) | M9 | |
| [T-046](T-046-core-reorganize-by-mechanic.md) | Sắp xếp lại Core theo cơ chế (vũ khí cận chiến/quanh người/đạn/vùng/phòng thủ, danh mục tách file, dispatch thành `switch`); chỉ di chuyển code, hành vi giữ nguyên từng bit (so dấu vân tay) | Claude subagent (`core-sim-engineer`) | done (dấu vân tay 18/18 khớp, CoreTests 622) | M9 | |
