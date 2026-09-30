using System.Collections.Generic;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PersonalArena.View
{
    /// <summary>
    /// Interpolated presentation of an externally stepped <see cref="SurvivorSim"/>: a fenced graveyard,
    /// pooled skeleton hordes, EXP gems and loot, thrown hammers and event-driven combat effects.
    /// Every view is pooled and reused; nothing is instantiated per frame once the pools are warm.
    /// Game rules stay in Core; this class only reads the simulation state and its events.
    /// </summary>
    public sealed partial class SurvivorRenderer : MonoBehaviour
    {
        private const float PositionSmoothTime = 0.03f;
        private const float HeroYawSmoothTime = 0.07f;
        private const float SnapDistance = 3f;
        private const float AfterimageSpacing = 0.42f;
        private const int NumbersPerFrame = 8;
        private const int PuffsPerFrame = 10;
        private const int SparksPerFrame = 8;

        private const int HeroIdle = 0;
        private const int HeroRun = 1;
        private const int HeroBlock = 2;
        private const int HeroStrikeA = 3;
        private const int HeroStrikeB = 4;
        private const int HeroKick = 5;
        private const int HeroDash = 6;
        private const int HeroDeath = 8;
        private const int HeroThrow = 9;

        private static readonly bool[] HeroLoops = { true, true, true, false, false, false, false, false, false, false, false, false };

        private static readonly Color StrikeColor = new Color(0.78f, 0.9f, 1f, 0.95f);
        private static readonly Color KickColor = new Color(1f, 0.6f, 0.25f);
        private static readonly Color BlockColor = new Color(0.4f, 0.75f, 1f);
        private static readonly Color ParryColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color DashColor = new Color(0.35f, 0.8f, 1f, 0.55f);
        private static readonly Color HurtColor = new Color(1f, 0.3f, 0.25f);
        private static readonly Color HealColor = new Color(0.35f, 1f, 0.45f);
        private static readonly Color DustColor = new Color(0.55f, 0.5f, 0.42f, 0.55f);
        private static readonly Color BoneDustColor = new Color(0.86f, 0.83f, 0.72f, 0.6f);
        private static readonly Color DamageColor = new Color(1f, 0.96f, 0.85f);
        private static readonly Color CritColor = new Color(1f, 0.35f, 0.2f);
        private static readonly Color GoldColor = new Color(1f, 0.82f, 0.3f);
        private static readonly Color EliteColor = new Color(0.72f, 0.4f, 1f);
        private static readonly Color BossColor = new Color(1f, 0.25f, 0.2f);
        private static readonly Color LevelUpColor = new Color(1f, 0.88f, 0.4f);

        private static string[] numberCache;

        [SerializeField] private ArenaArtSet artSet;
        [SerializeField] private SurvivorArtSet survivorArt;

        private SurvivorSim sim;
        private ArenaEffects effects;
        private Camera viewCamera;
        private MaterialPropertyBlock propertyBlock;
        private Material shadowMaterial;
        private bool built;

        private Transform actorsRoot;
        private Transform heroRoot;
        private Transform heroBody;
        private Transform heroShadow;
        private Renderer[] heroRenderers;
        private CharacterAnimator heroAnimator;
        private ArenaEffects.ShieldGlow heroShieldGlow;
        private ArenaEffects.StunStars heroStars;
        private TrailRenderer heroTrail;
        private float heroBodyScale = 1f;
        private Material heroFallbackMaterial;

        private Vector3 heroPrevious;
        private Vector3 heroCurrent;
        private float heroPreviousYaw;
        private float heroCurrentYaw;
        private Vector3 heroDisplayPosition;
        private Vector3 heroPositionVelocity;
        private float heroDisplayYaw;
        private float heroYawVelocity;
        private bool heroSnap = true;
        private bool heroWasAlive = true;
        private bool heroRunning;
        private bool heroWasDashing;
        private bool heroFlashTinted;
        private float heroFlashRemaining;
        private Vector3 lastAfterimagePosition;
        private int strikeCount;

        private float interpolationAlpha = 1f;
        private float animationClock;
        private float animationDelta;
        private float lastSyncedTime;
        private int frameCounter;
        private int numbersLeft = NumbersPerFrame;
        private int puffsLeft = PuffsPerFrame;
        private int sparksLeft = SparksPerFrame;

        public SurvivorSim Sim => sim;

        /// <summary>Where the hero is drawn this frame (the follow camera aims here).</summary>
        public Vector3 HeroWorldPosition => heroDisplayPosition;

        /// <summary>Camera used for culling and facing labels; defaults to <see cref="Camera.main"/>.</summary>
        public void SetCamera(Camera camera)
        {
            viewCamera = camera;
        }

        /// <summary>Attaches a simulation (built once) and starts presenting its current run.</summary>
        public void Bind(SurvivorSim survivorSim)
        {
            sim = survivorSim;
            EnsureBuilt();
            ResetRun();
        }

        /// <summary>Call after <see cref="SurvivorSim.Reset"/>: drops every view of the old run and snaps to the new one.</summary>
        public void ResetRun()
        {
            EnsureBuilt();
            effects.Clear();
            ReleaseAllEnemies();
            HideAllPickups();
            HideAllHammers();
            RebuildObstacles();
            if (sim == null)
            {
                heroRoot.gameObject.SetActive(false);
                return;
            }

            heroRoot.gameObject.SetActive(true);
            heroCurrent = ArenaSpace.ToWorld(sim.Hero.Position);
            heroPrevious = heroCurrent;
            heroCurrentYaw = ArenaSpace.YawDegrees(sim.Hero.Facing);
            heroPreviousYaw = heroCurrentYaw;
            heroDisplayPosition = heroCurrent;
            heroDisplayYaw = heroCurrentYaw;
            heroSnap = true;
            heroWasAlive = sim.Hero.Alive;
            heroRunning = false;
            heroWasDashing = false;
            heroFlashRemaining = 0f;
            if (heroTrail != null)
            {
                heroTrail.emitting = false;
                heroTrail.Clear();
            }
            heroAnimator?.ResetTo(HeroIdle);

            interpolationAlpha = 1f;
            animationClock = sim.Time;
            lastSyncedTime = sim.Time;
            SyncEnemies();
            SyncPickups();
            SyncHammers();
            PresentFrame(0f);
        }

        /// <summary>Call once right after every <see cref="SurvivorSim.Step"/> so no event or tick is missed.</summary>
        public void SyncAfterStep()
        {
            if (sim == null || !built)
            {
                return;
            }

            heroPrevious = heroCurrent;
            heroPreviousYaw = heroCurrentYaw;
            heroCurrent = ArenaSpace.ToWorld(sim.Hero.Position);
            heroCurrentYaw = ArenaSpace.YawDegrees(sim.Hero.Facing);

            ReadEvents();
            SyncEnemies();
            SyncPickups();
            SyncHammers();
            UpdateHeroAnimationState();
            lastSyncedTime = sim.Time;
        }

        /// <summary>0..1 position of the displayed frame between the previous and the current tick.</summary>
        public void SetInterpolationAlpha(float alpha)
        {
            interpolationAlpha = Mathf.Clamp01(alpha);
        }

        private void Awake()
        {
            EnsureBuilt();
        }

        private void LateUpdate()
        {
            if (sim == null || !built)
            {
                return;
            }

            float realDelta = Time.unscaledDeltaTime;
            float displayed = sim.Time - (1f - interpolationAlpha) * SurvivorSim.FixedDeltaTime;
            float delta = displayed - animationClock;
            animationClock = displayed;
            if (sim.IsEnded)
            {
                // The run is over and the sim no longer steps; let death animations finish in real time.
                delta = realDelta;
            }
            animationDelta = Mathf.Clamp(delta, 0f, 0.25f);

            heroFlashRemaining = Mathf.Max(0f, heroFlashRemaining - realDelta);
            PresentFrame(realDelta);
            TopUpEnemyPools();

            frameCounter++;
            numbersLeft = NumbersPerFrame;
            puffsLeft = PuffsPerFrame;
            sparksLeft = SparksPerFrame;
        }

        private void PresentFrame(float realDelta)
        {
            Camera camera = ResolveCamera();
            if (camera != null)
            {
                GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
                hasFrustum = true;
            }
            else
            {
                hasFrustum = false;
            }

            PresentHero(realDelta);
            PresentEnemies(realDelta);
            PresentDying(realDelta);
            PresentPickups();
            PresentHammers();
        }

        private Camera ResolveCamera()
        {
            if (viewCamera == null)
            {
                viewCamera = Camera.main;
            }
            return viewCamera;
        }

        // ------------------------------------------------------------------ build

        private void EnsureBuilt()
        {
            if (built)
            {
                return;
            }
            built = true;

            propertyBlock = new MaterialPropertyBlock();
            effects = GetComponent<ArenaEffects>();
            if (effects == null)
            {
                effects = gameObject.AddComponent<ArenaEffects>();
            }

            shadowMaterial = FxAssets.Create("Blob Shadow", FxAssets.SoftDot, false);
            shadowMaterial.color = new Color(0f, 0f, 0f, 0.42f);
            shadowMaterial.renderQueue = 2990;

            actorsRoot = CreateChild("Actors", transform);
            BuildScenery();
            BuildHero();
            BuildEnemyPools();
            BuildPickups();
            BuildHammers();
        }

        private static Transform CreateChild(string childName, Transform parent)
        {
            GameObject child = new GameObject(childName);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private void BuildHero()
        {
            heroRoot = CreateChild("Hero", actorsRoot);
            heroBody = CreateChild("Body Visual", heroRoot);

            ArenaArtSet.HeroLook look = artSet != null && artSet.HasCharacters ? artSet.HeroFor("warrior") : default;
            if (look.Body != null)
            {
                heroBodyScale = artSet.CharacterScale * (look.Scale > 0f ? look.Scale : 1f);
                Animator animator = SpawnCharacter(look.Body, heroBody, look.MainHand, look.OffHand);
                heroAnimator = new CharacterAnimator(animator, new[]
                {
                    artSet.HeroIdle, artSet.HeroRun, artSet.HeroBlock, artSet.HeroStrikeA, artSet.HeroStrikeB,
                    artSet.HeroKick, artSet.HeroDash, artSet.HeroHit, artSet.HeroDeath,
                    artSet.HeroThrow, artSet.HeroCast, artSet.HeroDodgeBack
                }, HeroLoops, "Survivor Hero");
                heroAnimator.ResetTo(HeroIdle);
            }
            else
            {
                heroBodyScale = 1f;
                heroFallbackMaterial = CreateStandard("Hero Fallback", new Color(0.22f, 0.48f, 0.85f), 0.3f);
                GameObject body = CreatePrimitive("Hero Body", PrimitiveType.Capsule, heroBody, heroFallbackMaterial);
                body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                body.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            }
            heroBody.localScale = Vector3.one * heroBodyScale;
            heroRenderers = heroBody.GetComponentsInChildren<Renderer>(true);

            heroShadow = CreateBlobShadow(heroRoot, 1.3f);
            heroShieldGlow = effects.CreateShield(heroRoot);
            heroStars = effects.CreateStunStars(heroRoot, 1.85f);
            heroTrail = effects.CreateTrail(heroRoot, 0.75f, DashColor, 0.95f, 0.22f);

            // A warm torch glow around the hero keeps the knight readable in the moonlit graveyard.
            GameObject lightObject = new GameObject("Hero Light");
            lightObject.transform.SetParent(heroRoot, false);
            lightObject.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            Light heroLight = lightObject.AddComponent<Light>();
            heroLight.type = LightType.Point;
            heroLight.range = 10f;
            heroLight.intensity = 1.5f;
            heroLight.color = new Color(1f, 0.8f, 0.58f);
            heroLight.shadows = LightShadows.None;
            heroLight.renderMode = LightRenderMode.ForcePixel;
        }

        // ------------------------------------------------------------------ hero

        private void UpdateHeroAnimationState()
        {
            SurvivorHero hero = sim.Hero;
            if (heroAnimator == null)
            {
                heroWasAlive = hero.Alive;
                return;
            }

            if (!hero.Alive)
            {
                if (heroWasAlive)
                {
                    heroAnimator.PlayOneShot(HeroDeath, 1f, true);
                }
            }
            else
            {
                if (!heroWasAlive)
                {
                    heroAnimator.ResetTo(HeroIdle);
                }
                float speed = hero.Velocity.Length;
                heroRunning = heroRunning ? speed > 0.2f : speed > 0.45f;
                if (hero.Blocking)
                {
                    heroAnimator.SetBase(HeroBlock, 1f);
                }
                else if (heroRunning)
                {
                    float top = Mathf.Max(0.5f, sim.DerivedStats.MoveSpeed);
                    heroAnimator.SetBase(HeroRun, Mathf.Clamp(speed / top, 0.6f, 1.3f) * 1.15f);
                }
                else
                {
                    heroAnimator.SetBase(HeroIdle, 1f);
                }
            }
            heroWasAlive = hero.Alive;
        }

        private void PresentHero(float realDelta)
        {
            SurvivorHero hero = sim.Hero;
            Vector3 target = Vector3.Lerp(heroPrevious, heroCurrent, interpolationAlpha);
            float targetYaw = Mathf.LerpAngle(heroPreviousYaw, heroCurrentYaw, interpolationAlpha);
            bool dashing = hero.Dashing && hero.Alive;
            if (heroSnap || (heroDisplayPosition - target).sqrMagnitude > SnapDistance * SnapDistance || realDelta <= 0f)
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
                heroDisplayPosition = dashing
                    ? target
                    : Vector3.SmoothDamp(heroDisplayPosition, target, ref heroPositionVelocity, PositionSmoothTime, Mathf.Infinity, realDelta);
                heroDisplayYaw = Mathf.SmoothDampAngle(heroDisplayYaw, targetYaw, ref heroYawVelocity, HeroYawSmoothTime, Mathf.Infinity, realDelta);
            }

            heroRoot.SetPositionAndRotation(heroDisplayPosition, Quaternion.Euler(0f, heroDisplayYaw, 0f));
            heroShieldGlow?.SetBlocking(hero.Blocking && hero.Alive);
            heroStars?.SetVisible(hero.Alive && hero.StunRemaining > 0f);
            if (heroShadow != null)
            {
                heroShadow.gameObject.SetActive(hero.Alive);
            }

            bool flash = heroFlashRemaining > 0f;
            if (flash || heroFlashTinted)
            {
                SetRendererTint(heroRenderers, new Color(1.7f, 0.55f, 0.55f), flash);
                heroFlashTinted = flash;
            }

            if (heroTrail != null && heroTrail.emitting != dashing)
            {
                heroTrail.emitting = dashing;
            }
            if (dashing)
            {
                if (!heroWasDashing || (heroDisplayPosition - lastAfterimagePosition).sqrMagnitude >= AfterimageSpacing * AfterimageSpacing)
                {
                    lastAfterimagePosition = heroDisplayPosition;
                    effects.Afterimage(heroRenderers, DashColor, 0.36f);
                }
            }
            else if (heroWasDashing && hero.Alive)
            {
                effects.Puff(heroDisplayPosition, DustColor, 5, 0.6f, 1.2f, 0.6f, 0.3f);
            }
            heroWasDashing = dashing;

            heroAnimator?.Tick(animationDelta);
        }

        private void PlayHeroOneShot(int state, float speed, bool interruptOthers)
        {
            if (heroAnimator == null || !sim.Hero.Alive)
            {
                return;
            }
            if (!interruptOthers && heroAnimator.OneShot >= 0)
            {
                return;
            }
            heroAnimator.PlayOneShot(state, speed, false);
        }

        // ------------------------------------------------------------------ events

        private void ReadEvents()
        {
            IReadOnlyList<SurvivorEvent> events = sim.Events;
            Vector3 heroPosition = heroCurrent;
            for (int i = 0; i < events.Count; i++)
            {
                SurvivorEvent e = events[i];
                switch (e.Type)
                {
                    case SurvivorEventType.WeaponFired:
                        OnWeaponFired(e, heroPosition);
                        break;
                    case SurvivorEventType.DamageDealt:
                    {
                        bool crit = i > 0 && events[i - 1].Type == SurvivorEventType.Crit && events[i - 1].Id == e.Id;
                        OnDamageDealt(e, crit);
                        break;
                    }
                    case SurvivorEventType.EnemyKilled:
                        OnEnemyKilled(e);
                        break;
                    case SurvivorEventType.EliteKilled:
                    {
                        Vector3 point = ArenaSpace.ToWorld(e.Point);
                        effects.Shockwave(point, EliteColor, 3.2f, 0.6f);
                        effects.Sparkle(point, EliteColor, 18, 0.8f, 2.2f, 0.26f);
                        break;
                    }
                    case SurvivorEventType.BossKilled:
                    {
                        Vector3 point = ArenaSpace.ToWorld(e.Point);
                        effects.Shockwave(point, GoldColor, 9f, 1.1f);
                        effects.Flash(point + Vector3.up, Color.white, 6f, 0.4f);
                        effects.Sparkle(point, GoldColor, 40, 2f, 3f, 0.35f);
                        effects.Text(point + Vector3.up * 3.5f, "TRÙM ĐÃ GỤC!", GoldColor, 2f, 2f);
                        break;
                    }
                    case SurvivorEventType.EliteSpawned:
                    {
                        Vector3 point = ArenaSpace.ToWorld(e.Point);
                        effects.Shockwave(point, EliteColor, 3f, 0.7f);
                        effects.Puff(point, new Color(0.45f, 0.3f, 0.6f, 0.6f), 10, 1f, 1.8f, 0.9f, 0.8f);
                        break;
                    }
                    case SurvivorEventType.BossSpawned:
                    {
                        Vector3 point = ArenaSpace.ToWorld(e.Point);
                        effects.Shockwave(point, BossColor, 8f, 1.2f);
                        effects.Puff(point, new Color(0.35f, 0.1f, 0.12f, 0.7f), 24, 1.6f, 3.5f, 1.4f, 1f);
                        effects.Text(heroPosition + Vector3.up * 3f, "TRÙM XUẤT HIỆN!", BossColor, 1.8f, 2.2f);
                        break;
                    }
                    case SurvivorEventType.HeroDamaged:
                        heroFlashRemaining = 0.14f;
                        if (numbersLeft > 0 && e.Value >= 1f)
                        {
                            numbersLeft--;
                            effects.Text(heroPosition + Vector3.up * 2.1f, "-" + NumberText(Mathf.RoundToInt(e.Value)), HurtColor, 1.05f, 0.8f);
                        }
                        break;
                    case SurvivorEventType.HeroDied:
                        effects.Puff(heroPosition, new Color(0.3f, 0.22f, 0.3f, 0.7f), 16, 1.2f, 2f, 1.2f, 0.6f);
                        break;
                    case SurvivorEventType.Blocked:
                        heroShieldGlow?.Flash(BlockColor);
                        effects.Sparks(heroPosition + Vector3.up * 0.9f + heroRoot.forward * 0.6f, heroRoot.forward, BlockColor, 6, 5f, 0.9f);
                        break;
                    case SurvivorEventType.Parry:
                        heroShieldGlow?.Flash(ParryColor);
                        effects.Flash(heroPosition + Vector3.up * 0.9f + heroRoot.forward * 0.6f, ParryColor, 2f, 0.25f);
                        effects.Text(heroPosition + Vector3.up * 2.4f, "ĐỠ ĐÒN!", ParryColor, 1.2f, 0.9f);
                        break;
                    case SurvivorEventType.SkillUsed:
                        OnSkillUsed(e, heroPosition);
                        break;
                    case SurvivorEventType.Healed:
                        effects.Sparkle(heroPosition, HealColor, 14, 0.7f, 2f, 0.24f);
                        effects.Text(heroPosition + Vector3.up * 2.2f, "+" + NumberText(Mathf.RoundToInt(e.Value)), HealColor, 1.1f, 1f);
                        break;
                    case SurvivorEventType.LevelUp:
                        effects.Shockwave(heroPosition, LevelUpColor, 4.5f, 0.7f);
                        effects.Sparkle(heroPosition, LevelUpColor, 22, 0.9f, 2.6f, 0.28f);
                        effects.Text(heroPosition + Vector3.up * 2.6f, "LÊN CẤP!", LevelUpColor, 1.4f, 1.3f);
                        break;
                    case SurvivorEventType.ItemPicked:
                    {
                        Color color = SurvivorViewLogic.ItemColor(e.Id);
                        effects.Shockwave(heroPosition, color, 3f, 0.5f);
                        effects.Sparkle(heroPosition, color, 16, 0.7f, 2.4f, 0.25f);
                        break;
                    }
                    case SurvivorEventType.GoldCollected:
                        if (e.Id < 0 && e.Point.X * e.Point.X + e.Point.Y * e.Point.Y > 0f && sparksLeft > 0)
                        {
                            sparksLeft--;
                            effects.Sparkle(ArenaSpace.ToWorld(e.Point, 0.3f), GoldColor, 5, 0.3f, 1.2f, 0.18f);
                        }
                        break;
                }
            }
        }

        private void OnWeaponFired(SurvivorEvent e, Vector3 heroPosition)
        {
            Vector3 direction = new Vector3(e.Point.X, 0f, e.Point.Y);
            if (direction.sqrMagnitude < 1e-6f)
            {
                direction = heroRoot.forward;
            }
            direction.Normalize();
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

            if (e.Id == 0)
            {
                int level = sim.Inventory.Level(0);
                float reach = SurvivorViewLogic.SweepRange(level, sim.DerivedStats.AreaMul) / 1.25f;
                bool mirror = (strikeCount & 1) == 1;
                PlayHeroOneShot(mirror ? HeroStrikeB : HeroStrikeA, 1.7f, false);
                strikeCount++;
                Vector3 origin = heroPosition + Vector3.up * 0.85f;
                effects.Slash(origin, yaw, StrikeColor, mirror, reach, 0.22f);
                if (SurvivorViewLogic.SweepHitsBehind(level))
                {
                    effects.Slash(origin, yaw + 180f, StrikeColor, !mirror, reach, 0.22f);
                }
            }
            else if (e.Id == 3)
            {
                PlayHeroOneShot(HeroThrow, 1.9f, false);
            }
        }

        private void OnSkillUsed(SurvivorEvent e, Vector3 heroPosition)
        {
            int slot = (int)e.Value;
            SkillDef[] skills = sim.Config.ClassDef.ActiveSkills;
            SkillKind kind = slot >= 0 && slot < skills.Length && skills[slot] != null ? skills[slot].Kind : SkillKind.None;
            Vector3 forward = heroRoot.forward;
            switch (kind)
            {
                case SkillKind.Kick:
                    PlayHeroOneShot(HeroKick, 1.5f, true);
                    effects.Shockwave(heroPosition + forward * 1f, KickColor, 2.6f, 0.35f);
                    effects.Sparks(heroPosition + Vector3.up * 0.6f + forward * 0.9f, forward, KickColor, 10, 7f, 0.9f);
                    break;
                case SkillKind.Dash:
                    PlayHeroOneShot(HeroDash, 1.6f, true);
                    effects.Puff(heroPosition, DustColor, 6, 0.7f, 1.2f, 0.6f, 0.3f);
                    break;
                case SkillKind.Block:
                    heroShieldGlow?.Flash(BlockColor);
                    break;
            }
        }

        private void OnDamageDealt(SurvivorEvent e, bool crit)
        {
            if (enemiesById.TryGetValue(e.Id, out EnemyView view))
            {
                view.Flash = 0.09f;
            }
            if (numbersLeft <= 0)
            {
                return;
            }
            Vector3 point = ArenaSpace.ToWorld(e.Point);
            if ((point - heroCurrent).sqrMagnitude > 900f)
            {
                return;
            }
            numbersLeft--;
            float height = view != null ? 1.5f * view.HeightScale : 1.6f;
            effects.Text(point + Vector3.up * height, NumberText(Mathf.Max(1, Mathf.RoundToInt(e.Value))),
                crit ? CritColor : DamageColor, crit ? 1.3f : 0.9f, crit ? 0.9f : 0.7f);
            if (crit && sparksLeft > 0)
            {
                sparksLeft--;
                effects.Sparks(point + Vector3.up * 0.9f, Vector3.up, CritColor, 6, 5f, 1f);
            }
        }

        private void OnEnemyKilled(SurvivorEvent e)
        {
            Vector3 point = ArenaSpace.ToWorld(e.Point);
            if (enemiesById.TryGetValue(e.Id, out EnemyView view))
            {
                StartDying(view);
            }
            if (puffsLeft > 0 && (point - heroCurrent).sqrMagnitude < 900f)
            {
                puffsLeft--;
                effects.Puff(point, BoneDustColor, 5, 0.7f, 1.2f, 0.7f, 0.5f);
            }
        }

        private static string NumberText(int value)
        {
            if (numberCache == null)
            {
                numberCache = new string[2048];
            }
            if (value < 0 || value >= numberCache.Length)
            {
                return value.ToString();
            }
            return numberCache[value] ?? (numberCache[value] = value.ToString());
        }

        // ------------------------------------------------------------------ helpers

        private Transform CreateBlobShadow(Transform parent, float size)
        {
            GameObject shadowObject = new GameObject("Blob Shadow");
            shadowObject.transform.SetParent(parent, false);
            shadowObject.transform.localPosition = new Vector3(0f, 0.025f, 0f);
            shadowObject.transform.localScale = new Vector3(size, 1f, size);
            shadowObject.AddComponent<MeshFilter>().sharedMesh = FxAssets.FlatQuad;
            MeshRenderer renderer = shadowObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = shadowMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return shadowObject.transform;
        }

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

        private static Material CreateStandard(string materialName, Color color, float gloss)
        {
            Shader shader = Shader.Find("Standard");
            Material material = new Material(shader) { name = materialName, color = color, enableInstancing = true };
            material.SetFloat("_Glossiness", gloss);
            return material;
        }

        private static Material CreateEmissive(string materialName, Color color, Color emission, float gloss, float metallic)
        {
            Material material = CreateStandard(materialName, color, gloss);
            material.SetFloat("_Metallic", metallic);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return material;
        }

        private static GameObject CreatePrimitive(string objectName, PrimitiveType type, Transform parent, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);
            Collider primitiveCollider = primitive.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                DestroyUnityObject(primitiveCollider);
            }
            Renderer primitiveRenderer = primitive.GetComponent<Renderer>();
            if (primitiveRenderer != null && material != null)
            {
                primitiveRenderer.sharedMaterial = material;
            }
            return primitive;
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
                if (renderers[i] != null)
                {
                    renderers[i].SetPropertyBlock(overrideTint ? propertyBlock : null);
                }
            }
        }

        private static void DestroyUnityObject(Object target)
        {
            if (target == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void OnDestroy()
        {
            heroAnimator?.Dispose();
            DisposeEnemyAnimators();
            DestroyUnityObject(shadowMaterial);
            DestroyUnityObject(heroFallbackMaterial);
            DestroyOwnedMaterials();
        }
    }
}
