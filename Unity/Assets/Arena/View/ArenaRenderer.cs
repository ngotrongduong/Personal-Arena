using System.Collections.Generic;
using PersonalArena.Core;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// Interpolated presentation of an externally stepped ArenaSim: the floating round stage, smoothed actors,
    /// falls into the abyss, health potions and event-driven combat effects. Uses the optional
    /// <see cref="ArenaArtSet"/> (rigged characters, animations) and falls back to procedural primitives without one.
    /// </summary>
    public sealed class ArenaRenderer : MonoBehaviour
    {
        private const float HeroHeight = 0.72f;
        private const float ZombieHeight = 0.68f;
        private const float CorpseSeconds = 2.6f;
        private const float FallGravity = 22f;
        private const float FallSeconds = 2.2f;
        private const float PositionSmoothTime = 0.03f;
        private const float HeroYawSmoothTime = 0.07f;
        private const float ZombieYawSmoothTime = 0.09f;
        private const float SnapDistance = 3f;
        private const float AfterimageSpacing = 0.42f;

        private const int HeroIdle = 0;
        private const int HeroRun = 1;
        private const int HeroBlock = 2;
        private const int HeroStrikeA = 3;
        private const int HeroStrikeB = 4;
        private const int HeroKick = 5;
        private const int HeroDash = 6;
        private const int HeroHit = 7;
        private const int HeroDeath = 8;

        private const int WalkerIdle = 0;
        private const int WalkerWalk = 1;
        private const int WalkerAttack = 2;
        private const int WalkerHit = 3;
        private const int WalkerDeath = 4;
        private const int WalkerSpawn = 5;

        private static readonly bool[] HeroLoops = { true, true, true, false, false, false, false, false, false };
        private static readonly bool[] WalkerLoops = { true, true, false, false, false, false };

        private static readonly Color StrikeColor = new Color(0.78f, 0.9f, 1f, 0.95f);
        private static readonly Color ImpactColor = new Color(1f, 0.72f, 0.3f);
        private static readonly Color KickColor = new Color(1f, 0.6f, 0.25f);
        private static readonly Color BlockColor = new Color(0.4f, 0.75f, 1f);
        private static readonly Color ParryColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color DashColor = new Color(0.35f, 0.8f, 1f, 0.55f);
        private static readonly Color HurtColor = new Color(1f, 0.25f, 0.2f);
        private static readonly Color HealColor = new Color(0.35f, 1f, 0.45f);
        private static readonly Color DustColor = new Color(0.55f, 0.48f, 0.42f, 0.55f);
        private static readonly Color DeathSmokeColor = new Color(0.28f, 0.22f, 0.32f, 0.75f);
        private static readonly Color SummonColor = new Color(0.65f, 0.35f, 1f);
        private static readonly Color PotionColor = new Color(1f, 0.25f, 0.32f);

        [SerializeField] private ArenaArtSet artSet;

        private readonly List<ZombieView> zombieViews = new List<ZombieView>();
        private readonly List<PotionView> potionViews = new List<PotionView>();
        private MaterialPropertyBlock propertyBlock;

        private ArenaSim sim;
        private ArenaEffects effects;
        private ArenaStage stage;
        private Transform environmentRoot;
        private Transform actorsRoot;
        private Transform heroRoot;
        private Transform heroBody;
        private Transform shield;
        private Transform heroShadow;
        private Renderer[] heroRenderers;
        private CharacterAnimator heroAnimator;
        private ArenaEffects.ShieldGlow heroShieldGlow;
        private ArenaEffects.StunStars heroStars;
        private TrailRenderer heroTrail;
        private float heroBodyScale = 1f;
        private Material heroMaterial;
        private Material spearMaterial;
        private Material shieldMaterial;
        private Material zombieMaterial;
        private Material zombieHeadMaterial;
        private Material facingMaterial;
        private Material shadowMaterial;
        private Material potionGlassMaterial;
        private Material potionNeckMaterial;
        private Material potionCorkMaterial;

        private ActorSnapshot previousHero;
        private ActorSnapshot currentHero;
        private ActorSnapshot[] previousZombies;
        private ActorSnapshot[] currentZombies;
        private float interpolationAlpha = 1f;
        private float lastSyncedTime;
        private float heroFlashRemaining;
        private float heroPopRemaining;
        private bool heroWasAlive;
        private bool heroRunning;
        private int strikeCount;
        private float animationClock;
        private float frameDelta;

        private Vector3 heroDisplayPosition;
        private Vector3 heroPositionVelocity;
        private float heroDisplayYaw;
        private float heroYawVelocity;
        private bool heroSnap = true;
        private bool heroFalling;
        private float heroFallTime;
        private Vector3 heroFallOrigin;
        private Vector3 heroFallVelocity;
        private Vector3 lastAfterimagePosition;
        private bool heroWasDashing;

        public ArenaSim Sim => sim;

        private bool UsesCharacterArt => artSet != null && artSet.HasCharacters;

        public void Bind(ArenaSim arenaSim)
        {
            sim = arenaSim;
            EnsureMaterials();
            EnsureEffects();
            BuildEnvironment();
            EnsureHeroView();

            int count = sim != null ? sim.Zombies.Count : 0;
            EnsureZombieViews(count);
            EnsurePotionViews(sim != null ? sim.Potions.Count : 0);
            effects.Clear();
            heroFlashRemaining = 0f;
            heroPopRemaining = 0f;
            heroFalling = false;
            heroSnap = true;
            heroRunning = false;
            heroWasDashing = false;
            if (heroTrail != null)
            {
                heroTrail.emitting = false;
                heroTrail.Clear();
            }
            for (int i = 0; i < zombieViews.Count; i++)
            {
                ZombieView view = zombieViews[i];
                view.FlashRemaining = 0f;
                view.PopRemaining = 0f;
                view.Falling = false;
                view.Snap = true;
                view.Walking = false;
            }
            for (int i = 0; i < potionViews.Count; i++)
            {
                potionViews[i].Root.gameObject.SetActive(false);
                potionViews[i].ShownId = -1;
            }
            previousZombies = new ActorSnapshot[count];
            currentZombies = new ActorSnapshot[count];
            if (sim == null)
            {
                if (heroRoot != null)
                {
                    heroRoot.gameObject.SetActive(false);
                }
                return;
            }

            heroRoot.gameObject.SetActive(true);
            currentHero = Snapshot(sim.Hero.Position, sim.Hero.Facing, sim.Hero.Alive);
            previousHero = currentHero;
            heroWasAlive = sim.Hero.Alive;
            heroAnimator?.ResetTo(HeroIdle);
            for (int i = 0; i < count; i++)
            {
                ZombieState zombie = sim.Zombies[i];
                currentZombies[i] = Snapshot(zombie.Position, zombie.Facing, zombie.Alive);
                previousZombies[i] = currentZombies[i];
                ZombieView view = zombieViews[i];
                view.WasAlive = zombie.Alive;
                view.WasStunned = false;
                view.LastPhase = zombie.AttackPhase;
                view.DeadSeconds = zombie.Alive ? 0f : CorpseSeconds;
                view.Animator?.ResetTo(WalkerIdle);
            }

            interpolationAlpha = 1f;
            lastSyncedTime = sim.Time;
            animationClock = sim.Time;
            UpdateAnimationStates();
            Present();
        }

        /// <summary>Call once immediately after the bound simulation takes a step.</summary>
        public void SyncAfterStep()
        {
            if (sim == null || currentZombies == null || currentZombies.Length != sim.Zombies.Count)
            {
                Bind(sim);
                return;
            }

            previousHero = currentHero;
            currentHero = Snapshot(sim.Hero.Position, sim.Hero.Facing, sim.Hero.Alive);
            for (int i = 0; i < sim.Zombies.Count; i++)
            {
                ZombieState zombie = sim.Zombies[i];
                ActorSnapshot oldCurrent = currentZombies[i];
                previousZombies[i] = oldCurrent;
                currentZombies[i] = Snapshot(zombie.Position, zombie.Facing, zombie.Alive);
                if (!oldCurrent.Alive && zombie.Alive)
                {
                    previousZombies[i] = currentZombies[i];
                    ZombieView view = zombieViews[i];
                    view.Snap = true;
                    view.Falling = false;
                    Vector3 feet = currentZombies[i].Position;
                    effects.Shockwave(feet, SummonColor, 2.2f, 0.6f);
                    effects.Puff(feet, new Color(0.45f, 0.3f, 0.6f, 0.6f), 10, 0.9f, 1.8f, 0.9f, 0.8f);
                    effects.Sparkle(feet, SummonColor, 10, 0.5f, 2f, 0.2f);
                }
            }

            ReadFeedbackEvents();
            UpdateAnimationStates();
            lastSyncedTime = sim.Time;
        }

        public void SetInterpolationAlpha(float alpha)
        {
            interpolationAlpha = Mathf.Clamp01(alpha);
        }

        private void LateUpdate()
        {
            if (sim != null && !Mathf.Approximately(sim.Time, lastSyncedTime))
            {
                SyncAfterStep();
            }
            float delta = Time.unscaledDeltaTime;
            heroFlashRemaining = Mathf.Max(0f, heroFlashRemaining - delta);
            heroPopRemaining = Mathf.Max(0f, heroPopRemaining - delta);
            for (int i = 0; i < zombieViews.Count; i++)
            {
                ZombieView view = zombieViews[i];
                view.FlashRemaining = Mathf.Max(0f, view.FlashRemaining - delta);
                view.PopRemaining = Mathf.Max(0f, view.PopRemaining - delta);
            }

            TickAnimation();
            Present();
        }

        private void Present()
        {
            if (sim == null || heroRoot == null || currentZombies == null)
            {
                return;
            }

            float delta = Time.unscaledDeltaTime;
            PresentHero(delta);

            int count = sim.Zombies.Count;
            for (int i = 0; i < zombieViews.Count; i++)
            {
                if (i >= count)
                {
                    zombieViews[i].Root.gameObject.SetActive(false);
                    continue;
                }
                PresentZombie(zombieViews[i], sim.Zombies[i], previousZombies[i], currentZombies[i], delta);
            }

            PresentPotions();
        }

        private void PresentHero(float delta)
        {
            HeroState hero = sim.Hero;
            Vector3 target = Vector3.Lerp(previousHero.Position, currentHero.Position, interpolationAlpha);
            float targetYaw = Mathf.LerpAngle(previousHero.Yaw, currentHero.Yaw, interpolationAlpha);
            bool dashing = hero.DashRemaining > 0f && hero.Alive;
            if (heroSnap || (heroDisplayPosition - target).sqrMagnitude > SnapDistance * SnapDistance)
            {
                heroDisplayPosition = target;
                heroDisplayYaw = targetYaw;
                heroPositionVelocity = Vector3.zero;
                heroYawVelocity = 0f;
                lastAfterimagePosition = target;
                heroSnap = false;
            }
            else
            {
                // Dashes stay crisp; ordinary movement is lightly smoothed to hide collision micro-jitter.
                heroDisplayPosition = dashing
                    ? target
                    : Vector3.SmoothDamp(heroDisplayPosition, target, ref heroPositionVelocity, PositionSmoothTime, Mathf.Infinity, delta);
                heroDisplayYaw = Mathf.SmoothDampAngle(heroDisplayYaw, targetYaw, ref heroYawVelocity, HeroYawSmoothTime, Mathf.Infinity, delta);
            }

            Vector3 position = heroDisplayPosition;
            Quaternion rotation = Quaternion.Euler(0f, heroDisplayYaw, 0f);
            bool visible = currentHero.Alive || heroAnimator != null;
            float shadowScale = 1f;
            if (heroFalling)
            {
                heroFallTime += frameDelta;
                position = FallPosition(heroFallOrigin, heroFallVelocity, heroFallTime);
                rotation *= Quaternion.Euler(heroFallTime * 140f, 0f, heroFallTime * 60f);
                visible = heroFallTime < FallSeconds;
                shadowScale = 0f;
            }
            heroRoot.SetPositionAndRotation(position, rotation);
            // Rigged heroes stay visible to play their death animation.
            heroRoot.gameObject.SetActive(visible);

            if (shield != null)
            {
                shield.gameObject.SetActive(hero.IsBlocking && currentHero.Alive);
            }
            heroShieldGlow?.SetBlocking(hero.IsBlocking && currentHero.Alive);
            heroStars?.SetVisible(hero.Alive && hero.StunRemaining > 0f);
            if (heroShadow != null)
            {
                heroShadow.gameObject.SetActive(shadowScale > 0f && currentHero.Alive);
            }

            float heroPop = heroPopRemaining > 0f ? 1f + 0.18f * (heroPopRemaining / 0.2f) : 1f;
            heroBody.localScale = Vector3.one * (heroPop * heroBodyScale);
            Color heroFlash = heroAnimator != null ? new Color(1.6f, 0.55f, 0.55f) : new Color(1f, 0.2f, 0.2f);
            SetRendererTint(heroRenderers, heroFlash, heroFlashRemaining > 0f);

            // Dash: a glowing ribbon plus frozen afterimages spaced along the path, so it reads as speed, not a teleport.
            if (heroTrail != null && heroTrail.emitting != dashing)
            {
                heroTrail.emitting = dashing;
            }
            if (dashing)
            {
                if (!heroWasDashing)
                {
                    lastAfterimagePosition = position;
                    effects.Afterimage(heroRenderers, DashColor, 0.36f);
                }
                else if ((position - lastAfterimagePosition).sqrMagnitude >= AfterimageSpacing * AfterimageSpacing)
                {
                    lastAfterimagePosition = position;
                    effects.Afterimage(heroRenderers, DashColor, 0.36f);
                }
            }
            else if (heroWasDashing && hero.Alive)
            {
                effects.Puff(position, DustColor, 5, 0.6f, 1.2f, 0.6f, 0.3f);
            }
            heroWasDashing = dashing;
        }

        private void PresentZombie(ZombieView view, ZombieState zombie, ActorSnapshot from, ActorSnapshot to, float delta)
        {
            view.Root.gameObject.SetActive(true);
            Vector3 target = Vector3.Lerp(from.Position, to.Position, interpolationAlpha);
            float targetYaw = Mathf.LerpAngle(from.Yaw, to.Yaw, interpolationAlpha);
            if (view.Snap || (view.DisplayPosition - target).sqrMagnitude > SnapDistance * SnapDistance)
            {
                view.DisplayPosition = target;
                view.DisplayYaw = targetYaw;
                view.PositionVelocity = Vector3.zero;
                view.YawVelocity = 0f;
                view.Snap = false;
            }
            else
            {
                view.DisplayPosition = Vector3.SmoothDamp(view.DisplayPosition, target, ref view.PositionVelocity, PositionSmoothTime, Mathf.Infinity, delta);
                view.DisplayYaw = Mathf.SmoothDampAngle(view.DisplayYaw, targetYaw, ref view.YawVelocity, ZombieYawSmoothTime, Mathf.Infinity, delta);
            }

            Vector3 position = view.DisplayPosition;
            Quaternion rotation = Quaternion.Euler(0f, view.DisplayYaw, 0f);
            bool rigged = view.Animator != null;
            bool showCorpse = rigged
                ? !zombie.Alive && view.DeadSeconds < CorpseSeconds
                : !zombie.Alive && view.PopRemaining > 0f;
            float sink = rigged && !zombie.Alive ? Mathf.Max(0f, view.DeadSeconds - 1.7f) * 0.9f : 0f;
            if (view.Falling)
            {
                view.FallTime += frameDelta;
                position = FallPosition(view.FallOrigin, view.FallVelocity, view.FallTime);
                rotation *= Quaternion.Euler(view.FallTime * 170f, 0f, view.FallTime * -80f);
                showCorpse = !zombie.Alive && view.FallTime < FallSeconds;
                sink = 0f;
            }
            view.Root.SetPositionAndRotation(position, rotation);
            view.BodyRoot.gameObject.SetActive(zombie.Alive || showCorpse);

            // Corpses sink into the floor before disappearing.
            view.BodyRoot.localPosition = new Vector3(0f, -sink, 0f);
            float pop = view.PopRemaining > 0f ? 1f + (rigged ? 0.12f : 0.28f) * (view.PopRemaining / 0.2f) : 1f;
            view.BodyRoot.localScale = Vector3.one * (pop * view.BodyScale);
            view.Shadow.gameObject.SetActive(zombie.Alive && !view.Falling);
            bool dizzy = zombie.Alive && zombie.StunRemaining > 0f;
            view.Stars.SetVisible(dizzy);

            // Stunned walkers sway in a slow drunken circle so the stun reads even without the stars.
            if (dizzy)
            {
                float sway = Time.time * 6f + view.DizzyPhase;
                float lean = 10f * Mathf.Clamp01(zombie.StunRemaining / 0.25f);
                view.BodyRoot.localRotation = Quaternion.Euler(Mathf.Sin(sway) * lean, 0f, Mathf.Cos(sway) * lean);
            }
            else
            {
                view.BodyRoot.localRotation = Quaternion.identity;
            }

            // Knocked-back walkers skid and kick up dust.
            if (zombie.Alive && zombie.KnockbackVelocity.Length > 1.5f)
            {
                view.DustTimer -= delta;
                if (view.DustTimer <= 0f)
                {
                    view.DustTimer = 0.04f;
                    effects.Puff(position, DustColor, 2, 0.55f, 0.5f, 0.55f, 0.35f);
                }
            }

            Color tint = rigged ? new Color(2.2f, 2.2f, 2.2f) : Color.white;
            bool overrideTint = true;
            if (view.FlashRemaining <= 0f)
            {
                if (zombie.StunRemaining > 0f)
                {
                    tint = rigged ? new Color(0.7f, 0.9f, 1.5f) : new Color(0.35f, 0.65f, 1f);
                }
                else if (zombie.AttackPhase == ZombieAttackPhase.Windup)
                {
                    // Textured models keep their detail with a warm wash instead of a flat color.
                    tint = rigged ? new Color(1.45f, 1.1f, 0.55f) : new Color(1f, 0.82f, 0.22f);
                }
                else
                {
                    overrideTint = false;
                }
            }
            SetRendererTint(view.Renderers, tint, overrideTint);
        }

        private void PresentPotions()
        {
            float time = Time.unscaledTime;
            IReadOnlyList<PotionState> potions = sim.Potions;
            for (int i = 0; i < potionViews.Count; i++)
            {
                PotionView view = potionViews[i];
                PotionState potion = i < potions.Count ? potions[i] : null;
                bool active = potion != null && potion.Active;
                if (!active)
                {
                    if (view.Root.gameObject.activeSelf)
                    {
                        view.Root.gameObject.SetActive(false);
                    }
                    view.ShownId = -1;
                    continue;
                }

                if (view.ShownId != potion.Id)
                {
                    view.ShownId = potion.Id;
                    view.Age = 0f;
                    view.SparkleTimer = 0f;
                }
                view.Age += Time.unscaledDeltaTime;
                Vector3 floor = ArenaSpace.ToWorld(potion.Position);
                float appear = Mathf.Clamp01(view.Age / 0.35f);
                float overshoot = 1f + 0.35f * Mathf.Sin(appear * Mathf.PI) * (1f - appear * 0.5f);
                float bob = 0.1f * Mathf.Sin(time * 2.6f + i);
                bool blinkHidden = potion.Remaining < 3f && Mathf.Repeat(time * 7f, 1f) < 0.35f;
                view.Root.gameObject.SetActive(true);
                view.Root.position = floor;
                view.Bottle.localPosition = new Vector3(0f, 0.12f + bob + (1f - appear) * 0.6f, 0f);
                view.Bottle.localRotation = Quaternion.Euler(0f, time * 90f + i * 40f, 8f * Mathf.Sin(time * 1.7f + i));
                view.Bottle.localScale = Vector3.one * (appear * overshoot);
                view.Bottle.gameObject.SetActive(!blinkHidden);
                float glow = 0.55f + 0.25f * Mathf.Sin(time * 4f + i);
                propertyBlock.Clear();
                propertyBlock.SetColor("_Color", new Color(PotionColor.r, PotionColor.g, PotionColor.b, glow * appear));
                view.Glow.SetPropertyBlock(propertyBlock);

                view.SparkleTimer -= Time.unscaledDeltaTime;
                if (view.SparkleTimer <= 0f)
                {
                    view.SparkleTimer = 0.22f;
                    effects.Sparkle(floor + Vector3.up * 0.25f, new Color(1f, 0.55f, 0.6f), 1, 0.3f, 0.8f, 0.16f);
                }
            }
        }

        private static Vector3 FallPosition(Vector3 origin, Vector3 velocity, float time)
        {
            // Horizontal speed eases off (drag) while gravity pulls the body down into the abyss.
            float drift = (1f - Mathf.Exp(-1.6f * time)) / 1.6f;
            return origin + velocity * drift + Vector3.down * (0.5f * FallGravity * time * time);
        }

        private Vector3 SnapshotVelocity(ActorSnapshot from, ActorSnapshot to)
        {
            Vector3 velocity = (to.Position - from.Position) / ArenaSim.FixedDeltaTime;
            velocity.y = 0f;
            if (velocity.sqrMagnitude < 4f)
            {
                // Barely moving when it went over: give it a small outward shove so it clearly tips off the rim.
                Vector3 outward = to.Position - stage.Center;
                outward.y = 0f;
                velocity = outward.sqrMagnitude > 1e-4f ? outward.normalized * 2.5f : Vector3.forward * 2.5f;
            }
            return Vector3.ClampMagnitude(velocity, 12f);
        }

        private void ReadFeedbackEvents()
        {
            bool blockedThisStep = false;
            HeroState hero = sim.Hero;
            Vector3 heroPosition = currentHero.Position;
            Vector3 heroForward = Quaternion.Euler(0f, currentHero.Yaw, 0f) * Vector3.forward;
            for (int i = 0; i < sim.Events.Count; i++)
            {
                SimEvent simEvent = sim.Events[i];
                switch (simEvent.Type)
                {
                    case SimEventType.HeroDamaged:
                    {
                        heroFlashRemaining = 0.14f;
                        if (heroAnimator != null && !hero.IsBlocking && heroAnimator.OneShot < 0)
                        {
                            heroAnimator.PlayOneShot(HeroHit, 1.4f, false);
                        }
                        Vector3 chest = heroPosition + Vector3.up * 0.9f;
                        int amount = Mathf.RoundToInt(simEvent.Value);
                        if (blockedThisStep)
                        {
                            if (amount > 0)
                            {
                                effects.Text(chest + Vector3.up * 0.7f, "-" + amount, new Color(0.75f, 0.8f, 0.9f), 0.8f, 0.8f);
                            }
                        }
                        else
                        {
                            Vector3 away = heroPosition - ZombieWorldPosition(simEvent.ZombieId, heroPosition - heroForward);
                            effects.Sparks(chest, away, HurtColor, 16, 5f, 0.9f);
                            effects.Flash(chest, HurtColor, 1.6f, 0.18f);
                            effects.Text(chest + Vector3.up * 0.8f, "-" + amount, HurtColor, 1.1f, 1f);
                        }
                        break;
                    }
                    case SimEventType.BlockedHit:
                    {
                        blockedThisStep = true;
                        Vector3 contact = heroPosition + heroForward * 0.65f + Vector3.up * 0.85f;
                        heroShieldGlow?.Flash(new Color(0.75f, 0.92f, 1f));
                        effects.Sparks(contact, heroForward, BlockColor, 18, 6f, 1f);
                        effects.Flash(contact, BlockColor, 1.4f, 0.16f);
                        effects.Shockwave(heroPosition, BlockColor, 2.4f, 0.35f);
                        break;
                    }
                    case SimEventType.Parry:
                    {
                        heroPopRemaining = 0.2f;
                        PopZombie(simEvent.ZombieId);
                        Vector3 contact = heroPosition + heroForward * 0.7f + Vector3.up * 0.9f;
                        heroShieldGlow?.Flash(ParryColor);
                        effects.Sparks(contact, heroForward, ParryColor, 30, 8f, 1.1f);
                        effects.Flash(contact, ParryColor, 2.6f, 0.25f);
                        effects.Shockwave(heroPosition, ParryColor, 4f, 0.5f);
                        effects.Text(contact + Vector3.up * 0.9f, "PARRY!", ParryColor, 1.4f, 1.2f);
                        break;
                    }
                    case SimEventType.Stagger:
                    {
                        Vector3 head = ZombieWorldPosition(simEvent.ZombieId, heroPosition) + Vector3.up * 2f;
                        effects.Text(head, "STAGGER", BlockColor, 0.95f, 1f);
                        break;
                    }
                    case SimEventType.HeroDealtDamage:
                    {
                        ZombieView view = FindZombieView(simEvent.ZombieId);
                        if (view != null)
                        {
                            view.FlashRemaining = 0.12f;
                            if (view.Animator != null && view.Animator.OneShot != WalkerAttack)
                            {
                                view.Animator.PlayOneShot(WalkerHit, 1.5f, false);
                            }
                        }
                        Vector3 target = ZombieWorldPosition(simEvent.ZombieId, heroPosition + heroForward);
                        Vector3 impact = target + Vector3.up * 0.9f;
                        effects.Sparks(impact, target - heroPosition, ImpactColor, 14, 6f, 0.8f);
                        effects.Flash(impact, ImpactColor, 1.2f, 0.14f);
                        int amount = Mathf.RoundToInt(simEvent.Value);
                        if (amount > 0)
                        {
                            effects.Text(impact + Vector3.up * 0.8f, amount.ToString(), new Color(1f, 0.93f, 0.6f), 1f, 0.9f);
                        }
                        break;
                    }
                    case SimEventType.Backstab:
                    {
                        PopZombie(simEvent.ZombieId);
                        Vector3 impact = ZombieWorldPosition(simEvent.ZombieId, heroPosition) + Vector3.up * 1f;
                        effects.Flash(impact, new Color(1f, 0.3f, 0.2f), 2.8f, 0.28f);
                        effects.Sparks(impact, heroForward, new Color(1f, 0.4f, 0.25f), 26, 8f, 1f);
                        effects.Text(impact + Vector3.up * 1.1f, "BACKSTAB!", new Color(1f, 0.45f, 0.3f), 1.3f, 1.2f);
                        break;
                    }
                    case SimEventType.Kick:
                    {
                        Vector3 target = ZombieWorldPosition(simEvent.ZombieId, heroPosition + heroForward);
                        Vector3 impact = Vector3.Lerp(heroPosition, target, 0.6f) + Vector3.up * 0.7f;
                        effects.Shockwave(target, KickColor, 3.2f, 0.45f);
                        effects.Flash(impact, KickColor, 1.8f, 0.18f);
                        effects.Sparks(impact, target - heroPosition, KickColor, 12, 7f, 0.6f);
                        effects.Puff(target, DustColor, 8, 0.8f, 2.4f, 0.8f, 0.4f);
                        break;
                    }
                    case SimEventType.ZombieKilled:
                    {
                        PopZombie(simEvent.ZombieId);
                        ZombieView view = FindZombieView(simEvent.ZombieId);
                        if (view != null && !view.Falling)
                        {
                            Vector3 body = ZombieWorldPosition(simEvent.ZombieId, heroPosition);
                            effects.Puff(body + Vector3.up * 0.4f, DeathSmokeColor, 14, 1.1f, 1.6f, 1.2f, 0.9f);
                            effects.Sparkle(body, SummonColor, 6, 0.4f, 1.6f, 0.18f);
                        }
                        break;
                    }
                    case SimEventType.ZombieFell:
                    {
                        int index = FindZombieIndex(simEvent.ZombieId);
                        if (index >= 0 && index < zombieViews.Count)
                        {
                            ZombieView view = zombieViews[index];
                            view.Falling = true;
                            view.FallTime = 0f;
                            view.FallOrigin = currentZombies[index].Position;
                            view.FallVelocity = SnapshotVelocity(previousZombies[index], currentZombies[index]);
                            effects.Puff(view.FallOrigin, DustColor, 6, 0.7f, 1.4f, 0.7f, 0.3f);
                            effects.Text(view.FallOrigin + Vector3.up * 1.8f, "RING OUT!", ParryColor, 1.2f, 1.3f);
                        }
                        break;
                    }
                    case SimEventType.HeroFell:
                    {
                        heroFalling = true;
                        heroFallTime = 0f;
                        heroFallOrigin = currentHero.Position;
                        heroFallVelocity = SnapshotVelocity(previousHero, currentHero);
                        effects.Puff(heroFallOrigin, DustColor, 8, 0.7f, 1.6f, 0.7f, 0.3f);
                        effects.Text(heroFallOrigin + Vector3.up * 1.8f, "FELL!", HurtColor, 1.3f, 1.4f);
                        break;
                    }
                    case SimEventType.PotionDropped:
                    {
                        PotionState potion = FindPotion(simEvent.ZombieId);
                        if (potion != null)
                        {
                            Vector3 floor = ArenaSpace.ToWorld(potion.Position);
                            effects.Sparkle(floor + Vector3.up * 0.3f, PotionColor, 12, 0.4f, 1.6f, 0.22f);
                            effects.Flash(floor + Vector3.up * 0.4f, PotionColor, 1.2f, 0.25f);
                        }
                        break;
                    }
                    case SimEventType.PotionPicked:
                    {
                        effects.Sparkle(heroPosition, HealColor, 22, 0.6f, 2.2f, 0.26f);
                        effects.Shockwave(heroPosition, HealColor, 2.6f, 0.5f);
                        effects.Flash(heroPosition + Vector3.up * 0.9f, HealColor, 1.8f, 0.3f);
                        effects.Text(heroPosition + Vector3.up * 2f, "+" + Mathf.RoundToInt(simEvent.Value), HealColor, 1.3f, 1.2f);
                        break;
                    }
                    case SimEventType.SkillUsed:
                        PlayHeroSkill((int)simEvent.Value - 1, heroPosition, heroForward);
                        break;
                }
            }
        }

        private void PlayHeroSkill(int slot, Vector3 heroPosition, Vector3 heroForward)
        {
            SkillDef[] skills = sim.HeroDef.Skills;
            if (slot < 0 || slot >= skills.Length || skills[slot] == null)
            {
                return;
            }

            switch (skills[slot].Kind)
            {
                case SkillKind.MeleeStrike:
                    strikeCount++;
                    heroAnimator?.PlayOneShot(strikeCount % 2 == 0 ? HeroStrikeA : HeroStrikeB, 1.9f, false);
                    effects.Slash(heroPosition + Vector3.up * 0.85f, currentHero.Yaw, StrikeColor, strikeCount % 2 == 0,
                        Mathf.Max(1f, skills[slot].Range * 0.9f), 0.2f);
                    break;
                case SkillKind.Kick:
                    heroAnimator?.PlayOneShot(HeroKick, 1.5f, false);
                    effects.Puff(heroPosition + heroForward * 0.4f, DustColor, 4, 0.5f, 1f, 0.5f, 0.25f);
                    break;
                case SkillKind.Dash:
                    heroAnimator?.PlayOneShot(HeroDash, 1.2f, false);
                    effects.Shockwave(heroPosition, DashColor, 2f, 0.35f);
                    effects.Puff(heroPosition, DustColor, 8, 0.7f, 1.8f, 0.6f, 0.3f);
                    break;
            }
        }

        /// <summary>Chooses looping states and fires state-change one-shots from the latest sim state.</summary>
        private void UpdateAnimationStates()
        {
            if (sim == null)
            {
                return;
            }

            if (heroAnimator != null)
            {
                HeroState hero = sim.Hero;
                if (!hero.Alive)
                {
                    if (heroWasAlive)
                    {
                        heroAnimator.PlayOneShot(hero.FellOff ? HeroHit : HeroDeath, hero.FellOff ? 0.8f : 1f, true);
                    }
                }
                else
                {
                    if (!heroWasAlive)
                    {
                        heroAnimator.ResetTo(HeroIdle);
                    }
                    // Hysteresis keeps the run cycle from flickering on and off near the threshold.
                    float speed = hero.Velocity.Length;
                    heroRunning = heroRunning ? speed > 0.2f : speed > 0.45f;
                    if (hero.IsBlocking)
                    {
                        heroAnimator.SetBase(HeroBlock, 1f);
                    }
                    else if (heroRunning)
                    {
                        heroAnimator.SetBase(HeroRun, Mathf.Clamp(speed / sim.HeroDef.MoveSpeed, 0.6f, 1.3f) * 1.15f);
                    }
                    else
                    {
                        heroAnimator.SetBase(HeroIdle, 1f);
                    }
                }
                heroWasAlive = hero.Alive;
            }

            for (int i = 0; i < sim.Zombies.Count && i < zombieViews.Count; i++)
            {
                ZombieView view = zombieViews[i];
                if (view.Animator == null)
                {
                    continue;
                }

                ZombieState zombie = sim.Zombies[i];
                if (!zombie.Alive)
                {
                    if (view.WasAlive)
                    {
                        view.Animator.PlayOneShot(view.Falling ? WalkerHit : WalkerDeath, view.Falling ? 0.8f : 1.5f, true);
                        view.DeadSeconds = 0f;
                    }
                }
                else
                {
                    if (!view.WasAlive)
                    {
                        view.Animator.ResetTo(WalkerIdle);
                        view.Animator.PlayOneShot(WalkerSpawn, 1.6f, false);
                    }
                    if (zombie.AttackPhase == ZombieAttackPhase.Windup && view.LastPhase != ZombieAttackPhase.Windup)
                    {
                        view.Animator.PlayOneShot(WalkerAttack, 1.1f, false);
                    }

                    bool stunned = zombie.StunRemaining > 0f;
                    if (stunned && !view.WasStunned)
                    {
                        view.Animator.PlayOneShot(WalkerHit, 0.8f, false);
                    }

                    float speed = zombie.Velocity.Length;
                    view.Walking = view.Walking ? speed > 0.12f : speed > 0.3f;
                    if (stunned)
                    {
                        view.Animator.SetBase(WalkerIdle, 0.35f);
                    }
                    else if (view.Walking)
                    {
                        view.Animator.SetBase(WalkerWalk, Mathf.Clamp(speed / Mathf.Max(0.1f, zombie.Def.MoveSpeed), 0.6f, 1.8f) * 1.2f);
                    }
                    else
                    {
                        view.Animator.SetBase(WalkerIdle, 1f);
                    }
                    view.WasStunned = stunned;
                }
                view.WasAlive = zombie.Alive;
                view.LastPhase = zombie.AttackPhase;
            }
        }

        /// <summary>Advances animations on the displayed sim clock so speed-up and pause carry over.</summary>
        private void TickAnimation()
        {
            if (sim == null)
            {
                frameDelta = 0f;
                return;
            }

            float displayed = sim.Time - (1f - interpolationAlpha) * ArenaSim.FixedDeltaTime;
            float delta = displayed - animationClock;
            animationClock = displayed;
            if (sim.Done)
            {
                // The sim stops at the end of an episode; keep death animations playing in real time.
                delta = Time.unscaledDeltaTime;
            }
            delta = Mathf.Clamp(delta, 0f, 0.25f);
            frameDelta = delta;

            heroAnimator?.Tick(delta);
            for (int i = 0; i < zombieViews.Count; i++)
            {
                ZombieView view = zombieViews[i];
                if (view.Animator == null)
                {
                    continue;
                }
                view.DeadSeconds += delta;
                view.Animator.Tick(delta);
            }
        }

        private void PopZombie(int zombieId)
        {
            ZombieView view = FindZombieView(zombieId);
            if (view != null)
            {
                view.PopRemaining = 0.2f;
            }
        }

        private int FindZombieIndex(int zombieId)
        {
            if (sim == null)
            {
                return -1;
            }

            for (int i = 0; i < sim.Zombies.Count; i++)
            {
                if (sim.Zombies[i].Id == zombieId)
                {
                    return i;
                }
            }
            return -1;
        }

        private ZombieView FindZombieView(int zombieId)
        {
            int index = FindZombieIndex(zombieId);
            return index >= 0 && index < zombieViews.Count ? zombieViews[index] : null;
        }

        private Vector3 ZombieWorldPosition(int zombieId, Vector3 fallback)
        {
            int index = FindZombieIndex(zombieId);
            return index >= 0 ? ArenaSpace.ToWorld(sim.Zombies[index].Position) : fallback;
        }

        private PotionState FindPotion(int potionId)
        {
            IReadOnlyList<PotionState> potions = sim.Potions;
            for (int i = 0; i < potions.Count; i++)
            {
                if (potions[i].Active && potions[i].Id == potionId)
                {
                    return potions[i];
                }
            }
            return null;
        }

        private void EnsureEffects()
        {
            if (effects == null)
            {
                effects = GetComponent<ArenaEffects>();
                if (effects == null)
                {
                    effects = gameObject.AddComponent<ArenaEffects>();
                }
            }
        }

        private void BuildEnvironment()
        {
            if (environmentRoot == null)
            {
                GameObject root = new GameObject("Environment");
                root.transform.SetParent(transform, false);
                environmentRoot = root.transform;
            }
            if (sim == null)
            {
                return;
            }

            Vector3 center = ArenaSpace.ToWorld(sim.Config.Center);
            float radius = sim.Config.Radius;
            if (stage != null && Mathf.Approximately(stage.Radius, radius) && (stage.Center - center).sqrMagnitude < 1e-4f)
            {
                return;
            }
            if (stage != null)
            {
                DestroyUnityObject(stage.gameObject);
            }
            stage = ArenaStage.Build(artSet, environmentRoot, center, radius);
        }

        private void EnsureHeroView()
        {
            if (actorsRoot == null)
            {
                GameObject root = new GameObject("Actors");
                root.transform.SetParent(transform, false);
                actorsRoot = root.transform;
            }
            if (heroRoot != null)
            {
                return;
            }

            GameObject rootObject = new GameObject("Hero");
            rootObject.transform.SetParent(actorsRoot, false);
            heroRoot = rootObject.transform;

            GameObject bodyRootObject = new GameObject("Body Visual");
            bodyRootObject.transform.SetParent(heroRoot, false);
            heroBody = bodyRootObject.transform;

            if (UsesCharacterArt)
            {
                heroBodyScale = artSet.CharacterScale;
                Animator animator = SpawnCharacter(artSet.Hero, heroBody, artSet.HeroMainHand, artSet.HeroOffHand);
                heroAnimator = new CharacterAnimator(animator, new[]
                {
                    artSet.HeroIdle, artSet.HeroRun, artSet.HeroBlock, artSet.HeroStrikeA, artSet.HeroStrikeB,
                    artSet.HeroKick, artSet.HeroDash, artSet.HeroHit, artSet.HeroDeath
                }, HeroLoops, "Hero");
                heroAnimator.ResetTo(HeroIdle);
            }
            else
            {
                GameObject body = CreatePrimitive("Warrior Body", PrimitiveType.Capsule, heroBody, heroMaterial);
                body.transform.localPosition = new Vector3(0f, HeroHeight, 0f);
                body.transform.localScale = new Vector3(0.9f, HeroHeight, 0.9f);

                GameObject spear = CreatePrimitive("Spear", PrimitiveType.Cube, heroBody, spearMaterial);
                spear.transform.localPosition = new Vector3(0.18f, 0.75f, 1.05f);
                spear.transform.localScale = new Vector3(0.1f, 0.1f, 1.45f);

                GameObject shieldObject = CreatePrimitive("Shield", PrimitiveType.Cube, heroBody, shieldMaterial);
                shieldObject.transform.localPosition = new Vector3(-0.45f, 0.72f, 0.55f);
                shieldObject.transform.localScale = new Vector3(0.65f, 0.9f, 0.12f);
                shield = shieldObject.transform;
            }
            heroRenderers = bodyRootObject.GetComponentsInChildren<Renderer>(true);

            heroShadow = CreateBlobShadow(heroRoot, 1.25f);
            heroShieldGlow = effects.CreateShield(heroRoot);
            heroStars = effects.CreateStunStars(heroRoot, 1.85f);
            heroTrail = effects.CreateTrail(heroRoot, 0.75f, DashColor, 0.95f, 0.22f);
        }

        private void EnsureZombieViews(int count)
        {
            if (actorsRoot == null)
            {
                EnsureHeroView();
            }

            while (zombieViews.Count < count)
            {
                int index = zombieViews.Count;
                GameObject rootObject = new GameObject("Zombie " + index);
                rootObject.transform.SetParent(actorsRoot, false);

                GameObject bodyRootObject = new GameObject("Body Visual");
                bodyRootObject.transform.SetParent(rootObject.transform, false);
                ZombieView view = new ZombieView
                {
                    Root = rootObject.transform,
                    BodyRoot = bodyRootObject.transform
                };

                if (UsesCharacterArt && artSet.Walkers.Length > 0 && artSet.Walkers[index % artSet.Walkers.Length].Body != null)
                {
                    ArenaArtSet.WalkerLook look = artSet.Walkers[index % artSet.Walkers.Length];
                    Animator animator = SpawnCharacter(look.Body, bodyRootObject.transform, look.MainHand, look.OffHand);
                    view.BodyScale = artSet.CharacterScale;
                    view.Animator = new CharacterAnimator(animator, new[]
                    {
                        artSet.WalkerIdle, artSet.WalkerWalk, artSet.WalkerAttack, artSet.WalkerHit,
                        artSet.WalkerDeath, artSet.WalkerSpawn
                    }, WalkerLoops, "Walker " + index);
                    view.Animator.ResetTo(WalkerIdle);
                }
                else
                {
                    GameObject body = CreatePrimitive("Body", PrimitiveType.Capsule, bodyRootObject.transform, zombieMaterial);
                    body.transform.localPosition = new Vector3(0f, ZombieHeight, 0f);
                    body.transform.localScale = new Vector3(0.86f, ZombieHeight, 0.86f);

                    GameObject head = CreatePrimitive("Head", PrimitiveType.Sphere, bodyRootObject.transform, zombieHeadMaterial);
                    head.transform.localPosition = new Vector3(0f, 1.45f, 0f);
                    head.transform.localScale = new Vector3(0.62f, 0.5f, 0.62f);

                    GameObject facing = CreatePrimitive("Facing", PrimitiveType.Cube, bodyRootObject.transform, facingMaterial);
                    facing.transform.localPosition = new Vector3(0f, 0.72f, 0.55f);
                    facing.transform.localScale = new Vector3(0.16f, 0.16f, 0.55f);
                }

                view.Renderers = bodyRootObject.GetComponentsInChildren<Renderer>(true);
                view.Shadow = CreateBlobShadow(rootObject.transform, 1.2f);
                view.Stars = effects.CreateStunStars(rootObject.transform, 1.8f);
                zombieViews.Add(view);
            }

            for (int i = count; i < zombieViews.Count; i++)
            {
                zombieViews[i].Root.gameObject.SetActive(false);
            }
        }

        private void EnsurePotionViews(int count)
        {
            while (potionViews.Count < count)
            {
                GameObject rootObject = new GameObject("Potion " + potionViews.Count);
                rootObject.transform.SetParent(actorsRoot, false);
                PotionView view = new PotionView { Root = rootObject.transform, ShownId = -1 };

                GameObject bottle = new GameObject("Bottle");
                bottle.transform.SetParent(rootObject.transform, false);
                view.Bottle = bottle.transform;

                GameObject flask = CreatePrimitive("Flask", PrimitiveType.Sphere, view.Bottle, potionGlassMaterial);
                flask.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                flask.transform.localScale = new Vector3(0.36f, 0.34f, 0.36f);
                GameObject neck = CreatePrimitive("Neck", PrimitiveType.Cylinder, view.Bottle, potionNeckMaterial);
                neck.transform.localPosition = new Vector3(0f, 0.42f, 0f);
                neck.transform.localScale = new Vector3(0.12f, 0.07f, 0.12f);
                GameObject cork = CreatePrimitive("Cork", PrimitiveType.Cylinder, view.Bottle, potionCorkMaterial);
                cork.transform.localPosition = new Vector3(0f, 0.52f, 0f);
                cork.transform.localScale = new Vector3(0.14f, 0.045f, 0.14f);
                foreach (Renderer part in bottle.GetComponentsInChildren<Renderer>(true))
                {
                    part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                GameObject glow = new GameObject("Glow");
                glow.transform.SetParent(rootObject.transform, false);
                glow.transform.localPosition = new Vector3(0f, 0.04f, 0f);
                glow.transform.localScale = new Vector3(1.4f, 1f, 1.4f);
                glow.AddComponent<MeshFilter>().sharedMesh = FxAssets.FlatQuad;
                view.Glow = glow.AddComponent<MeshRenderer>();
                view.Glow.sharedMaterial = FxAssets.GlowMaterial;
                view.Glow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                view.Glow.receiveShadows = false;

                rootObject.SetActive(false);
                potionViews.Add(view);
            }
        }

        private Transform CreateBlobShadow(Transform parent, float size)
        {
            GameObject shadowObject = new GameObject("Blob Shadow");
            shadowObject.transform.SetParent(parent, false);
            shadowObject.transform.localPosition = new Vector3(0f, 0.025f, 0f);
            shadowObject.transform.localScale = new Vector3(size, 1f, size);
            shadowObject.AddComponent<MeshFilter>().sharedMesh = FxAssets.FlatQuad;
            MeshRenderer renderer = shadowObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = shadowMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return shadowObject.transform;
        }

        /// <summary>Instantiates a rigged character, attaches hand props to its hand slots and returns its Animator.</summary>
        private static Animator SpawnCharacter(GameObject prefab, Transform parent, GameObject mainHand, GameObject offHand)
        {
            GameObject model = Instantiate(prefab, parent, false);
            model.name = prefab.name;
            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
            {
                animator = model.AddComponent<Animator>();
            }
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            AttachToSlot(model.transform, "handslot.r", mainHand);
            AttachToSlot(model.transform, "handslot.l", offHand);
            return animator;
        }

        private static void AttachToSlot(Transform model, string slotName, GameObject prop)
        {
            if (prop == null)
            {
                return;
            }
            Transform slot = FindDeep(model, slotName);
            if (slot == null)
            {
                Debug.LogWarning($"Hand slot '{slotName}' not found on {model.name}.");
                return;
            }
            GameObject instance = Instantiate(prop, slot, false);
            instance.name = prop.name;
        }

        private static Transform FindDeep(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }
                Transform found = FindDeep(child, childName);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private void EnsureMaterials()
        {
            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }
            if (heroMaterial != null)
            {
                return;
            }

            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("Personal Arena requires the built-in Standard shader.");
                return;
            }

            heroMaterial = CreateMaterial(shader, new Color(0.22f, 0.48f, 0.85f), "Warrior Blue");
            spearMaterial = CreateMaterial(shader, new Color(0.68f, 0.7f, 0.74f), "Spear Steel");
            shieldMaterial = CreateMaterial(shader, new Color(0.15f, 0.3f, 0.58f), "Shield Blue");
            zombieMaterial = CreateMaterial(shader, new Color(0.35f, 0.62f, 0.3f), "Walker Green");
            zombieHeadMaterial = CreateMaterial(shader, new Color(0.18f, 0.36f, 0.16f), "Walker Head");
            facingMaterial = CreateMaterial(shader, new Color(0.1f, 0.18f, 0.08f), "Walker Facing");

            potionGlassMaterial = CreateMaterial(shader, new Color(0.85f, 0.06f, 0.14f), "Potion Glass");
            potionGlassMaterial.SetFloat("_Glossiness", 0.93f);
            potionGlassMaterial.SetFloat("_Metallic", 0.15f);
            potionGlassMaterial.EnableKeyword("_EMISSION");
            potionGlassMaterial.SetColor("_EmissionColor", new Color(0.55f, 0.03f, 0.08f));
            potionGlassMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            potionNeckMaterial = CreateMaterial(shader, new Color(0.75f, 0.82f, 0.88f), "Potion Neck");
            potionNeckMaterial.SetFloat("_Glossiness", 0.9f);
            potionCorkMaterial = CreateMaterial(shader, new Color(0.45f, 0.29f, 0.15f), "Potion Cork");
            potionCorkMaterial.SetFloat("_Glossiness", 0.15f);

            shadowMaterial = FxAssets.Create("Blob Shadow", FxAssets.SoftDot, false);
            shadowMaterial.color = new Color(0f, 0f, 0f, 0.42f);
            shadowMaterial.renderQueue = 2990;
        }

        private static Material CreateMaterial(Shader shader, Color color, string materialName)
        {
            Material material = new Material(shader) { name = materialName };
            material.color = color;
            return material;
        }

        private static GameObject CreatePrimitive(string objectName, PrimitiveType type, Transform parent, Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = objectName;
            gameObject.transform.SetParent(parent, false);
            Collider primitiveCollider = gameObject.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                DestroyUnityObject(primitiveCollider);
            }
            Renderer primitiveRenderer = gameObject.GetComponent<Renderer>();
            if (primitiveRenderer != null && material != null)
            {
                primitiveRenderer.sharedMaterial = material;
            }
            return gameObject;
        }

        private void SetRendererTint(Renderer[] renderers, Color tint, bool overrideTint)
        {
            if (renderers == null)
            {
                return;
            }
            if (overrideTint)
            {
                propertyBlock.Clear();
                propertyBlock.SetColor("_Color", tint);
            }
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].SetPropertyBlock(overrideTint ? propertyBlock : null);
            }
        }

        private static ActorSnapshot Snapshot(Vec2 position, float facing, bool alive)
        {
            return new ActorSnapshot
            {
                Position = ArenaSpace.ToWorld(position),
                Yaw = ArenaSpace.YawDegrees(facing),
                Alive = alive
            };
        }

        private static void DestroyUnityObject(Object target)
        {
            if (target == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }

        private void OnDestroy()
        {
            heroAnimator?.Dispose();
            for (int i = 0; i < zombieViews.Count; i++)
            {
                zombieViews[i].Animator?.Dispose();
            }
            DestroyUnityObject(heroMaterial);
            DestroyUnityObject(spearMaterial);
            DestroyUnityObject(shieldMaterial);
            DestroyUnityObject(zombieMaterial);
            DestroyUnityObject(zombieHeadMaterial);
            DestroyUnityObject(facingMaterial);
            DestroyUnityObject(shadowMaterial);
            DestroyUnityObject(potionGlassMaterial);
            DestroyUnityObject(potionNeckMaterial);
            DestroyUnityObject(potionCorkMaterial);
        }

        private struct ActorSnapshot
        {
            public Vector3 Position;
            public float Yaw;
            public bool Alive;
        }

        private sealed class ZombieView
        {
            public Transform Root;
            public Transform BodyRoot;
            public Transform Shadow;
            public ArenaEffects.StunStars Stars;
            public Renderer[] Renderers;
            public CharacterAnimator Animator;
            public float BodyScale = 1f;
            public float FlashRemaining;
            public float PopRemaining;
            public float DeadSeconds;
            public bool WasAlive;
            public bool WasStunned;
            public bool Walking;
            public ZombieAttackPhase LastPhase;
            public Vector3 DisplayPosition;
            public Vector3 PositionVelocity;
            public float DisplayYaw;
            public float YawVelocity;
            public bool Snap = true;
            public bool Falling;
            public float FallTime;
            public Vector3 FallOrigin;
            public Vector3 FallVelocity;
            public float DustTimer;
            public float DizzyPhase = Random.Range(0f, 10f);
        }

        private sealed class PotionView
        {
            public Transform Root;
            public Transform Bottle;
            public MeshRenderer Glow;
            public int ShownId;
            public float Age;
            public float SparkleTimer;
        }
    }
}
