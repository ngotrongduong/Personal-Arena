using System.Collections.Generic;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PersonalArena.View
{
    public sealed partial class SurvivorRenderer
    {
        private const int KeyNone = -1;
        private const int KeyGold = 3;
        private const int KeyMeat = 4;
        private const int KeyChest = 5;
        private const int KeyMagnet = 6;
        private const int KeyMana = 7;
        private const int KeyBomb = 8;
        private const int KeyRage = 9;
        private const int KeyShield = 10;
        private const int KeyHaste = 11;
        private const int PickupKeyCount = 12;
        private const int ProjectileFxPerFrame = 26;
        private const int PickupPrewarm = 160;
        private const int HammerPrewarm = 12;
        private const float PickupHeight = 0.32f;
        private const float HammerHeight = 0.9f;

        private static readonly Color[] GemColors =
        {
            new Color(0.3f, 0.6f, 1f),
            new Color(0.3f, 1f, 0.45f),
            new Color(1f, 0.3f, 0.35f)
        };

        private readonly PickupView[] pickupViews = new PickupView[SurvivorSim.PickupCapacity];
        private readonly HammerView[] hammerViews = new HammerView[SurvivorSim.ProjectileCapacity];
        private readonly Mesh[] pickupMeshes = new Mesh[PickupKeyCount];
        private readonly Material[] pickupMaterials = new Material[PickupKeyCount];
        private readonly Material[] pickupGlowMaterials = new Material[PickupKeyCount];
        private readonly Vector3[] pickupScales = new Vector3[PickupKeyCount];
        private readonly float[] glowSizes = new float[PickupKeyCount];
        // Optional second part of a pickup with its own material: cork of a bottle, tips of the magnet, face of the coin.
        private readonly Mesh[] pickupExtraMeshes = new Mesh[PickupKeyCount];
        private readonly Material[] pickupExtraMaterials = new Material[PickupKeyCount];
        private readonly Vector3[] pickupExtraPositions = new Vector3[PickupKeyCount];
        private readonly Vector3[] pickupExtraScales = new Vector3[PickupKeyCount];
        private readonly List<PickupView> demoPickups = new List<PickupView>();
        private readonly List<HammerView> demoHammers = new List<HammerView>();
        private Mesh gemMesh;
        private Material bombShellMaterial;
        private Material bounceMaterial;
        private Material momentumMaterial;
        private Material orbRingMaterial;
        private Material orbHaloMaterial;
        private Material fireSwirlMaterial;
        private Material bounceStarMaterial;
        private Material momentumGlowMaterial;
        private int projectileFxLeft;
        private int coinSparklesLeft;
        private readonly List<Object> ownedObjects = new List<Object>();
        private Transform pickupRoot;
        private Transform hammerRoot;
        private Material hammerHandleMaterial;
        private Material hammerHeadMaterial;
        private Material boltMaterial;
        private Material boltGlowMaterial;
        private Material fireballMaterial;
        private Material fireballGlowMaterial;
        private Material arrowShaftMaterial;
        private Material arrowHeadMaterial;
        private Material powerShotMaterial;
        private readonly Gradient[] projectileTrails = new Gradient[ProjectileLookCount * 2];
        private float spinClock;

        private sealed class PickupView
        {
            public Transform Root;
            public Transform Model;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Transform Glow;
            public MeshRenderer GlowRenderer;
            public GameObject Chest;
            public Transform Extra;
            public MeshFilter ExtraFilter;
            public MeshRenderer ExtraRenderer;
            public int Key = KeyNone;
            public Vector3 Previous;
            public Vector3 Current;
            public bool Attracted;
            public float Phase;
        }

        private const int ProjectileLookCount = 8;

        private sealed class HammerView
        {
            public Transform Root;
            public Transform Spinner;
            public TrailRenderer Trail;
            public readonly GameObject[] Models = new GameObject[ProjectileLookCount];
            public readonly Transform[] Orbits = new Transform[ProjectileLookCount];
            public float Emit;
            public Renderer[] Renderers;
            public ProjectileLook Look = ProjectileLook.Hammer;
            public bool Evolved;
            public int Id = -1;
            public Vector3 Previous;
            public Vector3 Current;
            public Vector3 Direction = Vector3.forward;
        }

        private void BuildPickups()
        {
            pickupRoot = CreateChild("Pickups", actorsRoot);

            Mesh gem = BuildGemMesh();
            ownedObjects.Add(gem);
            gemMesh = gem;
            // The common small gem is kept small and faint: late in a run hundreds of them cover the ground.
            float[] gemSizes = { 0.27f, 0.5f, 0.7f };
            float[] glowAlphas = { 0.28f, 0.55f, 0.55f };
            for (int size = 0; size < 3; size++)
            {
                Color color = GemColors[size];
                pickupMeshes[size] = gem;
                pickupMaterials[size] = Own(CreateEmissive("Gem " + size, color, color * 0.9f, 0.85f, 0.1f));
                pickupScales[size] = new Vector3(gemSizes[size] * 0.75f, gemSizes[size], gemSizes[size] * 0.75f);
                pickupGlowMaterials[size] = Own(FxAssets.Create("Gem Glow " + size, FxAssets.RadialGlow, true));
                pickupGlowMaterials[size].color = new Color(color.r, color.g, color.b, glowAlphas[size]);
                glowSizes[size] = size == 0 ? 0.65f : 0.9f + 0.45f * size;
            }

            pickupMeshes[KeyGold] = BuiltinMesh(PrimitiveType.Cylinder);
            // A bright coin: little metal (a metal surface with nothing to reflect looks dull) and a strong warm glow.
            pickupMaterials[KeyGold] = Own(CreateEmissive("Gold Coin", new Color(1f, 0.84f, 0.3f), new Color(1.5f, 0.95f, 0.15f), 0.9f, 0.15f));
            pickupScales[KeyGold] = new Vector3(0.5f, 0.045f, 0.5f);
            pickupGlowMaterials[KeyGold] = Own(FxAssets.Create("Gold Glow", FxAssets.RadialGlow, true));
            pickupGlowMaterials[KeyGold].color = new Color(1f, 0.82f, 0.3f, 0.8f);
            glowSizes[KeyGold] = 1.6f;
            pickupExtraMeshes[KeyGold] = pickupMeshes[KeyGold];
            pickupExtraMaterials[KeyGold] = Own(CreateEmissive("Gold Coin Face", new Color(1f, 0.95f, 0.6f), new Color(2.2f, 1.7f, 0.6f), 0.9f, 0.1f));
            pickupExtraScales[KeyGold] = new Vector3(0.68f, 1.5f, 0.68f);

            // Health and mana potions: one flask mesh, red or blue liquid, a cork on top.
            Mesh flask = Own(BuildLatheMesh("Flask", FlaskProfile, 14));
            Mesh cylinder = BuiltinMesh(PrimitiveType.Cylinder);
            Material cork = Own(CreateStandard("Potion Cork", new Color(0.62f, 0.44f, 0.26f), 0.15f));
            pickupMeshes[KeyMeat] = flask;
            pickupMaterials[KeyMeat] = Own(CreateEmissive("Health Potion", new Color(0.95f, 0.14f, 0.2f), new Color(1.1f, 0.1f, 0.14f), 0.92f, 0f));
            pickupScales[KeyMeat] = Vector3.one * 0.85f;
            pickupGlowMaterials[KeyMeat] = Own(FxAssets.Create("Health Potion Glow", FxAssets.RadialGlow, true));
            pickupGlowMaterials[KeyMeat].color = new Color(1f, 0.3f, 0.35f, 0.7f);
            glowSizes[KeyMeat] = 1.6f;
            pickupMeshes[KeyMana] = flask;
            pickupMaterials[KeyMana] = Own(CreateEmissive("Mana Potion", new Color(0.2f, 0.45f, 1f), new Color(0.15f, 0.45f, 1.5f), 0.92f, 0f));
            pickupScales[KeyMana] = Vector3.one * 0.85f;
            pickupGlowMaterials[KeyMana] = Own(FxAssets.Create("Mana Potion Glow", FxAssets.RadialGlow, true));
            pickupGlowMaterials[KeyMana].color = new Color(0.35f, 0.6f, 1f, 0.7f);
            glowSizes[KeyMana] = 1.6f;
            for (int key = KeyMeat; key <= KeyMana; key += KeyMana - KeyMeat)
            {
                pickupExtraMeshes[key] = cylinder;
                pickupExtraMaterials[key] = cork;
                pickupExtraPositions[key] = new Vector3(0f, 0.5f, 0f);
                pickupExtraScales[key] = new Vector3(0.3f, 0.06f, 0.3f);
            }

            pickupMeshes[KeyChest] = BuiltinMesh(PrimitiveType.Cube);
            pickupMaterials[KeyChest] = Own(CreateEmissive("Chest Fallback", new Color(0.55f, 0.35f, 0.15f), new Color(0.15f, 0.08f, 0f), 0.3f, 0.2f));
            pickupScales[KeyChest] = new Vector3(0.9f, 0.6f, 0.6f);
            pickupGlowMaterials[KeyChest] = Own(FxAssets.Create("Chest Glow", FxAssets.RadialGlow, true));
            pickupGlowMaterials[KeyChest].color = new Color(1f, 0.85f, 0.35f, 0.6f);
            glowSizes[KeyChest] = 2.4f;

            // A horseshoe magnet: red body, silver tips.
            pickupMeshes[KeyMagnet] = Own(BuildMagnetMesh(false));
            pickupMaterials[KeyMagnet] = Own(CreateEmissive("Magnet", new Color(0.95f, 0.12f, 0.16f), new Color(0.9f, 0.08f, 0.1f), 0.8f, 0.1f));
            pickupScales[KeyMagnet] = Vector3.one * 1.05f;
            pickupGlowMaterials[KeyMagnet] = Own(FxAssets.Create("Magnet Glow", FxAssets.RadialGlow, true));
            pickupGlowMaterials[KeyMagnet].color = new Color(0.5f, 0.7f, 1f, 0.75f);
            glowSizes[KeyMagnet] = 1.9f;
            pickupExtraMeshes[KeyMagnet] = Own(BuildMagnetMesh(true));
            pickupExtraMaterials[KeyMagnet] = Own(CreateEmissive("Magnet Tips", new Color(0.9f, 0.92f, 0.96f), new Color(0.55f, 0.6f, 0.7f), 0.9f, 0.2f));
            pickupExtraScales[KeyMagnet] = Vector3.one;

            // Short power-ups: a small bomb, a red rage crystal, a blue shield disc, a green haste dart.
            Mesh sphere = BuiltinMesh(PrimitiveType.Sphere);
            pickupMeshes[KeyBomb] = sphere;
            pickupMaterials[KeyBomb] = Own(CreateEmissive("Bomb Pickup", new Color(0.14f, 0.14f, 0.17f), new Color(0.05f, 0.05f, 0.06f), 0.85f, 0.3f));
            pickupScales[KeyBomb] = Vector3.one * 0.55f;
            pickupExtraMeshes[KeyBomb] = sphere;
            pickupExtraMaterials[KeyBomb] = Own(CreateEmissive("Bomb Pickup Ember", new Color(1f, 0.55f, 0.15f), new Color(2.4f, 1f, 0.2f), 0.6f, 0f));
            pickupExtraPositions[KeyBomb] = new Vector3(0.12f, 0.55f, 0f);
            pickupExtraScales[KeyBomb] = Vector3.one * 0.32f;
            SetPickupGlow(KeyBomb, new Color(1f, 0.5f, 0.15f, 0.8f), 1.8f);

            pickupMeshes[KeyRage] = gem;
            pickupMaterials[KeyRage] = Own(CreateEmissive("Rage Crystal", new Color(1f, 0.2f, 0.1f), new Color(2f, 0.35f, 0.1f), 0.9f, 0f));
            pickupScales[KeyRage] = new Vector3(0.5f, 0.8f, 0.5f);
            SetPickupGlow(KeyRage, new Color(1f, 0.3f, 0.1f, 0.85f), 2f);

            pickupMeshes[KeyShield] = cylinder;
            pickupMaterials[KeyShield] = Own(CreateEmissive("Shield Disc", new Color(0.25f, 0.55f, 1f), new Color(0.2f, 0.6f, 1.6f), 0.9f, 0.1f));
            pickupScales[KeyShield] = new Vector3(0.62f, 0.05f, 0.62f);
            pickupExtraMeshes[KeyShield] = cylinder;
            pickupExtraMaterials[KeyShield] = pickupExtraMaterials[KeyMagnet];
            pickupExtraScales[KeyShield] = new Vector3(0.45f, 1.6f, 0.45f);
            SetPickupGlow(KeyShield, new Color(0.35f, 0.7f, 1f, 0.85f), 2f);

            pickupMeshes[KeyHaste] = gem;
            pickupMaterials[KeyHaste] = Own(CreateEmissive("Haste Dart", new Color(0.3f, 1f, 0.45f), new Color(0.3f, 1.8f, 0.5f), 0.9f, 0f));
            pickupScales[KeyHaste] = new Vector3(0.3f, 0.95f, 0.3f);
            SetPickupGlow(KeyHaste, new Color(0.4f, 1f, 0.5f, 0.85f), 2f);

            for (int i = 0; i < PickupPrewarm; i++)
            {
                pickupViews[i] = CreatePickupView(i);
            }
        }

        private void SetPickupGlow(int key, Color color, float size)
        {
            pickupGlowMaterials[key] = Own(FxAssets.Create("Pickup Glow " + key, FxAssets.RadialGlow, true));
            pickupGlowMaterials[key].color = color;
            glowSizes[key] = size;
        }

        private T Own<T>(T asset) where T : Object
        {
            ownedObjects.Add(asset);
            return asset;
        }

        private static Mesh BuiltinMesh(PrimitiveType type)
        {
            GameObject temporary = GameObject.CreatePrimitive(type);
            Mesh mesh = temporary.GetComponent<MeshFilter>().sharedMesh;
            DestroyUnityObject(temporary);
            return mesh;
        }

        /// <summary>Flat-shaded elongated octahedron (unit height), the classic EXP crystal.</summary>
        private static Mesh BuildGemMesh()
        {
            Vector3 top = new Vector3(0f, 0.5f, 0f);
            Vector3 bottom = new Vector3(0f, -0.5f, 0f);
            Vector3[] ring =
            {
                new Vector3(0.5f, 0f, 0f), new Vector3(0f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0f), new Vector3(0f, 0f, -0.5f)
            };
            List<Vector3> vertices = new List<Vector3>(24);
            List<int> triangles = new List<int>(24);
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = ring[i];
                Vector3 b = ring[(i + 1) % 4];
                AddTriangle(vertices, triangles, top, b, a);
                AddTriangle(vertices, triangles, bottom, a, b);
            }
            Mesh mesh = new Mesh { name = "Gem" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Outline of a potion flask from the bottom centre up to the lip: (radius, height).
        private static readonly Vector2[] FlaskProfile =
        {
            new Vector2(0f, 0f), new Vector2(0.3f, 0.02f), new Vector2(0.42f, 0.2f), new Vector2(0.38f, 0.42f),
            new Vector2(0.17f, 0.6f), new Vector2(0.13f, 0.8f), new Vector2(0.2f, 0.84f), new Vector2(0.2f, 0.9f),
            new Vector2(0f, 0.9f)
        };

        /// <summary>Turns an outline (radius, height) around the vertical axis; smooth shaded, centred on its height.</summary>
        private static Mesh BuildLatheMesh(string meshName, Vector2[] profile, int segments)
        {
            float middle = profile[profile.Length - 1].y * 0.5f;
            Vector3[] vertices = new Vector3[profile.Length * segments];
            List<int> triangles = new List<int>((profile.Length - 1) * segments * 6);
            for (int i = 0; i < profile.Length; i++)
            {
                for (int j = 0; j < segments; j++)
                {
                    float angle = j * Mathf.PI * 2f / segments;
                    vertices[i * segments + j] = new Vector3(profile[i].x * Mathf.Cos(angle), profile[i].y - middle, profile[i].x * Mathf.Sin(angle));
                }
            }
            for (int i = 0; i < profile.Length - 1; i++)
            {
                for (int j = 0; j < segments; j++)
                {
                    int next = (j + 1) % segments;
                    int a = i * segments + j;
                    int b = (i + 1) * segments + j;
                    int c = (i + 1) * segments + next;
                    int d = i * segments + next;
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(d);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }
            Mesh mesh = new Mesh { name = meshName };
            mesh.vertices = vertices;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A horseshoe magnet standing with its two poles up: the red U, or (tips) the two silver pole ends.</summary>
        private static Mesh BuildMagnetMesh(bool tips)
        {
            const float radius = 0.26f;
            const float thick = 0.095f;
            List<Vector3> vertices = new List<Vector3>(360);
            List<int> triangles = new List<int>(360);
            if (tips)
            {
                AddBox(vertices, triangles, new Vector3(-radius, 0.27f, 0f), new Vector3(thick + 0.004f, 0.08f, thick + 0.004f), Quaternion.identity);
                AddBox(vertices, triangles, new Vector3(radius, 0.27f, 0f), new Vector3(thick + 0.004f, 0.08f, thick + 0.004f), Quaternion.identity);
            }
            else
            {
                const int bends = 7;
                for (int i = 0; i < bends; i++)
                {
                    float angle = Mathf.PI + (i + 0.5f) * Mathf.PI / bends;
                    Vector3 center = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius - 0.05f, 0f);
                    float half = radius * Mathf.Tan(Mathf.PI / bends * 0.5f) + thick * 0.45f;
                    AddBox(vertices, triangles, center, new Vector3(half, thick, thick), Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 90f));
                }
                AddBox(vertices, triangles, new Vector3(-radius, 0.07f, 0f), new Vector3(thick, 0.125f, thick), Quaternion.identity);
                AddBox(vertices, triangles, new Vector3(radius, 0.07f, 0f), new Vector3(thick, 0.125f, thick), Quaternion.identity);
            }
            Mesh mesh = new Mesh { name = tips ? "Magnet Tips" : "Magnet" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBox(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector3 half, Quaternion rotation)
        {
            Vector3[] axes = { rotation * Vector3.right * half.x, rotation * Vector3.up * half.y, rotation * Vector3.forward * half.z };
            for (int axis = 0; axis < 3; axis++)
            {
                Vector3 normal = axes[axis];
                Vector3 u = axes[(axis + 1) % 3];
                Vector3 v = axes[(axis + 2) % 3];
                AddQuad(vertices, triangles, center + normal, u, v);
                AddQuad(vertices, triangles, center - normal, v, u);
            }
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector3 u, Vector3 v)
        {
            Vector3 a = center - u - v;
            Vector3 b = center + u - v;
            Vector3 c = center + u + v;
            Vector3 d = center - u + v;
            AddTriangle(vertices, triangles, a, b, c);
            AddTriangle(vertices, triangles, a, c, d);
        }

        private static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        private PickupView CreatePickupView(int slot)
        {
            PickupView view = new PickupView { Phase = (slot * 0.618034f % 1f) * Mathf.PI * 2f };
            GameObject root = new GameObject("Pickup");
            root.transform.SetParent(pickupRoot, false);
            view.Root = root.transform;

            GameObject model = new GameObject("Model");
            model.transform.SetParent(view.Root, false);
            view.Model = model.transform;
            view.Filter = model.AddComponent<MeshFilter>();
            view.Renderer = model.AddComponent<MeshRenderer>();
            view.Renderer.shadowCastingMode = ShadowCastingMode.Off;
            view.Renderer.receiveShadows = false;

            GameObject extra = new GameObject("Extra");
            extra.transform.SetParent(view.Model, false);
            view.Extra = extra.transform;
            view.ExtraFilter = extra.AddComponent<MeshFilter>();
            view.ExtraRenderer = extra.AddComponent<MeshRenderer>();
            view.ExtraRenderer.shadowCastingMode = ShadowCastingMode.Off;
            view.ExtraRenderer.receiveShadows = false;
            extra.SetActive(false);

            GameObject glow = new GameObject("Glow");
            glow.transform.SetParent(view.Root, false);
            view.Glow = glow.transform;
            glow.AddComponent<MeshFilter>().sharedMesh = FxAssets.FlatQuad;
            view.GlowRenderer = glow.AddComponent<MeshRenderer>();
            view.GlowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            view.GlowRenderer.receiveShadows = false;

            root.SetActive(false);
            return view;
        }

        private static int PickupKey(SurvivorPickup pickup)
        {
            switch (pickup.Kind)
            {
                case PickupKind.Gem: return SurvivorPickup.GemSize(pickup.Value);
                case PickupKind.Gold: return KeyGold;
                case PickupKind.Meat: return KeyMeat;
                case PickupKind.Chest: return KeyChest;
                case PickupKind.Magnet: return KeyMagnet;
                case PickupKind.Mana: return KeyMana;
                case PickupKind.Bomb: return KeyBomb;
                case PickupKind.Rage: return KeyRage;
                case PickupKind.Shield: return KeyShield;
                case PickupKind.Haste: return KeyHaste;
                default: return KeyNone;
            }
        }

        private void HideAllPickups()
        {
            for (int i = 0; i < pickupViews.Length; i++)
            {
                PickupView view = pickupViews[i];
                if (view != null && view.Key != KeyNone)
                {
                    view.Key = KeyNone;
                    view.Root.gameObject.SetActive(false);
                }
            }
        }

        private void SyncPickups()
        {
            if (sim == null)
            {
                return;
            }

            IReadOnlyList<SurvivorPickup> pickups = sim.Pickups;
            int count = Mathf.Min(pickups.Count, pickupViews.Length);
            for (int i = 0; i < count; i++)
            {
                SurvivorPickup pickup = pickups[i];
                PickupView view = pickupViews[i];
                int key = pickup.Active ? PickupKey(pickup) : KeyNone;
                if (key == KeyNone)
                {
                    if (view != null && view.Key != KeyNone)
                    {
                        view.Key = KeyNone;
                        view.Root.gameObject.SetActive(false);
                    }
                    continue;
                }

                if (view == null)
                {
                    view = pickupViews[i] = CreatePickupView(i);
                }

                Vector3 position = ArenaSpace.ToWorld(pickup.Position);
                if (view.Key != key)
                {
                    SetupPickup(view, key);
                    view.Previous = position;
                    view.Current = position;
                    view.Root.gameObject.SetActive(true);
                }
                else
                {
                    bool moved = (position - view.Current).sqrMagnitude > 1e-8f;
                    // Resting pickups never move, so a moved resting pickup is a new drop in a reused slot.
                    bool snap = moved && (!pickup.Attracted || (position - view.Current).sqrMagnitude > SnapDistance * SnapDistance);
                    view.Previous = snap ? position : view.Current;
                    view.Current = position;
                }
                view.Attracted = pickup.Attracted;
            }
        }

        private void SetupPickup(PickupView view, int key)
        {
            view.Key = key;
            bool useChestModel = key == KeyChest && survivorArt != null && survivorArt.Chest != null;
            if (useChestModel && view.Chest == null)
            {
                view.Chest = Instantiate(survivorArt.Chest, view.Model, false);
                view.Chest.name = survivorArt.Chest.name;
                view.Chest.transform.localScale = Vector3.one * 0.55f;
            }
            if (view.Chest != null)
            {
                view.Chest.SetActive(useChestModel);
            }

            view.Renderer.enabled = !useChestModel;
            view.Filter.sharedMesh = pickupMeshes[key];
            view.Renderer.sharedMaterial = pickupMaterials[key];
            view.Model.localScale = useChestModel ? Vector3.one : pickupScales[key];
            view.Model.localRotation = Quaternion.identity;
            bool hasExtra = !useChestModel && pickupExtraMeshes[key] != null;
            view.Extra.gameObject.SetActive(hasExtra);
            if (hasExtra)
            {
                view.ExtraFilter.sharedMesh = pickupExtraMeshes[key];
                view.ExtraRenderer.sharedMaterial = pickupExtraMaterials[key];
                view.Extra.localPosition = pickupExtraPositions[key];
                view.Extra.localScale = pickupExtraScales[key];
            }
            view.GlowRenderer.sharedMaterial = pickupGlowMaterials[key];
            float glow = glowSizes[key];
            view.Glow.localScale = new Vector3(glow, 1f, glow);
            view.Glow.localPosition = new Vector3(0f, 0.03f - PickupHeight, 0f);
        }

        private void PresentPickups()
        {
            spinClock = Time.unscaledTime;
            float t = spinClock;
            coinSparklesLeft = 3;
            for (int i = 0; i < demoPickups.Count; i++)
            {
                PresentPickup(demoPickups[i], t);
            }
            for (int i = 0; i < pickupViews.Length; i++)
            {
                PickupView view = pickupViews[i];
                if (view == null || view.Key == KeyNone)
                {
                    continue;
                }
                PresentPickup(view, t);
            }
        }

        private void PresentPickup(PickupView view, float t)
        {
            {
                Vector3 position = Vector3.LerpUnclamped(view.Previous, view.Current, interpolationAlpha);
                float bob = view.Attracted ? 0.1f : 0.07f * Mathf.Sin(t * 3f + view.Phase);
                view.Root.localPosition = new Vector3(position.x, PickupHeight + bob, position.z);

                float spin = (t * (view.Attracted ? 360f : 90f) + view.Phase * 57.3f) % 360f;
                switch (view.Key)
                {
                    case KeyGold:
                        view.Model.localRotation = Quaternion.Euler(0f, spin, 0f) * Quaternion.Euler(90f, 0f, 0f);
                        // Coins twinkle now and then.
                        if (coinSparklesLeft > 0 && Random.value < Time.deltaTime * 1.2f)
                        {
                            coinSparklesLeft--;
                            effects.Sparkle(view.Root.position, GoldColor, 1, 0.2f, 0.5f, 0.2f);
                        }
                        break;
                    case KeyChest:
                        view.Model.localRotation = Quaternion.Euler(0f, view.Phase * 57.3f, 0f);
                        break;
                    case KeyMagnet:
                        view.Model.localRotation = Quaternion.Euler(0f, spin, 0f) * Quaternion.Euler(-60f, 0f, 0f);
                        break;
                    case KeyShield:
                        view.Model.localRotation = Quaternion.Euler(0f, spin, 0f) * Quaternion.Euler(40f, 0f, 0f);
                        break;
                    case KeyHaste:
                        view.Model.localRotation = Quaternion.Euler(0f, spin, 0f) * Quaternion.Euler(0f, 0f, 70f);
                        break;
                    case KeyMeat:
                    case KeyMana:
                        view.Model.localRotation = Quaternion.Euler(0f, spin, 0f) * Quaternion.Euler(0f, 0f, 12f);
                        break;
                    default:
                        view.Model.localRotation = Quaternion.Euler(0f, spin, 0f);
                        break;
                }
            }
        }

        // ------------------------------------------------------------------ hammers

        private void BuildHammers()
        {
            hammerRoot = CreateChild("Hammers", actorsRoot);
            hammerHandleMaterial = Own(CreateStandard("Hammer Handle", new Color(0.45f, 0.28f, 0.14f), 0.2f));
            hammerHeadMaterial = Own(CreateEmissive("Hammer Head", new Color(0.62f, 0.64f, 0.7f), new Color(0.12f, 0.1f, 0.06f), 0.75f, 0.8f));
            boltMaterial = Own(CreateEmissive("Magic Bolt", new Color(0.45f, 0.65f, 1f), new Color(0.7f, 1.1f, 2.4f), 0.8f, 0f));
            boltGlowMaterial = Own(FxAssets.Create("Magic Bolt Glow", FxAssets.RadialGlow, true));
            boltGlowMaterial.color = new Color(0.45f, 0.7f, 1f, 0.7f);
            fireballMaterial = Own(CreateEmissive("Fireball", new Color(1f, 0.55f, 0.15f), new Color(2.4f, 1f, 0.2f), 0.6f, 0f));
            fireballGlowMaterial = Own(FxAssets.Create("Fireball Glow", FxAssets.RadialGlow, true));
            fireballGlowMaterial.color = new Color(1f, 0.45f, 0.12f, 0.8f);
            arrowShaftMaterial = Own(CreateStandard("Arrow Shaft", new Color(0.55f, 0.4f, 0.24f), 0.2f));
            arrowHeadMaterial = Own(CreateEmissive("Arrow Head", new Color(0.78f, 0.8f, 0.85f), new Color(0.1f, 0.1f, 0.12f), 0.85f, 0.85f));
            powerShotMaterial = Own(CreateEmissive("Power Shot", new Color(1f, 0.82f, 0.3f), new Color(1.6f, 1.1f, 0.3f), 0.85f, 0.6f));
            bombShellMaterial = Own(CreateEmissive("Bomb Shell", new Color(0.13f, 0.13f, 0.16f), new Color(0.03f, 0.03f, 0.04f), 0.85f, 0.3f));
            bounceMaterial = Own(CreateEmissive("Bounce Crystal", new Color(0.3f, 0.95f, 1f), new Color(0.3f, 1.5f, 1.8f), 0.9f, 0f));
            momentumMaterial = Own(CreateEmissive("Momentum Spirit", new Color(0.4f, 1f, 0.6f), new Color(0.4f, 1.7f, 0.7f), 0.9f, 0f));
            orbRingMaterial = Own(FxAssets.Create("Orb Ring", VfxLibrary.Texture("magic_02"), true));
            orbRingMaterial.color = new Color(0.55f, 0.8f, 1f, 0.95f);
            orbHaloMaterial = Own(FxAssets.Create("Orb Halo", VfxLibrary.Texture("star_06"), true));
            orbHaloMaterial.color = new Color(0.8f, 0.9f, 1f, 0.9f);
            fireSwirlMaterial = Own(FxAssets.Create("Fire Swirl", VfxLibrary.Texture("twirl_02"), true));
            fireSwirlMaterial.color = new Color(1f, 0.55f, 0.15f, 0.95f);
            bounceStarMaterial = Own(FxAssets.Create("Bounce Star", VfxLibrary.Texture("star_06"), true));
            bounceStarMaterial.color = new Color(0.4f, 1f, 1f, 0.9f);
            momentumGlowMaterial = Own(FxAssets.Create("Momentum Glow", FxAssets.RadialGlow, true));
            momentumGlowMaterial.color = new Color(0.4f, 1f, 0.6f, 0.75f);
            for (int i = 0; i < HammerPrewarm; i++)
            {
                hammerViews[i] = CreateHammerView();
            }
        }

        private HammerView CreateHammerView()
        {
            HammerView view = new HammerView();
            GameObject root = new GameObject("Projectile");
            root.transform.SetParent(hammerRoot, false);
            view.Root = root.transform;
            view.Spinner = CreateChild("Spinner", view.Root);
            view.Trail = effects.CreateTrail(view.Root, 0f, new Color(1f, 0.7f, 0.35f, 0.5f), 0.4f, 0.16f);
            EnsureProjectileModel(view, ProjectileLook.Hammer);
            root.SetActive(false);
            return view;
        }

        /// <summary>Builds (once per view) the model of a projectile look under the spinner, inactive.</summary>
        private GameObject EnsureProjectileModel(HammerView view, ProjectileLook look)
        {
            int index = (int)look;
            if (view.Models[index] != null)
            {
                return view.Models[index];
            }

            Transform model = CreateChild(look.ToString(), view.Spinner);
            switch (look)
            {
                case ProjectileLook.MagicBolt:
                {
                    GameObject storeOrb = effects.StoreAttach(StoreFx.MagicBall, model, 1.3f);
                    if (storeOrb != null)
                    {
                        storeOrb.SetActive(true);
                        break;
                    }
                    // A round orb with a bright star in it and two rune rings turning around it.
                    GameObject core = CreatePrimitive("Core", PrimitiveType.Sphere, model, boltMaterial);
                    core.transform.localScale = Vector3.one * 0.4f;
                    Transform glow = CreateFlatQuad("Glow", model, boltGlowMaterial);
                    glow.localScale = new Vector3(1.9f, 1f, 1.9f);
                    Transform orbit = CreateChild("Orbit", model);
                    CreateFlatQuad("Ring", orbit, orbRingMaterial).localScale = new Vector3(1.5f, 1f, 1.5f);
                    Transform tilted = CreateFlatQuad("Tilted Ring", orbit, orbRingMaterial);
                    tilted.localRotation = Quaternion.Euler(65f, 0f, 0f);
                    tilted.localScale = new Vector3(1.2f, 1f, 1.2f);
                    Transform halo = CreateFlatQuad("Halo", orbit, orbHaloMaterial);
                    halo.localPosition = new Vector3(0f, 0.25f, 0f);
                    halo.localScale = new Vector3(0.9f, 1f, 0.9f);
                    view.Orbits[index] = orbit;
                    break;
                }
                case ProjectileLook.Fireball:
                {
                    GameObject storeFire = effects.StoreAttach(StoreFx.FireBall, model, 1.3f);
                    if (storeFire != null)
                    {
                        storeFire.SetActive(true);
                        break;
                    }
                    GameObject core = CreatePrimitive("Core", PrimitiveType.Sphere, model, fireballMaterial);
                    core.transform.localScale = Vector3.one * 0.62f;
                    Transform glow = CreateFlatQuad("Glow", model, fireballGlowMaterial);
                    glow.localScale = new Vector3(3f, 1f, 3f);
                    Transform orbit = CreateChild("Orbit", model);
                    Transform swirl = CreateFlatQuad("Swirl", orbit, fireSwirlMaterial);
                    swirl.localPosition = new Vector3(0f, 0.34f, 0f);
                    swirl.localScale = new Vector3(1.9f, 1f, 1.9f);
                    view.Orbits[index] = orbit;
                    break;
                }
                case ProjectileLook.Bomb:
                {
                    // A black round bomb: cap, bent fuse and a burning tip.
                    GameObject shell = CreatePrimitive("Shell", PrimitiveType.Sphere, model, bombShellMaterial);
                    shell.transform.localScale = Vector3.one * 0.66f;
                    GameObject cap = CreatePrimitive("Cap", PrimitiveType.Cylinder, model, hammerHeadMaterial);
                    cap.transform.localPosition = new Vector3(0f, 0.33f, 0f);
                    cap.transform.localScale = new Vector3(0.2f, 0.05f, 0.2f);
                    GameObject fuse = CreatePrimitive("Fuse", PrimitiveType.Cylinder, model, arrowShaftMaterial);
                    fuse.transform.localPosition = new Vector3(0.05f, 0.47f, 0f);
                    fuse.transform.localRotation = Quaternion.Euler(0f, 0f, -22f);
                    fuse.transform.localScale = new Vector3(0.045f, 0.11f, 0.045f);
                    GameObject ember = CreatePrimitive("Ember", PrimitiveType.Sphere, model, fireballMaterial);
                    ember.transform.localPosition = new Vector3(0.1f, 0.59f, 0f);
                    ember.transform.localScale = Vector3.one * 0.13f;
                    Transform glow = CreateFlatQuad("Ember Glow", model, fireballGlowMaterial);
                    glow.localPosition = new Vector3(0.1f, 0.6f, 0f);
                    glow.localScale = new Vector3(0.9f, 1f, 0.9f);
                    break;
                }
                case ProjectileLook.Bounce:
                {
                    // A cut crystal that tumbles, with a star flare.
                    GameObject core = CreateMeshObject("Ricochet Crystal", model, gemMesh, bounceMaterial);
                    core.transform.localScale = new Vector3(0.42f, 0.6f, 0.42f);
                    Transform orbit = CreateChild("Orbit", model);
                    CreateFlatQuad("Star", orbit, bounceStarMaterial).localScale = new Vector3(1.7f, 1f, 1.7f);
                    view.Orbits[index] = orbit;
                    break;
                }
                case ProjectileLook.Momentum:
                {
                    // A long spearhead of light pointing where it flies.
                    GameObject core = CreateMeshObject("Spirit Lance", model, gemMesh, momentumMaterial);
                    core.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    core.transform.localScale = new Vector3(0.3f, 1.25f, 0.3f);
                    GameObject wings = CreateMeshObject("Wings", model, gemMesh, momentumMaterial);
                    wings.transform.localPosition = new Vector3(0f, 0f, -0.2f);
                    wings.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    wings.transform.localScale = new Vector3(0.75f, 0.5f, 0.08f);
                    Transform glow = CreateFlatQuad("Glow", model, momentumGlowMaterial);
                    glow.localScale = new Vector3(1.3f, 1f, 2.6f);
                    break;
                }
                case ProjectileLook.Arrow:
                case ProjectileLook.PowerShot:
                {
                    bool power = look == ProjectileLook.PowerShot;
                    float length = power ? 1.5f : 0.8f;
                    float thickness = power ? 0.08f : 0.05f;
                    GameObject shaft = CreatePrimitive("Shaft", PrimitiveType.Cube, model, power ? powerShotMaterial : arrowShaftMaterial);
                    shaft.transform.localScale = new Vector3(thickness, thickness, length);
                    GameObject head = CreatePrimitive("Head", PrimitiveType.Cube, model, power ? powerShotMaterial : arrowHeadMaterial);
                    head.transform.localPosition = new Vector3(0f, 0f, length * 0.5f + 0.05f);
                    head.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    head.transform.localScale = new Vector3(thickness * 2.6f, thickness, thickness * 2.6f);
                    GameObject fletching = CreatePrimitive("Fletching", PrimitiveType.Cube, model, power ? powerShotMaterial : arrowHeadMaterial);
                    fletching.transform.localPosition = new Vector3(0f, 0f, -length * 0.42f);
                    fletching.transform.localScale = new Vector3(thickness * 3f, thickness * 0.4f, length * 0.16f);
                    if (power)
                    {
                        Transform glow = CreateFlatQuad("Glow", model, boltGlowMaterial);
                        glow.localScale = new Vector3(0.8f, 1f, 2.4f);
                    }
                    break;
                }
                default:
                    if (survivorArt != null && survivorArt.HammerProp != null)
                    {
                        GameObject prop = Instantiate(survivorArt.HammerProp, model, false);
                        prop.name = survivorArt.HammerProp.name;
                    }
                    else
                    {
                        GameObject handle = CreatePrimitive("Handle", PrimitiveType.Cylinder, model, hammerHandleMaterial);
                        handle.transform.localPosition = new Vector3(0f, -0.12f, 0f);
                        handle.transform.localScale = new Vector3(0.09f, 0.34f, 0.09f);
                        GameObject head = CreatePrimitive("Head", PrimitiveType.Cube, model, hammerHeadMaterial);
                        head.transform.localPosition = new Vector3(0f, 0.26f, 0f);
                        head.transform.localScale = new Vector3(0.46f, 0.26f, 0.26f);
                    }
                    break;
            }

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            }
            model.gameObject.SetActive(look == view.Look);
            view.Models[index] = model.gameObject;
            return model.gameObject;
        }

        private static GameObject CreateMeshObject(string objectName, Transform parent, Mesh mesh, Material material)
        {
            GameObject created = new GameObject(objectName);
            created.transform.SetParent(parent, false);
            created.AddComponent<MeshFilter>().sharedMesh = mesh;
            created.AddComponent<MeshRenderer>().sharedMaterial = material;
            return created;
        }

        /// <summary>Shows the model of <paramref name="look"/> (gold and larger for an evolved weapon) and recolours the trail.</summary>
        private void SetProjectileLook(HammerView view, ProjectileLook look, bool evolved)
        {
            if (view.Look == look && view.Evolved == evolved && view.Renderers != null)
            {
                return;
            }
            GameObject shown = EnsureProjectileModel(view, look);
            for (int i = 0; i < view.Models.Length; i++)
            {
                if (view.Models[i] != null)
                {
                    view.Models[i].SetActive(view.Models[i] == shown);
                }
            }
            view.Look = look;
            view.Evolved = evolved;
            view.Renderers = shown.GetComponentsInChildren<Renderer>(true);
            view.Spinner.localScale = Vector3.one * (evolved ? SurvivorViewLogic.EvolutionScale : 1f);
            view.Spinner.localRotation = Quaternion.identity;
            SetRendererTint(view.Renderers, EvolvedTint, evolved);

            int key = (int)look * 2 + (evolved ? 1 : 0);
            if (projectileTrails[key] == null)
            {
                Color color = ProjectileTrailColor(look);
                if (evolved)
                {
                    color = Color.Lerp(color, SurvivorViewLogic.EvolutionGold, 0.7f);
                }
                projectileTrails[key] = TrailGradient(color);
            }
            view.Trail.colorGradient = projectileTrails[key];
            float scale = evolved ? SurvivorViewLogic.EvolutionScale : 1f;
            switch (look)
            {
                case ProjectileLook.Fireball:
                    view.Trail.widthMultiplier = 0.9f * scale;
                    view.Trail.time = 0.32f;
                    break;
                case ProjectileLook.PowerShot:
                    view.Trail.widthMultiplier = 0.4f * scale;
                    view.Trail.time = 0.4f;
                    break;
                case ProjectileLook.Arrow:
                    view.Trail.widthMultiplier = 0.16f * scale;
                    view.Trail.time = 0.12f;
                    break;
                case ProjectileLook.MagicBolt:
                    view.Trail.widthMultiplier = 0.45f * scale;
                    view.Trail.time = 0.3f;
                    break;
                case ProjectileLook.Bomb:
                    view.Trail.widthMultiplier = 0.24f * scale;
                    view.Trail.time = 0.2f;
                    break;
                case ProjectileLook.Bounce:
                    view.Trail.widthMultiplier = 0.32f * scale;
                    view.Trail.time = 0.35f;
                    break;
                case ProjectileLook.Momentum:
                    view.Trail.widthMultiplier = 0.42f * scale;
                    view.Trail.time = 0.28f;
                    break;
                default:
                    view.Trail.widthMultiplier = 0.4f * scale;
                    view.Trail.time = 0.16f;
                    break;
            }
        }

        private static Color ProjectileTrailColor(ProjectileLook look)
        {
            switch (look)
            {
                case ProjectileLook.MagicBolt: return new Color(0.45f, 0.7f, 1f, 0.6f);
                case ProjectileLook.Fireball: return new Color(1f, 0.45f, 0.12f, 0.75f);
                case ProjectileLook.Arrow: return new Color(0.85f, 0.9f, 0.8f, 0.45f);
                case ProjectileLook.PowerShot: return new Color(1f, 0.85f, 0.35f, 0.7f);
                case ProjectileLook.Bomb: return new Color(1f, 0.35f, 0.15f, 0.65f);
                case ProjectileLook.Bounce: return new Color(0.25f, 0.95f, 1f, 0.75f);
                case ProjectileLook.Momentum: return new Color(0.35f, 1f, 0.65f, 0.75f);
                default: return new Color(1f, 0.7f, 0.35f, 0.5f);
            }
        }

        private void HideAllHammers()
        {
            for (int i = 0; i < hammerViews.Length; i++)
            {
                HammerView view = hammerViews[i];
                if (view != null && view.Id >= 0)
                {
                    HideHammer(view);
                }
            }
        }

        private static void HideHammer(HammerView view)
        {
            view.Id = -1;
            view.Trail.emitting = false;
            view.Trail.Clear();
            view.Root.gameObject.SetActive(false);
        }

        private void SyncHammers()
        {
            if (sim == null)
            {
                return;
            }

            IReadOnlyList<SurvivorProjectile> projectiles = sim.Projectiles;
            int count = Mathf.Min(projectiles.Count, hammerViews.Length);
            for (int i = 0; i < count; i++)
            {
                SurvivorProjectile projectile = projectiles[i];
                HammerView view = hammerViews[i];
                if (!projectile.Active)
                {
                    if (view != null && view.Id >= 0)
                    {
                        HideHammer(view);
                    }
                    continue;
                }

                if (view == null)
                {
                    view = hammerViews[i] = CreateHammerView();
                }

                Vector3 position = ArenaSpace.ToWorld(projectile.Position, HammerHeight);
                Vector3 velocity = new Vector3(projectile.Velocity.X, 0f, projectile.Velocity.Y);
                if (velocity.sqrMagnitude > 1e-6f)
                {
                    view.Direction = velocity.normalized;
                }

                if (view.Id != projectile.Id)
                {
                    view.Id = projectile.Id;
                    ProjectileLook look = SurvivorViewLogic.ProjectileLookOf(projectile.SourceIndex, sim.Config.ClassDef);
                    SetProjectileLook(view, look, projectile.SourceIndex >= 0 && SurvivorViewLogic.IsEvolution(projectile.SourceIndex));
                    view.Previous = position;
                    view.Current = position;
                    view.Root.localPosition = position;
                    view.Root.gameObject.SetActive(true);
                    view.Trail.Clear();
                    view.Trail.emitting = true;
                }
                else
                {
                    view.Previous = view.Current;
                    view.Current = position;
                }
            }
        }

        private void PresentHammers()
        {
            float spin = (Time.unscaledTime * 900f) % 360f;
            projectileFxLeft = ProjectileFxPerFrame;
            for (int i = 0; i < demoHammers.Count; i++)
            {
                PresentHammer(demoHammers[i], spin);
            }
            for (int i = 0; i < hammerViews.Length; i++)
            {
                HammerView view = hammerViews[i];
                if (view == null || view.Id < 0)
                {
                    continue;
                }
                PresentHammer(view, spin);
            }
        }

        private void PresentHammer(HammerView view, float spin)
        {
            Vector3 position = Vector3.LerpUnclamped(view.Previous, view.Current, interpolationAlpha);
            view.Root.SetPositionAndRotation(position, Quaternion.LookRotation(view.Direction, Vector3.up));
            // Hammers, bombs and crystals tumble; bolts, fireballs and arrows fly straight along their direction.
            if (view.Look == ProjectileLook.Hammer)
            {
                view.Spinner.localRotation = Quaternion.Euler(spin, 0f, 0f);
            }
            else if (view.Look == ProjectileLook.Bomb || view.Look == ProjectileLook.Bounce)
            {
                view.Spinner.localRotation = Quaternion.Euler(spin * 0.4f, spin * 0.25f, 0f);
            }
            Transform orbit = view.Orbits[(int)view.Look];
            if (orbit != null)
            {
                // The rings stay level with the ground whatever the model does, and turn.
                orbit.rotation = Quaternion.Euler(0f, spin * (view.Look == ProjectileLook.Fireball ? 1.3f : 0.6f), 0f);
            }

            float rate = ProjectileFxRate(view.Look);
            if (rate <= 0f)
            {
                return;
            }
            view.Emit += Time.deltaTime * rate;
            while (view.Emit >= 1f)
            {
                view.Emit -= 1f;
                if (projectileFxLeft <= 0)
                {
                    view.Emit = 0f;
                    break;
                }
                projectileFxLeft--;
                Vector3 behind = position - view.Direction * 0.25f;
                switch (view.Look)
                {
                    case ProjectileLook.Fireball:
                        effects.Flame(behind - Vector3.up * 0.3f, FireballColor, view.Evolved ? 0.95f : 0.7f, 0.32f);
                        if (Random.value < 0.18f)
                        {
                            effects.Smoke(behind, new Color(0.14f, 0.13f, 0.13f, 0.4f), 1, 0.6f, 0.6f);
                        }
                        break;
                    case ProjectileLook.Bomb:
                        effects.Sparks(position + Vector3.up * 0.45f, Vector3.up, EmberColor, 1, 2.5f, 0.9f);
                        break;
                    case ProjectileLook.Bounce:
                        effects.Sparkle(behind, new Color(0.4f, 1f, 1f), 1, 0.15f, 0.2f, 0.2f);
                        break;
                    case ProjectileLook.Momentum:
                        effects.Sparkle(behind, new Color(0.45f, 1f, 0.6f), 1, 0.15f, 0.2f, 0.2f);
                        break;
                    case ProjectileLook.PowerShot:
                        effects.Sparkle(behind, new Color(1f, 0.85f, 0.35f), 1, 0.12f, 0.2f, 0.2f);
                        break;
                    default:
                        effects.Sparkle(behind, new Color(0.55f, 0.8f, 1f), 1, 0.16f, 0.2f, 0.2f);
                        break;
                }
            }
        }

        /// <summary>Particles per second left behind by a flying projectile; zero for plain ones (hammers, arrows).</summary>
        private static float ProjectileFxRate(ProjectileLook look)
        {
            switch (look)
            {
                case ProjectileLook.Fireball: return 45f;
                case ProjectileLook.MagicBolt: return 28f;
                case ProjectileLook.Bomb: return 30f;
                case ProjectileLook.Bounce:
                case ProjectileLook.Momentum:
                case ProjectileLook.PowerShot: return 22f;
                default: return 0f;
            }
        }

        /// <summary>-fxDemo: one of every pickup and every special projectile standing still beside the hero, and all buff auras.</summary>
        private void PlayM11Demo(Vector3 center)
        {
            buffAuraDemo = true;
            PlayEnemyDemo(center);
            if (demoPickups.Count > 0)
            {
                return;
            }
            int[] keys = { KeyGold, KeyMeat, KeyMana, KeyMagnet, KeyBomb, KeyRage, KeyShield, KeyHaste };
            for (int i = 0; i < keys.Length; i++)
            {
                PickupView pickup = CreatePickupView(i);
                SetupPickup(pickup, keys[i]);
                pickup.Previous = pickup.Current = center + new Vector3(-7.2f + (i % 4) * 1.5f, 0f, (i / 4) * -1.8f);
                pickup.Root.gameObject.SetActive(true);
                demoPickups.Add(pickup);
            }
            ProjectileLook[] looks =
            {
                ProjectileLook.MagicBolt, ProjectileLook.Fireball, ProjectileLook.Bomb, ProjectileLook.Bounce, ProjectileLook.Momentum
            };
            for (int i = 0; i < looks.Length; i++)
            {
                HammerView hammer = CreateHammerView();
                hammer.Id = int.MaxValue - i;
                hammer.Direction = Vector3.right;
                SetProjectileLook(hammer, looks[i], false);
                hammer.Previous = hammer.Current = center + new Vector3(2.4f + i * 1.6f, HammerHeight, 0f);
                hammer.Root.gameObject.SetActive(true);
                demoHammers.Add(hammer);
            }
        }

        private void DestroyOwnedMaterials()
        {
            for (int i = 0; i < ownedObjects.Count; i++)
            {
                DestroyUnityObject(ownedObjects[i]);
            }
            ownedObjects.Clear();
        }
    }
}
