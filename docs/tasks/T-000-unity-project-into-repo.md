# T-000: Đưa Unity project lên repo, xác nhận venv + GPU

- **Owner:** Owner (trên PC Windows)
- **Status:** todo
- **Milestone:** M0
- **Parallel OK with:** tất cả
- **Depends on:** none

## Mục tiêu

Repo có Unity project mở được (không chỉ thư mục `Core`), cài sẵn ML-Agents; xác nhận venv
ML chạy được trên GPU.

## Các bước

1. `cd C:\PersonalArena` rồi `git pull` (lấy các thay đổi mới nhất trên `main`).
2. Mở **Unity Hub → Add → chọn thư mục `C:\PersonalArena\Unity`** (Unity 6000.3.2f1).
   - Nếu Unity báo không phải project: tạo project mới 3D (URP) tên tạm ở chỗ khác, rồi chép
     `Packages/` và `ProjectSettings/` của nó vào `C:\PersonalArena\Unity\`, mở lại.
3. Trong Editor: `Window → Package Manager → Unity Registry → ML Agents` → Install (bản 4.x).
4. `Edit → Project Settings → Editor`: Version Control = *Visible Meta Files*,
   Asset Serialization = *Force Text*.
5. Đóng Unity. Kiểm tra `git status`: phải thấy `Unity/Packages/`, `Unity/ProjectSettings/`,
   các file `.meta`; **không** thấy `Unity/Library/`.
6. Venv + GPU (xem `docs/SETUP.md`):
   `.venv-ml\Scripts\python -c "import torch, mlagents; print(torch.cuda.is_available())"` → `True`.
7. Nhờ Claude commit (hoặc tự `git add -A && git commit -m "chore(unity): add project settings and ML-Agents" && git push`).

## Xong khi

- [ ] `Unity/Packages/manifest.json` có `com.unity.ml-agents`
- [ ] `Unity/ProjectSettings/` và `.meta` đã lên GitHub
- [ ] Lệnh bước 6 in `True`

## Báo cáo

- Kết quả:
- Ghi chú:
