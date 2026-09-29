using System.Collections.Generic;
using PersonalArena.Core;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>Procedural, interpolated presentation of an externally stepped ArenaSim.</summary>
    public sealed class ArenaRenderer : MonoBehaviour
    {
        private const float HeroHeight = 0.72f;
        private const float ZombieHeight = 0.68f;

        private readonly List<ZombieView> zombieViews = new List<ZombieView>();
        private MaterialPropertyBlock propertyBlock;

        private ArenaSim sim;
        private Transform environmentRoot;
        private Transform actorsRoot;
        private Transform heroRoot;
        private Transform heroBody;
        private Transform shield;
        private Renderer[] heroRenderers;
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

        public ArenaSim Sim => sim;

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
            for (int i = 0; i < count; i++)
            {
                ZombieState zombie = sim.Zombies[i];
                currentZombies[i] = Snapshot(zombie.Position, zombie.Facing, zombie.Alive);
                previousZombies[i] = currentZombies[i];
            }

            interpolationAlpha = 1f;
            lastSyncedTime = sim.Time;
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
            heroRoot.gameObject.SetActive(currentHero.Alive);
            shield.gameObject.SetActive(sim.Hero.IsBlocking && currentHero.Alive);
            float heroPop = heroPopRemaining > 0f ? 1f + 0.18f * (heroPopRemaining / 0.2f) : 1f;
            heroBody.localScale = new Vector3(heroPop, heroPop, heroPop);
            SetRendererTint(heroRenderers, new Color(1f, 0.2f, 0.2f), heroFlashRemaining > 0f);

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

                bool showCorpsePop = !zombie.Alive && view.PopRemaining > 0f;
                view.BodyRoot.gameObject.SetActive(zombie.Alive || showCorpsePop);
                view.RespawnMarker.gameObject.SetActive(!zombie.Alive && zombie.RespawnRemaining > 0f);
                if (!zombie.Alive && zombie.RespawnRemaining > 0f)
                {
                    float progress = 1f - Mathf.Clamp01(zombie.RespawnRemaining / 1.5f);
                    view.RespawnMarker.localScale = new Vector3(0.4f + progress, 0.03f, 0.4f + progress);
                }

                float pop = view.PopRemaining > 0f ? 1f + 0.28f * (view.PopRemaining / 0.2f) : 1f;
                view.BodyRoot.localScale = new Vector3(pop, pop, pop);
                Color tint = Color.white;
                bool overrideTint = true;
                if (view.FlashRemaining <= 0f)
                {
                    if (zombie.StunRemaining > 0f)
                    {
                        tint = new Color(0.35f, 0.65f, 1f);
                    }
                    else if (zombie.AttackPhase == ZombieAttackPhase.Windup)
                    {
                        tint = new Color(1f, 0.82f, 0.22f);
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
                if (simEvent.Type == SimEventType.HeroDamaged)
                {
                    heroFlashRemaining = 0.14f;
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
                    }
                }
                else if (simEvent.Type == SimEventType.Backstab || simEvent.Type == SimEventType.ZombieKilled)
                {
                    PopZombie(simEvent.ZombieId);
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

            GameObject root = new GameObject("Environment");
            root.transform.SetParent(transform, false);
            environmentRoot = root.transform;
            if (sim == null)
            {
                return;
            }

            float width = sim.Config.Width;
            float height = sim.Config.Height;
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
                GameObject body = CreatePrimitive("Body", PrimitiveType.Capsule, bodyRootObject.transform, zombieMaterial);
                body.transform.localPosition = new Vector3(0f, ZombieHeight, 0f);
                body.transform.localScale = new Vector3(0.86f, ZombieHeight, 0.86f);

                GameObject head = CreatePrimitive("Head", PrimitiveType.Sphere, bodyRootObject.transform, zombieHeadMaterial);
                head.transform.localPosition = new Vector3(0f, 1.45f, 0f);
                head.transform.localScale = new Vector3(0.62f, 0.5f, 0.62f);

                GameObject facing = CreatePrimitive("Facing", PrimitiveType.Cube, bodyRootObject.transform, facingMaterial);
                facing.transform.localPosition = new Vector3(0f, 0.72f, 0.55f);
                facing.transform.localScale = new Vector3(0.16f, 0.16f, 0.55f);

                GameObject marker = CreatePrimitive("Respawning", PrimitiveType.Cylinder, rootObject.transform, respawnMaterial);
                marker.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                marker.transform.localScale = new Vector3(0.5f, 0.03f, 0.5f);
                marker.SetActive(false);

                zombieViews.Add(new ZombieView
                {
                    Root = rootObject.transform,
                    BodyRoot = bodyRootObject.transform,
                    RespawnMarker = marker.transform,
                    Renderers = bodyRootObject.GetComponentsInChildren<Renderer>(true)
                });
            }

            for (int i = count; i < zombieViews.Count; i++)
            {
                zombieViews[i].Root.gameObject.SetActive(false);
            }
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
            public float FlashRemaining;
            public float PopRemaining;
        }
    }
}
