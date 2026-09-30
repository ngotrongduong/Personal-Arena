using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    public sealed partial class SurvivorSim
    {
        private int gemCount;

        private void SpawnGem(Vec2 point, float value)
        {
            if (gemCount >= GemCapacity)
            {
                SurvivorPickup nearest = null; float best = float.PositiveInfinity;
                for (int i = 0; i < pickupLimit; i++)
                {
                    if (!pickups[i].Active || pickups[i].Kind != PickupKind.Gem) continue;
                    float distance = (pickups[i].Position - point).LengthSquared;
                    if (distance < best) { best = distance; nearest = pickups[i]; }
                }
                nearest.Value += value; return;
            }
            SpawnPickup(PickupKind.Gem, point, value, false);
        }

        private SurvivorPickup SpawnPickup(PickupKind kind, Vec2 point, float value, bool countDrop)
        {
            SurvivorPickup pickup = null;
            for (int i = 0; i < pickupLimit; i++) if (!pickups[i].Active) { pickup = pickups[i]; break; }
            if (pickup == null && pickupLimit < pickups.Length) pickup = pickups[pickupLimit++];
            if (pickup == null) return null;
            pickup.Active = true; pickup.Kind = kind; pickup.Position = point; pickup.Value = value; pickup.Attracted = false;
            if (kind == PickupKind.Gem) gemCount++;
            if (countDrop && (kind == PickupKind.Gold || kind == PickupKind.Meat)) DropsSpawned++;
            return pickup;
        }

        private void UpdatePickups()
        {
            SurvivorTuning tuning = Config.Tuning;
            float flyStep = tuning.PickupFlySpeed * FixedDeltaTime;
            float collectRadius = Hero.Radius + tuning.CollectMargin;
            float pickupRadiusSquared = stats.PickupRadius * stats.PickupRadius;
            for (int i = 0; i < pickupLimit; i++)
            {
                SurvivorPickup p = pickups[i]; if (!p.Active) continue;
                Vec2 toHero = Hero.Position - p.Position; float distanceSquared = toHero.LengthSquared;
                if (distanceSquared <= pickupRadiusSquared) p.Attracted = true;
                if (p.Attracted && distanceSquared > 1e-6f)
                {
                    float distance = MathF.Sqrt(distanceSquared); p.Position += toHero / distance * MathF.Min(distance, flyStep);
                    toHero = Hero.Position - p.Position; distanceSquared = toHero.LengthSquared;
                }
                if (distanceSquared > collectRadius * collectRadius) continue;
                if (testDisablePickupCollection) continue;
                p.Active = false;
                if (p.Kind == PickupKind.Gem) { gemCount--; CollectXp(p.Value); }
                else if (p.Kind == PickupKind.Gold) { Gold += p.Value; DropsCollected++; AddEvent(SurvivorEventType.GoldCollected, p.Value, point: p.Position); }
                else if (p.Kind == PickupKind.Meat) { DropsCollected++; Heal(tuning.MeatHeal, true); }
            }
        }

        private void CollectXp(float raw)
        {
            float gained = raw * stats.GrowthMul; TotalXp += gained;
            float remaining = gained; float progress = 0f;
            while (remaining > 0f)
            {
                int required = XpCurve.Required(Level);
                float segment = MathF.Min(remaining, required - Xp);
                Xp += segment; remaining -= segment; progress += segment / required;
                if (Xp + 1e-5f >= required)
                {
                    Xp -= required; Level++; pendingLevelUps++; AddEvent(SurvivorEventType.LevelUp, Level);
                }
                else break;
            }
            AddEvent(SurvivorEventType.XpCollected, gained, progress);
        }

        private void OpenOffer()
        {
            int count = 0;
            for (int index = 0; index < SurvivorCatalog.CatalogSize; index++)
            {
                ItemDef def = SurvivorCatalog.Get(index); if (def == null || def.Kind == ItemKind.Filler) continue;
                int level = inventory.Level(index);
                if (level > 0 && level < def.MaxLevel) candidates[count++] = index;
            }
            if (inventory.WeaponCount < SurvivorCatalog.MaxWeapons)
            {
                for (int i = 0; i < Config.ClassDef.WeaponPool.Length; i++) if (inventory.Level(Config.ClassDef.WeaponPool[i]) == 0) candidates[count++] = Config.ClassDef.WeaponPool[i];
            }
            if (inventory.PassiveCount < SurvivorCatalog.MaxPassives)
            {
                for (int i = 0; i < Config.ClassDef.PassivePool.Length; i++) if (inventory.Level(Config.ClassDef.PassivePool[i]) == 0) candidates[count++] = Config.ClassDef.PassivePool[i];
            }
            int wanted = 3 + (rng.NextFloat() < stats.Luck / (100f + stats.Luck) ? 1 : 0);
            if (count == 0)
            {
                offers[0] = SurvivorCatalog.BonusGoldIndex; offerLevels[0] = 0; offers[1] = SurvivorCatalog.BonusHealIndex; offerLevels[1] = 0; OfferCount = 2;
            }
            else
            {
                OfferCount = Math.Min(wanted, count);
                for (int i = 0; i < OfferCount; i++)
                {
                    int chosen = i + rng.NextInt(count - i);
                    int value = candidates[chosen]; candidates[chosen] = candidates[i]; candidates[i] = value;
                    offers[i] = value; offerLevels[i] = inventory.Level(value) + 1;
                }
            }
            AddEvent(SurvivorEventType.OfferShown, OfferCount);
        }

        /// <summary>
        /// Applies a level-up pick. Filler gold is added without a GoldCollected event (a pick gives
        /// no reward); a weapon's cooldown resets only when the weapon is newly acquired.
        /// </summary>
        private void HandlePick(int pick)
        {
            if (pick < 1 || pick > OfferCount) return;
            int index = offers[pick - 1]; int newLevel = offerLevels[pick - 1];
            if (index == SurvivorCatalog.BonusGoldIndex) Gold += Config.Tuning.FillerGold * stats.GreedMul * stats.TierGold;
            else if (index == SurvivorCatalog.BonusHealIndex) Heal(Config.Tuning.FillerHeal, true);
            else
            {
                inventory.Set(index, newLevel); RecomputeStats(true);
                if (newLevel == 1) weaponCooldowns[index] = 0f;
            }
            AddEvent(SurvivorEventType.ItemPicked, newLevel, id: index);
            pendingLevelUps--; OfferCount = 0;
            if (pendingLevelUps > 0) OpenOffer();
        }

        private void Heal(float amount, bool emit)
        {
            float restored = MathF.Min(amount, Hero.MaxHp - Hero.Hp); if (restored <= 0f) return;
            Hero.Hp += restored; if (emit) AddEvent(SurvivorEventType.Healed, restored, point: Hero.Position);
        }
    }
}
