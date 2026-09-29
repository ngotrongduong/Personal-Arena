using System.Collections.Generic;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>Lays out modular dungeon pieces around an arena of the given size (world units, origin at a corner).</summary>
    public static class DungeonDressing
    {
        // Native size of the KayKit modular grid (floor tiles and wall segments are 4x4).
        private const float ModuleSize = 4f;
        private const float OuterMarginX = 14f;
        private const float OuterMarginSouth = 8f;
        private const float OuterMarginNorth = 10f;
        private static readonly Color TorchColor = new Color(1f, 0.62f, 0.3f);
        // The pack textures are pale; darken the floor so characters and torch light stand out.
        private static readonly Color InnerFloorTint = new Color(0.78f, 0.76f, 0.74f);
        private static readonly Color OuterFloorTint = new Color(0.36f, 0.32f, 0.3f);

        public static void Build(ArenaArtSet art, Transform parent, float width, float height, List<Light> torchLights)
        {
            System.Random random = new System.Random(1234);
            float s = art.DungeonScale;
            float tile = ModuleSize * s;

            BuildFloor(art, parent, width, height, tile, s, random);
            BuildWalls(art, parent, width, height, tile, s, random, torchLights);
            BuildProps(art, parent, width, height, s, random);
        }

        private static void BuildFloor(ArenaArtSet art, Transform parent, float width, float height, float tile, float s, System.Random random)
        {
            Transform floor = Group("Floor", parent);
            int columns = Mathf.Max(1, Mathf.CeilToInt(width / tile - 0.01f));
            int rows = Mathf.Max(1, Mathf.CeilToInt(height / tile - 0.01f));
            float cellX = width / columns;
            float cellZ = height / rows;
            Vector3 scale = new Vector3(s * cellX / tile, s, s * cellZ / tile);
            for (int x = 0; x < columns; x++)
            {
                for (int z = 0; z < rows; z++)
                {
                    Vector3 position = new Vector3((x + 0.5f) * cellX, 0f, (z + 0.5f) * cellZ);
                    Tint(Place(art.FloorTile, floor, position, 90f * random.Next(4), scale), InnerFloorTint);
                }
            }

            GameObject outer = art.OuterFloorTile != null ? art.OuterFloorTile : art.FloorTile;
            int minX = Mathf.FloorToInt(-OuterMarginX / tile);
            int maxX = Mathf.CeilToInt((width + OuterMarginX) / tile);
            int minZ = Mathf.FloorToInt(-OuterMarginSouth / tile);
            int maxZ = Mathf.CeilToInt((height + OuterMarginNorth) / tile);
            Vector3 outerScale = new Vector3(s, s, s);
            for (int x = minX; x < maxX; x++)
            {
                for (int z = minZ; z < maxZ; z++)
                {
                    Vector3 position = new Vector3((x + 0.5f) * tile, -0.03f, (z + 0.5f) * tile);
                    bool inside = position.x > 0f && position.x < width && position.z > 0f && position.z < height;
                    if (!inside)
                    {
                        Tint(Place(outer, floor, position, 90f * random.Next(4), outerScale), OuterFloorTint);
                    }
                }
            }
        }

        private static void BuildWalls(ArenaArtSet art, Transform parent, float width, float height, float tile, float s,
            System.Random random, List<Light> torchLights)
        {
            Transform walls = Group("Walls", parent);
            float half = 0.5f * s;

            // Yaw turns the piece's front (+Z) toward the arena interior.
            WallRun(art, walls, new Vector3(0f, 0f, height + half), Vector3.right, width, 180f, tile, s, random, torchLights, true);
            WallRun(art, walls, new Vector3(-half, 0f, 0f), Vector3.forward, height, 90f, tile, s, random, torchLights, false);
            WallRun(art, walls, new Vector3(width + half, 0f, 0f), Vector3.forward, height, -90f, tile, s, random, torchLights, false);

            GameObject barrier = art.LowBarrier != null ? art.LowBarrier : art.Wall;
            int southSegments = Mathf.Max(1, Mathf.CeilToInt(width / tile - 0.01f));
            float southLength = width / southSegments;
            for (int i = 0; i < southSegments; i++)
            {
                Place(barrier, walls, new Vector3((i + 0.5f) * southLength, 0f, -half), 0f,
                    new Vector3(s * southLength / tile, s, s));
            }

            if (art.Pillar != null)
            {
                Vector3 pillarScale = new Vector3(s, s, s);
                Place(art.Pillar, walls, new Vector3(-half, 0f, height + half), 0f, pillarScale);
                Place(art.Pillar, walls, new Vector3(width + half, 0f, height + half), 0f, pillarScale);
                Vector3 lowPillar = new Vector3(s, s * 0.35f, s);
                Place(art.Pillar, walls, new Vector3(-half, 0f, -half), 0f, lowPillar);
                Place(art.Pillar, walls, new Vector3(width + half, 0f, -half), 0f, lowPillar);
            }
        }

        private static void WallRun(ArenaArtSet art, Transform parent, Vector3 start, Vector3 along, float length, float yaw,
            float tile, float s, System.Random random, List<Light> torchLights, bool decorated)
        {
            int segments = Mathf.Max(1, Mathf.CeilToInt(length / tile - 0.01f));
            float segment = length / segments;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 inward = rotation * Vector3.forward;
            Vector3 scale = new Vector3(s * segment / tile, s, s);
            for (int i = 0; i < segments; i++)
            {
                Vector3 position = start + along * ((i + 0.5f) * segment);
                GameObject piece = art.Wall;
                if (decorated && i == segments / 2 && art.WallDoorway != null)
                {
                    piece = art.WallDoorway;
                }
                else if (art.WallVariants.Length > 0 && random.NextDouble() < 0.3)
                {
                    piece = art.WallVariants[random.Next(art.WallVariants.Length)];
                }
                Place(piece, parent, position, yaw, scale);

                if (i % 3 == 1 && art.Torch != null)
                {
                    Vector3 face = position + inward * (0.5f * s);
                    Place(art.Torch, parent, face + Vector3.up * (2.6f * s), yaw, new Vector3(s, s, s));
                    torchLights.Add(CreateTorchLight(parent, face + inward * 0.45f + Vector3.up * (3.1f * s)));
                }
                else if (decorated && i % 3 == 2 && art.Banners.Length > 0)
                {
                    Place(art.Banners[random.Next(art.Banners.Length)], parent, position, yaw, new Vector3(s, s, s));
                }
            }
        }

        private static void BuildProps(ArenaArtSet art, Transform parent, float width, float height, float s, System.Random random)
        {
            if (art.Props.Length == 0)
            {
                return;
            }

            Transform props = Group("Props", parent);
            Vector3 scale = new Vector3(s, s, s);
            const float spacing = 3.4f;
            for (float x = -OuterMarginX + 1.5f; x < width + OuterMarginX - 1f; x += spacing)
            {
                for (float z = -OuterMarginSouth + 1.5f; z < height + OuterMarginNorth - 1f; z += spacing)
                {
                    bool nearArena = x > -2f && x < width + 2f && z > -2.5f && z < height + 2f;
                    if (nearArena || random.NextDouble() > 0.42)
                    {
                        continue;
                    }
                    float jitterX = (float)(random.NextDouble() - 0.5) * 1.2f;
                    float jitterZ = (float)(random.NextDouble() - 0.5) * 1.2f;
                    GameObject prop = art.Props[random.Next(art.Props.Length)];
                    Place(prop, props, new Vector3(x + jitterX, 0f, z + jitterZ), (float)random.NextDouble() * 360f, scale);
                }
            }

            if (art.Pillar != null)
            {
                for (float z = 2f; z < height; z += 6f)
                {
                    Place(art.Pillar, props, new Vector3(-3.2f, 0f, z), 0f, scale);
                    Place(art.Pillar, props, new Vector3(width + 3.2f, 0f, z), 0f, scale);
                }
            }
        }

        private static Light CreateTorchLight(Transform parent, Vector3 position)
        {
            GameObject lightObject = new GameObject("Torch Light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.position = position;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = TorchColor;
            light.range = 7f;
            light.intensity = 2.1f;
            light.shadows = LightShadows.None;
            return light;
        }

        private static Transform Group(string name, Transform parent)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static GameObject Place(GameObject prefab, Transform parent, Vector3 position, float yaw, Vector3 scale)
        {
            if (prefab == null)
            {
                return null;
            }
            GameObject instance = Object.Instantiate(prefab, parent);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            instance.transform.localScale = scale;
            return instance;
        }

        private static void Tint(GameObject instance, Color color)
        {
            if (instance == null)
            {
                return;
            }
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(block);
                block.SetColor("_Color", color);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
