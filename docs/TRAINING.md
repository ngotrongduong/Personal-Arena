# Huấn luyện AI

Nguồn chung cho skill `/mlagents-training` (Claude) và `train-and-report` (Codex).

Môi trường: mỗi process Unity chạy **16 arena độc lập** (headless, đổi bằng `--arena-agents`).
Mỗi agent chạy một trận Survivor (`SurvivorSim`, D-026) 60 Hz của riêng nó và ra quyết định mỗi
5 tick (12 Hz). Lúc đang có bảng lên cấp thì quyết định mỗi tick. Action 3 nhánh 9/5/5 (di chuyển,
skill chủ động, chọn nâng cấp); quan sát schema v4 = 2264 số, có chừa chỗ trống (D-027).
Hiện chỉ Warrior được train (behavior `Warrior`, config `Trainer/config/warrior_survivor_ppo.yaml`).
Mage/Archer có config Survivor riêng ở M7; service từ chối rõ ràng nếu được yêu cầu train chúng.

## 1. Build môi trường

Trong Editor: **Personal Arena > Build Training Scene**, rồi **Personal Arena > Build Training
Env (Windows)**. Hoặc headless (không mở Editor):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.2f1\Editor\Unity.exe" `
  -batchmode -nographics -quit -projectPath Unity `
  -executeMethod PersonalArena.ML.Editor.TrainingSceneBuilder.Build
& "C:\Program Files\Unity\Hub\Editor\6000.3.2f1\Editor\Unity.exe" `
  -batchmode -nographics -quit -projectPath Unity `
  -executeMethod PersonalArena.ML.Editor.TrainingBuild.BuildWindows
```

- Kết quả: `Build/Training/PersonalArenaTraining.exe` (không commit). `-buildPath <thư mục>`
  để đổi chỗ ra.
- Đang train thì exe bị khóa: build ra `-buildPath Build/TrainingNext`. Lần bấm TRAIN kế tiếp,
  `train_service` tự tráo `Build/TrainingNext` vào `Build/Training` trước khi chạy mlagents
  (lỗi thì giữ bản cũ và ghi vào log của run).
- Bản build chỉ chứa `Assets/Scenes/Training.unity`; tool không đổi `EditorBuildSettings`.
- Lúc chạy, `--arena-agents N` đổi số agent mỗi process (mặc định 16).
- **Luôn có `-quit`** với `-executeMethod` đồng bộ, nếu không Unity treo mãi sau khi xong.

## 2. Chạy

Từ gốc repo (`C:\PersonalArena`, nơi có `.venv-ml`):

> **Python gốc của `.venv-ml` phải nằm ở thư mục thật**, hiện là `C:\PersonalArena\.venv-python310`
> (gitignore bởi `.venv*/`), khai báo ở dòng `home` của `.venv-ml\pyvenv.cfg`.
> - Đừng cài Python bằng `uv` từ trong app Claude desktop. App này là gói MSIX, nên mọi thứ ghi vào
>   `AppData\Roaming` chỉ nằm trong thư mục ảo của app.
> - Khi đó, trình xem mở từ Desktop không thấy Python. Nút TRAIN THE AI báo "closed right away
>   (code 103)" (mã 103 của trình khởi chạy venv nghĩa là "No Python"), và bộ xuất não cũng chết theo.

```powershell
# Wrapper: 4 process, time-scale 20, no-graphics, config warrior_survivor_ppo.yaml, ra Trainer/runs/
& .venv-ml\Scripts\python.exe Trainer\arena_trainer.py --run-id warrior-s002
& .venv-ml\Scripts\python.exe Trainer\arena_trainer.py --run-id warrior-s002 --dry-run   # chỉ in lệnh
& .venv-ml\Scripts\python.exe Trainer\arena_trainer.py --run-id warrior-s002 --resume
& .venv-ml\Scripts\python.exe Trainer\arena_trainer.py --run-id warrior-ft --initialize-from warrior-s001
# Gọi thẳng mlagents-learn (vd. smoke test 1 process):
& .venv-ml\Scripts\mlagents-learn.exe Trainer/config/warrior_survivor_ppo.yaml --run-id smoke `
  --env Build/Training/PersonalArenaTraining.exe --num-envs 1 --time-scale 20 --no-graphics `
  --results-dir Trainer/runs
& .venv-ml\Scripts\tensorboard.exe --logdir Trainer/runs
```

- Kết quả ở `Trainer/runs/<run-id>/` (không commit). Mỗi run ghi `schema_version.txt`.
- Đặt tên run Survivor: `<class>-s<3 số>` và ghi 1 dòng vào bảng "Nhật ký run" bên dưới.
- Config `warrior_survivor_ppo.yaml`:
  - MLP 3 × 512, `normalize: false`, batch 4096, buffer 81920, lr 3e-4, beta 5e-3, gamma 0.995.
  - Curriculum `run_seconds` 180 → 360 → 600 → 900 (ngưỡng reward 5.0 / 6.5 / 8.5, tối thiểu 200
    episode mỗi bài). Ngưỡng là **ước lượng** từ bảng thưởng; chỉnh sau run thật.
  - Curriculum theo **tiến độ** (T-019, D-033), tính trên `max_steps` 1e8 gốc:
    - `build_level_max`: 0 tới 22%, 10 tới 30%, 25 tới 40%, sau đó 50;
    - `tier_max`: 1 tới 45%, 3 tới 60%, 6 tới 75%, sau đó 10.
- Environment parameters mà `HeroAgent` đọc:
  - `run_seconds`: độ dài trận;
  - `tier_min` / `tier_max`: bậc độ khó random trong khoảng;
  - `build_level_max`: điểm chỉ số rải ngẫu nhiên tối đa (0 = build trắng);
  - `review_share` (0,2) / `hard_share` (0 tới 30% tiến độ, sau đó 0,1): tỉ lệ trận **ôn tập**
    (bậc 1, build trắng) và trận
    **khó** (bậc +1, build ngẫu nhiên, mở đầu bằng vòng 16 quái quanh nhân vật); còn lại là trận
    mới. Thống kê `Arena/Episode/Survived<New|Review|Hard>`;
  - `own_build_share`: tỉ lệ trận dùng build thật của owner (M5).
- Thưởng (`SurvivorRewardConfig`): thắng +10, hết giờ +5, chết −5, sống +0.01/s, −1 cho mỗi lượng
  máu bằng MaxHp bị mất, +0.05 mỗi cấp, +0.001 mỗi vàng, +2 × tỉ lệ máu boss bị trừ.
- Nâng cấp não khi schema quan sát đổi: `Trainer/brain_upgrade.py` (D-027, D-033). Schema mô tả
  bằng `Trainer/schemas/survivor_v<N>.json`; bản mới chỉ được **thêm** (không xoá, không đổi chỗ).
  Khi bấm TRAIN mà run mới nhất có schema cũ hơn, service tự nâng cấp sang run mới: đầu vào mới
  trọng số 0, hành động mới khởi tạo nhỏ, bỏ trạng thái Adam, giữ `global_step`. Não sau nâng cấp
  cho đầu ra y hệt não cũ với quan sát cũ (test Python + C#). Schema giữ nguyên thì service học tiếp
  từ checkpoint.

## 3. Đọc kết quả (TensorBoard)

| Chỉ số | Mong đợi | Nếu sai |
|---|---|---|
| `Environment/Cumulative Reward` | tăng dần | phẳng: reward quá thưa hoặc sai dấu → chạy test dấu reward |
| `Policy/Entropy` | giảm chậm | sụp nhanh về 0: tăng `beta`; không giảm: reward nhiễu |
| `Environment/Episode Length` | tăng (sống lâu hơn) | |
| `Environment/Lesson` (curriculum) | lên dần 0→3 | kẹt: hạ `threshold` hoặc tăng `min_lesson_length` |
| `Losses/Value Loss` | giảm rồi ổn định | tăng mãi: `normalize: true`, giảm `learning_rate` |
| `Arena/SurvivedSeconds`, `Arena/Level`, `Arena/Kills`, `Arena/Gold` | tăng | ghi bởi `SurvivorEpisodeStats` |
| `Arena/Died`, `Arena/Catastrophic` (chết trước 180 s) | giảm | |

Nghiệm thu M4A (D-030) dùng `Tools/SurvivorEval` chạy não đã xuất trên 100 seed: trung vị sống
≥ 600 s, P10 ≥ 420 s, không trận nào chết trước 180 s.

## 4. Xem AI chơi trực tiếp (trong lúc train)

### Nút "TRAIN THE AI" (cách chính, D-020)

- Mở `Xem-AI.cmd`. Bảng **TRAINING** ở góc phải: bấm **TRAIN THE AI** → AI train ngầm liên tục
  (tiếp tục run mới nhất trong `Trainer/runs`, chưa có thì tạo `warrior-s001`). Bảng hiện bước,
  mean reward, bài curriculum đang tập, thời gian phiên và biểu đồ reward từ bước 0.
- **STOP TRAINING** → lưu checkpoint rồi dừng (có thể mất tới ~1 phút, hiện "SAVING..."). Đóng
  cửa sổ game cũng dừng và lưu y như vậy.
- Nhân vật trong trình xem tự đổi sang não mới mỗi lần training lưu (~500k bước).
- Nhật ký: `Trainer/runs/<run>.log` (dòng `[service]` là của service). Nếu train đang chạy từ
  terminal, nút bị khóa và ghi "TRAINING (OUTSIDE)".
- Chạy service không cần game: `.venv-ml\Scripts\python.exe Trainer\train_service.py`
  (dừng: tạo file `Trainer/runs/training_service.stop`).
- **Đừng kill cứng** mlagents: checkpoint `.pt` vẫn còn nhưng tiến độ curriculum
  (`run_logs/training_status.json`) chỉ được lưu khi dừng êm → lần sau quay lại bài đầu tiên.
- **Nút Power** (dưới nút train, D-021): bấm để đổi mức LIGHT / NORMAL / FAST / MAX = số game ẩn
  × số warrior mỗi game (xem bảng đo bên dưới). Đổi lúc đang train → service lưu, dừng êm rồi tự
  chạy lại với mức mới. Mức được nhớ giữa các lần mở. "View speed x1/x2/x4" (`Space`) chỉ là tốc
  độ **xem**, không ảnh hưởng tốc độ train.
- **Tự chạy lại khi crash** (D-021): nếu mlagents chết bất ngờ (ví dụ lỗi driver GPU
  `0xC0000409` trong `nvcuda64.dll`), service tự tiếp tục từ checkpoint cuối, tối đa 5 lần liên
  tiếp; crash nhanh 2 lần liền → chuyển sang train bằng CPU. Chạy êm ≥10 phút thì đếm lại từ 0.
- Service lỗi thì bảng hiện chữ đỏ và ghi chi tiết vào `Trainer/runs/training_service.err.log`.
- Tham số service: `--num-envs N --arena-agents M --time-scale T [--torch-device cpu]`.

Số đo trên PC của owner (RTX 4070 Ti, 2026-09-29, mỗi mức ~110 s, dao động ±30%):

| Game ẩn × arena mỗi game | time-scale | bước/giây | Mức |
|---|---|---|---|
| 2 × 16 = 32 | 20 | (chưa đo) | LIGHT — nhẹ máy |
| 4 × 16 = 64 | 20 | ~1 650 | NORMAL (mức cũ) |
| 4 × 32 = 128 | 20 | ~2 500 | FAST (mặc định) |
| 4 × 64 = 256 | 20 | ~3 400 (2 lần đo) | MAX |
| 8 × 16 / 12 × 16 | 20 | ~1 900 / ~2 100 | — thêm game kém hơn thêm arena |
| 8 × 32 | 20 | 3 470 rồi 1 860 | — không ổn định |
| 4 × 16 / 4 × 32 / 4 × 64 | 40 | ~2 400 / ~2 400 / ~2 900 | — x40 không lợi đều |

Kết luận: nhồi nhiều arena vào ít game lợi hơn mở thêm game (mỗi game là một process Unity, tốn
CPU); tăng time-scale không giúp chắc chắn. Mọi arena dùng chung một não, nên nhiều arena = nhiều
kinh nghiệm mỗi giây. Luật chơi chạy theo tick cố định nên time-scale không đổi kết quả trận.

### Chi tiết

- **Nhấp đúp `Xem-AI.cmd`** ở gốc repo. Nó chạy ngầm `export_brain.py --watch` (mỗi 5 s đổi
  checkpoint `.pt` mới nhất thành `Trainer/runs/<run>/<Behavior>/latest.brain`) và mở trình xem
  `Build/Watch/PersonalArenaWatch.exe`. Trình xem tìm `latest.brain` mới nhất, nạp lại mỗi khi
  training lưu não mới (~500k bước, ~8 phút). Đóng cửa sổ thì exporter cũng tắt.
- Trình xem Survivor (T-016): camera đi theo Warrior trên bản đồ nghĩa địa; HUD có thanh EXP,
  cấp, đồng hồ, vàng, icon món đồ; bảng lên cấp tô sáng món AI chọn; màn kết trận.
- Phím: `Space` tốc độ xem, `T` chọn hành động deterministic/sampled, `R` trận mới, `Esc` tạm dừng.
  Hết trận tự chơi trận mới.
- Nút **TRAINING DATA** (cuối bảng TRAINING) hoặc phím `G` mở màn hình 6 đồ thị: reward, zombie
  giết mỗi trận, số giây sống, tỉ lệ chết trước giờ, cấp đạt được, độ dài trận đang tập
  (curriculum). Dữ liệu đọc từ `Trainer/runs/<run>/training_history.json` (service ghi từ
  TensorBoard), tự cập nhật mỗi 5 s. `Esc`, `G` hoặc nút X để đóng.
- Não có `schema_version.txt` khác schema hiện tại (hoặc không có file này = schema 1) không được
  nạp; khi chỉ còn não cũ, bảng góc phải nhắc bấm TRAIN THE AI.
- Build trình xem: **Personal Arena > Build Watch AI Viewer (Windows)**, hoặc headless
  `-batchmode -quit -executeMethod PersonalArena.View.Editor.WatchBuild.BuildWindows`.
- Xuất tay: `python Trainer/export_brain.py --checkpoint <file.pt> --output <file.brain>`.
  Tham số exe: `-brain <file>` (một não cố định) hoặc `-runs <thư mục runs>`.
- Định dạng `.brain` và lý do không dùng ONNX: D-017. Chỉ hỗ trợ actor MLP không normalize,
  không LSTM (config hiện tại). Đổi config mạng thì phải sửa exporter.
- TensorBoard (biểu đồ): `tensorboard --logdir Trainer/runs` → http://localhost:6006.

## 5. Bài học từ video Pezzza

- Kiểm tra **dấu** mọi reward term (agent không bao giờ dùng giáo vì phạt nhầm).
- Curriculum tăng dần (trước đây số zombie 1→2→4→8→16, nay độ dài trận 3→6→10→15 phút); chỉ
  lên bài khi reward ổn định.
- Quyết định 12 Hz; video dùng ~92 tia 360°, mình dùng 72 (D-013).

## 6. Nhật ký run

| Run id | Ngày | Config | Bước | Kết quả | Ghi chú |
|---|---|---|---|---|---|
| smoke | 2026-09-29 | warrior_ppo.yaml, 1 env × 16 arena, time-scale 20, RTX 4070 Ti | 210k (5 phút) | Mean reward −0.01 → 15.4; tự lên bài 2 (TwoZombies) | Chỉ để kiểm tra pipeline; ~700 bước/s |
| warrior-001 | 2026-09-29 | warrior_ppo.yaml, 4 env × 16 arena, time-scale 20, RTX 4070 Ti | 9.87M (13:27–15:26), tiếp tục bằng nút | Curriculum lên 16 zombie ở ~480k (quá nhanh); mean reward 14.2 ở 720k; ở 1M bước, xem thử 4 zombie: 8 kill, sống 20 s | ~1000 bước/s. Ngưỡng 6 quá dễ → cần nâng |
| warrior-002 | 2026-09-29 | **luật v2 (D-022)**, warrior_ppo.yaml, 4 env × 64 arena (MAX), time-scale 20, RTX 4070 Ti | 48.9M (16:49–23:40), dừng êm khi lên luật v3 — không train tiếp | Lên 16 zombie ở ~3M; ở 24M: reward ~290, ~143 zombie giết/ván, sống ~98 s, rơi vực 4.8% (đầu run ~97%); gần như không hất zombie xuống vực | ~2 700 bước/s. Não luật cũ (`warrior-001`) không dùng được |
| mage-001 | 2026-09-30 | **luật v3 (D-024)**, mage_ppo.yaml, 4 env × 64 (MAX), time-scale 20 | 657k (smoke) | Reward ~0 → 10.9 ở 600k, còn 1 zombie | Chỉ kiểm tra class chạy được; train tiếp bằng nút |
| archer-001 | 2026-09-30 | luật v3, archer_ppo.yaml, 4 env × 64 (MAX), time-scale 20 | 1.14M (smoke) | Reward 32.9 ở 1.08M, lên 2 zombie | Như trên |
| warrior-003 | 2026-09-30 | luật v3, warrior_ppo.yaml, 4 env × 64 (MAX), time-scale 20 | đang chạy (1.41M lúc 00:40) | Reward 19.5 ở 1.4M, 2 zombie Walker | Run mới do đổi `rules_version.txt`; owner dừng bằng nút STOP |
| warrior-s001 | 2026-09-30 | **Survivor (D-026), schema v4**, warrior_survivor_ppo.yaml, 4 env × 16, time-scale 20 | 14.55M, dừng êm, học tiếp bằng nút | Ở 14.55M reward ~4.1. Ở 12.4M: sống ~156/180 s, chết 46% trận, reward ~1.1, vẫn bài 1 (180 s) | ~4M bước/giờ. Sửa luật giữa chừng (học tiếp cùng schema) |
