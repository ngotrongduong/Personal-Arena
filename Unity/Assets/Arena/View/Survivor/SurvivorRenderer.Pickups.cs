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

        private const int ProjectileLookCount = 9;
        /// <summary>Half-width of the sword wave model at spinner scale 1 (the catalog radius of the wave).</summary>
        private const float SwordWaveRadius = 1.1f;

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
                ProjectileLook.MagicBolt, ProjectileLook.Fireball, ProjectileLook.Bomb, ProjectileLook.Bounce, ProjectileLook.Momentum,
                ProjectileLook.SwordWave
            };
            for (int i = 0; i < looks.Length; i++)
            {
                HammerView hammer = CreateHammerView();
                hammer.Id = int.MaxValue - i;
                hammer.Direction = Vector3.right;
                SetProjectileLook(hammer, looks[i], -1);
                hammer.Previous = hammer.Current = center + new Vector3(2.4f + i * 1.6f, HammerHeight, 0f);
                hammer.Root.gameObject.SetActive(true);
                demoHammers.Add(hammer);
            }
            // One projectile of every evolved thrower, in a row below, each with its own element.
            int shown = 0;
            for (int index = 0; index < SurvivorCatalog.CatalogSize; index++)
            {
                if (!SurvivorViewLogic.IsEvolution(index))
                {
                    continue;
                }
                ProjectileLook look = SurvivorViewLogic.ProjectileLookOf(index, sim.Config.ClassDef);
                WeaponVisual visual = SurvivorViewLogic.WeaponVisualOf(index);
                bool thrower = visual == WeaponVisual.Sweep || visual == WeaponVisual.Hammer || visual == WeaponVisual.MagicBolt
                    || visual == WeaponVisual.Arrow || visual == WeaponVisual.MultiShot || visual == WeaponVisual.Bomb
                    || visual == WeaponVisual.Bounce || visual == WeaponVisual.Momentum || visual == WeaponVisual.Trio
                    || visual == WeaponVisual.Quad;
                if (!thrower)
                {
                    continue;
                }
                HammerView hammer = CreateHammerView();
                hammer.Id = int.MaxValue - 100 - shown;
                hammer.Direction = Vector3.right;
                SetProjectileLook(hammer, look, index);
                hammer.Previous = hammer.Current = center + new Vector3(-12f + shown * 2.2f, HammerHeight, -2.4f);
                hammer.Root.gameObject.SetActive(true);
                demoHammers.Add(hammer);
                shown++;
            }
            PlayEvolutionDemo(center);
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
