using NUnit.Framework;

namespace PersonalArena.View.Tests
{
    public sealed class InputLatchTests
    {
        [Test]
        public void Press_RemainsLatchedUntilTickConsumesIt()
        {
            InputLatch latch = new InputLatch();
            latch.Capture(true, false, false, false);
            latch.Capture(false, false, false, false);

            Assert.That(latch.ConsumeSkill(), Is.EqualTo(1));
            Assert.That(latch.ConsumeSkill(), Is.Zero);
        }

        [Test]
        public void Priority_IsBlockThenStrikeThenKickThenDash()
        {
            InputLatch latch = new InputLatch();
            latch.Capture(true, true, true, true);
            Assert.That(latch.ConsumeSkill(), Is.EqualTo(3));

            latch.Capture(true, true, true, false);
            Assert.That(latch.ConsumeSkill(), Is.EqualTo(1));

            latch.Capture(false, true, true, false);
            Assert.That(latch.ConsumeSkill(), Is.EqualTo(2));

            latch.Capture(false, false, true, false);
            Assert.That(latch.ConsumeSkill(), Is.EqualTo(4));
        }

        [Test]
        public void HeldBlock_IsReturnedOnEveryTick()
        {
            InputLatch latch = new InputLatch();
            latch.Capture(false, false, false, true);

            Assert.That(latch.ConsumeSkill(), Is.EqualTo(3));
            Assert.That(latch.ConsumeSkill(), Is.EqualTo(3));
        }
    }
}
