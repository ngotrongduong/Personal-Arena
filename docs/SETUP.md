# Cài đặt môi trường

## Máy của owner (Windows)

- Unity **6000.3.2f1**; GPU RTX 4070 Ti 12 GB.
- Repo ở `C:\PersonalArena` (venv ML nằm cùng chỗ: `C:\PersonalArena\.venv-ml`).
- Máy chỉ có Python 3.14 hệ thống, nên venv ML dùng Python 3.10.12 riêng qua `uv`.

## Python / ML-Agents

```powershell
cd C:\PersonalArena
uv python install 3.10.12
uv venv .venv-ml --python 3.10.12
uv pip install --python .venv-ml -r Trainer/requirements-ml.lock.txt `
  --extra-index-url https://download.pytorch.org/whl/cu121 --index-strategy unsafe-best-match
.venv-ml\Scripts\mlagents-learn --help
.venv-ml\Scripts\python -c "import torch; print(torch.cuda.is_available())"   # phải in True
```

- `setuptools<70` là bắt buộc (mlagents còn import `pkg_resources`); lock đã ghim 69.5.1.
- Lock có `pywin32`/`pypiwin32` nên chỉ cài được trên Windows.

## Unity project

- Thư mục project là `Unity/`. Phải có trong git: `Assets/`, `Packages/manifest.json`,
  `Packages/packages-lock.json`, `ProjectSettings/`, và mọi file `.meta`.
- Package đã cài (PR #3): `com.unity.ml-agents` 4.1.0, `com.unity.ai.inference` 2.6.1,
  `com.unity.inputsystem`, `com.unity.ugui`, `com.unity.test-framework`. Active Input Handling = Both.
- Mở project: Unity Hub → Add → `C:\PersonalArena\Unity`.
- Editor settings: Version Control = *Visible Meta Files*, Asset Serialization = *Force Text*.
- Fixed Timestep hiện là 0.02 s (mặc định); sim Core tự chạy 1/60 s (xem AGENTS.md §6).

## .NET (test Core)

- .NET SDK 10: `dotnet test CoreTests`.
- CI (GitHub Actions) chạy lệnh này mỗi lần push, nên phiên cloud không cần cài .NET.

## Unity MCP (cho Claude/Codex điều khiển Unity Editor) — đề xuất D-008

[CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) (MIT, miễn phí, hỗ trợ Unity 6):

1. Package Manager → *Add package from git URL* →
   `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main`
2. `Window → MCP for Unity → Configure All Detected Clients` (tự cấu hình Claude Code/Codex).
3. Làm theo README của repo nếu cần `uv`.

Có MCP thì Claude/Codex tạo scene, prefab, chạy test, đọc Console trực tiếp thay vì owner
phải bấm tay. Chỉ dùng được khi phiên chạy trên PC (hoặc cloud được liên kết với PC).

## Codex

- Codex đọc `AGENTS.md` ở gốc repo và skill ở `.agents/skills/`.
- Cách giao việc: xem `docs/tasks/README.md`.
