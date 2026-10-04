using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    public sealed class SurvivorM11EnemyTests
    {
        private static SurvivorSim Sim(int seed = 7)
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), seed);
            sim.SetWeaponCooldownForTests(sim.Config.ClassDef.StartingWeapon, 9999f);
            return sim;
        }

        private static void Run(SurvivorSim sim, float seconds)
        {
            float until = sim.Time + seconds; int guard = 0;
            while (sim.Time < until && !sim.IsEnded && guard++ < 100000) { if (sim.IsAwaitingPick) sim.Step(new SurvivorInput(0, 0, 1)); else sim.Step(default); }
        }

        private static int Count(SurvivorSim sim, int type, bool small)
        {
            int count = 0;
            for (int i = 0; i < sim.Enemies.Count; i++) if (sim.Enemies[i].Active && sim.Enemies[i].TypeIndex == type && sim.Enemies[i].Small == small) count++;
            return count;
        }

        [Test]
        public void ChargerWindsUpThenDashesAndHurtsAHeroWhoStands()
        {
            SurvivorSim sim = Sim();
            SurvivorEnemy charger = sim.SpawnEnemyForTests(SurvivorDefaults.ChargerTypeIndex, sim.Hero.Position + new Vec2(5f, 0f));
            bool wound = false, charged = false; float fastest = 0f; int guard = 0;
            while (sim.Time < 4f && !sim.IsEnded && guard++ < 10000)
            {
                Vec2 before = charger.Position;
                sim.Step(default);
                wound |= charger.WindingUp;
                if (charger.Charging) { charged = true; fastest = System.MathF.Max(fastest, (charger.Position - before).Length / SurvivorSim.FixedDeltaTime); }
            }
            Assert.That(wound, Is.True, "the dash is announced by a wind-up");
            Assert.That(charged, Is.True);
            Assert.That(fastest, Is.GreaterThan(SurvivorDefaults.EnemyDef(SurvivorDefaults.ChargerTypeIndex).MoveSpeed * 2f));
            Assert.That(sim.DamageTaken, Is.GreaterThan(0f), "a hero who stands still is hit");
        }

        [Test]
        public void ChargerMissesAHeroWhoIsNotInItsPath()
        {
            SurvivorSim sim = Sim();
            SurvivorEnemy charger = sim.SpawnEnemyForTests(SurvivorDefaults.ChargerTypeIndex, sim.Hero.Position + new Vec2(5f, 0f));
            int guard = 0;
            while (!charger.Charging && guard++ < 10000) sim.Step(default);
            Assert.That(charger.Charging, Is.True);
            float facing = charger.Facing;
            sim.SetHeroStateForTests(sim.Hero.Position + new Vec2(0f, 6f), Vec2.Zero, 0f);
            while (charger.Charging && guard++ < 10000) sim.Step(default);
            Assert.That(charger.Facing, Is.EqualTo(facing), "the dash does not steer");
            Assert.That(sim.DamageTaken, Is.Zero);
        }

        [Test]
        public void SplitterLeavesTwoSmallCopiesThatDoNotSplitAgain()
        {
            SurvivorSim sim = Sim(); sim.SetHeroInvulnerableForTests();
            int type = SurvivorDefaults.SplitterTypeIndex;
            SurvivorEnemy splitter = sim.SpawnEnemyForTests(type, sim.Hero.Position + new Vec2(6f, 0f));
            float hp = splitter.MaxHp;
            sim.DamageEnemyForTests(splitter, 99999f);
            Assert.That(Count(sim, type, false), Is.Zero);
            Assert.That(Count(sim, type, true), Is.EqualTo(2));
            SurvivorEnemy small = null;
            for (int i = 0; i < sim.Enemies.Count; i++) if (sim.Enemies[i].Active && sim.Enemies[i].Small) small = sim.Enemies[i];
            Assert.That(small.MaxHp, Is.EqualTo(hp * SurvivorSim.SplitHpMul).Within(0.01f));
            sim.DamageEnemyForTests(small, 99999f);
            Assert.That(Count(sim, type, true), Is.EqualTo(1));
        }

        [Test]
        public void ShamanHealsTheEnemiesAroundIt()
        {
            SurvivorSim sim = Sim(); sim.SetHeroInvulnerableForTests();
            sim.SpawnEnemyForTests(SurvivorDefaults.ShamanTypeIndex, sim.Hero.Position + new Vec2(8f, 0f));
            SurvivorEnemy brute = sim.SpawnEnemyForTests(2, sim.Hero.Position + new Vec2(9f, 0f));
            sim.DamageEnemyForTests(brute, 30f);
            float hurt = brute.Hp;
            Run(sim, 3.5f);
            Assert.That(brute.Hp, Is.GreaterThan(hurt + brute.MaxHp * SurvivorSim.ShamanHealFraction * 0.9f));
            Assert.That(sim.DamageTaken, Is.Zero);
        }

        [Test]
        public void GoldenEnemyHasTripleHealthAndAlwaysDropsFiveTimesTheGold()
        {
            SurvivorSim sim = Sim(); sim.SetHeroInvulnerableForTests(); sim.DisablePickupCollectionForTests();
            SurvivorEnemy plain = sim.SpawnEnemyForTests(0, sim.Hero.Position + new Vec2(6f, 0f));
            SurvivorEnemy golden = sim.SpawnGoldenForTests(0, sim.Hero.Position + new Vec2(-6f, 0f));
            Assert.That(golden.Golden, Is.True);
            Assert.That(golden.MaxHp, Is.EqualTo(plain.MaxHp * SurvivorSim.GoldenHpMul).Within(0.01f));
            sim.DamageEnemyForTests(golden, 99999f);
            float gold = 0f;
            for (int i = 0; i < sim.Pickups.Count; i++) if (sim.Pickups[i].Active && sim.Pickups[i].Kind == PickupKind.Gold) gold = System.MathF.Max(gold, sim.Pickups[i].Value);
            Assert.That(gold, Is.GreaterThanOrEqualTo(sim.Config.Tuning.GoldMin * SurvivorSim.GoldenGoldMul));
        }

        [Test]
        public void NewTypesBorrowAnOldObservationChannel_AndCanBeSwitchedOff()
        {
            Assert.That(SurvivorDefaults.ObservedType(SurvivorDefaults.ChargerTypeIndex), Is.EqualTo(2));
            Assert.That(SurvivorDefaults.ObservedType(SurvivorDefaults.SplitterTypeIndex), Is.EqualTo(0));
            Assert.That(SurvivorDefaults.ObservedType(SurvivorDefaults.ShamanTypeIndex), Is.EqualTo(SurvivorDefaults.NecromancerTypeIndex));
            for (int type = 0; type < SurvivorDefaults.EnemyTypeCount; type++) Assert.That(SurvivorDefaults.ObservedType(type), Is.EqualTo(type));

            SpawnPhase phase = SurvivorDefaults.PhaseAt(450f);
            SurvivorSim on = new SurvivorSim(new SurvivorConfig { ObstacleCount = 0 }, 3);
            SurvivorSim off = new SurvivorSim(SurvivorTestHelpers.PreM11Drops(SurvivorTestHelpers.Config()), 3);
            for (int type = SurvivorDefaults.EnemyTypeCount; type < SurvivorDefaults.AllEnemyTypeCount; type++)
            {
                Assert.That(on.SpawnWeight(phase, type), Is.GreaterThan(0));
                Assert.That(off.SpawnWeight(phase, type), Is.Zero);
            }
        }
    }
}
