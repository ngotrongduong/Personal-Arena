using System;
using PersonalArena.Core;

namespace PersonalArena.CoreTests
{
    internal static class TestHelpers
    {
        public static ArenaSim Sim(int zombieCount = 1, bool respawn = false, int seed = 7)
        {
            return new ArenaSim(DefaultDefs.Warrior(), new ArenaConfig
            {
                Width = 20f,
                Height = 20f,
                ZombieCount = zombieCount,
                RespawnKilledZombies = respawn,
                Seed = seed,
                ZombieSpawns = new[] { new ZombieSpawnEntry(DefaultDefs.Walker()) }
            });
        }

        public static void PlaceForAttack(ArenaSim sim, bool behindHero = false)
        {
            ZombieState zombie = sim.Zombies[0];
            sim.Hero.Position = new Vec2(10f, 10f);
            sim.Hero.Facing = MathF.PI * 0.5f;
            zombie.Position = behindHero ? new Vec2(10f, 9f) : new Vec2(10f, 11f);
            zombie.Facing = behindHero ? MathF.PI * 0.5f : -MathF.PI * 0.5f;
            zombie.AttackPhase = ZombieAttackPhase.Windup;
            zombie.AttackTimer = 0f;
            zombie.StunRemaining = 0f;
        }

        public static bool HasEvent(ArenaSim sim, SimEventType type)
        {
            for (int i = 0; i < sim.Events.Count; i++)
            {
                if (sim.Events[i].Type == type)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
