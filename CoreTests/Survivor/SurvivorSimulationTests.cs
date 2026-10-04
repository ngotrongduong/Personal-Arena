using System;
using NUnit.Framework;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    public sealed class SurvivorSimulationTests
    {
        [Test]
        public void OpeningRing_SpawnsWeakEnemiesEvenlyAtSixMetres()
        {
            SurvivorConfig config = SurvivorTestHelpers.Config(3);
            config.OpeningRing = 16;

            SurvivorSim sim = new SurvivorSim(config, 17);

            Assert.That(sim.AliveEnemyCount, Is.EqualTo(16));
            for (int i = 0; i < sim.Enemies.Count; i++)
            {
                SurvivorEnemy enemy = sim.Enemies[i];
                if (!enemy.Active) continue;
                Assert.That(enemy.TypeIndex, Is.Zero);
                Assert.That(Vec2.Distance(enemy.Position, sim.Hero.Position), Is.EqualTo(6f).Within(1e-4f));
                Assert.That(enemy.MaxHp, Is.EqualTo(15f * 1.7f).Within(1e-4f));
            }
        }

        [Test]
        public void OpeningRing_ZeroLeavesResetEmpty()
        {
            SurvivorConfig config = SurvivorTestHelpers.Config();
            config.OpeningRing = 0;

            SurvivorSim sim = new SurvivorSim(config, 17);

            Assert.That(sim.AliveEnemyCount, Is.Zero);
        }

        [Test]
        public void OpeningRing_SameSeedProducesTheSameReset()
        {
            SurvivorConfig firstConfig = new SurvivorConfig { OpeningRing = 16, ObstacleCount = 40 };
            SurvivorConfig secondConfig = new SurvivorConfig { OpeningRing = 16, ObstacleCount = 40 };
            SurvivorSim first = new SurvivorSim(firstConfig, 2026);
            SurvivorSim second = new SurvivorSim(secondConfig, 2026);

            Assert.That(second.AliveEnemyCount, Is.EqualTo(first.AliveEnemyCount));
            for (int i = 0; i < first.Enemies.Count; i++)
            {
                Assert.That(second.Enemies[i].Active, Is.EqualTo(first.Enemies[i].Active));
                if (!first.Enemies[i].Active) continue;
                Assert.That(second.Enemies[i].Position.X, Is.EqualTo(first.Enemies[i].Position.X));
                Assert.That(second.Enemies[i].Position.Y, Is.EqualTo(first.Enemies[i].Position.Y));
                Assert.That(second.Enemies[i].Hp, Is.EqualTo(first.Enemies[i].Hp));
            }
        }

        [Test]
        public void Spawn_FirstMinuteOnlyWalkers_AndRespectsMax()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 10); sim.SetHeroInvulnerableForTests();
            SurvivorTestHelpers.Step(sim, 59 * 60);
            Assert.That(sim.AliveEnemyCount, Is.LessThanOrEqualTo(30));
            for (int i = 0; i < sim.Enemies.Count; i++) if (sim.Enemies[i].Active) Assert.That(sim.Enemies[i].TypeIndex, Is.EqualTo(0));
            sim.SetTimeForTests(200f); SurvivorTestHelpers.Step(sim, 600);
            for (int i = 0; i < sim.Enemies.Count; i++) if (sim.Enemies[i].Active) Assert.That(sim.Enemies[i].TypeIndex, Is.AnyOf(0, 1, 2, SurvivorDefaults.ChargerTypeIndex));
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
            // The sword throws a wave along the facing: it reaches the front enemy a few ticks later and cuts it once.
            SurvivorEnemy front = sim.SpawnEnemyForTests(2, new Vec2(2, 0)); SurvivorEnemy back = sim.SpawnEnemyForTests(2, new Vec2(-2, 0)); sim.SetHeroInvulnerableForTests(); SurvivorTestHelpers.Step(sim, 10);
            Assert.That(front.Hp, Is.LessThan(front.MaxHp)); Assert.That(back.Hp, Is.EqualTo(back.MaxHp)); float hp = front.Hp; SurvivorTestHelpers.Step(sim, 10); Assert.That(front.Hp, Is.EqualTo(hp));
            SurvivorSim maxed = new SurvivorSim(SurvivorTestHelpers.Config(), 2); maxed.SetHeroInvulnerableForTests(); maxed.GiveItemForTests(0, 5); SurvivorEnemy rear = maxed.SpawnEnemyForTests(2, new Vec2(-2, 0)); SurvivorTestHelpers.Step(maxed, 10); Assert.That(rear.Hp, Is.LessThan(rear.MaxHp));
        }

        [Test]
        public void SwordWave_CutsEveryEnemyOnItsPathOnce_AndStopsAtItsRange()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 2); sim.SetHeroInvulnerableForTests(); sim.SetHeroStateForTests(Vec2.Zero, Vec2.Zero, 0f);
            SurvivorEnemy near = sim.SpawnEnemyForTests(2, new Vec2(2.5f, 0f)); SurvivorEnemy mid = sim.SpawnEnemyForTests(2, new Vec2(4.5f, 0f)); SurvivorEnemy side = sim.SpawnEnemyForTests(2, new Vec2(3f, 4f));
            SurvivorEnemy beyond = sim.SpawnEnemyForTests(3, new Vec2(12f, 0f)); beyond.AttackCooldown = 100f;
            sim.Step(default); Assert.That(ActiveProjectiles(sim), Is.EqualTo(1), "one wave at level 1");
            SurvivorProjectile wave = null; for (int i = 0; i < sim.Projectiles.Count; i++) if (sim.Projectiles[i].Active) wave = sim.Projectiles[i];
            Assert.That(wave.SourceIndex, Is.EqualTo(0)); Assert.That(wave.Radius, Is.EqualTo(1.1f).Within(1e-4f)); Assert.That(wave.Velocity.X, Is.EqualTo(12f).Within(1e-3f));
            SurvivorTestHelpers.Step(sim, 40);
            // Brutes have 80 health and the wave deals 20: both in the lane were cut exactly once, the others never.
            Assert.That(near.MaxHp - near.Hp, Is.EqualTo(mid.MaxHp - mid.Hp).Within(1e-3f)); Assert.That(near.Hp, Is.LessThan(near.MaxHp));
            Assert.That(side.Hp, Is.EqualTo(side.MaxHp)); Assert.That(beyond.Hp, Is.EqualTo(beyond.MaxHp));
            Assert.That(wave.Active, Is.False, "the wave ends after its range");
        }

        [Test]
        public void SwordWave_RangeGrowsWithLevel_AndL5ThrowsASecondWaveBackwards()
        {
            SurvivorSim low = new SurvivorSim(SurvivorTestHelpers.Config(), 2); low.SetHeroInvulnerableForTests(); low.SetHeroStateForTests(Vec2.Zero, Vec2.Zero, 0f); low.Step(default);
            SurvivorSim high = new SurvivorSim(SurvivorTestHelpers.Config(), 2); high.SetHeroInvulnerableForTests(); high.SetHeroStateForTests(Vec2.Zero, Vec2.Zero, 0f); high.GiveItemForTests(0, 5); high.Step(default);
            Assert.That(ActiveProjectiles(low), Is.EqualTo(1)); Assert.That(ActiveProjectiles(high), Is.EqualTo(2));
            float lowLife = 0f, highLife = 0f; bool forward = false, backward = false;
            for (int i = 0; i < low.Projectiles.Count; i++) if (low.Projectiles[i].Active) lowLife = low.Projectiles[i].Lifetime;
            for (int i = 0; i < high.Projectiles.Count; i++)
            {
                SurvivorProjectile p = high.Projectiles[i]; if (!p.Active) continue;
                highLife = p.Lifetime; if (p.Velocity.X > 0f) forward = true; else backward = true;
            }
            Assert.That(forward && backward, Is.True); Assert.That(highLife, Is.EqualTo(lowLife * 1.4f).Within(0.03f));
        }

        [Test]
        public void Hammer_PierceOne_NoFireWithoutTarget_CountByLevel()
        {
            SurvivorSim empty = new SurvivorSim(SurvivorTestHelpers.Config(), 4); empty.GiveItemForTests(3, 5); empty.Step(default); Assert.That(ActiveProjectiles(empty, 3), Is.Zero);
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 4); sim.GiveItemForTests(3, 5); sim.SpawnEnemyForTests(2, new Vec2(6, 0)); sim.Step(default); Assert.That(ActiveProjectiles(sim, 3), Is.EqualTo(3));
            SurvivorProjectile p = null; for (int i = 0; i < sim.Projectiles.Count; i++) if (sim.Projectiles[i].Active && sim.Projectiles[i].SourceIndex == 3) { p = sim.Projectiles[i]; break; }
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

        [Test]
        public void Won_BossKilledWhileLethalDamageSameTick_StaysWon()
        {
            SurvivorSim sim = BossAndLethalWalker(bossHp: 1f);
            sim.Step(default);
            Assert.That(sim.EndReason, Is.EqualTo(EndReason.Won)); Assert.That(sim.Hero.Alive, Is.True); Assert.That(sim.DeathCause, Is.EqualTo(DeathCause.None));
            Assert.That(sim.Events, Has.None.Matches<SurvivorEvent>(e => e.Type == SurvivorEventType.HeroDied || e.Type == SurvivorEventType.HeroDamaged || e.Type == SurvivorEventType.EnemySpawned));
            SurvivorRewardConfig rewards = new SurvivorRewardConfig();
            Assert.That(new SurvivorRewardCalculator(rewards).Compute(sim.Events, sim.LastStepSeconds), Is.GreaterThanOrEqualTo(rewards.Win));
            sim.Step(default); Assert.That(sim.Events, Is.Empty); Assert.That(sim.EndReason, Is.EqualTo(EndReason.Won));
            // Control: without the kill the same walker is lethal on that tick.
            SurvivorSim control = BossAndLethalWalker(bossHp: float.MaxValue);
            control.Step(default); Assert.That(control.EndReason, Is.EqualTo(EndReason.Died));
        }

        [Test]
        public void BossSpawn_PoolFull_RetriesEachTick_ThenSpawns()
        {
            SurvivorSim sim = FullPool(21);
            sim.SetTimeForTests(899.99f); SurvivorTestHelpers.Step(sim, 2);
            Assert.That(sim.BossAlive, Is.False); Assert.That(sim.IsEnded, Is.False);
            SurvivorTestHelpers.Step(sim, 3); Assert.That(sim.BossAlive, Is.False);
            sim.DamageEnemyForTests(FindEnemy(sim, false, false), 1e6f); sim.Step(default);
            Assert.That(sim.BossAlive, Is.True);
        }

        [Test]
        public void BossSpawn_NeverPossible_EndsExpired()
        {
            SurvivorSim sim = FullPool(22);
            sim.SetTimeForTests(899.99f); SurvivorTestHelpers.Step(sim, 2); sim.SetTimeForTests(1019.99f); SurvivorTestHelpers.Step(sim, 2);
            Assert.That(sim.BossAlive, Is.False); Assert.That(sim.EndReason, Is.EqualTo(EndReason.Expired));
        }

        [Test]
        public void Knockback_BackArcSweep_PushesEnemyAwayFromHero()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 2); sim.SetHeroInvulnerableForTests(); sim.GiveItemForTests(0, 5);
            sim.SetHeroStateForTests(Vec2.Zero, Vec2.Zero, 0f);
            SurvivorEnemy rear = sim.SpawnEnemyForTests(2, new Vec2(-2f, 0f));
            // The backward sword wave reaches it a few ticks later and pushes it along its flight (-x), away from the hero.
            float before = rear.Position.X;
            for (int i = 0; i < 30 && rear.Hp >= rear.MaxHp; i++) { before = rear.Position.X; sim.Step(default); }
            Assert.That(rear.Hp, Is.LessThan(rear.MaxHp));
            Assert.That(rear.Position.X, Is.LessThan(before - 0.1f)); Assert.That(MathF.Abs(rear.Position.Y), Is.LessThan(0.2f));
        }

        [Test]
        public void Knockback_Hammer_PushesAlongFlight()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 3); sim.SetHeroInvulnerableForTests(); sim.GiveItemForTests(3, 1);
            SurvivorEnemy target = sim.SpawnEnemyForTests(0, new Vec2(0f, 6f)); target.StunRemaining = 10f;
            for (int tick = 0; tick < 60 && target.Hp >= target.MaxHp; tick++) sim.Step(default);
            Assert.That(target.Hp, Is.LessThan(target.MaxHp));
            Assert.That(target.Position.Y, Is.GreaterThan(6.4f)); Assert.That(MathF.Abs(target.Position.X), Is.LessThan(0.01f));
        }

        [Test]
        public void Hammer_PierceOne_HitsExactlyTwoEnemies()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 6); sim.SetHeroInvulnerableForTests(); sim.GiveItemForTests(3, 1);
            SurvivorEnemy first = sim.SpawnEnemyForTests(2, new Vec2(6f, 0f)); SurvivorEnemy second = sim.SpawnEnemyForTests(2, new Vec2(7.5f, 0f)); SurvivorEnemy third = sim.SpawnEnemyForTests(2, new Vec2(9f, 0f));
            int thrown = 0; System.Collections.Generic.HashSet<int> hit = new System.Collections.Generic.HashSet<int>();
            for (int tick = 0; tick < 60; tick++)
            {
                sim.Step(default);
                for (int i = 0; i < sim.Events.Count; i++)
                {
                    SurvivorEvent e = sim.Events[i];
                    if (e.Type == SurvivorEventType.WeaponFired && e.Id == 3) thrown++;
                    if (e.Type == SurvivorEventType.DamageDealt) hit.Add(e.Id);
                }
            }
            Assert.That(thrown, Is.EqualTo(1)); Assert.That(hit, Is.EquivalentTo(new[] { first.Id, second.Id })); Assert.That(third.Hp, Is.EqualTo(third.MaxHp));
        }

        [Test]
        public void Hammer_WeaponFiredPoint_IsThrowDirection()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 7); sim.SetHeroInvulnerableForTests(); sim.GiveItemForTests(3, 1);
            sim.SpawnEnemyForTests(0, new Vec2(0f, 6f)); sim.Step(default);
            SurvivorEvent fired = default; bool found = false;
            for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.WeaponFired && sim.Events[i].Id == 3) { fired = sim.Events[i]; found = true; }
            Assert.That(found, Is.True); Assert.That(fired.Point.X, Is.EqualTo(0f).Within(1e-4f)); Assert.That(fired.Point.Y, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Crit_RollBetweenChanceAndDouble_CritsOnlyWhenStunned()
        {
            // Seed whose first roll r satisfies p <= r < 2p: a normal hit must not crit, a stunned hit must.
            float p = new SurvivorSim(SurvivorTestHelpers.Config(), 0).DerivedStats.CritChance;
            int seed = -1;
            for (int candidate = 1; candidate < 100000 && seed < 0; candidate++) { float r = new Rng(candidate).NextFloat(); if (r >= p && r < p * 2f) seed = candidate; }
            Assert.That(seed, Is.GreaterThan(0));
            SurvivorSim normal = new SurvivorSim(SurvivorTestHelpers.Config(), seed); SurvivorEnemy a = normal.SpawnEnemyForTests(2, new Vec2(3, 0));
            normal.DamageEnemyForTests(a, 10f); Assert.That(a.MaxHp - a.Hp, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(normal.Events, Has.None.Matches<SurvivorEvent>(e => e.Type == SurvivorEventType.Crit));
            SurvivorSim stunned = new SurvivorSim(SurvivorTestHelpers.Config(), seed); SurvivorEnemy b = stunned.SpawnEnemyForTests(2, new Vec2(3, 0)); b.StunRemaining = 1f;
            stunned.DamageEnemyForTests(b, 10f); Assert.That(b.MaxHp - b.Hp, Is.EqualTo(15f).Within(1e-4f));
            Assert.That(stunned.Events, Has.Some.Matches<SurvivorEvent>(e => e.Type == SurvivorEventType.Crit));
        }

        [Test]
        public void Separation_HeroAndElite_UsesMaxEnemyRadius_VelocityIsActualMotion()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 3); sim.SetHeroInvulnerableForTests(); sim.SetEnemiesInvulnerableForTests();
            sim.SetHeroStateForTests(new Vec2(0.7f, 0f), Vec2.Zero, 0f);
            SurvivorEnemy elite = sim.SpawnEnemyForTests(2, new Vec2(2.2f, 0f), true); elite.StunRemaining = 10f;
            float needed = sim.Hero.Radius + elite.Radius;
            Assert.That(needed, Is.GreaterThan(1.5f));
            sim.Step(default);
            Assert.That(Vec2.Distance(sim.Hero.Position, elite.Position), Is.GreaterThanOrEqualTo(needed - 1e-3f));
            Assert.That(sim.Hero.Position.X, Is.LessThan(0.7f));
            Assert.That(sim.Hero.Velocity.X, Is.EqualTo((sim.Hero.Position.X - 0.7f) / SurvivorSim.FixedDeltaTime).Within(1e-3f));
        }

        [Test]
        public void Separation_EnemiesReclampedToMap()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 3); sim.SetHeroInvulnerableForTests(); sim.SetEnemiesInvulnerableForTests();
            sim.SetHeroStateForTests(new Vec2(44f, 0f), Vec2.Zero, 0f); // within relocation distance of both brutes
            SurvivorEnemy outer = sim.SpawnEnemyForTests(2, new Vec2(49.3f, 0f)); SurvivorEnemy inner = sim.SpawnEnemyForTests(2, new Vec2(48.5f, 0f));
            outer.StunRemaining = 10f; inner.StunRemaining = 10f;
            sim.Step(default);
            Assert.That(outer.Position.X, Is.LessThanOrEqualTo(50f - outer.Radius + 1e-4f)); Assert.That(inner.Position.X, Is.LessThan(48.5f));
        }

        [Test]
        public void LastSkill_OnlyAppliedSkills()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.OldConfig(), 3);
            sim.Step(new SurvivorInput(0, 1, 0)); Assert.That(sim.LastSkill, Is.EqualTo(1));
            sim.Step(new SurvivorInput(0, 1, 0)); Assert.That(sim.LastSkill, Is.EqualTo(0), "kick on cooldown is not applied");
            sim.Step(new SurvivorInput(0, 4, 0)); Assert.That(sim.LastSkill, Is.EqualTo(0), "empty slot is masked");
            sim.Step(new SurvivorInput(0, 2, 0)); sim.Step(new SurvivorInput(0, 2, 0)); Assert.That(sim.LastSkill, Is.EqualTo(2), "held block counts");
            sim.Step(default); Assert.That(sim.LastSkill, Is.EqualTo(0));
        }

        private static SurvivorSim BossAndLethalWalker(float bossHp)
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 11); sim.SetTimeForTests(950f);
            sim.SetHeroStateForTests(Vec2.Zero, Vec2.Zero, 0f); sim.SetHeroHpForTests(1f);
            SurvivorEnemy boss = sim.SpawnEnemyForTests(4, new Vec2(3f, 0f)); boss.Hp = MathF.Min(boss.MaxHp, bossHp);
            sim.SpawnEnemyForTests(0, new Vec2(-0.9f, 0f));
            return sim;
        }

        private static SurvivorSim FullPool(int seed)
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), seed); sim.SetHeroInvulnerableForTests();
            for (int i = 0; i < SurvivorSim.EnemyCapacity; i++) Assert.That(sim.SpawnEnemyForTests(0, Vec2.FromAngle(i * 2.399963f) * (30f + i % 15)), Is.Not.Null);
            Assert.That(sim.SpawnEnemyForTests(0, new Vec2(40f, 0f)), Is.Null);
            return sim;
        }

        private static SurvivorSim NewLethal(int seed) { SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), seed); sim.SetHeroHpForTests(1f); return sim; }
        private static SurvivorEnemy FindEnemy(SurvivorSim sim, bool elite, bool boss) { for (int i = 0; i < sim.Enemies.Count; i++) if (sim.Enemies[i].Active && sim.Enemies[i].Elite == elite && sim.Enemies[i].IsBoss == boss) return sim.Enemies[i]; return null; }
        private static int ActiveProjectiles(SurvivorSim sim) { int count = 0; for (int i = 0; i < sim.Projectiles.Count; i++) if (sim.Projectiles[i].Active) count++; return count; }
        private static int ActiveProjectiles(SurvivorSim sim, int source) { int count = 0; for (int i = 0; i < sim.Projectiles.Count; i++) if (sim.Projectiles[i].Active && sim.Projectiles[i].SourceIndex == source) count++; return count; }
    }
}
