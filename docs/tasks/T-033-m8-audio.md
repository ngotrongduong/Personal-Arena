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

(filled in when done)
