@echo off
rem Double-click to watch the AI play with its newest trained brain (see docs/TRAINING.md).
start "" powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0Trainer\watch_ai.ps1"
