using System;
using System.IO;
using NUnit.Framework;

namespace PersonalArena.View.Tests
{
    public sealed class ChampionInfoTests
    {
        // Shape written by Trainer/champion.py (summary from Tools/SurvivorEval, PascalCase keys).
        private const string ChampionJson = @"{
  ""run_id"": ""warrior-s001"",
  ""step"": 16999928,
  ""schema_version"": 4,
  ""score"": 812.5,
  ""passes_m4a"": true,
  ""summary"": {
    ""Runs"": 100,
    ""MedianSurvivedSeconds"": 655.5,
    ""P10SurvivedSeconds"": 431.0,
    ""MeanSurvivedSeconds"": 610.0,
    ""WinRate"": 0.12,
    ""CatastrophicCount"": 0,
    ""GoldPerMinute"": 14.2,
    ""XpPerMinute"": 310.0,
    ""DamageTakenPerMinute"": 22.0,
    ""MeanBossDamageFraction"": 0.4,
    ""MeanLevel"": 23.5,
    ""EndReasonCounts"": { ""Died"": 70, ""Won"": 12, ""TimeUp"": 18 },
    ""DeathCauseCounts"": { ""None"": 30, ""Surrounded"": 41, ""Boss"": 20, ""Projectile"": 9 },
    ""behavior"": { ""Aggression"": 0.5 }
  },
  ""behavior"": {
    ""Aggression"": 0.62, ""Caution"": 0.4, ""Greed"": 0.8, ""Exploration"": 0.3,
    ""CrowdControl"": -1, ""BossHunting"": 0.7, ""SkillDiscipline"": 0.55,
    ""PreferredRange"": 3.25, ""KeepDistance"": 4.1,
    ""KickUses"": 1200, ""BlockUses"": 300, ""DashUses"": 250,
    ""EffectiveKicks"": 900, ""EffectiveBlocks"": 210, ""EffectiveDashes"": 100
  },
  ""settings"": { ""seeds"": 100 },
  ""evaluated_at"": ""2026-09-30T10:22:33+00:00"",
  ""brain_file"": ""champion.brain""
}";

        private string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "ChampionInfoTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void ParseRecord_ReadsChampionJson()
        {
            ChampionRecord record = ChampionInfo.ParseRecord(ChampionJson);

            Assert.That(record, Is.Not.Null);
            Assert.That(record.run_id, Is.EqualTo("warrior-s001"));
            Assert.That(record.step, Is.EqualTo(16999928L));
            Assert.That(record.passes_m4a, Is.True);
            Assert.That(record.summary.Runs, Is.EqualTo(100));
            Assert.That(record.summary.MedianSurvivedSeconds, Is.EqualTo(655.5f));
            Assert.That(record.summary.P10SurvivedSeconds, Is.EqualTo(431f));
            Assert.That(record.summary.WinRate, Is.EqualTo(0.12f).Within(1e-6f));
            Assert.That(record.behavior.Aggression, Is.EqualTo(0.62f).Within(1e-6f));
            Assert.That(record.behavior.CrowdControl, Is.EqualTo(-1f));
            Assert.That(record.behavior.KickUses, Is.EqualTo(1200));
            Assert.That(record.When, Is.EqualTo("2026-09-30T10:22:33+00:00"));
        }

        [Test]
        public void ParseRecord_DeathCausesSkipNoneAndReadNumericKeys()
        {
            ChampionRecord record = ChampionInfo.ParseRecord(ChampionJson);

            Assert.That(record.DeathCauses.Count, Is.EqualTo(3));
            Assert.That(record.DeathCauses["Surrounded"], Is.EqualTo(41));
            Assert.That(record.DeathCauses["Boss"], Is.EqualTo(20));
            Assert.That(record.DeathCauses.ContainsKey("None"), Is.False);

            var numeric = ChampionInfo.ParseDeathCauses(@"{""DeathCauseCounts"": {""0"": 5, ""1"": 7, ""5"": 2}}");
            Assert.That(numeric["Surrounded"], Is.EqualTo(7));
            Assert.That(numeric["Projectile"], Is.EqualTo(2));
            Assert.That(numeric.ContainsKey("None"), Is.False);
        }

        [Test]
        public void ParseRecord_RejectsMissingOrInvalidText()
        {
            Assert.That(ChampionInfo.ParseRecord(null), Is.Null);
            Assert.That(ChampionInfo.ParseRecord("   "), Is.Null);
            Assert.That(ChampionInfo.ParseRecord("{\"step\": 5}"), Is.Null);
            // JsonUtility fills a missing summary with zeros; that is not an evaluation.
            Assert.That(ChampionInfo.ParseRecord("{\"run_id\": \"warrior-s001\", \"step\": 5}"), Is.Null);
        }

        [Test]
        public void Load_WithoutChampionFolder_HasNoChampion()
        {
            ChampionInfo info = ChampionInfo.Load(root, "Warrior");

            Assert.That(info.HasChampion, Is.False);
            Assert.That(info.LastEvaluation, Is.Null);
            Assert.That(ChampionInfo.Load(null, "Warrior").HasChampion, Is.False);
        }

        [Test]
        public void Load_FindsChampionLastEvaluationAndPreviousChampion()
        {
            string folder = Directory.CreateDirectory(BrainLocator.ChampionDirectory(root, "Warrior")).FullName;
            string history = Directory.CreateDirectory(Path.Combine(folder, "history")).FullName;
            File.WriteAllText(Path.Combine(folder, "champion.json"), ChampionJson);
            File.WriteAllText(Path.Combine(folder, "latest_eval.json"),
                ChampionJson.Replace("\"step\": 16999928", "\"step\": 18999900").Replace("\"passes_m4a\": true", "\"won\": false, \"passes_m4a\": false"));

            string current = Path.Combine(history, "warrior-s001-16999928.json");
            File.WriteAllText(current, ChampionJson);
            File.SetLastWriteTimeUtc(current, new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc));
            string older = Path.Combine(history, "warrior-s001-12000000.json");
            File.WriteAllText(older, ChampionJson.Replace("16999928", "12000000"));
            File.SetLastWriteTimeUtc(older, new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc));
            string oldest = Path.Combine(history, "warrior-s001-8000000.json");
            File.WriteAllText(oldest, ChampionJson.Replace("16999928", "8000000"));
            File.SetLastWriteTimeUtc(oldest, new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc));

            ChampionInfo info = ChampionInfo.Load(root, "Warrior");

            Assert.That(info.HasChampion, Is.True);
            Assert.That(info.Champion.step, Is.EqualTo(16999928L));
            Assert.That(info.LastEvaluation.step, Is.EqualTo(18999900L));
            Assert.That(info.LastEvaluation.won, Is.False);
            Assert.That(info.PreviousChampion, Is.Not.Null);
            Assert.That(info.PreviousChampion.step, Is.EqualTo(12000000L));
        }

        [Test]
        public void ProfileFormatting_IsReadable()
        {
            Assert.That(BehaviorProfilePanel.FormatSeconds(552f), Is.EqualTo("9:12"));
            Assert.That(BehaviorProfilePanel.FormatSeconds(-1f), Is.EqualTo("—"));
            Assert.That(BehaviorProfilePanel.FormatSteps(16999928L), Is.EqualTo("17.0M"));
            Assert.That(BehaviorProfilePanel.FormatSteps(2500L), Is.EqualTo("3K").Or.EqualTo("2K"));
            Assert.That(BehaviorProfilePanel.DeathCauseName("Surrounded"), Is.EqualTo("Surrounded"));
            Assert.That(BehaviorProfilePanel.DeathCauseName("Projectile"), Is.EqualTo("Projectiles"));
        }
    }
}
