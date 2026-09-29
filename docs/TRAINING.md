# Huấn luyện AI

Nguồn chung cho skill `/mlagents-training` (Claude) và `train-and-report` (Codex).

## Chạy

```powershell
cd C:\PersonalArena
# Trong Editor (bấm Play khi thấy "Listening on port 5004"):
.venv-ml\Scripts\mlagents-learn Trainer/config/warrior_ppo.yaml --run-id=warrior-001
# Với bản build headless (nhanh hơn nhiều):
.venv-ml\Scripts\mlagents-learn Trainer/config/warrior_ppo.yaml --run-id=warrior-002 `
  --env=Builds/Headless/PersonalArena.exe --no-graphics --num-envs=4 --time-scale=20
# Tiếp tục run bị dừng:           --resume
# Fine-tune từ model nền class:    --initialize-from=warrior-base
.venv-ml\Scripts\tensorboard --logdir results
```

- Kết quả ở `results/<run-id>/` (không commit). Model: `results/<run-id>/Warrior.onnx`.
- Đặt tên run: `<class>-<3 số>` và ghi 1 dòng vào bảng "Nhật ký run" bên dưới.

## Đọc kết quả (TensorBoard)

| Chỉ số | Mong đợi | Nếu sai |
|---|---|---|
| `Environment/Cumulative Reward` | tăng dần | phẳng: reward quá thưa hoặc sai dấu → chạy test dấu reward |
| `Policy/Entropy` | giảm chậm | sụp nhanh về 0: tăng `beta`; không giảm: reward nhiễu |
| `Environment/Episode Length` | tăng (sống lâu hơn) | |
| `Environment/Lesson` (curriculum) | lên dần 0→4 | kẹt: hạ `threshold` hoặc tăng `min_lesson_length` |
| `Losses/Value Loss` | giảm rồi ổn định | tăng mãi: `normalize: true`, giảm `learning_rate` |

## Bài học từ video Pezzza

- Kiểm tra **dấu** mọi reward term (agent không bao giờ dùng giáo vì phạt nhầm).
- Curriculum số zombie 1→2→4→8→16; chỉ lên cấp khi reward ổn định.
- Quyết định 12 Hz; ~90 tia 360°.

## Nhật ký run

| Run id | Ngày | Config | Bước | Kết quả | Ghi chú |
|---|---|---|---|---|---|
| — | | | | | |
