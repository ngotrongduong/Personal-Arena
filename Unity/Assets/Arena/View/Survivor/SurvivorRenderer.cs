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
        private const int HeroCast = 10;
        private const int HeroDodgeBack = 11;

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
        private static readonly Color BubbleColor = new Color(0.45f, 0.7f, 1f);
        private static readonly Color BlinkColor = new Color(0.65f, 0.55f, 1f);
        private static readonly Color FireballColor = new Color(1f, 0.5f, 0.15f);
        private static readonly Color FrostColor = new Color(0.6f, 0.9f, 1f);
        private static readonly Color ExplodeColor = new Color(1f, 0.38f, 0.15f);
        private static readonly Color SmokeColor = new Color(0.25f, 0.18f, 0.15f, 0.65f);
        private static readonly Color SummonColor = new Color(0.7f, 0.35f, 1f);

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
        private string heroClassId;
        private Transform heroBubble;
        private Transform heroBubbleRing;
        private Transform heroBubbleFill;
        private Material bubbleRingMaterial;
        private Material bubbleFillMaterial;
        private float bubbleFlash;
        private Color bubbleFlashColor = BubbleColor;
        private bool heroHasBubble;

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

        /// <summary>Raised when the simulation evolves a weapon (argument: evolution catalog index), e.g. for a HUD toast.</summary>
        public event System.Action<int> WeaponEvolved;

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
            HideAllWeapons();
            RebuildObstacles();
            if (sim == null)
            {
                heroRoot.gameObject.SetActive(false);
                return;
            }

            heroRoot.gameObject.SetActive(true);
            string classId = sim.Config.ClassDef != null && !string.IsNullOrEmpty(sim.Config.ClassDef.Id) ? sim.Config.ClassDef.Id : "warrior";
            if (classId != heroClassId)
            {
                BuildHeroBody(classId);
            }
            heroHasBubble = SurvivorViewLogic.BlockIsBubble(sim.Config.ClassDef);
            bubbleFlash = 0f;
            heroBubble.gameObject.SetActive(false);
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
            SyncWeapons();
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
            SyncWeapons();
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
            PresentWeapons(realDelta);
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
            BuildWeapons();
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
            BuildHeroBody("warrior");

            heroShadow = CreateBlobShadow(heroRoot, 1.3f);
            heroShieldGlow = effects.CreateShield(heroRoot);
            BuildBubble();
            BuildBuffAuras();
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

        /// <summary>
        /// (Re)builds the hero model for a class (warrior knight, mage, hooded archer) with the shared hero clips.
        /// Classes without an art entry fall back to the default hero model inside <see cref="ArenaArtSet.HeroFor"/>.
        /// </summary>
        private void BuildHeroBody(string classId)
        {
            heroClassId = classId;
            heroAnimator?.Dispose();
            heroAnimator = null;
            for (int i = heroBody.childCount - 1; i >= 0; i--)
            {
                GameObject child = heroBody.GetChild(i).gameObject;
                // Detach first so GetComponentsInChildren below never sees the old model during a deferred Destroy.
                child.SetActive(false);
                child.transform.SetParent(null, false);
                DestroyUnityObject(child);
            }

            ArenaArtSet.HeroLook look = artSet != null && artSet.HasCharacters ? artSet.HeroFor(classId) : default;
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
                if (heroFallbackMaterial == null)
                {
                    heroFallbackMaterial = CreateStandard("Hero Fallback", new Color(0.22f, 0.48f, 0.85f), 0.3f);
                }
                heroFallbackMaterial.color = classId == "mage" ? new Color(0.45f, 0.3f, 0.85f)
                    : classId == "archer" ? new Color(0.3f, 0.6f, 0.3f)
                    : new Color(0.22f, 0.48f, 0.85f);
                GameObject body = CreatePrimitive("Hero Body", PrimitiveType.Capsule, heroBody, heroFallbackMaterial);
                body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                body.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            }
            heroBody.localScale = Vector3.one * heroBodyScale;
            heroRenderers = heroBody.GetComponentsInChildren<Renderer>(true);
            heroFlashTinted = false;
        }

        /// <summary>The mage's mana shield: a camera-facing ring plus a soft fill, shown while blocking.</summary>
        private void BuildBubble()
        {
            heroBubble = CreateChild("Mana Bubble", heroRoot);
            heroBubble.localPosition = new Vector3(0f, 0.95f, 0f);
            bubbleRingMaterial = Own(FxAssets.Create("Mana Bubble Ring", FxAssets.Ring, true));
            bubbleFillMaterial = Own(FxAssets.Create("Mana Bubble Fill", FxAssets.RadialGlow, true));
            heroBubbleFill = CreateFlatQuad("Fill", heroBubble, bubbleFillMaterial);
            heroBubbleRing = CreateFlatQuad("Ring", heroBubble, bubbleRingMaterial);
            heroBubble.gameObject.SetActive(false);
        }

        private void PresentBubble(bool blocking, float realDelta)
        {
            bubbleFlash = Mathf.Max(0f, bubbleFlash - realDelta * 4f);
            bool show = heroHasBubble && (blocking || bubbleFlash > 0f);
            if (heroBubble.gameObject.activeSelf != show)
            {
                heroBubble.gameObject.SetActive(show);
            }
            if (!show)
            {
                return;
            }

            Camera camera = ResolveCamera();
            Vector3 toCamera = camera != null ? -camera.transform.forward : Vector3.up;
            // The flat quads face +Y; turn them toward the camera so the bubble reads as a sphere outline.
            heroBubble.rotation = Quaternion.FromToRotation(Vector3.up, toCamera);
            float pulse = 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 6f);
            const float radius = 1.15f;
            float ringSize = radius * RingQuadPerRadius * pulse;
            heroBubbleRing.localScale = new Vector3(ringSize, 1f, ringSize);
            float fillSize = radius * 2.3f * pulse;
            heroBubbleFill.localScale = new Vector3(fillSize, 1f, fillSize);
            Color tint = Color.Lerp(BubbleColor, bubbleFlashColor, bubbleFlash);
            float strength = blocking ? 1f : bubbleFlash;
            bubbleRingMaterial.color = new Color(tint.r, tint.g, tint.b, (0.55f + 0.45f * bubbleFlash) * strength);
            bubbleFillMaterial.color = new Color(tint.r, tint.g, tint.b, (0.14f + 0.3f * bubbleFlash) * strength);
        }

        /// <summary>Block feedback: the mage's bubble flashes, everyone else's front shield does.</summary>
        private void FlashGuard(Color color)
        {
            if (heroHasBubble)
            {
                bubbleFlash = 1f;
                bubbleFlashColor = color;
            }
            else
            {
                heroShieldGlow?.Flash(color);
            }
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
            bool blocking = hero.Blocking && hero.Alive;
            heroShieldGlow?.SetBlocking(blocking && !heroHasBubble);
            PresentBubble(blocking, realDelta);
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
            BeginM9Events();
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
                        if (!StoreArea(StoreFx.PoisonExplode, point + Vector3.up * 0.6f, 2.2f, 1.8f))
                        {
                            effects.Sparkle(point, EliteColor, 18, 0.8f, 2.2f, 0.26f);
                        }
                        break;
                    }
                    case SurvivorEventType.BossKilled:
                    {
                        Vector3 point = ArenaSpace.ToWorld(e.Point);
                        effects.Shockwave(point, GoldColor, 9f, 1.1f);
                        effects.Flash(point + Vector3.up, Color.white, 6f, 0.4f);
                        if (!StoreArea(StoreFx.MagicCircleExplode, point + Vector3.up * 0.3f, 5f, 2.6f))
                        {
                            effects.Sparkle(point, GoldColor, 40, 2f, 3f, 0.35f);
                        }
                        effects.Text(point + Vector3.up * 3.5f, "TRÙM ĐÃ GỤC!", GoldColor, 2f, 2f);
                        break;
                    }
                    case SurvivorEventType.EliteSpawned:
                    {
                        Vector3 point = ArenaSpace.ToWorld(e.Point);
                        effects.Shockwave(point, EliteColor, 3f, 0.7f);
                        if (!StoreArea(StoreFx.SummonCircle2, point + Vector3.up * 0.1f, 2f, 2f))
                        {
                            effects.Puff(point, new Color(0.45f, 0.3f, 0.6f, 0.6f), 10, 1f, 1.8f, 0.9f, 0.8f);
                        }
                        break;
                    }
                    case SurvivorEventType.BossSpawned:
                    {
                        Vector3 point = ArenaSpace.ToWorld(e.Point);
                        effects.Shockwave(point, BossColor, 8f, 1.2f);
                        if (!StoreArea(StoreFx.SummonCircle2, point + Vector3.up * 0.1f, 4.5f, 2.6f))
                        {
                            effects.Puff(point, new Color(0.35f, 0.1f, 0.12f, 0.7f), 24, 1.6f, 3.5f, 1.4f, 1f);
                        }
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
                        if (!StoreArea(StoreFx.SmokeBlast, heroPosition, 3f, 2f))
                        {
                            effects.Puff(heroPosition, new Color(0.3f, 0.22f, 0.3f, 0.7f), 16, 1.2f, 2f, 1.2f, 0.6f);
                        }
                        break;
                    case SurvivorEventType.Blocked:
                        FlashGuard(heroHasBubble ? BubbleColor : BlockColor);
                        effects.Sparks(heroPosition + Vector3.up * 0.9f + heroRoot.forward * 0.6f, heroRoot.forward, heroHasBubble ? BubbleColor : BlockColor, 6, 5f, 0.9f);
                        break;
                    case SurvivorEventType.Parry:
                        FlashGuard(ParryColor);
                        effects.Flash(heroPosition + Vector3.up * 0.9f + heroRoot.forward * 0.6f, ParryColor, 2f, 0.25f);
                        effects.Text(heroPosition + Vector3.up * 2.4f, "ĐỠ ĐÒN!", ParryColor, 1.2f, 0.9f);
                        break;
                    case SurvivorEventType.SkillUsed:
                        OnSkillUsed(e, heroPosition);
                        break;
                    case SurvivorEventType.Healed:
                        effects.Sparkle(heroPosition, HealColor, 14, 0.7f, 2f, 0.24f);
                        PulseHealAura();
                        effects.Text(heroPosition + Vector3.up * 2.2f, "+" + NumberText(Mathf.RoundToInt(e.Value)), HealColor, 1.1f, 1f);
                        break;
                    case SurvivorEventType.LevelUp:
                        effects.Shockwave(heroPosition, LevelUpColor, 4.5f, 0.7f);
                        if (!StoreAt(StoreFx.StarAura, heroPosition, 2.2f, 2f))
                        {
                            effects.Sparkle(heroPosition, LevelUpColor, 22, 0.9f, 2.6f, 0.28f);
                        }
                        effects.Text(heroPosition + Vector3.up * 2.6f, "LÊN CẤP!", LevelUpColor, 1.4f, 1.3f);
                        break;
                    case SurvivorEventType.ItemPicked:
                    {
                        Color color = SurvivorViewLogic.ItemColor(e.Id);
                        effects.Shockwave(heroPosition, color, 3f, 0.5f);
                        if (!StoreAt(StoreFx.StarHit, heroPosition + Vector3.up * 1f, 2.2f, 1.2f))
                        {
                            effects.Sparkle(heroPosition, color, 16, 0.7f, 2.4f, 0.25f);
                        }
                        break;
                    }
                    case SurvivorEventType.ChestOpened:
                        OnChestOpened(e);
                        break;
                    case SurvivorEventType.MagnetPicked:
                        OnMagnetPicked(heroPosition);
                        break;
                    case SurvivorEventType.BuffStarted:
                        OnBuffStarted(e, heroPosition);
                        break;
                    case SurvivorEventType.BombExploded:
                        OnBombPickup(e);
                        break;
                    case SurvivorEventType.ManaRestored:
                        OnManaRestored(e, heroPosition);
                        break;
                    case SurvivorEventType.GoldCollected:
                        if (e.Id < 0 && e.Point.X * e.Point.X + e.Point.Y * e.Point.Y > 0f && sparksLeft > 0)
                        {
                            sparksLeft--;
                            effects.Sparkle(ArenaSpace.ToWorld(e.Point, 0.3f), GoldColor, 5, 0.3f, 1.2f, 0.18f);
                        }
                        break;
                    case SurvivorEventType.StrikeLanded:
                        OnStrikeLanded(e, events, i, heroPosition);
                        break;
                    case SurvivorEventType.EnemyExploded:
                        OnEnemyExploded(e);
                        break;
                    case SurvivorEventType.EnemyCharged:
                        OnEnemyCharged(e);
                        break;
                    case SurvivorEventType.EnemySplit:
                        OnEnemySplit(e);
                        break;
                    case SurvivorEventType.EnemyHealed:
                        OnEnemyHealed(e);
                        break;
                    case SurvivorEventType.GoldenSpawned:
                        OnGoldenSpawned(e);
                        break;
                    case SurvivorEventType.GoldenKilled:
                        OnGoldenKilled(e);
                        break;
                    case SurvivorEventType.EnemySummoned:
                    {
                        Vector3 point = ArenaSpace.ToWorld(e.Point);
                        if (enemiesById.TryGetValue(e.Id, out EnemyView summoner))
                        {
                            summoner.Animator?.PlayOneShot(WalkerThrowState, 1.2f, false);
                        }
                        effects.Shockwave(point, SummonColor, 3.5f * RingQuadPerRadius * 0.5f, 0.8f);
                        effects.Flash(point + Vector3.up * 1.2f, SummonColor, 2.2f, 0.3f);
                        StoreArea(StoreFx.RuneOfMagic, point + Vector3.up * 0.1f, 1.8f, 1.6f);
                        if (puffsLeft > 0)
                        {
                            puffsLeft--;
                            effects.Puff(point, new Color(0.35f, 0.15f, 0.5f, 0.6f), 10, 1f, 2.6f, 1f, 0.7f);
                        }
                        break;
                    }
                    case SurvivorEventType.WeaponEvolved:
                    {
                        Color gold = SurvivorViewLogic.EvolutionGold;
                        effects.Shockwave(heroPosition, gold, 6f, 0.8f);
                        effects.Flash(heroPosition + Vector3.up, gold, 4f, 0.35f);
                        effects.Sparkle(heroPosition, gold, 32, 1.2f, 3f, 0.3f);
                        effects.Text(heroPosition + Vector3.up * 3f, "TIẾN HÓA!", gold, 1.6f, 1.6f);
                        WeaponEvolved?.Invoke(e.Id);
                        break;
                    }
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

            WeaponVisual visual = SurvivorViewLogic.WeaponVisualOf(e.Id);
            switch (visual)
            {
                case WeaponVisual.Sweep:
                case WeaponVisual.Dagger:
                {
                    // Sword, dagger and their evolutions: a crescent the size of the real hit arc.
                    bool dagger = visual == WeaponVisual.Dagger;
                    int level = Mathf.Max(1, sim.Inventory.Level(e.Id));
                    float reach = SurvivorViewLogic.SweepRange(e.Id, level, sim.DerivedStats.AreaMul) / 1.25f;
                    bool mirror = (strikeCount & 1) == 1;
                    PlayHeroOneShot(mirror ? HeroStrikeB : HeroStrikeA, dagger ? 2.3f : 1.7f, false);
                    strikeCount++;
                    Color color = FxColor(e.Id, StrikeColor);
                    float life = dagger ? 0.16f : 0.22f;
                    Vector3 origin = heroPosition + Vector3.up * 0.85f;
                    effects.Slash(origin, yaw, color, mirror, reach, life);
                    if (SurvivorViewLogic.SweepHitsBehind(e.Id, level))
                    {
                        effects.Slash(origin, yaw + 180f, color, !mirror, reach, life);
                    }
                    if (SurvivorViewLogic.IsEvolution(e.Id) && sparksLeft > 0)
                    {
                        sparksLeft--;
                        effects.Sparkle(origin + direction * reach * 0.6f, SurvivorViewLogic.EvolutionGold, 6, reach * 0.4f, 1f, 0.18f);
                    }
                    break;
                }
                case WeaponVisual.FlameCone:
                {
                    PlayHeroOneShot(HeroCast, 2.1f, false);
                    Vector3 origin = heroPosition + Vector3.up * 0.8f;
                    Color color = FxColor(e.Id, FireballColor);
                    effects.Slash(origin, yaw - 22f, color, false, 2.2f, 0.16f);
                    effects.Slash(origin, yaw, color, true, 2.8f, 0.2f);
                    effects.Slash(origin, yaw + 22f, color, false, 2.2f, 0.16f);
                    Vector3 side = Vector3.Cross(Vector3.up, direction);
                    for (int k = 0; k < 12; k++)
                    {
                        float reach = Random.Range(0.8f, 4.5f);
                        effects.Flame(heroPosition + direction * reach + side * (Random.Range(-0.4f, 0.4f) * reach),
                            color, Random.Range(0.8f, 1.4f), 0.5f);
                    }
                    break;
                }
                case WeaponVisual.Combo:
                {
                    bool finisher = e.Extra >= 3f;
                    bool mirror = ((int)e.Extra & 1) == 0;
                    PlayHeroOneShot(mirror ? HeroStrikeB : HeroStrikeA, finisher ? 2.2f : 2.5f, false);
                    effects.Slash(heroPosition + Vector3.up * 0.85f, yaw, FxColor(e.Id, StrikeColor), mirror,
                        finisher ? 3f : 2.2f, finisher ? 0.28f : 0.16f);
                    break;
                }
                case WeaponVisual.Hammer:
                case WeaponVisual.Arrow:
                case WeaponVisual.Bomb:
                case WeaponVisual.Bounce:
                case WeaponVisual.Momentum:
                case WeaponVisual.Trio:
                case WeaponVisual.Quad:
                    PlayHeroOneShot(HeroThrow, 1.9f, false);
                    break;
                case WeaponVisual.MagicBolt:
                case WeaponVisual.MultiShot:
                    PlayHeroOneShot(HeroCast, 1.9f, false);
                    break;
                default:
                    OnNewWeaponFired(e, heroPosition, direction, visual);
                    break;
            }
        }

        private void OnSkillUsed(SurvivorEvent e, Vector3 heroPosition)
        {
            int slot = (int)e.Value;
            SkillDef[] skills = sim.Config.ClassDef.ActiveSkills;
            SkillDef skill = slot >= 0 && slot < skills.Length ? skills[slot] : null;
            SkillKind kind = skill != null ? skill.Kind : SkillKind.None;
            Vector3 forward = heroRoot.forward;
            switch (kind)
            {
                case SkillKind.Kick:
                    PlayHeroOneShot(HeroKick, 1.5f, true);
                    effects.Shockwave(heroPosition + forward * 1f, KickColor, 2.6f, 0.35f);
                    effects.Twirl(heroPosition + forward * 0.8f + Vector3.up * 0.5f, KickColor, 2.6f, 0.3f);
                    effects.Sparks(heroPosition + Vector3.up * 0.6f + forward * 0.9f, forward, KickColor, 10, 7f, 0.9f);
                    break;
                case SkillKind.Dash:
                    PlayHeroOneShot(skill.DashBackward ? HeroDodgeBack : HeroDash, 1.6f, true);
                    effects.Puff(heroPosition, DustColor, 6, 0.7f, 1.2f, 0.6f, 0.3f);
                    break;
                case SkillKind.Block:
                    FlashGuard(heroHasBubble ? BubbleColor : BlockColor);
                    break;
                case SkillKind.Projectile:
                {
                    // Fireball for the mage, power shot for the archer.
                    bool fire = SurvivorViewLogic.ProjectileLookOf(-1 - slot, sim.Config.ClassDef) == ProjectileLook.Fireball;
                    PlayHeroOneShot(fire ? HeroCast : HeroThrow, 1.8f, true);
                    Color color = fire ? FireballColor : SurvivorViewLogic.EvolutionGold;
                    effects.Flash(heroPosition + Vector3.up * 1f + forward * 0.7f, color, 1.8f, 0.2f);
                    break;
                }
                case SkillKind.AreaBurst:
                    // The ice ring itself is drawn from the StrikeLanded event of the burst.
                    PlayHeroOneShot(HeroCast, 2f, true);
                    break;
                case SkillKind.Teleport:
                {
                    // Core moved the hero before announcing the skill: flash at both ends and snap the model.
                    Vector3 from = heroPrevious;
                    Vector3 to = heroCurrent;
                    effects.Afterimage(heroRenderers, BlinkColor, 0.45f);
                    effects.Flash(from + Vector3.up * 0.9f, BlinkColor, 2.6f, 0.3f);
                    effects.Flash(to + Vector3.up * 0.9f, BlinkColor, 2.6f, 0.3f);
                    effects.Sparkle(from, BlinkColor, 12, 0.6f, 1.6f, 0.22f);
                    effects.Sparkle(to, BlinkColor, 12, 0.6f, 1.6f, 0.22f);
                    effects.Shockwave(to, BlinkColor, 2.4f, 0.35f);
                    if (!StoreAt(StoreFx.Portal, from + Vector3.up * 0.9f, 1.8f, 1.2f))
                    {
                        effects.Rune(from, BlinkColor, 3f, 0.6f);
                    }
                    if (!StoreAt(StoreFx.Portal, to + Vector3.up * 0.9f, 2.2f, 1.2f))
                    {
                        effects.Rune(to, BlinkColor, 3.6f, 0.8f);
                    }
                    heroSnap = true;
                    break;
                }
                case SkillKind.Leap:
                    PlayHeroOneShot(HeroDash, 1.8f, true);
                    effects.Afterimage(heroRenderers, WhirlColor, 0.4f);
                    effects.Puff(heroPosition, DustColor, 8, 0.8f, 1.5f, 0.65f, 0.25f);
                    RememberM9Skill(kind);
                    break;
                case SkillKind.Whirlwind:
                    PlayHeroOneShot(HeroStrikeA, 2.4f, true);
                    effects.Shockwave(heroPosition, WhirlColor, 5.5f, 0.35f);
                    effects.Twirl(heroPosition + Vector3.up * 0.7f, WhirlColor, 5.5f, 0.5f);
                    RememberM9Skill(kind);
                    break;
                case SkillKind.Trap:
                    PlayHeroOneShot(HeroThrow, 2f, true);
                    effects.Flash(heroPosition + forward * 0.8f + Vector3.up * 0.4f, TrapColor, 1.2f, 0.18f);
                    RememberM9Skill(kind);
                    break;
                case SkillKind.Barrage:
                    PlayHeroOneShot(HeroThrow, 2.2f, true);
                    effects.Sparkle(heroPosition + forward, SurvivorViewLogic.ItemColor(SurvivorCatalog.QuadShotIndex), 9, 0.8f, 1.5f, 0.17f);
                    RememberM9Skill(kind);
                    break;
                case SkillKind.Wall:
                    PlayHeroOneShot(HeroCast, 2f, true);
                    effects.Flash(heroPosition + forward * 1.2f + Vector3.up * 0.5f, WallColor, 2f, 0.2f);
                    RememberM9Skill(kind);
                    break;
                case SkillKind.Chain:
                    PlayHeroOneShot(HeroCast, 2.2f, true);
                    effects.Flash(heroPosition + Vector3.up, LightningColor, 2f, 0.18f);
                    RememberM9Skill(kind);
                    break;
            }
        }

        private void OnEnemyExploded(SurvivorEvent e)
        {
            Vector3 point = ArenaSpace.ToWorld(e.Point);
            float radius = Mathf.Max(0.5f, e.Value);
            effects.Shockwave(point, ExplodeColor, radius * RingQuadPerRadius, 0.45f);
            Blast(point, ExplodeColor, radius, StoreFx.PoisonExplode);
            effects.Sparks(point + Vector3.up * 0.5f, Vector3.up, ExplodeColor, 10, 8f, 1.2f);
            if (puffsLeft > 0)
            {
                puffsLeft--;
                effects.Puff(point, SmokeColor, 10, 1.1f, radius, 0.9f, 0.9f);
            }
            // The exploder is gone at once (no death animation, no EnemyKilled follows).
            if (enemiesById.TryGetValue(e.Id, out EnemyView view))
            {
                ReleaseEnemy(view);
            }
        }

        /// <summary>
        /// Effect colour of a weapon: the renderer's own colour for the warrior's weapons, the item colour for the
        /// mage and archer weapons, pulled toward gold for an evolution.
        /// </summary>
        private static Color FxColor(int catalogIndex, Color original)
        {
            if (catalogIndex < 0)
            {
                return original;
            }
            int baseIndex = SurvivorViewLogic.BaseWeapon(catalogIndex);
            Color color = baseIndex <= 5 ? original : SurvivorViewLogic.ItemColor(baseIndex);
            return SurvivorViewLogic.IsEvolution(catalogIndex) ? Color.Lerp(color, SurvivorViewLogic.EvolutionGold, 0.6f) : color;
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
            if (view != null)
            {
                QueueElementFx(point, 1.5f * view.HeightScale, false, view.Chilled);
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
                if ((point - heroCurrent).sqrMagnitude < 900f)
                {
                    QueueElementFx(point, 1.5f * view.HeightScale, true, view.Chilled);
                }
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
