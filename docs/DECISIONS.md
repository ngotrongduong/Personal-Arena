# Quyết định đã chốt

Mỗi quyết định có mã `D-xxx`. Muốn đổi thì thêm quyết định mới ghi "thay D-xxx", không xoá cái cũ.

| Mã | Ngày | Quyết định | Lý do |
|---|---|---|---|
| D-001 | 2026-09 | Game 3D nhìn từ trên xuống, low-poly, Unity 6000.3 | Owner chọn |
| D-002 | 2026-09 | Repo GitHub **private** `ngotrongduong/Personal-Arena` | Owner chọn |
| D-003 | 2026-09 | AI = ML-Agents 4.x (PPO) + `mlagents==1.1.0`, Python 3.10.12, torch 2.2.2+cu121 | Có sẵn PPO, ray sensor, curriculum, BC/GAIL, inference trong game |
| D-004 | 2026-09 | Logic game ở `Core/` thuần C#, không `UnityEngine`, test bằng dotnet (`CoreTests`) | Test nhanh ngoài Unity, deterministic |
| D-005 | 2026-09 | Một `HeroAgent` chung, 3 nhánh hành động (di chuyển 9 hướng, xoay 3, skill 0–4); mỗi class là một behavior riêng | Mở rộng class chỉ bằng data |
| D-006 | 2026-09 | Mỗi nhân vật đã mua có "não" riêng, fine-tune từ model nền của class bằng `--initialize-from` | Ý "mua và tự huấn luyện riêng" |
| D-007 | 2026-09 | Chỉ vàng trong game. Không tiền thật, quảng cáo, analytics hay gọi mạng | Owner chọn |
| D-008 | 2026-09-29 | Đề xuất Unity MCP: **CoplayDev/unity-mcp** (MIT, miễn phí) để Claude/Codex điều khiển Unity Editor. *Chờ owner xác nhận.* | MCP chính thức của Unity cần gói AI trả phí |
| D-009 | 2026-09-29 | Git: nhánh `feature/…`, `fix/…`, `chore/…` merge `--no-ff` vào `main`; bỏ `develop` | Dự án 1 người, đỡ rối |
| D-010 | 2026-09-29 | Chia việc: Codex làm task nhỏ theo file trong `docs/tasks/`, không chạy git; Claude review, git, tích hợp | Hai AI làm song song, không đụng nhau |
| D-011 | 2026-09-29 | Asset chỉ dùng CC0/MIT: Quaternius, Kenney | Không lo bản quyền |
| D-012 | 2026-09-29 | Tài liệu cho owner viết tiếng Việt; code, commit, prompt agent viết tiếng Anh | Owner đọc dễ, agent làm việc chuẩn |
