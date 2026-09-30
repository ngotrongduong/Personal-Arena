using System;
using System.IO;
using System.Text.Json;
using NUnit.Framework;
using PersonalArena.Core;

namespace PersonalArena.CoreTests
{
    public sealed class PolicyBrainUpgradeTests
    {
        [Test]
        public void UpgradedFixturePreservesEveryOldLogit()
        {
            string root = PersonalArena.Core.Tests.Survivor.SurvivorObservationSchemaTests.FindRepoRoot();
            string fixtures = Path.Combine(root, "CoreTests", "Fixtures", "brain_upgrade");
            PolicyBrain oldBrain = PolicyBrain.Load(File.ReadAllBytes(Path.Combine(fixtures, "old.brain")));
            PolicyBrain newBrain = PolicyBrain.Load(File.ReadAllBytes(Path.Combine(fixtures, "new.brain")));
            using JsonDocument map = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixtures, "map.json")));
            JsonElement columns = map.RootElement.GetProperty("columns");
            JsonElement actions = map.RootElement.GetProperty("actions");

            Assert.That(columns.GetArrayLength(), Is.EqualTo(oldBrain.ObservationSize));
            Assert.That(actions.GetArrayLength(), Is.EqualTo(oldBrain.BranchCount));
            float[] oldObservation = new float[oldBrain.ObservationSize];
            float[] newObservation = new float[newBrain.ObservationSize];
            float[] oldLogits = new float[LogitCount(oldBrain)];
            float[] newLogits = new float[LogitCount(newBrain)];
            Random random = new Random(19019);
            for (int sample = 0; sample < 20; sample++)
            {
                Array.Clear(newObservation, 0, newObservation.Length);
                for (int i = 0; i < oldObservation.Length; i++)
                {
                    oldObservation[i] = (float)(random.NextDouble() * 2.0 - 1.0);
                    newObservation[columns[i].GetInt32()] = oldObservation[i];
                }
                oldBrain.Evaluate(oldObservation, oldLogits);
                newBrain.Evaluate(newObservation, newLogits);

                int oldOffset = 0;
                for (int branch = 0; branch < oldBrain.BranchCount; branch++)
                {
                    int newBranch = actions[branch][0].GetInt32();
                    int oldSize = actions[branch][1].GetInt32();
                    int newOffset = BranchOffset(newBrain, newBranch);
                    for (int action = 0; action < oldSize; action++)
                    {
                        Assert.That(newLogits[newOffset + action],
                            Is.EqualTo(oldLogits[oldOffset + action]).Within(1e-5f),
                            $"sample {sample}, branch {branch}, action {action}");
                    }
                    oldOffset += oldSize;
                }
            }
        }

        private static int LogitCount(PolicyBrain brain)
        {
            int count = 0;
            for (int i = 0; i < brain.BranchCount; i++) count += brain.BranchSize(i);
            return count;
        }

        private static int BranchOffset(PolicyBrain brain, int branch)
        {
            int offset = 0;
            for (int i = 0; i < branch; i++) offset += brain.BranchSize(i);
            return offset;
        }
    }
}
