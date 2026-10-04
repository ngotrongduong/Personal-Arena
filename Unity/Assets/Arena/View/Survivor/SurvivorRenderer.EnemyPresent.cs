using System.Collections.Generic;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PersonalArena.View
{
    /// <summary>Enemy views each frame: animation, culling, tint and death, plus the effects of enemy events.</summary>
    public sealed partial class SurvivorRenderer
    {
        private void UpdateEnemyAnimationState(EnemyView view, SurvivorEnemy enemy, Vector3 hero, float heroRadius)
        {
            CharacterAnimator animator = view.Animator;
            bool winding = enemy.WindingUp;
            if (winding && !view.WasWinding && view.Look == LookCharger)
            {
                // A charger announces its dash.
                effects.Shockwave(view.Current, HurtColor, 3.2f, 0.7f);
                effects.Text(view.Current + Vector3.up * 2.6f, "!", HurtColor, 1.8f, 0.8f);
            }
            if (animator == null)
            {
                view.WasWinding = winding;
                return;
            }

            if (winding && !view.WasWinding)
            {
                SurvivorEnemyDef def = SurvivorDefaults.EnemyDef(enemy.TypeIndex);
                float windup = def != null && def.WindupSeconds > 0f ? def.WindupSeconds : 0.9f;
                // Spitters and necromancers cast from range; everyone else swings.
                bool ranged = view.Look == LookSpitter || view.Look == LookNecromancer;
                animator.PlayOneShot(ranged ? WalkerThrowState : WalkerAttack, Mathf.Clamp(0.55f / windup, 0.6f, 1.4f), false);
            }
            view.WasWinding = winding;

            view.Claw -= SurvivorSim.FixedDeltaTime;
            if (!winding && view.Look != LookSpitter && view.Look != LookNecromancer && view.Look != LookShaman && view.Claw <= 0f && enemy.StunRemaining <= 0f && sim.Hero.Alive && animator.OneShot < 0)
            {
                float reach = heroRadius + enemy.Radius + 0.35f;
                float dx = view.Current.x - hero.x;
                float dz = view.Current.z - hero.z;
                if (dx * dx + dz * dz <= reach * reach)
                {
                    view.Claw = ClawCooldownSeconds;
                    animator.PlayOneShot(WalkerAttack, 1.3f, false);
                }
            }

            float speed = enemy.Velocity.Length;
            view.Walking = view.Walking ? speed > 0.12f : speed > 0.3f;
            if (enemy.StunRemaining > 0f || !view.Walking)
            {
                animator.SetBase(WalkerIdle, 1f);
            }
            else if (view.Runs || enemy.TypeIndex == 1 || enemy.Charging)
            {
                animator.SetBase(WalkerRun, Mathf.Clamp(speed / 4f, 0.6f, 1.4f));
            }
            else
            {
                SurvivorEnemyDef def = SurvivorDefaults.EnemyDef(enemy.TypeIndex);
                float top = def != null && def.MoveSpeed > 0f ? def.MoveSpeed : 2f;
                animator.SetBase(WalkerWalk, Mathf.Clamp(speed / top, 0.6f, 1.4f));
            }
        }

        /// <summary>Per-frame interpolation, tint and (culled, strided) animation of live enemies.</summary>
        private void PresentEnemies(float realDelta)
        {
            IReadOnlyList<SurvivorEnemy> enemies = sim.Enemies;
            int stride = visibleEnemies > 150 ? 3 : visibleEnemies > 70 ? 2 : 1;
            int visible = 0;
            float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 5f);
            int count = Mathf.Min(enemies.Count, slotViews.Length);
            for (int i = 0; i < count; i++)
            {
                EnemyView view = slotViews[i];
                if (view == null)
                {
                    continue;
                }
                SurvivorEnemy enemy = enemies[i];

                Vector3 position = Vector3.LerpUnclamped(view.Previous, view.Current, interpolationAlpha);
                float yaw = Mathf.LerpAngle(view.PreviousYaw, view.CurrentYaw, interpolationAlpha);
                view.Root.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                if (view.Aura.gameObject.activeSelf)
                {
                    float auraSize = enemy.Radius * (view.Golden ? 5f : 2.8f) * pulse;
                    view.Aura.localScale = new Vector3(auraSize, 1f, auraSize);
                    if (view.Golden)
                    {
                        view.Aura.localRotation = Quaternion.Euler(0f, (Time.unscaledTime * 70f) % 360f, 0f);
                    }
                }

                view.Flash = Mathf.Max(0f, view.Flash - realDelta);
                view.Chilled = enemy.SlowRemaining > 0f;
                int tint = view.Flash > 0f ? TintFlash
                    : enemy.WindingUp ? TintWindup
                    : enemy.StunRemaining > 0f ? TintStun
                    : enemy.SlowRemaining > 0f ? TintSlow
                    : view.Golden ? TintGolden
                    : view.Boss ? TintBoss
                    : view.Elite ? TintElite
                    : TintNone;
                ApplyEnemyTint(view, tint);
                if (view.Look == LookExploder)
                {
                    PresentExploderPulse(view, enemy.WindingUp && view.Flash <= 0f);
                }
                else if (view.Look == LookGhost)
                {
                    // Ghosts float a little above the ground and bob.
                    view.Body.localPosition = new Vector3(0f, 0.3f + 0.1f * Mathf.Sin(Time.unscaledTime * 3f + view.Phase), 0f);
                }

                bool onScreen = IsOnScreen(position, enemy.Radius, view.HeightScale);
                if (onScreen)
                {
                    visible++;
                    if (view.Golden)
                    {
                        view.Twinkle -= realDelta;
                        if (view.Twinkle <= 0f)
                        {
                            view.Twinkle = 0.1f;
                            effects.Sparkle(position + Vector3.up * 0.5f, GoldColor, 1, 0.6f, 1.6f, 0.26f);
                        }
                    }
                    if (enemy.Charging)
                    {
                        effects.Puff(position, new Color(0.75f, 0.6f, 0.5f, 0.5f), 1, 0.8f, 0.4f, 0.5f, 0.3f);
                    }
                    if (enemy.SlowRemaining > 0f)
                    {
                        PresentChill(position, view.HeightScale, realDelta);
                    }
                }
                TickEnemyAnimation(view, onScreen, stride);
            }
            visibleEnemies = visible;
        }

        private void PresentDying(float realDelta)
        {
            for (int i = dyingViews.Count - 1; i >= 0; i--)
            {
                EnemyView view = dyingViews[i];
                float step = sim.IsEnded ? realDelta : animationDelta;
                view.DeadSeconds += step;
                if (view.DeadSeconds >= ReleaseAfterSeconds)
                {
                    dyingViews.RemoveAt(i);
                    ReleaseEnemy(view);
                    continue;
                }

                ApplyEnemyTint(view, TintNone);
                if (view.DeadSeconds > SinkAfterSeconds)
                {
                    float sink = (view.DeadSeconds - SinkAfterSeconds) / (ReleaseAfterSeconds - SinkAfterSeconds);
                    view.Body.localPosition = new Vector3(0f, -1.2f * view.HeightScale * sink, 0f);
                    view.Shadow.localScale = Vector3.one * Mathf.Max(0.05f, 1f - sink) * (view.Boss ? 3.6f : 1.05f);
                }

                bool onScreen = IsOnScreen(view.Root.position, 0.6f, view.HeightScale);
                view.PendingAnimation += step;
                if (onScreen && view.Animator != null)
                {
                    view.Animator.Tick(Mathf.Min(view.PendingAnimation, 0.25f));
                    view.PendingAnimation = 0f;
                }
            }
        }

        private void TickEnemyAnimation(EnemyView view, bool onScreen, int stride)
        {
            view.PendingAnimation += animationDelta;
            if (view.Animator == null)
            {
                return;
            }
            if (!onScreen)
            {
                view.PendingAnimation = Mathf.Min(view.PendingAnimation, 0.25f);
                return;
            }
            if (stride > 1 && (frameCounter + view.Phase) % stride != 0)
            {
                return;
            }
            view.Animator.Tick(Mathf.Min(view.PendingAnimation, 0.25f));
            view.PendingAnimation = 0f;
        }

        private bool IsOnScreen(Vector3 position, float radius, float heightScale)
        {
            if (!hasFrustum)
            {
                return true;
            }
            float height = 2f * heightScale;
            Bounds bounds = new Bounds(position + Vector3.up * (height * 0.5f), new Vector3(radius * 2f + 0.6f, height, radius * 2f + 0.6f));
            return GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
        }

        private void ApplyEnemyTint(EnemyView view, int tint)
        {
            if (view.AppliedTint == tint)
            {
                return;
            }
            view.AppliedTint = tint;
            if (HasBaseTint(view.Look))
            {
                // M7 enemies keep their own colour at rest and their alpha (the ghost) under every tint.
                Color baseTint = BaseTint(view.Look);
                Color color = tint == TintNone ? baseTint : TintColors[tint];
                color.a = baseTint.a;
                SetRendererTint(view.Renderers, color, true);
                return;
            }
            SetRendererTint(view.Renderers, TintColors[tint], tint != TintNone);
        }

        /// <summary>An exploder about to blow up throbs: hot tint and a swelling body.</summary>
        private void PresentExploderPulse(EnemyView view, bool pulsing)
        {
            if (pulsing)
            {
                float beat = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 22f);
                SetRendererTint(view.Renderers, Color.Lerp(ExploderTint, new Color(2.8f, 1.4f, 0.6f, 1f), beat), true);
                view.AppliedTint = -1;
                view.Body.localScale = Vector3.one * (view.DisplayScale * (1f + 0.14f * beat));
                view.Pulsing = true;
            }
            else if (view.Pulsing)
            {
                view.Pulsing = false;
                view.Body.localScale = Vector3.one * view.DisplayScale;
            }
        }

        private void OnEnemyCharged(SurvivorEvent e)
        {
            Vector3 point = ArenaSpace.ToWorld(e.Point);
            effects.Shockwave(point, HurtColor, 2.4f, 0.4f);
            if (!StoreAt(StoreFx.DustPuff, point, 0.3f, 1.2f))
            {
                effects.Puff(point, new Color(0.75f, 0.6f, 0.5f, 0.6f), 6, 0.9f, 1f, 0.6f, 0.4f);
            }
        }

        private void OnEnemySplit(SurvivorEvent e)
        {
            Vector3 point = ArenaSpace.ToWorld(e.Point);
            effects.Shockwave(point, SplitterTint, 3f, 0.5f);
            if (!StoreArea(StoreFx.SwampBall, point + Vector3.up * 0.6f, 1.6f, 0.8f))
            {
                effects.Puff(point, new Color(0.45f, 0.9f, 0.4f, 0.6f), 8, 0.9f, 1.4f, 0.7f, 0.6f);
            }
        }

        private void OnEnemyHealed(SurvivorEvent e)
        {
            Vector3 point = ArenaSpace.ToWorld(e.Point);
            float radius = Mathf.Max(1f, e.Value);
            if (enemiesById.TryGetValue(e.Id, out EnemyView shaman))
            {
                shaman.Animator?.PlayOneShot(WalkerThrowState, 1.2f, false);
            }
            if (!StoreArea(StoreFx.HealCircle, point, radius, 1.6f))
            {
                effects.AreaFill(point, ShamanHealColor, radius, 0.7f);
            }
            effects.Shockwave(point, ShamanHealColor, radius * RingQuadPerRadius, 0.7f);
            effects.Sparkle(point, ShamanHealColor, 14, radius * 0.7f, 1.8f, 0.22f);
        }

        private void OnGoldenSpawned(SurvivorEvent e)
        {
            Vector3 point = ArenaSpace.ToWorld(e.Point);
            effects.Shockwave(point, GoldColor, 4f, 0.7f);
            if (!StoreAt(StoreFx.StarHit, point + Vector3.up * 1f, 2.4f, 1.4f))
            {
                effects.Sparkle(point, GoldColor, 16, 0.8f, 2.4f, 0.28f);
            }
            effects.Text(point + Vector3.up * 2.8f, "QUÁI VÀNG!", GoldColor, 1.3f, 1.4f);
        }

        private void OnGoldenKilled(SurvivorEvent e)
        {
            Vector3 point = ArenaSpace.ToWorld(e.Point);
            effects.Shockwave(point, GoldColor, 6f, 0.8f);
            effects.Flash(point + Vector3.up, GoldColor, 4f, 0.3f);
            if (!StoreArea(StoreFx.RainbowExplode, point + Vector3.up * 0.6f, 2.8f, 2f))
            {
                effects.Sparkle(point, GoldColor, 36, 1.5f, 3f, 0.32f);
            }
            effects.Text(point + Vector3.up * 2.8f, "VÀNG x5!", GoldColor, 1.4f, 1.4f);
        }

        /// <summary>-fxDemo: the three M11 enemy looks and a golden walker standing below the hero.</summary>
        private void PlayEnemyDemo(Vector3 center)
        {
            if (demoEnemies.Count > 0)
            {
                return;
            }
            int[] looks = { LookCharger, LookSplitter, LookShaman, LookWalker };
            for (int i = 0; i < looks.Length; i++)
            {
                EnemyView view = CreateEnemyView(looks[i]);
                view.Golden = i == looks.Length - 1;
                Vector3 point = center + new Vector3(2.6f + i * 2.2f, 0f, -3.6f);
                view.Root.SetPositionAndRotation(point, Quaternion.Euler(0f, 180f, 0f));
                view.Root.gameObject.SetActive(true);
                if (view.Animator != null)
                {
                    view.Animator.ResetTo(WalkerIdle);
                    view.Animator.Tick(0.1f);
                }
                view.AppliedTint = -1;
                ApplyEnemyTint(view, view.Golden ? TintGolden : TintNone);
                if (view.Golden)
                {
                    view.AuraRenderer.sharedMaterial = goldenAuraMaterial;
                    view.Aura.localScale = new Vector3(2.4f, 1f, 2.4f);
                    view.Aura.gameObject.SetActive(true);
                    effects.Sparkle(point + Vector3.up * 0.5f, GoldColor, 10, 0.7f, 1.6f, 0.26f);
                }
                demoEnemies.Add(view);
            }
        }
    }
}
