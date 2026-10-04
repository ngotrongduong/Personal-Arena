using System;
using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    /// <summary>M9 (T-045): evolutions 112..127 of the new weapons (26-33, 64-66, 68-72); purge (67) has none.</summary>
    public sealed class SurvivorM9EvolutionTests
    {
        private static readonly object[][] Rows =
        {
            new object[] { 112, 26, 58, "inferno", "Inferno" },
            new object[] { 113, 27, 12, "phantom-blade", "Phantom Blade" },
            new object[] { 114, 28, 7, "mountain-hammer", "Mountain Hammer" },
            new object[] { 115, 29, 35, "carpet-bomb", "Bombardment" },
            new object[] { 116, 30, 36, "fury", "Wrath" },
            new object[] { 117, 31, 6, "aegis", "Eternal Barrier" },
            new object[] { 118, 32, 10, "storm-boomerang", "Storm Boomerang" },
            new object[] { 119, 33, 34, "miasma", "Toxic Swamp" },
            new object[] { 120, 64, 59, "chaos-shot", "Chaos Shot" },
            new object[] { 121, 65, 37, "wraith", "Speed Phantom" },
            new object[] { 122, 66, 10, "eternal-corridor", "Eternal Corridor" },
            new object[] { 123, 68, 11, "nebula", "Nebula" },
            new object[] { 124, 69, 8, "meteor", "Meteor" },
            new object[] { 125, 70, 9, "twin-bracelet", "Twin Bracer" },
            new object[] { 126, 71, 13, "four-winds", "Four Winds" },
            new object[] { 127, 72, 60, "sage-stone", "Philosopher's Stone" }
        };

        private static readonly int[] OldBases = { 0, 1, 2, 3, 4, 5, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25 };

        // ---- catalog ----

        [TestCaseSource(nameof(Rows))]
        public void Catalog_RowMatchesTheTaskTable(int evolution, int baseWeapon, int passive, string id, string name)
        {
            ItemDef e = SurvivorCatalog.Get(evolution), b = SurvivorCatalog.Get(baseWeapon);
            Assert.That(e, Is.Not.Null); Assert.That(e.CatalogIndex, Is.EqualTo(evolution)); Assert.That(e.Id, Is.EqualTo(id)); Assert.That(e.Name, Is.EqualTo(name));
            Assert.That(e.Kind, Is.EqualTo(ItemKind.Weapon)); Assert.That(e.MaxLevel, Is.EqualTo(1)); Assert.That(e.Pattern, Is.EqualTo(b.Pattern));
            Assert.That(e.EvolvesFrom, Is.EqualTo(baseWeapon)); Assert.That(e.EvolutionPassive, Is.EqualTo(passive)); Assert.That(SurvivorCatalog.Get(passive).Kind, Is.EqualTo(ItemKind.Passive));
            Assert.That(SurvivorCatalog.EvolutionOf(baseWeapon), Is.EqualTo(evolution)); Assert.That(SurvivorCatalog.EvolutionOf(evolution), Is.EqualTo(-1), "evolutions do not evolve again");
            Assert.That(evolution, Is.InRange(SurvivorCatalog.FirstNewEvolutionIndex, SurvivorCatalog.FirstNewEvolutionIndex + SurvivorCatalog.NewEvolutionCount - 1));
        }

        [Test]
        public void Catalog_RangesAreDisjoint_OldRangeIsUnchanged_PurgeHasNone()
        {
            Assert.That(SurvivorCatalog.FirstEvolutionIndex, Is.EqualTo(40)); Assert.That(SurvivorCatalog.EvolutionCount, Is.EqualTo(18));
            Assert.That(SurvivorCatalog.FirstNewEvolutionIndex, Is.EqualTo(112)); Assert.That(SurvivorCatalog.NewEvolutionCount, Is.EqualTo(16));
            Assert.That(Rows.Length, Is.EqualTo(SurvivorCatalog.NewEvolutionCount));
            HashSet<int> used = new HashSet<int>(), ids = new HashSet<int>(); HashSet<string> names = new HashSet<string>(), codes = new HashSet<string>();
            for (int i = 0; i < SurvivorCatalog.EvolutionCount; i++) { int index = 40 + i; Assert.That(used.Add(index), Is.True); Assert.That(SurvivorCatalog.Get(index).EvolvesFrom, Is.InRange(0, 25)); Assert.That(SurvivorCatalog.Get(index).EvolvesFrom, Is.EqualTo(OldBases[i])); }
            foreach (object[] row in Rows) { Assert.That(used.Add((int)row[0]), Is.True, "overlap " + row[0]); Assert.That(ids.Add((int)row[1]), Is.True, "one evolution per weapon " + row[1]); Assert.That(names.Add((string)row[4]), Is.True); Assert.That(codes.Add((string)row[3]), Is.True); }
            for (int index = 0; index < SurvivorCatalog.CatalogSize; index++) { ItemDef def = SurvivorCatalog.Get(index); if (def != null) Assert.That(codes.Contains(def.Id) && index < 112, Is.False, "id reused by " + index); }
            Assert.That(SurvivorCatalog.EvolutionOf(SurvivorCatalog.PurgeIndex), Is.EqualTo(-1));
            Assert.That(SurvivorCatalog.EvolutionOf(0), Is.EqualTo(40)); Assert.That(SurvivorCatalog.EvolutionOf(25), Is.EqualTo(57)); Assert.That(SurvivorCatalog.Get(40).Id, Is.EqualTo("storm-blade")); Assert.That(SurvivorCatalog.Get(57).Id, Is.EqualTo("siege-crossbow"));
            Assert.That(SurvivorCatalog.Get(58).Kind, Is.EqualTo(ItemKind.Passive)); Assert.That(SurvivorCatalog.Get(111), Is.Null); Assert.That(SurvivorCatalog.Get(128), Is.Null); Assert.That(SurvivorCatalog.Get(-1), Is.Null);
            foreach (int passive in new[] { 6, 7, 8, 9, 10, 11, 12, 13, 34, 35, 36, 37, 58, 59, 60, 61 }) Assert.That(SurvivorCatalog.EvolutionOf(passive), Is.EqualTo(-1));
        }

        [TestCaseSource(nameof(Rows))]
        public void Catalog_GenericScale_DamageRangeCooldown(int evolution, int baseWeapon, int passive, string id, string name)
        {
            ItemDef e = SurvivorCatalog.Get(evolution), b = SurvivorCatalog.Get(baseWeapon);
            Assert.That(e.BaseDamage, Is.EqualTo((b.BaseDamage + b.DamagePerLevel * 4f) * 1.5f).Within(1e-3f)); Assert.That(e.DamagePerLevel, Is.Zero);
            Assert.That(e.BaseCooldown, Is.EqualTo((b.BaseCooldown + b.CooldownPerLevel * 4f) * 0.8f).Within(1e-3f)); Assert.That(e.CooldownPerLevel, Is.Zero);
            float rangeMul = baseWeapon == 30 ? 1.5f : 1.2f;
            Assert.That(e.BaseRange, Is.EqualTo(b.BaseRange * rangeMul).Within(1e-3f));
            Assert.That(e.ProjectileRange, Is.EqualTo(b.ProjectileRange * 1.2f).Within(1e-3f));
            if (b.CountByLevel.Count > 0) Assert.That(e.CountByLevel, Is.EqualTo(new[] { b.CountByLevel[Math.Min(4, b.CountByLevel.Count - 1)] + (baseWeapon == 68 ? 2 : 1) }));
            else Assert.That(e.CountByLevel, Is.Empty);
        }

        [Test]
        public void Catalog_PatternSpecificNumbers()
        {
            ItemDef barrier = SurvivorCatalog.Get(117), retaliate = SurvivorCatalog.Get(116), boomerang = SurvivorCatalog.Get(118), pool = SurvivorCatalog.Get(119), bounce = SurvivorCatalog.Get(120);
            ItemDef momentum = SurvivorCatalog.Get(121), clock = SurvivorCatalog.Get(122), ring = SurvivorCatalog.Get(123), trio = SurvivorCatalog.Get(125), quad = SurvivorCatalog.Get(126), stone = SurvivorCatalog.Get(127);
            Assert.That(barrier.CountByLevel, Is.EqualTo(new[] { 4 })); Assert.That(barrier.BaseCooldown, Is.EqualTo(6.4f).Within(1e-4f));
            Assert.That(retaliate.BaseRange, Is.EqualTo(3.75f).Within(1e-4f)); Assert.That(retaliate.BaseCooldown, Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(boomerang.CountByLevel, Is.EqualTo(new[] { 4 }));
            Assert.That(pool.Duration, Is.EqualTo(3.5f * 1.3f).Within(1e-4f)); Assert.That(pool.Width, Is.EqualTo(1.8f * 1.3f).Within(1e-4f));
            Assert.That(bounce.BouncesByLevel, Is.EqualTo(new[] { 7 })); Assert.That(bounce.CountByLevel, Is.EqualTo(new[] { 4 }));
            Assert.That(momentum.MomentumFloor, Is.EqualTo(0.4f)); Assert.That(SurvivorCatalog.Get(65).MomentumFloor, Is.Zero);
            Assert.That(clock.StunSeconds, Is.EqualTo(2.5f).Within(1e-4f)); Assert.That(clock.Chance, Is.EqualTo(0.8f).Within(1e-4f)); Assert.That(clock.ChancePerLevel, Is.Zero); Assert.That(clock.StunPerLevel, Is.Zero); Assert.That(clock.BaseCooldown, Is.EqualTo(3.2f).Within(1e-4f));
            Assert.That(ring.CountByLevel, Is.EqualTo(new[] { 8 })); Assert.That(ring.CountByLevel[0], Is.LessThanOrEqualTo(SurvivorCatalog.MaxPendingBlasts));
            Assert.That(trio.CountByLevel, Is.EqualTo(new[] { 4 })); Assert.That(quad.CountByLevel, Is.EqualTo(new[] { 4 }));
            Assert.That(stone.BaseDamage, Is.EqualTo(150f).Within(1e-3f)); Assert.That(stone.CountByLevel, Is.EqualTo(new[] { 4 }));
            Assert.That(SurvivorCatalog.Get(114).Pierce, Is.EqualTo(4)); Assert.That(SurvivorCatalog.Get(112).CountByLevel, Is.Empty);
            // The old evolutions keep their numbers (no momentum floor, no bounces, no chance).
            for (int i = 40; i < 58; i++) { ItemDef old = SurvivorCatalog.Get(i); Assert.That(old.MomentumFloor, Is.Zero); Assert.That(old.BouncesByLevel, Is.Empty); Assert.That(old.Chance, Is.Zero); }
            Assert.That(SurvivorCatalog.Get(40).BaseRange, Is.EqualTo(2.5f * 1.4f * 1.2f).Within(1e-3f), "old evolutions keep the old formula");
            Assert.That(SurvivorCatalog.Get(45).StunSeconds, Is.EqualTo(0f)); Assert.That(SurvivorCatalog.Get(48).StunSeconds, Is.EqualTo(0.9f).Within(1e-4f));
        }

        // ---- chest ----

        [TestCaseSource(nameof(Rows))]
        public void Chest_EvolvesAMaxedWeaponWithItsPassive(int evolution, int baseWeapon, int passive, string id, string name)
        {
            SurvivorSim sim = Own(baseWeapon, 5, passive, 1, 11);
            int slot = SlotOf(sim, baseWeapon); int weapons = sim.Inventory.WeaponCount;
            sim.SpawnPickupForTests(PickupKind.Chest, sim.Hero.Position, 0f);
            SurvivorEvent evolved = StepUntil(sim, SurvivorEventType.WeaponEvolved, 30);
            Assert.That(evolved.Id, Is.EqualTo(evolution)); Assert.That((int)evolved.Extra, Is.EqualTo(baseWeapon));
            Assert.That(sim.Inventory.Level(evolution), Is.EqualTo(1)); Assert.That(sim.Inventory.Level(baseWeapon), Is.Zero); Assert.That(sim.Inventory.Level(passive), Is.EqualTo(1));
            Assert.That(sim.Inventory.WeaponCount, Is.EqualTo(weapons)); Assert.That(sim.Inventory.WeaponAt(slot), Is.EqualTo(evolution), "keeps the slot");
            Assert.That(sim.WeaponCooldownForTests(evolution), Is.LessThanOrEqualTo(0f));
            float[] values = new float[SurvivorObservation.Size]; new SurvivorObservation().Write(sim, values);
            Assert.That(values[SurvivorObservation.InventoryOffset + evolution], Is.EqualTo(1f)); Assert.That(values[SurvivorObservation.InventoryOffset + baseWeapon], Is.Zero);
        }

        [Test]
        public void Chest_EvolutionEmitsTheNoticeAndTheChestEventsInOrder()
        {
            SurvivorSim sim = Own(26, 5, 58, 1, 12); sim.SpawnPickupForTests(PickupKind.Chest, sim.Hero.Position, 0f);
            List<SurvivorEventType> seen = new List<SurvivorEventType>(); SurvivorEvent opened = default, evolved = default; float gold = 0f;
            for (int tick = 0; tick < 30; tick++)
            {
                sim.Step(default);
                for (int i = 0; i < sim.Events.Count; i++)
                {
                    SurvivorEvent e = sim.Events[i];
                    if (e.Type == SurvivorEventType.WeaponEvolved) { evolved = e; seen.Add(e.Type); } else if (e.Type == SurvivorEventType.ChestOpened) { opened = e; seen.Add(e.Type); } else if (e.Type == SurvivorEventType.GoldCollected) gold += e.Value;
                }
            }
            Assert.That(seen, Is.EqualTo(new[] { SurvivorEventType.WeaponEvolved, SurvivorEventType.ChestOpened }));
            Assert.That(evolved.Id, Is.EqualTo(112)); Assert.That((int)evolved.Extra, Is.EqualTo(26)); Assert.That(opened.Id, Is.EqualTo(112)); Assert.That(opened.Value, Is.GreaterThan(0f)); Assert.That(gold, Is.GreaterThan(0f));
        }

        [TestCaseSource(nameof(Rows))]
        public void Chest_DoesNotEvolveBelowMaxLevel_OrWithoutThePassive(int evolution, int baseWeapon, int passive, string id, string name)
        {
            foreach (bool withPassive in new[] { false, true })
            {
                SurvivorSim sim = Own(baseWeapon, withPassive ? 4 : 5, passive, withPassive ? 1 : 0, 13);
                sim.SpawnPickupForTests(PickupKind.Chest, sim.Hero.Position, 0f);
                StepUntil(sim, SurvivorEventType.ChestOpened, 30, out bool sawEvolution);
                Assert.That(sawEvolution, Is.False, id + (withPassive ? " below max" : " no passive")); Assert.That(sim.Inventory.Level(evolution), Is.Zero);
                Assert.That(sim.Inventory.Level(baseWeapon), Is.GreaterThanOrEqualTo(withPassive ? 4 : 5));
            }
        }

        [Test]
        public void Chest_PassiveAtAnyLevelIsEnough_AndAnotherWeaponsPassiveIsNot()
        {
            SurvivorSim sim = Own(32, 5, 10, 5, 14); sim.SpawnPickupForTests(PickupKind.Chest, sim.Hero.Position, 0f);
            Assert.That(StepUntil(sim, SurvivorEventType.WeaponEvolved, 30).Id, Is.EqualTo(118));
            SurvivorSim wrong = Own(32, 5, 34, 3, 14); wrong.SpawnPickupForTests(PickupKind.Chest, wrong.Hero.Position, 0f);
            StepUntil(wrong, SurvivorEventType.ChestOpened, 30, out bool saw); Assert.That(saw, Is.False);
        }

        [Test]
        public void Chest_WithoutTheBaseWeapon_NeverEvolves_AndNoClassOffersAnEvolution()
        {
            foreach (object[] row in Rows)
            {
                int baseWeapon = (int)row[1], passive = (int)row[2];
                foreach (string cls in new[] { "warrior", "mage", "archer" })
                {
                    SurvivorClassDef kit = SurvivorDefaults.ForClass(cls); bool owns = Array.IndexOf(kit.WeaponPool, baseWeapon) >= 0;
                    if (owns) continue;
                    SurvivorSim sim = ClassSim(cls, 15); sim.GiveItemForTests(passive, 5);
                    sim.SpawnPickupForTests(PickupKind.Chest, sim.Hero.Position, 0f); StepUntil(sim, SurvivorEventType.ChestOpened, 30, out bool saw);
                    Assert.That(saw, Is.False, cls + " without " + baseWeapon); Assert.That(sim.Inventory.Level((int)row[0]), Is.Zero);
                }
            }
            foreach (string cls in new[] { "warrior", "mage", "archer" })
            {
                for (int seed = 0; seed < 150; seed++)
                {
                    SurvivorSim sim = ClassSim(cls, seed); sim.GiveXpForTests(5f); sim.Step(default);
                    for (int i = 0; i < sim.OfferCount; i++) Assert.That(sim.GetOffer(i).CatalogIndex, Is.Not.InRange(112, 127), cls + " offered an evolution");
                }
            }
        }

        [Test]
        public void ClassPools_EveryEvolutionIsReachableByAtLeastOneClass_AndItsPassiveIsInEveryPool()
        {
            foreach (object[] row in Rows)
            {
                bool reachable = false;
                foreach (string cls in new[] { "warrior", "mage", "archer" })
                {
                    SurvivorClassDef kit = SurvivorDefaults.ForClass(cls); Assert.That(Array.IndexOf(kit.WeaponPool, (int)row[0]), Is.LessThan(0), "pools list no evolution");
                    if (Array.IndexOf(kit.WeaponPool, (int)row[1]) >= 0) { reachable = true; Assert.That(Array.IndexOf(kit.PassivePool, (int)row[2]), Is.GreaterThanOrEqualTo(0), cls + " passive " + row[2]); }
                }
                Assert.That(reachable, Is.True, "evolution " + row[0]);
            }
        }

        [TestCaseSource(nameof(Rows))]
        public void Offers_AfterEvolving_TheBaseWeaponIsNeverOfferedAgain(int evolution, int baseWeapon, int passive, string id, string name)
        {
            SurvivorSim sim = Own(baseWeapon, 5, passive, 1, 16); sim.SpawnPickupForTests(PickupKind.Chest, sim.Hero.Position, 0f);
            StepUntil(sim, SurvivorEventType.WeaponEvolved, 30); int offers = 0;
            for (int round = 0; round < 30 && !sim.IsEnded; round++)
            {
                sim.GiveXpForTests(XpCurve.Required(sim.Level)); sim.Step(default);
                if (!sim.IsAwaitingPick) continue;
                for (int i = 0; i < sim.OfferCount; i++) { Assert.That(sim.GetOffer(i).CatalogIndex, Is.Not.EqualTo(baseWeapon), "base offered again"); Assert.That(sim.GetOffer(i).CatalogIndex, Is.Not.EqualTo(evolution), "the evolution is never an offer"); offers++; }
                sim.Step(new SurvivorInput(0, 0, 1));
            }
            Assert.That(offers, Is.GreaterThan(0)); Assert.That(sim.Inventory.Level(baseWeapon), Is.Zero);
        }

        [Test]
        public void Chest_EvolvesTheFirstEligibleWeaponInInventoryOrder_OneChestAtATime_AndSlotsStayAtSix()
        {
            SurvivorConfig config = Cfg("warrior"); config.ClassDef.StartingWeapon = 0; SurvivorSim sim = new SurvivorSim(config, 17);
            foreach (int weapon in new[] { 31, 26, 33, 29, 28 }) sim.GiveItemForTests(weapon, 5);
            Assert.That(sim.Inventory.WeaponCount, Is.EqualTo(6)); foreach (int passive in new[] { 6, 58, 34, 35, 7 }) sim.GiveItemForTests(passive, 1);
            int[] expected = { 117, 112, 119, 115, 114 }; int[] bases = { 31, 26, 33, 29, 28 };
            for (int chest = 0; chest < 5; chest++)
            {
                sim.SpawnPickupForTests(PickupKind.Chest, sim.Hero.Position, 0f);
                SurvivorEvent evolved = StepUntil(sim, SurvivorEventType.WeaponEvolved, 30);
                Assert.That(evolved.Id, Is.EqualTo(expected[chest])); Assert.That((int)evolved.Extra, Is.EqualTo(bases[chest]));
                Assert.That(sim.Inventory.WeaponCount, Is.EqualTo(6)); Assert.That(sim.Inventory.PassiveCount, Is.EqualTo(5));
            }
            Assert.That(sim.Inventory.WeaponAt(0), Is.EqualTo(0)); Assert.That(sim.Inventory.WeaponAt(1), Is.EqualTo(117));
            // With six weapons owned no new weapon is offered, with five evolved ones the offers still never name a base.
            for (int round = 0; round < 20 && !sim.IsEnded; round++)
            {
                sim.GiveXpForTests(XpCurve.Required(sim.Level)); sim.Step(default);
                if (!sim.IsAwaitingPick) continue;
                for (int i = 0; i < sim.OfferCount; i++) { int index = sim.GetOffer(i).CatalogIndex; Assert.That(SurvivorCatalog.Get(index).Kind == ItemKind.Weapon && sim.Inventory.Level(index) == 0, Is.False, "weapon offered with full slots " + index); }
                sim.Step(new SurvivorInput(0, 0, 1));
            }
        }

        // ---- behaviour ----

        [TestCaseSource(nameof(Rows))]
        public void Evolution_Fires_AndDealsDamage(int evolution, int baseWeapon, int passive, string id, string name)
        {
            if (evolution == 116 || evolution == 117) { Assert.Pass("retaliate and barrier are covered by their own tests"); return; }
            SurvivorSim sim = Arena(evolution, 21);
            bool fired = false;
            for (int tick = 0; tick < 1500 && !fired; tick++) { sim.Step(default); for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.WeaponFired && sim.Events[i].Id == evolution) fired = true; }
            Assert.That(fired, Is.True, id + " never fired");
            if (SurvivorCatalog.Get(evolution).Pattern != WeaponPattern.Freeze) { Step(sim, 300); Assert.That(sim.DamageDealtTotal, Is.GreaterThan(0f), id + " dealt nothing"); }
        }

        [Test]
        public void Barrier_Aegis_HoldsFourCharges_AndRechargesInEightyPercentOfTheTime()
        {
            SurvivorSim evolved = BarrierSim(117), control = BarrierSim(31, 5);
            evolved.Step(default); control.Step(default);
            Assert.That(evolved.BarrierCharges, Is.EqualTo(4)); Assert.That(control.BarrierCharges, Is.EqualTo(3));
            SurvivorEnemy a = evolved.SpawnEnemyForTests(2, new Vec2(30f, 0f)), b = control.SpawnEnemyForTests(2, new Vec2(30f, 0f));
            float hp = evolved.Hero.Hp;
            for (int hit = 0; hit < 4; hit++) evolved.DamageHeroForTests(10f, a);
            for (int hit = 0; hit < 3; hit++) control.DamageHeroForTests(10f, b);
            Assert.That(evolved.Hero.Hp, Is.EqualTo(hp)); Assert.That(evolved.BarrierCharges, Is.Zero); Assert.That(control.BarrierCharges, Is.Zero);
            Assert.That(evolved.WeaponCooldownForTests(117), Is.EqualTo(6.4f).Within(1e-3f)); Assert.That(control.WeaponCooldownForTests(31), Is.EqualTo(8f).Within(1e-3f));
            evolved.DamageHeroForTests(10f, a); Assert.That(evolved.Hero.Hp, Is.LessThan(hp), "the fifth hit gets through");
            for (int tick = 0; tick < 6 * 60 + 40; tick++) evolved.Step(default);
            Assert.That(evolved.BarrierCharges, Is.EqualTo(4), "back after 6.4 s");
        }

        [Test]
        public void Retaliate_Fury_BlastsRadiusThreePointSevenFive_AndHitsWhatTheBaseCannot()
        {
            SurvivorSim evolved = Own(30, 1, 0, 0, 22, 116), control = Own(30, 5, 0, 0, 22);
            foreach (SurvivorSim sim in new[] { evolved, control }) { sim.SetHeroInvulnerableForTests(); SurvivorEnemy far = sim.SpawnEnemyForTests(2, new Vec2(3.5f, 0f)); far.StunRemaining = 1000f; far.Hp = far.MaxHp = 5000f; }
            float radius = -1f;
            foreach (SurvivorSim sim in new[] { evolved, control })
            {
                sim.RequestRetaliateForTests(); sim.Step(default);
                for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.StrikeLanded && sim == evolved) radius = sim.Events[i].Value;
            }
            Assert.That(radius, Is.EqualTo(3.75f).Within(1e-4f));
            Assert.That(evolved.DamageDealtTotal, Is.GreaterThan(0f)); Assert.That(control.DamageDealtTotal, Is.Zero, "level-5 radius is 2.5");
            Assert.That(evolved.WeaponCooldownForTests(116), Is.GreaterThan(0.5f).And.LessThanOrEqualTo(0.8f));
        }

        [Test]
        public void Boomerang_Storm_ThrowsFour()
        {
            SurvivorSim sim = Arena(118, 23); int count = -1;
            for (int tick = 0; tick < 200 && count < 0; tick++) { sim.Step(default); for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.WeaponFired && sim.Events[i].Id == 118) count = (int)sim.Events[i].Extra; }
            Assert.That(count, Is.EqualTo(4));
        }

        [Test]
        public void PoisonPool_Miasma_LastsThirteenPercentLongerAndSpreadsWider()
        {
            SurvivorSim sim = Arena(119, 24); float seconds = -1f; SurvivorZone zone = null;
            for (int tick = 0; tick < 200 && seconds < 0f; tick++) { sim.Step(default); for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.WeaponFired && sim.Events[i].Id == 119) seconds = sim.Events[i].Value; }
            for (int i = 0; i < sim.Zones.Count; i++) if (sim.Zones[i].Active) zone = sim.Zones[i];
            Assert.That(seconds, Is.EqualTo(4.55f).Within(1e-3f)); Assert.That(zone, Is.Not.Null); Assert.That(zone.Radius, Is.EqualTo(2.34f).Within(1e-3f)); Assert.That(zone.SourceIndex, Is.EqualTo(119)); Assert.That(zone.Duration, Is.EqualTo(4.55f).Within(1e-3f));
        }

        [Test]
        public void BounceShot_ChaosShot_FiresFourProjectilesWithSevenBounces()
        {
            SurvivorSim sim = Arena(120, 25); int count = -1; int bounces = -1;
            for (int tick = 0; tick < 200 && count < 0; tick++)
            {
                sim.Step(default);
                for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.WeaponFired && sim.Events[i].Id == 120) count = (int)sim.Events[i].Extra;
                if (count >= 0) for (int i = 0; i < sim.Projectiles.Count; i++) if (sim.Projectiles[i].Active && sim.Projectiles[i].SourceIndex == 120) bounces = Math.Max(bounces, sim.Projectiles[i].BouncesLeft);
            }
            Assert.That(count, Is.EqualTo(4)); Assert.That(bounces, Is.EqualTo(7));
        }

        [Test]
        public void Momentum_Wraith_NeverFallsBelowFactorZeroPointFour()
        {
            SurvivorSim sim = Arena(121, 26, noEnemies: true); sim.SetMovementFactorForTests(0f);
            float damage = -1f; int count = -1;
            for (int tick = 0; tick < 100 && count < 0; tick++)
            {
                sim.Step(default);
                for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.WeaponFired && sim.Events[i].Id == 121) count = (int)sim.Events[i].Extra;
                if (count >= 0) for (int i = 0; i < sim.Projectiles.Count; i++) if (sim.Projectiles[i].Active && sim.Projectiles[i].SourceIndex == 121) damage = sim.Projectiles[i].Damage;
            }
            float full = (10f + 4f * 4f) * 1.5f;
            Assert.That(count, Is.EqualTo(4)); Assert.That(damage, Is.EqualTo(full * (0.4f + 1.2f * 0.4f)).Within(1e-3f), "idle damage");
            SurvivorSim control = Arena(65, 26, noEnemies: true); control.GiveItemForTests(65, 5); control.SetMovementFactorForTests(0f); float controlDamage = -1f;
            for (int tick = 0; tick < 100 && controlDamage < 0f; tick++)
            {
                control.Step(default);
                for (int i = 0; i < control.Projectiles.Count; i++) if (control.Projectiles[i].Active && control.Projectiles[i].SourceIndex == 65) controlDamage = control.Projectiles[i].Damage;
            }
            Assert.That(controlDamage, Is.EqualTo(26f * 0.4f).Within(1e-3f), "the base idles at 0.4 of its damage");
        }

        [Test]
        public void TimeClock_EternalCorridor_StunsTwoAndAHalfSecondsAtEightyPercent()
        {
            float seconds = -1f; int triggers = 0, tries = 0;
            for (int seed = 0; seed < 40; seed++)
            {
                SurvivorSim sim = Arena(122, 100 + seed);
                for (int tick = 0; tick < 700; tick++)
                {
                    sim.Step(default);
                    for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.WeaponFired && sim.Events[i].Id == 122) { seconds = sim.Events[i].Value; triggers++; }
                }
                tries += 4;
            }
            Assert.That(seconds, Is.EqualTo(2.5f).Within(1e-4f)); Assert.That(triggers / (float)tries, Is.GreaterThan(0.6f), "about 80% of the casts trigger");
        }

        [Test]
        public void BombRing_Nebula_QueuesEightBlasts_AndTheEvolutionIdLandsAndKnocksBack()
        {
            SurvivorSim sim = Arena(123, 27); int count = -1; int landed = 0, wrongId = 0;
            for (int tick = 0; tick < 200; tick++)
            {
                sim.Step(default);
                for (int i = 0; i < sim.Events.Count; i++)
                {
                    SurvivorEvent e = sim.Events[i];
                    if (e.Type == SurvivorEventType.WeaponFired && e.Id == 123 && count < 0) count = (int)e.Extra;
                    if (e.Type == SurvivorEventType.StrikeLanded) { if (e.Id == 123) landed++; else if (e.Id == 68) wrongId++; }
                }
            }
            Assert.That(count, Is.EqualTo(8)); Assert.That(landed, Is.GreaterThanOrEqualTo(8)); Assert.That(wrongId, Is.Zero);
        }

        [Test]
        public void Quad_FourWinds_FiresFourPerDirection()
        {
            SurvivorSim sim = Arena(126, 28, noEnemies: true); int count = -1; int shots = 0;
            for (int tick = 0; tick < 120 && count < 0; tick++)
            {
                sim.Step(default);
                for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.WeaponFired && sim.Events[i].Id == 126) count = (int)sim.Events[i].Extra;
                if (count >= 0) for (int i = 0; i < sim.Projectiles.Count; i++) if (sim.Projectiles[i].Active && sim.Projectiles[i].SourceIndex == 126) shots++;
            }
            Assert.That(count, Is.EqualTo(4)); Assert.That(shots, Is.EqualTo(16));
        }

        [Test]
        public void Trio_TwinBracelet_FiresFourAtOneTarget()
        {
            SurvivorSim sim = Arena(125, 29); int count = -1;
            for (int tick = 0; tick < 200 && count < 0; tick++) { sim.Step(default); for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.WeaponFired && sim.Events[i].Id == 125) count = (int)sim.Events[i].Extra; }
            Assert.That(count, Is.EqualTo(4));
        }

        [Test]
        public void Stone_SageStone_DealsFixedOneFiftyToFourEnemies_IgnoringMightAndCrit()
        {
            SurvivorConfig config = Cfg("mage"); config.ClassDef.StartingWeapon = 127; config.ClassDef.CritChance = 1f; SurvivorSim sim = new SurvivorSim(config, 30); sim.SetHeroInvulnerableForTests();
            sim.GiveItemForTests(8, 5);
            for (int k = 0; k < 4; k++) { SurvivorEnemy e = sim.SpawnEnemyForTests(2, new Vec2(3f + k, 0f)); e.StunRemaining = 1000f; e.Hp = e.MaxHp = 5000f; }
            List<float> dealt = new List<float>(); int fired = -1;
            for (int tick = 0; tick < 20 && fired < 0; tick++)
            {
                sim.Step(default);
                for (int i = 0; i < sim.Events.Count; i++)
                {
                    SurvivorEvent e = sim.Events[i];
                    if (e.Type == SurvivorEventType.DamageDealt) dealt.Add(e.Value); else if (e.Type == SurvivorEventType.WeaponFired && e.Id == 127) fired = (int)e.Extra; else Assert.That(e.Type, Is.Not.EqualTo(SurvivorEventType.Crit));
                }
            }
            Assert.That(fired, Is.EqualTo(4)); Assert.That(dealt, Is.EqualTo(new[] { 150f, 150f, 150f, 150f }));
        }

        [Test]
        public void HeavyHammer_MountainHammer_PiercesFiveEnemiesInALine()
        {
            SurvivorSim sim = Arena(114, 31, noEnemies: true);
            SurvivorEnemy[] line = new SurvivorEnemy[8];
            for (int k = 0; k < line.Length; k++) { line[k] = sim.SpawnEnemyForTests(0, new Vec2(1.5f + k, 0f)); line[k].StunRemaining = 1000f; line[k].Hp = line[k].MaxHp = 1e6f; }
            Dictionary<int, int> hits = new Dictionary<int, int>();
            for (int tick = 0; tick < 110; tick++)
            {
                sim.Step(default);
                for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.DamageDealt) hits[sim.Events[i].Id] = hits.TryGetValue(sim.Events[i].Id, out int n) ? n + 1 : 1;
            }
            int total = 0; foreach (int n in hits.Values) total += n;
            Assert.That(total, Is.EqualTo(15), "three hammers, five enemies each (a fifth hit needs a pierce of 4)");
        }

        [Test]
        public void Combo_PhantomBlade_SwingsEveryTwoTenthsOfASecond()
        {
            SurvivorSim sim = Arena(113, 32); List<int> ticks = new List<int>();
            for (int tick = 0; tick < 100; tick++) { sim.Step(default); for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.WeaponFired && sim.Events[i].Id == 113 && ticks.Count < 3) ticks.Add(tick); }
            Assert.That(ticks.Count, Is.EqualTo(3)); Assert.That(ticks[1] - ticks[0], Is.InRange(11, 13)); Assert.That(ticks[2] - ticks[1], Is.InRange(11, 13));
        }

        // ---- engine ----

        [Test]
        public void Determinism_AllSixteenEvolutionsOwned_SameSeedSameRun()
        {
            Assert.That(RunHash(41), Is.EqualTo(RunHash(41))); Assert.That(RunHash(41), Is.Not.EqualTo(RunHash(42)));
        }

        [Test]
        public void ZeroAllocation_WithSeveralEvolutionsOwned_StepAndObservation()
        {
            SurvivorConfig config = Cfg("warrior"); config.ClassDef.MaxHp = 1e7f; config.MapHalfSize = 15f; SurvivorSim sim = new SurvivorSim(config, 51);
            sim.SetEnemiesInvulnerableForTests(); sim.DisablePickupCollectionForTests();
            foreach (object[] row in Rows) sim.GiveItemForTests((int)row[0], 1);
            foreach (int passive in new[] { 34, 35, 36, 37 }) sim.GiveItemForTests(passive, 3);
            for (int i = 0; i < 40; i++) sim.SpawnEnemyForTests(i % 4, Vec2.FromAngle(i * 0.7f) * (2f + i % 7));
            SurvivorObservation observation = new SurvivorObservation(); float[] values = new float[SurvivorObservation.Size];
            for (int i = 0; i < 900; i++) { sim.Step(new SurvivorInput(i / 20 % 9, 0, 0)); observation.Write(sim, values); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 900; i < 2400; i++) sim.Step(new SurvivorInput(i / 20 % 9, 0, 0));
            long step = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 100; i++) observation.Write(sim, values); long write = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(sim.IsEnded, Is.False); Assert.That(step, Is.EqualTo(0L)); Assert.That(write, Is.EqualTo(0L));
            foreach (object[] row in Rows) Assert.That(values[SurvivorObservation.InventoryOffset + (int)row[0]], Is.EqualTo(1f));
        }

        [Test]
        public void ChestEvolution_AllocatesNothing()
        {
            SurvivorSim warm = Own(26, 5, 58, 1, 52); warm.SpawnPickupForTests(PickupKind.Chest, warm.Hero.Position, 0f); StepUntil(warm, SurvivorEventType.WeaponEvolved, 30); // JIT the path once
            SurvivorSim sim = Own(32, 5, 10, 1, 53); sim.Step(default);
            sim.SpawnPickupForTests(PickupKind.Chest, sim.Hero.Position, 0f);
            long before = GC.GetAllocatedBytesForCurrentThread(); sim.Step(default); long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(sim.Inventory.Level(118), Is.EqualTo(1), "the chest evolved the weapon in the measured tick"); Assert.That(allocated, Is.EqualTo(0L));
        }

        // ---- helpers ----

        private static string ClassFor(int baseWeapon) => Array.IndexOf(SurvivorDefaults.Warrior().WeaponPool, baseWeapon) >= 0 ? "warrior" : Array.IndexOf(SurvivorDefaults.Mage().WeaponPool, baseWeapon) >= 0 ? "mage" : "archer";

        private static SurvivorConfig Cfg(string cls)
        {
            SurvivorConfig config = SurvivorTestHelpers.Config(); config.ClassDef = SurvivorDefaults.ForClass(cls); config.ClassDef.CritChance = 0f; config.OpeningRing = 0; config.MapHalfSize = 50f; return config;
        }

        private static SurvivorSim ClassSim(string cls, int seed) => new SurvivorSim(Cfg(cls), seed);

        /// <summary>A hero of the class that can own <paramref name="baseWeapon"/>, holding it at <paramref name="level"/> and (when above 0) the passive; <paramref name="start"/> optionally replaces the starting weapon.</summary>
        private static SurvivorSim Own(int baseWeapon, int level, int passive, int passiveLevel, int seed, int start = -1)
        {
            SurvivorConfig config = Cfg(ClassFor(start >= 0 ? SurvivorCatalog.Get(start).EvolvesFrom : baseWeapon)); if (start >= 0) config.ClassDef.StartingWeapon = start;
            SurvivorSim sim = new SurvivorSim(config, seed);
            if (start < 0) sim.GiveItemForTests(baseWeapon, level);
            if (passiveLevel > 0) sim.GiveItemForTests(passive, passiveLevel);
            return sim;
        }

        private static int SlotOf(SurvivorSim sim, int weapon) { for (int i = 0; i < sim.Inventory.WeaponCount; i++) if (sim.Inventory.WeaponAt(i) == weapon) return i; return -1; }

        /// <summary>The evolution as the only weapon of a class that can own its base; four stunned, very tough brutes 3 m away on the axes; hero invulnerable.</summary>
        private static SurvivorSim Arena(int evolution, int seed, bool noEnemies = false)
        {
            int baseWeapon = SurvivorCatalog.Get(evolution).EvolvesFrom; baseWeapon = baseWeapon >= 0 ? baseWeapon : evolution;
            SurvivorConfig config = Cfg(ClassFor(baseWeapon)); config.ClassDef.StartingWeapon = evolution; SurvivorSim sim = new SurvivorSim(config, seed); sim.SetHeroInvulnerableForTests();
            if (!noEnemies) for (int k = 0; k < 4; k++) { SurvivorEnemy e = sim.SpawnEnemyForTests(2, Vec2.FromAngle(k * MathF.PI / 2f) * 3f); e.StunRemaining = 1e6f; e.Hp = e.MaxHp = 1e6f; }
            return sim;
        }

        private static SurvivorSim BarrierSim(int weapon, int level = 1)
        {
            SurvivorConfig config = Cfg("warrior"); config.ClassDef.StartingWeapon = weapon; SurvivorSim sim = new SurvivorSim(config, 61); if (level != 1) sim.GiveItemForTests(weapon, level); return sim;
        }

        private static void Step(SurvivorSim sim, int ticks) { for (int i = 0; i < ticks; i++) sim.Step(default); }

        private static SurvivorEvent StepUntil(SurvivorSim sim, SurvivorEventType type, int maxTicks) => StepUntil(sim, type, maxTicks, out _);

        private static SurvivorEvent StepUntil(SurvivorSim sim, SurvivorEventType type, int maxTicks, out bool sawEvolution)
        {
            sawEvolution = false;
            for (int tick = 0; tick < maxTicks && !sim.IsEnded; tick++)
            {
                sim.Step(sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1) : default);
                for (int i = 0; i < sim.Events.Count; i++)
                {
                    if (sim.Events[i].Type == SurvivorEventType.WeaponEvolved) sawEvolution = true;
                    if (sim.Events[i].Type == type) return sim.Events[i];
                }
            }
            Assert.Fail("Missing event " + type); return default;
        }

        private static long RunHash(int seed)
        {
            SurvivorConfig config = Cfg("warrior"); config.OpeningRing = 6; SurvivorSim sim = new SurvivorSim(config, seed); long hash = 17;
            foreach (object[] row in Rows) sim.GiveItemForTests((int)row[0], 1);
            for (int tick = 0; tick < 3600 && !sim.IsEnded; tick++)
            {
                SurvivorInput input = sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1 + tick % 3) : new SurvivorInput((tick / 40) % 9, (tick / 90) % 4, 0);
                sim.Step(input); hash = hash * 31 + SurvivorM5GoldenTests.StateHash(sim); hash = hash * 31 + BitConverter.SingleToInt32Bits(sim.MovementFactor); hash = hash * 31 + sim.PendingBlastCount;
            }
            Assert.That(sim.Kills, Is.GreaterThan(0)); return hash;
        }
    }
}
