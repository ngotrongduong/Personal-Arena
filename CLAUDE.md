# Personal Arena — Claude project memory

@AGENTS.md

## Claude-specific

- Talk to the owner in **Vietnamese**; keep code, commits and agent prompts in English.
- The owner granted full edit rights (AGENTS.md §2). Do not ask before editing, creating or
  deleting files; this also applies to every subagent you spawn.
- The owner is often **not linked** to this session. Cloud sessions work from the GitHub
  clone; anything needing the Windows PC (Unity Editor, GPU training) goes to the board with
  `Owner: Owner` and exact steps, unless the computer is linked.
- A `SessionStart` hook prints the top of `docs/STATUS.md`; still run `/session-start` for
  a full picture, and always `/session-end` before stopping.

## Skills (`.claude/skills/`)

| Skill | Use when |
|---|---|
| `/session-start` | First thing in a session: rebuild context, pick the next task |
| `/session-end` | Before stopping: update STATUS, BOARD, DECISIONS; commit and push |
| `/codex-task` | Splitting work for Codex: write a task file from the template, review Codex output |
| `/mlagents-training` | Anything about ML-Agents config, running or diagnosing training |

Also useful from the account-wide `unity` plugin: `unity:unity-cli` (batchmode, tests,
builds), `unity:unity-package-management` (UPM packages headless), `unity:ui`,
`unity:physics-3d-collision`, `unity:initialize-ai-navigation`.

## Subagents (`.claude/agents/`)

| Agent | For |
|---|---|
| `core-sim-engineer` | Pure C# simulation in `Unity/Assets/Arena/Core` + `CoreTests` |
| `rl-trainer` | Observations, actions, rewards, YAML configs, curriculum, reading training curves |
| `unity-integrator` | MonoBehaviours, scenes, prefabs, ML-Agents components, UI, builds |
| `reviewer` | Independent review of a diff against AGENTS.md invariants before merge |

Split big work: independent tasks → Codex (via `/codex-task`) and subagents in parallel;
Claude keeps integration, git and review.
