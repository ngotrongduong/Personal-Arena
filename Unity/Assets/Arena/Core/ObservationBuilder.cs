using System;

namespace PersonalArena.Core
{
    /// <summary>Builds the fixed-size vector observation consumed by an agent.</summary>
    /// <remarks>
    /// Hero block (19 values): 0 hp, 1 energy, 2-5 skill cooldowns, 6 blocking, 7 stun,
    /// 8-9 local velocity (forward, left), 10 platform radius / 40, 11 distance from centre / radius,
    /// 12-15 distance to the abyss forward / back / left / right (/20), 16 dashing,
    /// 17-18 local unit direction to the centre (forward, left). Rays follow.
    /// </remarks>
    public sealed class ObservationBuilder
    {
        public const int HeroObservationSize = 19;

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
            ArenaConfig config = sim.Config;
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
            buffer[8] = Math.Clamp(Vec2.Dot(hero.Velocity, forward) / moveSpeed, -4f, 4f);
            buffer[9] = Math.Clamp(Vec2.Dot(hero.Velocity, left) / moveSpeed, -4f, 4f);

            Vec2 centre = config.Center;
            float radius = config.Radius;
            Vec2 toCentre = centre - hero.Position;
            float centreDistance = toCentre.Length;
            buffer[10] = Clamp01(radius / 40f);
            buffer[11] = Clamp01(radius > 0f ? centreDistance / radius : 0f);
            buffer[12] = Clamp01(RaySensor.EdgeDistance(hero.Position, forward, centre, radius) / 20f);
            buffer[13] = Clamp01(RaySensor.EdgeDistance(hero.Position, -forward, centre, radius) / 20f);
            buffer[14] = Clamp01(RaySensor.EdgeDistance(hero.Position, left, centre, radius) / 20f);
            buffer[15] = Clamp01(RaySensor.EdgeDistance(hero.Position, -left, centre, radius) / 20f);
            buffer[16] = hero.DashRemaining > 0f ? 1f : 0f;
            if (centreDistance > 1e-4f)
            {
                Vec2 direction = toCentre / centreDistance;
                buffer[17] = Vec2.Dot(direction, forward);
                buffer[18] = Vec2.Dot(direction, left);
            }
            else
            {
                buffer[17] = 0f;
                buffer[18] = 0f;
            }

            raySensor.Write(sim, buffer, HeroObservationSize);
        }

        private static float SafeRatio(float value, float maximum) =>
            maximum > 0f ? Clamp01(value / maximum) : 0f;

        private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
    }
}
