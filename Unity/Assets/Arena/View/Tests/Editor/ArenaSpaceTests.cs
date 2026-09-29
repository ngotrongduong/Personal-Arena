using NUnit.Framework;
using PersonalArena.Core;
using UnityEngine;

namespace PersonalArena.View.Tests
{
    public sealed class ArenaSpaceTests
    {
        [Test]
        public void ToWorld_MapsSimulationYToWorldZ()
        {
            Vector3 world = ArenaSpace.ToWorld(new Vec2(3.5f, 7.25f), 1.2f);

            Assert.That(world.x, Is.EqualTo(3.5f));
            Assert.That(world.y, Is.EqualTo(1.2f));
            Assert.That(world.z, Is.EqualTo(7.25f));
            Assert.That(ArenaSpace.ToSim(world), Is.EqualTo(new Vec2(3.5f, 7.25f)));
        }

        [TestCase(0f, 90f)]
        [TestCase(1.57079637f, 0f)]
        [TestCase(3.14159274f, -90f)]
        public void YawDegrees_MapsFacingToUnityForward(float facing, float expectedYaw)
        {
            Assert.That(ArenaSpace.YawDegrees(facing), Is.EqualTo(expectedYaw).Within(0.001f));
        }
    }
}
