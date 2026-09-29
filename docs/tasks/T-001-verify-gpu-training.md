# T-001: Xác nhận venv ML chạy trên GPU (smoke test)

- **Owner:** Owner (trên PC) — hoặc Claude nếu PC đang liên kết
- **Status:** todo
- **Milestone:** M0
- **Parallel OK with:** tất cả
- **Depends on:** none

## Mục tiêu

Chắc chắn `mlagents-learn` + torch cu121 chạy được trên RTX 4070 Ti trước khi làm M2.

## Các bước

```powershell
cd C:\PersonalArena
git pull
.venv-ml\Scripts\python -c "import torch, mlagents_envs; print(torch.__version__, torch.cuda.is_available())"
# Mong đợi: 2.2.2+cu121 True
.venv-ml\Scripts\mlagents-learn --help
```

Nếu venv chưa có: làm theo `docs/SETUP.md` mục "Python / ML-Agents".

Tuỳ chọn (chắc chắn hơn): mở scene mẫu 3DBall từ ML-Agents samples trong Package Manager,
chạy `.venv-ml\Scripts\mlagents-learn --run-id=smoke-3dball` rồi bấm Play ~2 phút; thấy
`Mean Reward` tăng là đạt.

## Xong khi

- [ ] In ra `True` cho CUDA
- [ ] `mlagents-learn --help` chạy không lỗi

## Báo cáo

- Kết quả:
- Ghi chú:
