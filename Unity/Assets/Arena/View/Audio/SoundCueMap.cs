using System;
using System.Collections.Generic;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.View
{
    /// <summary>One sound wanted by a sim event: the cue and where it happened (world point, or the hero).</summary>
    public readonly struct SoundRequest
    {
        public readonly SoundCue Cue;
        public readonly Vec2 Point;
        public readonly bool AtHero;

        public SoundRequest(SoundCue cue, Vec2 point, bool atHero)
        {
            Cue = cue;
            Point = point;
            AtHero = atHero;
        }
    }

    /// <summary>
    /// Maps survivor sim events to sound cues. Pure: reads the event, the catalog (<see cref="SurvivorCatalog"/>)
    /// and the hero's class kit (<see cref="SurvivorClassDef.ActiveSkills"/>); never changes the sim.
    /// Run-end jingles are chosen by <see cref="JingleFor"/> once per run, not from events.
    /// </summary>
    public static class SoundCueMap
    {
        /// <summary>The cue of one event, or <see cref="SoundCue.None"/> for silent events.</summary>
        public static SoundCue Map(in SurvivorEvent survivorEvent, SurvivorClassDef kit)
        {
            switch (survivorEvent.Type)
            {
                case SurvivorEventType.WeaponFired: return WeaponCue(survivorEvent.Id);
                case SurvivorEventType.StrikeLanded: return StrikeCue(survivorEvent.Id, kit);
                case SurvivorEventType.SkillUsed: return SkillCue(Skill(kit, (int)survivorEvent.Value));
                case SurvivorEventType.DamageDealt: return SoundCue.Hit;
                case SurvivorEventType.Crit: return SoundCue.CritHit;
                case SurvivorEventType.EnemyKilled: return SoundCue.EnemyKill;
                case SurvivorEventType.EliteKilled: return SoundCue.EliteKill;
                case SurvivorEventType.BossDamaged: return SoundCue.BossHit;
                case SurvivorEventType.BossKilled: return SoundCue.BossKill;
                case SurvivorEventType.HeroDamaged: return SoundCue.HeroHurt;
                case SurvivorEventType.Blocked: return SoundCue.ShieldBlock;
                case SurvivorEventType.Parry: return SoundCue.Parry;
                case SurvivorEventType.HeroDied: return SoundCue.HeroDeath;
                case SurvivorEventType.EnemyExploded: return SoundCue.Explosion;
                case SurvivorEventType.EnemySummoned: return SoundCue.Summon;
                case SurvivorEventType.XpCollected: return SoundCue.Gem;
                case SurvivorEventType.GoldCollected: return SoundCue.Coin;
                case SurvivorEventType.Healed: return SoundCue.Heal;
                case SurvivorEventType.MagnetPicked: return SoundCue.Magnet;
                case SurvivorEventType.ChestOpened: return SoundCue.Chest;
                case SurvivorEventType.LevelUp: return SoundCue.LevelUp;
                case SurvivorEventType.ItemPicked: return SoundCue.CardPick;
                case SurvivorEventType.WeaponEvolved: return SoundCue.Evolution;
                case SurvivorEventType.EliteSpawned: return SoundCue.EliteSpawn;
                case SurvivorEventType.BossSpawned: return SoundCue.BossSpawn;
                // OfferShown: the cards cue plays when the viewer shows them. Spawns, failed skills and the run
                // end events stay silent (the jingle comes from JingleFor).
                default: return SoundCue.None;
            }
        }

        /// <summary>
        /// Maps all events of one step into <paramref name="output"/> (cleared first), skipping silent ones.
        /// A Mage frost burst emits StrikeLanded (id −1) right before its SkillUsed; that blast is dropped so the
        /// burst is heard once (as <see cref="SoundCue.FrostBurst"/>), not also as a fireball explosion.
        /// </summary>
        public static void MapStep(IReadOnlyList<SurvivorEvent> events, SurvivorClassDef kit, List<SoundRequest> output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            output.Clear();
            if (events == null)
            {
                return;
            }

            int pendingBurstBlast = -1;
            for (int i = 0; i < events.Count; i++)
            {
                SurvivorEvent survivorEvent = events[i];
                if (survivorEvent.Type == SurvivorEventType.SkillUsed &&
                    Skill(kit, (int)survivorEvent.Value)?.Kind == SkillKind.AreaBurst && pendingBurstBlast >= 0)
                {
                    output.RemoveAt(pendingBurstBlast);
                    pendingBurstBlast = -1;
                }

                SoundCue cue = Map(survivorEvent, kit);
                if (cue == SoundCue.None)
                {
                    continue;
                }

                if (survivorEvent.Type == SurvivorEventType.StrikeLanded && survivorEvent.Id == -1)
                {
                    pendingBurstBlast = output.Count;
                }

                bool atHero = !HasWorldPoint(survivorEvent);
                output.Add(new SoundRequest(cue, atHero ? default : survivorEvent.Point, atHero));
            }
        }

        /// <summary>The jingle at the end of a run: victory when won, a defeat sting for death or expiry, else the end jingle.</summary>
        public static SoundCue JingleFor(EndReason reason)
        {
            switch (reason)
            {
                case EndReason.Won: return SoundCue.VictoryJingle;
                case EndReason.Died:
                case EndReason.Expired:
                    return SoundCue.DefeatJingle;
                case EndReason.TimeUp: return SoundCue.EndJingle;
                default: return SoundCue.None;
            }
        }

        /// <summary>
        /// True when the event's Point is a world position. Weapon, skill, xp, magnet, level and offer events carry a
        /// direction or nothing, so they sound at the hero; a default point (boss gold) also means "at the hero".
        /// </summary>
        public static bool HasWorldPoint(in SurvivorEvent survivorEvent)
        {
            if (survivorEvent.Point.X == 0f && survivorEvent.Point.Y == 0f)
            {
                return false;
            }

            switch (survivorEvent.Type)
            {
                case SurvivorEventType.DamageDealt:
                case SurvivorEventType.Crit:
                case SurvivorEventType.EnemyKilled:
                case SurvivorEventType.EliteKilled:
                case SurvivorEventType.BossKilled:
                case SurvivorEventType.BossDamaged:
                case SurvivorEventType.GoldCollected:
                case SurvivorEventType.ChestOpened:
                case SurvivorEventType.StrikeLanded:
                case SurvivorEventType.EnemyExploded:
                case SurvivorEventType.EnemySummoned:
                case SurvivorEventType.EnemySpawned:
                case SurvivorEventType.EliteSpawned:
                case SurvivorEventType.BossSpawned:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The cue of a weapon volley, by its base weapon (evolutions sound like their base). Auras are silent.</summary>
        public static SoundCue WeaponCue(int catalogIndex)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            if (def == null)
            {
                return SoundCue.None;
            }

            int baseIndex = def.EvolvesFrom >= 0 ? def.EvolvesFrom : catalogIndex;
            switch (baseIndex)
            {
                case SurvivorCatalog.SweepIndex: return SoundCue.SwordSwing;
                case SurvivorCatalog.SpearThrustIndex: return SoundCue.SpearThrust;
                case SurvivorCatalog.OrbitAxeIndex: return SoundCue.AxeWhirl;
                case SurvivorCatalog.ThrownHammerIndex: return SoundCue.HammerThrow;
                case SurvivorCatalog.ShockwaveIndex: return SoundCue.Shockwave;
                case SurvivorCatalog.MagicBoltIndex: return SoundCue.MagicBolt;
                case SurvivorCatalog.FireOrbIndex: return SoundCue.FireOrb;
                case SurvivorCatalog.FrostNovaIndex: return SoundCue.FrostNova;
                case SurvivorCatalog.ArcaneBeamIndex: return SoundCue.ArcaneBeam;
                case SurvivorCatalog.ArrowIndex: return SoundCue.ArrowShot;
                case SurvivorCatalog.MultiShotIndex: return SoundCue.MultiShot;
                case SurvivorCatalog.OrbitKnifeIndex: return SoundCue.KnifeWhirl;
                case SurvivorCatalog.DaggerIndex: return SoundCue.DaggerSlash;
                case SurvivorCatalog.CrossbowIndex: return SoundCue.CrossbowShot;
                // Lightning and arrow rain sound where they land (StrikeLanded); auras pulse silently.
                case SurvivorCatalog.LightningIndex:
                case SurvivorCatalog.ArrowRainIndex:
                case SurvivorCatalog.AuraIndex:
                case SurvivorCatalog.HolyFieldIndex:
                    return SoundCue.None;
            }

            ItemDef baseDef = SurvivorCatalog.Get(baseIndex) ?? def;
            switch (baseDef.Pattern)
            {
                case WeaponPattern.Sweep: return SoundCue.SwordSwing;
                case WeaponPattern.Thrust: return SoundCue.SpearThrust;
                case WeaponPattern.Orbit: return SoundCue.AxeWhirl;
                case WeaponPattern.Thrown: return SoundCue.MagicBolt;
                case WeaponPattern.Shockwave: return SoundCue.Shockwave;
                case WeaponPattern.Fan: return SoundCue.MultiShot;
                default: return SoundCue.None;
            }
        }

        /// <summary>
        /// A strike or blast on the ground: id ≥ 0 is a weapon (lightning or arrow rain, evolutions included);
        /// id −1−slot is a skill blast (exploding skill projectile, or the area burst of slot 0..3).
        /// </summary>
        public static SoundCue StrikeCue(int id, SurvivorClassDef kit)
        {
            if (id >= 0)
            {
                ItemDef def = SurvivorCatalog.Get(id);
                int baseIndex = def != null && def.EvolvesFrom >= 0 ? def.EvolvesFrom : id;
                return baseIndex == SurvivorCatalog.ArrowRainIndex ? SoundCue.ArrowRain : SoundCue.LightningStrike;
            }

            SkillDef skill = Skill(kit, -1 - id);
            return skill != null && skill.Kind == SkillKind.AreaBurst ? SoundCue.FrostBurst : SoundCue.Explosion;
        }

        /// <summary>The cue of a hero skill: by its id for the known skills, else by its kind.</summary>
        public static SoundCue SkillCue(SkillDef skill)
        {
            if (skill == null)
            {
                return SoundCue.None;
            }

            switch (skill.Id)
            {
                case "fireball": return SoundCue.Fireball;
                case "mana-shield": return SoundCue.ManaShield;
                case "blink": return SoundCue.Blink;
                case "frost-burst": return SoundCue.FrostBurst;
                case "power-shot": return SoundCue.PowerShot;
            }

            switch (skill.Kind)
            {
                case SkillKind.Kick: return SoundCue.Kick;
                case SkillKind.Block: return SoundCue.ShieldUp;
                case SkillKind.Dash: return SoundCue.Dash;
                case SkillKind.Projectile: return SoundCue.PowerShot;
                case SkillKind.AreaBurst: return SoundCue.FrostBurst;
                case SkillKind.Teleport: return SoundCue.Blink;
                case SkillKind.MeleeStrike: return SoundCue.SwordSwing;
                default: return SoundCue.None;
            }
        }

        private static SkillDef Skill(SurvivorClassDef kit, int slot)
        {
            SkillDef[] skills = kit?.ActiveSkills;
            return skills != null && slot >= 0 && slot < skills.Length ? skills[slot] : null;
        }
    }
}
