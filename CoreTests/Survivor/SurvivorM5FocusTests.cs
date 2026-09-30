using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    public sealed class SurvivorM5FocusTests
    {
        private static readonly TrainingFocus[] AllFocuses =
            { TrainingFocus.Balanced, TrainingFocus.Survival, TrainingFocus.Gold, TrainingFocus.Boss, TrainingFocus.Offense };

        [Test]
        public void Ids_Parse_DisplayNames()
        {
            Assert.That(TrainingFocusInfo.Count, Is.EqualTo(5));
            string[] ids = { "balanced", "survival", "gold", "boss", "offense" };
            string[] names = { "Cân bằng", "Sống sót", "Vàng", "Boss", "Tấn công" };
            for (int i = 0; i < AllFocuses.Length; i++)
            {
                Assert.That(TrainingFocusInfo.Id(AllFocuses[i]), Is.EqualTo(ids[i]));
                Assert.That(TrainingFocusInfo.DisplayName(AllFocuses[i]), Is.EqualTo(names[i]));
                Assert.That(TrainingFocusInfo.TryParse(ids[i], out TrainingFocus parsed), Is.True); Assert.That(parsed, Is.EqualTo(AllFocuses[i]));
                Assert.That(TrainingFocusInfo.TryParse("  " + ids[i].ToUpperInvariant() + " ", out parsed), Is.True); Assert.That(parsed, Is.EqualTo(AllFocuses[i]));
            }
            foreach (string bad in new[] { null, "", "   ", "speed", "gold!" })
            {
                Assert.That(TrainingFocusInfo.TryParse(bad, out TrainingFocus parsed), Is.False, bad ?? "null");
                Assert.That(parsed, Is.EqualTo(TrainingFocus.Balanced));
            }
        }

        [Test]
        public void ForFocus_ChangesOnlyTheTableFields()
        {
            SurvivorRewardConfig d = new SurvivorRewardConfig();
            AssertConfig(SurvivorRewardConfig.ForFocus(TrainingFocus.Balanced), d.HpLostPerMaxHp, d.Death, d.PerGold, d.BossDamage, d.PerLevelProgress, d.PerKill, d.PerEliteKill);
            AssertConfig(SurvivorRewardConfig.ForFocus(TrainingFocus.Survival), 1.6f, 6f, 0.0005f, d.BossDamage, d.PerLevelProgress, 0.002f, d.PerEliteKill);
            AssertConfig(SurvivorRewardConfig.ForFocus(TrainingFocus.Gold), d.HpLostPerMaxHp, d.Death, 0.003f, d.BossDamage, d.PerLevelProgress, d.PerKill, d.PerEliteKill);
            AssertConfig(SurvivorRewardConfig.ForFocus(TrainingFocus.Boss), d.HpLostPerMaxHp, d.Death, d.PerGold, 5f, d.PerLevelProgress, d.PerKill, d.PerEliteKill);
            AssertConfig(SurvivorRewardConfig.ForFocus(TrainingFocus.Offense), 0.7f, d.Death, d.PerGold, d.BossDamage, 0.1f, 0.008f, 0.2f);
            Assert.That(SurvivorRewardConfig.ForFocus(TrainingFocus.Gold), Is.Not.SameAs(SurvivorRewardConfig.ForFocus(TrainingFocus.Gold)));
        }

        [Test]
        public void Balanced_EqualsDefault_FieldByField()
        {
            SurvivorRewardConfig d = new SurvivorRewardConfig(), b = SurvivorRewardConfig.ForFocus(TrainingFocus.Balanced);
            foreach (System.Reflection.FieldInfo field in typeof(SurvivorRewardConfig).GetFields())
            {
                if (field.IsStatic) continue;
                Assert.That(field.GetValue(b), Is.EqualTo(field.GetValue(d)), field.Name);
            }
            // The default numbers are pinned; T-027 added the kill rows (warrior-s001 keeps learning, schema unchanged).
            Assert.That(new[] { d.Win, d.TimeUp, d.Death, d.SurvivePerSecond, d.HpLostPerMaxHp, d.PerLevelProgress, d.PerGold, d.BossDamage, d.PerDamage, d.PerKill, d.PerEliteKill },
                Is.EqualTo(new[] { 10f, 5f, 5f, 0.01f, 1f, 0.05f, 0.001f, 2f, 0f, 0.004f, 0.1f }));
        }

        [TestCaseSource(nameof(AllFocuses))]
        public void EveryFocus_Validates_KeepsSigns_AndHierarchy(TrainingFocus focus)
        {
            SurvivorRewardConfig config = SurvivorRewardConfig.ForFocus(focus);
            Assert.DoesNotThrow(config.Validate);
            SurvivorRewardCalculator calculator = new SurvivorRewardCalculator(config);
            Assert.That(calculator.Compute(Events(new SurvivorEvent(SurvivorEventType.RunWon)), 0f), Is.Positive, "win");
            Assert.That(calculator.Compute(Events(new SurvivorEvent(SurvivorEventType.RunTimeUp)), 0f), Is.Positive, "time up");
            Assert.That(calculator.Compute(Events(new SurvivorEvent(SurvivorEventType.HeroDied)), 0f), Is.Negative, "death");
            Assert.That(calculator.Compute(Events(new SurvivorEvent(SurvivorEventType.HeroDamaged, 10f, 0.1f)), 0f), Is.Negative, "hp lost");
            Assert.That(calculator.Compute(Events(new SurvivorEvent(SurvivorEventType.XpCollected, 5f, 0.2f)), 0f), Is.Positive, "xp");
            Assert.That(calculator.Compute(Events(new SurvivorEvent(SurvivorEventType.GoldCollected, 10f)), 0f), Is.Positive, "gold");
            Assert.That(calculator.Compute(Events(new SurvivorEvent(SurvivorEventType.BossDamaged, 100f, 0.1f)), 0f), Is.Positive, "boss");
            Assert.That(calculator.Compute(Events(), 1f), Is.Positive, "survive");
            Assert.That(calculator.Compute(Events(new SurvivorEvent(SurvivorEventType.EnemyKilled, 0f)), 0f), Is.Positive, "kill");
            Assert.That(calculator.Compute(Events(new SurvivorEvent(SurvivorEventType.EliteKilled)), 0f), Is.Positive, "elite kill");
            // A kill never outweighs a full max-HP of damage: trading heavy damage for one kill stays a loss.
            Assert.That(config.PerKill + config.PerEliteKill, Is.LessThan(config.HpLostPerMaxHp));
            // Hierarchy checked for every focus: Win > 900 s of survival, and Death > 0.
            Assert.That(config.Win, Is.GreaterThan(900f * config.SurvivePerSecond));
            Assert.That(config.Death, Is.GreaterThan(0f));
        }

        [TestCaseSource(nameof(AllFocuses))]
        public void EveryFocus_OutcomeDominates(TrainingFocus focus)
        {
            SurvivorRewardCalculator calculator = new SurvivorRewardCalculator(SurvivorRewardConfig.ForFocus(focus));
            // Same loot and progress (400 gold, 5 levels of progress, 90% of max HP lost); one dies at 300 s, one survives to 900 s.
            SurvivorEvent[] shared =
            {
                new SurvivorEvent(SurvivorEventType.GoldCollected, 400f),
                new SurvivorEvent(SurvivorEventType.XpCollected, 100f, 5f),
                new SurvivorEvent(SurvivorEventType.HeroDamaged, 90f, 0.9f)
            };
            // The run that dies also killed 400 extra enemies: reckless fighting must still lose to surviving.
            SurvivorEvent[] extraKills = new SurvivorEvent[400];
            for (int i = 0; i < extraKills.Length; i++) extraKills[i] = new SurvivorEvent(SurvivorEventType.EnemyKilled);
            float died = calculator.Compute(shared, 300f) + calculator.Compute(extraKills, 0f)
                + calculator.Compute(Events(new SurvivorEvent(SurvivorEventType.HeroDamaged, 10f, 0.1f), new SurvivorEvent(SurvivorEventType.HeroDied)), 0f);
            float survived = calculator.Compute(shared, 900f) + calculator.Compute(Events(new SurvivorEvent(SurvivorEventType.RunTimeUp)), 0f);
            Assert.That(died, Is.LessThan(survived));
        }

        private static void AssertConfig(SurvivorRewardConfig c, float hpLost, float death, float perGold, float boss, float level, float perKill, float perEliteKill)
        {
            SurvivorRewardConfig d = new SurvivorRewardConfig();
            Assert.That(c.HpLostPerMaxHp, Is.EqualTo(hpLost)); Assert.That(c.Death, Is.EqualTo(death)); Assert.That(c.PerGold, Is.EqualTo(perGold));
            Assert.That(c.BossDamage, Is.EqualTo(boss)); Assert.That(c.PerLevelProgress, Is.EqualTo(level));
            Assert.That(c.PerKill, Is.EqualTo(perKill)); Assert.That(c.PerEliteKill, Is.EqualTo(perEliteKill));
            Assert.That(c.Win, Is.EqualTo(d.Win)); Assert.That(c.TimeUp, Is.EqualTo(d.TimeUp)); Assert.That(c.SurvivePerSecond, Is.EqualTo(d.SurvivePerSecond)); Assert.That(c.PerDamage, Is.EqualTo(d.PerDamage));
        }

        private static IReadOnlyList<SurvivorEvent> Events(params SurvivorEvent[] events) => events;
    }
}
