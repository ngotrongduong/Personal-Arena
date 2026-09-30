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
        private const int LookCount = 5;
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

        private static readonly bool[] WalkerLoops = { true, true, false, false, false, false, true, false };
        private static readonly int[] PrewarmCounts = { 40, 20, 10, 1, 8 };
        private static readonly int[] ReserveCounts = { 16, 10, 6, 1, 4 };
        private static readonly Color[] TintColors =
        {
            Color.white,
            new Color(2.2f, 2.2f, 2.2f),
            new Color(1.45f, 1.1f, 0.55f),
            new Color(0.7f, 0.9f, 1.5f),
            new Color(1.3f, 0.9f, 1.7f),
            new Color(1.45f, 0.8f, 0.75f)
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
        private Material enemyFallbackMaterial;

        private sealed class EnemyView
        {
            public Transform Root;
            public Transform Body;
            public Transform Shadow;
            public Transform Aura;
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
            public float Claw;
            public bool Dying;
            public float DeadSeconds;
            public float PendingAnimation;
            public int AppliedTint;
            public int Phase;
        }

        private void BuildEnemyPools()
        {
            enemyPoolRoot = CreateChild("Enemies", actorsRoot);
            eliteAuraMaterial = FxAssets.Create("Elite Aura", FxAssets.Ring, true);
            eliteAuraMaterial.color = new Color(0.75f, 0.35f, 1f, 0.9f);
            bossAuraMaterial = FxAssets.Create("Boss Aura", FxAssets.Ring, true);
            bossAuraMaterial.color = new Color(1f, 0.2f, 0.15f, 0.95f);
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
                int walkerIndex = look == LookBoss ? LookBrute : look == LookSpitter ? SpitterTypeIndex : look;
                ArenaArtSet.WalkerLook walker = artSet.WalkerFor(walkerIndex);
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
                view.BodyScale = artSet.CharacterScale * scale;
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
                    float scale = view.BodyScale * (view.Elite ? eliteScale : 1f);
                    view.HeightScale = scale / Mathf.Max(0.01f, artSet != null ? artSet.CharacterScale : 1f);
                    view.Body.localScale = Vector3.one * scale;
                    view.Body.localPosition = Vector3.zero;
                    view.Shadow.localScale = Vector3.one * (view.Elite ? eliteScale : 1f) * (view.Boss ? 3.6f : view.Look == LookBrute ? 1.5f : 1.05f);
                    bool aura = view.Elite || view.Boss;
                    view.Aura.gameObject.SetActive(aura);
                    if (aura)
                    {
                        float auraSize = enemy.Radius * 2.8f;
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

        private void UpdateEnemyAnimationState(EnemyView view, SurvivorEnemy enemy, Vector3 hero, float heroRadius)
        {
            CharacterAnimator animator = view.Animator;
            bool winding = enemy.WindingUp;
            if (animator == null)
            {
                view.WasWinding = winding;
                return;
            }

            if (winding && !view.WasWinding)
            {
                SurvivorEnemyDef def = SurvivorDefaults.EnemyDef(enemy.TypeIndex);
                float windup = def != null && def.WindupSeconds > 0f ? def.WindupSeconds : 0.9f;
                // Spitters lob a bone from range; everyone else swings.
                animator.PlayOneShot(view.Look == LookSpitter ? WalkerThrowState : WalkerAttack, Mathf.Clamp(0.55f / windup, 0.6f, 1.4f), false);
            }
            view.WasWinding = winding;

            view.Claw -= SurvivorSim.FixedDeltaTime;
            if (!winding && view.Look != LookSpitter && view.Claw <= 0f && enemy.StunRemaining <= 0f && sim.Hero.Alive && animator.OneShot < 0)
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
            else if (view.Runs || enemy.TypeIndex == 1)
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
                    float auraSize = enemy.Radius * 2.8f * pulse;
                    view.Aura.localScale = new Vector3(auraSize, 1f, auraSize);
                }

                view.Flash = Mathf.Max(0f, view.Flash - realDelta);
                int tint = view.Flash > 0f ? TintFlash
                    : enemy.WindingUp ? TintWindup
                    : enemy.StunRemaining > 0f ? TintStun
                    : view.Boss ? TintBoss
                    : view.Elite ? TintElite
                    : TintNone;
                ApplyEnemyTint(view, tint);

                bool onScreen = IsOnScreen(position, enemy.Radius, view.HeightScale);
                if (onScreen)
                {
                    visible++;
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
            SetRendererTint(view.Renderers, TintColors[tint], tint != TintNone);
        }

        private void DisposeEnemyAnimators()
        {
            for (int i = 0; i < allEnemyViews.Count; i++)
            {
                allEnemyViews[i].Animator?.Dispose();
            }
            DestroyUnityObject(eliteAuraMaterial);
            DestroyUnityObject(bossAuraMaterial);
            DestroyUnityObject(enemyFallbackMaterial);
        }
    }
}
