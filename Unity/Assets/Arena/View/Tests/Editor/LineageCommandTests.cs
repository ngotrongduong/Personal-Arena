using System;
using System.IO;
using NUnit.Framework;

namespace PersonalArena.View.Tests
{
    public sealed class LineageCommandTests
    {
        [Test]
        public void ParseResult_ReadsOkLine()
        {
            LineageResult result = LineageCommand.ParseResult(
                "Importing...\n{\"ok\": true, \"run_id\": \"warrior-s003\", \"id\": \"warrior-s001-96999889\", \"step\": 96999889}\n",
                0);

            Assert.That(result.ok, Is.True);
            Assert.That(result.run_id, Is.EqualTo("warrior-s003"));
            Assert.That(result.id, Is.EqualTo("warrior-s001-96999889"));
        }

        [Test]
        public void ParseResult_ReadsAWarningOnSuccess()
        {
            LineageResult result = LineageCommand.ParseResult(
                "{\"ok\": true, \"id\": \"warrior-s001-5\", \"warning\": \"Đã lưu phiên bản nhưng chưa chấm điểm được.\"}\n", 0);

            Assert.That(result.ok, Is.True);
            Assert.That(result.warning, Does.Contain("chưa chấm điểm"));
        }

        [Test]
        public void ParseResult_ReadsErrorLine()
        {
            LineageResult result = LineageCommand.ParseResult("{\"ok\": false, \"error\": \"no checkpoint.pt\"}\r\n", 1);

            Assert.That(result.ok, Is.False);
            Assert.That(result.error, Is.EqualTo("no checkpoint.pt"));
        }

        [Test]
        public void ParseResult_UsesTheLastJsonLine()
        {
            LineageResult result = LineageCommand.ParseResult(
                "{\"ok\": false, \"error\": \"early\"}\nlog line\n{\"ok\": true, \"imported\": 2}\n\n", 0);

            Assert.That(result.ok, Is.True);
        }

        [Test]
        public void ParseResult_GarbageOrEmptyMentionsTheExitCode()
        {
            LineageResult garbage = LineageCommand.ParseResult("Traceback (most recent call last):\n  boom\n", 2);
            Assert.That(garbage.ok, Is.False);
            Assert.That(garbage.error, Does.Contain("2"));

            LineageResult broken = LineageCommand.ParseResult("{\"ok\": tru", 1);
            Assert.That(broken.ok, Is.False);
            Assert.That(broken.error, Does.Contain("1"));

            Assert.That(LineageCommand.ParseResult(null, -1).ok, Is.False);
            Assert.That(LineageCommand.ParseResult(string.Empty, 0).error, Is.Not.Empty);
        }

        [Test]
        public void ParseResult_FailureWithoutMessageGetsOne()
        {
            LineageResult result = LineageCommand.ParseResult("{\"ok\": false}", 3);

            Assert.That(result.ok, Is.False);
            Assert.That(result.error, Does.Contain("3"));
        }

        [Test]
        public void Arguments_QuotePathsAndPassTheCommand()
        {
            string arguments = LineageCommand.Arguments(@"C:\Repo\Trainer\brain_lineage.py", @"C:\Repo\Trainer\runs", "Warrior",
                " fork --version warrior-s001-500 ");

            Assert.That(arguments, Is.EqualTo(
                "\"C:\\Repo\\Trainer\\brain_lineage.py\" --results-dir \"C:\\Repo\\Trainer\\runs\" --behavior Warrior fork --version warrior-s001-500"));
            Assert.That(LineageCommand.Arguments("s.py", "r", null, "sync"), Does.EndWith("--behavior Warrior sync"));
        }

        [Test]
        public void Constructor_FindsScriptNextToRuns()
        {
            string root = Path.Combine(Path.GetTempPath(), "LineageCommandTests-" + Guid.NewGuid().ToString("N"));
            LineageCommand command = new LineageCommand(Path.Combine(root, "Trainer", "runs"), null);

            Assert.That(command.Behavior, Is.EqualTo("Warrior"));
            Assert.That(command.RepositoryRoot, Is.EqualTo(Path.GetFullPath(root)));
            Assert.That(command.ScriptPath, Is.EqualTo(Path.Combine(Path.GetFullPath(root), "Trainer", "brain_lineage.py")));
            Assert.That(command.IsRunning, Is.False);
            Assert.That(command.MissingPiece(), Is.Not.Null, "nothing is installed in a temp folder");
            Assert.That(command.Poll(out LineageResult result, out string kind, out object tag), Is.False);
            Assert.That(result, Is.Null);
        }
    }
}
