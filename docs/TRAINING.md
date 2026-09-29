# Training the Warrior

The M2 environment runs 16 independent headless arenas per Unity process by default. Each agent
advances its deterministic simulation at 60 Hz and requests a new policy decision every five ticks
(12 Hz).

## Build the environment

From the Unity project, run **Personal Arena > Build Training Scene**, then **Personal Arena > Build
Training Env (Windows)**. The output is:

```text
Build/Training/PersonalArenaTraining.exe
```

The same operations can be run without opening the Editor UI:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.2f1\Editor\Unity.exe" `
  -batchmode -quit -projectPath Unity `
  -executeMethod PersonalArena.ML.Editor.TrainingSceneBuilder.Build

& "C:\Program Files\Unity\Hub\Editor\6000.3.2f1\Editor\Unity.exe" `
  -batchmode -projectPath Unity `
  -executeMethod PersonalArena.ML.Editor.TrainingBuild.BuildWindows
```

Pass `-buildPath <directory>` to the second command to change the output directory. The build has
only `Assets/Scenes/Training.unity`; the tool does not alter `EditorBuildSettings`. At runtime,
`--arena-agents N` overrides the default 16 agents per process.

## Start training

Run from the repository root:

```powershell
& .venv-ml\Scripts\python.exe Trainer\arena_trainer.py --run-id warrior-v1
```

The wrapper defaults to four Unity processes, time scale 20, no graphics, the built environment
above, and `Trainer/config/warrior_ppo.yaml`. Inspect the exact command without launching anything:

```powershell
& .venv-ml\Scripts\python.exe Trainer\arena_trainer.py --run-id warrior-v1 --dry-run
```

The curriculum uses 1, 2, 4, 8, then 16 zombies. Its smoothed-reward threshold of 6 and minimum 300
episodes per lesson are initial estimates; tune them after inspecting real learning curves. The
`warrior_ppo_randomized.yaml` variant keeps the first four lessons fixed and randomizes arena size
from 14 to 32 and zombie HP, damage, and speed multipliers from 0.8 to 1.25 in the final lesson.

## Monitor, resume, and fine-tune

Start TensorBoard in another terminal:

```powershell
& .venv-ml\Scripts\tensorboard.exe --logdir Trainer/runs
```

Resume an interrupted run using the same run ID:

```powershell
& .venv-ml\Scripts\python.exe Trainer\arena_trainer.py --run-id warrior-v1 --resume
```

Initialize a new run from an earlier run:

```powershell
& .venv-ml\Scripts\python.exe Trainer\arena_trainer.py `
  --run-id warrior-finetune --initialize-from warrior-v1
```

ML-Agents writes checkpoints and the exported model beneath `Trainer/runs/<run-id>/`. The final
Warrior model is normally `Trainer/runs/<run-id>/Warrior.onnx`; checkpoint exports can also appear
under the same run directory. Runs and ONNX files are intentionally ignored by source control.
