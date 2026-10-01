# T-033: M8 audio — free sound effects and music in the viewer

- **Milestone:** M8 (D-039)
- **Owner:** Claude subagent (unity-integrator) + Claude (PC)
- **Branch / worktree:** `task/t033-m8-audio` at `C:\PersonalArena-wt\t033` (off `develop` 0a6d6e2;
  `Unity/Library` copied from the T-032 worktree, so batchmode imports are incremental)
- **Depends on:** M7 (T-030..T-032)

## Goal (owner view)

The owner opens `Xem-AI.cmd` and the run now **sounds** like a Vampire Survivors game: weapons swing and
shoot, enemies pop, gems tinkle, coins clink, level-ups chime, the boss roars and the music changes, a
victory or defeat jingle plays at the end. A horde of 250 enemies at view speed x8 never turns into a
wall of noise. `M` mutes, and by default the game goes silent while its window is not in front (the viewer
keeps running in the background for hours). Nothing plays during hidden Auto Farm runs or in the
training build.

## Assets (free only: CC0 first, CC BY allowed with credit)

- Sound effects: **Kenney** CC0 packs, e.g. *Impact Sounds*, *RPG Audio*, *Interface Sounds*,
  *Music Jingles* (https://kenney.nl/assets). Find the real download links on each asset page.
- Music: two looping tracks, a calm/eerie run loop and a tenser boss loop. Prefer CC0 from
  OpenGameArt (https://opengameart.org, filter by CC0) or other CC0 sources; CC BY 3.0/4.0 is allowed
  with credit. Keep music under ~8 MB total; `.ogg` preferred.
- Only import the files actually used (not whole packs) into `Unity/Assets/ThirdParty/Audio/<Pack>/`,
  keep each pack's license text next to it, and add one row per pack to
  `Unity/Assets/ThirdParty/CREDITS.md`. Import settings: effects *Decompress On Load*, music *Streaming*,
  Vorbis.
- Downloading free CC0 / CC BY assets is pre-approved. Never download executables.

## Work

1. **Pure logic, EditMode-tested** (in `Unity/Assets/Arena/View/Audio/`, no `MonoBehaviour`):
   - `SoundCue` enum and a map from `SurvivorEvent` (type, Id, Value, the hero's class kit) to a cue.
     Read `Core/Survivor/SurvivorEvent.cs`, `SurvivorSim` (events of the last step: `sim.Events`) and the
     kits in `SurvivorDefaults` / `SurvivorCatalog`. Suggested mapping:
     - `WeaponFired` → by weapon pattern/visual (sword swing, spear thrust, axe whirl, hammer throw,
       shockwave, magic bolt, fire orb, frost, holy, lightning, arcane, arrow, crossbow, dagger…). Aura
       and other always-on weapons fire silently or very softly.
     - `DamageDealt` → soft hit; `Crit` → sharp hit; `EnemyKilled` → pop/bone crack; `EliteKilled` → big
       impact; `BossDamaged` → heavy hit.
     - `HeroDamaged` → hero hurt; `Blocked` / `Parry` → shield clang; `HeroDied` → death sting.
     - `SkillUsed` → per skill kind (kick, shield, dash/roll, fireball, mana shield, blink, frost burst,
       power shot); `SkillFailed` → nothing.
     - `StrikeLanded` → lightning crack / arrow rain thud / fireball explosion (by source);
       `EnemyExploded` → explosion; `EnemySummoned` → eerie cast.
     - `XpCollected` → gem blip whose pitch rises while gems keep coming (resets after a short gap);
       `GoldCollected` → coin; `Healed` → heal; `MagnetPicked` → whoosh; `ChestOpened` → chest + coins.
     - `LevelUp` / `OfferShown` → level-up chime; `ItemPicked` → card select; `WeaponEvolved` → fanfare.
     - `EliteSpawned` → growl; `BossSpawned` → boss roar **and** music switches to the boss loop.
     - `RunWon` / `BossKilled` → victory jingle; `HeroDied` / `RunTimeUp` / `RunExpired` → defeat/end jingle.
   - A throttle/voice limiter: per-cue minimum interval and maximum simultaneous voices, a global voice cap
     (about 24), small random pitch/volume variation, and loudness falling off with distance from the hero
     (enemies far off-screen are quiet). It must keep a 250-enemy horde at speed x8 readable: few hit
     sounds per frame, kills thinned out, important cues (level-up, boss, chest, evolution, death,
     victory) never dropped. Time comes from the caller (testable).
   - Audio settings: master, music and effects volume (0..1), muted, "mute when the window is in the
     background" (default **on**); stored in `PlayerPrefs` through a small injectable storage so tests
     do not touch real prefs.
   - EditMode tests for the mapping, the throttle (horde at x8 stays under the caps; important cues pass),
     the gem pitch combo and the settings round trip.
2. **`SurvivorAudio` MonoBehaviour** (viewer only): an `AudioSource` pool, two music sources with a short
   crossfade (run loop ↔ boss loop), jingles, 2D sounds with a little stereo pan from screen position.
   Clips come from an asset the scene references (follow the existing `SurvivorArtSet` /
   `SurvivorArtSetBuilder` / `PlaySceneBuilder` pattern so batch builds wire it up), or `Resources`
   if that is clearly simpler.
   - Hook it into `SurvivorWatchController`: after every `sim.Step` (the step loop and `ResolvePick`), on
     run start/restart, when the level-up cards appear, when the AI's pick is highlighted, on run end
     (win/lose), on class switch. Music pauses or ducks while paused (Esc) and on the end screen.
   - Panel and HUD buttons get a soft click (find the shared button factory in `View/Meta/MetaPanel.cs`
     or `SurvivorHud`); buying something plays a coin sound, a refused action a soft error.
   - Key **M** toggles mute (show a short HUD notice "Đã tắt tiếng" / "Đã bật tiếng"); add "M tắt/bật
     tiếng" to the help text. Apply the settings every frame cheaply via `AudioListener.volume`
     (0 when muted, or when `!Application.isFocused` and mute-in-background is on).
   - **Never make noise in automated runs:** any of `-screenshot`, `-quitAfterScreenshot`, `-smokeTest`
     or a new `-mute` flag forces silence (the owner's speakers must stay quiet while Claude checks builds).
   - `-audioLog <path>`: at quit (or run end in screenshot mode) write a small text/JSON file with how many
     times each cue was requested and actually played, so the wiring can be checked without hearing it.
   - Auto Farm runs (background threads, no renderer) must not produce sound; the training build/scene
     must not get any audio component.
3. **Docs:** short section in `docs/tasks/T-033-m8-audio.md` (this file, "Result") listing the packs and
   the cue → file mapping; CREDITS rows.

## Checks

- `dotnet test` CoreTests stays green (no Core change expected).
- Unity EditMode tests green (the 238 existing plus the new ones).
- Build the viewer to a scratch folder (`-watchBuildOutput <scratch>`), run it with `-mute -screenshot
  <png> -quitAfterScreenshot -audioLog <txt>` and a champion/latest brain copy; the log shows cues for
  weapons, hits, kills, gems, level-up, and the run end; `Player.log` has no errors or missing clips.
- Do **not** touch `Build/Watch`, `Build/Training`, `Trainer/runs/` or the owner's running training.

## Result

### Packs (all CC0 1.0; rows in `Unity/Assets/ThirdParty/CREDITS.md`, license text in each folder)

Only the files named in `SurvivorSoundSetBuilder.Table` are imported (about 90 short effects + 2 music loops),
into `Unity/Assets/ThirdParty/Audio/<Pack>/`:

| Folder | Pack | Source |
|---|---|---|
| `KenneyImpactSounds` | Kenney *Impact Sounds* | https://kenney.nl/assets/impact-sounds |
| `KenneyRpgAudio` | Kenney *RPG Audio* | https://kenney.nl/assets/rpg-audio |
| `KenneyInterfaceSounds` | Kenney *Interface Sounds* | https://kenney.nl/assets/interface-sounds |
| `KenneySciFiSounds` | Kenney *Sci-fi Sounds* | https://kenney.nl/assets/sci-fi-sounds |
| `KenneyDigitalAudio` | Kenney *Digital Audio* | https://kenney.nl/assets/digital-audio |
| `KenneyMusicJingles` | Kenney *Music Jingles* | https://kenney.nl/assets/music-jingles |
| `Music` | "When the Shadows Gather" by Tsorthan Grove (run loop, 2.6 MB); "Heavy Boss Battle 2" by MintoDog (boss loop, 3.9 MB) | OpenGameArt (CC0) |

Import rules (`View/Editor/AudioImportRules.cs`, everything under `Assets/ThirdParty/Audio/`): Vorbis;
effects *Decompress On Load* (quality 0.7), music *Streaming* (quality 0.6, load in background).

### Code

- `View/Audio/` (pure, EditMode-tested): `SoundCue`, `SoundCueInfo` (per-cue interval, voices, hold time,
  volume, pitch, jitter, spatial, important), `SoundCueMap` (event → cue, jingle by `EndReason`),
  `SoundThrottle` (per-cue interval and voice cap, global cap 24 with 4 voices kept for important cues),
  `GemCombo` (gem pitch climbs a pentatonic scale, resets after 0.7 s), `SoundMixer` (throttle + jitter +
  distance fall-off + pan + counts), `SoundStats` (`-audioLog` text), `SoundSettings` (master / music /
  effects / muted / mute-in-background, PlayerPrefs through `ISoundSettingsStorage`), `UiSounds` (menu hub).
- `SurvivorAudio` (viewer only): 28 effect sources, 2 crossfading music sources (run ↔ boss), 1 jingle
  source; all 2D with a small stereo pan; music ducks while paused, on the end screen and while the cards
  are open. Clips come from `SurvivorSoundSet.asset`, built by `SurvivorSoundSetBuilder` (called by
  `PlaySceneBuilder`, menu *Personal Arena/Rebuild Survivor Sound Set*).
- `SurvivorWatchController` hooks: after every step (step loop and `ResolvePick`), run start/restart,
  cards shown, AI pick highlighted, run end (one jingle), class switch, Esc pause, **M** mute toggle
  ("Đã tắt tiếng" / "Đã bật tiếng", saved), help line "M tắt/bật tiếng". `AudioListener.volume` is set
  every frame: 0 when muted, when the window is in the background (default on), or forced silent by
  `-screenshot`, `-quitAfterScreenshot`, `-smokeTest` or `-mute`. `-audioLog <path>` writes the counts at
  quit (and at run end in screenshot mode).
- Buttons: `MetaPanel.CreateButton`, the HUD button factory and the panel close buttons click; buying a
  class or a level plays a coin, a refused purchase / stat point a soft error.
- Silent by construction: Auto Farm runs use `SurvivorEvaluator` (Core, no view); the training scene has
  no `SurvivorAudio`.

### Cue → file (relative to `Assets/ThirdParty/Audio/`; one clip picked at random per play)

| Cue | Files |
|---|---|
| SwordSwing | RpgAudio knifeSlice, knifeSlice2 |
| SpearThrust | RpgAudio drawKnife1, drawKnife2 |
| AxeWhirl | RpgAudio cloth3, cloth4 |
| HammerThrow | RpgAudio cloth1, cloth2 |
| Shockwave | Impact impactPunch_heavy_000/001 |
| MagicBolt | SciFi laserSmall_000/001/002 |
| FireOrb | SciFi forceField_000/001 |
| FrostNova | Impact impactGlass_light_000/001 |
| ArcaneBeam | SciFi laserRetro_000/001 |
| ArrowShot, MultiShot, PowerShot | Interface pluck_001/002 |
| KnifeWhirl | RpgAudio drawKnife3 |
| DaggerSlash | RpgAudio knifeSlice2 |
| CrossbowShot | RpgAudio metalLatch, metalClick |
| LightningStrike | Digital zap1, zap2 |
| ArrowRain | Impact impactWood_light_000/001/002 |
| Explosion (fireball blast, exploder) | SciFi explosionCrunch_000/001/002 |
| Summon | Digital phaserDown1, phaserDown2 |
| Hit | Impact impactSoft_medium_000/001/002 |
| CritHit | Impact impactPunch_medium_000/001/002 |
| EnemyKill | Impact impactPlank_medium_000/001/002 |
| EliteKill | Impact impactWood_heavy_000/001 |
| BossHit | Impact impactPunch_heavy_002/003 |
| BossKill | SciFi lowFrequency_explosion_001 |
| HeroHurt | Impact impactSoft_heavy_000/001/002 |
| ShieldBlock | Impact impactMetal_medium_000/001 |
| Parry | Impact impactPlate_light_000/001 |
| HeroDeath | Impact impactBell_heavy_000 |
| Kick | Impact impactPunch_medium_003/004 |
| ShieldUp | Impact impactMetal_light_000/001 |
| Dash (dash, roll-back) | RpgAudio cloth1, cloth2 |
| Fireball | SciFi thrusterFire_000/001 |
| ManaShield | SciFi forceField_002/003 |
| Blink | Digital phaseJump1, phaseJump2 |
| FrostBurst | Impact impactGlass_medium_000/001 |
| Gem (rising pitch) | Interface glass_001 |
| Coin | RpgAudio handleCoins, handleCoins2 |
| Heal | Digital powerUp3 |
| Magnet | Digital highUp |
| Chest | RpgAudio doorOpen_1 |
| LevelUp | Digital powerUp1 |
| CardsShown / CardPick | Interface maximize_001 / confirmation_001 |
| Evolution | MusicJingles jingles-hit_01 |
| EliteSpawn | SciFi slime_000/001 |
| BossSpawn (+ boss music) | SciFi lowFrequency_explosion_000 |
| VictoryJingle / DefeatJingle / EndJingle | MusicJingles jingles-hit_00 / jingles-pizzicato_03 / jingles-pizzicato_00 |
| UiClick / UiCoin / UiError / UiToggle | Interface click_001 / RpgAudio handleCoins2 / Interface error_004 / Interface switch_002 |
| Run music / boss music | Music when_the_shadows_gather / heavy_boss_battle_2_bpm110 |

Silent on purpose: aura and holy-field pulses, enemy spawns, `OfferShown` (the viewer plays CardsShown when
it shows the cards), `SkillFailed`, and the run-end events (the jingle is chosen once per run by `EndReason`).
A Mage frost burst is heard once as FrostBurst (its −1 strike is dropped), the fireball blast as Explosion.

### Tests and verification

- New EditMode tests (35): `SoundCueMapTests` (15: weapons of all classes, evolutions, silent auras,
  strikes, every skill of every kit, frost-burst de-dup, fireball blast, silent events, world points,
  jingles, every cue has files in the builder table), `SoundThrottleTests` (7: 250-enemy horde at 48
  steps per frame for 10 s stays ≤ 20 ordinary voices and ≤ the per-cue caps, hits still heard every second;
  important cues pass under a full mix; interval; hold; reserve; reset), `SoundMixerTests` (8: gem combo,
  distance fall-off and pan, jitter range, audio-log counts), `SoundSettingsTests` (5: defaults, round trip,
  clamping, listener volume cases, buses).
- Verification run (scratch build, never `Build/Watch`): `-mute -screenshot -quitAfterScreenshot
  -audioLog -speed 8 -seed 23 -runSeconds 60 -endShot -profile <scratch>` with a copy of the Warrior
  champion brain; the audio log must list weapon, hit, kill, gem, level-up and a run-end jingle with
  `forcedSilent true` and `missingClipCues 0`, and `Player.log` has no errors.
- Measured 2026-10-01: CoreTests 236/236, EditMode 273/273 (35 audio). Verification run (7 runs of 60 s):
  2232 cues requested, 1525 played (throttle), weapon 318, hit 289, kill 244, gem 103, level-up 11,
  run-end jingle 7, `forcedSilent true`, `missingClipCues 0`, 0 problem lines in `Player.log`.
- Tuning after the run: the Warrior brain raises its shield ~1.2 times per second (SurvivorEval), so
  `ShieldUp` is soft and sparse (0.7 s interval, 1 voice, volume 0.22); `Kick` 0.5.
