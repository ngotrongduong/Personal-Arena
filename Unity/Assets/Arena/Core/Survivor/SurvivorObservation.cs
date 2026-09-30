using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    /// <summary>
    /// Observation schema v4 offsets: self 0..63, inventory 64..127, offers 128..391,
    /// rays 392..2191 (72 x 25), density 2192..2263 (3 rings x 8 sectors x 3 channels).
    /// Every value is finite and clamped to [-1, 1].
    /// </summary>
    public sealed class SurvivorObservation
    {
        public const int SchemaVersion = 4;
        public const int Size = 2264;
        public const int SelfOffset = 0;
        public const int InventoryOffset = 64;
        public const int OffersOffset = 128;
        public const int RaysOffset = 392;
        public const int RayStride = 25;
        public const int DensityOffset = 2192;
        private const float RayRange = 20f;
        private readonly bool[] moveMask = new bool[SurvivorInput.MoveBranchSize];
        private readonly bool[] skillMask = new bool[SurvivorInput.SkillBranchSize];
        private readonly bool[] pickMask = new bool[SurvivorInput.PickBranchSize];
        private readonly int[] densityEnemyCount = new int[24];
        private readonly float[] densityGemXp = new float[24];
        private readonly float[] densityApproach = new float[24];
        private readonly float[] solidDistances = new float[72];
        private readonly int[] solidKinds = new int[72];
        private readonly SurvivorEnemy[] solidEnemies = new SurvivorEnemy[72];
        private readonly float[] pickupDistances = new float[72];
        private readonly int[] pickupKinds = new int[72];
        private readonly Vec2[] rayDirections = new Vec2[72];

        public SurvivorObservation()
        {
            for (int i = 0; i < rayDirections.Length; i++) rayDirections[i] = Vec2.FromAngle(i * 5f * MathF.PI / 180f);
        }

        public void Write(SurvivorSim sim, float[] buffer)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            if (buffer == null || buffer.Length < Size) throw new ArgumentException("Observation buffer is too small.", nameof(buffer));
            Array.Clear(buffer, 0, Size);
            WriteSelf(sim, buffer);
            for (int i = 0; i < SurvivorCatalog.CatalogSize; i++) buffer[InventoryOffset + i] = sim.Inventory.Level(i) / 5f;
            WriteOffers(sim, buffer);
            Array.Clear(densityEnemyCount, 0, densityEnemyCount.Length); Array.Clear(densityGemXp, 0, densityGemXp.Length); Array.Clear(densityApproach, 0, densityApproach.Length);
            WriteRays(sim, buffer);
            WriteDensity(sim, buffer);
            for (int i = 0; i < Size; i++) buffer[i] = SafeClamp(buffer[i]);
        }

        private void WriteSelf(SurvivorSim sim, float[] b)
        {
            SurvivorHero h = sim.Hero; SurvivorDerivedStats s = sim.DerivedStats;
            b[0] = h.MaxHp > 0f ? h.Hp / h.MaxHp : 0f; b[1] = h.MaxHp / 500f; b[2] = h.MaxEnergy > 0f ? h.Energy / h.MaxEnergy : 0f;
            SurvivorActionMask.WriteMask(sim, moveMask, skillMask, pickMask);
            for (int i = 0; i < 4; i++)
            {
                SkillDef def = sim.Config.ClassDef.ActiveSkills[i];
                b[3 + i] = def == null || def.Cooldown <= 0f ? 0f : h.SkillCooldowns[i] / def.Cooldown;
                b[7 + i] = skillMask[i + 1] ? 1f : 0f;
            }
            b[11] = h.Blocking ? 1f : 0f; b[12] = h.Dashing ? 1f : 0f;
            b[14] = h.Velocity.X / 20f; b[15] = h.Velocity.Y / 20f;
            float half = sim.Config.MapHalfSize;
            b[16] = (half - h.Position.X) / 100f; b[17] = (h.Position.X + half) / 100f;
            b[18] = (half - h.Position.Y) / 100f; b[19] = (h.Position.Y + half) / 100f;
            b[20] = sim.Time / 1020f; b[21] = sim.Level / 50f; b[22] = sim.XpToNext > 0 ? sim.Xp / sim.XpToNext : 0f;
            b[23] = sim.Config.Build.Tier / 10f; b[24] = sim.IsAwaitingPick ? 1f : 0f; b[25] = sim.BossAlive ? 1f : 0f;
            SurvivorEnemy boss = sim.BossEnemy;
            if (boss != null)
            {
                Vec2 delta = boss.Position - h.Position; Vec2 direction = delta.Normalized();
                b[26] = direction.X; b[27] = direction.Y; b[28] = delta.Length / 50f; b[29] = boss.Hp / boss.MaxHp;
            }
            for (int i = 0; i < StatInfo.SlotCount; i++) { int cap = StatInfo.Cap((StatId)i); b[30 + i] = cap > 0 ? (float)sim.Config.Build.Points[i] / cap : 0f; }
            b[46] = sim.AliveEnemyCount / 300f; b[47] = MathF.Log10(1f + sim.Gold) / 4f;
            if (sim.LastMove >= 0 && sim.LastMove < 9) b[48 + sim.LastMove] = 1f;
            if (sim.LastSkill >= 0 && sim.LastSkill < 5) b[57 + sim.LastSkill] = 1f;
            b[62] = MathF.Cos(h.Facing); b[63] = MathF.Sin(h.Facing);
            _ = s;
        }

        private static void WriteOffers(SurvivorSim sim, float[] b)
        {
            for (int slot = 0; slot < sim.OfferCount; slot++)
            {
                (int index, int nextLevel) = sim.GetOffer(slot); int offset = OffersOffset + slot * 66;
                b[offset + index] = 1f; b[offset + 64] = nextLevel / 5f; b[offset + 65] = 1f;
            }
        }

        private void WriteRays(SurvivorSim sim, float[] b)
        {
            Vec2 origin = sim.Hero.Position;
            for (int ray = 0; ray < 72; ray++)
            {
                Vec2 direction = rayDirections[ray];
                solidDistances[ray] = WallDistance(origin, direction, sim.Config.MapHalfSize);
                solidKinds[ray] = 1; solidEnemies[ray] = null; pickupDistances[ray] = RayRange + 1f; pickupKinds[ray] = 0;
            }
            SurvivorObstacle[] obstacles = sim.ObstaclePool;
            for (int i = 0; i < sim.ActiveObstacleCount; i++)
            {
                SurvivorObstacle obstacle = obstacles[i]; AccumulateSolid(origin, obstacle.Position, obstacle.Radius, 1, null);
            }
            SurvivorEnemy[] enemies = sim.EnemyPool;
            for (int i = 0; i < sim.EnemyLimit; i++)
            {
                SurvivorEnemy enemy = enemies[i];
                if (enemy.Active) { AccumulateSolid(origin, enemy.Position, enemy.Radius, 2 + enemy.TypeIndex, enemy); AccumulateEnemyDensity(sim, origin, enemy); }
            }
            SurvivorPickup[] pickups = sim.PickupPool;
            for (int i = 0; i < sim.PickupLimit; i++)
            {
                SurvivorPickup pickup = pickups[i];
                if (pickup.Active) { AccumulatePickup(origin, pickup.Position, MathF.Max(pickup.Radius, 0.5f), (int)pickup.Kind); AccumulatePickupDensity(origin, pickup); }
            }
            for (int ray = 0; ray < 72; ray++)
            {
                Vec2 direction = rayDirections[ray]; int offset = RaysOffset + ray * RayStride;
                float solidDistance = solidDistances[ray]; SurvivorEnemy solidEnemy = solidEnemies[ray];
                if (solidDistance > RayRange) { b[offset] = 1f; b[offset + 12] = 1f; }
                else
                {
                    b[offset + solidKinds[ray]] = 1f; b[offset + 12] = solidDistance / RayRange;
                    if (solidEnemy != null)
                    {
                        b[offset + 13] = solidEnemy.Elite ? 1f : 0f; b[offset + 14] = solidEnemy.WindingUp ? 1f : 0f; b[offset + 15] = solidEnemy.StunRemaining > 0f ? 1f : 0f;
                        Vec2 towardHero = (origin - solidEnemy.Position).Normalized(); b[offset + 16] = Vec2.Dot(solidEnemy.Velocity - sim.Hero.Velocity, towardHero) / 10f;
                    }
                    else b[offset + 16] = Vec2.Dot(sim.Hero.Velocity, direction) / 10f;
                }
                b[offset + 17 + pickupKinds[ray]] = 1f; b[offset + 24] = pickupDistances[ray] <= RayRange ? pickupDistances[ray] / RayRange : 1f;
            }
        }

        private void AccumulateSolid(Vec2 origin, Vec2 center, float radius, int kind, SurvivorEnemy enemy)
        {
            ForCandidateRays(origin, center, radius, kind, enemy, false);
        }

        private void AccumulatePickup(Vec2 origin, Vec2 center, float radius, int kind)
        {
            ForCandidateRays(origin, center, radius, kind, null, true);
        }

        private void ForCandidateRays(Vec2 origin, Vec2 center, float radius, int kind, SurvivorEnemy enemy, bool pickup)
        {
            Vec2 delta = center - origin; float distance = delta.Length;
            if (distance - radius > RayRange || distance < 1e-6f) return;
            float angle = delta.Angle(); if (angle < 0f) angle += MathF.PI * 2f;
            float width = distance <= radius ? MathF.PI : MathF.Asin(MathF.Min(1f, radius / distance));
            int centerRay = (int)MathF.Round(angle * 36f / MathF.PI) % 72;
            int spread = Math.Min(36, (int)MathF.Ceiling(width * 36f / MathF.PI) + 1);
            for (int offset = -spread; offset <= spread; offset++)
            {
                int ray = centerRay + offset; while (ray < 0) ray += 72; while (ray >= 72) ray -= 72;
                Vec2 direction = rayDirections[ray]; float hit = RayCircle(origin, direction, center, radius);
                if (hit < 0f || hit > RayRange) continue;
                if (pickup)
                {
                    if (hit < pickupDistances[ray]) { pickupDistances[ray] = hit; pickupKinds[ray] = kind; }
                }
                else if (hit < solidDistances[ray]) { solidDistances[ray] = hit; solidKinds[ray] = kind; solidEnemies[ray] = enemy; }
            }
        }

        private void WriteDensity(SurvivorSim sim, float[] b)
        {
            for (int cell = 0; cell < 24; cell++)
            {
                int offset = DensityOffset + cell * 3; b[offset] = densityEnemyCount[cell] / 20f; b[offset + 1] = densityGemXp[cell] / 50f;
                b[offset + 2] = densityEnemyCount[cell] == 0 ? 0f : densityApproach[cell] / densityEnemyCount[cell] / 5f;
            }
            _ = sim;
        }

        private void AccumulateEnemyDensity(SurvivorSim sim, Vec2 origin, SurvivorEnemy enemy)
        {
            Vec2 delta = enemy.Position - origin; float distanceSquared = delta.LengthSquared; int ring = RingSquared(distanceSquared); if (ring < 0) return;
            int cell = ring * 8 + Sector(delta.Angle()); densityEnemyCount[cell]++;
            if (distanceSquared > 1e-8f) densityApproach[cell] += Vec2.Dot(enemy.Velocity - sim.Hero.Velocity, -delta / MathF.Sqrt(distanceSquared));
        }

        private void AccumulatePickupDensity(Vec2 origin, SurvivorPickup pickup)
        {
            if (pickup.Kind != PickupKind.Gem) return; Vec2 delta = pickup.Position - origin; int ring = RingSquared(delta.LengthSquared);
            if (ring >= 0) densityGemXp[ring * 8 + Sector(delta.Angle())] += pickup.Value;
        }

        private static int Ring(float distance) => distance < 5f ? 0 : distance < 12f ? 1 : distance < 30f ? 2 : -1;
        private static int RingSquared(float distanceSquared) => distanceSquared < 25f ? 0 : distanceSquared < 144f ? 1 : distanceSquared < 900f ? 2 : -1;
        private static int Sector(float angle)
        {
            int sector = (int)MathF.Floor((angle + MathF.PI / 8f) / (MathF.PI / 4f)); sector %= 8; if (sector < 0) sector += 8; return sector;
        }
        private static float WallDistance(Vec2 origin, Vec2 direction, float half)
        {
            float result = float.PositiveInfinity;
            if (direction.X > 1e-6f) result = MathF.Min(result, (half - origin.X) / direction.X);
            else if (direction.X < -1e-6f) result = MathF.Min(result, (-half - origin.X) / direction.X);
            if (direction.Y > 1e-6f) result = MathF.Min(result, (half - origin.Y) / direction.Y);
            else if (direction.Y < -1e-6f) result = MathF.Min(result, (-half - origin.Y) / direction.Y);
            return result;
        }
        private static float RayCircle(Vec2 origin, Vec2 direction, Vec2 center, float radius)
        {
            Vec2 toCenter = center - origin; float projection = Vec2.Dot(toCenter, direction); float perpendicularSq = toCenter.LengthSquared - projection * projection;
            float radiusSq = radius * radius; if (perpendicularSq > radiusSq) return -1f;
            float offset = MathF.Sqrt(MathF.Max(0f, radiusSq - perpendicularSq)); float near = projection - offset; float far = projection + offset;
            return near >= 0f ? near : far >= 0f ? far : -1f;
        }
        private static float SafeClamp(float value) { if (float.IsNaN(value) || float.IsInfinity(value)) return 0f; return value < -1f ? -1f : value > 1f ? 1f : value; }
    }
}
