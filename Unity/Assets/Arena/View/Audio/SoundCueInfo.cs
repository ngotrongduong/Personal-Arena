using System;

namespace PersonalArena.View
{
    /// <summary>Which volume slider a cue follows.</summary>
    public enum SoundBus
    {
        Effects,
        Music
    }

    /// <summary>
    /// Mixing rules of one cue: how often it may start (<see cref="MinInterval"/>, real seconds), how many copies may
    /// sound at once (<see cref="MaxVoices"/>, each held for <see cref="HoldSeconds"/>), its base loudness and pitch,
    /// the random variation, whether it fades with distance from the hero, and whether it is important
    /// (never dropped by the throttle: level-up, boss, chest, evolution, death, jingles, menus).
    /// </summary>
    public readonly struct SoundCueInfo
    {
        public readonly float MinInterval;
        public readonly int MaxVoices;
        public readonly float HoldSeconds;
        public readonly float Volume;
        public readonly float Pitch;
        public readonly float PitchJitter;
        public readonly float VolumeJitter;
        public readonly bool Spatial;
        public readonly bool Important;
        public readonly SoundBus Bus;

        public SoundCueInfo(float minInterval, int maxVoices, float holdSeconds, float volume, float pitch = 1f,
            float pitchJitter = 0.06f, float volumeJitter = 0.1f, bool spatial = false, bool important = false,
            SoundBus bus = SoundBus.Effects)
        {
            MinInterval = minInterval;
            MaxVoices = maxVoices;
            HoldSeconds = holdSeconds;
            Volume = volume;
            Pitch = pitch;
            PitchJitter = pitchJitter;
            VolumeJitter = volumeJitter;
            Spatial = spatial;
            Important = important;
            Bus = bus;
        }

        /// <summary>The rules of <paramref name="cue"/> (tuned so a 250-enemy horde at x8 stays readable).</summary>
        public static SoundCueInfo For(SoundCue cue)
        {
            switch (cue)
            {
                // Weapons: a few per second at most; hero-centred.
                case SoundCue.SwordSwing: return new SoundCueInfo(0.09f, 2, 0.3f, 0.42f, 0.95f);
                case SoundCue.SpearThrust: return new SoundCueInfo(0.09f, 2, 0.3f, 0.42f);
                case SoundCue.AxeWhirl: return new SoundCueInfo(0.25f, 1, 0.5f, 0.38f, 0.9f);
                case SoundCue.HammerThrow: return new SoundCueInfo(0.12f, 2, 0.35f, 0.4f, 0.85f);
                case SoundCue.Shockwave: return new SoundCueInfo(0.2f, 1, 0.5f, 0.5f, 0.85f);
                case SoundCue.MagicBolt: return new SoundCueInfo(0.08f, 2, 0.3f, 0.3f, 1.1f);
                case SoundCue.FireOrb: return new SoundCueInfo(0.25f, 1, 0.5f, 0.35f);
                case SoundCue.FrostNova: return new SoundCueInfo(0.2f, 1, 0.5f, 0.45f, 1.05f);
                case SoundCue.ArcaneBeam: return new SoundCueInfo(0.12f, 2, 0.4f, 0.32f);
                case SoundCue.ArrowShot: return new SoundCueInfo(0.08f, 2, 0.25f, 0.42f, 1.05f);
                case SoundCue.MultiShot: return new SoundCueInfo(0.1f, 2, 0.3f, 0.45f, 0.9f);
                case SoundCue.KnifeWhirl: return new SoundCueInfo(0.25f, 1, 0.4f, 0.35f, 1.15f);
                case SoundCue.DaggerSlash: return new SoundCueInfo(0.08f, 2, 0.25f, 0.38f, 1.25f);
                case SoundCue.CrossbowShot: return new SoundCueInfo(0.1f, 2, 0.3f, 0.48f);

                // Strikes and blasts happen at a point on the ground.
                case SoundCue.LightningStrike: return new SoundCueInfo(0.07f, 3, 0.4f, 0.5f, spatial: true);
                case SoundCue.ArrowRain: return new SoundCueInfo(0.05f, 3, 0.25f, 0.4f, pitchJitter: 0.12f, spatial: true);
                case SoundCue.Explosion: return new SoundCueInfo(0.1f, 3, 0.6f, 0.62f, spatial: true);
                case SoundCue.Summon: return new SoundCueInfo(0.4f, 1, 0.8f, 0.5f, 0.85f, spatial: true);

                // Combat: the horde cues are thinned hardest.
                case SoundCue.Hit: return new SoundCueInfo(0.05f, 3, 0.15f, 0.3f, pitchJitter: 0.12f, volumeJitter: 0.15f, spatial: true);
                case SoundCue.CritHit: return new SoundCueInfo(0.08f, 2, 0.2f, 0.42f, 1.1f, 0.1f, spatial: true);
                case SoundCue.EnemyKill: return new SoundCueInfo(0.06f, 3, 0.2f, 0.4f, pitchJitter: 0.14f, volumeJitter: 0.15f, spatial: true);
                case SoundCue.EliteKill: return new SoundCueInfo(0.15f, 2, 0.5f, 0.75f, 0.9f, spatial: true);
                case SoundCue.BossHit: return new SoundCueInfo(0.12f, 1, 0.3f, 0.55f, 0.8f, spatial: true);
                case SoundCue.BossKill: return new SoundCueInfo(0f, 2, 1.2f, 0.95f, 0.8f, 0.02f, 0f, important: true);
                case SoundCue.HeroHurt: return new SoundCueInfo(0.25f, 1, 0.3f, 0.6f, 0.8f);
                case SoundCue.ShieldBlock: return new SoundCueInfo(0.12f, 2, 0.3f, 0.55f);
                case SoundCue.Parry: return new SoundCueInfo(0.1f, 2, 0.4f, 0.75f, 1.1f);
                case SoundCue.HeroDeath: return new SoundCueInfo(0f, 1, 2.5f, 0.9f, 0.9f, 0f, 0f, important: true);

                // Skills: the hero's own moves, always heard.
                // The Warrior brain raises its shield ~1.2 times per second all run (SurvivorEval), so the shield cue is
                // kept soft and sparse; the real block (ShieldBlock) stays loud.
                case SoundCue.Kick: return new SoundCueInfo(0.05f, 2, 0.3f, 0.5f, 0.95f);
                case SoundCue.ShieldUp: return new SoundCueInfo(0.7f, 1, 0.3f, 0.22f, 1.1f);
                case SoundCue.Dash: return new SoundCueInfo(0.05f, 2, 0.35f, 0.5f, 1.2f);
                case SoundCue.Fireball: return new SoundCueInfo(0.05f, 2, 0.5f, 0.55f);
                case SoundCue.ManaShield: return new SoundCueInfo(0.05f, 2, 0.6f, 0.5f);
                case SoundCue.Blink: return new SoundCueInfo(0.05f, 2, 0.4f, 0.5f);
                case SoundCue.FrostBurst: return new SoundCueInfo(0.05f, 2, 0.6f, 0.6f, 0.9f);
                case SoundCue.PowerShot: return new SoundCueInfo(0.05f, 2, 0.4f, 0.6f, 0.75f);

                // Pickups: gems get a rising pitch (GemCombo) instead of random pitch.
                case SoundCue.Gem: return new SoundCueInfo(0.04f, 4, 0.15f, 0.3f, pitchJitter: 0f, volumeJitter: 0.05f);
                case SoundCue.Coin: return new SoundCueInfo(0.07f, 2, 0.3f, 0.45f);
                case SoundCue.Heal: return new SoundCueInfo(0.3f, 1, 0.6f, 0.5f);
                case SoundCue.Magnet: return new SoundCueInfo(0f, 1, 0.8f, 0.7f, important: true);
                case SoundCue.Chest: return new SoundCueInfo(0f, 2, 0.8f, 0.8f, important: true);
                case SoundCue.LevelUp: return new SoundCueInfo(0f, 2, 0.8f, 0.6f, 1f, 0f, 0f, important: true);
                case SoundCue.CardsShown: return new SoundCueInfo(0f, 1, 0.5f, 0.55f, 1f, 0f, 0f, important: true);
                case SoundCue.CardPick: return new SoundCueInfo(0f, 1, 0.5f, 0.6f, 1f, 0f, 0f, important: true);
                case SoundCue.Evolution: return new SoundCueInfo(0f, 1, 2f, 0.8f, 1f, 0f, 0f, important: true);
                case SoundCue.EliteSpawn: return new SoundCueInfo(0.5f, 1, 1f, 0.55f, 0.7f, spatial: true);
                case SoundCue.BossSpawn: return new SoundCueInfo(0f, 1, 2f, 1f, 0.75f, 0f, 0f, important: true);

                case SoundCue.VictoryJingle:
                case SoundCue.DefeatJingle:
                case SoundCue.EndJingle:
                    return new SoundCueInfo(0f, 1, 3f, 0.85f, 1f, 0f, 0f, important: true);

                case SoundCue.UiClick: return new SoundCueInfo(0.03f, 2, 0.15f, 0.45f, 1f, 0.03f, 0f, important: true);
                case SoundCue.UiCoin: return new SoundCueInfo(0.03f, 2, 0.4f, 0.6f, 1f, 0.03f, 0f, important: true);
                case SoundCue.UiError: return new SoundCueInfo(0.03f, 1, 0.3f, 0.45f, 1f, 0f, 0f, important: true);
                case SoundCue.UiToggle: return new SoundCueInfo(0.03f, 1, 0.2f, 0.5f, 1f, 0f, 0f, important: true);

                case SoundCue.MusicRun:
                case SoundCue.MusicBoss:
                    return new SoundCueInfo(0f, 1, 0f, 1f, 1f, 0f, 0f, important: true, bus: SoundBus.Music);

                default: return new SoundCueInfo(0.1f, 1, 0.3f, 0.5f);
            }
        }

        /// <summary>Number of values in <see cref="SoundCue"/> (array size for per-cue tables).</summary>
        public static readonly int CueCount = Enum.GetValues(typeof(SoundCue)).Length;
    }
}
