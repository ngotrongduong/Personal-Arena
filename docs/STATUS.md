# Trạng thái dự án

> "Bộ nhớ" giữa các phiên. Đọc đầu tiên, cập nhật cuối cùng. **Giữ file dưới 200 dòng** (CI kiểm):
> nhật ký cũ chuyển sang `docs/archive/SESSIONS.md`, task xong sang `docs/archive/BOARD-DONE.md`.
> Cập nhật lần cuối: 2026-10-05 (phiên Claude trên PC: M16 — rừng quanh bản đồ, game tiếng Anh, HUD mới + tooltip; bản xem v0.8.189).

## Đang ở đâu (đọc phần này là đủ để bắt đầu)

- **Hướng đi:** chỉ làm Personal Arena (D-016), game giống **Vampire Survivors** nhất có thể; AI tự học chơi
  (owner chỉ xem, D-019). Repo public để CI miễn phí (D-015) → không bao giờ commit secret.
- **Milestone:** M0–M9 xong (lịch sử ở `docs/archive/SESSIONS.md`). M9 = schema **v5** (quan sát 2592, hành động
  9/7/5, danh mục 128, 6 + 6 ô, 6 skill mỗi class). M10–M14 xong ngày 2026-10-04/05, tất cả đã merge vào `develop`
  (PR #8–#17), **schema vẫn v5**; chi tiết từng milestone ở `docs/archive/SESSIONS.md`.
- **M10:** hiệu ứng kỹ năng/vũ khí bằng Kenney Particle Pack (CC0), `ArenaEffects.Vfx.cs`; cờ `-fxDemo`, `-perfLog`.
- **M11:** hiệu ứng diện rộng to và lâu hơn, đạn và vật phẩm đúng hình, hào quang buff; vật phẩm rơi mới (bình mana,
  bom, cuồng nộ, khiên, tốc độ — `SurvivorSim.Buffs.cs`); 3 quái mới (lao tới, phân thân, thầy cúng) và quái vàng
  hiếm (`SurvivorSim.Rares.cs`).
- **M12/M12b:** dùng 2 gói Asset Store của owner (Hovl *Magic Effects FREE*, GAPH *52 Special Effects Pack*) qua
  `ArenaEffects.Store.cs` / `.StoreMap.cs`. **Hai gói KHÔNG nằm trong repo** (D-046); không có gói thì về Kenney.
- **M13:** theo phản hồi owner (D-047): mỗi đòn một hiệu ứng rõ, đúng cỡ vùng sát thương, đúng hệ, không chồng hiệu
  ứng lên thân nhân vật. Đạn nảy trúng quái thì bật sang quái gần nhất (`RicochetOffEnemy`).
- **M14:** Kiếm quét (catalog 0) phóng **sóng kiếm bay** chém mọi quái trên đường bay, cấp 5 thêm sóng ra sau
  (`LaunchSwordWaves`, `ProjectileLook.SwordWave`); 4 golden tier-1 ghi lại.
- **M15:** mỗi vũ khí tiến hóa (34 cái) có **hệ và màu riêng** thay cho màu vàng chung
  (`SurvivorEvolutionStyle.cs`, `SurvivorRenderer.Evolutions.cs`): vệt bay, màu, dấu trên đất ở đúng vùng trúng;
  chỉ sửa trình xem, điều kiện tiến hóa và Core giữ nguyên. HUD có đồng hồ buff (T-047). Tắt màn logo Unity nên
  trình xem mở nhanh hơn ~3 s (T-048); mọi file View dưới 700 dòng (T-049).
- **M16 (T-050):** rừng 5 hàng cây quanh bản đồ + sương mù theo mức zoom (hết vùng tối ngoài hàng rào); **toàn bộ
  chữ trong game là tiếng Anh** (D-049); **HUD mới** gọn hơn, Tab ẩn cột phải, H mở bảng phím; **tooltip** khi rê
  chuột vào ô vũ khí/bị động/skill và thẻ lên cấp ghi số liệu thật, sinh từ catalog (`SurvivorItemDetails`, D-050).
- **Bản cài (2026-10-05):** `Build/WatchNext` v0.8.189, `Build/TrainingNext` v0.8.175 (luật chơi không đổi từ đó);
  smoke test 3 class đạt; 60 FPS, mở app ~0,9 s tới khung hình đầu.
- **Test:** CoreTests 645/645, pytest 204/204, EditMode 308/308 (PC, 2026-10-05).

## Việc tiếp theo (theo thứ tự)

1. **Owner:** mở `Xem-AI.cmd` (bản xem v0.8.189), xem HUD mới + tooltip + hiệu ứng M13–M15 trong trận thật và bấm
   TRAIN — não cần học tiếp để quen Kiếm quét mới, quái mới, vật phẩm mới (điểm số sẽ dao động một thời gian).
2. **Claude:** bảng "Codex" liệt kê mọi vũ khí/bị động/tiến hóa và công thức (tooltip hiện mới chỉ có món đang cầm);
   ngọc EXP rải kín đất — chưa thấy với não Chiến binh (nó nhặt hết), cần xem với não yếu rồi mới sửa.
3. Nợ nhỏ (nhật ký 2026-10-02 trong archive): rẽ nhánh từ champion cũ thiếu `training_status.json`,
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
| M9: nội dung kiểu Vampire Survivors (6 + 6 ô, vũ khí/phụ kiện/tiến hóa mới, 6 skill, schema v5) | xong (T-036..T-046); tách file View lớn chuyển sang T-049 |
| M10–M14: hiệu ứng, vật phẩm rơi, quái mới, gói Asset Store, sóng kiếm bay | xong (PR #8–#17) — chờ owner xem bản v0.8.171 và train tiếp |
| M15: hình ảnh riêng cho 34 vũ khí tiến hóa + đồng hồ buff HUD (T-047), mở app nhanh (T-048), tách file View (T-049) | xong |
| M16: rừng quanh bản đồ, game tiếng Anh, HUD mới + tooltip chi tiết (T-050) | xong — chờ owner xem bản v0.8.189 |


## Cách làm trên PC (Claude)

- `C:\PersonalArena` ở một nhánh feature fast-forward theo `develop` (`git merge --ff-only origin/develop`); `develop`
  checkout ở worktree `C:\PersonalArena-wt\develop` để merge. Worktree task của Codex tạo dưới
  `C:\PersonalArena-wt\` và xoá sau khi merge; `main` = `develop`. Venv ML ở `C:\PersonalArena\.venv-ml`
  (Python 3.10.12, mlagents 1.1.0, torch 2.2.2+cu121, có pytest).
- Unity headless: `Unity.exe -batchmode -nographics -quit -projectPath <Unity> -executeMethod X
  -logFile L`. **Luôn có `-quit`**, nếu không Unity treo mãi.
- Test EditMode: `-runTests -testPlatform EditMode -testResults <xml>` (không dùng `-quit`).
- Chụp màn hình bản build: chạy exe cửa sổ (`-screen-fullscreen 0`) rồi `PrintWindow(h, dc, 2)`;
  `CopyFromScreen` bị khóa foreground.
- Unity batch đôi khi sập lúc khởi động (exit -1073741819) hoặc script build trả exit ≠ 0 dù build xong: chạy lại
  hoặc chạy tiếp bước sau.
- Build xong thì `git checkout -- Unity/Assets/Scenes/ArenaWatch.unity Unity/ProjectSettings/ProjectSettings.asset`
  (build tự ghi lại scene và thêm define `SENTIS_ANALYTICS_ENABLED`). `-watchBuildOutput` tương đối tính từ `Unity/`
  → dùng đường dẫn tuyệt đối `C:\PersonalArena\Build\WatchNext`.
- Máy không có `gh`: không có GitHub MCP thì merge ở worktree `develop` (`git merge --no-ff`) rồi `git push`.
  **Sau mỗi lần push phải kiểm CI** qua API công khai `api.github.com/repos/ngotrongduong/Personal-Arena/actions/runs`
  (job hỏng: `check-runs/<job id>/annotations` có tên test và thông báo).
- Tránh hộp "Allow": gom lệnh nhiều bước vào file `.ps1` rồi chạy một lệnh đơn.
- `arena_trainer.py` tìm `.venv-ml` ở gốc repo → chạy từ `C:\PersonalArena`, không từ worktree.

## Vướng mắc / câu hỏi mở

- CI (`.github/workflows/ci.yml`) chạy miễn phí vì repo public. Golden bit-exact chỉ so trên job Windows
  (Linux bỏ qua vì `MathF.Sin/Cos/Atan2` của libm cho bit khác).
- Unity MCP: đề xuất CoplayDev `unity-mcp` (D-008), chưa cài.
- Core dùng 72 tia, video ~92. Giữ 72 cho tới khi training cho thấy cần hơn.
- Package Inference tự thêm define `SENTIS_ANALYTICS_ENABLED` (analytics phía Editor). Xem lại
  trước khi phát hành để đảm bảo game không gọi mạng.

## Nhật ký phiên (mới nhất trên cùng; giữ ~3 mục, cũ hơn → archive)

### 2026-10-05 (tối) — Claude (PC): M16 rừng quanh bản đồ, game tiếng Anh, HUD mới (T-050)
- Owner yêu cầu thêm giữa phiên: UI chuyên nghiệp và gọn hơn, toàn bộ chữ tiếng Anh, skill + vũ khí có thông tin
  chi tiết (D-049, D-050).
- Cảnh: thay 72 cây thưa bằng 5 hàng cây quanh bản đồ, nền đất kéo dài 60 m, sương mù đi theo mức zoom.
- Tiếng Anh: 756 chuỗi ở 57 file + thông báo của trainer, dịch bằng script trích/áp chuỗi; số kiểu `1,234`.
- HUD: thẻ nhân vật nhỏ, cấp cạnh đồng hồ, thanh skill mảnh có số ô, cột phải 3 thẻ (Tab ẩn), bảng phím (H).
  Tooltip + thẻ lên cấp sinh từ số catalog; trước đó một nửa số món không có mô tả.
- Cờ kiểm tra mới (đi kèm `-perfLog`): `-cameraAt x,z[,khoảng cách]` giữ camera ở một điểm; `-hudDemo` ghim lần
  lượt tooltip món, tooltip skill, bảng phím vào từng ảnh `-perfShots`.

### 2026-10-05 (sau) — Claude (PC): T-048 mở trình xem nhanh hơn, T-049 tách file View
- T-048: "khựng 3 s ở khung hình đầu" hóa ra là thời gian mở app (màn logo Unity 2,2 s), bị `-perfLog` tính nhầm
  thành một khung hình; hiệu ứng không phải thủ phạm (bộ gói nạp trong 22 ms). Tắt logo Unity → khung hình đầu
  hiện sau ~0,85 s thay vì ~3,9 s. `-perfLog` giờ ghi riêng các bước mở app và mọi khung > 50 ms ra
  `*_startup.csv` (`PerfTrace.cs`). Trong trận: 60 s ở x8 với 6 vũ khí, khung tệ nhất 50 ms.
- T-049: 13 file View > 700 dòng tách thành 36 file, file lớn nhất 647 dòng; chỉ dời code (partial class), không
  đổi logic. Kiểu dữ liệu lineage ra `LineageModels.cs`, enum của trình xem ra `SurvivorViewTypes.cs`.
- Máy không có `gh` và phiên không có GitHub MCP → gộp bằng git ở worktree `develop` rồi push (không qua PR).
- CI Linux đỏ từ M13/M14 mà không ai thấy: golden `Tier1_ScriptedRun` lệch bit trên Linux sau khi kiếm thành sóng
  bay (libm khác Windows) → golden này giờ chỉ chạy trên Windows như 2 golden dài. CI ghi tên test hỏng vào
  annotation, đọc được qua API công khai không cần đăng nhập.

### 2026-10-05 — Claude (PC): M15 vũ khí tiến hóa có hình ảnh riêng
- Owner: giữ điều kiện tiến hóa, mỗi bản tiến hóa phải khác hẳn bản thường, không chỉ đổi màu vàng; chỉ sửa trình xem.
- Bảng kiểu `SurvivorEvolutionStyles` (hệ + màu cho 34 bản): sét, lửa, băng, gió, đất, thánh, phép, bóng tối, độc,
  sao, hỗn loạn, thiên thạch. Đạn/vũ khí xoay mang màu hệ và rải vệt của hệ; đòn diện rộng thêm một dấu của hệ
  đúng cỡ vùng trúng (theo D-047, không vẽ lên thân nhân vật).
- T-047: 3 ô đồng hồ buff (cuồng nộ, khiên, tốc độ) trên HUD, có thanh cạn dần và số giây.
- Bản xem + bản train v0.8.175 đã cài; EditMode 302; Core và schema v5 không đổi.

