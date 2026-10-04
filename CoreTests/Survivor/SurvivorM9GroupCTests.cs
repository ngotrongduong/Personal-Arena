using System;
using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    /// <summary>M9 group C (T-042): bounce-shot 64, momentum-spirit 65, time-clock 66, purge 67, bomb-ring 68.</summary>
    public sealed class SurvivorM9GroupCTests
    {
        private static readonly int[] NewWeapons = { 64, 65, 66, 67, 68 };

        // ---- catalog and pools ----

        [Test]
        public void Catalog_RowsMatchTheTaskTable()
        {
            ItemDef b = SurvivorCatalog.Get(64), m = SurvivorCatalog.Get(65), t = SurvivorCatalog.Get(66), p = SurvivorCatalog.Get(67), r = SurvivorCatalog.Get(68);
            Assert.That(b.Id, Is.EqualTo("bounce-shot")); Assert.That(b.Pattern, Is.EqualTo(WeaponPattern.Bounce)); Assert.That(b.BaseDamage, Is.EqualTo(12f)); Assert.That(b.DamagePerLevel, Is.EqualTo(4f));
            Assert.That(b.ProjectileSpeed, Is.EqualTo(10f)); Assert.That(b.ProjectileRadius, Is.EqualTo(0.35f)); Assert.That(b.ProjectileRange / b.ProjectileSpeed, Is.EqualTo(4f)); Assert.That(b.BaseCooldown, Is.EqualTo(2f));
            Assert.That(b.BouncesByLevel, Is.EqualTo(new[] { 3, 3, 4, 4, 5 })); Assert.That(b.CountByLevel, Is.EqualTo(new[] { 1, 1, 2, 2, 3 }));
            Assert.That(m.Id, Is.EqualTo("momentum-spirit")); Assert.That(m.Pattern, Is.EqualTo(WeaponPattern.Momentum)); Assert.That(m.BaseDamage, Is.EqualTo(10f)); Assert.That(m.DamagePerLevel, Is.EqualTo(4f)); Assert.That(m.ProjectileSpeed, Is.EqualTo(12f)); Assert.That(m.BaseCooldown, Is.EqualTo(1f)); Assert.That(m.CountByLevel, Is.EqualTo(new[] { 1, 2, 2, 3, 3 }));
            Assert.That(t.Id, Is.EqualTo("time-clock")); Assert.That(t.Pattern, Is.EqualTo(WeaponPattern.Freeze)); Assert.That(t.Chance, Is.EqualTo(0.3f)); Assert.That(t.ChancePerLevel, Is.EqualTo(0.1f)); Assert.That(t.StunSeconds, Is.EqualTo(1.2f)); Assert.That(t.StunPerLevel, Is.EqualTo(0.2f)); Assert.That(t.BaseCooldown, Is.EqualTo(6f)); Assert.That(t.CooldownPerLevel, Is.EqualTo(-0.5f)); Assert.That(t.BaseRange, Is.EqualTo(14f));
            Assert.That(p.Id, Is.EqualTo("purge")); Assert.That(p.Pattern, Is.EqualTo(WeaponPattern.Purge)); Assert.That(p.BaseCooldown, Is.EqualTo(30f)); Assert.That(p.CooldownPerLevel, Is.EqualTo(-2f)); Assert.That(p.BaseRange, Is.EqualTo(14f));
            Assert.That(r.Id, Is.EqualTo("bomb-ring")); Assert.That(r.Pattern, Is.EqualTo(WeaponPattern.BombRing)); Assert.That(r.CountByLevel, Is.EqualTo(new[] { 4, 4, 5, 5, 6 })); Assert.That(r.Width, Is.EqualTo(1.3f)); Assert.That(r.BaseDamage, Is.EqualTo(22f)); Assert.That(r.DamagePerLevel, Is.EqualTo(7f)); Assert.That(r.BaseCooldown, Is.EqualTo(3.5f)); Assert.That(r.BaseRange, Is.EqualTo(4f)); Assert.That(r.HitInterval, Is.EqualTo(0.15f));
            foreach (int index in NewWeapons) { ItemDef def = SurvivorCatalog.Get(index); Assert.That(def.Kind, Is.EqualTo(ItemKind.Weapon)); Assert.That(def.MaxLevel, Is.EqualTo(5)); Assert.That(def.CatalogIndex, Is.EqualTo(index)); Assert.That(def.Name, Is.Not.Empty); Assert.That(SurvivorCatalog.EvolutionOf(index), index == 67 ? Is.EqualTo(-1) : Is.GreaterThanOrEqualTo(112)); }
            Assert.That(SurvivorCatalog.Get(73), Is.Null); Assert.That(SurvivorObservation.Size, Is.EqualTo(2592)); Assert.That(SurvivorInput.SkillBranchSize, Is.EqualTo(7));
        }

        [Test]
        public void Pools_EachWeaponIsInTheRightClasses_AndNoClassHoldsTwoOfAPersistentPattern()
        {
            int[][] expected = { new[] { 64 }, new[] { 64, 66, 67, 68 }, new[] { 64, 65 } };
            SurvivorClassDef[] kits = { SurvivorDefaults.Warrior(), SurvivorDefaults.Mage(), SurvivorDefaults.Archer() };
            for (int k = 0; k < 3; k++)
                foreach (int weapon in NewWeapons) Assert.That(Array.IndexOf(kits[k].WeaponPool, weapon) >= 0, Is.EqualTo(Array.IndexOf(expected[k], weapon) >= 0), kits[k].Id + " " + weapon);
            foreach (SurvivorClassDef kit in kits)
            {
                int[] counts = new int[Enum.GetValues(typeof(WeaponPattern)).Length];
                foreach (int index in kit.WeaponPool) counts[(int)SurvivorCatalog.Get(index).Pattern]++;
                foreach (WeaponPattern pattern in new[] { WeaponPattern.Orbit, WeaponPattern.Aura, WeaponPattern.Shockwave, WeaponPattern.Combo, WeaponPattern.Retaliate, WeaponPattern.Barrier })
                    Assert.That(counts[(int)pattern], Is.LessThanOrEqualTo(kit.Id == "warrior" || pattern != WeaponPattern.Orbit ? 1 : 2), kit.Id + " " + pattern);
            }
            Assert.That(SurvivorDefaults.Mage().WeaponPool[0], Is.EqualTo(14)); Assert.That(SurvivorDefaults.Archer().WeaponPool[0], Is.EqualTo(20));
        }

        [Test]
        public void OldRules_ExcludeAllFiveFromEveryClassPool()
        {
            foreach (SurvivorClassDef kit in new[] { SurvivorDefaults.Warrior(), SurvivorDefaults.Mage(), SurvivorDefaults.Archer() })
            {
                SurvivorConfig old = SurvivorTestHelpers.OldConfig(); old.ClassDef = kit; SurvivorTestHelpers.OldRules(old);
                foreach (int index in NewWeapons) Assert.That(Array.IndexOf(old.ClassDef.WeaponPool, index), Is.LessThan(0), kit.Id + " " + index);
            }
        }

        [Test]
        public void Offers_EachClassCanSeeItsNewWeapons_AndOnlyThose()
        {
            SurvivorClassDef[] kits = { SurvivorDefaults.Warrior(), SurvivorDefaults.Mage(), SurvivorDefaults.Archer() };
            int[][] expected = { new[] { 64 }, new[] { 64, 66, 67, 68 }, new[] { 64, 65 } };
            for (int k = 0; k < 3; k++)
            {
                HashSet<int> seen = new HashSet<int>();
                for (int seed = 0; seed < 400; seed++)
                {
                    SurvivorConfig config = Cfg(); config.ClassDef = kits[k] = SurvivorDefaults.ForClass(kits[k].Id); config.ClassDef.CritChance = 0f;
                    SurvivorSim sim = new SurvivorSim(config, seed); sim.GiveXpForTests(5f); sim.Step(default);
                    for (int i = 0; i < sim.OfferCount; i++) seen.Add(sim.GetOffer(i).CatalogIndex);
                }
                foreach (int weapon in NewWeapons) Assert.That(seen.Contains(weapon), Is.EqualTo(Array.IndexOf(expected[k], weapon) >= 0), kits[k].Id + " " + weapon);
            }
        }

        [Test]
        public void SixPlusSixSlotsStillHold_WithTheNewWeaponsOwned()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                SurvivorSim sim = Sim(14, 1, seed, mage: true);
                foreach (int index in new[] { 64, 66, 67, 68, 15 }) sim.GiveItemForTests(index, 1);
                foreach (int index in new[] { 6, 7, 8, 9, 10, 11 }) sim.GiveItemForTests(index, 1);
                Assert.That(sim.Inventory.WeaponCount, Is.EqualTo(6)); Assert.That(sim.Inventory.PassiveCount, Is.EqualTo(6));
                for (int n = 0; n < 5; n++)
                {
                    sim.GiveXpForTests(500f); sim.Step(default);
                    for (int i = 0; i < sim.OfferCount; i++) { int index = sim.GetOffer(i).CatalogIndex; Assert.That(sim.Inventory.Level(index), Is.GreaterThan(0).Or.EqualTo(62).Or.EqualTo(63), "offer " + index + " would exceed the slots"); }
                    Step(sim, 3);
                    Assert.That(sim.Inventory.WeaponCount, Is.LessThanOrEqualTo(6)); Assert.That(sim.Inventory.PassiveCount, Is.LessThanOrEqualTo(6));
                }
            }
        }

        // ---- bounce shot ----

        [Test]
        public void Bounce_FiresCountPerLevelAtTheNearestEnemy_AndReportsTheCount()
        {
            int[] counts = { 1, 1, 2, 2, 3 };
            for (int level = 1; level <= 5; level++)
            {
                SurvivorSim sim = Sim(64, level, 300 + level); Brute(sim, 6f, 0f); Brute(sim, -9f, 0f);
                sim.Step(default); List<SurvivorProjectile> shots = Shots(sim, 64);
                Assert.That(shots.Count, Is.EqualTo(counts[level - 1]), "level " + level); Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 64).Extra, Is.EqualTo(counts[level - 1]));
                float sumY = 0f; foreach (SurvivorProjectile s in shots) { Assert.That(s.Velocity.X, Is.GreaterThan(0f)); Assert.That(s.Velocity.Length, Is.EqualTo(10f).Within(1e-3f)); Assert.That(s.Radius, Is.EqualTo(0.35f).Within(1e-5f)); Assert.That(s.Bounces, Is.Zero); sumY += s.Velocity.Y; }
                if (shots.Count % 2 == 1) Assert.That(sumY, Is.EqualTo(0f).Within(1e-3f));
                Assert.That(shots.Exists(shot => MathF.Abs(shot.Velocity.Y) < 1e-3f), Is.True, "one shot flies straight at the target (level " + level + ")");
            }
        }

        [Test]
        public void Bounce_NoEnemyInRange_DoesNotFireOrSpendTheCooldown()
        {
            SurvivorSim sim = Sim(64, 1, 310); Brute(sim, 20f, 0f); sim.Step(default);
            Assert.That(Shots(sim, 64), Is.Empty); Assert.That(sim.WeaponCooldownForTests(64), Is.Zero);
            Brute(sim, 5f, 0f); sim.Step(default); Assert.That(Shots(sim, 64).Count, Is.EqualTo(1)); Assert.That(sim.WeaponCooldownForTests(64), Is.EqualTo(2f).Within(0.02f));
        }

        [Test]
        public void Bounce_DamageGrowsPerLevel_AndDuplicatorAddsAShot()
        {
            for (int level = 1; level <= 5; level++)
            {
                SurvivorSim sim = Sim(64, level, 320); Brute(sim, 6f, 0f); sim.Step(default);
                foreach (SurvivorProjectile s in Shots(sim, 64)) Assert.That(s.Damage, Is.EqualTo(12f + 4f * (level - 1)));
            }
            SurvivorSim plain = Sim(64, 1, 321), doubled = Sim(64, 1, 321); doubled.GiveItemForTests(35, 1);
            Brute(plain, 6f, 0f); Brute(doubled, 6f, 0f); plain.Step(default); doubled.Step(default);
            Assert.That(Shots(doubled, 64).Count, Is.EqualTo(Shots(plain, 64).Count + 1));
        }

        [Test]
        public void Bounce_WallBouncesAreCounted_AndTheShotDiesWhenTheyAreUsedUp()
        {
            int[] allowed = { 3, 3, 4, 4, 5 };
            foreach (int level in new[] { 1, 3, 5 })
            {
                SurvivorSim sim = Sim(64, level, 330, half: 11f); sim.SetEnemiesInvulnerableForTests();
                sim.SetHeroStateForTests(new Vec2(9f, 0f), Vec2.Zero, 0f); Brute(sim, 10f, 0f);
                sim.Step(default); List<SurvivorProjectile> shots = Shots(sim, 64); Assert.That(shots.Count, Is.GreaterThan(0));
                foreach (SurvivorProjectile s in shots) s.Lifetime = 60f; // long enough that only the bounces can end it
                sim.SetWeaponCooldownForTests(64, 1000f);
                int max = 0; bool alive = true;
                for (int tick = 0; tick < 1500 && alive; tick++)
                {
                    sim.Step(default); alive = false;
                    foreach (SurvivorProjectile s in shots) { max = Math.Max(max, s.Bounces); if (s.Active) { alive = true; Assert.That(Math.Abs(s.Position.X), Is.LessThanOrEqualTo(11f)); Assert.That(Math.Abs(s.Position.Y), Is.LessThanOrEqualTo(11f)); } }
                }
                Assert.That(alive, Is.False, "level " + level); Assert.That(max, Is.EqualTo(allowed[level - 1]), "bounces at level " + level);
            }
        }

        [Test]
        public void Bounce_ReflectsOffAnObstacle()
        {
            for (int seed = 1; seed < 60; seed++)
            {
                SurvivorConfig config = Cfg(); config.ObstacleCount = 1; config.ObstacleClearRadius = 0f; config.ClassDef.StartingWeapon = 64;
                SurvivorSim sim = new SurvivorSim(config, seed); SurvivorObstacle o = null;
                for (int i = 0; i < sim.Obstacles.Count; i++) if (sim.Obstacles[i].Active) { o = sim.Obstacles[i]; break; }
                if (o == null || Math.Abs(o.Position.X - 6f) > 45f || Math.Abs(o.Position.Y) > 45f) continue;
                sim.SetHeroInvulnerableForTests(); sim.SetEnemiesInvulnerableForTests();
                sim.SetHeroStateForTests(new Vec2(o.Position.X - 6f, o.Position.Y), Vec2.Zero, 0f); Brute(sim, o.Position.X + 3f, o.Position.Y); // behind the obstacle: a shot that hit the enemy first would ricochet off it
                sim.Step(default); List<SurvivorProjectile> shots = Shots(sim, 64); Assert.That(shots.Count, Is.EqualTo(1));
                sim.SetWeaponCooldownForTests(64, 1000f);
                for (int tick = 0; tick < 60 && shots[0].Bounces == 0; tick++) sim.Step(default);
                Assert.That(shots[0].Bounces, Is.EqualTo(1)); Assert.That(shots[0].Active, Is.True);
                Assert.That(shots[0].Velocity.X, Is.LessThan(0f)); Assert.That(shots[0].Velocity.Y, Is.EqualTo(0f).Within(1e-3f)); Assert.That(shots[0].Velocity.Length, Is.EqualTo(10f).Within(1e-3f));
                Assert.That(Vec2.Distance(shots[0].Position, o.Position), Is.GreaterThanOrEqualTo(o.Radius + 0.35f - 1e-3f));
                return;
            }
            Assert.Inconclusive("no seed placed a usable obstacle");
        }

        [Test]
        public void Bounce_HittingALoneEnemyReflectsTheShot_WithoutUsingABounce()
        {
            SurvivorSim sim = Sim(64, 1, 340); SurvivorEnemy brute = Brute(sim, 6f, 0f); sim.Step(default);
            SurvivorProjectile shot = Shots(sim, 64)[0]; sim.SetWeaponCooldownForTests(64, 1000f); int hits = 0;
            for (int tick = 0; tick < 40 && hits == 0; tick++) { sim.Step(default); for (int e = 0; e < sim.Events.Count; e++) if (sim.Events[e].Type == SurvivorEventType.DamageDealt && sim.Events[e].Id == brute.Id) hits++; }
            Assert.That(hits, Is.EqualTo(1)); Assert.That(shot.Active, Is.True); Assert.That(shot.Bounces, Is.Zero);
            Assert.That(shot.Velocity.X, Is.LessThan(0f), "the shot bounces back off the enemy"); Assert.That(shot.Velocity.Length, Is.EqualTo(10f).Within(1e-3f));
        }

        [Test]
        public void Bounce_HittingAnEnemyTurnsTheShotTowardAnotherEnemy()
        {
            SurvivorSim sim = Sim(64, 1, 342); SurvivorEnemy first = Brute(sim, 5f, 0f); SurvivorEnemy second = Brute(sim, 5f, 6f); sim.Step(default);
            SurvivorProjectile shot = Shots(sim, 64)[0]; sim.SetWeaponCooldownForTests(64, 1000f); bool hitFirst = false, hitSecond = false;
            for (int tick = 0; tick < 120 && !hitSecond; tick++)
            {
                sim.Step(default);
                for (int e = 0; e < sim.Events.Count; e++)
                {
                    if (sim.Events[e].Type != SurvivorEventType.DamageDealt) continue;
                    if (sim.Events[e].Id == first.Id) hitFirst = true; else if (sim.Events[e].Id == second.Id) hitSecond = true;
                }
                if (hitFirst && !hitSecond && shot.Active) Assert.That(shot.Velocity.Y, Is.GreaterThan(0f), "after the first hit the shot heads for the second enemy");
            }
            Assert.That(hitFirst, Is.True); Assert.That(hitSecond, Is.True); Assert.That(shot.Bounces, Is.Zero);
        }

        [Test]
        public void Bounce_SameEnemyIsHitAtMostOncePerHalfSecondPerShot()
        {
            SurvivorSim sim = Sim(64, 1, 341, half: 11f); SurvivorEnemy brute = Brute(sim, 10.2f, 0f); sim.Step(default);
            Assert.That(Shots(sim, 64).Count, Is.EqualTo(1)); sim.SetWeaponCooldownForTests(64, 1000f);
            List<float> times = new List<float>(); int bounces = 0;
            for (int tick = 0; tick < 300; tick++)
            {
                sim.Step(default);
                for (int e = 0; e < sim.Events.Count; e++) if (sim.Events[e].Type == SurvivorEventType.DamageDealt && sim.Events[e].Id == brute.Id) times.Add(sim.Time);
                bounces = Math.Max(bounces, Shots(sim, 64).Count > 0 ? Shots(sim, 64)[0].Bounces : bounces);
            }
            Assert.That(times.Count, Is.GreaterThan(0)); Assert.That(bounces, Is.GreaterThan(0), "the shot should bounce back over the enemy");
            for (int i = 1; i < times.Count; i++) Assert.That(times[i] - times[i - 1], Is.GreaterThanOrEqualTo(0.5f - 0.02f));
        }

        [Test]
        public void Bounce_RehitMemory_NeverForgetsAnEnemyWhileItsTimerRuns()
        {
            // The memory once overwrote its oldest entry even while that timer was still running, so a crowd of more than
            // eight enemies inside 0.5 s let the first one be hit again. A full memory now refuses new hits instead.
            SurvivorSim sim = Sim(64, 1, 343); sim.SetTimeForTests(10f);
            SurvivorProjectile p = new SurvivorProjectile { Bouncing = true }; int capacity = p.BounceIds.Length;
            Assert.That(capacity, Is.GreaterThanOrEqualTo(16));
            for (int id = 1; id <= capacity; id++) Assert.That(sim.RecordBounceHit(p, id), Is.True, "slot for enemy " + id);
            Assert.That(sim.RecordBounceHit(p, capacity + 1), Is.False, "a full memory must not drop a live entry");
            for (int id = 1; id <= capacity; id++) Assert.That(sim.AlreadyHit(p, id), Is.True, "enemy " + id + " is still remembered");
            sim.SetTimeForTests(10f + SurvivorCatalog.BounceRehitSeconds);
            Assert.That(sim.AlreadyHit(p, 1), Is.False); Assert.That(sim.RecordBounceHit(p, capacity + 1), Is.True, "expired slots are reused");
        }

        // ---- momentum spirit ----

        [Test]
        public void Momentum_VolleyCountPerLevel_AlongFacingWhenStanding()
        {
            int[] counts = { 1, 2, 2, 3, 3 };
            for (int level = 1; level <= 5; level++)
            {
                SurvivorSim sim = Sim(65, level, 350 + level); sim.SetHeroStateForTests(Vec2.Zero, Vec2.Zero, MathF.PI / 2f);
                sim.Step(default); List<SurvivorProjectile> shots = Shots(sim, 65);
                Assert.That(shots.Count, Is.EqualTo(counts[level - 1])); Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 65).Extra, Is.EqualTo(counts[level - 1]));
                float sumX = 0f; foreach (SurvivorProjectile s in shots) { Assert.That(s.Velocity.Y, Is.GreaterThan(11f)); Assert.That(s.Velocity.Length, Is.EqualTo(12f).Within(1e-3f)); sumX += s.Velocity.X; }
                Assert.That(sumX, Is.EqualTo(0f).Within(1e-3f));
            }
        }

        [Test]
        public void Momentum_FiresAlongTheMovementDirection()
        {
            SurvivorSim sim = Sim(65, 1, 355); sim.SetWeaponCooldownForTests(65, 1000f);
            for (int i = 0; i < 40; i++) sim.Step(new SurvivorInput(3, 0, 0)); // move 3 = +y
            sim.SetWeaponCooldownForTests(65, 0f); sim.Step(new SurvivorInput(3, 0, 0));
            SurvivorProjectile shot = Shots(sim, 65)[0]; Assert.That(shot.Velocity.Y, Is.GreaterThan(11.9f)); Assert.That(Math.Abs(shot.Velocity.X), Is.LessThan(0.5f));
        }

        [Test]
        public void Momentum_DamageFollowsTheMovementFactor()
        {
            foreach (int level in new[] { 1, 4 })
            {
                float baseDamage = 10f + 4f * (level - 1), low = 0f, high = 0f;
                foreach (float factor in new[] { 0f, 0.5f, 1f })
                {
                    SurvivorSim sim = Sim(65, level, 356); sim.SetMovementFactorForTests(factor); sim.Step(default);
                    SurvivorProjectile shot = Shots(sim, 65)[0];
                    Assert.That(shot.Damage, Is.EqualTo(baseDamage * (0.4f + 1.2f * sim.MovementFactor)).Within(1e-3f)); Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 65).Value, Is.EqualTo(sim.MovementFactor).Within(1e-6f));
                    if (factor == 0f) low = shot.Damage; if (factor == 1f) high = shot.Damage;
                }
                Assert.That(low, Is.EqualTo(baseDamage * 0.4f).Within(1e-3f)); Assert.That(high, Is.EqualTo(baseDamage * 1.6f * (1f - 1f / 90f) + baseDamage * 0.4f * (1f / 90f)).Within(0.2f));
                Assert.That(high, Is.GreaterThan(low * 3.5f));
            }
        }

        [Test]
        public void Momentum_FactorIsAnEmaOfMoving_WithTimeConstant1p5()
        {
            SurvivorSim still = Sim(65, 1, 357); still.SetWeaponCooldownForTests(65, 1e6f); Step(still, 300);
            Assert.That(still.MovementFactor, Is.Zero);
            SurvivorSim sim = Sim(65, 1, 358); sim.SetWeaponCooldownForTests(65, 1e6f); float previous = 0f;
            for (int i = 0; i < 90; i++) { sim.Step(new SurvivorInput(1, 0, 0)); Assert.That(sim.MovementFactor, Is.GreaterThanOrEqualTo(previous)); previous = sim.MovementFactor; }
            Assert.That(sim.MovementFactor, Is.InRange(0.60f, 0.66f), "1 - 1/e after one time constant");
            for (int i = 0; i < 210; i++) sim.Step(new SurvivorInput(1 + i / 40 % 8, 0, 0)); Assert.That(sim.MovementFactor, Is.InRange(0.95f, 1f));
            float top = sim.MovementFactor; for (int i = 0; i < 90; i++) sim.Step(default); Assert.That(sim.MovementFactor, Is.InRange(top * 0.34f, top * 0.40f), "decays to about 1/e");
        }

        [Test]
        public void Momentum_FiresWithNoEnemyAround_AndHonoursItsCooldown()
        {
            SurvivorSim sim = Sim(65, 1, 359); sim.Step(default); Assert.That(Shots(sim, 65).Count, Is.EqualTo(1)); Assert.That(sim.WeaponCooldownForTests(65), Is.EqualTo(1f).Within(0.02f));
        }

        // ---- time clock ----

        [Test]
        public void TimeClock_StunsEveryNonBossInRange_ElitesForHalf_BossImmune_FarOnesUntouched()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                SurvivorSim sim = Sim(66, 5, seed); // level 5: 70%, stun 2.0 s
                SurvivorEnemy near = sim.SpawnEnemyForTests(0, new Vec2(13.5f, 0f)), elite = sim.SpawnEnemyForTests(0, new Vec2(0f, 8f), true), far = sim.SpawnEnemyForTests(0, new Vec2(-16f, 0f)), boss = sim.SpawnBossForTests(new Vec2(0f, -9f));
                sim.Step(default); bool fired = false; for (int e = 0; e < sim.Events.Count; e++) if (sim.Events[e].Type == SurvivorEventType.WeaponFired && sim.Events[e].Id == 66) fired = true;
                if (!fired) continue;
                Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 66).Value, Is.EqualTo(2.0f).Within(1e-5f)); Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 66).Extra, Is.EqualTo(2f));
                Assert.That(near.StunRemaining, Is.InRange(1.9f, 2.0f)); Assert.That(elite.StunRemaining, Is.InRange(0.9f, 1.0f)); Assert.That(far.StunRemaining, Is.Zero); Assert.That(boss.StunRemaining, Is.Zero);
                Assert.That(sim.WeaponCooldownForTests(66), Is.EqualTo(4f).Within(0.02f)); return;
            }
            Assert.Fail("never triggered in 200 seeds at 70%");
        }

        [Test]
        public void TimeClock_ChanceGrowsPerLevel_AndFailedRollStillSpendsTheCooldown()
        {
            float[] rate = new float[2]; int[] levels = { 1, 5 };
            for (int n = 0; n < 2; n++)
            {
                SurvivorSim sim = Sim(66, levels[n], 370); SurvivorEnemy walker = sim.SpawnEnemyForTests(0, new Vec2(5f, 0f)); int triggered = 0, trials = 800;
                for (int i = 0; i < trials; i++)
                {
                    sim.SetWeaponCooldownForTests(66, 0f); walker.StunRemaining = 0f; sim.Step(default); bool fired = false;
                    for (int e = 0; e < sim.Events.Count; e++) if (sim.Events[e].Type == SurvivorEventType.WeaponFired && sim.Events[e].Id == 66) fired = true;
                    if (fired) triggered++;
                    Assert.That(sim.WeaponCooldownForTests(66), Is.GreaterThan(3f), "cooldown spent on success and on failure");
                }
                rate[n] = triggered / (float)trials;
            }
            Assert.That(rate[0], Is.InRange(0.24f, 0.36f)); Assert.That(rate[1], Is.InRange(0.64f, 0.76f));
        }

        [Test]
        public void TimeClock_StunAndCooldownScalePerLevel_AndDurationCharmLengthensTheStun()
        {
            float[] stun = { 1.2f, 1.4f, 1.6f, 1.8f, 2.0f }, cooldown = { 6f, 5.5f, 5f, 4.5f, 4f };
            for (int level = 1; level <= 5; level++) Assert.That(TriggerStun(level, 0), Is.EqualTo(stun[level - 1]).Within(1e-4f));
            Assert.That(TriggerStun(1, 3), Is.EqualTo(1.2f * 1.3f).Within(1e-4f));
            for (int level = 1; level <= 5; level++) { SurvivorSim sim = Sim(66, level, 371); sim.SpawnEnemyForTests(0, new Vec2(5f, 0f)); sim.Step(default); Assert.That(sim.WeaponCooldownForTests(66), Is.EqualTo(cooldown[level - 1]).Within(0.02f)); }
        }

        [Test]
        public void TimeClock_NeedsAnEnemyInRange_ElseKeepsItsCooldown()
        {
            SurvivorSim sim = Sim(66, 5, 372); sim.SpawnEnemyForTests(0, new Vec2(15f, 0f)); sim.Step(default);
            Assert.That(sim.WeaponCooldownForTests(66), Is.Zero);
        }

        // ---- purge ----

        [Test]
        public void Purge_KillsNormalEnemiesInRange_ElitesAndBossLoseFivePercent_FarOnesLive()
        {
            SurvivorSim sim = Sim(67, 1, 380);
            SurvivorEnemy a = sim.SpawnEnemyForTests(0, new Vec2(13f, 0f)), b = sim.SpawnEnemyForTests(2, new Vec2(0f, 9f)), c = sim.SpawnEnemyForTests(3, new Vec2(-5f, -5f));
            SurvivorEnemy far = sim.SpawnEnemyForTests(0, new Vec2(-16f, 0f)), elite = sim.SpawnEnemyForTests(2, new Vec2(6f, 6f), true), boss = sim.SpawnBossForTests(new Vec2(0f, -10f));
            float eliteMax = elite.MaxHp, bossMax = boss.MaxHp; int kills = sim.Kills;
            sim.Step(default);
            Assert.That(a.Active, Is.False); Assert.That(b.Active, Is.False); Assert.That(c.Active, Is.False); Assert.That(far.Active, Is.True);
            Assert.That(elite.Active, Is.True); Assert.That(elite.Hp, Is.EqualTo(eliteMax * 0.95f).Within(0.01f)); Assert.That(boss.Active, Is.True); Assert.That(boss.Hp, Is.EqualTo(bossMax * 0.95f).Within(0.1f));
            Assert.That(sim.Kills - kills, Is.EqualTo(3)); Assert.That(sim.BossDamageFraction, Is.EqualTo(0.05f).Within(1e-3f)); Assert.That(sim.IsEnded, Is.False);
            Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 67).Extra, Is.EqualTo(3f)); Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 67).Value, Is.EqualTo(14f).Within(1e-4f));
            SurvivorEvent landed = Find(sim.Events, SurvivorEventType.StrikeLanded, 67); Assert.That(landed.Value, Is.EqualTo(14f).Within(1e-4f));
            int killedEvents = 0; for (int e = 0; e < sim.Events.Count; e++) if (sim.Events[e].Type == SurvivorEventType.EnemyKilled) killedEvents++; Assert.That(killedEvents, Is.EqualTo(3));
            int gems = 0; for (int i = 0; i < sim.Pickups.Count; i++) if (sim.Pickups[i].Active && sim.Pickups[i].Kind == PickupKind.Gem) gems++; Assert.That(gems, Is.GreaterThan(0), "kills drop XP gems like any kill");
        }

        [Test]
        public void Purge_RadiusGrowsWithArea_AndCooldownScalesPerLevel()
        {
            SurvivorSim plain = Sim(67, 1, 381), charmed = Sim(67, 1, 381); charmed.GiveItemForTests(11, 5); // area +40% -> 19.6 m
            SurvivorEnemy p = plain.SpawnEnemyForTests(0, new Vec2(17f, 0f)), c = charmed.SpawnEnemyForTests(0, new Vec2(17f, 0f));
            plain.Step(default); charmed.Step(default);
            Assert.That(p.Active, Is.True); Assert.That(plain.WeaponCooldownForTests(67), Is.Zero, "no enemy in range: no fire, no cooldown"); Assert.That(c.Active, Is.False);
            Assert.That(Find(charmed.Events, SurvivorEventType.StrikeLanded, 67).Value, Is.EqualTo(14f * 1.4f).Within(1e-3f));
            float[] cooldown = { 30f, 28f, 26f, 24f, 22f };
            for (int level = 1; level <= 5; level++) { SurvivorSim sim = Sim(67, level, 382); sim.SpawnEnemyForTests(0, new Vec2(5f, 0f)); sim.Step(default); Assert.That(sim.WeaponCooldownForTests(67), Is.EqualTo(cooldown[level - 1]).Within(0.02f)); }
        }

        [Test]
        public void Purge_RespectsTheCooldown_AndKillsAgainWhenReady()
        {
            SurvivorSim sim = Sim(67, 1, 383); SurvivorEnemy first = sim.SpawnEnemyForTests(0, new Vec2(5f, 0f)); sim.Step(default); Assert.That(first.Active, Is.False);
            SurvivorEnemy second = sim.SpawnEnemyForTests(0, new Vec2(5f, 1f)); sim.Step(default); sim.Step(default); Assert.That(second.Active, Is.True, "on cooldown");
            sim.SetWeaponCooldownForTests(67, 0f); sim.Step(default); Assert.That(second.Active, Is.False);
        }

        [Test]
        public void Purge_NeverKillsTheBossOrAnEliteEvenAcrossManyCasts()
        {
            SurvivorSim sim = Sim(67, 1, 384); SurvivorEnemy boss = sim.SpawnBossForTests(new Vec2(0f, 5f)), elite = sim.SpawnEnemyForTests(2, new Vec2(4f, 0f), true);
            boss.StunRemaining = 0f; elite.StunRemaining = 0f;
            for (int i = 0; i < 10; i++) { sim.SetWeaponCooldownForTests(67, 0f); sim.Step(default); }
            Assert.That(boss.Active, Is.True); Assert.That(boss.Hp, Is.LessThan(boss.MaxHp * 0.65f)); Assert.That(sim.IsEnded, Is.False);
        }

        // ---- bomb ring ----

        [Test]
        public void BombRing_BlastCountPositionsAndTimingMatchTheTable()
        {
            int[] counts = { 4, 4, 5, 5, 6 };
            for (int level = 1; level <= 5; level++)
            {
                SurvivorSim sim = Sim(68, level, 390 + level); sim.SetEnemiesInvulnerableForTests(); Brute(sim, 3f, 0f);
                List<Vec2> points = new List<Vec2>(); List<int> ticks = new List<int>(); int n = counts[level - 1];
                for (int tick = 1; tick <= 120; tick++)
                {
                    sim.Step(default); if (tick == 1) Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 68).Extra, Is.EqualTo(n));
                    for (int e = 0; e < sim.Events.Count; e++) if (sim.Events[e].Type == SurvivorEventType.StrikeLanded && sim.Events[e].Id == 68) { points.Add(sim.Events[e].Point); ticks.Add(tick); Assert.That(sim.Events[e].Value, Is.EqualTo(1.3f).Within(1e-5f)); }
                    if (tick == 60) Assert.That(points.Count, Is.EqualTo(n), "one salvo only before the cooldown ends");
                    if (tick >= 60) break;
                }
                Assert.That(points.Count, Is.EqualTo(n));
                for (int k = 0; k < n; k++)
                {
                    Assert.That(points[k].Length, Is.EqualTo(4f).Within(1e-3f)); float angle = MathF.PI * 2f * k / n;
                    Assert.That(points[k].X, Is.EqualTo(4f * MathF.Cos(angle)).Within(1e-3f)); Assert.That(points[k].Y, Is.EqualTo(4f * MathF.Sin(angle)).Within(1e-3f));
                    Assert.That(ticks[k] - ticks[0], Is.InRange(9 * k - 1, 9 * k + 1), "one blast every 0.15 s");
                }
            }
        }

        [Test]
        public void BombRing_RingTurnsByAFixedStepEverySalvo()
        {
            SurvivorSim sim = Sim(68, 1, 395); sim.SetEnemiesInvulnerableForTests(); Brute(sim, 3f, 0f);
            List<Vec2> firsts = new List<Vec2>();
            for (int salvo = 0; salvo < 3; salvo++)
            {
                sim.SetWeaponCooldownForTests(68, 0f); bool got = false;
                for (int tick = 0; tick < 60; tick++) { sim.Step(default); for (int e = 0; e < sim.Events.Count; e++) if (sim.Events[e].Type == SurvivorEventType.StrikeLanded && sim.Events[e].Id == 68 && !got) { firsts.Add(sim.Events[e].Point); got = true; } }
                Assert.That(got, Is.True);
            }
            float step = MathF.PI / 6f;
            for (int s = 0; s < 3; s++) { Assert.That(MathF.Atan2(firsts[s].Y, firsts[s].X), Is.EqualTo(step * s).Within(1e-3f), "salvo " + s); Assert.That(firsts[s].Length, Is.EqualTo(4f).Within(1e-3f)); }
        }

        [Test]
        public void BombRing_DamagePerLevel_AreaScalesTheRing_AndBlastHitsEnemiesOnIt()
        {
            for (int level = 1; level <= 5; level++)
            {
                SurvivorSim sim = Sim(68, level, 396); SurvivorEnemy brute = Brute(sim, 4f, 0f); float hp = brute.Hp; sim.Step(default);
                Assert.That(hp - brute.Hp, Is.EqualTo(22f + 7f * (level - 1)).Within(1e-3f), "level " + level);
            }
            SurvivorSim big = Sim(68, 1, 397); big.GiveItemForTests(11, 5); big.SetEnemiesInvulnerableForTests(); Brute(big, 4f, 0f); big.Step(default);
            SurvivorEvent landed = Find(big.Events, SurvivorEventType.StrikeLanded, 68); Assert.That(landed.Point.Length, Is.EqualTo(5.6f).Within(1e-3f)); Assert.That(landed.Value, Is.EqualTo(1.3f * 1.4f).Within(1e-3f));
        }

        [Test]
        public void BombRing_DuplicatorAddsBlasts_AndAFullBufferSkipsWithoutSpendingTheCooldown()
        {
            SurvivorSim sim = Sim(68, 5, 398); sim.GiveItemForTests(35, 2); sim.SetEnemiesInvulnerableForTests(); Brute(sim, 3f, 0f);
            sim.Step(default); Assert.That(Find(sim.Events, SurvivorEventType.WeaponFired, 68).Extra, Is.EqualTo(8f)); Assert.That(sim.PendingBlastCount, Is.EqualTo(7), "the first blast lands at once");
            sim.SetWeaponCooldownForTests(68, 0f); sim.Step(default);
            Assert.That(sim.WeaponCooldownForTests(68), Is.Zero, "no room for a new salvo: skipped, cooldown kept"); Assert.That(sim.PendingBlastCount, Is.EqualTo(7), "the old salvo goes on untouched");
            sim.SetWeaponCooldownForTests(68, 1000f); for (int i = 0; i < 100; i++) sim.Step(default);
            Assert.That(sim.PendingBlastCount, Is.EqualTo(0));
        }

        [Test]
        public void BombRing_NeedsAnEnemyNearTheRing()
        {
            SurvivorSim sim = Sim(68, 1, 399); Brute(sim, 12f, 0f); sim.Step(default);
            Assert.That(sim.PendingBlastCount, Is.Zero); Assert.That(sim.WeaponCooldownForTests(68), Is.Zero);
        }

        // ---- whole kit: determinism, allocation, old rules ----

        [Test]
        public void GroupC_RunsDeterministically()
        {
            Assert.That(RunHash(401), Is.EqualTo(RunHash(401))); Assert.That(RunHash(401), Is.Not.EqualTo(RunHash(402)));
        }

        [Test]
        public void GroupC_StepAndObservationAllocateNothing()
        {
            SurvivorSim sim = FullKitSim(403); SurvivorObservation observation = new SurvivorObservation(); float[] values = new float[SurvivorObservation.Size];
            for (int i = 0; i < 900; i++) { sim.Step(new SurvivorInput(i / 20 % 9, 0, 0)); observation.Write(sim, values); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 900; i < 2400; i++) sim.Step(new SurvivorInput(i / 20 % 9, 0, 0));
            long stepAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 100; i++) observation.Write(sim, values); long writeAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(sim.IsEnded, Is.False); Assert.That(stepAllocated, Is.EqualTo(0L), "Step"); Assert.That(writeAllocated, Is.EqualTo(0L), "Write");
        }

        [Test]
        public void GroupC_EveryWeaponActsInTheFullKitRun()
        {
            SurvivorSim sim = FullKitSim(404); HashSet<int> fired = new HashSet<int>(); int landed68 = 0, bounces = 0, kills = 0;
            for (int i = 0; i < 2700; i++)
            {
                sim.Step(new SurvivorInput(i / 20 % 9, 0, 0));
                for (int e = 0; e < sim.Events.Count; e++)
                {
                    if (sim.Events[e].Type == SurvivorEventType.WeaponFired) fired.Add(sim.Events[e].Id);
                    if (sim.Events[e].Type == SurvivorEventType.StrikeLanded && sim.Events[e].Id == 68) landed68++;
                }
                for (int p = 0; p < sim.Projectiles.Count; p++) if (sim.Projectiles[p].Active && sim.Projectiles[p].SourceIndex == 64) bounces = Math.Max(bounces, sim.Projectiles[p].Bounces);
                kills = sim.Kills;
            }
            foreach (int index in NewWeapons) Assert.That(fired.Contains(index), Is.True, "weapon " + index);
            Assert.That(landed68, Is.GreaterThan(10)); Assert.That(bounces, Is.GreaterThan(0));
        }

        [Test]
        public void GroupC_NotOwned_TheRunIsBitIdenticalToTheOldRules()
        {
            SurvivorConfig now = SurvivorTestHelpers.PreM11Drops(SurvivorTestHelpers.Config()); now.ClassDef = SurvivorDefaults.Mage();
            SurvivorConfig old = SurvivorTestHelpers.Config(); old.ClassDef = SurvivorDefaults.Mage(); SurvivorTestHelpers.OldRules(old);
            SurvivorSim a = new SurvivorSim(now, 405), b = new SurvivorSim(old, 405); a.DisablePickupCollectionForTests(); b.DisablePickupCollectionForTests();
            for (int tick = 0; tick < 1500; tick++)
            {
                SurvivorInput input = new SurvivorInput(tick / 30 % 9, 0, 0); a.Step(input); b.Step(input);
                Assert.That(SurvivorM5GoldenTests.StateHash(a), Is.EqualTo(SurvivorM5GoldenTests.StateHash(b)), "tick " + tick);
            }
        }

        // ---- helpers ----

        private static SurvivorConfig Cfg(float half = 50f)
        {
            SurvivorConfig config = SurvivorTestHelpers.Config(); config.ClassDef.CritChance = 0f; config.OpeningRing = 0; config.MapHalfSize = half; return config;
        }

        /// <summary>A hero with <paramref name="item"/> as its weapon at <paramref name="level"/>, no opening ring, no crits, invulnerable.</summary>
        private static SurvivorSim Sim(int item, int level, int seed, float half = 50f, bool mage = false)
        {
            SurvivorConfig config = Cfg(half); if (mage) { config.ClassDef = SurvivorDefaults.Mage(); config.ClassDef.CritChance = 0f; } else config.ClassDef.StartingWeapon = item;
            SurvivorSim sim = new SurvivorSim(config, seed); if (level != 1) sim.GiveItemForTests(item, level);
            sim.SetHeroInvulnerableForTests(); return sim;
        }

        private static SurvivorEnemy Brute(SurvivorSim sim, float x, float y) { SurvivorEnemy enemy = sim.SpawnEnemyForTests(2, new Vec2(x, y)); enemy.StunRemaining = 1000f; return enemy; }

        private static List<SurvivorProjectile> Shots(SurvivorSim sim, int source)
        {
            List<SurvivorProjectile> list = new List<SurvivorProjectile>();
            for (int i = 0; i < sim.Projectiles.Count; i++) if (sim.Projectiles[i].Active && sim.Projectiles[i].SourceIndex == source) list.Add(sim.Projectiles[i]);
            return list;
        }

        private static void Step(SurvivorSim sim, int ticks) { for (int i = 0; i < ticks; i++) sim.Step(default); }

        private static float TriggerStun(int level, int charmLevel)
        {
            for (int seed = 0; seed < 300; seed++)
            {
                SurvivorSim sim = Sim(66, level, seed); if (charmLevel > 0) sim.GiveItemForTests(34, charmLevel); sim.GiveItemForTests(66, level);
                sim.SpawnEnemyForTests(0, new Vec2(5f, 0f)); sim.SetWeaponCooldownForTests(66, 0f);
                for (int i = 0; i < 40; i++) { sim.SetWeaponCooldownForTests(66, 0f); sim.Step(default); for (int e = 0; e < sim.Events.Count; e++) if (sim.Events[e].Type == SurvivorEventType.WeaponFired && sim.Events[e].Id == 66) return sim.Events[e].Value; }
            }
            Assert.Fail("time clock never triggered"); return 0f;
        }

        private static SurvivorSim FullKitSim(int seed)
        {
            SurvivorConfig config = Cfg(15f); config.ClassDef.MaxHp = 1e7f; config.ClassDef.StartingWeapon = 64; SurvivorSim sim = new SurvivorSim(config, seed);
            sim.SetEnemiesInvulnerableForTests(); sim.DisablePickupCollectionForTests();
            foreach (int index in NewWeapons) sim.GiveItemForTests(index, 5);
            foreach (int index in new[] { 34, 35, 36, 37 }) sim.GiveItemForTests(index, index == 35 ? 2 : 3);
            for (int i = 0; i < 40; i++) sim.SpawnEnemyForTests(i % 4, Vec2.FromAngle(i * 0.7f) * (2f + i % 7));
            return sim;
        }

        private static long RunHash(int seed)
        {
            SurvivorConfig config = Cfg(); config.OpeningRing = 6; SurvivorSim sim = new SurvivorSim(config, seed); long hash = 17;
            foreach (int index in NewWeapons) sim.GiveItemForTests(index, 4);
            for (int tick = 0; tick < 3600 && !sim.IsEnded; tick++)
            {
                SurvivorInput input = sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1 + tick % 3) : new SurvivorInput((tick / 40) % 9, (tick / 90) % 4, 0);
                sim.Step(input); hash = hash * 31 + SurvivorM5GoldenTests.StateHash(sim); hash = hash * 31 + BitConverter.SingleToInt32Bits(sim.MovementFactor);
                for (int i = 0; i < sim.Projectiles.Count; i++) if (sim.Projectiles[i].Active) hash = hash * 31 + sim.Projectiles[i].Bounces;
                hash = hash * 31 + sim.PendingBlastCount;
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
