using System;
using System.IO;
using NUnit.Framework;

namespace PersonalArena.View.Tests
{
    public sealed class TrainingServiceClientTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 29, 16, 0, 0, DateTimeKind.Utc);
        private static readonly double NowUnix = TrainingServiceClient.ToUnix(Now);

        private static TrainingStatus Status(string state, double ageSeconds)
        {
            return new TrainingStatus { state = state, updated_unix = NowUnix - ageSeconds };
        }

        [Test]
        public void Parse_ReadsTheServiceStatusFile()
        {
            TrainingStatus status = TrainingStatus.Parse(
                "{\"state\": \"training\", \"message\": \"The AI is training.\", \"run_id\": \"warrior-001\", " +
                "\"pid\": 12, \"trainer_pid\": 34, \"step\": 7512000, \"max_steps\": 30000000, " +
                "\"has_reward\": true, \"mean_reward\": 95.25, \"zombies\": 16.0, \"session_seconds\": 750.5, " +
                "\"updated_unix\": 1790000000.25, \"steps\": [30000, 60000], \"rewards\": [-0.5, 12.25]}");

            Assert.That(status.state, Is.EqualTo("training"));
            Assert.That(status.run_id, Is.EqualTo("warrior-001"));
            Assert.That(status.step, Is.EqualTo(7512000L));
            Assert.That(status.has_reward, Is.True);
            Assert.That(status.mean_reward, Is.EqualTo(95.25f));
            Assert.That(status.zombies, Is.EqualTo(16f));
            Assert.That(status.updated_unix, Is.EqualTo(1790000000.25).Within(1e-6));
            Assert.That(status.steps, Is.EqualTo(new long[] { 30000, 60000 }));
            Assert.That(status.rewards, Is.EqualTo(new[] { -0.5f, 12.25f }));
        }

        [Test]
        public void Parse_ReadsZombieMix()
        {
            TrainingStatus status = TrainingStatus.Parse(
                "{\"state\": \"training\", \"behavior\": \"Mage\", " +
                "\"zombie_mix\": {\"walker\": 1.0, \"runner\": 0.5, \"brute\": 0.0, \"spitter\": 0.25}}");

            Assert.That(status.behavior, Is.EqualTo("Mage"));
            Assert.That(status.zombie_mix, Is.Not.Null);
            Assert.That(status.zombie_mix.runner, Is.EqualTo(0.5f));
            Assert.That(status.zombie_mix.Describe(), Is.EqualTo("walker+runner+spitter"));
        }

        [Test]
        public void ZombieMix_DescribesWalkersOnly()
        {
            Assert.That(new ZombieMix { walker = 1f }.Describe(), Is.EqualTo("walkers only"));
            Assert.That(new ZombieMix { walker = 1f, runner = 1f, brute = 1f, spitter = 1f }.Describe(),
                Is.EqualTo("walker+runner+brute+spitter"));
        }

        [Test]
        public void Parse_RejectsEmptyOrBrokenText()
        {
            Assert.That(TrainingStatus.Parse(null), Is.Null);
            Assert.That(TrainingStatus.Parse("{}"), Is.Null);
            Assert.That(TrainingStatus.Parse("{not json"), Is.Null);
        }

        [Test]
        public void Classify_UsesFreshActiveStates()
        {
            Assert.That(Classify(Status("training", 3)), Is.EqualTo(TrainingState.Training));
            Assert.That(Classify(Status("starting", 3)), Is.EqualTo(TrainingState.Starting));
            Assert.That(Classify(Status("stopping", 3)), Is.EqualTo(TrainingState.Stopping));
        }

        [Test]
        public void Classify_TreatsAStaleActiveStateAsIdle()
        {
            Assert.That(Classify(Status("training", 600)), Is.EqualTo(TrainingState.Idle));
        }

        [Test]
        public void Classify_KeepsFinalStatesUntilSomethingNewer()
        {
            Assert.That(Classify(Status("stopped", 600)), Is.EqualTo(TrainingState.Stopped));
            Assert.That(Classify(Status("error", 600)), Is.EqualTo(TrainingState.Error));
            Assert.That(Classify(null), Is.EqualTo(TrainingState.Idle));
        }

        [Test]
        public void Classify_DetectsTrainingStartedOutsideTheGame()
        {
            Assert.That(Classify(null, Now.AddSeconds(-20)), Is.EqualTo(TrainingState.External));
            Assert.That(Classify(Status("stopped", 3600), Now.AddSeconds(-20)), Is.EqualTo(TrainingState.External));
            // The service's own final write and log line happen together: not external.
            Assert.That(Classify(Status("stopped", 20), Now.AddSeconds(-20)), Is.EqualTo(TrainingState.Stopped));
            // Old logs do not count.
            Assert.That(Classify(null, Now.AddMinutes(-10)), Is.EqualTo(TrainingState.Idle));
        }

        [Test]
        public void Classify_ShowsStartingRightAfterLaunch()
        {
            DateTime launched = Now.AddSeconds(-2);

            Assert.That(Classify(Status("stopped", 600), null, launched), Is.EqualTo(TrainingState.Starting));
            Assert.That(Classify(Status("error", 1), null, launched), Is.EqualTo(TrainingState.Error));
        }

        [Test]
        public void LaunchFailure_IsNotHiddenByAnOlderFinalStatus()
        {
            DateTime launched = Now.AddSeconds(-5);

            Assert.That(TrainingServiceClient.LaunchFailure(1, Status("stopped", 600), launched), Does.Contain("closed right away"));
            Assert.That(TrainingServiceClient.LaunchFailure(3, null, launched), Does.Contain("already running"));
            // The service explained itself after this launch.
            Assert.That(TrainingServiceClient.LaunchFailure(1, Status("error", 1), launched), Is.Null);
        }

        [Test]
        public void Power_BecomesServiceArguments()
        {
            TrainingPower power = new TrainingPower("FAST", 8, 16, 20f);

            Assert.That(power.Fighters, Is.EqualTo(128));
            Assert.That(power.Arguments(), Is.EqualTo(" --num-envs 8 --arena-agents 16 --time-scale 20.0"));
        }

        [Test]
        public void Parse_ReadsThePowerFields()
        {
            TrainingStatus status = TrainingStatus.Parse(
                "{\"state\": \"training\", \"num_envs\": 8, \"arena_agents\": 32, \"cpu\": true}");

            Assert.That(status.num_envs, Is.EqualTo(8));
            Assert.That(status.arena_agents, Is.EqualTo(32));
            Assert.That(status.cpu, Is.True);
        }

        [Test]
        public void Paths_AreResolvedFromTheRunsFolder()
        {
            string runs = Path.Combine(Path.GetTempPath(), "PA-Root", "Trainer", "runs");
            TrainingServiceClient client = new TrainingServiceClient(runs);
            string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PA-Root"));

            Assert.That(client.RepositoryRoot, Is.EqualTo(root));
            Assert.That(client.PythonPath, Is.EqualTo(Path.Combine(root, ".venv-ml", "Scripts", "python.exe")));
            Assert.That(client.ScriptPath, Is.EqualTo(Path.Combine(root, "Trainer", "train_service.py")));
            Assert.That(client.StopPath, Is.EqualTo(Path.Combine(Path.GetFullPath(runs), "training_service.stop")));
            Assert.That(client.MissingPiece(), Is.Not.Null);
        }

        [Test]
        public void Bucket_AveragesLongHistoriesAndKeepsShortOnes()
        {
            Assert.That(TrainingHistory.Bucket(null, 4), Is.Empty);
            Assert.That(TrainingHistory.Bucket(new[] { 1f, 2f }, 4), Is.EqualTo(new[] { 1f, 2f }));
            Assert.That(TrainingHistory.Bucket(new[] { 1f, 3f, 5f, 7f, 9f, 11f }, 3), Is.EqualTo(new[] { 2f, 6f, 10f }));
        }

        [Test]
        public void Parse_ReadsOwnerTrainingFields()
        {
            TrainingStatus status = TrainingStatus.Parse(
                "{\"state\": \"training\", \"training_focus\": \"gold\", \"owner_build\": true, \"owner_tier\": 3}");

            Assert.That(status.training_focus, Is.EqualTo("gold"));
            Assert.That(status.owner_build, Is.True);
            Assert.That(status.owner_tier, Is.EqualTo(3));
        }

        [Test]
        public void Parse_OldStatusFilesHaveNoOwnerTraining()
        {
            TrainingStatus status = TrainingStatus.Parse("{\"state\": \"training\", \"step\": 5}");

            Assert.That(status.training_focus, Is.Null.Or.Empty);
            Assert.That(status.owner_build, Is.False);
            Assert.That(status.owner_tier, Is.EqualTo(0));
        }

        [Test]
        public void OwnerTrainingArguments_EmptyWithoutOwner()
        {
            Assert.That(OwnerTraining.Arguments(null), Is.Empty);
            Assert.That(OwnerTraining.Arguments(new OwnerTraining()), Is.Empty);
        }

        [Test]
        public void OwnerTrainingArguments_BuildTierAndFocus()
        {
            var owner = new OwnerTraining
            {
                Points = new[] { 5, 0, 2, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                Tier = 4,
                FocusId = " Gold ",
            };

            Assert.That(OwnerTraining.Arguments(owner), Is.EqualTo(
                " --owner-build 5,0,2,0,3,0,0,0,0,0,0,0,0,0,0,0 --owner-tier 4 --training-focus gold"));
        }

        [TestCase(0, 1)]
        [TestCase(12, 10)]
        public void OwnerTrainingArguments_ClampsTheTier(int tier, int expected)
        {
            var owner = new OwnerTraining { Points = new int[16], Tier = tier };

            Assert.That(OwnerTraining.Arguments(owner), Does.EndWith(" --owner-tier " + expected));
        }

        [Test]
        public void OwnerTrainingArguments_FocusOnly()
        {
            Assert.That(OwnerTraining.Arguments(new OwnerTraining { FocusId = "survival" }),
                Is.EqualTo(" --training-focus survival"));
        }

        [Test]
        public void OwnerTrainingArguments_ClampsPointsAndDropsBadInput()
        {
            var owner = new OwnerTraining
            {
                // MaxHp cap 20, Armor cap 10, reserved slot 13 must be 0.
                Points = new[] { 99, -3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 7, 0, 0 },
                Tier = 2,
                FocusId = "gold --evil",
            };

            Assert.That(OwnerTraining.Arguments(owner), Is.EqualTo(
                " --owner-build 20,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0 --owner-tier 2"));
            Assert.That(OwnerTraining.Arguments(new OwnerTraining { Points = new int[5], FocusId = "boss" }),
                Is.EqualTo(" --training-focus boss"));
        }

        private static TrainingState Classify(TrainingStatus status, DateTime? newestLog = null, DateTime? launched = null)
        {
            return TrainingServiceClient.Classify(status, NowUnix, newestLog, Now, launched);
        }
    }
}
