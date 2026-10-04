using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    /// <summary>Melee weapons: arc sweeps (sword, dagger, flame cone), spear/beam thrusts and the three-strike combo blade.</summary>
    public sealed partial class SurvivorSim
    {
        private int comboHitsLeft;
        private float comboTimer;

        private void Sweep(ItemDef def, int level, float damageMul = 1f)
        {
            float range = def.BaseRange * (1f + def.RangePerLevel * (level - 1)) * stats.AreaMul;
            float damage = (def.BaseDamage + def.DamagePerLevel * (level - 1)) * damageMul;
            bool hasBackArc = def.BackArcLevel > 0 && level >= def.BackArcLevel;
            for (int i = 0; i < enemyLimit && !IsEnded; i++)
            {
                SurvivorEnemy enemy = enemies[i]; if (!enemy.Active) continue;
                Vec2 delta = enemy.Position - Hero.Position;
                float reach = range + enemy.Radius;
                if (delta.LengthSquared > reach * reach) continue;
                bool front = InArc(Hero.Facing, delta, def.ArcDegrees);
                bool back = hasBackArc && InArc(Hero.Facing + MathF.PI, delta, def.ArcDegrees);
                if (front || back) DamageEnemy(enemy, damage, def.Knockback, AwayFromHero(enemy));
            }
        }

        private bool ThrustSpears(ItemDef def, int level, out Vec2 aim, out int count)
        {
            aim = Vec2.Zero; count = VolleyCount(def.CountByLevel, level);
            float length = def.BaseRange * stats.AreaMul;
            SurvivorEnemy target = NearestEnemy(length);
            if (target == null) return false;
            aim = (target.Position - Hero.Position).Normalized();
            if (aim.LengthSquared < 1e-8f) aim = Vec2.FromAngle(Hero.Facing);
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            float halfWidth = def.Width * stats.AreaMul;
            for (int spear = 0; spear < count && !IsEnded; spear++)
            {
                Vec2 direction = Rotate(aim, SurvivorCatalog.ThrustAngleOffset(spear, count, Config.Tuning.CenteredEvenVolleys));
                for (int i = 0; i < enemyLimit && !IsEnded; i++)
                {
                    SurvivorEnemy enemy = enemies[i]; if (!enemy.Active) continue;
                    if (CircleIntersectsSegment(enemy.Position, enemy.Radius + halfWidth, Hero.Position, direction, length))
                        DamageEnemy(enemy, damage, def.Knockback, direction);
                }
            }
            return true;
        }

        /// <summary>
        /// Combo blade: when ready, strikes at once, then twice more every <see cref="ItemDef.HitInterval"/> seconds
        /// (a sweep each time, along the facing at that moment); the last strike deals double. The cooldown starts with
        /// the first strike. A swing that has begun finishes even if the weapon is replaced.
        /// </summary>
        private void UpdateCombo(ItemDef def, int level)
        {
            if (comboHitsLeft > 0)
            {
                comboTimer -= FixedDeltaTime;
                if (comboTimer > 1e-6f) return;
                comboHitsLeft--; comboTimer += def.HitInterval;
                bool last = comboHitsLeft == 0;
                Vec2 facing = Vec2.FromAngle(Hero.Facing);
                Sweep(def, level, last ? SurvivorCatalog.ComboFinisherMul : 1f);
                AddEvent(SurvivorEventType.WeaponFired, extra: SurvivorCatalog.ComboHits - comboHitsLeft, id: def.CatalogIndex, point: facing);
                return;
            }
            if (weaponCooldowns[def.CatalogIndex] > 0f) return;
            Vec2 first = Vec2.FromAngle(Hero.Facing);
            Sweep(def, level);
            comboHitsLeft = SurvivorCatalog.ComboHits - 1; comboTimer = def.HitInterval;
            weaponCooldowns[def.CatalogIndex] = def.BaseCooldown * stats.CooldownMul;
            AddEvent(SurvivorEventType.WeaponFired, extra: 1, id: def.CatalogIndex, point: first);
        }
    }
}
