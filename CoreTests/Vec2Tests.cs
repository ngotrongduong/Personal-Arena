using NUnit.Framework;
using PersonalArena.Core;

namespace PersonalArena.CoreTests
{
    public class Vec2Tests
    {
        [Test]
        public void NormalizedHasUnitLength()
        {
            Assert.That(new Vec2(3f, 4f).Normalized().Length, Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void NormalizedZeroStaysZero()
        {
            Assert.That(Vec2.Zero.Normalized(), Is.EqualTo(Vec2.Zero));
        }
    }
}
