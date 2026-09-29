using NUnit.Framework;

namespace PersonalArena.View.Tests
{
    public sealed class TrainingHistoryTests
    {
        [Test]
        public void Parse_ReadsHistoryAndFindsSeries()
        {
            const string json = "{\"run_id\":\"warrior-002\",\"behavior\":\"Warrior\"," +
                "\"rules_version\":2,\"updated_unix\":1790000000.0,\"last_step\":1200000," +
                "\"series\":[{\"tag\":\"Environment/Cumulative Reward\"," +
                "\"steps\":[30000,60000],\"values\":[-1.8,-1.0]}]}";

            TrainingHistory history = TrainingHistory.Parse(json);

            Assert.That(history, Is.Not.Null);
            Assert.That(history.run_id, Is.EqualTo("warrior-002"));
            Assert.That(history.behavior, Is.EqualTo("Warrior"));
            Assert.That(history.rules_version, Is.EqualTo(2));
            Assert.That(history.updated_unix, Is.EqualTo(1790000000.0).Within(1e-6));
            Assert.That(history.last_step, Is.EqualTo(1200000L));
            TrainingSeries reward = history.Find("Environment/Cumulative Reward");
            Assert.That(reward, Is.Not.Null);
            Assert.That(reward.steps, Is.EqualTo(new long[] { 30000, 60000 }));
            Assert.That(reward.values, Is.EqualTo(new[] { -1.8f, -1f }));
            Assert.That(history.Find("Arena/Kills"), Is.Null);
        }

        [Test]
        public void Parse_RejectsEmptyOrBrokenText()
        {
            Assert.That(TrainingHistory.Parse(null), Is.Null);
            Assert.That(TrainingHistory.Parse("{}"), Is.Null);
            Assert.That(TrainingHistory.Parse("{not json"), Is.Null);
        }

        [Test]
        public void Smooth_UsesATrailingMovingAverage()
        {
            Assert.That(TrainingHistory.Smooth(null, 3), Is.Empty);
            Assert.That(TrainingHistory.Smooth(new[] { 1f, 2f, 3f, 4f }, 3),
                Is.EqualTo(new[] { 1f, 1.5f, 2f, 3f }));
            Assert.That(TrainingHistory.Smooth(new[] { 2f, 4f }, 0), Is.EqualTo(new[] { 2f, 4f }));
        }
    }
}
