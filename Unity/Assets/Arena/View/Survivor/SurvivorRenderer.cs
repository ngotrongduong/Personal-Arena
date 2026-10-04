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
        private static readonly Color DaggerSlashColor = new Color(1f, 0.22f, 0.18f, 0.95f);
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
            double at = PerfTrace.Now;
            PresentFrame(realDelta);
            TopUpEnemyPools();
            PerfTrace.Span("renderer late update", at, 20.0);

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
            double at = PerfTrace.Now;
            BuildScenery();
            PerfTrace.Span("build scenery", at);
            at = PerfTrace.Now;
            BuildHero();
            PerfTrace.Span("build hero", at);
            at = PerfTrace.Now;
            BuildEnemyPools();
            PerfTrace.Span("build enemy pools", at);
            at = PerfTrace.Now;
            BuildPickups();
            BuildHammers();
            PerfTrace.Span("build pickups", at);
            at = PerfTrace.Now;
            BuildWeapons();
            PerfTrace.Span("build weapons", at);
        }

        private static Transform CreateChild(string childName, Transform parent)
        {
            GameObject child = new GameObject(childName);
            child.transform.SetParent(parent, false);
            return child.transform;
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
