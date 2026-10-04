using System.Collections.Generic;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PersonalArena.View
{
    /// <summary>
    /// Ring weapons: bodies that circle the hero all the time at a fixed distance (spirit orbs, saws, frost shards,
    /// the comet). One pooled view per body, built for its weapon the first time it is needed and sized to the
    /// body's real hit radius. No trails and no pack effects on them: a trail drawn in real time becomes a full
    /// circle when the run is watched at x8, and the packs' ball effects leave their particles behind.
    /// </summary>
    public sealed partial class SurvivorRenderer
    {
        private const float RingHeight = 0.85f;
        private static readonly Color SpiritColor = new Color(0.62f, 0.5f, 1f, 1f);
        private static readonly Color SawColor = new Color(0.86f, 0.89f, 0.96f, 1f);
        private static readonly Color HaloColor = new Color(0.55f, 0.9f, 1f, 1f);
        private static readonly Color CometColor = new Color(1f, 0.55f, 0.2f, 1f);

        private sealed class RingBodyView
        {
            public Transform Root;
            public Transform Spinner;
            /// <summary>Catalog index the view was built for.</summary>
            public int Weapon;
            /// <summary>Inventory slot × 16 + body: tells a body that moved from a new one.</summary>
            public int Key = -1;
            public bool Active;
            public bool Seen;
            public bool Clockwise;
            public Vector3 Previous;
            public Vector3 Current;
            /// <summary>Degrees per second the model turns around the vertical.</summary>
            public float Spin;
        }

        private readonly List<RingBodyView> ringViews = new List<RingBodyView>();
        private readonly Dictionary<int, Material> ringMaterials = new Dictionary<int, Material>();
        private Transform ringRoot;
        private Mesh sawMesh;
        private Mesh shardMesh;

        /// <summary>Theme colour of a ring weapon (its bodies and icon).</summary>
        public static Color RingColor(int catalogIndex)
        {
            switch (catalogIndex)
            {
                case SurvivorCatalog.SawRingIndex: return SawColor;
                case SurvivorCatalog.FrostHaloIndex: return HaloColor;
                case SurvivorCatalog.CometIndex: return CometColor;
                default: return SpiritColor;
            }
        }

        private void SyncRings()
        {
            for (int i = 0; i < ringViews.Count; i++)
            {
                ringViews[i].Seen = false;
            }

            Vec2 hero = sim.Hero.Position;
            for (int slot = 0; slot < SurvivorCatalog.MaxWeapons; slot++)
            {
                int count = sim.RingBodyCount(slot);
                if (count == 0)
                {
                    continue;
                }
                int weapon = sim.Inventory.WeaponAt(slot);
                ItemDef def = SurvivorCatalog.Get(weapon);
                float scale = sim.RingBodyRadius(slot) / Mathf.Max(0.01f, def.ProjectileRadius);
                for (int k = 0; k < count; k++)
                {
                    Vec2 body = sim.GetRingBodyPosition(slot, k);
                    Vector3 offset = new Vector3(body.X - hero.X, 0f, body.Y - hero.Y);
                    int key = slot * 16 + k;
                    RingBodyView view = AcquireRingView(weapon, key);
                    view.Seen = true;
                    view.Root.localScale = Vector3.one * scale;
                    if (!view.Active || view.Key != key)
                    {
                        view.Active = true;
                        view.Key = key;
                        view.Previous = offset;
                        view.Current = offset;
                        view.Root.gameObject.SetActive(true);
                    }
                    else
                    {
                        view.Previous = view.Current;
                        view.Current = offset;
                    }
                }
            }

            for (int i = 0; i < ringViews.Count; i++)
            {
                if (ringViews[i].Active && !ringViews[i].Seen)
                {
                    HideRingView(ringViews[i]);
                }
            }
        }

        private void PresentRings(float realDelta)
        {
            for (int i = 0; i < ringViews.Count; i++)
            {
                RingBodyView view = ringViews[i];
                if (!view.Active)
                {
                    continue;
                }
                // Blend on the circle so the bodies keep their distance between ticks.
                Vector3 offset = Vector3.Slerp(view.Previous, view.Current, interpolationAlpha);
                view.Root.position = heroDisplayPosition + offset + Vector3.up * RingHeight;
                // The body faces the way it travels (the comet's tail points back along the circle).
                Vector3 forward = new Vector3(-offset.z, 0f, offset.x) * (view.Clockwise ? -1f : 1f);
                if (forward.sqrMagnitude > 1e-6f)
                {
                    view.Root.rotation = Quaternion.LookRotation(forward, Vector3.up);
                }
                if (view.Spin != 0f)
                {
                    view.Spinner.Rotate(0f, view.Spin * realDelta, 0f, Space.Self);
                }
            }
        }

        private void HideAllRings()
        {
            for (int i = 0; i < ringViews.Count; i++)
            {
                if (ringViews[i].Active)
                {
                    HideRingView(ringViews[i]);
                }
            }
        }

        private static void HideRingView(RingBodyView view)
        {
            view.Active = false;
            view.Key = -1;
            view.Root.gameObject.SetActive(false);
        }

        /// <summary>The view already showing this body, else a free one of the same weapon, else a new one.</summary>
        private RingBodyView AcquireRingView(int weapon, int key)
        {
            RingBodyView free = null;
            for (int i = 0; i < ringViews.Count; i++)
            {
                RingBodyView view = ringViews[i];
                if (view.Weapon != weapon || view.Seen)
                {
                    continue;
                }
                if (view.Active && view.Key == key)
                {
                    return view;
                }
                if (!view.Active && free == null)
                {
                    free = view;
                }
            }
            if (free != null)
            {
                return free;
            }
            RingBodyView created = CreateRingView(weapon);
            ringViews.Add(created);
            return created;
        }

        private RingBodyView CreateRingView(int weapon)
        {
            if (ringRoot == null)
            {
                ringRoot = CreateChild("Ring Weapons", weaponRoot);
            }
            ItemDef def = SurvivorCatalog.Get(weapon);
            RingBodyView view = new RingBodyView { Weapon = weapon, Clockwise = def != null && def.AngularSpeedDegrees < 0f };
            GameObject root = new GameObject("Ring Body");
            root.transform.SetParent(ringRoot, false);
            view.Root = root.transform;
            view.Spinner = CreateChild("Spinner", view.Root);
            Color color = RingColor(weapon);
            float size = (def != null ? def.ProjectileRadius : 0.4f) * 2f;

            switch (weapon)
            {
                case SurvivorCatalog.SawRingIndex:
                    BuildSaw(view, color, size);
                    break;
                case SurvivorCatalog.FrostHaloIndex:
                    BuildShard(view, color, size);
                    break;
                case SurvivorCatalog.CometIndex:
                    BuildOrb(view, color, new Color(2.4f, 0.9f, 0.2f), size, true);
                    break;
                default:
                    BuildOrb(view, color, new Color(1.1f, 0.8f, 2.4f), size, false);
                    break;
            }

            Renderer[] renderers = view.Root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            }
            root.SetActive(false);
            return view;
        }

        /// <summary>A glowing ball as wide as the body over a soft light on the ground; the comet drags a short tail.</summary>
        private void BuildOrb(RingBodyView view, Color color, Color emission, float size, bool tail)
        {
            Material material = RingMaterial(view.Weapon * 4, () => CreateEmissive("Ring Orb", color, emission, 0.7f, 0.1f));
            GameObject core = CreatePrimitive("Core", PrimitiveType.Sphere, view.Spinner, material);
            core.transform.localScale = Vector3.one * size * 0.9f;
            Material glowMaterial = RingMaterial(view.Weapon * 4 + 1, () =>
            {
                Material created = FxAssets.Create("Ring Orb Glow", FxAssets.RadialGlow, true);
                created.color = new Color(color.r, color.g, color.b, 0.5f);
                return created;
            });
            Transform glow = CreateFlatQuad("Glow", view.Root, glowMaterial);
            glow.localPosition = new Vector3(0f, 0.06f - RingHeight, 0f);
            glow.localScale = new Vector3(size * 1.8f, 1f, size * 1.8f);
            if (tail)
            {
                // A fixed length behind the body, so it looks the same at every watch speed.
                Transform trail = CreateFlatQuad("Tail", view.Root, glowMaterial);
                trail.localPosition = new Vector3(0f, 0f, -size * 1.3f);
                trail.localScale = new Vector3(size * 0.9f, 1f, size * 2.4f);
            }
        }

        /// <summary>A flat toothed disc that spins fast.</summary>
        private void BuildSaw(RingBodyView view, Color color, float size)
        {
            if (sawMesh == null)
            {
                sawMesh = Own(BuildSawMesh());
            }
            Material steel = RingMaterial(view.Weapon * 4, () =>
            {
                Material created = FxAssets.Create("Ring Saw", Texture2D.whiteTexture, false);
                created.color = color;
                return created;
            });
            Material hub = RingMaterial(view.Weapon * 4 + 1, () => CreateStandard("Ring Saw Hub", new Color(0.22f, 0.24f, 0.3f), 0.4f));
            GameObject disc = new GameObject("Disc");
            disc.transform.SetParent(view.Spinner, false);
            disc.transform.localScale = new Vector3(size, 1f, size);
            disc.AddComponent<MeshFilter>().sharedMesh = sawMesh;
            disc.AddComponent<MeshRenderer>().sharedMaterial = steel;
            GameObject centre = CreatePrimitive("Hub", PrimitiveType.Cylinder, view.Spinner, hub);
            centre.transform.localScale = new Vector3(size * 0.3f, 0.03f, size * 0.3f);
            view.Spin = 900f;
        }

        /// <summary>An upright ice crystal that turns slowly.</summary>
        private void BuildShard(RingBodyView view, Color color, float size)
        {
            if (shardMesh == null)
            {
                shardMesh = Own(BuildGemMesh());
            }
            Material ice = RingMaterial(view.Weapon * 4, () => CreateEmissive("Ring Shard", color, new Color(0.35f, 1.1f, 1.6f), 0.9f, 0.2f));
            GameObject shard = new GameObject("Shard");
            shard.transform.SetParent(view.Spinner, false);
            shard.transform.localScale = new Vector3(size * 0.55f, size * 1.1f, size * 0.55f);
            shard.AddComponent<MeshFilter>().sharedMesh = shardMesh;
            shard.AddComponent<MeshRenderer>().sharedMaterial = ice;
            view.Spin = 140f;
        }

        /// <summary>One material per weapon and part, shared by every body of that ring.</summary>
        private Material RingMaterial(int key, System.Func<Material> create)
        {
            if (!ringMaterials.TryGetValue(key, out Material material))
            {
                material = Own(create());
                ringMaterials[key] = material;
            }
            return material;
        }

        /// <summary>A flat disc with teeth, one unit wide, facing up.</summary>
        private static Mesh BuildSawMesh()
        {
            const int teeth = 10;
            const int rim = teeth * 2;
            Vector3[] vertices = new Vector3[rim + 1];
            int[] triangles = new int[rim * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < rim; i++)
            {
                float angle = i * Mathf.PI / teeth;
                float radius = i % 2 == 0 ? 0.5f : 0.36f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }
            for (int i = 0; i < rim; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % rim + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            Mesh mesh = new Mesh { name = "Saw" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
