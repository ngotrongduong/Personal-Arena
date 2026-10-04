using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>Kind of scenery model used to draw a survivor obstacle.</summary>
    public enum ObstacleKind
    {
        Grave,
        Tree,
        Rock
    }

    /// <summary>How the renderer draws a weapon when it fires (evolutions use their base weapon's visual).</summary>
    public enum WeaponVisual
    {
        None,
        /// <summary>Sword sweep crescent (0).</summary>
        Sweep,
        /// <summary>Spear streaks (1).</summary>
        Spear,
        /// <summary>Orbiting axes (2).</summary>
        OrbitAxe,
        /// <summary>Thrown hammer projectiles (3).</summary>
        Hammer,
        /// <summary>Golden aura disc (4).</summary>
        Aura,
        /// <summary>Expanding ring (5).</summary>
        Shockwave,
        MagicBolt,
        FireOrb,
        FrostNova,
        HolyField,
        Lightning,
        ArcaneBeam,
        Arrow,
        MultiShot,
        ArrowRain,
        OrbitKnife,
        Dagger,
        Crossbow,
        FlameCone,
        Combo,
        Bomb,
        Retaliate,
        Barrier,
        Boomerang,
        Zone,
        Bounce,
        Momentum,
        Freeze,
        Purge,
        BombRing,
        FireballNova,
        Trio,
        Quad,
        Stone
    }

    /// <summary>Model of a hero projectile.</summary>
    public enum ProjectileLook
    {
        Hammer,
        MagicBolt,
        Arrow,
        Fireball,
        PowerShot,
        Bomb,
        Bounce,
        Momentum,
        SwordWave
    }
}
