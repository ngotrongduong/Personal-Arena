import sys
from pathlib import Path

# Make the repo root importable so tests can do `from Trainer import arena_trainer`.
sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
