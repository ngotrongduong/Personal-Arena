# Huấn luyện AI

Nguồn chung cho skill `/mlagents-training` (Claude) và `train-and-report` (Codex).

Môi trường M2: mỗi process Unity chạy **16 arena độc lập** (headless). Mỗi agent chạy sim Core
60 Hz của riêng nó và ra quyết định mỗi 5 tick (12 Hz). Behavior `Warrior`, action 9/3/5.

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
- Bản build chỉ chứa `Assets/Scenes/Training.unity`; tool không đổi `EditorBuildSettings`.
- Lúc chạy, `--arena-agents N` đổi số agent mỗi process (mặc định 16).
- **Luôn có `-quit`** với `-executeMethod` đồng bộ, nếu không Unity treo mãi sau khi xong.

## 2. Chạy

Từ gốc repo (`C:\PersonalArena`, nơi có `.venv-ml`):

```powershell
# Wrapper: 4 process, time-scale 20, no-graphics, config warrior_ppo.yaml, ra Trainer/runs/
& .venv-ml\Scripts\python.exe Trainer\arena_trainer.py --run-id warrior-001
& .venv-ml\Scripts\python.exe Trainer\arena_trainer.py --run-id warrior-001 --dry-run   # chỉ in lệnh
& .venv-ml\Scripts\python.exe Trainer\arena_trainer.py --run-id warrior-001 --resume
& .venv-ml\Scripts\python.exe Trainer\arena_trainer.py --run-id warrior-ft --initialize-from warrior-001
# Gọi thẳng mlagents-learn (vd. smoke test 1 process):
& .venv-ml\Scripts\mlagents-learn.exe Trainer/config/warrior_ppo.yaml --run-id smoke `
  --env Build/Training/PersonalArenaTraining.exe --num-envs 1 --time-scale 20 --no-graphics `
  --results-dir Trainer/runs
& .venv-ml\Scripts\tensorboard.exe --logdir Trainer/runs
```

- Kết quả ở `Trainer/runs/<run-id>/` (không commit). Model: `Trainer/runs/<run-id>/Warrior.onnx`.
- Đặt tên run: `<class>-<3 số>` và ghi 1 dòng vào bảng "Nhật ký run" bên dưới.
- Config:
  - `warrior_ppo.yaml`: curriculum `zombie_count` 1→2→4→8→16, arena cố định 28 m (sàn tròn bán kính 14, D-022).
  - `warrior_ppo_randomized.yaml`: giữ 4 bài đầu, bài cuối random `arena_size` 14–32 và
    `hp_mult` / `damage_mult` / `speed_mult` 0.8–1.25.
- Ngưỡng curriculum (reward 6, tối thiểu 300 episode mỗi bài) là **ước lượng đầu**: chỉnh
  sau run thật.
- Environment parameters mà `HeroAgent` đọc: `zombie_count`, `arena_size`, `hp_mult`,
  `damage_mult`, `speed_mult`.

## 3. Đọc kết quả (TensorBoard)

| Chỉ số | Mong đợi | Nếu sai |
|---|---|---|
| `Environment/Cumulative Reward` | tăng dần | phẳng: reward quá thưa hoặc sai dấu → chạy test dấu reward |
| `Policy/Entropy` | giảm chậm | sụp nhanh về 0: tăng `beta`; không giảm: reward nhiễu |
| `Environment/Episode Length` | tăng (sống lâu hơn) | |
| `Environment/Lesson` (curriculum) | lên dần 0→4 | kẹt: hạ `threshold` hoặc tăng `min_lesson_length` |
| `Losses/Value Loss` | giảm rồi ổn định | tăng mãi: `normalize: true`, giảm `learning_rate` |
| `Arena/*` (kill, sống sót, backstab, parry…) | tăng | ghi bởi `EpisodeStats` trong `HeroAgent` |

## 4. Xem AI chơi trực tiếp (trong lúc train)

### Nút "TRAIN THE AI" (cách chính, D-020)

- Mở `Xem-AI.cmd`. Bảng **TRAINING** ở góc phải: bấm **TRAIN THE AI** → AI train ngầm liên tục
  (tiếp tục run mới nhất trong `Trainer/runs`, chưa có thì tạo `warrior-001`). Bảng hiện bước,
  mean reward, số zombie đang tập, thời gian phiên và biểu đồ reward từ bước 0.
- **STOP TRAINING** → lưu checkpoint rồi dừng (có thể mất tới ~1 phút, hiện "SAVING..."). Đóng
  cửa sổ game cũng dừng và lưu y như vậy.
- Nhân vật trong trình xem tự đổi sang não mới mỗi lần training lưu (~500k bước).
- Nhật ký: `Trainer/runs/<run>.log` (dòng `[service]` là của service). Nếu train đang chạy từ
  terminal, nút bị khóa và ghi "TRAINING (OUTSIDE)".
- Chạy service không cần game: `.venv-ml\Scripts\python.exe Trainer\train_service.py`
  (dừng: tạo file `Trainer/runs/training_service.stop`).
- **Đừng kill cứng** mlagents: checkpoint `.pt` vẫn còn nhưng tiến độ curriculum
  (`run_logs/training_status.json`) chỉ được lưu khi dừng êm → lần sau quay lại bài 1 zombie.
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
- Phím: `1`–`6` số zombie 1/2/4/8/16/32, `Space` tốc độ x1/x2/x4, `T` chọn hành động
  deterministic/sampled, `R` ván mới, `Esc` tạm dừng. Hết ván tự chơi lại sau 3 s.
- Camera (D-022): kéo chuột trái/phải = xoay, lăn chuột hoặc `+`/`-` = zoom, `Q`/`E` = xoay
  ngang, chuột giữa hoặc `Shift`+kéo = dời, `C` = đổi chế độ (toàn sân / theo warrior / tự do),
  `Home` = về góc mặc định.
- Nút **TRAINING DATA** (cuối bảng TRAINING) hoặc phím `G` mở màn hình 6 đồ thị: reward, zombie giết mỗi ván,
  số giây sống, tỉ lệ rơi vực, số zombie bị hất xuống vực, độ khó curriculum. Dữ liệu đọc từ
  `Trainer/runs/<run>/training_history.json` (service ghi từ TensorBoard), tự cập nhật mỗi 5 s.
  `Esc`, `G` hoặc nút X để đóng.
- Não luật cũ (`rules_version.txt` khác `ArenaSim.RulesVersion`) không được nạp; khi chỉ còn não
  cũ, bảng góc phải nhắc bấm TRAIN THE AI.
- Build trình xem: **Personal Arena > Build Watch AI Viewer (Windows)**, hoặc headless
  `-batchmode -quit -executeMethod PersonalArena.View.Editor.WatchBuild.BuildWindows`.
- Xuất tay: `python Trainer/export_brain.py --checkpoint <file.pt> --output <file.brain>`.
  Tham số exe: `-brain <file>` (một não cố định) hoặc `-runs <thư mục runs>`.
- Định dạng `.brain` và lý do không dùng ONNX: D-017. Chỉ hỗ trợ actor MLP không normalize,
  không LSTM (config hiện tại). Đổi config mạng thì phải sửa exporter.
- TensorBoard (biểu đồ): `tensorboard --logdir Trainer/runs` → http://localhost:6006.

## 5. Bài học từ video Pezzza

- Kiểm tra **dấu** mọi reward term (agent không bao giờ dùng giáo vì phạt nhầm).
- Curriculum số zombie 1→2→4→8→16; chỉ lên cấp khi reward ổn định.
- Quyết định 12 Hz; video dùng ~92 tia 360°, mình dùng 72 (D-013).

## 6. Nhật ký run

| Run id | Ngày | Config | Bước | Kết quả | Ghi chú |
|---|---|---|---|---|---|
| smoke | 2026-09-29 | warrior_ppo.yaml, 1 env × 16 arena, time-scale 20, RTX 4070 Ti | 210k (5 phút) | Mean reward −0.01 → 15.4; tự lên bài 2 (TwoZombies) | Chỉ để kiểm tra pipeline; ~700 bước/s |
| warrior-001 | 2026-09-29 | warrior_ppo.yaml, 4 env × 16 arena, time-scale 20, RTX 4070 Ti | 9.87M (13:27–15:26), tiếp tục bằng nút | Curriculum lên 16 zombie ở ~480k (quá nhanh); mean reward 14.2 ở 720k; ở 1M bước, xem thử 4 zombie: 8 kill, sống 20 s | ~1000 bước/s. Ngưỡng 6 quá dễ → cần nâng |
| warrior-002 | 2026-09-29 | **luật v2 (D-022)**, warrior_ppo.yaml, 4 env × 64 arena (MAX), time-scale 20, RTX 4070 Ti | 24M (16:49–19:16), vẫn train | Lên 16 zombie ở ~3M; ở 24M: reward ~290, ~143 zombie giết/ván, sống ~98 s, rơi vực 4.8% (đầu run ~97%); gần như không hất zombie xuống vực | ~2 700 bước/s. Não luật cũ (`warrior-001`) không dùng được |
