using System;
using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    /// <summary>M7 (T-030): Mage and Archer kits, weapon evolutions, the new Strike/Fan patterns and the new enemies.</summary>
    public sealed class SurvivorM7Tests
    {
        private static readonly int[] BaseWeapons = { 0, 1, 2, 3, 4, 5, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25 };

        [Test]
        public void Kits_EachClassStartsWithItsOwnWeapon_WarriorUnchanged()
        {
            SurvivorSim warrior = ClassSim(SurvivorDefaults.Warrior(), 1);
            Assert.That(warrior.Inventory.Level(0), Is.EqualTo(1)); Assert.That(warrior.Hero.MaxHp, Is.EqualTo(150f));
            Assert.That(SurvivorDefaults.Warrior().WeaponPool, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));

            SurvivorSim mage = ClassSim(SurvivorDefaults.Mage(), 1);
            Assert.That(mage.Inventory.Level(SurvivorCatalog.MagicBoltIndex), Is.EqualTo(1)); Assert.That(mage.Inventory.Level(0), Is.Zero);
            Assert.That(mage.Hero.MaxHp, Is.EqualTo(110f)); Assert.That(mage.Hero.MaxEnergy, Is.EqualTo(150f));

            SurvivorSim archer = ClassSim(SurvivorDefaults.Archer(), 1);
            Assert.That(archer.Inventory.Level(SurvivorCatalog.ArrowIndex), Is.EqualTo(1)); Assert.That(archer.Inventory.Level(0), Is.Zero);

            Assert.That(SurvivorDefaults.ForClass("mage").Id, Is.EqualTo("mage"));
            Assert.That(SurvivorDefaults.ForClass("archer").Id, Is.EqualTo("archer"));
            Assert.That(SurvivorDefaults.ForClass("rogue"), Is.Null);
        }

        [TestCase("mage")]
        [TestCase("archer")]
        public void Offers_OnlyShowTheClassPool(string classId)
        {
            SurvivorClassDef kit = SurvivorDefaults.ForClass(classId);
            HashSet<int> allowed = new HashSet<int>(kit.WeaponPool); allowed.UnionWith(kit.PassivePool); allowed.Add(62); allowed.Add(63);
            HashSet<int> seen = new HashSet<int>();
            for (int seed = 0; seed < 200; seed++)
            {
                SurvivorSim sim = ClassSim(SurvivorDefaults.ForClass(classId), seed); sim.GiveXpForTests(5f); sim.Step(default);
                Assert.That(sim.IsAwaitingPick, Is.True);
                for (int i = 0; i < sim.OfferCount; i++) { int index = sim.GetOffer(i).CatalogIndex; Assert.That(allowed.Contains(index), Is.True, classId + " offered " + index); seen.Add(index); }
            }
            foreach (int weapon in kit.WeaponPool) if (weapon != kit.StartingWeapon) Assert.That(seen.Contains(weapon), Is.True, "never offered " + weapon);
        }

        [Test]
        public void Catalog_EveryBaseWeaponHasOneEvolution_WithLevelFiveNumbersImproved()
        {
            HashSet<int> used = new HashSet<int>();
            foreach (int weapon in BaseWeapons)
            {
                ItemDef b = SurvivorCatalog.Get(weapon);
                int evolution = SurvivorCatalog.EvolutionOf(weapon);
                Assert.That(evolution, Is.InRange(SurvivorCatalog.FirstEvolutionIndex, SurvivorCatalog.FirstEvolutionIndex + SurvivorCatalog.EvolutionCount - 1), "weapon " + weapon);
                Assert.That(used.Add(evolution), Is.True, "shared evolution " + evolution);
                ItemDef e = SurvivorCatalog.Get(evolution);
                Assert.That(e.CatalogIndex, Is.EqualTo(evolution)); Assert.That(e.Kind, Is.EqualTo(ItemKind.Weapon)); Assert.That(e.MaxLevel, Is.EqualTo(1));
                Assert.That(e.Pattern, Is.EqualTo(b.Pattern)); Assert.That(e.EvolvesFrom, Is.EqualTo(weapon));
                Assert.That(SurvivorCatalog.Get(e.EvolutionPassive).Kind, Is.EqualTo(ItemKind.Passive));
                Assert.That(e.BaseDamage, Is.EqualTo((b.BaseDamage + b.DamagePerLevel * 4f) * 1.5f).Within(1e-4f));
                Assert.That(e.BaseDamage, Is.GreaterThan(b.BaseDamage + b.DamagePerLevel * 4f));
                Assert.That(SurvivorCatalog.EvolutionOf(evolution), Is.EqualTo(-1), "evolutions do not evolve again");
            }
            for (int passive = 6; passive <= 13; passive++) Assert.That(SurvivorCatalog.EvolutionOf(passive), Is.EqualTo(-1));
            Assert.That(SurvivorCatalog.EvolutionOf(0), Is.EqualTo(40)); Assert.That(SurvivorCatalog.Get(40).EvolutionPassive, Is.EqualTo(8));
            Assert.That(SurvivorCatalog.EvolutionOf(14), Is.EqualTo(46)); Assert.That(SurvivorCatalog.Get(46).EvolutionPassive, Is.EqualTo(10));
            Assert.That(SurvivorCatalog.EvolutionOf(20), Is.EqualTo(52)); Assert.That(SurvivorCatalog.Get(52).EvolutionPassive, Is.EqualTo(12));
        }

        [Test]
        public void Chest_EvolvesAMaxedWeaponWithItsPassive_AndTheBaseIsNeverOfferedAgain()
        {
            SurvivorSim sim = ClassSim(SurvivorDefaults.Warrior(), 3);
            sim.GiveItemForTests(0, 5); sim.GiveItemForTests(SurvivorCatalog.MightGauntletIndex, 1);
            sim.SpawnPickupForTests(PickupKind.Chest, sim.Hero.Position, 0f);
            SurvivorEvent evolved = StepUntil(sim, SurvivorEventType.WeaponEvolved, 30);
            Assert.That(evolved.Id, Is.EqualTo(40)); Assert.That((int)evolved.Extra, Is.EqualTo(0));
            Assert.That(sim.Inventory.Level(40), Is.EqualTo(1)); Assert.That(sim.Inventory.Level(0), Is.Zero);
            Assert.That(sim.Inventory.WeaponCount, Is.EqualTo(1));

            float[] observation = new float[SurvivorObservation.Size]; new SurvivorObservation().Write(sim, observation);
            Assert.That(observation[SurvivorObservation.InventoryOffset + 40], Is.EqualTo(1f));
            Assert.That(observation[SurvivorObservation.InventoryOffset + 0], Is.Zero);

            for (int round = 0; round < 40; round++)
            {
                sim.GiveXpForTests(XpCurve.Required(sim.Level)); sim.Step(default);
                if (!sim.IsAwaitingPick) continue;
                for (int i = 0; i < sim.OfferCount; i++) Assert.That(sim.GetOffer(i).CatalogIndex, Is.Not.EqualTo(0), "evolved-away base offered again");
                sim.Step(new SurvivorInput(0, 0, 1));
            }
        }

        [TestCase(5, false)]
        [TestCase(4, true)]
        public void Chest_DoesNotEvolveWithoutPassiveOrBelowMaxLevel(int level, bool withPassive)
        {
            SurvivorSim sim = ClassSim(SurvivorDefaults.Warrior(), 4);
            sim.GiveItemForTests(0, level); if (withPassive) sim.GiveItemForTests(SurvivorCatalog.MightGauntletIndex, 1);
            sim.SpawnPickupForTests(PickupKind.Chest, sim.Hero.Position, 0f);
            StepUntil(sim, SurvivorEventType.ChestOpened, 30, out bool sawEvolution);
            Assert.That(sawEvolution, Is.False); Assert.That(sim.Inventory.Level(40), Is.Zero); Assert.That(sim.Inventory.Level(0), Is.GreaterThanOrEqualTo(level));
        }

        [Test]
        public void Observation_InventoryIsLevelOverMaxLevel()
        {
            SurvivorSim sim = ClassSim(SurvivorDefaults.Warrior(), 5);
            sim.GiveItemForTests(0, 3); sim.GiveItemForTests(46, 1); sim.GiveItemForTests(SurvivorCatalog.HourglassIndex, 5);
            float[] observation = new float[SurvivorObservation.Size]; new SurvivorObservation().Write(sim, observation);
            Assert.That(observation[SurvivorObservation.InventoryOffset + 0], Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(observation[SurvivorObservation.InventoryOffset + 46], Is.EqualTo(1f));
            Assert.That(observation[SurvivorObservation.InventoryOffset + SurvivorCatalog.HourglassIndex], Is.EqualTo(1f));
            Assert.That(observation[SurvivorObservation.InventoryOffset + 1], Is.Zero);
        }

        [Test]
        public void MageFireball_ExplodesOnFirstHit_AndDamagesTheArea()
        {
            SurvivorSim sim = ClassSim(SurvivorDefaults.Mage(), 6); sim.SetWeaponCooldownForTests(SurvivorCatalog.MagicBoltIndex, 1000f);
            SurvivorEnemy first = sim.SpawnEnemyForTests(2, new Vec2(5f, 0f));
            SurvivorEnemy beside = sim.SpawnEnemyForTests(2, new Vec2(5f, 1.2f));
            SurvivorEnemy far = sim.SpawnEnemyForTests(2, new Vec2(5f, 6f));
            sim.Step(new SurvivorInput(0, 1, 0));
            Assert.That(sim.SkillUses[0], Is.EqualTo(1)); Assert.That(sim.Hero.SkillCooldowns[0], Is.EqualTo(3f));
            SurvivorEvent blast = StepUntil(sim, SurvivorEventType.StrikeLanded, 40);
            Assert.That(blast.Id, Is.EqualTo(-1)); Assert.That(blast.Value, Is.EqualTo(2f));
            Assert.That(first.MaxHp - first.Hp, Is.EqualTo(40f).Within(1e-3f));
            Assert.That(beside.MaxHp - beside.Hp, Is.EqualTo(40f).Within(1e-3f));
            Assert.That(far.Hp, Is.EqualTo(far.MaxHp));
            for (int i = 0; i < sim.Projectiles.Count; i++) Assert.That(sim.Projectiles[i].Active && sim.Projectiles[i].SourceIndex == -1, Is.False, "fireball must be gone");
        }

        [Test]
        public void ArcherPowerShot_PiercesTwoEnemies()
        {
            SurvivorSim sim = ClassSim(SurvivorDefaults.Archer(), 7); sim.SetWeaponCooldownForTests(SurvivorCatalog.ArrowIndex, 1000f);
            SurvivorEnemy[] line = { sim.SpawnEnemyForTests(2, new Vec2(4f, 0f)), sim.SpawnEnemyForTests(2, new Vec2(6f, 0f)), sim.SpawnEnemyForTests(2, new Vec2(8f, 0f)), sim.SpawnEnemyForTests(2, new Vec2(10f, 0f)) };
            sim.Step(new SurvivorInput(0, 1, 0));
            SurvivorTestHelpers.Step(sim, 40);
            int hit = 0; foreach (SurvivorEnemy enemy in line) if (enemy.Hp < enemy.MaxHp) { hit++; Assert.That(enemy.MaxHp - enemy.Hp, Is.EqualTo(50f).Within(1e-3f)); }
            Assert.That(hit, Is.EqualTo(3), "pierce 2 = three enemies hit");
            Assert.That(line[3].Hp, Is.EqualTo(line[3].MaxHp));
        }

        [Test]
        public void MageFrostBurst_DamagesAndStunsAroundTheHero()
        {
            SurvivorSim sim = ClassSim(SurvivorDefaults.Mage(), 8); sim.SetWeaponCooldownForTests(SurvivorCatalog.MagicBoltIndex, 1000f);
            SurvivorEnemy front = sim.SpawnEnemyForTests(2, new Vec2(2f, 0f));
            SurvivorEnemy back = sim.SpawnEnemyForTests(2, new Vec2(-2f, 1f));
            SurvivorEnemy outside = sim.SpawnEnemyForTests(2, new Vec2(6f, 0f));
            sim.Step(new SurvivorInput(0, 4, 0));
            SurvivorEvent burst = Find(sim.Events, SurvivorEventType.StrikeLanded);
            Assert.That(burst.Id, Is.EqualTo(-1)); Assert.That(burst.Value, Is.EqualTo(3f));
            Assert.That(Find(sim.Events, SurvivorEventType.SkillUsed).Extra, Is.EqualTo(2f));
            foreach (SurvivorEnemy enemy in new[] { front, back })
            {
                Assert.That(enemy.MaxHp - enemy.Hp, Is.EqualTo(15f).Within(1e-3f)); Assert.That(enemy.StunRemaining, Is.GreaterThan(1.4f));
            }
            Assert.That(outside.Hp, Is.EqualTo(outside.MaxHp)); Assert.That(outside.StunRemaining, Is.Zero);
            Assert.That(sim.Hero.Energy, Is.LessThan(150f - 39f));
        }

        [TestCase(1, 4f, 0f)]
        [TestCase(3, 0f, 4f)]
        [TestCase(0, 4f, 0f)]
        public void MageBlink_JumpsAlongTheMoveDirection_ElseTheFacing(int move, float x, float y)
        {
            SurvivorSim sim = ClassSim(SurvivorDefaults.Mage(), 9);
            sim.Step(new SurvivorInput(move, 3, 0));
            Assert.That(sim.Hero.Position.X, Is.EqualTo(x).Within(0.05f)); Assert.That(sim.Hero.Position.Y, Is.EqualTo(y).Within(0.05f));
            Assert.That(sim.Hero.SkillCooldowns[2], Is.EqualTo(4f));
        }

        [Test]
        public void MageBlink_StaysInsideTheMap()
        {
            SurvivorSim sim = ClassSim(SurvivorDefaults.Mage(), 10);
            sim.SetHeroStateForTests(new Vec2(48f, 0f), Vec2.Zero, 0f);
            sim.Step(new SurvivorInput(1, 3, 0));
            Assert.That(sim.Hero.Position.X, Is.LessThanOrEqualTo(50f - sim.Hero.Radius + 1e-4f));
        }

        [Test]
        public void ArcherRollBack_DashesAwayFromTheFacing_WhateverTheMove()
        {
            SurvivorSim archer = ClassSim(SurvivorDefaults.Archer(), 11);
            archer.Step(new SurvivorInput(1, 2, 0));
            Assert.That(archer.Hero.Dashing, Is.True);
            Assert.That(archer.Hero.Position.X, Is.EqualTo(-0.3f).Within(1e-3f)); Assert.That(archer.Hero.Position.Y, Is.EqualTo(0f).Within(1e-4f));

            SurvivorSim warrior = ClassSim(SurvivorDefaults.Warrior(), 11);
            warrior.Step(new SurvivorInput(1, 3, 0));
            Assert.That(warrior.Hero.Position.X, Is.EqualTo(0.3f).Within(1e-3f), "warrior dash still follows the move");
        }

        [Test]
        public void MageManaShield_CoversEveryDirection_WarriorShieldOnlyTheFront()
        {
            Assert.That(DamageFromBehindWhileBlocking(SurvivorDefaults.Mage()), Is.EqualTo(20f * 0.4f).Within(1e-3f));
            Assert.That(DamageFromBehindWhileBlocking(SurvivorDefaults.Warrior()), Is.EqualTo(20f).Within(1e-3f));
        }

        [Test]
        public void Exploder_BlowsUpNextToTheHero_WithoutKillCreditOrDrops()
        {
            SurvivorSim sim = ClassSim(SurvivorDefaults.Warrior(), 12); sim.SetWeaponCooldownForTests(0, 1000f);
            SurvivorEnemy exploder = sim.SpawnEnemyForTests(SurvivorDefaults.ExploderTypeIndex, new Vec2(1.3f, 0f));
            float hp = sim.Hero.Hp; float damage = exploder.Damage;
            SurvivorEvent boom = StepUntil(sim, SurvivorEventType.EnemyExploded, 60);
            Assert.That(boom.Id, Is.EqualTo(exploder.Id)); Assert.That(boom.Value, Is.EqualTo(2.2f));
            Assert.That(sim.Time, Is.EqualTo(0.6f).Within(0.05f));
            Assert.That(exploder.Active, Is.False); Assert.That(sim.Kills, Is.Zero);
            Assert.That(hp - sim.Hero.Hp, Is.EqualTo(damage).Within(0.2f));
            for (int i = 0; i < sim.Pickups.Count; i++) Assert.That(sim.Pickups[i].Active, Is.False, "an explosion drops nothing");
        }

        [Test]
        public void Exploder_HeroWhoRunsAwayTakesNoDamage()
        {
            SurvivorSim sim = ClassSim(SurvivorDefaults.Warrior(), 13); sim.SetWeaponCooldownForTests(0, 1000f);
            sim.SpawnEnemyForTests(SurvivorDefaults.ExploderTypeIndex, new Vec2(1.3f, 0f));
            float hp = sim.Hero.Hp;
            StepUntil(sim, SurvivorEventType.EnemyExploded, 60, new SurvivorInput(5, 0, 0));
            Assert.That(sim.Hero.Hp, Is.GreaterThanOrEqualTo(hp));
        }

        [Test]
        public void Exploder_KillingBlowIsAnExplosionDeath()
        {
            SurvivorSim sim = ClassSim(SurvivorDefaults.Warrior(), 14); sim.SetWeaponCooldownForTests(0, 1000f);
            sim.SpawnEnemyForTests(SurvivorDefaults.ExploderTypeIndex, new Vec2(1.3f, 0f)); sim.SetHeroHpForTests(5f);
            StepUntil(sim, SurvivorEventType.HeroDied, 60);
            Assert.That(sim.EndReason, Is.EqualTo(EndReason.Died)); Assert.That(sim.DeathCause, Is.EqualTo(DeathCause.Explosion));
        }

        [Test]
        public void Ghost_FloatsThroughObstacles_WalkerIsPushedOut()
        {
            SurvivorObstacle obstacle = NearObstacle(ObstacleSim(15));
            SurvivorSim ghostSim = ObstacleSim(15); SurvivorEnemy ghost = ghostSim.SpawnEnemyForTests(SurvivorDefaults.GhostTypeIndex, obstacle.Position);
            SurvivorSim walkerSim = ObstacleSim(15); SurvivorEnemy walker = walkerSim.SpawnEnemyForTests(0, obstacle.Position);
            ghostSim.Step(default); walkerSim.Step(default);
            Assert.That(Vec2.Distance(ghost.Position, obstacle.Position), Is.LessThan(obstacle.Radius));
            Assert.That(Vec2.Distance(walker.Position, obstacle.Position), Is.GreaterThanOrEqualTo(obstacle.Radius + walker.Radius - 1e-3f));
        }

        [Test]
        public void Necromancer_SummonsThreeWalkersEverySixSeconds()
        {
            SurvivorSim sim = ClassSim(SurvivorDefaults.Warrior(), 16); sim.SetHeroInvulnerableForTests(); sim.SetWeaponCooldownForTests(0, 1000f);
            SurvivorEnemy necromancer = sim.SpawnEnemyForTests(SurvivorDefaults.NecromancerTypeIndex, new Vec2(15f, 0f));
            Assert.That(sim.SummonCooldownForTests(necromancer), Is.EqualTo(6f));
            List<float> times = new List<float>();
            for (int tick = 0; tick < 760 && !sim.IsEnded; tick++)
            {
                sim.Step(sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1) : default);
                for (int i = 0; i < sim.Events.Count; i++)
                {
                    SurvivorEvent e = sim.Events[i]; if (e.Type != SurvivorEventType.EnemySummoned) continue;
                    Assert.That(e.Id, Is.EqualTo(necromancer.Id)); Assert.That(e.Value, Is.EqualTo(3f));
                    Assert.That(WalkersNear(sim, e.Point, 2f), Is.GreaterThanOrEqualTo(3));
                    times.Add(sim.Time);
                }
            }
            Assert.That(times.Count, Is.EqualTo(2));
            Assert.That(times[0], Is.EqualTo(6f).Within(0.05f)); Assert.That(times[1], Is.EqualTo(12f).Within(0.05f));
        }

        [Test]
        public void Spawning_NewEnemiesOnlyFromMinuteFive_AndNeverAsElites()
        {
            for (int phase = 0; phase < SurvivorDefaults.PhaseCount; phase++)
            {
                SpawnPhase p = SurvivorDefaults.GetPhase(phase);
                Assert.That(p.Weights.Count, Is.EqualTo(SurvivorDefaults.EnemyTypeCount));
                bool hasNew = p.Weights[5] + p.Weights[6] + p.Weights[7] > 0;
                if (p.From < 300f) Assert.That(hasNew, Is.False, "phase " + phase);
                Assert.That(p.Weights[4], Is.Zero, "the boss is never scheduled");
            }
            Assert.That(SurvivorDefaults.GetPhase(SurvivorDefaults.PhaseCount - 1).Weights[SurvivorDefaults.NecromancerTypeIndex], Is.GreaterThan(0));
            Assert.That(SurvivorDefaults.EliteTypeCount, Is.EqualTo(4));
        }

        [Test]
        public void Lightning_StrikesAnEnemyInRange_AndBlastsItsNeighbours()
        {
            SurvivorSim sim = WeaponClassSim(SurvivorDefaults.Mage(), SurvivorCatalog.LightningIndex, 17);
            SurvivorEnemy target = sim.SpawnEnemyForTests(2, new Vec2(5f, 0f));
            SurvivorEnemy neighbour = sim.SpawnEnemyForTests(2, new Vec2(5.5f, 0.6f));
            sim.Step(default);
            SurvivorEvent strike = Find(sim.Events, SurvivorEventType.StrikeLanded, SurvivorCatalog.LightningIndex);
            Assert.That(strike.Value, Is.EqualTo(1f));
            Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, SurvivorCatalog.LightningIndex).Extra, Is.EqualTo(1f));
            Assert.That(target.MaxHp - target.Hp, Is.EqualTo(28f).Within(1e-3f)); Assert.That(neighbour.MaxHp - neighbour.Hp, Is.EqualTo(28f).Within(1e-3f));
            Assert.That(sim.WeaponCooldownForTests(SurvivorCatalog.LightningIndex), Is.EqualTo(2f).Within(1e-5f));

            SurvivorSim empty = WeaponClassSim(SurvivorDefaults.Mage(), SurvivorCatalog.LightningIndex, 18);
            SurvivorEnemy distant = empty.SpawnEnemyForTests(2, new Vec2(15f, 0f)); empty.Step(default);
            Assert.That(empty.WeaponCooldownForTests(SurvivorCatalog.LightningIndex), Is.Zero); Assert.That(distant.Hp, Is.EqualTo(distant.MaxHp));
        }

        [Test]
        public void ArrowRain_StrikesSeveralDifferentEnemies()
        {
            SurvivorSim sim = WeaponClassSim(SurvivorDefaults.Archer(), SurvivorCatalog.ArrowRainIndex, 19);
            SurvivorEnemy a = sim.SpawnEnemyForTests(2, new Vec2(6f, 0f)); SurvivorEnemy b = sim.SpawnEnemyForTests(2, new Vec2(-6f, 0f));
            sim.Step(default);
            Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, SurvivorCatalog.ArrowRainIndex).Extra, Is.EqualTo(2f));
            Assert.That(a.Hp, Is.LessThan(a.MaxHp)); Assert.That(b.Hp, Is.LessThan(b.MaxHp));
        }

        [Test]
        public void MultiShot_FiresAFanCentredOnTheNearestEnemy()
        {
            SurvivorSim sim = WeaponClassSim(SurvivorDefaults.Archer(), SurvivorCatalog.MultiShotIndex, 20);
            sim.SpawnEnemyForTests(2, new Vec2(6f, 0f));
            sim.Step(default);
            List<float> angles = new List<float>();
            for (int i = 0; i < sim.Projectiles.Count; i++) if (sim.Projectiles[i].Active && sim.Projectiles[i].SourceIndex == SurvivorCatalog.MultiShotIndex) angles.Add(sim.Projectiles[i].Velocity.Angle());
            angles.Sort();
            Assert.That(angles.Count, Is.EqualTo(3));
            Assert.That(angles[0], Is.EqualTo(-20f * MathF.PI / 180f).Within(1e-3f)); Assert.That(angles[1], Is.EqualTo(0f).Within(1e-3f)); Assert.That(angles[2], Is.EqualTo(20f * MathF.PI / 180f).Within(1e-3f));
            Assert.That(sim.WeaponCooldownForTests(SurvivorCatalog.MultiShotIndex), Is.EqualTo(1.6f).Within(1e-5f));

            SurvivorSim empty = WeaponClassSim(SurvivorDefaults.Archer(), SurvivorCatalog.MultiShotIndex, 21); empty.Step(default);
            Assert.That(empty.WeaponCooldownForTests(SurvivorCatalog.MultiShotIndex), Is.Zero);
        }

        [TestCase("mage")]
        [TestCase("archer")]
        public void NewClasses_RunAFullMinuteDeterministically(string classId)
        {
            Assert.That(RunHash(classId, 31), Is.EqualTo(RunHash(classId, 31)));
            Assert.That(RunHash(classId, 31), Is.Not.EqualTo(RunHash(classId, 32)));
        }

        private static long RunHash(string classId, int seed)
        {
            SurvivorConfig config = SurvivorTestHelpers.Config(); config.ClassDef = SurvivorDefaults.ForClass(classId);
            SurvivorSim sim = new SurvivorSim(config, seed); long hash = 17;
            for (int tick = 0; tick < 3600 && !sim.IsEnded; tick++)
            {
                SurvivorInput input = sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1 + tick % 3) : new SurvivorInput((tick / 40) % 9, (tick / 90) % 5, 0);
                sim.Step(input); hash = hash * 31 + SurvivorM5GoldenTests.StateHash(sim);
            }
            Assert.That(sim.Kills, Is.GreaterThan(0), classId + " kit should kill something in a minute");
            return hash;
        }

        private static float DamageFromBehindWhileBlocking(SurvivorClassDef kit)
        {
            SurvivorSim sim = ClassSim(kit, 22);
            SurvivorEnemy behind = sim.SpawnEnemyForTests(0, new Vec2(-20f, 0f));
            sim.Step(new SurvivorInput(0, 2, 0));
            Assert.That(sim.Hero.Blocking, Is.True); Assert.That(sim.Hero.Facing, Is.EqualTo(0f));
            float hp = sim.Hero.Hp; sim.DamageHeroForTests(20f, behind, true);
            return hp - sim.Hero.Hp;
        }

        private static SurvivorSim ClassSim(SurvivorClassDef kit, int seed)
        {
            kit.CritChance = 0f;
            SurvivorConfig config = SurvivorTestHelpers.Config(); config.ClassDef = kit;
            return new SurvivorSim(config, seed);
        }

        private static SurvivorSim WeaponClassSim(SurvivorClassDef kit, int weapon, int seed)
        {
            kit.StartingWeapon = weapon; return ClassSim(kit, seed);
        }

        private static SurvivorSim ObstacleSim(int seed)
        {
            SurvivorConfig config = SurvivorTestHelpers.Config(); config.ObstacleCount = 30; config.ClassDef.CritChance = 0f;
            SurvivorSim sim = new SurvivorSim(config, seed); sim.SetWeaponCooldownForTests(0, 1000f); return sim;
        }

        private static SurvivorObstacle NearObstacle(SurvivorSim sim)
        {
            for (int i = 0; i < sim.Obstacles.Count; i++)
            {
                SurvivorObstacle o = sim.Obstacles[i];
                if (o.Active && o.Position.Length < 25f && o.Radius >= 0.6f) return o;
            }
            Assert.Fail("No obstacle near the hero"); return null;
        }

        private static int WalkersNear(SurvivorSim sim, Vec2 point, float radius)
        {
            int count = 0;
            for (int i = 0; i < sim.Enemies.Count; i++) if (sim.Enemies[i].Active && sim.Enemies[i].TypeIndex == 0 && Vec2.Distance(sim.Enemies[i].Position, point) <= radius) count++;
            return count;
        }

        private static SurvivorEvent StepUntil(SurvivorSim sim, SurvivorEventType type, int maxTicks, SurvivorInput input = default)
        {
            SurvivorEvent found = StepUntil(sim, type, maxTicks, out _, input);
            return found;
        }

        private static SurvivorEvent StepUntil(SurvivorSim sim, SurvivorEventType type, int maxTicks, out bool sawEvolution, SurvivorInput input = default)
        {
            sawEvolution = false;
            for (int tick = 0; tick < maxTicks && !sim.IsEnded; tick++)
            {
                sim.Step(sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1) : input);
                for (int i = 0; i < sim.Events.Count; i++)
                {
                    if (sim.Events[i].Type == SurvivorEventType.WeaponEvolved) sawEvolution = true;
                    if (sim.Events[i].Type == type) return sim.Events[i];
                }
            }
            Assert.Fail("Missing event " + type); return default;
        }

        private static SurvivorEvent Find(IReadOnlyList<SurvivorEvent> events, SurvivorEventType type, int id = int.MinValue)
        {
            for (int i = 0; i < events.Count; i++) if (events[i].Type == type && (id == int.MinValue || events[i].Id == id)) return events[i];
            Assert.Fail("Missing event " + type); return default;
        }
    }
}
