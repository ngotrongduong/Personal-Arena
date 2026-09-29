using System;

namespace PersonalArena.Core
{
    /// <summary>Builds the fixed-size vector observation consumed by an agent.</summary>
    public sealed class ObservationBuilder
    {
        public const int HeroObservationSize = 16;

        private readonly RaySensor raySensor;

        public ObservationBuilder()
            : this(new RaySensorConfig())
        {
        }

        public ObservationBuilder(RaySensorConfig config)
        {
            raySensor = new RaySensor(config);
        }

        public int Size => HeroObservationSize + raySensor.TotalSize;

        public void Write(ArenaSim sim, float[] buffer)
        {
            if (sim == null)
            {
                throw new ArgumentNullException(nameof(sim));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (buffer.Length < Size)
            {
                throw new ArgumentException("Buffer is too small for the observation.", nameof(buffer));
            }

            HeroState hero = sim.Hero;
            HeroClassDef heroDef = sim.HeroDef;
            buffer[0] = SafeRatio(hero.Hp, heroDef.MaxHp);
            buffer[1] = SafeRatio(hero.Energy, heroDef.MaxEnergy);
            for (int i = 0; i < 4; i++)
            {
                SkillDef skill = heroDef.Skills[i];
                buffer[2 + i] = skill == null || skill.Cooldown <= 0f
                    ? 0f
                    : Clamp01(hero.CooldownRemaining[i] / skill.Cooldown);
            }

            buffer[6] = hero.IsBlocking ? 1f : 0f;
            buffer[7] = Clamp01(hero.StunRemaining / 1.5f);
            Vec2 forward = Vec2.FromAngle(hero.Facing);
            Vec2 left = new Vec2(-forward.Y, forward.X);
            float moveSpeed = heroDef.MoveSpeed > 0f ? heroDef.MoveSpeed : 1f;
            buffer[8] = Vec2.Dot(hero.Velocity, forward) / moveSpeed;
            buffer[9] = Vec2.Dot(hero.Velocity, left) / moveSpeed;
            buffer[10] = Clamp01(sim.Config.Width / 80f);
            buffer[11] = Clamp01(sim.Config.Height / 80f);
            buffer[12] = Clamp01(WallDistance(hero.Position, forward, sim.Config) / 20f);
            buffer[13] = Clamp01(WallDistance(hero.Position, -forward, sim.Config) / 20f);
            buffer[14] = Clamp01(WallDistance(hero.Position, left, sim.Config) / 20f);
            buffer[15] = Clamp01(WallDistance(hero.Position, -left, sim.Config) / 20f);

            raySensor.Write(sim, buffer, HeroObservationSize);
        }

        private static float WallDistance(Vec2 origin, Vec2 direction, ArenaConfig config)
        {
            float nearest = float.PositiveInfinity;
            if (direction.X > 1e-6f)
            {
                nearest = MathF.Min(nearest, (config.Width - origin.X) / direction.X);
            }
            else if (direction.X < -1e-6f)
            {
                nearest = MathF.Min(nearest, -origin.X / direction.X);
            }

            if (direction.Y > 1e-6f)
            {
                nearest = MathF.Min(nearest, (config.Height - origin.Y) / direction.Y);
            }
            else if (direction.Y < -1e-6f)
            {
                nearest = MathF.Min(nearest, -origin.Y / direction.Y);
            }

            return nearest;
        }

        private static float SafeRatio(float value, float maximum) =>
            maximum > 0f ? Clamp01(value / maximum) : 0f;

        private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
    }
}
