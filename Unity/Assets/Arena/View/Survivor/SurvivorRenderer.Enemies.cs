using System.Collections.Generic;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PersonalArena.View
{
    public sealed partial class SurvivorRenderer
    {
        private const int LookWalker = 0;
        private const int LookRunner = 1;
        private const int LookBrute = 2;
        private const int LookBoss = 3;
        private const int LookSpitter = 4;
        private const int LookExploder = 5;
        private const int LookGhost = 6;
        private const int LookNecromancer = 7;
        private const int LookCharger = 8;
        private const int LookSplitter = 9;
        private const int LookShaman = 10;
        private const int LookCount = 11;
        private const int SpitterTypeIndex = 3;
        private const int MaxCreatesPerFrame = 3;
        private const int MaxDying = 60;
        private const float SinkAfterSeconds = 1.1f;
        private const float ReleaseAfterSeconds = 2f;
        private const float ClawCooldownSeconds = 0.9f;

        private const int WalkerIdle = 0;
        private const int WalkerWalk = 1;
        private const int WalkerAttack = 2;
        private const int WalkerDeath = 4;
        private const int WalkerSpawn = 5;
        private const int WalkerRun = 6;
        private const int WalkerThrowState = 7;

        private const int TintNone = 0;
        private const int TintFlash = 1;
        private const int TintWindup = 2;
        private const int TintStun = 3;
        private const int TintElite = 4;
        private const int TintBoss = 5;
        private const int TintSlow = 6;
        private const int TintGolden = 7;

        private static readonly bool[] WalkerLoops = { true, true, false, false, false, false, true, false };
        private static readonly int[] PrewarmCounts = { 40, 20, 10, 1, 8, 6, 6, 2, 4, 6, 2 };
        private static readonly int[] ReserveCounts = { 16, 10, 6, 1, 4, 4, 4, 2, 3, 4, 2 };

        // Resting colours of the M7 enemies (multiplied into their KayKit textures).
        private static readonly Color ExploderTint = new Color(1.6f, 0.72f, 0.42f, 1f);
        private static readonly Color GhostTint = new Color(0.72f, 0.9f, 1.35f, 0.5f);
        private static readonly Color NecromancerTint = new Color(1f, 0.72f, 1.45f, 1f);
        // M11 enemies.
        private static readonly Color ChargerTint = new Color(1.55f, 0.5f, 0.45f, 1f);
        private static readonly Color SplitterTint = new Color(0.65f, 1.5f, 0.55f, 1f);
        private static readonly Color ShamanTint = new Color(0.55f, 1.45f, 1.3f, 1f);
        private static readonly Color ShamanHealColor = new Color(0.4f, 1f, 0.6f);
        private static readonly Color[] TintColors =
        {
            Color.white,
            new Color(2.2f, 2.2f, 2.2f),
            new Color(1.45f, 1.1f, 0.55f),
            new Color(0.7f, 0.9f, 1.5f),
            new Color(1.3f, 0.9f, 1.7f),
            new Color(1.45f, 0.8f, 0.75f),
            new Color(0.55f, 0.8f, 1.35f),
            new Color(2.1f, 1.6f, 0.45f)
        };

        private readonly Stack<EnemyView>[] enemyPools = new Stack<EnemyView>[LookCount];
        private readonly List<EnemyView> allEnemyViews = new List<EnemyView>(512);
        private readonly List<EnemyView> dyingViews = new List<EnemyView>(MaxDying + 8);
        private readonly Dictionary<int, EnemyView> enemiesById = new Dictionary<int, EnemyView>(512);
        private readonly EnemyView[] slotViews = new EnemyView[SurvivorSim.EnemyCapacity];
        private readonly Plane[] frustumPlanes = new Plane[6];
        private bool hasFrustum;
        private int visibleEnemies;
        private Transform enemyPoolRoot;
        private Material eliteAuraMaterial;
        private Material bossAuraMaterial;
        private Material goldenAuraMaterial;
        private readonly List<EnemyView> demoEnemies = new List<EnemyView>();
        private Material enemyFallbackMaterial;

        private sealed class EnemyView
        {
            public Transform Root;
            public Transform Body;
            public Transform Shadow;
            public Transform Aura;
            public MeshRenderer AuraRenderer;
            public bool Golden;
            public float Twinkle;
            public Renderer[] Renderers;
            public CharacterAnimator Animator;
            public int Look;
            public float BodyScale;
            public float HeightScale = 1f;
            public bool Runs;
            public int Id = -1;
            public int Slot = -1;
            public Vector3 Previous;
            public Vector3 Current;
            public float PreviousYaw;
            public float CurrentYaw;
            public bool Elite;
            public bool Boss;
            public bool Walking;
            public bool WasWinding;
            public float Flash;
            public bool Chilled;
            public float Claw;
            public bool Dying;
            public float DeadSeconds;
            public float PendingAnimation;
            public int AppliedTint;
            public int Phase;
            public float DisplayScale = 1f;
            public bool Pulsing;
        }

        private readonly Dictionary<Material, Material> ghostMaterials = new Dictionary<Material, Material>();

        /// <summary>Art walker used for a look (see <see cref="SurvivorViewLogic.EnemyWalkerIndex"/>).</summary>
        private static int WalkerIndexOfLook(int look)
        {
            switch (look)
            {
                case LookBoss: return LookBrute;
                case LookSpitter: return SpitterTypeIndex;
                case LookExploder: return SurvivorViewLogic.EnemyWalkerIndex(SurvivorDefaults.ExploderTypeIndex);
                case LookGhost: return SurvivorViewLogic.EnemyWalkerIndex(SurvivorDefaults.GhostTypeIndex);
                case LookNecromancer: return SurvivorViewLogic.EnemyWalkerIndex(SurvivorDefaults.NecromancerTypeIndex);
                case LookCharger: return LookBrute;
                case LookSplitter: return LookWalker;
                case LookShaman: return SpitterTypeIndex;
                default: return look;
            }
        }

        private static bool HasBaseTint(int look) => look == LookExploder || look == LookGhost || look == LookNecromancer || look >= LookCharger;

        /// <summary>Extra body size of a look on top of its walker model.</summary>
        private static float LookScale(int look) => look == LookSplitter ? 1.35f : look == LookCharger ? 0.9f : 1f;

        private static Color BaseTint(int look)
        {
            switch (look)
            {
                case LookExploder: return ExploderTint;
                case LookGhost: return GhostTint;
                case LookNecromancer: return NecromancerTint;
                case LookCharger: return ChargerTint;
                case LookSplitter: return SplitterTint;
                case LookShaman: return ShamanTint;
                default: return Color.white;
            }
        }

        /// <summary>Lets the ghost's tint alpha show: swaps its materials for shared Standard "Fade" copies.</summary>
        private void MakeGhostly(Renderer[] renderers)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] shared = renderers[i].sharedMaterials;
                for (int j = 0; j < shared.Length; j++)
                {
                    shared[j] = GhostMaterialFor(shared[j]);
                }
                renderers[i].sharedMaterials = shared;
            }
        }

        private Material GhostMaterialFor(Material source)
        {
            if (source == null)
            {
                return null;
            }
            if (ghostMaterials.TryGetValue(source, out Material ghost))
            {
                return ghost;
            }
            ghost = Own(new Material(source) { name = source.name + " (Ghost)" });
            // Standard shader "Fade" rendering mode, set the same way the Standard material inspector does.
            ghost.SetFloat("_Mode", 2f);
            ghost.SetOverrideTag("RenderType", "Transparent");
            ghost.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            ghost.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            ghost.SetFloat("_ZWrite", 0f);
            ghost.DisableKeyword("_ALPHATEST_ON");
            ghost.EnableKeyword("_ALPHABLEND_ON");
            ghost.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            ghost.renderQueue = (int)RenderQueue.Transparent;
            ghostMaterials[source] = ghost;
            return ghost;
        }

        private void BuildEnemyPools()
        {
            enemyPoolRoot = CreateChild("Enemies", actorsRoot);
            eliteAuraMaterial = FxAssets.Create("Elite Aura", FxAssets.Ring, true);
            eliteAuraMaterial.color = new Color(0.75f, 0.35f, 1f, 0.9f);
            bossAuraMaterial = FxAssets.Create("Boss Aura", FxAssets.Ring, true);
            bossAuraMaterial.color = new Color(1f, 0.2f, 0.15f, 0.95f);
            goldenAuraMaterial = FxAssets.Create("Golden Aura", VfxLibrary.Texture("magic_02"), true);
            goldenAuraMaterial.color = new Color(1f, 0.82f, 0.25f, 0.95f);
            for (int look = 0; look < LookCount; look++)
            {
                enemyPools[look] = new Stack<EnemyView>(64);
                for (int i = 0; i < PrewarmCounts[look]; i++)
                {
                    enemyPools[look].Push(CreateEnemyView(look));
                }
            }
        }

        private EnemyView CreateEnemyView(int look)
        {
            EnemyView view = new EnemyView { Look = look, Phase = allEnemyViews.Count };
            GameObject root = new GameObject(LookName(look));
            root.transform.SetParent(enemyPoolRoot, false);
            view.Root = root.transform;
            view.Body = CreateChild("Body Visual", view.Root);

            GameObject body = null;
            GameObject mainHand = null;
            GameObject offHand = null;
            float scale = 1f;
            if (look == LookBoss && survivorArt != null && survivorArt.BossBody != null)
            {
                body = survivorArt.BossBody;
                mainHand = survivorArt.BossMainHand;
                offHand = survivorArt.BossOffHand;
                scale = survivorArt.BossScale > 0f ? survivorArt.BossScale : 2.6f;
            }
            else if (artSet != null && artSet.HasCharacters && artSet.Walkers.Length > 0)
            {
                ArenaArtSet.WalkerLook walker = artSet.WalkerFor(WalkerIndexOfLook(look));
                body = walker.Body;
                mainHand = walker.MainHand;
                offHand = walker.OffHand;
                scale = walker.Scale > 0f ? walker.Scale : 1f;
                view.Runs = walker.Runs;
                if (look == LookBoss)
                {
                    scale = 2.6f;
                }
            }

            if (body != null && artSet != null)
            {
                view.BodyScale = artSet.CharacterScale * scale * LookScale(look);
                Animator animator = SpawnCharacter(body, view.Body, mainHand, offHand);
                view.Animator = new CharacterAnimator(animator, new[]
                {
                    artSet.WalkerIdle, artSet.WalkerWalk, artSet.WalkerAttack, artSet.WalkerHit,
                    artSet.WalkerDeath, artSet.WalkerSpawn, artSet.WalkerRun, artSet.WalkerThrow
                }, WalkerLoops, "Survivor Enemy");
            }
            else
            {
                if (enemyFallbackMaterial == null)
                {
                    enemyFallbackMaterial = CreateStandard("Enemy Fallback", new Color(0.78f, 0.76f, 0.66f), 0.15f);
                }
                view.BodyScale = look == LookBoss ? 2.6f : look == LookBrute ? 1.4f : 1f;
                GameObject capsule = CreatePrimitive("Enemy Body", PrimitiveType.Capsule, view.Body, enemyFallbackMaterial);
                capsule.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                capsule.transform.localScale = new Vector3(0.7f, 0.8f, 0.7f);
            }

            view.Body.localScale = Vector3.one * view.BodyScale;
            view.Renderers = view.Body.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < view.Renderers.Length; i++)
            {
                // The horde uses cheap blob shadows; hundreds of shadow casters would dominate the frame.
                view.Renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            }
            if (look == LookGhost)
            {
                MakeGhostly(view.Renderers);
            }

            float shadowSize = look == LookBoss ? 3.6f : look == LookBrute ? 1.5f : 1.05f;
            view.Shadow = CreateBlobShadow(view.Root, shadowSize);

            GameObject aura = new GameObject("Aura");
            aura.transform.SetParent(view.Root, false);
            aura.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            aura.AddComponent<MeshFilter>().sharedMesh = FxAssets.FlatQuad;
            MeshRenderer auraRenderer = aura.AddComponent<MeshRenderer>();
            auraRenderer.sharedMaterial = look == LookBoss ? bossAuraMaterial : eliteAuraMaterial;
            auraRenderer.shadowCastingMode = ShadowCastingMode.Off;
            auraRenderer.receiveShadows = false;
            view.Aura = aura.transform;
            view.AuraRenderer = auraRenderer;
            aura.SetActive(false);

            root.SetActive(false);
            allEnemyViews.Add(view);
            return view;
        }

        private static string LookName(int look)
        {
            switch (look)
            {
                case LookRunner: return "Runner";
                case LookBrute: return "Brute";
                case LookBoss: return "Bone Lord";
                case LookSpitter: return "Spitter";
                case LookExploder: return "Exploder";
                case LookGhost: return "Ghost";
                case LookNecromancer: return "Necromancer";
                case LookCharger: return "Charger";
                case LookSplitter: return "Splitter";
                case LookShaman: return "Shaman";
                default: return "Walker";
            }
        }

        private static int LookFor(SurvivorEnemy enemy)
        {
            if (enemy.IsBoss)
            {
                return LookBoss;
            }
            int type = enemy.TypeIndex;
            if (type == 1)
            {
                return LookRunner;
            }
            if (type == 2)
            {
                return LookBrute;
            }
            if (type == SpitterTypeIndex)
            {
                return LookSpitter;
            }
            if (type == SurvivorDefaults.ExploderTypeIndex)
            {
                return LookExploder;
            }
            if (type == SurvivorDefaults.GhostTypeIndex)
            {
                return LookGhost;
            }
            if (type == SurvivorDefaults.NecromancerTypeIndex)
            {
                return LookNecromancer;
            }
            if (type == SurvivorDefaults.ChargerTypeIndex)
            {
                return LookCharger;
            }
            if (type == SurvivorDefaults.SplitterTypeIndex)
            {
                return LookSplitter;
            }
            if (type == SurvivorDefaults.ShamanTypeIndex)
            {
                return LookShaman;
            }
            return LookWalker;
        }

        private EnemyView AcquireEnemy(int look)
        {
            Stack<EnemyView> pool = enemyPools[look];
            return pool.Count > 0 ? pool.Pop() : CreateEnemyView(look);
        }

        private void ReleaseEnemy(EnemyView view)
        {
            if (view.Slot >= 0 && view.Slot < slotViews.Length && slotViews[view.Slot] == view)
            {
                slotViews[view.Slot] = null;
            }
            if (view.Id >= 0 && enemiesById.TryGetValue(view.Id, out EnemyView mapped) && mapped == view)
            {
                enemiesById.Remove(view.Id);
            }
            view.Id = -1;
            view.Slot = -1;
            view.Dying = false;
            view.Root.gameObject.SetActive(false);
            enemyPools[view.Look].Push(view);
        }

        private void ReleaseAllEnemies()
        {
            for (int i = 0; i < slotViews.Length; i++)
            {
                if (slotViews[i] != null)
                {
                    ReleaseEnemy(slotViews[i]);
                }
            }
            for (int i = 0; i < dyingViews.Count; i++)
            {
                ReleaseEnemy(dyingViews[i]);
            }
            dyingViews.Clear();
            enemiesById.Clear();
        }

        private void TopUpEnemyPools()
        {
            int budget = MaxCreatesPerFrame;
            for (int look = 0; look < LookCount && budget > 0; look++)
            {
                while (enemyPools[look].Count < ReserveCounts[look] && budget > 0)
                {
                    enemyPools[look].Push(CreateEnemyView(look));
                    budget--;
                }
            }
        }

        private void StartDying(EnemyView view)
        {
            if (view.Dying)
            {
                return;
            }
            if (view.Slot >= 0 && slotViews[view.Slot] == view)
            {
                slotViews[view.Slot] = null;
            }
            enemiesById.Remove(view.Id);
            view.Slot = -1;
            view.Dying = true;
            view.DeadSeconds = 0f;
            view.Flash = 0f;
            view.Aura.gameObject.SetActive(false);
            if (view.Pulsing)
            {
                view.Pulsing = false;
                view.Body.localScale = Vector3.one * view.DisplayScale;
            }
            view.Animator?.PlayOneShot(WalkerDeath, 1.5f, true);
            view.PendingAnimation = Mathf.Max(view.PendingAnimation, 0.02f);
            if (dyingViews.Count >= MaxDying)
            {
                EnemyView oldest = dyingViews[0];
                dyingViews.RemoveAt(0);
                ReleaseEnemy(oldest);
            }
            dyingViews.Add(view);
        }

        /// <summary>Matches the pooled views to the simulation's enemy slots after a tick.</summary>
        private void SyncEnemies()
        {
            if (sim == null)
            {
                return;
            }

            IReadOnlyList<SurvivorEnemy> enemies = sim.Enemies;
            float eliteScale = sim.Config.Tuning.EliteRadiusMul > 0f ? sim.Config.Tuning.EliteRadiusMul : 1.6f;
            Vector3 hero = heroCurrent;
            float heroRadius = sim.Hero.Radius;
            int count = Mathf.Min(enemies.Count, slotViews.Length);
            for (int i = 0; i < count; i++)
            {
                SurvivorEnemy enemy = enemies[i];
                EnemyView view = slotViews[i];
                if (!enemy.Active)
                {
                    if (view != null)
                    {
                        ReleaseEnemy(view);
                    }
                    continue;
                }

                int look = LookFor(enemy);
                if (view != null && (view.Id != enemy.Id || view.Look != look))
                {
                    ReleaseEnemy(view);
                    view = null;
                }

                Vector3 position = ArenaSpace.ToWorld(enemy.Position);
                float yaw = ArenaSpace.YawDegrees(enemy.Facing);
                if (view == null)
                {
                    view = AcquireEnemy(look);
                    view.Id = enemy.Id;
                    view.Slot = i;
                    view.Elite = enemy.Elite;
                    view.Boss = enemy.IsBoss;
                    view.Golden = enemy.Golden;
                    view.Twinkle = 0f;
                    view.Previous = position;
                    view.Current = position;
                    view.PreviousYaw = yaw;
                    view.CurrentYaw = yaw;
                    view.Walking = false;
                    view.WasWinding = false;
                    view.Flash = 0f;
                    view.Claw = 0f;
                    view.Dying = false;
                    view.PendingAnimation = 0f;
                    view.AppliedTint = -1;
                    float scale = view.BodyScale * (view.Elite ? eliteScale : 1f)
                        * (enemy.Small ? SurvivorSim.SplitRadiusMul : 1f) * (enemy.Golden ? SurvivorSim.GoldenRadiusMul : 1f);
                    view.HeightScale = scale / Mathf.Max(0.01f, artSet != null ? artSet.CharacterScale : 1f);
                    view.Body.localScale = Vector3.one * scale;
                    view.Body.localPosition = Vector3.zero;
                    view.DisplayScale = scale;
                    view.Pulsing = false;
                    view.Shadow.localScale = Vector3.one * (view.Elite ? eliteScale : 1f) * (view.Boss ? 3.6f : view.Look == LookBrute ? 1.5f : 1.05f);
                    bool aura = view.Elite || view.Boss || view.Golden;
                    view.Aura.gameObject.SetActive(aura);
                    if (aura)
                    {
                        view.AuraRenderer.sharedMaterial = view.Golden ? goldenAuraMaterial : view.Boss ? bossAuraMaterial : eliteAuraMaterial;
                        float auraSize = enemy.Radius * (view.Golden ? 5f : 2.8f);
                        view.Aura.localScale = new Vector3(auraSize, 1f, auraSize);
                    }
                    view.Root.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                    view.Root.gameObject.SetActive(true);
                    if (view.Animator != null)
                    {
                        view.Animator.ResetTo(WalkerIdle);
                        if (sim.Time > 0.5f && !view.Boss)
                        {
                            view.Animator.PlayOneShot(WalkerSpawn, 2.2f, false);
                        }
                    }
                    slotViews[i] = view;
                    enemiesById[enemy.Id] = view;
                }
                else
                {
                    view.Previous = view.Current;
                    view.PreviousYaw = view.CurrentYaw;
                    view.Current = position;
                    view.CurrentYaw = yaw;
                    if ((view.Current - view.Previous).sqrMagnitude > SnapDistance * SnapDistance)
                    {
                        // Relocated in front of the hero: jump instead of sliding across the map.
                        view.Previous = view.Current;
                        view.PreviousYaw = view.CurrentYaw;
                    }
                }

                UpdateEnemyAnimationState(view, enemy, hero, heroRadius);
            }
        }

        private void DisposeEnemyAnimators()
        {
            for (int i = 0; i < allEnemyViews.Count; i++)
            {
                allEnemyViews[i].Animator?.Dispose();
            }
            DestroyUnityObject(eliteAuraMaterial);
            DestroyUnityObject(bossAuraMaterial);
            DestroyUnityObject(goldenAuraMaterial);
            DestroyUnityObject(enemyFallbackMaterial);
        }
    }
}
