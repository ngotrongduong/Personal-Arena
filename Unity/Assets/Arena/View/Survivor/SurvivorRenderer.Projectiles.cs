using System.Collections.Generic;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PersonalArena.View
{
    /// <summary>Thrown and flying weapons (hammers, arrows, orbs): pooled views, the look of each projectile and its trail.</summary>
    public sealed partial class SurvivorRenderer
    {
        private sealed class HammerView
        {
            public Transform Root;
            public Transform Spinner;
            public TrailRenderer Trail;
            public readonly GameObject[] Models = new GameObject[ProjectileLookCount];
            public readonly Transform[] Orbits = new Transform[ProjectileLookCount];
            // Looks shown by a store effect, which brings its own particles.
            public readonly bool[] StoreModel = new bool[ProjectileLookCount];
            public float Emit;
            public Renderer[] Renderers;
            public ProjectileLook Look = ProjectileLook.Hammer;
            public bool Evolved;
            // Catalog index of the evolved weapon that threw it (its element decides tint and trail); -1 otherwise.
            public int Source = -1;
            public int Id = -1;
            public Vector3 Previous;
            public Vector3 Current;
            public Vector3 Direction = Vector3.forward;
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
                        view.StoreModel[index] = true;
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
                        view.StoreModel[index] = true;
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
                    GameObject storeIce = effects.StoreAttach(StoreFx.IceBall, model, 1.1f);
                    if (storeIce != null)
                    {
                        storeIce.SetActive(true);
                        view.StoreModel[index] = true;
                        break;
                    }
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
                    GameObject storeSpirit = effects.StoreAttach(StoreFx.ElementalBall2, model, 1.2f);
                    if (storeSpirit != null)
                    {
                        storeSpirit.SetActive(true);
                        view.StoreModel[index] = true;
                        break;
                    }
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
                case ProjectileLook.SwordWave:
                {
                    // A flat crescent as wide as the real hit width, with a brighter thin core on its leading edge.
                    float size = SwordWaveRadius / 0.9f;
                    GameObject blade = CreateMeshObject("Wave", model, FxAssets.WaveCrescent, FxAssets.GhostMaterial);
                    blade.transform.localScale = Vector3.one * size;
                    GameObject core = CreateMeshObject("Wave Core", model, FxAssets.WaveCrescent, FxAssets.GhostMaterial);
                    core.transform.localPosition = new Vector3(0f, 0.02f, 0.06f);
                    core.transform.localScale = new Vector3(size * 0.96f, size, size * 0.55f);
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

        /// <summary>
        /// Shows the model of <paramref name="look"/> and recolours the trail. A projectile of an evolved weapon
        /// (<paramref name="source"/> is its catalog index, −1 otherwise) is larger and takes its element's colour.
        /// </summary>
        private void SetProjectileLook(HammerView view, ProjectileLook look, int source)
        {
            bool evolved = source >= 0 && SurvivorViewLogic.IsEvolution(source);
            if (!evolved)
            {
                source = -1;
            }
            if (view.Look == look && view.Source == source && view.Renderers != null)
            {
                return;
            }
            view.Source = source;
            Color element = evolved ? SurvivorEvolutionStyles.Of(source).Color : Color.white;
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
            SetRendererTint(view.Renderers, new Color(element.r * 1.3f, element.g * 1.3f, element.b * 1.3f, 1f), evolved);
            if (look == ProjectileLook.SwordWave)
            {
                // The ghost material is white: the wave takes the sword's slash colour (its element once evolved).
                SetRendererTint(view.Renderers, evolved ? element : StrikeColor, true);
            }

            if (evolved)
            {
                view.Trail.colorGradient = EvolutionTrailGradient(source);
            }
            else
            {
                int key = (int)look;
                if (projectileTrails[key] == null)
                {
                    projectileTrails[key] = TrailGradient(ProjectileTrailColor(look));
                }
                view.Trail.colorGradient = projectileTrails[key];
            }
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
                case ProjectileLook.SwordWave:
                    // The crescent is the whole effect: a ribbon behind it reads as a white block.
                    view.Trail.widthMultiplier = 0f;
                    view.Trail.time = 0.05f;
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
                case ProjectileLook.SwordWave: return new Color(0.78f, 0.9f, 1f, 0.3f);
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
                    SetProjectileLook(view, look, projectile.SourceIndex);
                    if (look == ProjectileLook.SwordWave)
                    {
                        // As wide as the wave really cuts (area bonuses and the evolution widen it).
                        view.Spinner.localScale = Vector3.one * (projectile.Radius / SwordWaveRadius);
                    }
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

            // An evolved projectile always sheds its element, whatever the base weapon does.
            float rate = view.Evolved ? EvolutionTrailRate : ProjectileFxRate(view.Look);
            if (rate <= 0f || (!view.Evolved && view.StoreModel[(int)view.Look]))
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
                if (view.Evolved)
                {
                    EvolutionTrail(view.Source, behind);
                    continue;
                }
                switch (view.Look)
                {
                    case ProjectileLook.Fireball:
                        effects.Flame(behind - Vector3.up * 0.3f, FireballColor, 0.7f, 0.32f);
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
    }
}
