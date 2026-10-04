using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    public sealed partial class SurvivorSim
    {
        private SurvivorEnemyProjectile SpawnEnemyProjectile(Vec2 point, Vec2 velocity, float radius, float damage, float lifetime, int sourceId)
        {
            SurvivorEnemyProjectile projectile = null;
            for (int i = 0; i < enemyProjectileLimit; i++)
            {
                if (!enemyProjectiles[i].Active) { projectile = enemyProjectiles[i]; break; }
            }
            if (projectile == null && enemyProjectileLimit < enemyProjectiles.Length) projectile = enemyProjectiles[enemyProjectileLimit++];
            if (projectile == null) return null;
            projectile.Active = true; projectile.Id = nextEnemyProjectileId++; projectile.Position = point;
            projectile.Velocity = velocity; projectile.Radius = radius; projectile.Damage = damage;
            projectile.Lifetime = lifetime; projectile.SourceId = sourceId; projectile.JustSpawned = true;
            return projectile;
        }

        private void UpdateEnemyProjectiles()
        {
            for (int i = 0; i < enemyProjectileLimit; i++)
            {
                SurvivorEnemyProjectile projectile = enemyProjectiles[i]; if (!projectile.Active) continue;
                if (projectile.JustSpawned) { projectile.JustSpawned = false; continue; }
                projectile.Position += projectile.Velocity * FixedDeltaTime;
                projectile.Lifetime -= FixedDeltaTime;
                if (projectile.Lifetime <= 0f || MathF.Abs(projectile.Position.X) > Config.MapHalfSize || MathF.Abs(projectile.Position.Y) > Config.MapHalfSize ||
                    OverlapsObstacle(projectile.Position, projectile.Radius))
                {
                    projectile.Active = false; continue;
                }
                float hitRadius = Hero.Radius + projectile.Radius;
                if ((projectile.Position - Hero.Position).LengthSquared > hitRadius * hitRadius) continue;
                projectile.Active = false;
                bool blocked = BlockCovers(projectile.Position);
                if (blocked)
                {
                    AddEvent(SurvivorEventType.Blocked, id: projectile.SourceId);
                    continue;
                }
                if (testInvulnerable || TryAbsorbHit()) continue;
                bool killed = ApplyHeroDamage(projectile.Damage, projectile.SourceId);
                LastHitCause = DeathCause.Projectile;
                if (killed)
                {
                    KillHero(null, false, true, projectile.SourceId);
                    return;
                }
            }
        }
    }
}
