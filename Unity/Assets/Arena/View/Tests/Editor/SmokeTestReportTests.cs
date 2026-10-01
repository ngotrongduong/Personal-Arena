using System.IO;
using NUnit.Framework;

namespace PersonalArena.View.Tests
{
    public sealed class SmokeTestReportTests
    {
        private string scratch;

        [SetUp]
        public void SetUp()
        {
            scratch = Path.Combine(Path.GetTempPath(), "pa-smoke-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(scratch);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(scratch))
            {
                Directory.Delete(scratch, true);
            }
        }

        private static SmokeClassResult PassingClass(string id)
        {
            return new SmokeClassResult
            {
                ClassId = id, Selected = true, RunStarted = true, OffersShown = 2, Picks = 2, EndReason = "Died",
                GoldAdded = 40, GoldBooked = true, Saved = true, NextRunStarted = true
            };
        }

        private static SmokeTestReport PassingReport()
        {
            SmokeTestReport report = new SmokeTestReport { PanelsPassed = true, FarmStarted = true, FarmBooked = true };
            report.Classes.Add(PassingClass("warrior"));
            report.Classes.Add(PassingClass("mage"));
            report.Classes.Add(PassingClass("archer"));
            return report;
        }

        [Test]
        public void AFullReportPasses()
        {
            Assert.That(PassingReport().Passed, Is.True);
        }

        [Test]
        public void EachClassCheckIsReportedInOrder()
        {
            SmokeClassResult result = PassingClass("mage");
            Assert.That(result.Problem, Is.Null);

            result.NextRunStarted = false;
            Assert.That(result.Problem, Is.EqualTo("next run did not start"));
            result.Saved = false;
            Assert.That(result.Problem, Is.EqualTo("profile not saved to disk"));
            result.GoldBooked = false;
            Assert.That(result.Problem, Is.EqualTo("gold not booked into the profile"));
            result.EndReason = null;
            Assert.That(result.Problem, Is.EqualTo("run did not end"));
            result.Picks = 0;
            Assert.That(result.Problem, Is.EqualTo("no level-up pick"));
            result.OffersShown = 0;
            Assert.That(result.Problem, Is.EqualTo("no level-up offer shown"));
            result.RunStarted = false;
            Assert.That(result.Problem, Is.EqualTo("run did not start"));
            result.Selected = false;
            Assert.That(result.Problem, Is.EqualTo("class not bought or selected"));
            Assert.That(result.Passed, Is.False);
        }

        [Test]
        public void AnyMissingPartFailsTheReport()
        {
            SmokeTestReport report = PassingReport();
            report.Classes[1].Picks = 0;
            Assert.That(report.Passed, Is.False);

            report = PassingReport();
            report.Classes.RemoveAt(2);
            Assert.That(report.Passed, Is.False);

            report = PassingReport();
            report.AddError("NullReferenceException: boom");
            Assert.That(report.Passed, Is.False);

            report = PassingReport();
            report.TimedOut = true;
            Assert.That(report.Passed, Is.False);

            report = PassingReport();
            report.PanelsPassed = false;
            Assert.That(report.Passed, Is.False);

            report = PassingReport();
            report.FarmBooked = false;
            Assert.That(report.Passed, Is.False);

            report = PassingReport();
            report.Refused = "no profile";
            Assert.That(report.Passed, Is.False);
        }

        [Test]
        public void ErrorsAreCapped()
        {
            SmokeTestReport report = new SmokeTestReport();
            for (int i = 0; i < SmokeTestReport.MaxErrors + 7; i++)
            {
                report.AddError("error " + i);
            }

            report.AddError(null);
            Assert.That(report.Errors.Count, Is.EqualTo(SmokeTestReport.MaxErrors));
            Assert.That(report.DroppedErrors, Is.EqualTo(8));
            Assert.That(report.Errors[0], Is.EqualTo("error 0"));
        }

        [Test]
        public void RefusesWithoutAScratchProfileOrABrain()
        {
            string brain = Path.Combine(scratch, "warrior.brain");
            File.WriteAllBytes(brain, new byte[] { 1, 2, 3 });
            string realProfile = Path.Combine(scratch, "Real Profile");
            string scratchProfile = Path.Combine(scratch, "profile");

            Assert.That(SmokeTestReport.RefusalReason(null, brain, realProfile), Does.Contain("-profile"));
            Assert.That(SmokeTestReport.RefusalReason("  ", brain, realProfile), Does.Contain("-profile"));
            Assert.That(SmokeTestReport.RefusalReason(scratchProfile, null, realProfile), Does.Contain("-brain"));
            Assert.That(SmokeTestReport.RefusalReason(scratchProfile, Path.Combine(scratch, "missing.brain"), realProfile),
                Does.Contain("not found"));
            Assert.That(SmokeTestReport.RefusalReason(realProfile, brain, realProfile), Does.Contain("real profile"));
            Assert.That(SmokeTestReport.RefusalReason(Path.Combine(realProfile, "sub"), brain, realProfile),
                Does.Contain("real profile"));
            Assert.That(SmokeTestReport.RefusalReason(scratchProfile, brain, realProfile), Is.Null);
        }

        [Test]
        public void SameOrInsideIgnoresCaseAndTrailingSeparators()
        {
            string root = Path.Combine(scratch, "Personal Arena");
            Assert.That(SmokeTestReport.IsSameOrInside(root.ToUpperInvariant() + Path.DirectorySeparatorChar, root), Is.True);
            Assert.That(SmokeTestReport.IsSameOrInside(Path.Combine(root, "profiles", "x"), root), Is.True);
            Assert.That(SmokeTestReport.IsSameOrInside(root + " Copy", root), Is.False);
            Assert.That(SmokeTestReport.IsSameOrInside(scratch, root), Is.False);
            Assert.That(SmokeTestReport.IsSameOrInside(null, root), Is.False);
            Assert.That(SmokeTestReport.IsSameOrInside(root, ""), Is.False);
        }

        [Test]
        public void JsonCarriesTheVerdictAndEscapesText()
        {
            SmokeTestReport report = PassingReport();
            report.PanelsChecked.Add("C: ok");
            report.Seconds = 123.456;
            string json = report.ToJson();

            Assert.That(json, Does.Contain("\"passed\": true"));
            Assert.That(json, Does.Contain("\"class\": \"archer\""));
            Assert.That(json, Does.Contain("\"problem\": null"));
            Assert.That(json, Does.Contain("\"checked\": [\"C: ok\"]"));
            Assert.That(json, Does.Contain("\"seconds\": 123.5"));

            report.AddError("bad \"quote\"\\path\nline");
            json = report.ToJson();
            Assert.That(json, Does.Contain("\"passed\": false"));
            Assert.That(json, Does.Contain("\"bad \\\"quote\\\"\\\\path\\nline\""));

            Assert.That(SmokeTestReport.Quote(null), Is.EqualTo("null"));
            Assert.That(SmokeTestReport.Quote("a\tb\u0001"), Is.EqualTo("\"a\\tb\\u0001\""));
        }
    }
}
