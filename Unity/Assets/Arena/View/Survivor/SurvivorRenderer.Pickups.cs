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
        private const int PickupKeyCount = 7;
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
        private readonly List<Object> ownedObjects = new List<Object>();
        private Transform pickupRoot;
        private Transform hammerRoot;
        private Material hammerHandleMaterial;
        private Material hammerHeadMaterial;
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
            public int Key = KeyNone;
            public Vector3 Previous;
            public Vector3 Current;
            public bool Attracted;
            public float Phase;
        }

        private sealed class HammerView
        {
            public Transform Root;
            public Transform Spinner;
            public TrailRenderer Trail;
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
            float[] gemSizes = { 0.36f, 0.5f, 0.7f };
            for (int size = 0; size < 3; size++)
            {
                Color color = GemColors[size];
                pickupMeshes[size] = gem;
                pickupMaterials[size] = Own(CreateEmissive("Gem " + size, color, color * 0.9f, 0.85f, 0.1f));
                pickupScales[size] = new Vector3(gemSizes[size] * 0.75f, gemSizes[size], gemSizes[size] * 0.75f);
                pickupGlowMaterials[size] = Own(FxAssets.Create("Gem Glow " + size, FxAssets.RadialGlow, true));
                pickupGlowMaterials[size].color = new Color(color.r, color.g, color.b, 0.55f);
                glowSizes[size] = 0.9f + 0.45f * size;
            }

            pickupMeshes[KeyGold] = BuiltinMesh(PrimitiveType.Cylinder);
            pickupMaterials[KeyGold] = Own(CreateEmissive("Gold Coin", new Color(1f, 0.78f, 0.25f), new Color(0.45f, 0.3f, 0.05f), 0.8f, 0.85f));
            pickupScales[KeyGold] = new Vector3(0.36f, 0.04f, 0.36f);
            pickupGlowMaterials[KeyGold] = Own(FxAssets.Create("Gold Glow", FxAssets.RadialGlow, true));
            pickupGlowMaterials[KeyGold].color = new Color(1f, 0.8f, 0.3f, 0.45f);
            glowSizes[KeyGold] = 1f;

            pickupMeshes[KeyMeat] = BuiltinMesh(PrimitiveType.Sphere);
            pickupMaterials[KeyMeat] = Own(CreateEmissive("Meat", new Color(0.78f, 0.32f, 0.26f), new Color(0.2f, 0.05f, 0.03f), 0.45f, 0f));
            pickupScales[KeyMeat] = new Vector3(0.55f, 0.38f, 0.38f);
            pickupGlowMaterials[KeyMeat] = Own(FxAssets.Create("Meat Glow", FxAssets.RadialGlow, true));
            pickupGlowMaterials[KeyMeat].color = new Color(0.4f, 1f, 0.45f, 0.5f);
            glowSizes[KeyMeat] = 1.3f;

            pickupMeshes[KeyChest] = BuiltinMesh(PrimitiveType.Cube);
            pickupMaterials[KeyChest] = Own(CreateEmissive("Chest Fallback", new Color(0.55f, 0.35f, 0.15f), new Color(0.15f, 0.08f, 0f), 0.3f, 0.2f));
            pickupScales[KeyChest] = new Vector3(0.9f, 0.6f, 0.6f);
            pickupGlowMaterials[KeyChest] = Own(FxAssets.Create("Chest Glow", FxAssets.RadialGlow, true));
            pickupGlowMaterials[KeyChest].color = new Color(1f, 0.85f, 0.35f, 0.6f);
            glowSizes[KeyChest] = 2.4f;

            pickupMeshes[KeyMagnet] = BuiltinMesh(PrimitiveType.Capsule);
            pickupMaterials[KeyMagnet] = Own(CreateEmissive("Magnet", new Color(0.9f, 0.2f, 0.25f), new Color(0.35f, 0.05f, 0.08f), 0.7f, 0.5f));
            pickupScales[KeyMagnet] = new Vector3(0.3f, 0.3f, 0.3f);
            pickupGlowMaterials[KeyMagnet] = Own(FxAssets.Create("Magnet Glow", FxAssets.RadialGlow, true));
            pickupGlowMaterials[KeyMagnet].color = new Color(0.5f, 0.7f, 1f, 0.55f);
            glowSizes[KeyMagnet] = 1.4f;

            for (int i = 0; i < PickupPrewarm; i++)
            {
                pickupViews[i] = CreatePickupView(i);
            }
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
            view.GlowRenderer.sharedMaterial = pickupGlowMaterials[key];
            float glow = glowSizes[key];
            view.Glow.localScale = new Vector3(glow, 1f, glow);
            view.Glow.localPosition = new Vector3(0f, 0.03f - PickupHeight, 0f);
        }

        private void PresentPickups()
        {
            spinClock = Time.unscaledTime;
            float t = spinClock;
            for (int i = 0; i < pickupViews.Length; i++)
            {
                PickupView view = pickupViews[i];
                if (view == null || view.Key == KeyNone)
                {
                    continue;
                }

                Vector3 position = Vector3.LerpUnclamped(view.Previous, view.Current, interpolationAlpha);
                float bob = view.Attracted ? 0.1f : 0.07f * Mathf.Sin(t * 3f + view.Phase);
                view.Root.localPosition = new Vector3(position.x, PickupHeight + bob, position.z);

                float spin = (t * (view.Attracted ? 360f : 90f) + view.Phase * 57.3f) % 360f;
                switch (view.Key)
                {
                    case KeyGold:
                        view.Model.localRotation = Quaternion.Euler(0f, spin, 0f) * Quaternion.Euler(90f, 0f, 0f);
                        break;
                    case KeyChest:
                        view.Model.localRotation = Quaternion.Euler(0f, view.Phase * 57.3f, 0f);
                        break;
                    case KeyMagnet:
                        view.Model.localRotation = Quaternion.Euler(0f, spin, 90f);
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
            for (int i = 0; i < HammerPrewarm; i++)
            {
                hammerViews[i] = CreateHammerView();
            }
        }

        private HammerView CreateHammerView()
        {
            HammerView view = new HammerView();
            GameObject root = new GameObject("Hammer");
            root.transform.SetParent(hammerRoot, false);
            view.Root = root.transform;
            view.Spinner = CreateChild("Spinner", view.Root);

            if (survivorArt != null && survivorArt.HammerProp != null)
            {
                GameObject prop = Instantiate(survivorArt.HammerProp, view.Spinner, false);
                prop.name = survivorArt.HammerProp.name;
            }
            else
            {
                GameObject handle = CreatePrimitive("Handle", PrimitiveType.Cylinder, view.Spinner, hammerHandleMaterial);
                handle.transform.localPosition = new Vector3(0f, -0.12f, 0f);
                handle.transform.localScale = new Vector3(0.09f, 0.34f, 0.09f);
                GameObject head = CreatePrimitive("Head", PrimitiveType.Cube, view.Spinner, hammerHeadMaterial);
                head.transform.localPosition = new Vector3(0f, 0.26f, 0f);
                head.transform.localScale = new Vector3(0.46f, 0.26f, 0.26f);
            }
            Renderer[] renderers = view.Spinner.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            }

            view.Trail = effects.CreateTrail(view.Root, 0f, new Color(1f, 0.7f, 0.35f, 0.5f), 0.4f, 0.16f);
            root.SetActive(false);
            return view;
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
            for (int i = 0; i < hammerViews.Length; i++)
            {
                HammerView view = hammerViews[i];
                if (view == null || view.Id < 0)
                {
                    continue;
                }
                Vector3 position = Vector3.LerpUnclamped(view.Previous, view.Current, interpolationAlpha);
                view.Root.SetPositionAndRotation(position, Quaternion.LookRotation(view.Direction, Vector3.up));
                view.Spinner.localRotation = Quaternion.Euler(spin, 0f, 0f);
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
