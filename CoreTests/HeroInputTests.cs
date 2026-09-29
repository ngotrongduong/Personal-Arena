using NUnit.Framework;
using PersonalArena.Core;

namespace PersonalArena.CoreTests
{
    public class HeroInputTests
    {
        [TestCase(0, 0, 0)]
        [TestCase(1, 0, 1)]
        [TestCase(1, 1, 2)]
        [TestCase(0, 1, 3)]
        [TestCase(-1, 1, 4)]
        [TestCase(-1, 0, 5)]
        [TestCase(-1, -1, 6)]
        [TestCase(0, -1, 7)]
        [TestCase(1, -1, 8)]
        [TestCase(5, -3, 8)]
        public void MoveFromAxesMapsCounterClockwiseFromPlusX(int dx, int dy, int expected)
        {
            Assert.That(HeroInput.MoveFromAxes(dx, dy), Is.EqualTo(expected));
        }

        [Test]
        public void TurnTowardPicksShortestDirection()
        {
            Assert.That(HeroInput.TurnToward(0f, 1f, 0.05f), Is.EqualTo(1));
            Assert.That(HeroInput.TurnToward(0f, -1f, 0.05f), Is.EqualTo(-1));
            Assert.That(HeroInput.TurnToward(3f, -3f, 0.05f), Is.EqualTo(1));
            Assert.That(HeroInput.TurnToward(1f, 1.02f, 0.05f), Is.EqualTo(0));
        }

        [Test]
        public void TurnBranchRoundTrips()
        {
            for (int turn = -1; turn <= 1; turn++)
                Assert.That(HeroInput.BranchToTurn(HeroInput.TurnToBranch(turn)), Is.EqualTo(turn));
        }
    }
}
