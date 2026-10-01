namespace PersonalArena.View
{
    /// <summary>
    /// Every sound the survivor viewer can play. <see cref="SoundCueMap"/> turns sim events into cues,
    /// <see cref="SoundCueInfo"/> holds each cue's mixing rules and the sound set holds its clips.
    /// New cues go at the end (the sound set asset stores them as numbers).
    /// </summary>
    public enum SoundCue
    {
        None = 0,

        // Weapons (WeaponFired, by base weapon).
        SwordSwing,
        SpearThrust,
        AxeWhirl,
        HammerThrow,
        Shockwave,
        MagicBolt,
        FireOrb,
        FrostNova,
        ArcaneBeam,
        ArrowShot,
        MultiShot,
        KnifeWhirl,
        DaggerSlash,
        CrossbowShot,

        // Strikes and blasts (StrikeLanded, EnemyExploded, EnemySummoned).
        LightningStrike,
        ArrowRain,
        Explosion,
        Summon,

        // Combat.
        Hit,
        CritHit,
        EnemyKill,
        EliteKill,
        BossHit,
        BossKill,
        HeroHurt,
        ShieldBlock,
        Parry,
        HeroDeath,

        // Hero skills (SkillUsed, by skill).
        Kick,
        ShieldUp,
        Dash,
        Fireball,
        ManaShield,
        Blink,
        FrostBurst,
        PowerShot,

        // Pickups and progress.
        Gem,
        Coin,
        Heal,
        Magnet,
        Chest,
        LevelUp,
        CardsShown,
        CardPick,
        Evolution,
        EliteSpawn,
        BossSpawn,

        // Run end jingles (one per run).
        VictoryJingle,
        DefeatJingle,
        EndJingle,

        // Menus and buttons.
        UiClick,
        UiCoin,
        UiError,
        UiToggle,

        // Music starts (counted in the audio log; played on the music sources, not the effect pool).
        MusicRun,
        MusicBoss
    }
}
