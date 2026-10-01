using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PersonalArena.View.Editor
{
    /// <summary>
    /// Creates or refreshes the SurvivorSoundSet asset from the free audio packs in Assets/ThirdParty/Audio
    /// (Kenney CC0 sound packs, CC0 music from OpenGameArt; see CREDITS.md). The table below is the single
    /// cue → file mapping; only the files named here are imported into the project.
    /// </summary>
    public static class SurvivorSoundSetBuilder
    {
        public const string AssetPath = "Assets/Arena/View/Audio/SurvivorSoundSet.asset";
        public const string AudioRoot = "Assets/ThirdParty/Audio/";
        public const string RunMusicFile = "Music/when_the_shadows_gather.ogg";
        public const string BossMusicFile = "Music/heavy_boss_battle_2_bpm110.ogg";

        /// <summary>Cue → clip files (relative to <see cref="AudioRoot"/>). One clip is picked at random per play.</summary>
        public static readonly IReadOnlyList<KeyValuePair<SoundCue, string[]>> Table = new[]
        {
            Entry(SoundCue.SwordSwing, "KenneyRpgAudio/knifeSlice.ogg", "KenneyRpgAudio/knifeSlice2.ogg"),
            Entry(SoundCue.SpearThrust, "KenneyRpgAudio/drawKnife1.ogg", "KenneyRpgAudio/drawKnife2.ogg"),
            Entry(SoundCue.AxeWhirl, "KenneyRpgAudio/cloth3.ogg", "KenneyRpgAudio/cloth4.ogg"),
            Entry(SoundCue.HammerThrow, "KenneyRpgAudio/cloth1.ogg", "KenneyRpgAudio/cloth2.ogg"),
            Entry(SoundCue.Shockwave, "KenneyImpactSounds/impactPunch_heavy_000.ogg", "KenneyImpactSounds/impactPunch_heavy_001.ogg"),
            Entry(SoundCue.MagicBolt, "KenneySciFiSounds/laserSmall_000.ogg", "KenneySciFiSounds/laserSmall_001.ogg",
                "KenneySciFiSounds/laserSmall_002.ogg"),
            Entry(SoundCue.FireOrb, "KenneySciFiSounds/forceField_000.ogg", "KenneySciFiSounds/forceField_001.ogg"),
            Entry(SoundCue.FrostNova, "KenneyImpactSounds/impactGlass_light_000.ogg", "KenneyImpactSounds/impactGlass_light_001.ogg"),
            Entry(SoundCue.ArcaneBeam, "KenneySciFiSounds/laserRetro_000.ogg", "KenneySciFiSounds/laserRetro_001.ogg"),
            Entry(SoundCue.ArrowShot, "KenneyInterfaceSounds/pluck_001.ogg", "KenneyInterfaceSounds/pluck_002.ogg"),
            Entry(SoundCue.MultiShot, "KenneyInterfaceSounds/pluck_001.ogg", "KenneyInterfaceSounds/pluck_002.ogg"),
            Entry(SoundCue.KnifeWhirl, "KenneyRpgAudio/drawKnife3.ogg"),
            Entry(SoundCue.DaggerSlash, "KenneyRpgAudio/knifeSlice2.ogg"),
            Entry(SoundCue.CrossbowShot, "KenneyRpgAudio/metalLatch.ogg", "KenneyRpgAudio/metalClick.ogg"),

            Entry(SoundCue.LightningStrike, "KenneyDigitalAudio/zap1.ogg", "KenneyDigitalAudio/zap2.ogg"),
            Entry(SoundCue.ArrowRain, "KenneyImpactSounds/impactWood_light_000.ogg", "KenneyImpactSounds/impactWood_light_001.ogg",
                "KenneyImpactSounds/impactWood_light_002.ogg"),
            Entry(SoundCue.Explosion, "KenneySciFiSounds/explosionCrunch_000.ogg", "KenneySciFiSounds/explosionCrunch_001.ogg",
                "KenneySciFiSounds/explosionCrunch_002.ogg"),
            Entry(SoundCue.Summon, "KenneyDigitalAudio/phaserDown1.ogg", "KenneyDigitalAudio/phaserDown2.ogg"),

            Entry(SoundCue.Hit, "KenneyImpactSounds/impactSoft_medium_000.ogg", "KenneyImpactSounds/impactSoft_medium_001.ogg",
                "KenneyImpactSounds/impactSoft_medium_002.ogg"),
            Entry(SoundCue.CritHit, "KenneyImpactSounds/impactPunch_medium_000.ogg", "KenneyImpactSounds/impactPunch_medium_001.ogg",
                "KenneyImpactSounds/impactPunch_medium_002.ogg"),
            Entry(SoundCue.EnemyKill, "KenneyImpactSounds/impactPlank_medium_000.ogg", "KenneyImpactSounds/impactPlank_medium_001.ogg",
                "KenneyImpactSounds/impactPlank_medium_002.ogg"),
            Entry(SoundCue.EliteKill, "KenneyImpactSounds/impactWood_heavy_000.ogg", "KenneyImpactSounds/impactWood_heavy_001.ogg"),
            Entry(SoundCue.BossHit, "KenneyImpactSounds/impactPunch_heavy_002.ogg", "KenneyImpactSounds/impactPunch_heavy_003.ogg"),
            Entry(SoundCue.BossKill, "KenneySciFiSounds/lowFrequency_explosion_001.ogg"),
            Entry(SoundCue.HeroHurt, "KenneyImpactSounds/impactSoft_heavy_000.ogg", "KenneyImpactSounds/impactSoft_heavy_001.ogg",
                "KenneyImpactSounds/impactSoft_heavy_002.ogg"),
            Entry(SoundCue.ShieldBlock, "KenneyImpactSounds/impactMetal_medium_000.ogg", "KenneyImpactSounds/impactMetal_medium_001.ogg"),
            Entry(SoundCue.Parry, "KenneyImpactSounds/impactPlate_light_000.ogg", "KenneyImpactSounds/impactPlate_light_001.ogg"),
            Entry(SoundCue.HeroDeath, "KenneyImpactSounds/impactBell_heavy_000.ogg"),

            Entry(SoundCue.Kick, "KenneyImpactSounds/impactPunch_medium_003.ogg", "KenneyImpactSounds/impactPunch_medium_004.ogg"),
            Entry(SoundCue.ShieldUp, "KenneyImpactSounds/impactMetal_light_000.ogg", "KenneyImpactSounds/impactMetal_light_001.ogg"),
            Entry(SoundCue.Dash, "KenneyRpgAudio/cloth1.ogg", "KenneyRpgAudio/cloth2.ogg"),
            Entry(SoundCue.Fireball, "KenneySciFiSounds/thrusterFire_000.ogg", "KenneySciFiSounds/thrusterFire_001.ogg"),
            Entry(SoundCue.ManaShield, "KenneySciFiSounds/forceField_002.ogg", "KenneySciFiSounds/forceField_003.ogg"),
            Entry(SoundCue.Blink, "KenneyDigitalAudio/phaseJump1.ogg", "KenneyDigitalAudio/phaseJump2.ogg"),
            Entry(SoundCue.FrostBurst, "KenneyImpactSounds/impactGlass_medium_000.ogg", "KenneyImpactSounds/impactGlass_medium_001.ogg"),
            Entry(SoundCue.PowerShot, "KenneyInterfaceSounds/pluck_001.ogg", "KenneyInterfaceSounds/pluck_002.ogg"),

            Entry(SoundCue.Gem, "KenneyInterfaceSounds/glass_001.ogg"),
            Entry(SoundCue.Coin, "KenneyRpgAudio/handleCoins.ogg", "KenneyRpgAudio/handleCoins2.ogg"),
            Entry(SoundCue.Heal, "KenneyDigitalAudio/powerUp3.ogg"),
            Entry(SoundCue.Magnet, "KenneyDigitalAudio/highUp.ogg"),
            Entry(SoundCue.Chest, "KenneyRpgAudio/doorOpen_1.ogg"),
            Entry(SoundCue.LevelUp, "KenneyDigitalAudio/powerUp1.ogg"),
            Entry(SoundCue.CardsShown, "KenneyInterfaceSounds/maximize_001.ogg"),
            Entry(SoundCue.CardPick, "KenneyInterfaceSounds/confirmation_001.ogg"),
            Entry(SoundCue.Evolution, "KenneyMusicJingles/jingles-hit_01.ogg"),
            Entry(SoundCue.EliteSpawn, "KenneySciFiSounds/slime_000.ogg", "KenneySciFiSounds/slime_001.ogg"),
            Entry(SoundCue.BossSpawn, "KenneySciFiSounds/lowFrequency_explosion_000.ogg"),

            Entry(SoundCue.VictoryJingle, "KenneyMusicJingles/jingles-hit_00.ogg"),
            Entry(SoundCue.DefeatJingle, "KenneyMusicJingles/jingles-pizzicato_03.ogg"),
            Entry(SoundCue.EndJingle, "KenneyMusicJingles/jingles-pizzicato_00.ogg"),

            Entry(SoundCue.UiClick, "KenneyInterfaceSounds/click_001.ogg"),
            Entry(SoundCue.UiCoin, "KenneyRpgAudio/handleCoins2.ogg"),
            Entry(SoundCue.UiError, "KenneyInterfaceSounds/error_004.ogg"),
            Entry(SoundCue.UiToggle, "KenneyInterfaceSounds/switch_002.ogg")
        };

        [MenuItem("Personal Arena/Rebuild Survivor Sound Set")]
        public static void BuildFromMenu()
        {
            Selection.activeObject = EnsureSoundSet();
        }

        public static SurvivorSoundSet EnsureSoundSet()
        {
            SurvivorSoundSet set = AssetDatabase.LoadAssetAtPath<SurvivorSoundSet>(AssetPath);
            if (set == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                AssetDatabase.Refresh();
                set = ScriptableObject.CreateInstance<SurvivorSoundSet>();
                AssetDatabase.CreateAsset(set, AssetPath);
            }

            List<string> missing = new List<string>();
            List<SurvivorSoundSet.CueClips> cues = new List<SurvivorSoundSet.CueClips>(Table.Count);
            foreach (KeyValuePair<SoundCue, string[]> entry in Table)
            {
                List<AudioClip> clips = new List<AudioClip>(entry.Value.Length);
                foreach (string file in entry.Value)
                {
                    AudioClip clip = Clip(file, missing);
                    if (clip != null)
                    {
                        clips.Add(clip);
                    }
                }
                cues.Add(new SurvivorSoundSet.CueClips { Cue = entry.Key, Clips = clips.ToArray() });
            }

            set.Cues = cues.ToArray();
            set.RunMusic = Clip(RunMusicFile, missing);
            set.BossMusic = Clip(BossMusicFile, missing);
            if (missing.Count > 0)
            {
                throw new InvalidOperationException("Missing audio clips (run the T-033 fetch script): " + string.Join(", ", missing));
            }

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return set;
        }

        private static AudioClip Clip(string file, List<string> missing)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot + file);
            if (clip == null)
            {
                missing.Add(file);
            }
            return clip;
        }

        private static KeyValuePair<SoundCue, string[]> Entry(SoundCue cue, params string[] files)
        {
            return new KeyValuePair<SoundCue, string[]>(cue, files);
        }
    }
}
