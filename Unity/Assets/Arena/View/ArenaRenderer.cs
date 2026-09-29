using System.Collections.Generic;
using PersonalArena.Core;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// Interpolated presentation of an externally stepped ArenaSim. Uses the optional <see cref="ArenaArtSet"/>
    /// (rigged characters, animations, dungeon pieces) and falls back to procedural primitives without one.
    /// </summary>
    public sealed class ArenaRenderer : MonoBehaviour
    {
        private const float HeroHeight = 0.72f;
        private const float ZombieHeight = 0.68f;
        private const float CorpseSeconds = 2.6f;

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

        [SerializeField] private ArenaArtSet artSet;

        private readonly List<ZombieView> zombieViews = new List<ZombieView>();
        private readonly List<Light> torchLights = new List<Light>();
        private MaterialPropertyBlock propertyBlock;

        private ArenaSim sim;
        private Transform environmentRoot;
        private Transform actorsRoot;
        private Transform heroRoot;
        private Transform heroBody;
        private Transform shield;
        private Renderer[] heroRenderers;
        private CharacterAnimator heroAnimator;
        private float heroBodyScale = 1f;
        private Material floorMaterial;
        private Material wallMaterial;
        private Material heroMaterial;
        private Material spearMaterial;
        private Material shieldMaterial;
        private Material zombieMaterial;
        private Material zombieHeadMaterial;
        private Material facingMaterial;
        private Material respawnMaterial;
        private Texture2D checkerTexture;

        private ActorSnapshot previousHero;
        private ActorSnapshot currentHero;
        private ActorSnapshot[] previousZombies;
        private ActorSnapshot[] currentZombies;
        private float interpolationAlpha = 1f;
        private float lastSyncedTime;
        private float heroFlashRemaining;
        private float heroPopRemaining;
        private bool heroWasAlive;
        private int strikeCount;
        private float animationClock;

        public ArenaSim Sim => sim;

        private bool UsesCharacterArt => artSet != null && artSet.HasCharacters;

        public void Bind(ArenaSim arenaSim)
        {
            sim = arenaSim;
            EnsureMaterials();
            BuildEnvironment();
            EnsureHeroView();

            int count = sim != null ? sim.Zombies.Count : 0;
            EnsureZombieViews(count);
            heroFlashRemaining = 0f;
            heroPopRemaining = 0f;
            for (int i = 0; i < zombieViews.Count; i++)
            {
                zombieViews[i].FlashRemaining = 0f;
                zombieViews[i].PopRemaining = 0f;
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
            FlickerTorches();
            Present();
        }

        private void Present()
        {
            if (sim == null || heroRoot == null || currentZombies == null)
            {
                return;
            }

            Vector3 heroPosition = Vector3.Lerp(previousHero.Position, currentHero.Position, interpolationAlpha);
            float heroYaw = Mathf.LerpAngle(previousHero.Yaw, currentHero.Yaw, interpolationAlpha);
            heroRoot.SetPositionAndRotation(heroPosition, Quaternion.Euler(0f, heroYaw, 0f));
            // Rigged heroes stay visible to play their death animation.
            heroRoot.gameObject.SetActive(currentHero.Alive || heroAnimator != null);
            if (shield != null)
            {
                shield.gameObject.SetActive(sim.Hero.IsBlocking && currentHero.Alive);
            }
            float heroPop = heroPopRemaining > 0f ? 1f + 0.18f * (heroPopRemaining / 0.2f) : 1f;
            heroBody.localScale = Vector3.one * (heroPop * heroBodyScale);
            Color heroFlash = heroAnimator != null ? new Color(1.6f, 0.55f, 0.55f) : new Color(1f, 0.2f, 0.2f);
            SetRendererTint(heroRenderers, heroFlash, heroFlashRemaining > 0f);

            int count = sim.Zombies.Count;
            for (int i = 0; i < zombieViews.Count; i++)
            {
                ZombieView view = zombieViews[i];
                if (i >= count)
                {
                    view.Root.gameObject.SetActive(false);
                    continue;
                }

                ZombieState zombie = sim.Zombies[i];
                ActorSnapshot from = previousZombies[i];
                ActorSnapshot to = currentZombies[i];
                view.Root.gameObject.SetActive(true);
                view.Root.SetPositionAndRotation(
                    Vector3.Lerp(from.Position, to.Position, interpolationAlpha),
                    Quaternion.Euler(0f, Mathf.LerpAngle(from.Yaw, to.Yaw, interpolationAlpha), 0f));

                bool rigged = view.Animator != null;
                bool showCorpse = rigged
                    ? !zombie.Alive && view.DeadSeconds < CorpseSeconds
                    : !zombie.Alive && view.PopRemaining > 0f;
                view.BodyRoot.gameObject.SetActive(zombie.Alive || showCorpse);
                bool respawning = !rigged && !zombie.Alive && zombie.RespawnRemaining > 0f;
                view.RespawnMarker.gameObject.SetActive(respawning);
                if (respawning)
                {
                    float progress = 1f - Mathf.Clamp01(zombie.RespawnRemaining / 1.5f);
                    view.RespawnMarker.localScale = new Vector3(0.4f + progress, 0.03f, 0.4f + progress);
                }

                // Corpses sink into the floor before disappearing.
                float sink = rigged && !zombie.Alive ? Mathf.Max(0f, view.DeadSeconds - 1.7f) * 0.9f : 0f;
                view.BodyRoot.localPosition = new Vector3(0f, -sink, 0f);
                float pop = view.PopRemaining > 0f ? 1f + (rigged ? 0.12f : 0.28f) * (view.PopRemaining / 0.2f) : 1f;
                view.BodyRoot.localScale = Vector3.one * (pop * view.BodyScale);
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
        }

        private void ReadFeedbackEvents()
        {
            for (int i = 0; i < sim.Events.Count; i++)
            {
                SimEvent simEvent = sim.Events[i];
                if (simEvent.Type == SimEventType.HeroDamaged || simEvent.Type == SimEventType.HeroDamagedFromBehind)
                {
                    heroFlashRemaining = 0.14f;
                    if (heroAnimator != null && !sim.Hero.IsBlocking && heroAnimator.OneShot < 0)
                    {
                        heroAnimator.PlayOneShot(HeroHit, 1.4f, false);
                    }
                }
                else if (simEvent.Type == SimEventType.Parry)
                {
                    heroPopRemaining = 0.2f;
                    PopZombie(simEvent.ZombieId);
                }
                else if (simEvent.Type == SimEventType.HeroDealtDamage)
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
                }
                else if (simEvent.Type == SimEventType.Backstab || simEvent.Type == SimEventType.ZombieKilled)
                {
                    PopZombie(simEvent.ZombieId);
                }
                else if (simEvent.Type == SimEventType.SkillUsed && heroAnimator != null)
                {
                    PlayHeroSkill((int)simEvent.Value - 1);
                }
            }
        }

        private void PlayHeroSkill(int slot)
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
                    heroAnimator.PlayOneShot(strikeCount % 2 == 0 ? HeroStrikeA : HeroStrikeB, 1.9f, false);
                    break;
                case SkillKind.Kick:
                    heroAnimator.PlayOneShot(HeroKick, 1.5f, false);
                    break;
                case SkillKind.Dash:
                    heroAnimator.PlayOneShot(HeroDash, 1.2f, false);
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
                    if (hero.IsBlocking)
                    {
                        heroAnimator.SetBase(HeroBlock, 1f);
                    }
                    else if (speed > 0.3f)
                    {
                        heroAnimator.SetBase(HeroRun, Mathf.Clamp(speed / sim.HeroDef.MoveSpeed, 0.5f, 1.3f) * 1.15f);
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
                        view.Animator.PlayOneShot(WalkerDeath, 1.5f, true);
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
                    if (stunned)
                    {
                        view.Animator.SetBase(WalkerIdle, 0.35f);
                    }
                    else if (speed > 0.2f)
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

        private void FlickerTorches()
        {
            float time = Time.unscaledTime;
            for (int i = 0; i < torchLights.Count; i++)
            {
                if (torchLights[i] != null)
                {
                    torchLights[i].intensity = 1.6f * (0.82f + 0.3f * Mathf.PerlinNoise(i * 7.31f, time * 3.2f));
                }
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

        private ZombieView FindZombieView(int zombieId)
        {
            if (sim == null)
            {
                return null;
            }

            for (int i = 0; i < sim.Zombies.Count; i++)
            {
                if (sim.Zombies[i].Id == zombieId)
                {
                    return i < zombieViews.Count ? zombieViews[i] : null;
                }
            }
            return null;
        }

        private void BuildEnvironment()
        {
            if (environmentRoot != null)
            {
                DestroyUnityObject(environmentRoot.gameObject);
            }
            torchLights.Clear();

            GameObject root = new GameObject("Environment");
            root.transform.SetParent(transform, false);
            environmentRoot = root.transform;
            if (sim == null)
            {
                return;
            }

            float width = sim.Config.Width;
            float height = sim.Config.Height;
            if (artSet != null && artSet.HasDungeon)
            {
                DungeonDressing.Build(artSet, environmentRoot, width, height, torchLights);
                QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 8);
                return;
            }

            GameObject floor = CreatePrimitive("Floor", PrimitiveType.Cube, environmentRoot, floorMaterial);
            floor.transform.position = new Vector3(width * 0.5f, -0.1f, height * 0.5f);
            floor.transform.localScale = new Vector3(width, 0.2f, height);
            floorMaterial.mainTextureScale = new Vector2(Mathf.Max(1f, width * 0.5f), Mathf.Max(1f, height * 0.5f));

            const float wallThickness = 0.35f;
            const float wallHeight = 0.75f;
            CreateWall("Wall South", new Vector3(width * 0.5f, wallHeight * 0.5f, -wallThickness * 0.5f), new Vector3(width + wallThickness * 2f, wallHeight, wallThickness));
            CreateWall("Wall North", new Vector3(width * 0.5f, wallHeight * 0.5f, height + wallThickness * 0.5f), new Vector3(width + wallThickness * 2f, wallHeight, wallThickness));
            CreateWall("Wall West", new Vector3(-wallThickness * 0.5f, wallHeight * 0.5f, height * 0.5f), new Vector3(wallThickness, wallHeight, height));
            CreateWall("Wall East", new Vector3(width + wallThickness * 0.5f, wallHeight * 0.5f, height * 0.5f), new Vector3(wallThickness, wallHeight, height));
        }

        private void CreateWall(string name, Vector3 position, Vector3 scale)
        {
            GameObject wall = CreatePrimitive(name, PrimitiveType.Cube, environmentRoot, wallMaterial);
            wall.transform.position = position;
            wall.transform.localScale = scale;
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
                heroRenderers = bodyRootObject.GetComponentsInChildren<Renderer>(true);
                return;
            }

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
            heroRenderers = bodyRootObject.GetComponentsInChildren<Renderer>(true);
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

                GameObject marker = CreatePrimitive("Respawning", PrimitiveType.Cylinder, rootObject.transform, respawnMaterial);
                marker.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                marker.transform.localScale = new Vector3(0.5f, 0.03f, 0.5f);
                marker.SetActive(false);

                view.RespawnMarker = marker.transform;
                view.Renderers = bodyRootObject.GetComponentsInChildren<Renderer>(true);
                zombieViews.Add(view);
            }

            for (int i = count; i < zombieViews.Count; i++)
            {
                zombieViews[i].Root.gameObject.SetActive(false);
            }
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
            if (floorMaterial != null)
            {
                return;
            }

            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("Personal Arena requires the built-in Standard shader.");
                return;
            }

            floorMaterial = CreateMaterial(shader, new Color(0.16f, 0.18f, 0.2f), "Arena Floor");
            checkerTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "Arena Checker",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            Color dark = new Color(0.16f, 0.18f, 0.2f);
            Color light = new Color(0.2f, 0.22f, 0.24f);
            checkerTexture.SetPixels(new[] { dark, light, light, dark });
            checkerTexture.Apply(false, true);
            floorMaterial.mainTexture = checkerTexture;
            wallMaterial = CreateMaterial(shader, new Color(0.25f, 0.27f, 0.3f), "Arena Walls");
            heroMaterial = CreateMaterial(shader, new Color(0.22f, 0.48f, 0.85f), "Warrior Blue");
            spearMaterial = CreateMaterial(shader, new Color(0.68f, 0.7f, 0.74f), "Spear Steel");
            shieldMaterial = CreateMaterial(shader, new Color(0.15f, 0.3f, 0.58f), "Shield Blue");
            zombieMaterial = CreateMaterial(shader, new Color(0.35f, 0.62f, 0.3f), "Walker Green");
            zombieHeadMaterial = CreateMaterial(shader, new Color(0.18f, 0.36f, 0.16f), "Walker Head");
            facingMaterial = CreateMaterial(shader, new Color(0.1f, 0.18f, 0.08f), "Walker Facing");
            respawnMaterial = CreateMaterial(shader, new Color(0.35f, 0.85f, 0.55f), "Respawn Marker");
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
            DestroyUnityObject(floorMaterial);
            DestroyUnityObject(wallMaterial);
            DestroyUnityObject(heroMaterial);
            DestroyUnityObject(spearMaterial);
            DestroyUnityObject(shieldMaterial);
            DestroyUnityObject(zombieMaterial);
            DestroyUnityObject(zombieHeadMaterial);
            DestroyUnityObject(facingMaterial);
            DestroyUnityObject(respawnMaterial);
            DestroyUnityObject(checkerTexture);
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
            public Transform RespawnMarker;
            public Renderer[] Renderers;
            public CharacterAnimator Animator;
            public float BodyScale = 1f;
            public float FlashRemaining;
            public float PopRemaining;
            public float DeadSeconds;
            public bool WasAlive;
            public bool WasStunned;
            public ZombieAttackPhase LastPhase;
        }
    }
}
