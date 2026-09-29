using System;
using NUnit.Framework;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    public sealed class SurvivorSimulationTests
    {
        [Test]
        public void Spawn_FirstMinuteOnlyWalkers_AndRespectsMax()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 10); sim.SetHeroInvulnerableForTests();
            SurvivorTestHelpers.Step(sim, 59 * 60);
            Assert.That(sim.AliveEnemyCount, Is.LessThanOrEqualTo(30));
            for (int i = 0; i < sim.Enemies.Count; i++) if (sim.Enemies[i].Active) Assert.That(sim.Enemies[i].TypeIndex, Is.EqualTo(0));
            sim.SetTimeForTests(200f); SurvivorTestHelpers.Step(sim, 600);
            for (int i = 0; i < sim.Enemies.Count; i++) if (sim.Enemies[i].Active) Assert.That(sim.Enemies[i].TypeIndex, Is.AnyOf(0, 1, 2));
        }

        [Test]
        public void Spawn_TierScalesCountAndHp()
        {
            SurvivorSim one = new SurvivorSim(SurvivorTestHelpers.Config(1), 3); SurvivorSim three = new SurvivorSim(SurvivorTestHelpers.Config(3), 3);
            SurvivorEnemy a = one.SpawnEnemyForTests(0, new Vec2(10, 0)); SurvivorEnemy b = three.SpawnEnemyForTests(0, new Vec2(10, 0));
            Assert.That(b.MaxHp / a.MaxHp, Is.EqualTo(1.7f).Within(0.001f)); one.SetHeroInvulnerableForTests(); three.SetHeroInvulnerableForTests();
            SurvivorTestHelpers.Step(one, 1200); SurvivorTestHelpers.Step(three, 1200); Assert.That(three.AliveEnemyCount, Is.GreaterThanOrEqualTo(one.AliveEnemyCount));
        }

        [Test]
        public void Spawn_NeverInsideObstacleOrOutsideMap()
        {
            SurvivorConfig config = new SurvivorConfig { ObstacleCount = 40 }; SurvivorSim sim = new SurvivorSim(config, 44); sim.SetHeroInvulnerableForTests(); SurvivorTestHelpers.Step(sim, 3600);
            for (int i = 0; i < sim.Enemies.Count; i++) if (sim.Enemies[i].Active)
            {
                SurvivorEnemy enemy = sim.Enemies[i]; Assert.That(MathF.Abs(enemy.Position.X), Is.LessThanOrEqualTo(50f - enemy.Radius + 0.001f));
                for (int j = 0; j < sim.Obstacles.Count; j++) if (sim.Obstacles[j].Active) Assert.That(Vec2.Distance(enemy.Position, sim.Obstacles[j].Position), Is.GreaterThanOrEqualTo(enemy.Radius + sim.Obstacles[j].Radius - 0.01f));
            }
        }

        [Test]
        public void Elite_SpawnsAt180s_AndDropsGoldBurstAndBigGem()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 5); sim.SetTimeForTests(179.99f); sim.SetHeroInvulnerableForTests(); SurvivorTestHelpers.Step(sim, 2);
            SurvivorEnemy elite = FindEnemy(sim, true, false); Assert.That(elite, Is.Not.Null); sim.DamageEnemyForTests(elite, 100000f);
            bool gold = false, gem = false; for (int i = 0; i < sim.Pickups.Count; i++) if (sim.Pickups[i].Active) { gold |= sim.Pickups[i].Kind == PickupKind.Gold; gem |= sim.Pickups[i].Kind == PickupKind.Gem && sim.Pickups[i].Value >= 50f; }
            Assert.That(gold, Is.True); Assert.That(gem, Is.True);
        }

        [Test]
        public void Boss_SpawnsAt900_AndKillingItWins()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 7); sim.SetTimeForTests(899.99f); sim.SetHeroInvulnerableForTests(); SurvivorTestHelpers.Step(sim, 2);
            Assert.That(sim.BossAlive, Is.True); sim.DamageEnemyForTests(sim.BossEnemy, 100000f); Assert.That(sim.EndReason, Is.EqualTo(EndReason.Won)); Assert.That(sim.Gold, Is.GreaterThanOrEqualTo(500f));
        }

        [Test]
        public void Boss_AliveAt1020_RunExpires()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 8); sim.SetTimeForTests(899.99f); sim.SetHeroInvulnerableForTests(); SurvivorTestHelpers.Step(sim, 2); sim.SetTimeForTests(1019.99f); SurvivorTestHelpers.Step(sim, 2);
            Assert.That(sim.EndReason, Is.EqualTo(EndReason.Expired)); Assert.That(sim.Events, Has.None.Matches<SurvivorEvent>(e => e.Type == SurvivorEventType.HeroDied));
        }

        [Test]
        public void ShortRun_EndsWithTimeUp()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(runSeconds: 180f), 1); sim.SetTimeForTests(179.99f); sim.SetHeroInvulnerableForTests(); SurvivorTestHelpers.Step(sim, 2); Assert.That(sim.EndReason, Is.EqualTo(EndReason.TimeUp));
        }

        [Test]
        public void Sweep_ArcAndL5BackArc_OnCooldown()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 2); sim.SetHeroStateForTests(Vec2.Zero, Vec2.Zero, 0f);
            SurvivorEnemy front = sim.SpawnEnemyForTests(2, new Vec2(2, 0)); SurvivorEnemy back = sim.SpawnEnemyForTests(2, new Vec2(-2, 0)); sim.Step(default);
            Assert.That(front.Hp, Is.LessThan(front.MaxHp)); Assert.That(back.Hp, Is.EqualTo(back.MaxHp)); float hp = front.Hp; sim.Step(default); Assert.That(front.Hp, Is.EqualTo(hp));
            SurvivorSim maxed = new SurvivorSim(SurvivorTestHelpers.Config(), 2); maxed.GiveItemForTests(0, 5); SurvivorEnemy rear = maxed.SpawnEnemyForTests(2, new Vec2(-2, 0)); maxed.Step(default); Assert.That(rear.Hp, Is.LessThan(rear.MaxHp));
        }

        [Test]
        public void Hammer_PierceOne_NoFireWithoutTarget_CountByLevel()
        {
            SurvivorSim empty = new SurvivorSim(SurvivorTestHelpers.Config(), 4); empty.GiveItemForTests(3, 5); empty.Step(default); Assert.That(ActiveProjectiles(empty), Is.Zero);
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 4); sim.GiveItemForTests(3, 5); sim.SpawnEnemyForTests(2, new Vec2(6, 0)); sim.Step(default); Assert.That(ActiveProjectiles(sim), Is.EqualTo(3));
            SurvivorProjectile p = null; for (int i = 0; i < sim.Projectiles.Count; i++) if (sim.Projectiles[i].Active) { p = sim.Projectiles[i]; break; }
            Assert.That(p.PierceRemaining, Is.EqualTo(1));
        }

        [Test]
        public void Crit_And_Armor_Math()
        {
            SurvivorConfig config = SurvivorTestHelpers.Config(); config.Build.Points[(int)StatId.Crit] = 20; SurvivorSim sim = new SurvivorSim(config, 1); SurvivorEnemy target = sim.SpawnEnemyForTests(2, new Vec2(2, 0));
            sim.DamageEnemyForTests(target, 10f); Assert.That(target.MaxHp - target.Hp, Is.EqualTo(15f).Within(0.01f));
            SurvivorConfig armoredConfig = SurvivorTestHelpers.Config(); armoredConfig.Build.Points[(int)StatId.Armor] = 10; SurvivorSim armored = new SurvivorSim(armoredConfig, 1); SurvivorEnemy source = armored.SpawnEnemyForTests(0, new Vec2(0.8f, 0)); float before = armored.Hero.Hp; armored.DamageHeroForTests(1f, source); Assert.That(before - armored.Hero.Hp, Is.EqualTo(1f));
        }

        [Test]
        public void Crit_DoubledAgainstStunned()
        {
            SurvivorConfig config = SurvivorTestHelpers.Config(); config.Build.Points[(int)StatId.Crit] = 20; SurvivorSim sim = new SurvivorSim(config, 9); SurvivorEnemy target = sim.SpawnEnemyForTests(2, new Vec2(2, 0)); target.StunRemaining = 1f;
            sim.DamageEnemyForTests(target, 10f); Assert.That(target.MaxHp - target.Hp, Is.EqualTo(15f).Within(0.01f));
        }

        [Test]
        public void Block_FrontalContactHalved_ParryStunsBrute()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 2); sim.Step(new SurvivorInput(0, 2, 0)); SurvivorEnemy walker = sim.SpawnEnemyForTests(0, new Vec2(0.8f, 0)); float hp = sim.Hero.Hp; sim.DamageHeroForTests(8f, walker, true); Assert.That(hp - sim.Hero.Hp, Is.EqualTo(4f).Within(0.1f));
            SurvivorEnemy brute = sim.SpawnEnemyForTests(2, new Vec2(1, 0)); hp = sim.Hero.Hp; sim.DamageHeroForTests(25f, brute, false); Assert.That(sim.Hero.Hp, Is.EqualTo(hp)); Assert.That(brute.StunRemaining, Is.GreaterThan(0f));
        }

        [Test]
        public void Dash_PassesThroughEnemies_NotObstacles()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 1); sim.SpawnEnemyForTests(2, new Vec2(1f, 0)); sim.Step(new SurvivorInput(1, 3, 0)); SurvivorTestHelpers.Step(sim, 12, new SurvivorInput(1, 0, 0)); Assert.That(sim.Hero.Position.X, Is.GreaterThan(2f));
            SurvivorConfig blockedConfig = SurvivorTestHelpers.Config(); blockedConfig.ObstacleCount = 1; blockedConfig.ObstacleClearRadius = 0f; SurvivorSim blocked = new SurvivorSim(blockedConfig, 23); SurvivorObstacle obstacle = null; for (int i = 0; i < blocked.Obstacles.Count; i++) if (blocked.Obstacles[i].Active) obstacle = blocked.Obstacles[i];
            blocked.SetHeroStateForTests(obstacle.Position - new Vec2(obstacle.Radius + 0.6f, 0), Vec2.Zero, 0); blocked.Step(new SurvivorInput(1, 3, 0)); SurvivorTestHelpers.Step(blocked, 12, new SurvivorInput(1, 0, 0)); Assert.That(Vec2.Distance(blocked.Hero.Position, obstacle.Position), Is.GreaterThanOrEqualTo(blocked.Hero.Radius + obstacle.Radius - 0.01f));
        }

        [Test]
        public void Surrounded_HeroCannotPushThroughRing()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 2); sim.SetHeroInvulnerableForTests();
            for (int i = 0; i < 8; i++) { SurvivorEnemy e = sim.SpawnEnemyForTests(2, Vec2.FromAngle(i * MathF.PI / 4f) * 1.1f); e.StunRemaining = 10f; }
            SurvivorTestHelpers.Step(sim, 60, new SurvivorInput(1, 0, 0)); Assert.That(sim.Hero.Position.X, Is.LessThan(2f));
        }

        [Test]
        public void DeathCause_SurroundedBruteBossContact()
        {
            SurvivorSim surrounded = NewLethal(1); for (int i = 0; i < 6; i++) surrounded.SpawnEnemyForTests(0, Vec2.FromAngle(i * MathF.PI / 3f) * 0.8f); surrounded.DamageHeroForTests(1000f, surrounded.Enemies[0]); Assert.That(surrounded.DeathCause, Is.EqualTo(DeathCause.Surrounded));
            SurvivorSim brute = NewLethal(2); SurvivorEnemy b = brute.SpawnEnemyForTests(2, new Vec2(1, 0)); brute.DamageHeroForTests(1000f, b, false); Assert.That(brute.DeathCause, Is.EqualTo(DeathCause.Brute));
            SurvivorSim boss = NewLethal(3); SurvivorEnemy bo = boss.SpawnEnemyForTests(4, new Vec2(2, 0)); boss.DamageHeroForTests(1000f, bo, false); Assert.That(boss.DeathCause, Is.EqualTo(DeathCause.Boss));
            SurvivorSim contact = NewLethal(4); SurvivorEnemy c = contact.SpawnEnemyForTests(0, new Vec2(1, 0)); contact.DamageHeroForTests(1000f, c); Assert.That(contact.DeathCause, Is.EqualTo(DeathCause.Contact));
        }

        private static SurvivorSim NewLethal(int seed) { SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), seed); sim.SetHeroHpForTests(1f); return sim; }
        private static SurvivorEnemy FindEnemy(SurvivorSim sim, bool elite, bool boss) { for (int i = 0; i < sim.Enemies.Count; i++) if (sim.Enemies[i].Active && sim.Enemies[i].Elite == elite && sim.Enemies[i].IsBoss == boss) return sim.Enemies[i]; return null; }
        private static int ActiveProjectiles(SurvivorSim sim) { int count = 0; for (int i = 0; i < sim.Projectiles.Count; i++) if (sim.Projectiles[i].Active) count++; return count; }
    }
}
