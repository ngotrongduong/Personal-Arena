using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    /// <summary>Defensive weapons that react to the hero being hit: barrier charges, the retaliate ring and the spiked-armor reflect.</summary>
    public sealed partial class SurvivorSim
    {
        private bool retaliatePending;
        private float reflectPending;
        private float lastHeroDamage;
        private bool barrierInit;
        private int barrierCharges;
        private int barrierMax;

        /// <summary>Barrier hits still absorbable right now (0 while recharging or not owned).</summary>
        public int BarrierCharges => OwnedWeaponWithPattern(WeaponPattern.Barrier) >= 0 && barrierInit ? barrierCharges : 0;

        // ---- barrier ----

        /// <summary>Charges track the level (a level-up adds the difference while the shield is up); a broken shield returns when its timer ends.</summary>
        private void SyncBarrier(ItemDef def, int level)
        {
            int max = Math.Min(CountAtLevel(def.CountByLevel, level), SurvivorCatalog.MaxVolleyCount);
            if (!barrierInit) { barrierInit = true; barrierMax = max; barrierCharges = max; return; }
            if (max > barrierMax) { if (barrierCharges > 0) barrierCharges += max - barrierMax; barrierMax = max; }
        }

        private void UpdateBarrier(ItemDef def, int level)
        {
            SyncBarrier(def, level);
            if (barrierCharges > 0 || weaponCooldowns[def.CatalogIndex] > 0f) return;
            barrierCharges = barrierMax;
            AddEvent(SurvivorEventType.WeaponFired, extra: barrierCharges, id: def.CatalogIndex, point: Hero.Position);
        }

        /// <summary>
        /// Called for a hit that would hurt the hero (after a parry, before block and armor). A standing barrier eats it: no damage,
        /// no HeroDamaged, no retaliate, no reflect. The last charge breaks the shield and starts its recharge timer.
        /// </summary>
        private bool TryAbsorbHit()
        {
            int index = OwnedWeaponWithPattern(WeaponPattern.Barrier);
            if (index < 0) return false;
            ItemDef def = SurvivorCatalog.Get(index); int level = inventory.Level(index);
            SyncBarrier(def, level);
            if (barrierCharges <= 0) return false;
            barrierCharges--;
            if (barrierCharges == 0)
            {
                weaponCooldowns[index] = (def.BaseCooldown + def.CooldownPerLevel * (level - 1)) * stats.CooldownMul;
                AddEvent(SurvivorEventType.WeaponFired, extra: 0f, id: index, point: Hero.Position);
            }
            return true;
        }

        // ---- retaliate ----

        /// <summary>
        /// Retaliate: when the hero lost HP this tick, blasts a ring around the hero (radius BaseRange × area) and starts
        /// its internal cooldown (BaseCooldown). Resolved after the enemy phase so no enemy dies mid-update.
        /// </summary>
        private void ResolveRetaliate()
        {
            if (!retaliatePending) return;
            retaliatePending = false;
            int index = OwnedWeaponWithPattern(WeaponPattern.Retaliate);
            if (index < 0 || weaponCooldowns[index] > 0f || !Hero.Alive) return;
            ItemDef def = SurvivorCatalog.Get(index);
            float radius = def.BaseRange * stats.AreaMul;
            float damage = def.BaseDamage + def.DamagePerLevel * (inventory.Level(index) - 1);
            weaponCooldowns[index] = def.BaseCooldown * stats.CooldownMul;
            AddEvent(SurvivorEventType.WeaponFired, id: index, point: Hero.Position);
            AddEvent(SurvivorEventType.StrikeLanded, radius, id: index, point: Hero.Position);
            Blast(Hero.Position, radius, damage, def.Knockback, 0f);
        }

        // ---- spiked armor ----

        /// <summary>Contact hits taken this tick were summed in reflectPending (a share of the damage actually taken); every enemy touching the hero takes it, flat.</summary>
        private void ResolveReflect()
        {
            if (reflectPending <= 0f) return;
            float damage = reflectPending; reflectPending = 0f;
            if (!Hero.Alive) return;
            float margin = Config.Tuning.ContactMargin;
            for (int i = 0; i < enemyLimit && !IsEnded; i++)
            {
                SurvivorEnemy e = enemies[i]; if (!e.Active) continue;
                float reach = Hero.Radius + e.Radius + margin;
                if ((e.Position - Hero.Position).LengthSquared <= reach * reach) DamageEnemy(e, damage, 0f, AwayFromHero(e), true);
            }
        }
    }
}
