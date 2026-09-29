using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    public sealed partial class SurvivorSim
    {
        private void SpawnGem(Vec2 point, float value)
        {
            int gems = 0;
            for (int i = 0; i < pickupLimit; i++) if (pickups[i].Active && pickups[i].Kind == PickupKind.Gem) gems++;
            if (gems >= GemCapacity)
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
            for (int i = 0; i < pickupLimit; i++)
            {
                if (pickups[i].Active) continue;
                pickups[i].Active = true; pickups[i].Kind = kind; pickups[i].Position = point;
                pickups[i].Value = value; pickups[i].Attracted = false;
                if (countDrop && (kind == PickupKind.Gold || kind == PickupKind.Meat)) DropsSpawned++;
                return pickups[i];
            }
            if (pickupLimit < pickups.Length)
            {
                SurvivorPickup pickup = pickups[pickupLimit++]; pickup.Active = true; pickup.Kind = kind; pickup.Position = point; pickup.Value = value; pickup.Attracted = false;
                if (countDrop && (kind == PickupKind.Gold || kind == PickupKind.Meat)) DropsSpawned++;
                return pickup;
            }
            return null;
        }

        private void UpdatePickups()
        {
            for (int i = 0; i < pickupLimit; i++)
            {
                SurvivorPickup p = pickups[i]; if (!p.Active) continue;
                Vec2 toHero = Hero.Position - p.Position; float distanceSquared = toHero.LengthSquared;
                if (distanceSquared <= stats.PickupRadius * stats.PickupRadius) p.Attracted = true;
                if (p.Attracted && distanceSquared > 1e-6f)
                {
                    float distance = MathF.Sqrt(distanceSquared); p.Position += toHero / distance * MathF.Min(distance, 12f * FixedDeltaTime);
                    toHero = Hero.Position - p.Position; distanceSquared = toHero.LengthSquared;
                }
                float collectRadius = Hero.Radius + 0.3f;
                if (distanceSquared > collectRadius * collectRadius) continue;
                if (testDisablePickupCollection) continue;
                p.Active = false;
                if (p.Kind == PickupKind.Gem) CollectXp(p.Value);
                else if (p.Kind == PickupKind.Gold) { Gold += p.Value; DropsCollected++; AddEvent(SurvivorEventType.GoldCollected, p.Value, point: p.Position); }
                else if (p.Kind == PickupKind.Meat) { DropsCollected++; Heal(30f, true); }
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
                offers[0] = 62; offerLevels[0] = 0; offers[1] = 63; offerLevels[1] = 0; OfferCount = 2;
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

        private void HandlePick(int pick)
        {
            if (pick < 1 || pick > OfferCount) return;
            int index = offers[pick - 1]; int newLevel = offerLevels[pick - 1];
            if (index == 62) { float gold = 25f * stats.GreedMul * stats.TierGold; Gold += gold; AddEvent(SurvivorEventType.GoldCollected, gold); }
            else if (index == 63) Heal(30f, true);
            else { inventory.Set(index, newLevel); RecomputeStats(true); weaponCooldowns[index] = 0f; }
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
