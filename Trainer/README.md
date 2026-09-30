# Trainer

Python side of Personal Arena.

| Path | What |
|---|---|
| `requirements-ml.lock.txt` | Exact ML environment (Windows, Python 3.10.12, torch cu121). Install: `docs/SETUP.md` |
| `config/warrior_survivor_ppo.yaml` | Survivor PPO and its reward-measured run-length curriculum |
| `arena_trainer.py` *(M4)* | Wrapper the game calls to train one owned hero and write `progress.json` |
| `tests/` *(M4)* | pytest |

## Survivor runs

New runs use names such as `warrior-s001`, `warrior-s002`, and so on. Legacy names such as
`warrior-011` do not affect this sequence and are never resumed as Survivor runs.

Only the Warrior trains in Survivor mode for now. Mage and Archer get their own Survivor configs in
M7; until then the training service reports that clearly instead of starting a run.

Each run records `schema_version.txt`; a run without it counts as schema 1. Training resumes a
checkpoint when its observation schema matches the current schema. Balance or rules changes that
leave the schema unchanged continue the same run.

The Warrior Survivor config uses a 3-by-512 PPO network with game-normalized observations. Its
reward curriculum advances the episode limit from 3 to 6 to 10 to 15 minutes only after the
smoothed mean reward shows that most agents survive the current lesson.

How to train and read results: `docs/TRAINING.md`.

## Khi game đổi quan sát (schema)

Khi thêm quan sát hoặc action, tạo `schemas/survivor_v<N+1>.json` mô tả layout mới. Schema mới chỉ
được **tăng thuần**: có thể chèn/thêm segment hoặc field, tăng `repeat`, thêm action branch hoặc tăng
kích thước branch; không được xoá, đổi tên hay đổi thứ tự phần cũ. Các field/segment khớp nhau bằng
tên. Việc dùng một slot `reserved_<k>` đã có không cần tăng schema.

Khi owner bấm TRAIN mà chưa có run đúng schema, training service tự tìm checkpoint schema cũ mới
nhất, nâng cấp sang run kế tiếp và học tiếp ở cùng số bước. Có thể chạy thủ công:

```text
python -m Trainer.brain_upgrade --source warrior-s001 --new-run warrior-s002
```

Các tuỳ chọn `--runs-dir`, `--behavior`, `--old-version` và `--new-version` dùng khi đường dẫn,
behavior hoặc phiên bản không phải mặc định.

## Champion evaluation

`champion.py` evaluates the newest checkpoint on the fixed 100-seed Survivor suite and keeps the
best brain without overwriting its history:

```text
python Trainer/champion.py --behavior Warrior status
python Trainer/champion.py --behavior Warrior evaluate
python Trainer/champion.py --behavior Warrior evaluate --run-id warrior-s001
python Trainer/champion.py --behavior Warrior restore warrior-s001-2000000
```

The training service starts the same evaluation in a background thread whenever the current run
has a checkpoint at least 2,000,000 steps newer than its last recorded evaluation. Stopping the
service also stops the evaluator. Evaluation failures are reported in service status and never
stop training.

Champion data lives below `Trainer/runs/champions/<Behavior>/`:

```text
champion.brain                 current champion, replaced atomically
champion.json                  current champion metadata and evaluation
history/<run-id>-<step>.brain  immutable brain for every past champion
history/<run-id>-<step>.json   matching metadata
evaluations.jsonl              append-only record of every evaluation and restore
latest_eval.json               newest evaluation for the viewer
candidates/                    temporary challenger exports and evaluator output
```

The score is `median + 0.5 * P10 + 300 * win_rate - 60 * catastrophic_count`. A challenger wins
when there is no compatible-schema champion, or when its score exceeds the champion by more than
5 points without increasing catastrophic deaths. If evaluation settings changed, the current
champion is evaluated again under the new settings before comparison. `restore` copies an existing
history entry back to `champion.brain`; history entries are never deleted.

The fixed evaluation settings are 100 seeds starting at 1,000,000, tier 1, 900 seconds,
deterministic actions, and `max(1, CPU count / 4)` worker threads. M4A acceptance remains median
survival at least 600 seconds, P10 at least 420 seconds, and zero deaths before 180 seconds.

### SurvivorEval JSON

The top-level keys are `brain`, `settings`, `summary`, `passes_m4a`, and `runs`.
`brain` contains lowercase `name` and `step`. `settings` contains lowercase `seeds`,
`seed_start`, `tier`, `run_seconds`, and `deterministic`.

Existing evaluator statistics retain their PascalCase keys. `summary` contains `Runs`,
`MedianSurvivedSeconds`, `P10SurvivedSeconds`, `MeanSurvivedSeconds`, `WinRate`,
`CatastrophicCount`, `GoldPerMinute`, `XpPerMinute`, `DamageTakenPerMinute`,
`MeanBossDamageFraction`, `MeanLevel`, `EndReasonCounts`, `DeathCauseCounts`, plus lowercase
`behavior`. Each item in `runs` contains `Seed`, `SurvivedSeconds`, `EndReason`, `DeathCause`,
`Level`, `Kills`, `EliteKills`, `Gold`, `TotalXp`, `DamageTaken`, `DamageDealt`,
`BossDamageFraction`, `MinHpRatio`, `MinHpTime`, `SkillUses`, `FinalItemLevels`, plus lowercase
`behavior`.

Each `behavior` object contains `Aggression`, `Caution`, `Greed`, `Exploration`, `CrowdControl`,
`BossHunting`, `SkillDiscipline`, `PreferredRange`, `KeepDistance`, `KickUses`, `BlockUses`,
`DashUses`, `EffectiveKicks`, `EffectiveBlocks`, and `EffectiveDashes`. Float metrics use `-1`
when the run had no qualifying samples.
