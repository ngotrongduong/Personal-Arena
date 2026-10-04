using System.Collections.Generic;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PersonalArena.View
{
    public sealed partial class SurvivorRenderer
    {
        private const float SceneryScale = 0.55f;
        private const float FenceInset = 0.6f;
        private const float OuterRingMin = 53f;
        private const float OuterRingMax = 74f;
        private const float GroundTile = 7f;
        private const int OuterTreeCount = 72;
        private const int OuterDecorCount = 36;
        private const int InnerDecorCount = 45;
        private const int PatchCount = 28;
        private const float MaxTreeHeight = 5.5f;

        private readonly Dictionary<GameObject, Stack<Transform>> obstaclePools = new Dictionary<GameObject, Stack<Transform>>();
        private readonly Dictionary<GameObject, Bounds> footprints = new Dictionary<GameObject, Bounds>();
        private readonly List<ObstacleView> activeObstacles = new List<ObstacleView>(SurvivorSim.ObstacleCapacity);
        private Transform sceneryRoot;
        private Transform obstacleRoot;
        private Material halloweenMaterial;
        private Material obstacleFallbackMaterial;
        private GameObject fallbackObstaclePrefab;

        private struct ObstacleView
        {
            public GameObject Prefab;
            public Transform Root;
        }

        /// <summary>Half the side of the fenced map, in metres (the map is centred on the origin).</summary>
        public const float MapHalfExtent = 50f;

        private void BuildScenery()
        {
            sceneryRoot = CreateChild("Graveyard", transform);
            obstacleRoot = CreateChild("Obstacles", sceneryRoot);

            if (survivorArt != null && survivorArt.HalloweenTexture != null)
            {
                halloweenMaterial = Own(CreateStandard("Halloween Bits", Color.white, 0.1f));
                halloweenMaterial.mainTexture = survivorArt.HalloweenTexture;
            }
            obstacleFallbackMaterial = Own(CreateStandard("Obstacle Fallback", new Color(0.42f, 0.42f, 0.46f), 0.1f));

            float half = MapHalfExtent;
            BuildGround(half);
            if (survivorArt == null)
            {
                return;
            }

            System.Random random = new System.Random(20260930);
            BuildFence(half + FenceInset);
            ScatterRing(survivorArt.OuterTrees, OuterTreeCount, random, 0.9f, 1.5f, true);
            ScatterRing(survivorArt.Decor, OuterDecorCount, random, 0.8f, 1.2f, false);
            ScatterInside(survivorArt.SmallDecor, InnerDecorCount, random, half - 2f, 0.55f, 0.8f, false);
            ScatterInside(survivorArt.GroundPatches, PatchCount, random, half - 3f, 1f, 1.7f, true);
        }

        private void BuildGround(float half)
        {
            Texture2D texture = Own(BuildGroundTexture());
            Material ground = Own(CreateStandard("Graveyard Ground", Color.white, 0f));
            ground.mainTexture = texture;
            // The dark ground would otherwise be washed out to grey by sky reflections and highlights.
            ground.SetFloat("_GlossyReflections", 0f);
            ground.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            ground.SetFloat("_SpecularHighlights", 0f);
            ground.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            float size = half * 2f + 50f;
            ground.mainTextureScale = new Vector2(size / GroundTile, size / GroundTile);

            Mesh quad = Own(BuildLitQuad());
            GameObject groundObject = new GameObject("Ground");
            groundObject.transform.SetParent(sceneryRoot, false);
            groundObject.transform.localScale = new Vector3(size, 1f, size);
            groundObject.AddComponent<MeshFilter>().sharedMesh = quad;
            MeshRenderer groundRenderer = groundObject.AddComponent<MeshRenderer>();
            groundRenderer.sharedMaterial = ground;
            groundRenderer.shadowCastingMode = ShadowCastingMode.Off;
            groundRenderer.receiveShadows = true;

            Material outer = Own(CreateStandard("Outer Ground", new Color(0.05f, 0.06f, 0.055f), 0f));
            GameObject outerObject = new GameObject("Outer Ground");
            outerObject.transform.SetParent(sceneryRoot, false);
            outerObject.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            outerObject.transform.localScale = new Vector3(700f, 1f, 700f);
            outerObject.AddComponent<MeshFilter>().sharedMesh = quad;
            MeshRenderer outerRenderer = outerObject.AddComponent<MeshRenderer>();
            outerRenderer.sharedMaterial = outer;
            outerRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Unit quad on XZ facing up, with normals and tangents so it is lit like any other surface.</summary>
        private static Mesh BuildLitQuad()
        {
            Mesh mesh = new Mesh { name = "Lit Ground Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            Vector4 tangent = new Vector4(1f, 0f, 0f, -1f);
            mesh.tangents = new[] { tangent, tangent, tangent, tangent };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Tileable mossy-grass-and-dirt texture from periodic value noise (no texture asset needed).</summary>
        private static Texture2D BuildGroundTexture()
        {
            const int size = 256;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, true)
            {
                name = "Graveyard Ground",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            Color grass = new Color(0.22f, 0.29f, 0.17f);
            Color darkGrass = new Color(0.13f, 0.18f, 0.11f);
            Color dirt = new Color(0.33f, 0.27f, 0.19f);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float v = y / (float)size;
                    float broad = PeriodicNoise(u, v, 4) * 0.6f + PeriodicNoise(u, v, 8) * 0.4f;
                    float fine = PeriodicNoise(u, v, 32) * 0.6f + PeriodicNoise(u, v, 64) * 0.4f;
                    Color color = Color.Lerp(darkGrass, grass, Mathf.SmoothStep(0.25f, 0.75f, broad));
                    float dirtAmount = Mathf.SmoothStep(0.58f, 0.78f, PeriodicNoise(u + 0.37f, v + 0.11f, 6));
                    color = Color.Lerp(color, dirt, dirtAmount * 0.85f);
                    color *= 0.82f + 0.36f * fine;
                    pixels[y * size + x] = color;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static float PeriodicNoise(float u, float v, int period)
        {
            float x = Mathf.Repeat(u, 1f) * period;
            float y = Mathf.Repeat(v, 1f) * period;
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Lattice(x0 % period, y0 % period, period);
            float b = Lattice((x0 + 1) % period, y0 % period, period);
            float c = Lattice(x0 % period, (y0 + 1) % period, period);
            float d = Lattice((x0 + 1) % period, (y0 + 1) % period, period);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Lattice(int x, int y, int period)
        {
            return (SurvivorViewLogic.Hash(x + y * 4099, period) & 0xFFFF) / 65535f;
        }

        private void BuildFence(float edge)
        {
            if (survivorArt.Fence == null)
            {
                return;
            }
            Transform fenceRoot = CreateChild("Fence", sceneryRoot);
            Bounds bounds = Footprint(survivorArt.Fence);
            float length = Mathf.Max(0.5f, bounds.size.x) * SceneryScale;
            int segments = Mathf.Max(1, Mathf.RoundToInt(edge * 2f / length));
            float step = edge * 2f / segments;
            float stretch = step / length;
            for (int side = 0; side < 4; side++)
            {
                for (int k = 0; k < segments; k++)
                {
                    float along = -edge + step * (k + 0.5f);
                    Vector3 position;
                    float yaw;
                    switch (side)
                    {
                        case 0: position = new Vector3(along, 0f, edge); yaw = 0f; break;
                        case 1: position = new Vector3(along, 0f, -edge); yaw = 180f; break;
                        case 2: position = new Vector3(edge, 0f, along); yaw = 90f; break;
                        default: position = new Vector3(-edge, 0f, along); yaw = 270f; break;
                    }
                    int id = side * 1000 + k;
                    bool broken = survivorArt.FenceBroken != null && SurvivorViewLogic.Hash(id, 31) % 100u < 12u;
                    GameObject prefab = broken ? survivorArt.FenceBroken : survivorArt.Fence;
                    Transform segment = PlaceScenery(prefab, fenceRoot, position, yaw, SceneryScale, false);
                    segment.localScale = new Vector3(SceneryScale * stretch, SceneryScale, SceneryScale);

                    if (survivorArt.FencePillar != null && k % 2 == 0)
                    {
                        Vector3 joint = position;
                        if (side < 2)
                        {
                            joint.x -= step * 0.5f;
                        }
                        else
                        {
                            joint.z -= step * 0.5f;
                        }
                        PlaceScenery(survivorArt.FencePillar, fenceRoot, joint, yaw, SceneryScale, false);
                    }
                }
            }
        }

        private void ScatterRing(GameObject[] prefabs, int count, System.Random random, float minScale, float maxScale, bool castShadows)
        {
            if (prefabs == null || prefabs.Length == 0)
            {
                return;
            }
            Transform root = CreateChild("Outer Scenery", sceneryRoot);
            for (int i = 0; i < count; i++)
            {
                GameObject prefab = prefabs[random.Next(prefabs.Length)];
                if (prefab == null)
                {
                    continue;
                }
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Lerp(OuterRingMin, OuterRingMax, (float)random.NextDouble());
                // Push points onto the square ring outside the fence rather than a circle, so corners are filled too.
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float squareFactor = 1f / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.z));
                Vector3 position = direction * (radius * Mathf.Min(squareFactor, 1.35f));
                float scale = SceneryScale * Mathf.Lerp(minScale, maxScale, (float)random.NextDouble());
                PlaceScenery(prefab, root, position, (float)random.NextDouble() * 360f, scale, castShadows);
            }
        }

        private void ScatterInside(GameObject[] prefabs, int count, System.Random random, float extent, float minScale, float maxScale, bool flat)
        {
            if (prefabs == null || prefabs.Length == 0)
            {
                return;
            }
            Transform root = CreateChild(flat ? "Ground Patches" : "Small Decor", sceneryRoot);
            for (int i = 0; i < count; i++)
            {
                GameObject prefab = prefabs[random.Next(prefabs.Length)];
                if (prefab == null)
                {
                    continue;
                }
                Vector3 position = new Vector3(
                    Mathf.Lerp(-extent, extent, (float)random.NextDouble()), 0f,
                    Mathf.Lerp(-extent, extent, (float)random.NextDouble()));
                if (position.sqrMagnitude < 36f)
                {
                    // Keep the spawn point clean.
                    position *= 2.5f;
                }
                float scale = SceneryScale * Mathf.Lerp(minScale, maxScale, (float)random.NextDouble());
                Transform placed = PlaceScenery(prefab, root, position, (float)random.NextDouble() * 360f, scale, false);
                if (flat)
                {
                    // Sink the dirt tile so only its top surface shows, just above the ground plane.
                    Bounds bounds = Footprint(prefab);
                    placed.localPosition = new Vector3(position.x, 0.006f - bounds.max.y * scale, position.z);
                }
            }
        }

        /// <summary>Instantiates a scenery prop standing on the ground (its lowest point at y = 0).</summary>
        private Transform PlaceScenery(GameObject prefab, Transform parent, Vector3 position, float yaw, float scale, bool castShadows)
        {
            GameObject instance = Instantiate(prefab, parent, false);
            instance.name = prefab.name;
            Bounds bounds = Footprint(prefab);
            Transform placed = instance.transform;
            placed.localPosition = new Vector3(position.x, -bounds.min.y * scale, position.z);
            placed.localRotation = Quaternion.Euler(0f, yaw, 0f);
            placed.localScale = Vector3.one * scale;
            PrepareRenderers(instance, IsHalloween(prefab), castShadows);
            return placed;
        }

        private bool IsHalloween(GameObject prefab)
        {
            if (survivorArt == null || prefab == null)
            {
                return false;
            }
            if (prefab == survivorArt.Chest || prefab == survivorArt.HammerProp || prefab == survivorArt.AxeProp)
            {
                return false;
            }
            for (int i = 0; i < survivorArt.Rocks.Length; i++)
            {
                if (survivorArt.Rocks[i] == prefab)
                {
                    return false;
                }
            }
            return true;
        }

        private void PrepareRenderers(GameObject instance, bool halloween, bool castShadows)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (halloween && halloweenMaterial != null)
                {
                    renderers[i].sharedMaterial = halloweenMaterial;
                }
                renderers[i].shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
        }

        /// <summary>Local-space mesh bounds of a prefab at unit scale (cached; works on inactive objects).</summary>
        private Bounds Footprint(GameObject prefab)
        {
            if (footprints.TryGetValue(prefab, out Bounds cached))
            {
                return cached;
            }

            Transform root = prefab.transform;
            Matrix4x4 rootInverse = Matrix4x4.TRS(root.localPosition, root.localRotation, root.localScale).inverse;
            bool any = false;
            Bounds result = new Bounds(Vector3.zero, Vector3.zero);
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                if (mesh == null)
                {
                    continue;
                }
                Matrix4x4 toRoot = rootInverse * filters[i].transform.localToWorldMatrix;
                EncapsulateBounds(ref result, ref any, mesh.bounds, toRoot);
            }
            SkinnedMeshRenderer[] skinned = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinned.Length; i++)
            {
                if (skinned[i].sharedMesh != null)
                {
                    Matrix4x4 toRoot = rootInverse * skinned[i].transform.localToWorldMatrix;
                    EncapsulateBounds(ref result, ref any, skinned[i].sharedMesh.bounds, toRoot);
                }
            }
            if (!any)
            {
                result = new Bounds(new Vector3(0f, 0.5f, 0f), Vector3.one);
            }
            footprints[prefab] = result;
            return result;
        }

        private static void EncapsulateBounds(ref Bounds result, ref bool any, Bounds local, Matrix4x4 matrix)
        {
            Vector3 min = local.min;
            Vector3 max = local.max;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                Vector3 transformed = matrix.MultiplyPoint3x4(point);
                if (!any)
                {
                    result = new Bounds(transformed, Vector3.zero);
                    any = true;
                }
                else
                {
                    result.Encapsulate(transformed);
                }
            }
        }

        // ------------------------------------------------------------------ obstacles

        /// <summary>Redraws the run's obstacles (they change with the seed) from per-model pools.</summary>
        private void RebuildObstacles()
        {
            for (int i = 0; i < activeObstacles.Count; i++)
            {
                ObstacleView old = activeObstacles[i];
                old.Root.gameObject.SetActive(false);
                obstaclePools[old.Prefab].Push(old.Root);
            }
            activeObstacles.Clear();
            if (sim == null)
            {
                return;
            }

            IReadOnlyList<SurvivorObstacle> obstacles = sim.Obstacles;
            for (int i = 0; i < obstacles.Count; i++)
            {
                SurvivorObstacle obstacle = obstacles[i];
                if (!obstacle.Active)
                {
                    continue;
                }

                ObstacleKind kind = SurvivorViewLogic.ObstacleKindFor(i, obstacle.Radius);
                GameObject[] models = ModelsFor(kind);
                if (models.Length == 0)
                {
                    kind = ObstacleKind.Rock;
                    models = FirstNonEmpty();
                }
                int modelIndex = SurvivorViewLogic.ObstacleModelIndex(i, models.Length, (int)kind);
                GameObject prefab = modelIndex >= 0 ? models[modelIndex] : null;
                if (prefab == null)
                {
                    prefab = FallbackObstacle();
                }

                Transform root = AcquireObstacle(prefab);
                Transform model = root.GetChild(0);
                Transform shadow = root.GetChild(1);
                Bounds bounds = Footprint(prefab);
                float width = Mathf.Max(0.1f, Mathf.Max(bounds.size.x, bounds.size.z));
                float targetWidth = obstacle.Radius * 2f * (kind == ObstacleKind.Tree ? 1.8f : kind == ObstacleKind.Rock ? 1.2f : 1.15f);
                float scale = targetWidth / width;
                if (kind == ObstacleKind.Tree)
                {
                    scale = Mathf.Min(scale, MaxTreeHeight / Mathf.Max(0.1f, bounds.size.y));
                }
                else
                {
                    scale = Mathf.Min(scale, 3.2f / Mathf.Max(0.1f, bounds.size.y));
                }

                model.localScale = Vector3.one * scale;
                model.localPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);
                float shadowSize = obstacle.Radius * 2.5f;
                shadow.localScale = new Vector3(shadowSize, 1f, shadowSize);

                root.SetPositionAndRotation(ArenaSpace.ToWorld(obstacle.Position), Quaternion.Euler(0f, SurvivorViewLogic.ObstacleYaw(i), 0f));
                root.gameObject.SetActive(true);
                activeObstacles.Add(new ObstacleView { Prefab = prefab, Root = root });
            }
        }

        private GameObject[] ModelsFor(ObstacleKind kind)
        {
            if (survivorArt == null)
            {
                return System.Array.Empty<GameObject>();
            }
            switch (kind)
            {
                case ObstacleKind.Grave: return survivorArt.Graves ?? System.Array.Empty<GameObject>();
                case ObstacleKind.Tree: return survivorArt.Trees ?? System.Array.Empty<GameObject>();
                default: return survivorArt.Rocks ?? System.Array.Empty<GameObject>();
            }
        }

        private GameObject[] FirstNonEmpty()
        {
            if (survivorArt == null)
            {
                return System.Array.Empty<GameObject>();
            }
            if (survivorArt.Rocks != null && survivorArt.Rocks.Length > 0)
            {
                return survivorArt.Rocks;
            }
            if (survivorArt.Graves != null && survivorArt.Graves.Length > 0)
            {
                return survivorArt.Graves;
            }
            return survivorArt.Trees ?? System.Array.Empty<GameObject>();
        }

        private GameObject FallbackObstacle()
        {
            if (fallbackObstaclePrefab == null)
            {
                fallbackObstaclePrefab = CreatePrimitive("Obstacle Fallback", PrimitiveType.Cylinder, sceneryRoot, obstacleFallbackMaterial);
                fallbackObstaclePrefab.SetActive(false);
            }
            return fallbackObstaclePrefab;
        }

        private Transform AcquireObstacle(GameObject prefab)
        {
            if (!obstaclePools.TryGetValue(prefab, out Stack<Transform> pool))
            {
                pool = new Stack<Transform>(8);
                obstaclePools[prefab] = pool;
            }
            if (pool.Count > 0)
            {
                return pool.Pop();
            }

            GameObject root = new GameObject("Obstacle " + prefab.name);
            root.transform.SetParent(obstacleRoot, false);
            GameObject model = Instantiate(prefab, root.transform, false);
            model.name = prefab.name;
            model.SetActive(true);
            model.transform.localRotation = Quaternion.identity;
            PrepareRenderers(model, prefab != fallbackObstaclePrefab && IsHalloween(prefab), true);
            CreateBlobShadow(root.transform, 1f);
            return root.transform;
        }
    }
}
