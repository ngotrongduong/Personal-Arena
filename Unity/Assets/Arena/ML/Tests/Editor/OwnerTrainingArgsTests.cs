using NUnit.Framework;
using PersonalArena.Core.Survivor;

namespace PersonalArena.ML.Tests
{
    public sealed class OwnerTrainingArgsTests
    {
        private const string SixteenValues = "3,0,1,5,2,0,0,0,0,0,0,4,0,0,0,0";

        [Test]
        public void NoArgumentsMeansRandomBuildsAndBalancedFocus()
        {
            OwnerTrainingArgs args = OwnerTrainingArgs.Parse(new[] { "PersonalArenaTraining.exe", "--hero-class", "warrior" });

            Assert.IsNull(args.OwnBuild);
            Assert.AreEqual(TrainingFocus.Balanced, args.Focus);
            Assert.IsEmpty(args.Warnings);
            StringAssert.Contains("random builds only", args.Describe());
        }

        [Test]
        public void NullArgumentsAreSafe()
        {
            OwnerTrainingArgs args = OwnerTrainingArgs.Parse(null);

            Assert.IsNull(args.OwnBuild);
            Assert.AreEqual(TrainingFocus.Balanced, args.Focus);
        }

        [Test]
        public void ReadsBuildTierAndFocus()
        {
            OwnerTrainingArgs args = OwnerTrainingArgs.Parse(new[]
            {
                "x.exe", "--owner-build", SixteenValues, "--owner-tier", "4", "--training-focus", "GOLD"
            });

            Assert.IsNotNull(args.OwnBuild);
            Assert.AreEqual(3, args.OwnBuild.Points[0]);
            Assert.AreEqual(5, args.OwnBuild.Points[3]);
            Assert.AreEqual(4, args.OwnBuild.Points[11]);
            Assert.AreEqual(15, args.OwnBuild.Level);
            Assert.AreEqual(4, args.OwnBuild.Tier);
            Assert.AreEqual(TrainingFocus.Gold, args.Focus);
            Assert.IsEmpty(args.Warnings);
            args.OwnBuild.Validate();
            StringAssert.Contains("tier 4", args.Describe());
            StringAssert.Contains("focus gold", args.Describe());
        }

        [Test]
        public void MissingTierDefaultsToOne()
        {
            OwnerTrainingArgs args = OwnerTrainingArgs.Parse(new[] { "x.exe", "--owner-build", SixteenValues });

            Assert.AreEqual(1, args.OwnBuild.Tier);
            Assert.IsEmpty(args.Warnings);
        }

        [TestCase("0", 1)]
        [TestCase("11", 10)]
        [TestCase("abc", 1)]
        public void BadTiersAreClampedWithAWarning(string tier, int expected)
        {
            OwnerTrainingArgs args = OwnerTrainingArgs.Parse(new[] { "x.exe", "--owner-build", SixteenValues, "--owner-tier", tier });

            Assert.AreEqual(expected, args.OwnBuild.Tier);
            Assert.AreEqual(1, args.Warnings.Count);
        }

        [Test]
        public void WrongValueCountIgnoresTheBuild()
        {
            OwnerTrainingArgs args = OwnerTrainingArgs.Parse(new[] { "x.exe", "--owner-build", "1,2,3", "--owner-tier", "3" });

            Assert.IsNull(args.OwnBuild);
            Assert.AreEqual(1, args.Warnings.Count);
        }

        [Test]
        public void NonIntegerValueIgnoresTheBuild()
        {
            OwnerTrainingArgs args = OwnerTrainingArgs.Parse(new[] { "x.exe", "--owner-build", "1,2,x,0,0,0,0,0,0,0,0,0,0,0,0,0" });

            Assert.IsNull(args.OwnBuild);
            Assert.AreEqual(1, args.Warnings.Count);
        }

        [Test]
        public void ValuesAreClampedToStatCapsAndReservedSlotsToZero()
        {
            OwnerTrainingArgs args = OwnerTrainingArgs.Parse(new[]
            {
                "x.exe", "--owner-build", "99,-4,0,0,0,0,0,0,0,0,0,0,0,0,0,7"
            });

            Assert.AreEqual(StatInfo.Cap(StatId.MaxHp), args.OwnBuild.Points[0]);
            Assert.AreEqual(0, args.OwnBuild.Points[1]);
            Assert.AreEqual(0, args.OwnBuild.Points[15]);
            Assert.AreEqual(3, args.Warnings.Count);
            args.OwnBuild.Validate();
        }

        [Test]
        public void UnknownFocusFallsBackToBalancedWithAWarning()
        {
            OwnerTrainingArgs args = OwnerTrainingArgs.Parse(new[] { "x.exe", "--training-focus", "speedrun" });

            Assert.AreEqual(TrainingFocus.Balanced, args.Focus);
            Assert.AreEqual(1, args.Warnings.Count);
        }

        [Test]
        public void FocusWorksWithoutABuild()
        {
            OwnerTrainingArgs args = OwnerTrainingArgs.Parse(new[] { "x.exe", "--training-focus", "survival" });

            Assert.IsNull(args.OwnBuild);
            Assert.AreEqual(TrainingFocus.Survival, args.Focus);
        }
    }
}
