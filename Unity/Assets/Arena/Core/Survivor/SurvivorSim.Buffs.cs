using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    /// <summary>Short buffs given by dropped power-ups (M11).</summary>
    public enum BuffKind { Rage, Shield, Haste }

    /// <summary>
    /// M11 bonus drops: mana potion, bomb and three short buffs. They roll on their own random stream, so the
    /// sequence of every older roll (spawns, crits, gold, meat, magnet) is unchanged.
    /// </summary>
    public sealed partial class SurvivorSim
    {
        public const float ManaRestoreFraction = 0.5f;
        public const float BombRadius = 7f;
        public const float BombBaseDamage = 50f;
        public const float BombDamagePerMinute = 30f;
        public const float RageSeconds = 10f;
        public const float RageDamageMul = 1.5f;
        public const float ShieldSeconds = 8f;
        public const float ShieldDamageTakenMul = 0.4f;
        public const float HasteSeconds = 8f;
        public const float HasteSpeedMul = 1.35f;
        private const float BonusDropAngle = 5.1f;

        private Rng dropRng;

        public float RageRemaining { get; private set; }
        public float ShieldRemaining { get; private set; }
        public float HasteRemaining { get; private set; }

        private void ResetBuffs(int seed)
        {
            dropRng = new Rng(seed ^ 0x4D313144);
            RageRemaining = 0f; ShieldRemaining = 0f; HasteRemaining = 0f;
        }

        private void TickBuffs()
        {
            RageRemaining = MathF.Max(0f, RageRemaining - FixedDeltaTime);
            ShieldRemaining = MathF.Max(0f, ShieldRemaining - FixedDeltaTime);
            HasteRemaining = MathF.Max(0f, HasteRemaining - FixedDeltaTime);
        }

        /// <summary>One roll per normal kill; at most one bonus item drops.</summary>
        private void RollBonusDrop(SurvivorEnemy enemy)
        {
            float roll = dropRng.NextFloat();
            float mana = Config.Tuning.ManaChance, powerUp = Config.Tuning.PowerUpChance;
            PickupKind kind;
            if (roll < mana) kind = PickupKind.Mana;
            else if (roll < mana + powerUp) kind = PickupKind.Bomb;
            else if (roll < mana + powerUp * 2f) kind = PickupKind.Rage;
            else if (roll < mana + powerUp * 3f) kind = PickupKind.Shield;
            else if (roll < mana + powerUp * 4f) kind = PickupKind.Haste;
            else return;
            SpawnPickup(kind, enemy.Position + Vec2.FromAngle(BonusDropAngle) * Config.Tuning.DropOffset, 0f, false);
        }

        /// <summary>Applies a collected bonus item; returns false for any other pickup kind.</summary>
        private bool CollectBonus(SurvivorPickup pickup)
        {
            switch (pickup.Kind)
            {
                case PickupKind.Mana:
                {
                    float before = Hero.Energy;
                    Hero.Energy = MathF.Min(Hero.MaxEnergy, Hero.Energy + Hero.MaxEnergy * ManaRestoreFraction);
                    AddEvent(SurvivorEventType.ManaRestored, Hero.Energy - before, point: Hero.Position);
                    return true;
                }
                case PickupKind.Bomb:
                    AddEvent(SurvivorEventType.BombExploded, BombRadius, point: Hero.Position);
                    Blast(Hero.Position, BombRadius, BombBaseDamage + BombDamagePerMinute * (Time / 60f), 4f, 0.5f);
                    return true;
                case PickupKind.Rage:
                    RageRemaining = RageSeconds; AddEvent(SurvivorEventType.BuffStarted, RageSeconds, id: (int)BuffKind.Rage, point: Hero.Position);
                    return true;
                case PickupKind.Shield:
                    ShieldRemaining = ShieldSeconds; AddEvent(SurvivorEventType.BuffStarted, ShieldSeconds, id: (int)BuffKind.Shield, point: Hero.Position);
                    return true;
                case PickupKind.Haste:
                    HasteRemaining = HasteSeconds; AddEvent(SurvivorEventType.BuffStarted, HasteSeconds, id: (int)BuffKind.Haste, point: Hero.Position);
                    return true;
                default:
                    return false;
            }
        }

        internal void SetHeroEnergyForTests(float energy) { Hero.Energy = MathF.Min(Hero.MaxEnergy, energy); }
        internal void DamageHeroForTests(float raw) { ApplyHeroDamage(raw, -1); }
    }
}
