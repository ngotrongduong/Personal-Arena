import os
import sys
import tempfile
from pathlib import Path

# Make the repo root importable so tests can do `from Trainer import arena_trainer`.
REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPOSITORY_ROOT))

# Sandboxes that cannot reach the Windows user temp folder set PA_PYTEST_TEMP to a writable folder
# (and pass the same folder to pytest with --basetemp).
if os.environ.get("PA_PYTEST_TEMP"):
    TEST_TEMP = Path(os.environ["PA_PYTEST_TEMP"])
    TEST_TEMP.mkdir(parents=True, exist_ok=True)
    tempfile.tempdir = str(TEST_TEMP)
