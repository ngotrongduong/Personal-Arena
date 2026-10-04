"""PreToolUse hook for Read: saves tokens by refusing wasteful reads.

- Generated folders (Unity Library/Temp, Build, bin/obj) are refused.
- In log/output folders (Logs, results, Trainer/runs) only small files (<= LOG_MAX_LINES) may be read whole.
- A whole-file read (no offset/limit) of a text file longer than MAX_LINES is refused with a hint to
  Grep first and read a line range.
Anything unexpected (bad input, missing file, decode error) lets the read through.
"""
from __future__ import annotations

import json
import os
import sys

MAX_LINES = 600
LOG_MAX_LINES = 200
BLOCKED_DIRS = ("library", "temp", "obj", "bin", "build", "usersettings")
LOG_DIRS = ("logs", "results")
LOG_PREFIXES = ("trainer/runs/",)
TEXT_EXTS = {".cs", ".py", ".md", ".txt", ".json", ".yaml", ".yml", ".log", ".csv", ".xml", ".ps1",
             ".cmd", ".asmdef", ".uss", ".uxml", ".shader", ".toml", ".cfg", ".ini"}


def deny(reason: str) -> None:
    print(json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse",
                                             "permissionDecision": "deny",
                                             "permissionDecisionReason": reason}}))


def main() -> None:
    data = json.load(sys.stdin)
    tool_input = data.get("tool_input") or {}
    path = tool_input.get("file_path") or ""
    if not path:
        return
    root = os.environ.get("CLAUDE_PROJECT_DIR") or data.get("cwd") or os.getcwd()
    try:
        rel = os.path.relpath(os.path.abspath(path), os.path.abspath(root))
    except ValueError:  # other drive on Windows
        return
    if rel.startswith(".."):
        return  # outside the repo (scratchpad, transcripts): not our business
    rel_l = rel.replace("\\", "/").lower()
    parts = rel_l.split("/")[:-1]
    if any(p in BLOCKED_DIRS for p in parts):
        deny(f"read_guard: '{rel}' is generated output (Library/Temp/Build/bin/obj). Do not read it. Use Grep "
             "with a narrow pattern if you must look inside.")
        return
    is_log = any(p in LOG_DIRS for p in parts) or rel_l.startswith(LOG_PREFIXES) or rel_l.endswith(".log")
    if tool_input.get("offset") is not None or tool_input.get("limit") is not None:
        return
    if not os.path.isfile(path):
        return
    if not is_log and os.path.splitext(rel_l)[1] not in TEXT_EXTS:
        return
    with open(path, "rb") as f:
        lines = sum(1 for _ in f)
    if is_log and lines > LOG_MAX_LINES:
        deny(f"read_guard: '{rel}' is a log/output file with {lines} lines. Grep for errors/the value you need, "
             "or Read only the tail with offset/limit (Unity: use Tools/unity-run.ps1 summaries).")
    elif lines > MAX_LINES:
        deny(f"read_guard: '{rel}' has {lines} lines (limit {MAX_LINES} for a whole-file read). "
             "Grep for the symbol/section first (output_mode content, -n), then Read with offset/limit "
             "around it. If you truly need the whole file, Read it in chunks with explicit offset/limit.")


if __name__ == "__main__":
    try:
        main()
    except Exception:  # never block work because the guard itself failed
        pass
