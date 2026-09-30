# Tài nguyên bên ngoài

Đã tìm và chọn lọc ngày 2026-09-29. Cột "Dùng thế nào" cho biết phần nào đã đưa vào repo.

## Công cụ cho AI agent

| Tài nguyên | Là gì | Dùng thế nào |
|---|---|---|
| [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) | MCP server cho Unity Editor (MIT, ~47 tool: scene, GameObject, script, test, build) | Đề xuất cài (D-008, `docs/SETUP.md`) |
| [Unity AI MCP chính thức](https://unity.com/blog/unity-ai-mcp-how-to-get-started) | MCP trong gói AI Assistant ≥ 2.11 | Cần đăng ký AI trả phí → không chọn |
| Plugin `unity` trong tài khoản Claude | Skill `unity-cli`, `unity-package-management`, `ui`, `physics-3d-collision`… | Đã có sẵn, liệt kê trong `CLAUDE.md` |
| [Donchitos/Claude-Code-Game-Studios](https://github.com/Donchitos/Claude-Code-Game-Studios) | 49 agent + 74 skill kiểu studio (MIT) | Quá lớn cho dự án 1 người; lấy ý tưởng reviewer và quy trình design→story |
| [VoltAgent/awesome-claude-code-subagents](https://github.com/VoltAgent/awesome-claude-code-subagents) | Bộ sưu tập subagent (có `game-developer`) | Tham khảo; agent của dự án được viết riêng cho sát kiến trúc |
| [davila7/claude-code-templates](https://github.com/davila7/claude-code-templates) | Template agent, có `unity-game-developer` | Tham khảo |
| [Codex customization](https://learn.chatgpt.com/docs/customization/overview) | Codex đọc `AGENTS.md` và `.agents/skills/*/SKILL.md` | Đã dùng cho skill Codex |

## Mẫu AI / ML-Agents có sẵn

| Tài nguyên | Dùng thế nào |
|---|---|
| [ml-agents `config/ppo/*.yaml`](https://github.com/Unity-Technologies/ml-agents/tree/develop/config/ppo) | `Trainer/config/warrior_survivor_ppo.yaml` dựa trên cấu trúc `WallJump_curriculum.yaml` |
| ml-agents example envs: *DungeonEscape*, *PushBlock*, *Crawler*, *SoccerTwos* | Tham khảo cách dựng nhiều arena song song trong một scene (M2) |
| [ML-Agents 4.0 docs](https://docs.unity3d.com/Packages/com.unity.ml-agents@4.0/manual/Inference-Engine.html) | Inference Engine, nạp model; dùng cho spike T-006 |

## Asset 3D low-poly (CC0)

| Pack | Dùng cho |
|---|---|
| **Đang dùng:** [KayKit](https://kaylousberg.itch.io/) — Adventurers, Skeletons, Character Animations, Dungeon Pack (D-018) | Hiệp sĩ Warrior, bộ xương làm zombie, hoạt ảnh, đấu trường hầm ngục |
| [Quaternius — Animated Zombie Pack](https://quaternius.com/packs/animatedzombie.html) | Walker, Runner, Brute, Spitter |
| [Quaternius — tất cả pack](https://quaternius.com/) (Ultimate Animated Character, Medieval Weapons…) | Warrior/Mage/Archer, vũ khí |
| [Kenney](https://kenney.nl/assets) (Prototype Kit, Particle Pack, UI Pack) | Sàn/tường arena, hiệu ứng, UI |

Khi nhập asset: đặt vào `Unity/Assets/ThirdParty/<Pack>/` và ghi nguồn + license vào
`Unity/Assets/ThirdParty/CREDITS.md`.
