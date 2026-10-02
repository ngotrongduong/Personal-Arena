using System;
using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    /// <summary>T-039: schema v5 (catalog 128, six active skill slots, self block 72).</summary>
    public sealed class SurvivorSchemaV5Tests
    {
        private static SkillDef Burst(string id, float cooldown, float energy) =>
            new SkillDef { Id = id, Kind = SkillKind.AreaBurst, Damage = 10f, AreaRadius = 3f, StunSeconds = 0.5f, Knockback = 1f, Cooldown = cooldown, EnergyCost = energy };

        /// <summary>Warrior with two extra skills: slot 4 = 8 s / 10 energy, slot 5 = 4 s / 20 energy.</summary>
        private static SurvivorSim SixSkillSim(int seed, bool slot4 = true)
        {
            SurvivorConfig config = SurvivorTestHelpers.Config();
            if (slot4) config.ClassDef.ActiveSkills[4] = Burst("burst-a", 8f, 10f);
            config.ClassDef.ActiveSkills[5] = Burst("burst-b", 4f, 20f);
            SurvivorSim sim = new SurvivorSim(config, seed); sim.SetWeaponCooldownForTests(0, 1000f); return sim;
        }

        private static float[] Observe(SurvivorSim sim) { float[] values = new float[SurvivorObservation.Size]; new SurvivorObservation().Write(sim, values); return values; }

        [Test]
        public void Layout_SizeAndEveryOffset()
        {
            Assert.That(SurvivorObservation.SchemaVersion, Is.EqualTo(5)); Assert.That(SurvivorObservation.Size, Is.EqualTo(2592));
            Assert.That(SurvivorCatalog.CatalogSize, Is.EqualTo(128)); Assert.That(SurvivorObservation.OfferStride, Is.EqualTo(130));
            Assert.That(SurvivorObservation.SelfOffset, Is.Zero); Assert.That(SurvivorObservation.InventoryOffset, Is.EqualTo(72)); Assert.That(SurvivorObservation.OffersOffset, Is.EqualTo(200));
            Assert.That(SurvivorObservation.RaysOffset, Is.EqualTo(720)); Assert.That(SurvivorObservation.DensityOffset, Is.EqualTo(2520));
            Assert.That(SurvivorObservation.InventoryOffset + SurvivorCatalog.CatalogSize, Is.EqualTo(SurvivorObservation.OffersOffset));
            Assert.That(SurvivorObservation.OffersOffset + 4 * SurvivorObservation.OfferStride, Is.EqualTo(SurvivorObservation.RaysOffset));
            Assert.That(SurvivorObservation.RaysOffset + 72 * SurvivorObservation.RayStride, Is.EqualTo(SurvivorObservation.DensityOffset));
            Assert.That(SurvivorObservation.DensityOffset + 3 * 8 * 3, Is.EqualTo(SurvivorObservation.Size));
        }

        [Test]
        public void ActionLayout_IsNineSevenFive()
        {
            Assert.That(SurvivorInput.MoveBranchSize, Is.EqualTo(9)); Assert.That(SurvivorInput.SkillBranchSize, Is.EqualTo(7)); Assert.That(SurvivorInput.PickBranchSize, Is.EqualTo(5));
            Assert.That(SurvivorInput.SkillSlotCount, Is.EqualTo(6)); Assert.That(new SurvivorSim(SurvivorTestHelpers.Config(), 1).Hero.SkillCooldowns.Length, Is.EqualTo(6)); Assert.That(new SurvivorSim(SurvivorTestHelpers.Config(), 1).SkillUses.Length, Is.EqualTo(6));
        }

        [Test]
        public void Inventory_Has128Slots_AndHighSlotsStayZero()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 1); sim.GiveItemForTests(0, 5); sim.GiveItemForTests(37, 1);
            float[] values = Observe(sim);
            Assert.That(values[SurvivorObservation.InventoryOffset + 0], Is.EqualTo(1f)); Assert.That(values[SurvivorObservation.InventoryOffset + 37], Is.GreaterThan(0f));
            for (int i = 64; i < 128; i++) Assert.That(values[SurvivorObservation.InventoryOffset + i], Is.Zero, "reserved slot " + i);
            Assert.That(SurvivorCatalog.Get(73), Is.Null); Assert.That(SurvivorCatalog.Get(111), Is.Null); Assert.That(SurvivorCatalog.Get(128), Is.Null);
        }

        [Test]
        public void Offers_UseTheWiderStride_AndTheLastOfferFieldsSitBeforeTheRays()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 2); sim.GiveXpForTests(5f); sim.Step(default); Assert.That(sim.OfferCount, Is.GreaterThan(0));
            float[] values = Observe(sim);
            for (int slot = 0; slot < sim.OfferCount; slot++)
            {
                int offset = SurvivorObservation.OffersOffset + slot * SurvivorObservation.OfferStride;
                Assert.That(values[offset + sim.GetOffer(slot).CatalogIndex], Is.EqualTo(1f)); Assert.That(values[offset + 128], Is.GreaterThan(0f), "next level"); Assert.That(values[offset + 129], Is.EqualTo(1f), "present flag");
            }
            for (int slot = sim.OfferCount; slot < 4; slot++) Assert.That(values[SurvivorObservation.OffersOffset + slot * SurvivorObservation.OfferStride + 129], Is.Zero, "empty slot");
        }

        [TestCase("warrior")]
        [TestCase("mage")]
        [TestCase("archer")]
        public void Classes_KeepTheirFirstFourSkills_AndOldRulesHaveNoneInSlots4And5(string classId)
        {
            SurvivorClassDef kit = SurvivorDefaults.ForClass(classId); Assert.That(kit.ActiveSkills.Length, Is.EqualTo(6));
            string[] expected = classId == "warrior" ? new[] { "kick", "shield-block", "dash", "war-cry" } : classId == "mage" ? new[] { "fireball", "mana-shield", "blink", "frost-burst" } : new[] { "power-shot", "roll-back", "kick", "none" };
            for (int i = 0; i < 4; i++) Assert.That(kit.ActiveSkills[i].Id, Is.EqualTo(expected[i]), classId + " slot " + i);
            SurvivorConfig old = SurvivorTestHelpers.OldConfig(); old.ClassDef = kit; SurvivorTestHelpers.OldRules(old);
            for (int i = 4; i < 6; i++) { Assert.That(kit.ActiveSkills[i], Is.Not.Null); Assert.That(kit.ActiveSkills[i].Kind, Is.EqualTo(SkillKind.None)); }
        }

        [TestCase("warrior")]
        [TestCase("mage")]
        [TestCase("archer")]
        public void NoneInSlots4And5_IsMaskedOut_AndInputFailsWithoutSideEffects(string classId)
        {
            SurvivorConfig config = SurvivorTestHelpers.Config(); config.ClassDef = SurvivorDefaults.ForClass(classId); SurvivorTestHelpers.OldRules(config);
            SurvivorSim sim = new SurvivorSim(config, 5); bool[] move = new bool[9], skill = new bool[7], pick = new bool[5];
            SurvivorActionMask.WriteMask(sim, move, skill, pick); Assert.That(skill[0], Is.True); Assert.That(skill[5], Is.False); Assert.That(skill[6], Is.False);
            float energy = sim.Hero.Energy; sim.Step(new SurvivorInput(0, 5, 0));
            Assert.That(sim.LastSkill, Is.Zero); Assert.That(sim.SkillUses[4], Is.Zero); Assert.That(sim.Hero.SkillCooldowns[4], Is.Zero); Assert.That(sim.Hero.Energy, Is.GreaterThanOrEqualTo(energy));
            bool failed = false; sim.Step(new SurvivorInput(0, 6, 0)); foreach (SurvivorEvent e in sim.Events) if (e.Type == SurvivorEventType.SkillFailed && e.Value == 5f) failed = true;
            Assert.That(failed || sim.SkillUses[5] == 0, Is.True); Assert.That(sim.SkillUses[5], Is.Zero);
            float[] values = Observe(sim); Assert.That(values[66], Is.Zero); Assert.That(values[67], Is.Zero); Assert.That(values[64], Is.Zero); Assert.That(values[65], Is.Zero);
        }

        [Test]
        public void SkillInSlot5_WorksEndToEnd_MaskInputCooldownObservation()
        {
            SurvivorSim sim = SixSkillSim(11); SurvivorEnemy near = sim.SpawnEnemyForTests(2, new Vec2(2f, 0f)); bool[] move = new bool[9], skill = new bool[7], pick = new bool[5];
            SurvivorActionMask.WriteMask(sim, move, skill, pick); Assert.That(skill[5], Is.True, "slot 4 ready"); Assert.That(skill[6], Is.True, "slot 5 ready");
            float[] values = Observe(sim); Assert.That(values[66], Is.EqualTo(1f)); Assert.That(values[67], Is.EqualTo(1f)); Assert.That(values[65], Is.Zero);
            float energy = sim.Hero.Energy; float hp = near.Hp;
            sim.Step(new SurvivorInput(0, 6, 0));
            Assert.That(sim.LastSkill, Is.EqualTo(6)); Assert.That(sim.SkillUses[5], Is.EqualTo(1)); Assert.That(sim.SkillUses[4], Is.Zero); Assert.That(sim.Hero.SkillCooldowns[5], Is.EqualTo(4f)); Assert.That(sim.Hero.SkillCooldowns[4], Is.Zero);
            Assert.That(sim.Hero.Energy, Is.EqualTo(energy - 20f).Within(1f)); Assert.That(near.Hp, Is.LessThan(hp), "the burst hit");
            bool used = false; foreach (SurvivorEvent e in sim.Events) if (e.Type == SurvivorEventType.SkillUsed && e.Value == 5f) used = true; Assert.That(used, Is.True, "SkillUsed carries slot 5");
            SurvivorActionMask.WriteMask(sim, move, skill, pick); Assert.That(skill[6], Is.False, "on cooldown"); Assert.That(skill[5], Is.True, "slot 4 unaffected");
            values = Observe(sim); Assert.That(values[65], Is.GreaterThan(0.9f), "skill_cooldown_5"); Assert.That(values[67], Is.Zero, "skill_allowed_5"); Assert.That(values[66], Is.EqualTo(1f)); Assert.That(values[69], Is.EqualTo(1f), "last_skill 6");
            Assert.That(values[68], Is.Zero);
            for (int tick = 0; tick < 241; tick++) sim.Step(default);
            Assert.That(sim.Hero.SkillCooldowns[5], Is.Zero); SurvivorActionMask.WriteMask(sim, move, skill, pick); Assert.That(skill[6], Is.True, "ready again after 4 s");
        }

        [Test]
        public void SkillInSlot4_UsesItsOwnObservationFields()
        {
            SurvivorSim sim = SixSkillSim(12); sim.Step(new SurvivorInput(0, 5, 0));
            Assert.That(sim.LastSkill, Is.EqualTo(5)); Assert.That(sim.SkillUses[4], Is.EqualTo(1)); Assert.That(sim.Hero.SkillCooldowns[4], Is.EqualTo(8f));
            float[] values = Observe(sim); Assert.That(values[64], Is.GreaterThan(0.9f), "skill_cooldown_4"); Assert.That(values[66], Is.Zero, "skill_allowed_4"); Assert.That(values[68], Is.EqualTo(1f), "last_skill 5"); Assert.That(values[69], Is.Zero);
            for (int i = 57; i <= 61; i++) Assert.That(values[i], Is.Zero, "old last-skill one-hot stays empty for slot 4, index " + i);
        }

        [Test]
        public void ExistingSelfFields_KeepTheirOffsets()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 3); sim.SetWeaponCooldownForTests(0, 1000f); sim.Step(new SurvivorInput(0, 4, 0));
            float[] values = Observe(sim);
            Assert.That(values[6], Is.GreaterThan(0.9f), "cooldown of skill 3 (war-cry)"); Assert.That(values[10], Is.Zero); Assert.That(values[57 + 4], Is.EqualTo(1f)); Assert.That(values[62], Is.EqualTo(MathF.Cos(sim.Hero.Facing)).Within(1e-4f)); Assert.That(values[63], Is.EqualTo(MathF.Sin(sim.Hero.Facing)).Within(1e-4f));
            Assert.That(values[13], Is.Zero); Assert.That(values[70], Is.Zero); Assert.That(values[71], Is.Zero, "reserved");
        }

        [Test]
        public void Write_IsFiniteAndInRange_WithSixSkillsAndAFullInventory()
        {
            SurvivorSim sim = SixSkillSim(13); foreach (int item in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 31, 32, 33, 34, 35, 36, 37, 58, 59, 60, 61 }) sim.GiveItemForTests(item, 5);
            for (int i = 0; i < 6; i++) sim.SpawnEnemyForTests(i % 4, new Vec2(3f + i, i));
            sim.Step(new SurvivorInput(1, 6, 0)); sim.Step(new SurvivorInput(1, 5, 0)); sim.GiveXpForTests(50f); sim.Step(default);
            foreach (float v in Observe(sim)) { Assert.That(float.IsFinite(v), Is.True); Assert.That(v, Is.InRange(-1f, 1f)); }
        }

        [Test]
        public void SixSkillRun_IsDeterministic()
        {
            float[][] snapshots = new float[2][];
            for (int run = 0; run < 2; run++)
            {
                SurvivorSim sim = SixSkillSim(21); SurvivorObservation observation = new SurvivorObservation(); float[] values = new float[SurvivorObservation.Size];
                for (int i = 0; i < 900; i++) { sim.Step(sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1) : new SurvivorInput(i % 9, i % 7, 0)); observation.Write(sim, values); }
                snapshots[run] = values;
            }
            Assert.That(snapshots[1], Is.EqualTo(snapshots[0]));
        }

        [Test]
        public void SixSkillSteps_Write_WriteMask_DoNotAllocate()
        {
            SurvivorSim sim = SixSkillSim(22);
            for (int i = 0; i < 250; i++) sim.SpawnEnemyForTests(i % 3, Vec2.FromAngle(i * 2.399963f) * (6f + i % 24));
            for (int i = 0; i < 400; i++) sim.SpawnGemForTests(Vec2.FromAngle(i * 1.7f) * (3f + i % 27), 1 + i % 8);
            sim.SetHeroInvulnerableForTests(); sim.SetEnemiesInvulnerableForTests(); sim.DisablePickupCollectionForTests();
            SurvivorObservation observation = new SurvivorObservation(); float[] values = new float[SurvivorObservation.Size]; bool[] move = new bool[9], skill = new bool[7], pick = new bool[5];
            RunTicks(sim, observation, values, move, skill, pick);
            // A per-tick allocation shows in every window; a single one-off 24-byte runtime allocation (seen once per run of the first window) must not fail the test, so the minimum of four windows counts.
            long allocated = long.MaxValue;
            for (int window = 0; window < 4; window++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread(); RunTicks(sim, observation, values, move, skill, pick);
                allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
            }
            Assert.That(allocated, Is.EqualTo(0L)); Assert.That(sim.SkillUses[5], Is.GreaterThan(0), "slot 5 really fired during the window");
        }

        private static void RunTicks(SurvivorSim sim, SurvivorObservation observation, float[] values, bool[] move, bool[] skill, bool[] pick)
        {
            for (int i = 0; i < 300; i++) { sim.Step(new SurvivorInput(i % 9, i % 7, 0)); observation.Write(sim, values); SurvivorActionMask.WriteMask(sim, move, skill, pick); }
        }

        [Test]
        public void Pilot_RejectsTheOldSkillBranch_AndAcceptsTheNewOne()
        {
            Assert.That(SurvivorPilot.Validate(SurvivorTestHelpers.Brain(skill: 5)), Is.Not.Null); Assert.That(SurvivorPilot.Validate(SurvivorTestHelpers.Brain(observation: 2264)), Is.Not.Null); Assert.That(SurvivorPilot.Validate(SurvivorTestHelpers.Brain()), Is.Null);
        }

        [Test]
        public void Config_RequiresSixSkillSlots()
        {
            SurvivorConfig config = SurvivorTestHelpers.Config(); config.ClassDef.ActiveSkills = new SkillDef[4]; Assert.Throws<ArgumentException>(() => config.Validate());
            Assert.DoesNotThrow(() => SurvivorTestHelpers.Config().Validate());
        }

        [Test]
        public void EvaluatorResult_HasSixSkillCounters()
        {
            SurvivorRunStats stats = new SurvivorEvaluator().RunOne(SurvivorTestHelpers.Brain(), SurvivorTestHelpers.Config(runSeconds: 60f), 1, true);
            Assert.That(stats.SkillUses.Length, Is.EqualTo(6));
        }
    }
}
