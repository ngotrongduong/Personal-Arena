using System;
using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    /// <summary>
    /// M9 active skills 5 and 6 (T-043): warrior leap-slam / whirlwind, archer caltrop-trap / arrow-barrage, mage fire-wall / chain-lightning.
    /// Input skill value = slot + 1, so slot 4 is input 5 and slot 5 is input 6.
    /// </summary>
    public sealed class SurvivorM9SkillsTests
    {
        private const float Dt = SurvivorSim.FixedDeltaTime;
        private static readonly string[] Classes = { "warrior", "mage", "archer" };

        // ---- helpers ----

        private static SurvivorConfig Cfg(string classId = "warrior")
        {
            SurvivorConfig config = SurvivorTestHelpers.Config(); if (classId != "warrior") config.ClassDef = SurvivorDefaults.ForClass(classId);
            config.ClassDef.CritChance = 0f; config.OpeningRing = 0; config.MapHalfSize = 50f; return config;
        }

        /// <summary>Quiet hero (starting weapon on a 1000 s cooldown), no opening ring, no crits, no obstacles, invulnerable unless asked otherwise.</summary>
        private static SurvivorSim Sim(string classId = "warrior", int seed = 1, bool invulnerable = true)
        {
            SurvivorConfig config = Cfg(classId); SurvivorSim sim = new SurvivorSim(config, seed);
            sim.SetWeaponCooldownForTests(config.ClassDef.StartingWeapon, 1000f); if (invulnerable) sim.SetHeroInvulnerableForTests(); return sim;
        }

        /// <summary>A brute with a million HP; stunned (so it stands still) unless asked otherwise.</summary>
        private static SurvivorEnemy Dummy(SurvivorSim sim, float x, float y, bool stunned = true, int type = 2)
        {
            SurvivorEnemy e = sim.SpawnEnemyForTests(type, new Vec2(x, y)); e.MaxHp = 1e6f; e.Hp = 1e6f; e.StunRemaining = stunned ? 1000f : 0f; return e;
        }

        private static void Ready(SurvivorSim sim, int slot) { sim.Hero.SkillCooldowns[slot] = 0f; sim.Hero.Energy = sim.Hero.MaxEnergy; }

        private static void Cast(SurvivorSim sim, int slot, int move = 0) { Ready(sim, slot); sim.Step(new SurvivorInput(move, slot + 1, 0)); }

        private static void Steps(SurvivorSim sim, int ticks, SurvivorInput input = default) { for (int i = 0; i < ticks; i++) sim.Step(input); }

        private sealed class Tally
        {
            public readonly Dictionary<int, float> Damage = new Dictionary<int, float>();
            public readonly Dictionary<int, int> Hits = new Dictionary<int, int>();
            public int Strikes;
            public float Dealt(SurvivorEnemy e) => Damage.TryGetValue(e.Id, out float v) ? v : 0f;
            public int HitCount(SurvivorEnemy e) => Hits.TryGetValue(e.Id, out int v) ? v : 0;
        }

        private static void Collect(SurvivorSim sim, Tally tally)
        {
            for (int i = 0; i < sim.Events.Count; i++)
            {
                SurvivorEvent e = sim.Events[i];
                if (e.Type == SurvivorEventType.StrikeLanded && e.Id == -1) tally.Strikes++;
                if (e.Type != SurvivorEventType.DamageDealt) continue;
                tally.Damage[e.Id] = (tally.Damage.TryGetValue(e.Id, out float v) ? v : 0f) + e.Value;
                tally.Hits[e.Id] = (tally.Hits.TryGetValue(e.Id, out int n) ? n : 0) + 1;
            }
        }

        private static Tally Run(SurvivorSim sim, int ticks, SurvivorInput input = default) { Tally t = new Tally(); for (int i = 0; i < ticks; i++) { sim.Step(input); Collect(sim, t); } return t; }

        private static SurvivorEvent? FindStrike(SurvivorSim sim)
        {
            for (int i = 0; i < sim.Events.Count; i++) if (sim.Events[i].Type == SurvivorEventType.StrikeLanded && sim.Events[i].Id == -1) return sim.Events[i];
            return null;
        }

        private static int ActiveTraps(SurvivorSim sim) { int n = 0; for (int i = 0; i < sim.Traps.Count; i++) if (sim.Traps[i].Active) n++; return n; }
        private static int ActiveWalls(SurvivorSim sim) { int n = 0; for (int i = 0; i < sim.Walls.Count; i++) if (sim.Walls[i].Active) n++; return n; }

        private static float[] Observe(SurvivorSim sim) { float[] values = new float[SurvivorObservation.Size]; new SurvivorObservation().Write(sim, values); return values; }

        // ---- definitions ----

        [Test]
        public void SkillKind_NewMembersAreAppendedAtTheEnd()
        {
            Assert.That((int)SkillKind.Teleport, Is.EqualTo(7));
            Assert.That((int)SkillKind.Leap, Is.EqualTo(8)); Assert.That((int)SkillKind.Whirlwind, Is.EqualTo(9)); Assert.That((int)SkillKind.Trap, Is.EqualTo(10));
            Assert.That((int)SkillKind.Barrage, Is.EqualTo(11)); Assert.That((int)SkillKind.Wall, Is.EqualTo(12)); Assert.That((int)SkillKind.Chain, Is.EqualTo(13));
        }

        [Test]
        public void Kits_SlotsFourAndFiveMatchTheTaskTable()
        {
            SkillDef leap = SurvivorDefaults.Warrior().ActiveSkills[4], whirl = SurvivorDefaults.Warrior().ActiveSkills[5];
            Assert.That(leap.Id, Is.EqualTo("leap-slam")); Assert.That(leap.Kind, Is.EqualTo(SkillKind.Leap)); Assert.That(leap.DashDistance, Is.EqualTo(5f)); Assert.That(leap.Range, Is.EqualTo(7f)); Assert.That(leap.DashSpeed, Is.EqualTo(20f));
            Assert.That(leap.AreaRadius, Is.EqualTo(2.8f)); Assert.That(leap.Damage, Is.EqualTo(40f)); Assert.That(leap.StunSeconds, Is.EqualTo(0.6f)); Assert.That(leap.Knockback, Is.EqualTo(2.5f)); Assert.That(leap.Cooldown, Is.EqualTo(8f)); Assert.That(leap.EnergyCost, Is.EqualTo(30f));
            Assert.That(whirl.Id, Is.EqualTo("whirlwind")); Assert.That(whirl.Kind, Is.EqualTo(SkillKind.Whirlwind)); Assert.That(whirl.Duration, Is.EqualTo(1.5f)); Assert.That(whirl.TickSeconds, Is.EqualTo(0.15f)); Assert.That(whirl.AreaRadius, Is.EqualTo(2.4f));
            Assert.That(whirl.Damage, Is.EqualTo(12f)); Assert.That(whirl.Knockback, Is.EqualTo(0.5f)); Assert.That(whirl.SlowFactor, Is.EqualTo(0.7f)); Assert.That(whirl.Cooldown, Is.EqualTo(9f)); Assert.That(whirl.EnergyCost, Is.EqualTo(30f));
            SkillDef trap = SurvivorDefaults.Archer().ActiveSkills[4], barrage = SurvivorDefaults.Archer().ActiveSkills[5];
            Assert.That(trap.Id, Is.EqualTo("caltrop-trap")); Assert.That(trap.Kind, Is.EqualTo(SkillKind.Trap)); Assert.That(trap.AreaRadius, Is.EqualTo(2f)); Assert.That(trap.Duration, Is.EqualTo(6f)); Assert.That(trap.TickSeconds, Is.EqualTo(0.5f));
            Assert.That(trap.Damage, Is.EqualTo(6f)); Assert.That(trap.SlowFactor, Is.EqualTo(0.6f)); Assert.That(trap.MaxAlive, Is.EqualTo(2)); Assert.That(trap.Cooldown, Is.EqualTo(10f)); Assert.That(trap.EnergyCost, Is.EqualTo(20f));
            Assert.That(barrage.Id, Is.EqualTo("arrow-barrage")); Assert.That(barrage.Kind, Is.EqualTo(SkillKind.Barrage)); Assert.That(barrage.Count, Is.EqualTo(9)); Assert.That(barrage.ArcDegrees, Is.EqualTo(50f)); Assert.That(barrage.Range, Is.EqualTo(14f));
            Assert.That(barrage.Damage, Is.EqualTo(18f)); Assert.That(barrage.ProjectileSpeed, Is.EqualTo(16f)); Assert.That(barrage.Pierce, Is.True); Assert.That(barrage.Cooldown, Is.EqualTo(7f)); Assert.That(barrage.EnergyCost, Is.EqualTo(25f));
            SkillDef wall = SurvivorDefaults.Mage().ActiveSkills[4], chain = SurvivorDefaults.Mage().ActiveSkills[5];
            Assert.That(wall.Id, Is.EqualTo("fire-wall")); Assert.That(wall.Kind, Is.EqualTo(SkillKind.Wall)); Assert.That(wall.Width, Is.EqualTo(6f)); Assert.That(wall.Depth, Is.EqualTo(1.2f)); Assert.That(wall.Range, Is.EqualTo(3f)); Assert.That(wall.Duration, Is.EqualTo(4f));
            Assert.That(wall.TickSeconds, Is.EqualTo(0.4f)); Assert.That(wall.Damage, Is.EqualTo(8f)); Assert.That(wall.MaxAlive, Is.EqualTo(2)); Assert.That(wall.Cooldown, Is.EqualTo(12f)); Assert.That(wall.EnergyCost, Is.EqualTo(35f));
            Assert.That(chain.Id, Is.EqualTo("chain-lightning")); Assert.That(chain.Kind, Is.EqualTo(SkillKind.Chain)); Assert.That(chain.Range, Is.EqualTo(10f)); Assert.That(chain.JumpRange, Is.EqualTo(5f)); Assert.That(chain.Count, Is.EqualTo(6));
            Assert.That(chain.Falloff, Is.EqualTo(0.85f)); Assert.That(chain.Damage, Is.EqualTo(35f)); Assert.That(chain.StunSeconds, Is.EqualTo(0.3f)); Assert.That(chain.Cooldown, Is.EqualTo(6f)); Assert.That(chain.EnergyCost, Is.EqualTo(30f));
        }

        [Test]
        public void KitsKeepTheirFirstFourSkills_AndEverySlotIsNonNull()
        {
            Assert.That(SurvivorDefaults.Warrior().ActiveSkills[3].Id, Is.EqualTo("war-cry")); Assert.That(SurvivorDefaults.Mage().ActiveSkills[3].Id, Is.EqualTo("frost-burst")); Assert.That(SurvivorDefaults.Archer().ActiveSkills[3].Kind, Is.EqualTo(SkillKind.None));
            foreach (string id in Classes) { SurvivorClassDef kit = SurvivorDefaults.ForClass(id); Assert.That(kit.ActiveSkills.Length, Is.EqualTo(6)); foreach (SkillDef s in kit.ActiveSkills) Assert.That(s, Is.Not.Null); }
        }

        [Test]
        public void OldRules_SetSlotsFourAndFiveToNone_ForEveryClass_AndTheMaskBlocksThem()
        {
            foreach (string id in Classes)
            {
                SurvivorConfig config = Cfg(id); SurvivorTestHelpers.OldRules(config);
                Assert.That(config.ClassDef.ActiveSkills[4].Kind, Is.EqualTo(SkillKind.None), id); Assert.That(config.ClassDef.ActiveSkills[5].Kind, Is.EqualTo(SkillKind.None), id);
                SurvivorSim sim = new SurvivorSim(config, 3); bool[] move = new bool[9], skill = new bool[7], pick = new bool[5];
                SurvivorActionMask.WriteMask(sim, move, skill, pick); Assert.That(skill[5], Is.False, id); Assert.That(skill[6], Is.False, id);
                Steps(sim, 2, new SurvivorInput(0, 5, 0)); Steps(sim, 2, new SurvivorInput(0, 6, 0)); Assert.That(sim.SkillUses[4], Is.Zero, id); Assert.That(sim.SkillUses[5], Is.Zero, id);
                Assert.That(ActiveTraps(sim) + ActiveWalls(sim), Is.Zero, id);
            }
        }

        // ---- leap slam ----

        [Test]
        public void Leap_GoesTowardTheNearestEnemy_StopsAtBodyContact_AndBlastsOnLanding()
        {
            SurvivorSim sim = Sim(); SurvivorEnemy target = Dummy(sim, 6f, 0f), near = Dummy(sim, 6f, 2.4f), far = Dummy(sim, 6f, -5f);
            Ready(sim, 4); sim.Step(new SurvivorInput(0, 5, 0));
            Assert.That(sim.Hero.Dashing, Is.True, "airborne after the cast"); Assert.That(target.Hp, Is.EqualTo(1e6f), "no blast before landing"); Assert.That(FindStrike(sim), Is.Null);
            int guard = 0; while (sim.Hero.Dashing && guard++ < 60) sim.Step(default);
            Assert.That(sim.Hero.Dashing, Is.False); SurvivorEvent? strike = FindStrike(sim); Assert.That(strike.HasValue, Is.True, "StrikeLanded on the landing tick"); Assert.That(strike.Value.Value, Is.EqualTo(2.8f).Within(1e-4f));
            Assert.That(sim.Hero.Position.X, Is.EqualTo(6f - 0.5f - 0.7f).Within(0.05f), "stops where the bodies touch"); Assert.That(sim.Hero.Position.Y, Is.EqualTo(0f).Within(0.05f));
            Assert.That(guard, Is.InRange(12, 16), "5 m at 20 m/s is about 15 ticks");
            Assert.That(target.Hp, Is.EqualTo(1e6f - 40f)); Assert.That(near.Hp, Is.EqualTo(1e6f - 40f), "inside 2.8 m + radius"); Assert.That(far.Hp, Is.EqualTo(1e6f), "outside the blast");
            Assert.That(target.Position.X, Is.GreaterThan(6f), "knocked away");
            Assert.That(sim.SkillUses[4], Is.EqualTo(1)); Assert.That(sim.Hero.SkillCooldowns[4], Is.GreaterThan(7f));
        }

        [Test]
        public void Leap_StunsEnemiesForPointSixSeconds()
        {
            SurvivorSim sim = Sim(); SurvivorEnemy e = Dummy(sim, 5f, 0f, false);
            Cast(sim, 4); int guard = 0; while (sim.Hero.Dashing && guard++ < 60) sim.Step(default);
            Assert.That(e.StunRemaining, Is.InRange(0.5f, 0.6f)); Steps(sim, 40); Assert.That(e.StunRemaining, Is.Zero);
        }

        [Test]
        public void Leap_ExactlyFiveMetresToANearTarget_WhenItIsFartherThanTheLeap()
        {
            SurvivorSim sim = Sim(); Dummy(sim, 7f, 0f); Cast(sim, 4); int guard = 0; while (sim.Hero.Dashing && guard++ < 60) sim.Step(default);
            Assert.That(sim.Hero.Position.X, Is.EqualTo(5f).Within(0.05f), "5 m leap, target 7 m away");
        }

        [Test]
        public void Leap_WithoutATarget_UsesTheMoveDirection_ElseTheFacing()
        {
            SurvivorSim sim = Sim(); Cast(sim, 4, 3); int guard = 0; while (sim.Hero.Dashing && guard++ < 60) sim.Step(new SurvivorInput(3, 0, 0));
            Assert.That(sim.Hero.Position.Y, Is.GreaterThan(4.5f)); Assert.That(Math.Abs(sim.Hero.Position.X), Is.LessThan(0.5f), "move 3 = +y");
            SurvivorSim facing = Sim(); facing.SetHeroStateForTests(Vec2.Zero, Vec2.Zero, 0f); Cast(facing, 4); guard = 0; while (facing.Hero.Dashing && guard++ < 60) facing.Step(default);
            Assert.That(facing.Hero.Position.X, Is.EqualTo(5f).Within(0.05f)); Assert.That(facing.Hero.Position.Y, Is.EqualTo(0f).Within(0.05f));
        }

        [Test]
        public void Leap_IgnoresEnemiesBeyondSevenMetres()
        {
            SurvivorSim sim = Sim(); Dummy(sim, 0f, -8f); Cast(sim, 4, 1); int guard = 0; while (sim.Hero.Dashing && guard++ < 60) sim.Step(default);
            Assert.That(sim.Hero.Position.X, Is.EqualTo(5f).Within(0.05f)); Assert.That(Math.Abs(sim.Hero.Position.Y), Is.LessThan(0.05f), "kept the move direction");
        }

        [Test]
        public void Leap_BlocksEverySkillWhileAirborne_ThenFreesTheOthers()
        {
            SurvivorSim sim = Sim(); Dummy(sim, 6f, 0f); bool[] move = new bool[9], skill = new bool[7], pick = new bool[5];
            Cast(sim, 4); Assert.That(sim.Hero.Dashing, Is.True);
            SurvivorActionMask.WriteMask(sim, move, skill, pick); for (int i = 1; i < 7; i++) Assert.That(skill[i], Is.False, "skill " + i + " while leaping");
            Assert.That(skill[0], Is.True);
            sim.Hero.Energy = sim.Hero.MaxEnergy; sim.Step(new SurvivorInput(0, 6, 0)); Assert.That(sim.SkillUses[5], Is.Zero, "whirlwind refused mid-air"); Assert.That(sim.Whirling, Is.False);
            int guard = 0; while (sim.Hero.Dashing && guard++ < 60) sim.Step(default);
            sim.Hero.Energy = sim.Hero.MaxEnergy; SurvivorActionMask.WriteMask(sim, move, skill, pick); Assert.That(skill[6], Is.True, "whirlwind free after landing"); Assert.That(skill[5], Is.False, "leap on cooldown");
        }

        // ---- whirlwind ----

        [Test]
        public void Whirlwind_TicksTenTimesInOnePointFiveSeconds_TwelveDamageEach()
        {
            SurvivorSim sim = Sim(); SurvivorEnemy e = Dummy(sim, 1.5f, 0f); SurvivorEnemy outside = Dummy(sim, 4f, 0f);
            Tally t = new Tally(); Ready(sim, 5); sim.Step(new SurvivorInput(0, 6, 0)); Collect(sim, t);
            Assert.That(sim.Whirling, Is.True); Assert.That(t.Strikes, Is.Zero, "first tick comes after 0.15 s");
            for (int i = 1; i < 8; i++) { sim.Step(default); Collect(sim, t); } Assert.That(t.Strikes, Is.Zero, "nothing before the 9th tick");
            sim.Step(default); Collect(sim, t); Assert.That(t.Strikes, Is.EqualTo(1), "first tick at 0.15 s"); Assert.That(t.HitCount(e), Is.EqualTo(1));
            for (int i = 9; i < 100; i++) { sim.Step(default); Collect(sim, t); }
            Assert.That(t.Strikes, Is.EqualTo(10)); Assert.That(t.HitCount(e), Is.EqualTo(10)); Assert.That(t.Dealt(e), Is.EqualTo(120f).Within(0.01f)); Assert.That(e.Hp, Is.EqualTo(1e6f - 120f));
            Assert.That(sim.Whirling, Is.False, "spin over after 1.5 s"); Assert.That(t.HitCount(outside), Is.Zero, "4 m is outside 2.4 m + radius");
            Assert.That(e.Position.X, Is.GreaterThan(1.5f), "small knockback");
            Assert.That(sim.SkillUses[5], Is.EqualTo(1)); Assert.That(sim.Hero.SkillCooldowns[5], Is.GreaterThan(7f));
        }

        [Test]
        public void Whirlwind_StrikeEventsCarryTheRadius()
        {
            SurvivorSim sim = Sim(); Dummy(sim, 1.5f, 0f); Cast(sim, 5); SurvivorEvent? found = null; for (int i = 0; i < 12 && !found.HasValue; i++) { sim.Step(default); found = FindStrike(sim); }
            Assert.That(found.HasValue, Is.True); Assert.That(found.Value.Value, Is.EqualTo(2.4f).Within(1e-4f));
        }

        [Test]
        public void Whirlwind_HeroMovesAtSeventyPercent_AndKeepsControl()
        {
            SurvivorSim spin = Sim(), control = Sim(); SurvivorInput walk = new SurvivorInput(1, 0, 0);
            Ready(spin, 5); spin.Step(new SurvivorInput(1, 6, 0)); control.Step(walk); Steps(spin, 29, walk); Steps(control, 29, walk);
            float spinStart = spin.Hero.Position.X, controlStart = control.Hero.Position.X; Steps(spin, 50, walk); Steps(control, 50, walk);
            Assert.That(spin.Whirling, Is.True); float ratio = (spin.Hero.Position.X - spinStart) / (control.Hero.Position.X - controlStart);
            Assert.That(ratio, Is.EqualTo(0.7f).Within(0.02f)); Assert.That(spin.Hero.Position.X, Is.GreaterThan(spinStart + 2f), "still steerable");
            Steps(spin, 20, walk); Steps(control, 20, walk); Assert.That(spin.Whirling, Is.False);
        }

        [Test]
        public void Whirlwind_DoesNotBlockOtherSkills_AndEndsOnDeath()
        {
            SurvivorSim sim = Sim(); bool[] move = new bool[9], skill = new bool[7], pick = new bool[5];
            Cast(sim, 5); sim.Hero.Energy = sim.Hero.MaxEnergy; SurvivorActionMask.WriteMask(sim, move, skill, pick);
            Assert.That(skill[1], Is.True, "kick"); Assert.That(skill[5], Is.True, "leap"); Assert.That(skill[6], Is.False, "whirlwind itself on cooldown");
            SurvivorSim mortal = Sim("warrior", 2, false); Dummy(mortal, 5f, 0f); Cast(mortal, 5); Assert.That(mortal.Whirling, Is.True);
            mortal.DamageHeroForTests(1e6f, mortal.Enemies[0]); Assert.That(mortal.Hero.Alive, Is.False); Assert.That(mortal.Whirling, Is.False, "the spin ends with the hero");
            int uses = mortal.SkillUses[5]; Steps(mortal, 5, new SurvivorInput(0, 5, 0)); Assert.That(mortal.SkillUses[4], Is.Zero); Assert.That(mortal.SkillUses[5], Is.EqualTo(uses));
        }

        // ---- caltrop trap ----

        [Test]
        public void Trap_DropsAtTheHero_HurtsEveryHalfSecond_AndLastsSixSeconds()
        {
            SurvivorSim sim = Sim("archer"); SurvivorEnemy inside = Dummy(sim, 1.2f, 0f), outside = Dummy(sim, 4f, 0f);
            Tally t = new Tally(); Ready(sim, 4); sim.Step(new SurvivorInput(0, 5, 0)); Collect(sim, t);
            Assert.That(ActiveTraps(sim), Is.EqualTo(1)); SurvivorTrap trap = sim.Traps[0].Active ? sim.Traps[0] : sim.Traps[1];
            Assert.That(trap.Position.X, Is.EqualTo(sim.Hero.Position.X).Within(1e-4f)); Assert.That(trap.Radius, Is.EqualTo(2f)); Assert.That(trap.Duration, Is.EqualTo(6f));
            Steps(sim, 28); Assert.That(t.HitCount(inside), Is.Zero, "first tick at 0.5 s"); sim.Step(default); Collect(sim, t);
            for (int i = 0; i < 28; i++) { sim.Step(default); Collect(sim, t); } Assert.That(t.HitCount(inside), Is.EqualTo(1));
            for (int i = 58; i < 365; i++) { sim.Step(default); Collect(sim, t); }
            Assert.That(t.HitCount(inside), Is.EqualTo(12), "6 s / 0.5 s"); Assert.That(t.Dealt(inside), Is.EqualTo(72f).Within(0.01f)); Assert.That(t.HitCount(outside), Is.Zero);
            Assert.That(ActiveTraps(sim), Is.Zero, "gone after 6 s"); Assert.That(t.Strikes, Is.EqualTo(12));
        }

        [Test]
        public void Trap_SlowsEnemiesInsideToSixtyPercent_NotOnesOutside()
        {
            SurvivorSim trapped = Sim("archer", 4), control = Sim("archer", 4); SurvivorEnemy a = Dummy(trapped, 1.9f, 0f, false, 0), b = Dummy(control, 1.9f, 0f, false, 0);
            SurvivorEnemy aside = Dummy(trapped, -9f, 0f, false, 0); Cast(trapped, 4); control.Step(default);
            Assert.That(a.SlowRemaining, Is.GreaterThan(0f)); Assert.That(a.SlowMultiplier, Is.EqualTo(0.6f).Within(1e-5f)); Assert.That(aside.SlowRemaining, Is.Zero);
            Steps(trapped, 19); Steps(control, 19);
            float slowed = 1.9f - a.Position.X, normal = 1.9f - b.Position.X; Assert.That(normal, Is.GreaterThan(0.5f)); Assert.That(slowed / normal, Is.EqualTo(0.6f).Within(0.03f));
        }

        [Test]
        public void Trap_SlowEndsWhenTheEnemyLeaves_AndWhenTheTrapExpires()
        {
            SurvivorSim sim = Sim("archer"); sim.SetEnemiesInvulnerableForTests(); SurvivorEnemy e = Dummy(sim, 1f, 0f); Cast(sim, 4); Steps(sim, 3); Assert.That(e.SlowRemaining, Is.GreaterThan(0f));
            e.Position = new Vec2(10f, 0f); Steps(sim, 8); Assert.That(e.SlowRemaining, Is.Zero, "no slow once outside");
            e.Position = new Vec2(1f, 0f); Steps(sim, 3); Assert.That(e.SlowRemaining, Is.GreaterThan(0f)); Steps(sim, 360); Assert.That(ActiveTraps(sim), Is.Zero); Steps(sim, 8); Assert.That(e.SlowRemaining, Is.Zero, "no slow after expiry");
        }

        [Test]
        public void Trap_MaxTwoAlive_NewReplacesTheOldest()
        {
            SurvivorSim sim = Sim("archer"); float[] x = { -20f, 0f, 20f, 40f };
            for (int i = 0; i < 4; i++)
            {
                sim.SetHeroStateForTests(new Vec2(x[i], 0f), Vec2.Zero, 0f); Cast(sim, 4); Assert.That(ActiveTraps(sim), Is.EqualTo(Math.Min(i + 1, 2)), "after cast " + (i + 1));
            }
            List<float> alive = new List<float>(); for (int i = 0; i < sim.Traps.Count; i++) if (sim.Traps[i].Active) alive.Add(sim.Traps[i].Position.X);
            alive.Sort(); Assert.That(alive.Count, Is.EqualTo(2)); Assert.That(alive[0], Is.EqualTo(20f).Within(0.1f)); Assert.That(alive[1], Is.EqualTo(40f).Within(0.1f), "the two newest survive");
        }

        [Test]
        public void Trap_OverlappingTrapsTakeTheStrongerSlow_AndFreshTrapsStartTheirOwnTimers()
        {
            SurvivorSim sim = Sim("archer"); SurvivorEnemy e = Dummy(sim, 1f, 0f); sim.SetEnemiesInvulnerableForTests();
            Cast(sim, 4); Steps(sim, 20); Cast(sim, 4); Assert.That(ActiveTraps(sim), Is.EqualTo(2));
            Steps(sim, 345); Assert.That(ActiveTraps(sim), Is.EqualTo(1), "the first one expired at 6 s, the second lives on"); Assert.That(e.SlowRemaining, Is.GreaterThan(0f));
        }

        // ---- arrow barrage ----

        [Test]
        public void Barrage_FiresNineArrowsInAFiftyDegreeConeAtTheNearestEnemy()
        {
            SurvivorSim sim = Sim("archer"); Dummy(sim, 8f, 6f); Dummy(sim, -12f, 0f); Cast(sim, 5);
            List<float> angles = new List<float>(); float aim = MathF.Atan2(6f, 8f);
            for (int i = 0; i < sim.Projectiles.Count; i++)
            {
                SurvivorProjectile p = sim.Projectiles[i]; if (!p.Active || p.SourceIndex != -6) continue;
                angles.Add(MathF.Atan2(p.Velocity.Y, p.Velocity.X) - aim); Assert.That(p.Velocity.Length, Is.EqualTo(16f).Within(0.01f)); Assert.That(p.Damage, Is.EqualTo(18f)); Assert.That(p.PierceRemaining, Is.EqualTo(1));
            }
            Assert.That(angles.Count, Is.EqualTo(9)); angles.Sort();
            Assert.That(angles[0] * 180f / MathF.PI, Is.EqualTo(-25f).Within(0.5f)); Assert.That(angles[8] * 180f / MathF.PI, Is.EqualTo(25f).Within(0.5f));
            for (int i = 1; i < 9; i++) Assert.That((angles[i] - angles[i - 1]) * 180f / MathF.PI, Is.EqualTo(6.25f).Within(0.2f), "even spacing");
            Assert.That(angles[4], Is.EqualTo(0f).Within(0.01f), "the middle arrow points at the target");
        }

        [Test]
        public void Barrage_WithNoEnemyInRange_AimsAlongTheFacing()
        {
            SurvivorSim sim = Sim("archer"); sim.SetHeroStateForTests(Vec2.Zero, Vec2.Zero, 1f); Dummy(sim, 0f, 16f).Position = new Vec2(30f, 30f); Cast(sim, 5);
            float middle = float.NaN; List<float> angles = new List<float>();
            for (int i = 0; i < sim.Projectiles.Count; i++) if (sim.Projectiles[i].Active && sim.Projectiles[i].SourceIndex == -6) angles.Add(MathF.Atan2(sim.Projectiles[i].Velocity.Y, sim.Projectiles[i].Velocity.X));
            angles.Sort(); Assert.That(angles.Count, Is.EqualTo(9)); middle = angles[4]; Assert.That(middle, Is.EqualTo(sim.Hero.Facing).Within(0.01f));
        }

        [Test]
        public void Barrage_ATargetBeyondFourteenMetresIsNotAimedAt()
        {
            SurvivorSim sim = Sim("archer"); sim.SetHeroStateForTests(Vec2.Zero, Vec2.Zero, 0f); Dummy(sim, 0f, -16f); Cast(sim, 5);
            List<float> angles = new List<float>(); for (int i = 0; i < sim.Projectiles.Count; i++) if (sim.Projectiles[i].Active && sim.Projectiles[i].SourceIndex == -6) angles.Add(MathF.Atan2(sim.Projectiles[i].Velocity.Y, sim.Projectiles[i].Velocity.X));
            angles.Sort(); Assert.That(angles[4], Is.EqualTo(sim.Hero.Facing).Within(0.01f), "centred on the facing, not on the far enemy");
        }

        [Test]
        public void Barrage_EachArrowPiercesExactlyOneEnemy()
        {
            SurvivorSim sim = Sim("archer"); SurvivorEnemy a = Dummy(sim, 6f, 0f), b = Dummy(sim, 8f, 0f), c = Dummy(sim, 10f, 0f);
            Tally t = new Tally(); Ready(sim, 5); sim.Step(new SurvivorInput(0, 6, 0)); Collect(sim, t); for (int i = 0; i < 90; i++) { sim.Step(default); Collect(sim, t); }
            Assert.That(t.HitCount(a), Is.GreaterThan(0)); Assert.That(t.HitCount(b), Is.GreaterThan(0), "arrows passed through the first enemy"); Assert.That(t.HitCount(c), Is.Zero, "and stopped after the second");
            Assert.That(t.Dealt(a) % 18f, Is.EqualTo(0f).Within(0.01f).Or.EqualTo(18f).Within(0.01f)); Assert.That(sim.SkillUses[5], Is.EqualTo(1));
        }

        // ---- fire wall ----

        [Test]
        public void Wall_IsARectangleSixByOnePointTwo_ThreeMetresAhead_PerpendicularToTheFacing()
        {
            SurvivorSim sim = Sim("mage"); SurvivorEnemy centre = Dummy(sim, 3f, 0f), edge = Dummy(sim, 3f, 2.5f), beyondAcross = Dummy(sim, 3f, 3.9f), beyondDepth = Dummy(sim, 4.5f, 0f), behind = Dummy(sim, 1.5f, 0f), negEdge = Dummy(sim, 3f, -2.5f);
            Tally t = new Tally(); Ready(sim, 4); sim.Step(new SurvivorInput(0, 5, 0)); Collect(sim, t);
            SurvivorWall wall = sim.Walls[0].Active ? sim.Walls[0] : sim.Walls[1];
            Assert.That(wall.Position.X, Is.EqualTo(3f).Within(0.01f)); Assert.That(wall.Position.Y, Is.EqualTo(0f).Within(0.01f)); Assert.That(wall.Direction.X, Is.EqualTo(1f).Within(1e-4f)); Assert.That(wall.Width, Is.EqualTo(6f)); Assert.That(wall.Depth, Is.EqualTo(1.2f)); Assert.That(wall.Duration, Is.EqualTo(4f));
            for (int i = 1; i < 24; i++) { sim.Step(default); Collect(sim, t); }
            Assert.That(t.Dealt(centre), Is.EqualTo(8f).Within(0.01f)); Assert.That(t.Dealt(edge), Is.EqualTo(8f).Within(0.01f)); Assert.That(t.Dealt(negEdge), Is.EqualTo(8f).Within(0.01f));
            Assert.That(t.HitCount(beyondAcross), Is.Zero, "outside across the width"); Assert.That(t.HitCount(beyondDepth), Is.Zero, "outside along the depth"); Assert.That(t.HitCount(behind), Is.Zero, "behind the wall");
            Assert.That(t.Strikes, Is.EqualTo(1));
        }

        [Test]
        public void Wall_FollowsTheFacingAtCastTime()
        {
            SurvivorSim sim = Sim("mage"); sim.SetHeroStateForTests(Vec2.Zero, Vec2.Zero, MathF.PI / 2f); Cast(sim, 4);
            SurvivorWall wall = sim.Walls[0].Active ? sim.Walls[0] : sim.Walls[1];
            Assert.That(wall.Position.X, Is.EqualTo(0f).Within(0.01f)); Assert.That(wall.Position.Y, Is.EqualTo(3f).Within(0.01f)); Assert.That(wall.Direction.Y, Is.EqualTo(1f).Within(1e-4f));
            SurvivorEnemy wide = Dummy(sim, 2.8f, 3f); Tally t = Run(sim, 24); Assert.That(t.HitCount(wide), Is.EqualTo(1), "the wide side now runs along x");
        }

        [Test]
        public void Wall_LastsFourSeconds_TicksEveryPointFourSeconds()
        {
            SurvivorSim sim = Sim("mage"); SurvivorEnemy e = Dummy(sim, 3f, 0f); Tally t = new Tally(); Ready(sim, 4); sim.Step(new SurvivorInput(0, 5, 0)); Collect(sim, t);
            Assert.That(ActiveWalls(sim), Is.EqualTo(1)); for (int i = 1; i < 245; i++) { sim.Step(default); Collect(sim, t); }
            Assert.That(t.HitCount(e), Is.EqualTo(10), "4 s / 0.4 s"); Assert.That(t.Dealt(e), Is.EqualTo(80f).Within(0.01f)); Assert.That(ActiveWalls(sim), Is.Zero); Assert.That(t.Strikes, Is.EqualTo(10));
        }

        [Test]
        public void Wall_MaxTwoAlive_NewReplacesTheOldest()
        {
            SurvivorSim sim = Sim("mage"); float[] x = { -20f, 0f, 20f, 40f };
            for (int i = 0; i < 4; i++) { sim.SetHeroStateForTests(new Vec2(x[i], 0f), Vec2.Zero, 0f); Cast(sim, 4); Assert.That(ActiveWalls(sim), Is.EqualTo(Math.Min(i + 1, 2))); }
            List<float> alive = new List<float>(); for (int i = 0; i < sim.Walls.Count; i++) if (sim.Walls[i].Active) alive.Add(sim.Walls[i].Position.X);
            alive.Sort(); Assert.That(alive[0], Is.EqualTo(23f).Within(0.1f)); Assert.That(alive[1], Is.EqualTo(43f).Within(0.1f));
        }

        [Test]
        public void Wall_KillsWeakEnemies_OnTheSecondTick()
        {
            SurvivorSim sim = Sim("mage"); SurvivorEnemy w = sim.SpawnEnemyForTests(0, new Vec2(3f, 0f)); w.StunRemaining = 1000f; Cast(sim, 4);
            Steps(sim, 40); Assert.That(sim.Kills, Is.Zero, "one 8-damage tick leaves a 15 HP walker alive"); Steps(sim, 9); Assert.That(sim.Kills, Is.EqualTo(1), "dead on the second tick (tick 48)");
        }

        // ---- chain lightning ----

        [Test]
        public void Chain_StrikesTheNearestThenJumps_EachJumpAtEightyFivePercent()
        {
            SurvivorSim sim = Sim("mage"); SurvivorEnemy a = Dummy(sim, 3f, 0f, false), b = Dummy(sim, 6f, 0f, false), c = Dummy(sim, 9f, 0f, false), unreachable = Dummy(sim, 16f, 0f, false);
            Ready(sim, 5); sim.Step(new SurvivorInput(0, 6, 0));
            List<float> damage = new List<float>(); List<int> ids = new List<int>(); int strikes = 0, hitsReported = -1;
            for (int i = 0; i < sim.Events.Count; i++)
            {
                SurvivorEvent e = sim.Events[i];
                if (e.Type == SurvivorEventType.DamageDealt) { damage.Add(e.Value); ids.Add(e.Id); }
                if (e.Type == SurvivorEventType.StrikeLanded && e.Id == -1) { strikes++; Assert.That(e.Value, Is.EqualTo(0.6f).Within(1e-5f)); }
                if (e.Type == SurvivorEventType.SkillUsed) hitsReported = (int)e.Extra;
            }
            Assert.That(ids, Is.EqualTo(new List<int> { a.Id, b.Id, c.Id }), "order of the chain"); Assert.That(strikes, Is.EqualTo(3)); Assert.That(hitsReported, Is.EqualTo(3));
            Assert.That(damage[0], Is.EqualTo(35f).Within(1e-3f)); Assert.That(damage[1], Is.EqualTo(35f * 0.85f).Within(1e-3f)); Assert.That(damage[2], Is.EqualTo(35f * 0.85f * 0.85f).Within(1e-3f));
            Assert.That(unreachable.Hp, Is.EqualTo(1e6f), "16 m is beyond the 5 m jump");
            Assert.That(a.StunRemaining, Is.InRange(0.25f, 0.3f)); Assert.That(b.StunRemaining, Is.InRange(0.25f, 0.3f)); Assert.That(c.StunRemaining, Is.InRange(0.25f, 0.3f)); Assert.That(unreachable.StunRemaining, Is.Zero);
        }

        [Test]
        public void Chain_NeverHitsMoreThanSixTargets_AndNeverTheSameOneTwice()
        {
            SurvivorSim sim = Sim("mage"); SurvivorEnemy[] line = new SurvivorEnemy[9]; for (int i = 0; i < 9; i++) line[i] = Dummy(sim, 2f + i, 0f);
            Tally t = new Tally(); Ready(sim, 5); sim.Step(new SurvivorInput(0, 6, 0)); Collect(sim, t);
            int hit = 0; foreach (SurvivorEnemy e in line) { Assert.That(t.HitCount(e), Is.LessThanOrEqualTo(1)); hit += t.HitCount(e); }
            Assert.That(hit, Is.EqualTo(6)); for (int i = 0; i < 6; i++) Assert.That(t.HitCount(line[i]), Is.EqualTo(1), "the first six along the line"); Assert.That(t.HitCount(line[6]), Is.Zero);
        }

        [Test]
        public void Chain_TwoEnemies_DoNotJumpBackToTheFirst()
        {
            SurvivorSim sim = Sim("mage"); SurvivorEnemy a = Dummy(sim, 3f, 0f), b = Dummy(sim, 5f, 0f); Tally t = new Tally(); Ready(sim, 5); sim.Step(new SurvivorInput(0, 6, 0)); Collect(sim, t);
            Assert.That(t.HitCount(a), Is.EqualTo(1)); Assert.That(t.HitCount(b), Is.EqualTo(1)); Assert.That(t.Strikes, Is.EqualTo(2));
        }

        [Test]
        public void Chain_JumpIsMeasuredFromTheLastTarget_NotFromTheHero()
        {
            SurvivorSim sim = Sim("mage"); SurvivorEnemy a = Dummy(sim, 9f, 0f), b = Dummy(sim, 13.5f, 0f), c = Dummy(sim, 20f, 0f); Tally t = new Tally(); Ready(sim, 5); sim.Step(new SurvivorInput(0, 6, 0)); Collect(sim, t);
            Assert.That(t.HitCount(a), Is.EqualTo(1)); Assert.That(t.HitCount(b), Is.EqualTo(1), "4.5 m jump from the first target, 13.5 m from the hero"); Assert.That(t.HitCount(c), Is.Zero, "6.5 m jump is too far");
        }

        [Test]
        public void Chain_FirstTargetBeyondTenMetres_StrikesNothing_ButStillSpendsTheSkill()
        {
            SurvivorSim sim = Sim("mage"); SurvivorEnemy far = Dummy(sim, 11.5f, 0f); float energy = sim.Hero.MaxEnergy; Ready(sim, 5); sim.Step(new SurvivorInput(0, 6, 0));
            Assert.That(far.Hp, Is.EqualTo(1e6f)); Assert.That(FindStrike(sim), Is.Null); Assert.That(sim.SkillUses[5], Is.EqualTo(1)); Assert.That(sim.Hero.Energy, Is.EqualTo(energy - 30f).Within(1f)); Assert.That(sim.Hero.SkillCooldowns[5], Is.EqualTo(6f));
        }

        [Test]
        public void Chain_ContinuesFromAKilledTarget()
        {
            SurvivorSim sim = Sim("mage"); SurvivorEnemy a = sim.SpawnEnemyForTests(0, new Vec2(3f, 0f)), b = sim.SpawnEnemyForTests(0, new Vec2(6f, 0f)), c = sim.SpawnEnemyForTests(0, new Vec2(9f, 0f));
            Ready(sim, 5); sim.Step(new SurvivorInput(0, 6, 0)); Assert.That(a.Active, Is.False); Assert.That(b.Active, Is.False); Assert.That(c.Active, Is.False); Assert.That(sim.Kills, Is.EqualTo(3));
        }

        [Test]
        public void Chain_BossIsDamagedButNotStunned()
        {
            SurvivorSim sim = Sim("mage"); SurvivorEnemy boss = sim.SpawnBossForTests(new Vec2(5f, 0f)); Ready(sim, 5); sim.Step(new SurvivorInput(0, 6, 0)); Assert.That(boss.Hp, Is.LessThan(boss.MaxHp)); Assert.That(boss.StunRemaining, Is.Zero);
        }

        // ---- mask, cooldown, energy, observation ----

        [TestCase("warrior", 4)]
        [TestCase("warrior", 5)]
        [TestCase("archer", 4)]
        [TestCase("archer", 5)]
        [TestCase("mage", 4)]
        [TestCase("mage", 5)]
        public void NewSkill_MaskCooldownEnergyAndStun(string classId, int slot)
        {
            SurvivorSim sim = Sim(classId); SkillDef def = sim.Config.ClassDef.ActiveSkills[slot]; bool[] move = new bool[9], skill = new bool[7], pick = new bool[5];
            SurvivorActionMask.WriteMask(sim, move, skill, pick); Assert.That(skill[slot + 1], Is.True, "ready at the start");
            sim.Hero.Energy = def.EnergyCost - 0.1f; SurvivorActionMask.WriteMask(sim, move, skill, pick); Assert.That(skill[slot + 1], Is.False, "not enough energy");
            sim.Hero.Energy = def.EnergyCost; SurvivorActionMask.WriteMask(sim, move, skill, pick); Assert.That(skill[slot + 1], Is.True, "exactly enough energy");
            sim.Hero.Energy = sim.Hero.MaxEnergy; sim.Hero.StunRemaining = 1f; SurvivorActionMask.WriteMask(sim, move, skill, pick); Assert.That(skill[slot + 1], Is.False, "stunned");
            sim.Hero.StunRemaining = 0f; sim.Hero.Energy = sim.Hero.MaxEnergy; float before = sim.Hero.Energy;
            sim.Step(new SurvivorInput(0, slot + 1, 0));
            Assert.That(sim.LastSkill, Is.EqualTo(slot + 1)); Assert.That(sim.SkillUses[slot], Is.EqualTo(1)); Assert.That(sim.Hero.SkillCooldowns[slot], Is.EqualTo(def.Cooldown));
            Assert.That(sim.Hero.Energy, Is.EqualTo(before - def.EnergyCost + sim.Config.ClassDef.EnergyRegen * Dt).Within(0.05f));
            bool used = false; foreach (SurvivorEvent e in sim.Events) if (e.Type == SurvivorEventType.SkillUsed && (int)e.Value == slot) used = true; Assert.That(used, Is.True, "SkillUsed carries the slot");
            sim.Hero.Energy = sim.Hero.MaxEnergy; SurvivorActionMask.WriteMask(sim, move, skill, pick); Assert.That(skill[slot + 1], Is.False, "cooling down");
            sim.Hero.SkillCooldowns[slot] = 0.02f; Steps(sim, 2); sim.Hero.Energy = sim.Hero.MaxEnergy; sim.Hero.StunRemaining = 0f;
            if (!sim.Hero.Dashing) { SurvivorActionMask.WriteMask(sim, move, skill, pick); Assert.That(skill[slot + 1], Is.True, "ready again after the cooldown"); }
        }

        [TestCase("warrior", 4)]
        [TestCase("warrior", 5)]
        [TestCase("archer", 4)]
        [TestCase("archer", 5)]
        [TestCase("mage", 4)]
        [TestCase("mage", 5)]
        public void NewSkill_ObservationFieldsAndLastSkill(string classId, int slot)
        {
            SurvivorSim sim = Sim(classId); float[] values = Observe(sim); int cooldownAt = 60 + slot, allowedAt = 62 + slot;
            Assert.That(values[cooldownAt], Is.Zero); Assert.That(values[allowedAt], Is.EqualTo(1f)); Assert.That(values[68], Is.Zero); Assert.That(values[69], Is.Zero);
            sim.Step(new SurvivorInput(0, slot + 1, 0)); values = Observe(sim);
            Assert.That(values[cooldownAt], Is.GreaterThan(0.95f), "skill_cooldown_" + slot); Assert.That(values[allowedAt], Is.Zero, "skill_allowed_" + slot); Assert.That(values[68 + slot - 4], Is.EqualTo(1f), "last_skill"); Assert.That(values[68 + (5 - slot)], Is.Zero);
            for (int i = 57; i <= 61; i++) Assert.That(values[i], Is.Zero, "old last-skill one-hot index " + i);
            int otherAllowed = 62 + (slot == 4 ? 5 : 4); Assert.That(values[otherAllowed], Is.EqualTo(1f).Or.EqualTo(0f)); for (int i = 0; i < values.Length; i++) Assert.That(values[i], Is.InRange(-1f, 1f), "index " + i);
        }

        [Test]
        public void NoClassNeedsABrainUpgrade_TheSchemaIsUnchanged()
        {
            Assert.That(SurvivorObservation.SchemaVersion, Is.EqualTo(5)); Assert.That(SurvivorObservation.Size, Is.EqualTo(2592)); Assert.That(SurvivorInput.SkillBranchSize, Is.EqualTo(7)); Assert.That(SurvivorInput.SkillSlotCount, Is.EqualTo(6));
        }

        // ---- slow stays neutral, determinism, allocation ----

        [Test]
        public void WithoutATrap_NoEnemyIsEverSlowed()
        {
            SurvivorConfig config = Cfg(); config.OpeningRing = 20; SurvivorSim sim = new SurvivorSim(config, 77); sim.SetHeroInvulnerableForTests();
            for (int i = 0; i < 600; i++) { sim.Step(new SurvivorInput(i / 30 % 9, 0, 0)); for (int j = 0; j < sim.Enemies.Count; j++) if (sim.Enemies[j].Active) { Assert.That(sim.Enemies[j].SlowRemaining, Is.Zero); } }
        }

        private static long RunHash(string classId, int seed)
        {
            SurvivorConfig config = Cfg(classId); config.OpeningRing = 8; config.ClassDef.MaxHp = 1e6f; SurvivorSim sim = new SurvivorSim(config, seed); long hash = 17; int newUses = 0;
            for (int tick = 0; tick < 2400 && !sim.IsEnded; tick++)
            {
                sim.Hero.SkillCooldowns[4] = tick % 7 == 0 ? 0f : sim.Hero.SkillCooldowns[4]; sim.Hero.SkillCooldowns[5] = tick % 11 == 0 ? 0f : sim.Hero.SkillCooldowns[5]; sim.Hero.Energy = sim.Hero.MaxEnergy;
                SurvivorInput input = sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1 + tick % 3) : new SurvivorInput((tick / 40) % 9, 1 + (tick / 5) % 6, 0);
                sim.Step(input); hash = hash * 31 + SurvivorM5GoldenTests.StateHash(sim); hash = hash * 31 + ActiveTraps(sim) * 7 + ActiveWalls(sim) + (sim.Whirling ? 100 : 0);
            }
            newUses = sim.SkillUses[4] + sim.SkillUses[5]; Assert.That(newUses, Is.GreaterThan(5), classId + " used its new skills"); return hash;
        }

        [Test]
        public void AllThreeClasses_RunDeterministically_WithTheirNewSkills()
        {
            foreach (string id in Classes) { Assert.That(RunHash(id, 501), Is.EqualTo(RunHash(id, 501)), id); Assert.That(RunHash(id, 501), Is.Not.EqualTo(RunHash(id, 502)), id); }
        }

        [Test]
        public void AllThreeClasses_StepAndObservationAllocateNothing_WhileUsingEverySkill()
        {
            foreach (string id in Classes)
            {
                SurvivorConfig config = Cfg(id); config.MapHalfSize = 20f; config.ClassDef.MaxHp = 1e7f; SurvivorSim sim = new SurvivorSim(config, 603); sim.DisablePickupCollectionForTests();
                for (int i = 0; i < 40; i++) sim.SpawnEnemyForTests(i % 4, Vec2.FromAngle(i * 0.7f) * (2f + i % 7));
                SurvivorObservation observation = new SurvivorObservation(); float[] values = new float[SurvivorObservation.Size];
                for (int i = 0; i < 700; i++) { Drive(sim, i); sim.Step(new SurvivorInput(i / 20 % 9, 1 + i / 4 % 6, 0)); observation.Write(sim, values); }
                int uses4 = sim.SkillUses[4], uses5 = sim.SkillUses[5];
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 700; i < 2200; i++) { Drive(sim, i); sim.Step(new SurvivorInput(i / 20 % 9, 1 + i / 4 % 6, 0)); }
                long stepAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
                before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 100; i++) observation.Write(sim, values); long writeAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(sim.IsEnded, Is.False, id); Assert.That(sim.SkillUses[4], Is.GreaterThan(uses4), id + " slot 4 used in the measured window"); Assert.That(sim.SkillUses[5], Is.GreaterThan(uses5), id + " slot 5 used in the measured window");
                Assert.That(stepAllocated, Is.EqualTo(0L), id + " Step"); Assert.That(writeAllocated, Is.EqualTo(0L), id + " Write");
            }
        }

        private static void Drive(SurvivorSim sim, int tick) { for (int s = 0; s < 6; s++) if (tick % 4 == 0) sim.Hero.SkillCooldowns[s] = 0f; sim.Hero.Energy = sim.Hero.MaxEnergy; }
    }
}
