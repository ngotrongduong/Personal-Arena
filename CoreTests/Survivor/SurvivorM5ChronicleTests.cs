using System;
using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    public sealed class SurvivorM5ChronicleTests
    {
        [Test]
        public void FirstPick_AddsNewItem()
        {
            SurvivorSim sim = NewSim();
            RunChronicleRecorder recorder = new RunChronicleRecorder();
            int index = PickNewItem(sim, recorder);
            List<ChronicleEntry> news = Of(recorder, ChronicleKind.NewItem);
            Assert.That(news, Has.Count.EqualTo(1));
            Assert.That(news[0].ItemIndex, Is.EqualTo(index)); Assert.That(news[0].Level, Is.EqualTo(1));
            Assert.That(news[0].Time, Is.EqualTo(sim.Time));
        }

        [Test]
        public void ChestUpgradeToMax_AddsChestOpened_AndItemMaxed()
        {
            SurvivorSim sim = NewSim();
            int max = SurvivorCatalog.Get(0).MaxLevel;
            sim.GiveItemForTests(0, max - 1); // the starting weapon is the only item: the chest must upgrade it
            RunChronicleRecorder recorder = new RunChronicleRecorder();
            sim.SpawnPickupForTests(PickupKind.Chest, sim.Hero.Position, 0f);
            sim.Step(default); recorder.Observe(sim, SpectatorLabel.None);

            List<ChronicleEntry> chests = Of(recorder, ChronicleKind.ChestOpened);
            Assert.That(chests, Has.Count.EqualTo(1));
            Assert.That(chests[0].ItemIndex, Is.EqualTo(0)); Assert.That(chests[0].Level, Is.EqualTo(max));
            Assert.That(chests[0].Value, Is.EqualTo(sim.GetGold(GoldSource.Chest))); Assert.That(chests[0].Value, Is.GreaterThan(0f));
            List<ChronicleEntry> maxed = Of(recorder, ChronicleKind.ItemMaxed);
            Assert.That(maxed, Has.Count.EqualTo(1)); Assert.That(maxed[0].ItemIndex, Is.EqualTo(0)); Assert.That(maxed[0].Level, Is.EqualTo(max));
            Assert.That(Of(recorder, ChronicleKind.NewItem), Is.Empty);
        }

        [Test]
        public void NearDeath_OncePerDip_WithLowestRatio_AndGap()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(), 21); // not invulnerable: the brute hit below must land
            RunChronicleRecorder recorder = new RunChronicleRecorder();
            Hp(sim, recorder, 0.5f); Assert.That(Of(recorder, ChronicleKind.NearDeath), Is.Empty);
            Hp(sim, recorder, 0.15f); Hp(sim, recorder, 0.08f); Hp(sim, recorder, 0.3f); Hp(sim, recorder, 0.12f);
            List<ChronicleEntry> dips = Of(recorder, ChronicleKind.NearDeath);
            Assert.That(dips, Has.Count.EqualTo(1), "one entry per dip (not back above 0.4)");
            Assert.That(dips[0].Value, Is.EqualTo(0.08f).Within(1e-5f), "lowest ratio of the dip");
            Assert.That(dips[0].Cause, Is.EqualTo(DeathCause.None), "no hit taken yet");

            Hp(sim, recorder, 0.5f); Hp(sim, recorder, 0.1f);
            Assert.That(Of(recorder, ChronicleKind.NearDeath), Has.Count.EqualTo(1), "second dip within 60 s");

            sim.SetTimeForTests(61f);
            Hp(sim, recorder, 0.5f);
            SurvivorEnemy brute = sim.SpawnEnemyForTests(2, new Vec2(1.2f, 0f));
            sim.DamageHeroForTests(1f, brute, false);
            Assert.That(sim.LastHitCause, Is.EqualTo(DeathCause.Brute));
            Hp(sim, recorder, 0.1f);
            dips = Of(recorder, ChronicleKind.NearDeath);
            Assert.That(dips, Has.Count.EqualTo(2));
            Assert.That(dips[1].Value, Is.EqualTo(0.1f).Within(1e-5f)); Assert.That(dips[1].Cause, Is.EqualTo(DeathCause.Brute));
            Assert.That(dips[0].Value, Is.EqualTo(0.08f).Within(1e-5f), "an old dip is not touched");
        }

        [Test]
        public void NearDeath_WhenSurrounded_UsesSurrounded()
        {
            SurvivorSim sim = NewSim();
            RunChronicleRecorder recorder = new RunChronicleRecorder();
            int count = sim.Config.Tuning.SurroundedCount;
            for (int i = 0; i < count; i++) sim.SpawnEnemyForTests(0, Vec2.FromAngle(i * 2f * MathF.PI / count) * 0.8f);
            Assert.That(sim.TouchingEnemyCount(), Is.GreaterThanOrEqualTo(count));
            Hp(sim, recorder, 0.1f);
            List<ChronicleEntry> dips = Of(recorder, ChronicleKind.NearDeath);
            Assert.That(dips, Has.Count.EqualTo(1)); Assert.That(dips[0].Cause, Is.EqualTo(DeathCause.Surrounded));
        }

        [Test]
        public void StyleChange_FollowsTheMinuteRule()
        {
            SurvivorSim sim = NewSim(); sim.SetEnemiesInvulnerableForTests();
            RunChronicleRecorder recorder = new RunChronicleRecorder();
            // Minute 1: Charging all minute. Minute 2: Charging again (same style, no entry).
            // Minute 3: Looting 40 s + Kiting 20 s. Minute 4: Kiting 20 s only (33%, below 40%). Minute 5: None.
            Run(sim, recorder, 300f, t =>
            {
                int minute = (int)(t / 60f); float s = t - minute * 60f;
                switch (minute)
                {
                    case 0: case 1: return SpectatorLabel.Charging;
                    case 2: return s < 40f ? SpectatorLabel.Looting : SpectatorLabel.Kiting;
                    case 3: return s < 20f ? SpectatorLabel.Kiting : SpectatorLabel.None;
                    default: return SpectatorLabel.None;
                }
            });
            List<ChronicleEntry> styles = Of(recorder, ChronicleKind.StyleChange);
            Assert.That(styles, Has.Count.EqualTo(2));
            Assert.That(styles[0].Label, Is.EqualTo(SpectatorLabel.Charging)); Assert.That(styles[0].Time, Is.EqualTo(60f).Within(0.02f));
            Assert.That(styles[1].Label, Is.EqualTo(SpectatorLabel.Looting)); Assert.That(styles[1].Time, Is.EqualTo(180f).Within(0.02f));
        }

        [Test]
        public void EliteAndBoss_Entries_AndEnd()
        {
            SurvivorSim sim = NewSim();
            RunChronicleRecorder recorder = new RunChronicleRecorder();
            SurvivorEnemy elite = sim.SpawnEnemyForTests(0, new Vec2(8f, 0f), elite: true);
            sim.DamageEnemyForTests(elite, elite.MaxHp + 1f); recorder.Observe(sim, SpectatorLabel.None);
            sim.Step(default);
            SurvivorEnemy boss = sim.SpawnBossForTests(new Vec2(12f, 0f)); recorder.Observe(sim, SpectatorLabel.None);
            sim.Step(default);
            sim.DamageEnemyForTests(boss, boss.MaxHp + 1f); recorder.Observe(sim, SpectatorLabel.None);
            Assert.That(sim.EndReason, Is.EqualTo(EndReason.Won));
            recorder.Finish(sim); recorder.Finish(sim);
            recorder.Observe(sim, SpectatorLabel.Charging);

            RunChronicle r = recorder.Result;
            Assert.That(Kinds(r), Is.EqualTo(new[] { ChronicleKind.EliteKilled, ChronicleKind.BossSpawned, ChronicleKind.BossKilled, ChronicleKind.End }));
            ChronicleEntry end = r.Entries[r.Entries.Count - 1];
            Assert.That(end.Value, Is.EqualTo(sim.Time)); Assert.That(end.Cause, Is.EqualTo(sim.DeathCause));
            Assert.That(r.Finished, Is.True); Assert.That(r.EndReason, Is.EqualTo(EndReason.Won));
            Assert.That(r.SurvivedSeconds, Is.EqualTo(sim.Time)); Assert.That(r.Kills, Is.EqualTo(2)); Assert.That(r.Gold, Is.EqualTo(sim.Gold));
            Assert.That(r.Level, Is.EqualTo(sim.Level)); Assert.That(r.BossDamageFraction, Is.EqualTo(sim.BossDamageFraction));
            Assert.That(r.MinHpRatio, Is.EqualTo(sim.MinHpRatio));
            for (int i = 1; i < r.Entries.Count; i++) Assert.That(r.Entries[i].Time, Is.GreaterThanOrEqualTo(r.Entries[i - 1].Time));

            recorder.Reset();
            Assert.That(recorder.Result.Entries, Is.Empty); Assert.That(recorder.Result.Finished, Is.False);
        }

        [Test]
        public void Entries_CappedAt64_StyleChangeDroppedFirst_ThenNewItem()
        {
            SurvivorSim sim = NewSim(); sim.SetEnemiesInvulnerableForTests();
            RunChronicleRecorder recorder = new RunChronicleRecorder();
            PickNewItem(sim, recorder);
            Run(sim, recorder, 61f, t => SpectatorLabel.Charging);
            Assert.That(Of(recorder, ChronicleKind.StyleChange), Has.Count.EqualTo(1));
            int newItems = Of(recorder, ChronicleKind.NewItem).Count;
            Assert.That(newItems, Is.GreaterThanOrEqualTo(1));

            bool styleGone = false;
            for (int i = 0; i < 90; i++)
            {
                sim.SpawnPickupForTests(PickupKind.Chest, sim.Hero.Position, 0f);
                Step(sim, recorder, SpectatorLabel.None);
                Assert.That(recorder.Result.Entries.Count, Is.LessThanOrEqualTo(RunChronicleRecorder.Cap));
                if (!styleGone && Of(recorder, ChronicleKind.StyleChange).Count == 0)
                {
                    styleGone = true;
                    Assert.That(Of(recorder, ChronicleKind.NewItem), Has.Count.EqualTo(newItems), "the StyleChange went before any NewItem");
                }
            }
            Assert.That(styleGone, Is.True);
            Assert.That(recorder.Result.Entries, Has.Count.EqualTo(RunChronicleRecorder.Cap));
            Assert.That(Of(recorder, ChronicleKind.StyleChange), Is.Empty);
            Assert.That(Of(recorder, ChronicleKind.NewItem), Is.Empty);

            // A further style at the cap is dropped.
            Run(sim, recorder, sim.Time + 61f, t => SpectatorLabel.Looting);
            Assert.That(Of(recorder, ChronicleKind.StyleChange), Is.Empty);
            Assert.That(recorder.Result.Entries, Has.Count.EqualTo(RunChronicleRecorder.Cap));

            recorder.Finish(sim);
            Assert.That(recorder.Result.Entries, Has.Count.EqualTo(RunChronicleRecorder.Cap));
            Assert.That(recorder.Result.Entries[RunChronicleRecorder.Cap - 1].Kind, Is.EqualTo(ChronicleKind.End), "End always fits");
        }

        private static SurvivorSim NewSim()
        {
            SurvivorSim sim = new SurvivorSim(SurvivorTestHelpers.Config(runSeconds: 900f), 21);
            sim.SetHeroInvulnerableForTests();
            return sim;
        }

        /// <summary>Levels up and picks an offered item the hero does not own yet; returns its catalog index.</summary>
        private static int PickNewItem(SurvivorSim sim, RunChronicleRecorder recorder)
        {
            sim.GiveXpForTests(5f);
            Step(sim, recorder, SpectatorLabel.None);
            Assert.That(sim.IsAwaitingPick, Is.True);
            int pick = -1, index = -1;
            for (int i = 0; i < sim.OfferCount; i++) if (sim.GetOffer(i).NextLevel == 1) { pick = i + 1; index = sim.GetOffer(i).CatalogIndex; break; }
            Assert.That(pick, Is.GreaterThan(0), "an offer with a new item");
            sim.Step(new SurvivorInput(0, 0, pick)); recorder.Observe(sim, SpectatorLabel.None);
            while (sim.IsAwaitingPick) { sim.Step(new SurvivorInput(0, 0, 1)); recorder.Observe(sim, SpectatorLabel.None); }
            return index;
        }

        private static void Step(SurvivorSim sim, RunChronicleRecorder recorder, SpectatorLabel label)
        {
            sim.Step(sim.IsAwaitingPick ? new SurvivorInput(0, 0, 1) : default);
            recorder.Observe(sim, label);
        }

        private static void Run(SurvivorSim sim, RunChronicleRecorder recorder, float until, Func<float, SpectatorLabel> label)
        {
            while (sim.Time < until && !sim.IsEnded) Step(sim, recorder, label(sim.Time));
        }

        private static void Hp(SurvivorSim sim, RunChronicleRecorder recorder, float ratio)
        {
            sim.SetHeroHpForTests(sim.Hero.MaxHp * ratio);
            recorder.Observe(sim, SpectatorLabel.None);
        }

        private static List<ChronicleEntry> Of(RunChronicleRecorder recorder, ChronicleKind kind) =>
            recorder.Result.Entries.FindAll(e => e.Kind == kind);

        private static ChronicleKind[] Kinds(RunChronicle r) => r.Entries.ConvertAll(e => e.Kind).ToArray();
    }
}
