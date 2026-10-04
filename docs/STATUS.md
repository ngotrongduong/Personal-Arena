# Trạng thái dự án

> "Bộ nhớ" giữa các phiên. Đọc đầu tiên, cập nhật cuối cùng. **Giữ file dưới 200 dòng** (CI kiểm):
> nhật ký cũ chuyển sang `docs/archive/SESSIONS.md`, task xong sang `docs/archive/BOARD-DONE.md`.
> Cập nhật lần cuối: 2026-10-03 (phiên Claude cloud: rà soát vũ khí/skill, sửa Mưa tên và loạt chẵn — D-045).

## Đang ở đâu (đọc phần này là đủ để bắt đầu)

- **Hướng đi:** chỉ làm Personal Arena (D-016), game giống **Vampire Survivors** nhất có thể; AI tự học chơi
  (owner chỉ xem, D-019). Repo public để CI miễn phí (D-015) → không bao giờ commit secret.
- **Milestone:** M0–M8 xong (lịch sử ở `docs/archive/SESSIONS.md`). **M9 (D-041)** đang làm trên nhánh
  `claude/serene-sagan-3a70q5` (PR #7): Core + Trainer + Unity xong (T-041, T-037), chờ merge vào `develop`.
- **M9 đã có trong Core:** 6 + 6 ô mang theo; vũ khí mới 26–33, 64–72; phụ kiện 34–37, 58–61; tiến hóa 112–127;
  6 skill chủ động mỗi class; schema **v5** (quan sát 2592, hành động 9/7/5, danh mục 128);
  `brain_upgrade` v4 → v5. Đá không choáng trùm (D-042). Core chia file theo cơ chế (T-046).
- **Bản build (2026-10-04, Claude trên PC):** `Build/WatchNext` + `Build/TrainingNext` v0.8.147 theo schema v5,
  smoke test 3 class đạt; tự thay vào khi mở trình xem / bấm TRAIN lần tới. Codex hết token nên Claude làm nốt T-037.
- **Test:** CoreTests 629/629, pytest 204/204, EditMode 299/299 (PC, 2026-10-04).

## Việc tiếp theo (theo thứ tự)

1. Merge PR #7 vào `develop` khi CI xanh.
2. **Icon M9:** 32 icon mới đang là bản chép icon cũ (nhiều món trùng hình). Thay bằng icon riêng từ game-icons.net
   (CC BY 3.0; script sẵn ở scratchpad `m9_icons.py`, lần chạy đầu bị chặn quyền tải — cần owner cho phép).
3. **Mượt và đẹp (ưu tiên của owner 2026-10-04):** đo FPS trình xem lúc đông quái, xem tận mắt từng hiệu ứng M9;
   tách (chỉ dời code) các file View > 700 dòng còn lại của T-041.
4. Owner bấm TRAIN: não Warrior/Mage/Archer tự nâng v4 → v5 (`brain_upgrade`) và học tiếp. Theo dõi AI có dùng
   skill 5–6 và vũ khí mới không; cân bằng bằng `Tools/SurvivorEval --set`.
5. Nợ nhỏ (chưa sửa, ghi ở nhật ký 2026-10-02 trong archive): rẽ nhánh từ champion cũ thiếu `training_status.json`,
   khóa `brain_lineage` khi chạy song song, walker triệu hồi ngoài bản đồ 1 tick.

## Cách tiết kiệm token (áp dụng cho mọi agent)

- Hook `.claude/hooks/read_guard.py` tự chặn đọc thư mục sinh ra (`Library/`, `Temp/`, `obj/`, `bin/`, `Build/`,
  `results/`, `Trainer/runs/`) và đọc nguyên file lớn (> 600 dòng): dùng Grep tìm chỗ cần rồi Read có `offset/limit`.
- Unity trên PC: chạy `Tools/unity-run.ps1` (chỉ in tóm tắt), không đọc nguyên log Unity (AGENTS.md §8).
- STATUS < 200 dòng, BOARD chỉ giữ task chưa xong + milestone hiện tại (CI kiểm STATUS).

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
| M4A: train `warrior-s001` qua đánh giá 100 seed | đang làm — 23.1M bước, bài 360 s. Đánh giá 100 seed thử ở 17M: trung vị 10:32, P10 5:17 (cần 7:00), 1 lần chết sớm |
| M4B: champion/challenger + Hồ sơ AI (T-018, T-020) | xong |
| M4B: nâng cấp não + build ngẫu nhiên + ôn tập (T-019) | xong — có hiệu lực từ lần bấm TRAIN kế tiếp (tự tráo `Build/TrainingNext`) |
| M4C: đủ nội dung (6 vũ khí, 8 phụ kiện, đồ nhặt, rương, spitter, HUD) | code xong (T-021, T-022) — chờ train trên bản mới và qua đánh giá 100 seed |
| M5: kinh tế và build (T-023..T-026) + thưởng hạ quái (T-027, D-036) | code xong — chờ nghiệm thu: đổi build → TRAIN → AI đổi lối đánh |
| M6: Lịch sử não (T-028, T-029, D-037) | code xong — chờ nghiệm thu: mở bảng `L`, chọn não cũ, bấm "Xem ngay" |
| M7: class mua được, tiến hóa vũ khí, quái mới (T-030..T-032, D-038) | xong — owner đã mua Pháp sư + Cung thủ, chạy tốt (não nền Mage/Archer vẫn đang học) |
| M8: đánh bóng — âm thanh, cài đặt + phiên bản + smoke test, cân bằng (T-033..T-035, D-039) | code xong — chờ owner nghiệm thu bản build mới |
| M9: nội dung kiểu Vampire Survivors (6 + 6 ô, vũ khí/phụ kiện/tiến hóa mới, 6 skill, schema v5) | Core + Trainer + Unity xong (T-036..T-046); còn icon riêng và tách file View lớn |


## Cách làm trên PC (Claude)

- `C:\PersonalArena` đang ở nhánh `feature/survivor-core` (fast-forward theo `develop`); `develop`
  checkout ở worktree `C:\PersonalArena-wt\develop` để merge. Worktree task của Codex tạo dưới
  `C:\PersonalArena-wt\` và xoá sau khi merge; `main` = `develop`. Venv ML ở `C:\PersonalArena\.venv-ml`
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

## Nhật ký phiên (mới nhất trên cùng; giữ ~3 mục, cũ hơn → archive)

### 2026-10-03 (sau) — Claude (cloud): rà soát vũ khí và skill (D-045)
- Owner báo Mưa tên không giống AOE. Đo mọi vũ khí (cấp 1 và 5), mọi tiến hóa, mọi skill trên bãi quái đứng yên (dày và thưa).
- Sửa Mưa tên: rơi vào chỗ quái đông nhất, vùng 2,6 m, 1–3 vùng theo cấp; hiệu ứng trình xem rải tên khắp vùng.
- Sửa loạt chẵn (Nỏ, Giáo, Tia ma thuật, Đạn nảy, Boomerang 2 mũi): 1 mũi luôn thẳng vào mục tiêu. Nỏ cấp 5 từ 68 → 234 sát thương/giây khi quái đứng thưa.
- Skill `arrow-barrage` đổi tên tiếng Việt thành "Loạt tên". CoreTests 627/627. Hiệu ứng Mưa tên mới cần build lại trên PC (T-041).

### 2026-10-03 — Claude (cloud): cơ chế tiết kiệm token
- STATUS từ 498 → dưới 200 dòng; lịch sử chuyển `docs/archive/SESSIONS.md`, task xong `docs/archive/BOARD-DONE.md`.
- Hook chặn đọc file sinh ra / file lớn, CI kiểm STATUS ≤ 200 dòng, `Tools/unity-run.ps1` cho Codex,
  T-041 thêm yêu cầu tách các file View > 700 dòng.
- Tắt 23 skill claude.ai không dùng cho dự án này (`skillOverrides` trong `.claude/settings.json`), bớt ~4.500 token
  mỗi tin nhắn. Phiên sau chạy `/skill-doctor` để kiểm tra chúng đã ẩn chưa. CI xanh trên `9ebf35c`.

### 2026-10-02 (sau) — Claude (cloud): M9 đợt 1a, nội dung Warrior (T-036)
- Owner chốt: chia 2 đợt, ô mang theo 6 + 6 như Vampire Survivors, bắt đầu với Warrior, không ngại AI học lại.
  Đề xuất đầy đủ ở `docs/CONTENT-PROPOSAL.md`.
- T-036 (subagent `core-sim-engineer`, Claude review): ô 6 + 6 (`SurvivorTuning.MaxWeaponSlots/MaxPassiveSlots`),
  5 vũ khí Warrior (Phun lửa, Kiếm liên hoàn, Búa nặng, Bom, Phản đòn: ô 26–30), 4 phụ kiện cho cả 3 class
  (Hồi phục, May mắn, Tham lam, Vương miện: 58–61), skill Tiếng thét ở ô skill thứ 4 của Warrior. Schema v4 (2264)
  và hành động 9/5/5 không đổi. Golden cũ chạy bằng `OldRules` (4 + 4, pool cũ), không ghi lại giá trị.
- CoreTests 286/286 (Linux). Hai golden chỉ-Windows do job Windows của CI kiểm.
- **Chưa có hình ảnh/HUD trong trình xem** (T-037, cần PC có Unity): HUD 6 + 6 ô, icon, hiệu ứng, âm thanh.
  Não Warrior cũ chỉ dùng skill thứ 4 sau khi train lại. Số cân bằng là số khởi điểm, chưa chỉnh.
- **Đợt 1b (T-038)**: Vòng bảo hộ (31), Boomerang (32), Bình độc (33) cho Warrior; phụ kiện Bùa thời gian (34),
  Bộ nhân đôi (35, tối đa cấp 2), Giáp phản (36), Hộp tổng hợp (37) cho cả 3 class. Hai chỉ số mới `Duration`,
  `Amount` dùng chỗ `Reserved13/14`. CoreTests 342/342. Danh mục còn trống: 38–39. Chưa có hình ảnh/HUD (T-037).
- **Đợt 2, T-039 (Core) xong**: schema **v5** = 2592 giá trị (self 0..71, túi đồ 72..199 gồm 128 ô, đề nghị 200..719
  = 4 × 130, tia 720..2519, mật độ 2520..2591), hành động 9/7/5 (6 skill chủ động, 2 ô mới tạm là `none`),
  danh mục 128 (chỉ số cũ không dời). CoreTests 360/360. **T-040 (Trainer) xong**: `survivor_v5.json`, `SCHEMA_VERSION = 5`,
  `brain_upgrade` v4 → v5 giữ nguyên đầu ra cho input cũ (cột mới = 0; hai lựa chọn skill mới có bias thấp hơn
  bias nhỏ nhất cũ 5,0, chưa thử trong train thật), fixture nhị phân sinh bằng numpy nên giống nhau mọi nền tảng;
  pytest 203/203, CoreTests 361/361. **Chưa xong**: T-041 (Unity, cần PC: thanh
  skill 6 ô ở `SurvivorHud`, build lại, chạy EditMode). Hiện **bản xem/bản train cũ không chạy được với Core mới**
  (khác schema): đừng build hay train từ nhánh này trước khi T-040 và T-041 xong.
- **T-042 (nhóm vũ khí C) xong** trên schema v5: Đạn nảy (64; cả 3 class), Bóng tốc (65; Archer), Đồng hồ băng (66),
  Thanh tẩy (67), Mưa bom vòng (68) (66–68 cho Mage). CoreTests 395/395. Danh mục còn trống từ 69.
- **T-043 (skill thứ 5 và 6) xong**: Warrior Nhảy đập đất + Xoáy kiếm, Archer Bẫy gai + Mưa tên, Mage Tường lửa +
  Lôi liên hoàn (6 `SkillKind` mới thêm cuối enum; quái có thể bị làm chậm, trung tính khi không có bẫy).
  CoreTests 446/446. Cần Unity: thanh skill 6 ô, phím cho skill 5–6, icon, hiệu ứng.
- **T-044 xong**: Mage thêm Phun lửa, Vòng bảo hộ, Bình độc + 4 vũ khí mới; Archer thêm Bom, Boomerang, Bình độc;
  vũ khí mới 69 Hỏa cầu nổ (Mage), 70 Vòng tay ba mũi (Mage, Archer), 71 Bắn bốn hướng (Archer), 72 Đá tụ lực
  (Mage). CoreTests 495/495. Danh mục còn trống từ 73.
- **T-045 xong**: 16 tiến hóa cho vũ khí mới (ô 112–127; Thanh tẩy không có), cơ chế rương/ghép phụ kiện dùng chung với
  18 tiến hóa cũ (40–57). CoreTests 614/614. Lưu ý: tiến hóa Bóng ma tốc độ có hệ số sàn 0,4 nên sát thương lúc đứng
  yên là ×0,88 (task ghi 0,8; đổi `MomentumFloor` ≈ 0,333 nếu muốn 0,8). Đạn xuyên giờ nhớ tối đa 8 quái.
- **Đo hiệu năng `Step` với nội dung M9** (máy cloud, Release, 250 quái + 400 ngọc, test mới
  `SurvivorM9PerformanceTests`): bộ đồ M9 nặng nhất Warrior ≈ 0,058–0,061 ms, Archer ≈ 0,057–0,082 ms (dao động giữa
  các lần chạy), Mage ≈ 0,024–0,033 ms; bộ 6 vũ khí cũ cấp 5 trên cùng máy ≈ 0,046–0,049 ms. Đo từng vũ khí riêng: chỉ
  đám quái đã tốn ≈ 0,050 ms, mỗi vũ khí M9 chỉ cộng 0–0,015 ms (Đồng hồ băng còn làm nhanh hơn vì quái đứng yên) →
  chi phí chính là mô phỏng quái, không phải vũ khí mới. Mục tiêu 0,05 ms đặt cho PC của owner; CI giữ ngưỡng 0,2 ms.
  Nếu cần nhanh hơn: tối ưu cập nhật/tách quái, không phải vũ khí.
- **T-046 xong (chỉ di chuyển code)**: vũ khí chia theo cơ chế (`SurvivorSim.Weapons{,.Melee,.AroundHero,.Projectiles,
  .Area,.Defense}.cs`, skill gom vào `.Skills.cs`), danh mục tách thành `SurvivorDefs.cs` (kiểu), `SurvivorCatalog.cs`
  (logic), `SurvivorCatalog.Items.cs` (mọi dòng dữ liệu + 2 bảng tiến hóa, **phải ở cùng file theo thứ tự phụ thuộc**),
  `SurvivorClassKits.cs`, `SurvivorEnemyDefs.cs`; dispatch thành `switch`. Bỏ `Content/Wave1b/GroupC`. Hash 18 trận dài
  trước/sau khớp từng bit; CoreTests 622/622. Vũ khí mới đặt vào file theo cơ chế của nó.
  - Dọn tiếp (sau T-046): Đá không choáng trùm nữa (D-042); Quạt tên (`FireFan`) không tốn hồi chiêu khi pool đạn đầy;
    `ThrustSpears` dùng `NearestEnemy`; mảng đệm dùng `MaxVolleyCount`; `hammerTargetIds` đổi tên `volleyTargetIds`.
    Dấu vân tay 18 trận vẫn khớp từng bit; CoreTests 625/625.
- Chưa làm:
  toàn bộ phần hiển thị Unity (T-041 + icon/hiệu ứng/âm thanh), cân bằng bằng `SurvivorEval`.
