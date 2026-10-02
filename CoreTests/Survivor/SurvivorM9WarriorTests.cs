using System;
using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    /// <summary>M9 wave 1a (T-036): 6 + 6 slots, Warrior weapons 26..30, passives 58..61 and the war-cry skill.</summary>
    public sealed class SurvivorM9WarriorTests
    {
        private static readonly int[] NewWeapons = { 26, 27, 28, 29, 30 };
        private static readonly int[] NewPassives = { 58, 59, 60, 61 };

        // ---- slots and schema ----

        [Test]
        public void Slots_AreSixPlusSix_AndTheSchemaIsV5()
        {
            Assert.That(SurvivorCatalog.MaxWeapons, Is.EqualTo(6)); Assert.That(SurvivorCatalog.MaxPassives, Is.EqualTo(6));
            Assert.That(new SurvivorTuning().MaxWeaponSlots, Is.EqualTo(6)); Assert.That(new SurvivorTuning().MaxPassiveSlots, Is.EqualTo(6));
            Assert.That(SurvivorObservation.Size, Is.EqualTo(2592)); Assert.That(SurvivorObservation.SchemaVersion, Is.EqualTo(5));
            Assert.That(SurvivorCatalog.CatalogSize, Is.EqualTo(128)); Assert.That(SurvivorInput.SkillBranchSize, Is.EqualTo(7));
            Assert.That(SurvivorInput.MoveBranchSize, Is.EqualTo(9)); Assert.That(SurvivorInput.PickBranchSize, Is.EqualTo(5));
        }

        [Test]
        public void Observation_NewItemsFillTheirReservedSlots_ValuesStayInRange()
        {
            SurvivorSim sim = NewSim(1);
            foreach (int index in NewWeapons) sim.GiveItemForTests(index, 3);
            foreach (int index in NewPassives) sim.GiveItemForTests(index, 5);
            float[] values = new float[SurvivorObservation.Size]; new SurvivorObservation().Write(sim, values);
            foreach (int index in NewWeapons) Assert.That(values[SurvivorObservation.InventoryOffset + index], Is.EqualTo(0.6f).Within(1e-6f), "weapon " + index);
            foreach (int index in NewPassives) Assert.That(values[SurvivorObservation.InventoryOffset + index], Is.EqualTo(1f), "passive " + index);
            Assert.That(values[SurvivorObservation.InventoryOffset + 38], Is.Zero); Assert.That(values[SurvivorObservation.InventoryOffset + 62], Is.Zero);
            for (int i = 0; i < values.Length; i++) { Assert.That(float.IsFinite(values[i]), Is.True); Assert.That(values[i], Is.InRange(-1f, 1f)); }
        }

        [Test]
        public void Offers_OfNewItems_AreEncodedInTheirCatalogSlot()
        {
            SurvivorSim sim = NewSim(2); sim.GiveXpForTests(5f); sim.Step(default);
            float[] values = new float[SurvivorObservation.Size]; new SurvivorObservation().Write(sim, values);
            for (int slot = 0; slot < sim.OfferCount; slot++)
            {
                int offset = SurvivorObservation.OffersOffset + slot * SurvivorObservation.OfferStride; Assert.That(values[offset + sim.GetOffer(slot).CatalogIndex], Is.EqualTo(1f)); Assert.That(values[offset + SurvivorCatalog.CatalogSize + 1], Is.EqualTo(1f));
            }
        }

        [Test]
        public void Tuning_Validate_RejectsSlotCapsOutsideOneToSix()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SurvivorTuning { MaxWeaponSlots = 0 }.Validate());
            Assert.Throws<ArgumentOutOfRangeException>(() => new SurvivorTuning { MaxWeaponSlots = 7 }.Validate());
            Assert.Throws<ArgumentOutOfRangeException>(() => new SurvivorTuning { MaxPassiveSlots = 0 }.Validate());
            Assert.Throws<ArgumentOutOfRangeException>(() => new SurvivorTuning { MaxPassiveSlots = 7 }.Validate());
            Assert.DoesNotThrow(() => new SurvivorTuning { MaxWeaponSlots = 4, MaxPassiveSlots = 4 }.Validate());
        }

        [Test]
        public void SixSlotsFill_AndASeventhItemIsNeverOffered()
        {
            SurvivorSim sim = NewSim(3); sim.SetHeroInvulnerableForTests();
            Rng picks = new Rng(7); int fullTicks = 0;
            for (int round = 0; round < 260; round++)
            {
                sim.GiveXpForTests(XpCurve.Required(sim.Level)); sim.Step(default);
                if (!sim.IsAwaitingPick) continue;
                HashSet<int> seen = new HashSet<int>();
                for (int i = 0; i < sim.OfferCount; i++)
                {
                    int index = sim.GetOffer(i).CatalogIndex; Assert.That(seen.Add(index), Is.True, "duplicate in one offer");
                    ItemDef def = SurvivorCatalog.Get(index);
                    if (def.Kind == ItemKind.Filler || sim.Inventory.Level(index) > 0) continue;
                    if (def.Kind == ItemKind.Weapon) Assert.That(sim.Inventory.WeaponCount, Is.LessThan(6), "new weapon offered with full slots");
                    if (def.Kind == ItemKind.Passive) Assert.That(sim.Inventory.PassiveCount, Is.LessThan(6), "new passive offered with full slots");
                }
                Assert.That(sim.OfferCount, Is.InRange(1, 4));
                sim.Step(new SurvivorInput(0, 0, 1 + picks.NextInt(sim.OfferCount)));
                Assert.That(sim.Inventory.WeaponCount, Is.LessThanOrEqualTo(6)); Assert.That(sim.Inventory.PassiveCount, Is.LessThanOrEqualTo(6));
                if (sim.Inventory.WeaponCount == 6 && sim.Inventory.PassiveCount == 6) fullTicks++;
            }
            Assert.That(sim.Inventory.WeaponCount, Is.EqualTo(6)); Assert.That(sim.Inventory.PassiveCount, Is.EqualTo(6)); Assert.That(fullTicks, Is.GreaterThan(0));
        }

        [Test]
        public void Offers_NeverExceedThePool_WhenItIsSmall()
        {
            SurvivorConfig config = SurvivorTestHelpers.Config(); config.ClassDef.WeaponPool = new[] { 0, 26 }; config.ClassDef.PassivePool = new[] { 58 };
            for (int seed = 0; seed < 20; seed++)
            {
                SurvivorSim sim = new SurvivorSim(config, seed); sim.GiveXpForTests(5f); sim.Step(default);
                Assert.That(sim.OfferCount, Is.InRange(1, 3));
                for (int i = 0; i < sim.OfferCount; i++) Assert.That(sim.GetOffer(i).CatalogIndex, Is.AnyOf(0, 26, 58));
            }
        }

        [Test]
        public void Offers_EveryNewItemCanAppear_ForTheWarrior()
        {
            HashSet<int> seen = new HashSet<int>();
            for (int seed = 0; seed < 400; seed++)
            {
                SurvivorSim sim = NewSim(seed); sim.GiveXpForTests(5f); sim.Step(default);
                for (int i = 0; i < sim.OfferCount; i++) seen.Add(sim.GetOffer(i).CatalogIndex);
            }
            foreach (int index in NewWeapons) Assert.That(seen.Contains(index), Is.True, "weapon " + index);
            foreach (int index in NewPassives) Assert.That(seen.Contains(index), Is.True, "passive " + index);
        }

        [Test]
        public void MageAndArcher_GetTheNewPassives_ButNotTheWarriorWeapons()
        {
            foreach (SurvivorClassDef kit in new[] { SurvivorDefaults.Mage(), SurvivorDefaults.Archer(), SurvivorDefaults.Warrior() })
                foreach (int passive in NewPassives) Assert.That(Array.IndexOf(kit.PassivePool, passive) >= 0, Is.True, kit.Id + " " + passive);
            foreach (SurvivorClassDef kit in new[] { SurvivorDefaults.Mage(), SurvivorDefaults.Archer() })
                foreach (int weapon in NewWeapons) Assert.That(Array.IndexOf(kit.WeaponPool, weapon), Is.LessThan(0), kit.Id + " " + weapon);
        }

        [Test]
        public void Chest_StillEvolvesWithSixFullSlots()
        {
            SurvivorSim sim = NewSim(4);
            foreach (int index in new[] { 1, 2, 3, 26, 27 }) sim.GiveItemForTests(index, 2);
            foreach (int index in new[] { 7, 9, 10, 58, 59 }) sim.GiveItemForTests(index, 2);
            sim.GiveItemForTests(0, 5); sim.GiveItemForTests(SurvivorCatalog.MightGauntletIndex, 1);
            Assert.That(sim.Inventory.WeaponCount, Is.EqualTo(6)); Assert.That(sim.Inventory.PassiveCount, Is.EqualTo(6));
            sim.SpawnPickupForTests(PickupKind.Chest, sim.Hero.Position, 0f);
            bool evolved = false;
            for (int tick = 0; tick < 30 && !evolved; tick++)
            {
                sim.Step(sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1) : default);
                for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.WeaponEvolved) { evolved = true; Assert.That(sim.Events[i].Id, Is.EqualTo(40)); }
            }
            Assert.That(evolved, Is.True); Assert.That(sim.Inventory.WeaponCount, Is.EqualTo(6)); Assert.That(sim.Inventory.Level(0), Is.Zero);
        }

        [Test]
        public void Chest_RaisesANewWeaponLikeAnyOther()
        {
            SurvivorConfig config = NoCrit(); config.ClassDef.StartingWeapon = SurvivorCatalog.RetaliateIndex; config.ClassDef.WeaponPool = new[] { 30 }; config.ClassDef.PassivePool = Array.Empty<int>();
            SurvivorSim sim = new SurvivorSim(config, 5); sim.SpawnPickupForTests(PickupKind.Chest, sim.Hero.Position, 0f);
            SurvivorTestHelpers.Step(sim, 10);
            Assert.That(sim.Inventory.Level(30), Is.EqualTo(2));
        }

        [Test]
        public void NewWeapons_DoNotAddASecondOrbitAuraOrShockwave()
        {
            foreach (int index in NewWeapons)
            {
                WeaponPattern pattern = SurvivorCatalog.Get(index).Pattern;
                Assert.That(pattern, Is.Not.AnyOf(WeaponPattern.Orbit, WeaponPattern.Aura, WeaponPattern.Shockwave), "weapon " + index);
            }
            int combo = 0, retaliate = 0;
            foreach (int index in SurvivorDefaults.Warrior().WeaponPool) { WeaponPattern p = SurvivorCatalog.Get(index).Pattern; if (p == WeaponPattern.Combo) combo++; if (p == WeaponPattern.Retaliate) retaliate++; }
            Assert.That(combo, Is.EqualTo(1)); Assert.That(retaliate, Is.EqualTo(1));
        }

        [Test]
        public void Catalog_NewRowsMatchTheTaskTable()
        {
            ItemDef flame = SurvivorCatalog.Get(26), combo = SurvivorCatalog.Get(27), hammer = SurvivorCatalog.Get(28), bomb = SurvivorCatalog.Get(29), retaliate = SurvivorCatalog.Get(30);
            Assert.That(flame.Id, Is.EqualTo("flame-cone")); Assert.That(flame.BaseDamage, Is.EqualTo(6f)); Assert.That(flame.DamagePerLevel, Is.EqualTo(2f)); Assert.That(flame.BaseRange, Is.EqualTo(3.2f)); Assert.That(flame.BaseCooldown, Is.EqualTo(0.35f)); Assert.That(flame.ArcDegrees, Is.EqualTo(60f)); Assert.That(flame.Knockback, Is.Zero);
            Assert.That(combo.Id, Is.EqualTo("combo-blade")); Assert.That(combo.BaseDamage, Is.EqualTo(14f)); Assert.That(combo.DamagePerLevel, Is.EqualTo(5f)); Assert.That(combo.BaseRange, Is.EqualTo(2.6f)); Assert.That(combo.BaseCooldown, Is.EqualTo(1.6f)); Assert.That(combo.ArcDegrees, Is.EqualTo(90f));
            Assert.That(hammer.Id, Is.EqualTo("heavy-hammer")); Assert.That(hammer.BaseDamage, Is.EqualTo(60f)); Assert.That(hammer.DamagePerLevel, Is.EqualTo(15f)); Assert.That(hammer.BaseRange, Is.EqualTo(8f)); Assert.That(hammer.ProjectileRadius, Is.EqualTo(0.8f)); Assert.That(hammer.ProjectileSpeed, Is.EqualTo(8f)); Assert.That(hammer.BaseCooldown, Is.EqualTo(2.6f)); Assert.That(hammer.Pierce, Is.EqualTo(3)); Assert.That(hammer.CountByLevel, Is.EqualTo(new[] { 1, 1, 1, 2, 2 }));
            Assert.That(bomb.Id, Is.EqualTo("bomb")); Assert.That(bomb.BaseDamage, Is.EqualTo(30f)); Assert.That(bomb.DamagePerLevel, Is.EqualTo(9f)); Assert.That(bomb.BaseCooldown, Is.EqualTo(2f)); Assert.That(bomb.Width, Is.EqualTo(2f));
            Assert.That(retaliate.Id, Is.EqualTo("retaliate")); Assert.That(retaliate.BaseDamage, Is.EqualTo(20f)); Assert.That(retaliate.DamagePerLevel, Is.EqualTo(8f)); Assert.That(retaliate.BaseRange, Is.EqualTo(2.5f)); Assert.That(retaliate.BaseCooldown, Is.EqualTo(1f));
            foreach (int index in NewWeapons) { ItemDef def = SurvivorCatalog.Get(index); Assert.That(def.Kind, Is.EqualTo(ItemKind.Weapon)); Assert.That(def.MaxLevel, Is.EqualTo(5)); Assert.That(def.CatalogIndex, Is.EqualTo(index)); Assert.That(SurvivorCatalog.EvolutionOf(index), Is.EqualTo(-1)); Assert.That(def.Name, Is.Not.Empty); }
            string[] ids = { "recovery", "clover", "greed", "crown" };
            for (int i = 0; i < 4; i++) { ItemDef def = SurvivorCatalog.Get(58 + i); Assert.That(def.Id, Is.EqualTo(ids[i])); Assert.That(def.Kind, Is.EqualTo(ItemKind.Passive)); Assert.That(def.MaxLevel, Is.EqualTo(5)); Assert.That(def.PerLevel, Is.GreaterThan(0f)); }
            Assert.That(SurvivorCatalog.Get(62).Kind, Is.EqualTo(ItemKind.Filler)); Assert.That(SurvivorCatalog.Get(63).Kind, Is.EqualTo(ItemKind.Filler));
        }

        // ---- flame cone ----

        [Test]
        public void FlameCone_HitsOnlyTheNarrowConeInRange_WithoutKnockback()
        {
            SurvivorSim sim = WeaponSim(26, 1, 10);
            SurvivorEnemy front = Brute(sim, 2.5f, 0f), side = Brute(sim, 0f, 3f), past = Brute(sim, 4.6f, 0.5f), edge = Brute(sim, 2.2f, 1.8f);
            sim.Step(default);
            Assert.That(front.MaxHp - front.Hp, Is.EqualTo(6f).Within(1e-3f)); Assert.That(front.Position.X, Is.EqualTo(2.5f).Within(1e-3f), "no knockback");
            Assert.That(side.Hp, Is.EqualTo(side.MaxHp), "90 degrees off the axis"); Assert.That(past.Hp, Is.EqualTo(past.MaxHp), "beyond 3.2 m + radius");
            Assert.That(edge.Hp, Is.EqualTo(edge.MaxHp), "39 degrees off the axis: outside the 60 degree cone");
            Assert.That(sim.WeaponCooldownForTests(26), Is.EqualTo(0.35f).Within(1e-5f));
        }

        [TestCase(1, 6f)]
        [TestCase(3, 10f)]
        [TestCase(5, 14f)]
        public void FlameCone_DamageGrowsByTwoPerLevel(int level, float damage)
        {
            SurvivorSim sim = WeaponSim(26, level, 11); SurvivorEnemy front = Brute(sim, 2.5f, 0f); sim.Step(default);
            Assert.That(front.MaxHp - front.Hp, Is.EqualTo(damage).Within(1e-3f));
        }

        [Test]
        public void FlameCone_FiresEveryThirtyFifthOfASecond()
        {
            SurvivorSim sim = WeaponSim(26, 1, 12); SurvivorEnemy dummy = Brute(sim, 2.5f, 0f); dummy.MaxHp = 1000f; dummy.Hp = 1000f; int fired = 0;
            for (int tick = 0; tick < 60; tick++) { sim.Step(default); for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.WeaponFired && sim.Events[i].Id == 26) fired++; }
            Assert.That(fired, Is.InRange(2, 3), "cooldown 0.35 s means about 2.86 shots a second");
            Assert.That(sim.Time, Is.EqualTo(1f).Within(1e-3f));
        }

        // ---- combo blade ----

        [Test]
        public void ComboBlade_StrikesThreeTimes_TheThirdForDouble()
        {
            SurvivorSim sim = WeaponSim(27, 1, 20); SurvivorEnemy target = Brute(sim, 1.5f, 0f);
            List<int> hitTicks = new List<int>(); List<float> damages = new List<float>(); List<int> extras = new List<int>(); float last = target.Hp;
            for (int tick = 1; tick <= 45; tick++)
            {
                sim.Step(default);
                for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.WeaponFired && sim.Events[i].Id == 27) { hitTicks.Add(tick); extras.Add((int)sim.Events[i].Extra); damages.Add(last - target.Hp); last = target.Hp; }
            }
            Assert.That(extras, Is.EqualTo(new[] { 1, 2, 3 })); Assert.That(damages[0], Is.EqualTo(14f).Within(1e-3f)); Assert.That(damages[1], Is.EqualTo(14f).Within(1e-3f)); Assert.That(damages[2], Is.EqualTo(28f).Within(1e-3f));
            Assert.That(hitTicks[1] - hitTicks[0], Is.InRange(14, 16), "0.25 s apart"); Assert.That(hitTicks[2] - hitTicks[1], Is.InRange(14, 16));
        }

        [Test]
        public void ComboBlade_RepeatsAfterItsCooldown_AndScalesPerLevel()
        {
            SurvivorSim sim = WeaponSim(27, 3, 21); SurvivorEnemy target = Brute(sim, 1.5f, 0f); target.MaxHp = 10000f; target.Hp = 10000f;
            int swings = 0; float firstHit = 0f;
            for (int tick = 0; tick < 180; tick++)
            {
                float before = target.Hp; sim.Step(default);
                for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.WeaponFired && sim.Events[i].Id == 27 && sim.Events[i].Extra == 1f) { swings++; if (swings == 1) firstHit = before - target.Hp; }
            }
            Assert.That(swings, Is.EqualTo(2), "1.6 s cooldown: swings at 0 s and 1.6 s within 3 s"); Assert.That(firstHit, Is.EqualTo(24f).Within(1e-3f), "14 + 5 * 2 at level 3");
        }

        [Test]
        public void ComboBlade_HitsOnlyTheNinetyDegreeArcInRange()
        {
            SurvivorSim sim = WeaponSim(27, 1, 22); SurvivorEnemy front = Brute(sim, 2f, 0f), side = Brute(sim, 0f, 2.2f), far = Brute(sim, 4f, 0f);
            sim.Step(default);
            Assert.That(front.Hp, Is.LessThan(front.MaxHp)); Assert.That(side.Hp, Is.EqualTo(side.MaxHp)); Assert.That(far.Hp, Is.EqualTo(far.MaxHp));
        }

        // ---- heavy hammer ----

        [Test]
        public void HeavyHammer_PiercesThreeExtraEnemies_AndIsSlowAndHuge()
        {
            SurvivorSim sim = WeaponSim(28, 1, 30);
            SurvivorEnemy[] line = { Brute(sim, 2.5f, 0f), Brute(sim, 4f, 0f), Brute(sim, 5.5f, 0f), Brute(sim, 7f, 0f), Brute(sim, 7.8f, 0f) };
            sim.Step(default);
            SurvivorProjectile shot = null; for (int i = 0; i < sim.Projectiles.Count; i++) if (sim.Projectiles[i].Active && sim.Projectiles[i].SourceIndex == 28) shot = sim.Projectiles[i];
            Assert.That(shot, Is.Not.Null); Assert.That(shot.Velocity.Length, Is.EqualTo(8f).Within(1e-3f)); Assert.That(shot.Radius, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(sim.WeaponCooldownForTests(28), Is.EqualTo(2.6f).Within(1e-5f));
            SurvivorTestHelpers.Step(sim, 120);
            int hit = 0; foreach (SurvivorEnemy e in line) if (e.Hp < e.MaxHp) { hit++; Assert.That(e.MaxHp - e.Hp, Is.EqualTo(60f).Within(1e-3f)); }
            Assert.That(hit, Is.EqualTo(4), "pierce 3 = the first enemy plus three more"); Assert.That(line[4].Hp, Is.EqualTo(line[4].MaxHp));
        }

        [TestCase(1, 1, 60f)]
        [TestCase(3, 1, 90f)]
        [TestCase(4, 2, 105f)]
        [TestCase(5, 2, 120f)]
        public void HeavyHammer_CountAndDamageByLevel(int level, int count, float damage)
        {
            SurvivorSim sim = WeaponSim(28, level, 31); Brute(sim, 3f, 0f); Brute(sim, -3f, 0f); Brute(sim, 0f, 4f); sim.Step(default);
            int shots = 0; float dealt = 0f;
            for (int i = 0; i < sim.Projectiles.Count; i++) if (sim.Projectiles[i].Active && sim.Projectiles[i].SourceIndex == 28) { shots++; dealt = sim.Projectiles[i].Damage; }
            Assert.That(shots, Is.EqualTo(count)); Assert.That(dealt, Is.EqualTo(damage).Within(1e-3f));
        }

        // ---- bomb ----

        [Test]
        public void Bomb_ExplodesOnItsFirstHit_DamagingTheAreaForFullDamage()
        {
            SurvivorSim sim = WeaponSim(29, 1, 40);
            SurvivorEnemy target = Brute(sim, 6f, 0f), beside = Brute(sim, 6f, 1.2f), far = Brute(sim, 6f, 6f);
            sim.Step(default);
            Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 29).Id, Is.EqualTo(29));
            Assert.That(sim.WeaponCooldownForTests(29), Is.EqualTo(2f).Within(1e-5f));
            SurvivorEvent blast = default; bool found = false;
            for (int tick = 0; tick < 90 && !found; tick++) { sim.Step(default); for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.StrikeLanded && sim.Events[i].Id == 29) { blast = sim.Events[i]; found = true; } }
            Assert.That(found, Is.True); Assert.That(blast.Value, Is.EqualTo(2f).Within(1e-5f)); Assert.That(blast.Point.X, Is.LessThan(6f), "it explodes when it touches the first enemy");
            Assert.That(target.MaxHp - target.Hp, Is.EqualTo(30f).Within(1e-3f)); Assert.That(beside.MaxHp - beside.Hp, Is.EqualTo(30f).Within(1e-3f)); Assert.That(far.Hp, Is.EqualTo(far.MaxHp));
            for (int i = 0; i < sim.Projectiles.Count; i++) Assert.That(sim.Projectiles[i].Active && sim.Projectiles[i].SourceIndex == 29, Is.False, "the bomb is spent");
        }

        [Test]
        public void Bomb_ExplodesAtMaxRange_WhenItHitNothing()
        {
            SurvivorSim sim = WeaponSim(29, 1, 41);
            SurvivorEnemy first = Brute(sim, 3f, 0f), bystander = Brute(sim, 9f, 1.5f);
            sim.Step(default); sim.DamageEnemyForTests(first, first.MaxHp + 1f);
            SurvivorEvent blast = default; bool found = false;
            for (int tick = 0; tick < 90 && !found; tick++) { sim.Step(default); for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.StrikeLanded && sim.Events[i].Id == 29) { blast = sim.Events[i]; found = true; } }
            Assert.That(found, Is.True, "the bomb blows up at the end of its flight"); Assert.That(blast.Point.X, Is.EqualTo(9f).Within(0.3f));
            Assert.That(bystander.MaxHp - bystander.Hp, Is.EqualTo(30f).Within(1e-3f));
        }

        [TestCase(1, 30f)]
        [TestCase(3, 48f)]
        [TestCase(5, 66f)]
        public void Bomb_DamageGrowsByNinePerLevel(int level, float damage)
        {
            SurvivorSim sim = WeaponSim(29, level, 42); SurvivorEnemy target = Brute(sim, 4f, 0f); target.MaxHp = 500f; target.Hp = 500f;
            for (int tick = 0; tick < 60 && target.Hp >= 500f; tick++) sim.Step(default);
            Assert.That(500f - target.Hp, Is.EqualTo(damage).Within(1e-3f));
        }

        [Test]
        public void Bomb_DoesNotFireWithoutATargetInRange()
        {
            SurvivorSim sim = WeaponSim(29, 1, 43); Brute(sim, 14f, 0f); sim.Step(default);
            Assert.That(sim.WeaponCooldownForTests(29), Is.Zero);
            for (int i = 0; i < sim.Projectiles.Count; i++) Assert.That(sim.Projectiles[i].Active, Is.False);
        }

        // ---- retaliate ----

        [Test]
        public void Retaliate_BlastsARingWhenTheHeroTakesDamage_ThenWaitsOneSecond()
        {
            SurvivorSim sim = WeaponSim(30, 1, 50); SurvivorEnemy source = Brute(sim, 30f, 0f);
            SurvivorEnemy near = Brute(sim, 2f, 0f), behind = Brute(sim, -2.2f, 0.5f), outside = Brute(sim, 4f, 0f);
            sim.Step(default); Assert.That(near.Hp, Is.EqualTo(near.MaxHp), "no damage taken, no retaliation");
            sim.DamageHeroForTests(10f, source); sim.Step(default);
            Assert.That(near.MaxHp - near.Hp, Is.EqualTo(20f).Within(1e-3f)); Assert.That(behind.MaxHp - behind.Hp, Is.EqualTo(20f).Within(1e-3f)); Assert.That(outside.Hp, Is.EqualTo(outside.MaxHp), "outside 2.5 m");
            SurvivorEvent ring = Find(sim.Events, SurvivorEventType.StrikeLanded, 30); Assert.That(ring.Value, Is.EqualTo(2.5f).Within(1e-5f));
            Assert.That(sim.WeaponCooldownForTests(30), Is.EqualTo(1f).Within(1e-5f));
            sim.DamageHeroForTests(10f, source); sim.Step(default);
            Assert.That(near.MaxHp - near.Hp, Is.EqualTo(20f).Within(1e-3f), "internal cooldown: no second ring yet");
            SurvivorTestHelpers.Step(sim, 60);
            sim.DamageHeroForTests(10f, source); sim.Step(default);
            Assert.That(near.MaxHp - near.Hp, Is.EqualTo(40f).Within(1e-3f), "ready again after 1 s");
        }

        [TestCase(1, 20f)]
        [TestCase(3, 36f)]
        [TestCase(5, 52f)]
        public void Retaliate_DamageGrowsByEightPerLevel(int level, float damage)
        {
            SurvivorSim sim = WeaponSim(30, level, 51); SurvivorEnemy source = Brute(sim, 30f, 0f); SurvivorEnemy near = Brute(sim, 2f, 0f);
            sim.DamageHeroForTests(10f, source); sim.Step(default);
            Assert.That(near.MaxHp - near.Hp, Is.EqualTo(damage).Within(1e-3f));
        }

        [Test]
        public void Retaliate_NeedsTheWeapon_AndNeverFiresOnAKillingBlow()
        {
            SurvivorConfig plain = NoCrit(); SurvivorSim without = new SurvivorSim(plain, 52); SurvivorEnemy source = Brute(without, 30f, 0f); SurvivorEnemy near = Brute(without, 2f, 0f);
            without.SetWeaponCooldownForTests(0, 1000f); without.DamageHeroForTests(10f, source); without.Step(default);
            Assert.That(near.Hp, Is.EqualTo(near.MaxHp));

            SurvivorSim dying = WeaponSim(30, 1, 53); SurvivorEnemy killer = Brute(dying, 30f, 0f); SurvivorEnemy bystander = Brute(dying, 2f, 0f);
            dying.SetHeroHpForTests(5f); dying.DamageHeroForTests(50f, killer); dying.Step(default);
            Assert.That(dying.EndReason, Is.EqualTo(EndReason.Died)); Assert.That(bystander.Hp, Is.EqualTo(bystander.MaxHp));
        }

        // ---- passives ----

        [TestCase(1)]
        [TestCase(5)]
        public void Passives_ChangeTheirOwnStat(int level)
        {
            SurvivorSim baseline = NewSim(60);
            SurvivorSim recovery = NewSim(60); recovery.GiveItemForTests(58, level);
            SurvivorSim clover = NewSim(60); clover.GiveItemForTests(59, level);
            SurvivorSim greed = NewSim(60); greed.GiveItemForTests(60, level);
            SurvivorSim crown = NewSim(60); crown.GiveItemForTests(61, level);
            SurvivorDerivedStats b = baseline.DerivedStats;
            Assert.That(recovery.DerivedStats.Regen, Is.EqualTo(b.Regen + 0.2f * level).Within(1e-5f));
            Assert.That(clover.DerivedStats.Luck, Is.EqualTo(b.Luck + 5f * level).Within(1e-5f));
            Assert.That(greed.DerivedStats.GreedMul, Is.EqualTo(b.GreedMul + 0.1f * level).Within(1e-5f));
            Assert.That(crown.DerivedStats.GrowthMul, Is.EqualTo(b.GrowthMul + 0.08f * level).Within(1e-5f));
            Assert.That(recovery.DerivedStats.Luck, Is.EqualTo(b.Luck)); Assert.That(clover.DerivedStats.Regen, Is.EqualTo(b.Regen));
            Assert.That(greed.DerivedStats.GrowthMul, Is.EqualTo(b.GrowthMul)); Assert.That(crown.DerivedStats.GreedMul, Is.EqualTo(b.GreedMul));
            Assert.That(crown.DerivedStats.MaxHp, Is.EqualTo(b.MaxHp)); Assert.That(clover.DerivedStats.MoveSpeed, Is.EqualTo(b.MoveSpeed));
        }

        [Test]
        public void Passives_StackWithOwnerStatPoints()
        {
            SurvivorConfig config = NoCrit(); config.Build.Points[(int)StatId.Greed] = 4; config.Build.Points[(int)StatId.Regen] = 3; config.Build.Points[(int)StatId.Growth] = 2; config.Build.Points[(int)StatId.Luck] = 1;
            SurvivorSim sim = new SurvivorSim(config, 61);
            foreach (int index in NewPassives) sim.GiveItemForTests(index, 2);
            Assert.That(sim.DerivedStats.GreedMul, Is.EqualTo(1f + 0.05f * 4 + 0.2f).Within(1e-5f)); Assert.That(sim.DerivedStats.Regen, Is.EqualTo(0.2f + 0.1f * 3 + 0.4f).Within(1e-5f));
            Assert.That(sim.DerivedStats.GrowthMul, Is.EqualTo(1f + 0.03f * 2 + 0.16f).Within(1e-5f)); Assert.That(sim.DerivedStats.Luck, Is.EqualTo(5f + 10f).Within(1e-5f));
        }

        [Test]
        public void Recovery_HealsFasterOverTime()
        {
            SurvivorSim plain = NewSim(62), healer = NewSim(62); healer.GiveItemForTests(58, 5);
            foreach (SurvivorSim sim in new[] { plain, healer }) { sim.SetHeroInvulnerableForTests(); sim.SetHeroHpForTests(50f); sim.SetWeaponCooldownForTests(0, 1000f); }
            for (int tick = 0; tick < 600; tick++) { plain.Step(default); healer.Step(default); }
            Assert.That(healer.Hero.Hp - plain.Hero.Hp, Is.EqualTo(1f * 10f).Within(0.5f), "+1 HP/s over 10 s");
        }

        [Test]
        public void Crown_RaisesExperienceGained_ByEightPercentPerLevel()
        {
            SurvivorSim plain = NewSim(63), crowned = NewSim(63); crowned.GiveItemForTests(61, 5);
            plain.GiveXpForTests(10f); crowned.GiveXpForTests(10f);
            Assert.That(plain.TotalXp, Is.EqualTo(10f).Within(1e-4f)); Assert.That(crowned.TotalXp, Is.EqualTo(14f).Within(1e-3f));
        }

        [Test]
        public void Greed_RaisesGoldFromFillersAndChests()
        {
            SurvivorSim plain = NewSim(64), greedy = NewSim(64); greedy.GiveItemForTests(60, 5);
            foreach (SurvivorSim sim in new[] { plain, greedy }) { sim.SpawnPickupForTests(PickupKind.Chest, sim.Hero.Position, 0f); SurvivorTestHelpers.Step(sim, 5); }
            Assert.That(plain.Gold, Is.GreaterThan(0f)); Assert.That(greedy.Gold, Is.EqualTo(plain.Gold * 1.5f).Within(0.01f), "same chest roll, 1.5x gold");
        }

        [Test]
        public void Clover_OffersFourChoicesMoreOften()
        {
            int plainFour = 0, cloverFour = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                SurvivorSim plain = NewSim(seed); plain.GiveXpForTests(5f); plain.Step(default); if (plain.OfferCount == 4) plainFour++;
                SurvivorSim lucky = NewSim(seed); lucky.GiveItemForTests(59, 5); lucky.GiveXpForTests(5f); lucky.Step(default); if (lucky.OfferCount == 4) cloverFour++;
            }
            Assert.That(plainFour, Is.Zero); Assert.That(cloverFour, Is.InRange(15, 70), "luck 25 gives about 20 percent");
        }

        // ---- war cry ----

        [Test]
        public void WarCry_IsTheWarriorsFourthSkill_WithTheTaskNumbers()
        {
            SkillDef cry = SurvivorDefaults.Warrior().ActiveSkills[3];
            Assert.That(cry.Id, Is.EqualTo("war-cry")); Assert.That(cry.Kind, Is.EqualTo(SkillKind.AreaBurst)); Assert.That(cry.AreaRadius, Is.EqualTo(3.5f)); Assert.That(cry.Damage, Is.EqualTo(15f));
            Assert.That(cry.StunSeconds, Is.EqualTo(0.8f)); Assert.That(cry.Knockback, Is.EqualTo(2f)); Assert.That(cry.Cooldown, Is.EqualTo(10f)); Assert.That(cry.EnergyCost, Is.EqualTo(20f));
            Assert.That(SurvivorDefaults.Archer().ActiveSkills[3].Kind, Is.EqualTo(SkillKind.None)); Assert.That(SurvivorDefaults.Mage().ActiveSkills[3].Id, Is.EqualTo("frost-burst"));
        }

        [Test]
        public void WarCry_DamagesStunsAndPushesEnemiesAroundTheHero()
        {
            SurvivorSim sim = NewSim(70); sim.SetWeaponCooldownForTests(0, 1000f);
            SurvivorEnemy front = sim.SpawnEnemyForTests(2, new Vec2(2.5f, 0f)), back = sim.SpawnEnemyForTests(2, new Vec2(-2f, 1f)), outside = sim.SpawnEnemyForTests(2, new Vec2(5f, 0f));
            float energy = sim.Hero.Energy;
            sim.Step(new SurvivorInput(0, 4, 0));
            Assert.That(sim.LastSkill, Is.EqualTo(4)); Assert.That(sim.SkillUses[3], Is.EqualTo(1)); Assert.That(sim.Hero.SkillCooldowns[3], Is.EqualTo(10f));
            SurvivorEvent burst = Find(sim.Events, SurvivorEventType.StrikeLanded, -1); Assert.That(burst.Value, Is.EqualTo(3.5f).Within(1e-5f));
            Assert.That(Find(sim.Events, SurvivorEventType.SkillUsed).Extra, Is.EqualTo(2f));
            foreach (SurvivorEnemy e in new[] { front, back }) { Assert.That(e.MaxHp - e.Hp, Is.EqualTo(15f).Within(1e-3f)); Assert.That(e.StunRemaining, Is.InRange(0.7f, 0.8f)); }
            Assert.That(front.Position.X, Is.GreaterThan(2.5f + 0.3f), "knocked away"); Assert.That(outside.Hp, Is.EqualTo(outside.MaxHp)); Assert.That(outside.StunRemaining, Is.Zero);
            Assert.That(sim.Hero.Energy, Is.EqualTo(energy - 20f).Within(1f));
        }

        [Test]
        public void WarCry_ActionMask_ReadyThenCooldownThenEnergy_AndTheObservationShowsIt()
        {
            SurvivorSim sim = NewSim(71); sim.SetWeaponCooldownForTests(0, 1000f);
            bool[] move = new bool[SurvivorInput.MoveBranchSize], skill = new bool[SurvivorInput.SkillBranchSize], pick = new bool[SurvivorInput.PickBranchSize]; SurvivorActionMask.WriteMask(sim, move, skill, pick);
            Assert.That(skill[4], Is.True);
            float[] values = new float[SurvivorObservation.Size]; SurvivorObservation observation = new SurvivorObservation(); observation.Write(sim, values);
            Assert.That(values[10], Is.EqualTo(1f), "skill_allowed_3"); Assert.That(values[6], Is.Zero, "skill_cooldown_3");
            sim.Step(new SurvivorInput(0, 4, 0)); SurvivorActionMask.WriteMask(sim, move, skill, pick); Assert.That(skill[4], Is.False, "on cooldown");
            observation.Write(sim, values); Assert.That(values[10], Is.Zero); Assert.That(values[6], Is.GreaterThan(0.9f)); Assert.That(values[57 + 4], Is.EqualTo(1f), "last skill one-hot");
            for (int tick = 0; tick < 620; tick++) sim.Step(default);
            sim.Hero.Energy = 10f; SurvivorActionMask.WriteMask(sim, move, skill, pick); Assert.That(skill[4], Is.False, "not enough energy");
            sim.Hero.Energy = 20f; SurvivorActionMask.WriteMask(sim, move, skill, pick); Assert.That(skill[4], Is.True);
            sim.Step(new SurvivorInput(0, 4, 0)); Assert.That(sim.SkillUses[3], Is.EqualTo(2));
        }

        // ---- determinism and allocation ----

        [Test]
        public void NewContent_RunsDeterministically_WithSixPlusSixSlotsFull()
        {
            Assert.That(RunHash(81), Is.EqualTo(RunHash(81))); Assert.That(RunHash(81), Is.Not.EqualTo(RunHash(82)));
        }

        [Test]
        public void NewContent_StepAndObservationAllocateNothing()
        {
            SurvivorSim sim = FullKitSim(83); SurvivorObservation observation = new SurvivorObservation(); float[] values = new float[SurvivorObservation.Size];
            for (int i = 0; i < 600; i++) { Poke(sim, i); sim.Step(new SurvivorInput(i / 20 % 9, i / 7 % 5, 0)); observation.Write(sim, values); }
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 600; i < 1200; i++) { Poke(sim, i); sim.Step(new SurvivorInput(i / 20 % 9, i / 7 % 5, 0)); }
            long stepAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) observation.Write(sim, values);
            long writeAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(sim.IsEnded, Is.False); Assert.That(stepAllocated, Is.EqualTo(0L), "Step"); Assert.That(writeAllocated, Is.EqualTo(0L), "Write");
        }

        [Test]
        public void NewContent_EveryNewWeaponActuallyFiresInTheFullKitRun()
        {
            SurvivorSim sim = FullKitSim(84); HashSet<int> fired = new HashSet<int>(); bool cry = false;
            for (int i = 0; i < 1800; i++)
            {
                Poke(sim, i); sim.Step(new SurvivorInput(i / 20 % 9, i / 7 % 5, 0));
                for (int e = 0; e < sim.Events.Count; e++)
                {
                    if (sim.Events[e].Type == SurvivorEventType.WeaponFired) fired.Add(sim.Events[e].Id);
                    if (sim.Events[e].Type == SurvivorEventType.SkillUsed && sim.Events[e].Value == 3f) cry = true;
                }
            }
            foreach (int index in NewWeapons) Assert.That(fired.Contains(index), Is.True, "weapon " + index);
            Assert.That(cry, Is.True);
        }

        // ---- helpers ----

        private static SurvivorConfig NoCrit() { SurvivorConfig config = SurvivorTestHelpers.Config(); config.ClassDef.CritChance = 0f; return config; }
        private static SurvivorSim NewSim(int seed) => new SurvivorSim(NoCrit(), seed);

        private static SurvivorSim WeaponSim(int item, int level, int seed)
        {
            SurvivorConfig config = NoCrit(); config.ClassDef.StartingWeapon = item;
            SurvivorSim sim = new SurvivorSim(config, seed); if (level != 1) sim.GiveItemForTests(item, level); return sim;
        }

        /// <summary>A stunned brute: tough enough to survive a few hits and harmless to the hero.</summary>
        private static SurvivorEnemy Brute(SurvivorSim sim, float x, float y)
        {
            SurvivorEnemy enemy = sim.SpawnEnemyForTests(2, new Vec2(x, y)); enemy.StunRemaining = 1000f; return enemy;
        }

        private static SurvivorSim FullKitSim(int seed)
        {
            SurvivorSim sim = NewSim(seed); sim.SetHeroInvulnerableForTests(); sim.SetEnemiesInvulnerableForTests(); sim.DisablePickupCollectionForTests();
            foreach (int index in NewWeapons) sim.GiveItemForTests(index, 5);
            foreach (int index in NewPassives) sim.GiveItemForTests(index, 5);
            sim.GiveItemForTests(6, 3); sim.GiveItemForTests(9, 3);
            for (int i = 0; i < 30; i++) sim.SpawnEnemyForTests(i % 4, Vec2.FromAngle(i * 0.7f) * (2f + i % 7));
            return sim;
        }

        /// <summary>Keeps the hero hurt now and then so the retaliate weapon triggers; the hero stays invulnerable, so the hit is applied directly.</summary>
        private static void Poke(SurvivorSim sim, int tick) { if (tick % 25 == 0) sim.RequestRetaliateForTests(); }

        private static long RunHash(int seed)
        {
            SurvivorSim sim = new SurvivorSim(NoCrit(), seed); long hash = 17;
            foreach (int index in NewWeapons) sim.GiveItemForTests(index, 4);
            foreach (int index in NewPassives) sim.GiveItemForTests(index, 3);
            sim.GiveItemForTests(6, 2); sim.GiveItemForTests(7, 2);
            Assert.That(sim.Inventory.WeaponCount, Is.EqualTo(6)); Assert.That(sim.Inventory.PassiveCount, Is.EqualTo(6));
            for (int tick = 0; tick < 3600 && !sim.IsEnded; tick++)
            {
                SurvivorInput input = sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1 + tick % 3) : new SurvivorInput((tick / 40) % 9, (tick / 90) % 5, 0);
                sim.Step(input); hash = hash * 31 + SurvivorM5GoldenTests.StateHash(sim);
            }
            Assert.That(sim.Kills, Is.GreaterThan(0)); return hash;
        }

        private static SurvivorEvent Find(IReadOnlyList<SurvivorEvent> events, SurvivorEventType type, int id = int.MinValue)
        {
            for (int i = 0; i < events.Count; i++) if (events[i].Type == type && (id == int.MinValue || events[i].Id == id)) return events[i];
            Assert.Fail("Missing event " + type); return default;
        }
    }
}
