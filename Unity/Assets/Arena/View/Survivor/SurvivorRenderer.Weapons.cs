using System.Collections.Generic;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PersonalArena.View
{
    /// <summary>
    /// Weapon and loot visuals: thrust streaks (spear, arcane beam, crossbow), orbiting axes / fire orbs / knives,
    /// the aura disc, the shockwave ring, strike blasts (lightning, arrow rain), skill blasts (fireball, frost burst),
    /// Spitter spit and the chest / magnet bursts. Evolutions draw like their base weapon, larger and gold-tinted.
    /// Everything is pooled and built once.
    /// </summary>
    public sealed partial class SurvivorRenderer
    {
        private const int SpearPool = 14;
        private const int AxePool = 8;
        private const int SpitPrewarm = 16;
        private const float SpearLifetime = 0.24f;
        private const float SpearHeight = 0.9f;
        private const float AxeHeight = 0.95f;
        private const float SpitHeight = 0.9f;
        private const float LightningHeight = 7f;
        // The ring texture peaks at 80 % of its half size, so a ring of radius R needs a quad of 2.5 R.
        private const float RingQuadPerRadius = 2.5f;

        private static readonly Color SpearColor = new Color(0.55f, 0.85f, 1f, 1f);
        private static readonly Color AxeTrailColor = new Color(1f, 0.55f, 0.35f, 0.55f);
        private static readonly Color FireOrbTrailColor = new Color(1f, 0.5f, 0.15f, 0.65f);
        private static readonly Color KnifeTrailColor = new Color(0.75f, 0.88f, 1f, 0.5f);
        private static readonly Color AuraColor = new Color(1f, 0.88f, 0.4f, 1f);
        private static readonly Color WaveColor = new Color(0.6f, 0.68f, 1f, 1f);
        private static readonly Color LightningColor = new Color(0.75f, 0.85f, 1f, 1f);
        private static readonly Color SpitColor = new Color(0.55f, 1f, 0.3f, 1f);
        private static readonly Color MagnetColor = new Color(0.5f, 0.7f, 1f, 1f);
        private static readonly Color EvolvedTint = new Color(1.35f, 1.12f, 0.55f, 1f);

        private readonly SpearFx[] spears = new SpearFx[SpearPool];
        private readonly AxeView[] axes = new AxeView[AxePool];
        private readonly SpitView[] spitViews = new SpitView[SurvivorSim.EnemyProjectileCapacity];
        private Transform weaponRoot;
        private Transform auraRoot;
        private Transform auraDisc;
        private Transform auraRing;
        private Material auraDiscMaterial;
        private Material auraRingMaterial;
        private float auraPulse;
        private Transform waveRoot;
        private Material waveMaterial;
        private float wavePrevious;
        private float waveCurrent;
        private Material spitMaterial;
        private Material spitGlowMaterial;
        private Material axeFallbackMaterial;
        private Material fireOrbMaterial;
        private Material fireOrbGlowMaterial;
        private Material knifeBladeMaterial;
        private Material knifeHandleMaterial;
        private int nextSpear;
        private int orbitLookIndex = int.MinValue;
        private int auraWeapon = -1;
        private int waveWeapon = -1;
        private Color auraColor = AuraColor;
        private Color waveColor = WaveColor;

        private sealed class SpearFx
        {
            public Transform Root;
            public Material Material;
            public Vector3 Origin;
            public Vector3 Direction;
            public Vector3 Up = Vector3.up;
            public Color Color = SpearColor;
            public float Width = 1f;
            public float Length;
            public float Age = SpearLifetime;
        }

        private sealed class AxeView
        {
            public Transform Root;
            public Transform Spinner;
            public GameObject AxeModel;
            public GameObject OrbModel;
            public GameObject KnifeModel;
            public Renderer[] Renderers;
            public TrailRenderer Trail;
            public bool Active;
            public Vector3 Previous;
            public Vector3 Current;
        }

        private sealed class SpitView
        {
            public Transform Root;
            public TrailRenderer Trail;
            public int Id = -1;
            public Vector3 Previous;
            public Vector3 Current;
        }

        // ------------------------------------------------------------------ build

        private void BuildWeapons()
        {
            weaponRoot = CreateChild("Weapons", actorsRoot);

            for (int i = 0; i < SpearPool; i++)
            {
                SpearFx spear = new SpearFx();
                spear.Material = Own(FxAssets.Create("Spear Streak", FxAssets.SoftDot, true));
                spear.Root = CreateFlatQuad("Spear", weaponRoot, spear.Material);
                spear.Root.gameObject.SetActive(false);
                spears[i] = spear;
            }

            axeFallbackMaterial = Own(CreateEmissive("Axe Fallback", new Color(0.7f, 0.72f, 0.78f), new Color(0.25f, 0.12f, 0.05f), 0.75f, 0.8f));
            fireOrbMaterial = Own(CreateEmissive("Fire Orb", new Color(1f, 0.55f, 0.15f), new Color(2.2f, 0.9f, 0.2f), 0.6f, 0f));
            fireOrbGlowMaterial = Own(FxAssets.Create("Fire Orb Glow", FxAssets.RadialGlow, true));
            fireOrbGlowMaterial.color = new Color(1f, 0.5f, 0.15f, 0.75f);
            knifeBladeMaterial = Own(CreateEmissive("Knife Blade", new Color(0.82f, 0.86f, 0.92f), new Color(0.12f, 0.14f, 0.18f), 0.85f, 0.9f));
            knifeHandleMaterial = Own(CreateStandard("Knife Handle", new Color(0.35f, 0.24f, 0.14f), 0.2f));
            for (int i = 0; i < AxePool; i++)
            {
                axes[i] = CreateAxeView();
            }

            auraRoot = CreateChild("Aura", weaponRoot);
            auraDiscMaterial = Own(FxAssets.Create("Aura Disc", FxAssets.RadialGlow, true));
            auraRingMaterial = Own(FxAssets.Create("Aura Ring", FxAssets.Ring, true));
            auraDisc = CreateFlatQuad("Aura Disc", auraRoot, auraDiscMaterial);
            auraRing = CreateFlatQuad("Aura Ring", auraRoot, auraRingMaterial);
            auraRoot.gameObject.SetActive(false);

            waveMaterial = Own(FxAssets.Create("Shockwave Ring", FxAssets.Ring, true));
            waveRoot = CreateFlatQuad("Shockwave", weaponRoot, waveMaterial);
            waveRoot.gameObject.SetActive(false);

            spitMaterial = Own(CreateEmissive("Spit", new Color(0.35f, 0.8f, 0.2f), new Color(0.5f, 1.2f, 0.25f), 0.9f, 0f));
            spitGlowMaterial = Own(FxAssets.Create("Spit Glow", FxAssets.RadialGlow, true));
            spitGlowMaterial.color = new Color(0.5f, 1f, 0.3f, 0.7f);
            for (int i = 0; i < SpitPrewarm; i++)
            {
                spitViews[i] = CreateSpitView();
            }
            BuildM9Weapons();
        }

        private static Transform CreateFlatQuad(string objectName, Transform parent, Material material)
        {
            GameObject quad = new GameObject(objectName);
            quad.transform.SetParent(parent, false);
            quad.AddComponent<MeshFilter>().sharedMesh = FxAssets.FlatQuad;
            MeshRenderer renderer = quad.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return quad.transform;
        }

        private AxeView CreateAxeView()
        {
            AxeView view = new AxeView();
            GameObject root = new GameObject("Orbit Weapon");
            root.transform.SetParent(weaponRoot, false);
            view.Root = root.transform;
            view.Spinner = CreateChild("Spinner", view.Root);

            // Warrior: a KayKit axe laid flat.
            Transform axe = CreateChild("Axe", view.Spinner);
            view.AxeModel = axe.gameObject;
            if (survivorArt != null && survivorArt.AxeProp != null)
            {
                GameObject prop = Instantiate(survivorArt.AxeProp, axe, false);
                prop.name = survivorArt.AxeProp.name;
                // The KayKit axe stands along +Y; lay it flat so it sweeps like a thrown blade.
                prop.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                prop.transform.localPosition = new Vector3(0f, 0f, -0.25f);
                prop.transform.localScale = Vector3.one * 1.35f;
            }
            else
            {
                GameObject handle = CreatePrimitive("Handle", PrimitiveType.Cylinder, axe, hammerHandleMaterial);
                handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                handle.transform.localScale = new Vector3(0.08f, 0.32f, 0.08f);
                GameObject blade = CreatePrimitive("Blade", PrimitiveType.Cube, axe, axeFallbackMaterial);
                blade.transform.localPosition = new Vector3(0.18f, 0f, 0.26f);
                blade.transform.localScale = new Vector3(0.36f, 0.06f, 0.3f);
            }

            // Mage: a glowing fire orb.
            Transform orb = CreateChild("Fire Orb", view.Spinner);
            view.OrbModel = orb.gameObject;
            GameObject core = CreatePrimitive("Core", PrimitiveType.Sphere, orb, fireOrbMaterial);
            core.transform.localScale = Vector3.one * 0.44f;
            Transform glow = CreateFlatQuad("Glow", orb, fireOrbGlowMaterial);
            glow.localScale = new Vector3(1.5f, 1f, 1.5f);

            // Archer: a flat spinning knife.
            Transform knife = CreateChild("Knife", view.Spinner);
            view.KnifeModel = knife.gameObject;
            GameObject knifeBlade = CreatePrimitive("Blade", PrimitiveType.Cube, knife, knifeBladeMaterial);
            knifeBlade.transform.localPosition = new Vector3(0f, 0f, 0.14f);
            knifeBlade.transform.localScale = new Vector3(0.09f, 0.03f, 0.5f);
            GameObject knifeHandle = CreatePrimitive("Handle", PrimitiveType.Cube, knife, knifeHandleMaterial);
            knifeHandle.transform.localPosition = new Vector3(0f, 0f, -0.2f);
            knifeHandle.transform.localScale = new Vector3(0.07f, 0.05f, 0.18f);

            Renderer[] renderers = view.Spinner.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            }
            view.Renderers = renderers;

            view.Trail = effects.CreateTrail(view.Root, 0f, AxeTrailColor, 0.55f, 0.18f);
            view.Trail.emitting = false;
            ApplyOrbitLook(view, WeaponVisual.OrbitAxe, false, AxeTrailColor);
            root.SetActive(false);
            return view;
        }

        private SpitView CreateSpitView()
        {
            SpitView view = new SpitView();
            GameObject root = new GameObject("Spit");
            root.transform.SetParent(weaponRoot, false);
            view.Root = root.transform;

            GameObject orb = CreatePrimitive("Orb", PrimitiveType.Sphere, view.Root, spitMaterial);
            orb.transform.localScale = Vector3.one * 0.42f;
            orb.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Transform glow = CreateFlatQuad("Glow", view.Root, spitGlowMaterial);
            glow.localPosition = new Vector3(0f, 0.05f - SpitHeight, 0f);
            glow.localScale = new Vector3(1.3f, 1f, 1.3f);

            view.Trail = effects.CreateTrail(view.Root, 0f, new Color(0.5f, 1f, 0.3f, 0.5f), 0.32f, 0.2f);
            view.Trail.emitting = false;
            root.SetActive(false);
            return view;
        }

        /// <summary>Same colour ramp as <see cref="ArenaEffects.CreateTrail"/>, for recolouring a trail at runtime.</summary>
        private static Gradient TrailGradient(Color color)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 0.25f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.45f, 0.4f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        // ------------------------------------------------------------------ reset / sync

        private void HideAllWeapons()
        {
            for (int i = 0; i < spears.Length; i++)
            {
                if (spears[i] != null)
                {
                    spears[i].Age = SpearLifetime;
                    spears[i].Root.gameObject.SetActive(false);
                }
            }
            for (int i = 0; i < axes.Length; i++)
            {
                if (axes[i] != null && axes[i].Active)
                {
                    HideAxe(axes[i]);
                }
            }
            for (int i = 0; i < spitViews.Length; i++)
            {
                if (spitViews[i] != null && spitViews[i].Id >= 0)
                {
                    HideSpit(spitViews[i]);
                }
            }
            auraPulse = 0f;
            wavePrevious = 0f;
            waveCurrent = 0f;
            auraWeapon = -1;
            waveWeapon = -1;
            auraColor = AuraColor;
            waveColor = WaveColor;
            if (auraRoot != null)
            {
                auraRoot.gameObject.SetActive(false);
            }
            if (waveRoot != null)
            {
                waveRoot.gameObject.SetActive(false);
            }
            HideAllM9Weapons();
        }

        private static void HideAxe(AxeView view)
        {
            view.Active = false;
            view.Trail.emitting = false;
            view.Trail.Clear();
            view.Root.gameObject.SetActive(false);
        }

        private static void HideSpit(SpitView view)
        {
            view.Id = -1;
            view.Trail.emitting = false;
            view.Trail.Clear();
            view.Root.gameObject.SetActive(false);
        }

        /// <summary>Switches every orbit view to the model of the owned orbit weapon (axe, fire orb, knife; gold if evolved).</summary>
        private void SetOrbitWeapon(int catalogIndex)
        {
            orbitLookIndex = catalogIndex;
            WeaponVisual visual = SurvivorViewLogic.WeaponVisualOf(catalogIndex);
            bool evolved = SurvivorViewLogic.IsEvolution(catalogIndex);
            Color trail = visual == WeaponVisual.FireOrb ? FireOrbTrailColor
                : visual == WeaponVisual.OrbitKnife ? KnifeTrailColor
                : AxeTrailColor;
            if (evolved)
            {
                trail = Color.Lerp(trail, SurvivorViewLogic.EvolutionGold, 0.7f);
                trail.a = 0.65f;
            }
            for (int i = 0; i < axes.Length; i++)
            {
                ApplyOrbitLook(axes[i], visual, evolved, trail);
            }
        }

        private void ApplyOrbitLook(AxeView view, WeaponVisual visual, bool evolved, Color trail)
        {
            bool orb = visual == WeaponVisual.FireOrb;
            bool knife = visual == WeaponVisual.OrbitKnife;
            view.AxeModel.SetActive(!orb && !knife);
            view.OrbModel.SetActive(orb);
            view.KnifeModel.SetActive(knife);
            float scale = evolved ? SurvivorViewLogic.EvolutionScale : 1f;
            view.Spinner.localScale = Vector3.one * scale;
            view.Trail.colorGradient = TrailGradient(trail);
            view.Trail.widthMultiplier = (orb ? 0.7f : 0.55f) * scale;
            SetRendererTint(view.Renderers, EvolvedTint, evolved);
        }

        private void SyncWeapons()
        {
            if (sim == null)
            {
                return;
            }

            int orbitWeapon = sim.OrbitWeaponIndex;
            if (orbitWeapon >= 0 && orbitWeapon != orbitLookIndex)
            {
                SetOrbitWeapon(orbitWeapon);
            }

            int aura = sim.AuraWeaponIndex;
            if (aura != auraWeapon)
            {
                auraWeapon = aura;
                auraColor = FxColor(aura, AuraColor);
            }
            int wave = sim.ShockwaveWeaponIndex;
            if (wave != waveWeapon)
            {
                waveWeapon = wave;
                waveColor = FxColor(wave, WaveColor);
            }

            Vec2 hero = sim.Hero.Position;
            int axeCount = sim.Hero.Alive ? Mathf.Min(sim.OrbitAxeCount, axes.Length) : 0;
            for (int k = 0; k < axes.Length; k++)
            {
                AxeView view = axes[k];
                if (k >= axeCount)
                {
                    if (view.Active)
                    {
                        effects.Sparkle(view.Root.position, AxeTrailColor, 4, 0.2f, 1f, 0.16f);
                        HideAxe(view);
                    }
                    continue;
                }

                Vec2 axe = sim.GetOrbitAxePosition(k);
                Vector3 offset = new Vector3(axe.X - hero.X, 0f, axe.Y - hero.Y);
                if (!view.Active)
                {
                    view.Active = true;
                    view.Previous = offset;
                    view.Current = offset;
                    view.Root.gameObject.SetActive(true);
                    view.Trail.Clear();
                    view.Trail.emitting = true;
                }
                else
                {
                    view.Previous = view.Current;
                    view.Current = offset;
                }
            }

            wavePrevious = waveCurrent;
            waveCurrent = sim.ShockwaveRadius;
            if (waveCurrent <= 0f)
            {
                wavePrevious = 0f;
            }

            SyncSpit();
            SyncM9Weapons();
        }

        private void SyncSpit()
        {
            IReadOnlyList<SurvivorEnemyProjectile> projectiles = sim.EnemyProjectiles;
            int count = Mathf.Min(projectiles.Count, spitViews.Length);
            for (int i = 0; i < count; i++)
            {
                SurvivorEnemyProjectile projectile = projectiles[i];
                SpitView view = spitViews[i];
                if (!projectile.Active)
                {
                    if (view != null && view.Id >= 0)
                    {
                        if (puffsLeft > 0)
                        {
                            puffsLeft--;
                            effects.Puff(view.Current, new Color(0.45f, 0.9f, 0.25f, 0.6f), 4, 0.45f, 0.8f, 0.4f, 0.2f);
                        }
                        HideSpit(view);
                    }
                    continue;
                }

                if (view == null)
                {
                    view = spitViews[i] = CreateSpitView();
                }

                Vector3 position = ArenaSpace.ToWorld(projectile.Position, SpitHeight);
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

        // ------------------------------------------------------------------ events

        /// <summary>
        /// Every weapon visual except the sweep and the projectile throwers (those stay in <see cref="OnWeaponFired"/>):
        /// thrusts, orbits, auras, shockwaves and strikes. Evolutions arrive with their own index and draw like the base.
        /// </summary>
        private void OnNewWeaponFired(SurvivorEvent e, Vector3 heroPosition, Vector3 direction, WeaponVisual visual)
        {
            bool evolved = SurvivorViewLogic.IsEvolution(e.Id);
            switch (visual)
            {
                case WeaponVisual.Spear:
                case WeaponVisual.ArcaneBeam:
                case WeaponVisual.Crossbow:
                {
                    if (visual == WeaponVisual.Spear)
                    {
                        PlayHeroOneShot((strikeCount & 1) == 1 ? HeroStrikeB : HeroStrikeA, 2f, false);
                        strikeCount++;
                    }
                    else
                    {
                        PlayHeroOneShot(visual == WeaponVisual.ArcaneBeam ? HeroCast : HeroThrow, 2f, false);
                    }
                    int n = Mathf.Max(1, Mathf.RoundToInt(e.Extra));
                    float length = SurvivorViewLogic.ThrustLength(e.Id, sim.DerivedStats.AreaMul);
                    float width = (visual == WeaponVisual.ArcaneBeam ? 1.35f : visual == WeaponVisual.Crossbow ? 0.6f : 1f)
                        * (evolved ? SurvivorViewLogic.EvolutionScale : 1f);
                    Color color = FxColor(e.Id, SpearColor);
                    for (int k = 0; k < n; k++)
                    {
                        float angle = SurvivorCatalog.ThrustAngleOffset(k, n);
                        // Positive offsets turn counter-clockwise in sim space (x, y) = world (x, z).
                        Vector3 spearDirection = Quaternion.AngleAxis(-angle * Mathf.Rad2Deg, Vector3.up) * direction;
                        StartStreak(heroPosition + Vector3.up * SpearHeight, spearDirection, length, color, width, Vector3.up, true);
                    }
                    break;
                }
                case WeaponVisual.OrbitAxe:
                case WeaponVisual.FireOrb:
                case WeaponVisual.OrbitKnife:
                    effects.Sparkle(heroPosition, FxColor(e.Id, AxeTrailColor), 8, 1.6f, 1.2f, 0.2f);
                    break;
                case WeaponVisual.Aura:
                case WeaponVisual.HolyField:
                    auraPulse = 1f;
                    break;
                case WeaponVisual.Shockwave:
                case WeaponVisual.FrostNova:
                {
                    Vector3 center = ArenaSpace.ToWorld(e.Point);
                    Color color = FxColor(e.Id, WaveColor);
                    effects.Flash(center + Vector3.up * 0.6f, color, 3f, 0.18f);
                    if (visual == WeaponVisual.FrostNova)
                    {
                        effects.Sparkle(center, color, 14, 1.2f, 1.2f, 0.22f);
                        PlayHeroOneShot(HeroCast, 1.8f, false);
                    }
                    else
                    {
                        effects.Puff(center, DustColor, 10, 0.9f, 2.4f, 0.6f, 0.3f);
                        PlayHeroOneShot(HeroKick, 1.6f, false);
                    }
                    break;
                }
                case WeaponVisual.Lightning:
                    PlayHeroOneShot(HeroCast, 1.9f, false);
                    break;
                case WeaponVisual.ArrowRain:
                    PlayHeroOneShot(HeroThrow, 1.9f, false);
                    break;
                case WeaponVisual.Retaliate:
                    PlayHeroOneShot(HeroStrikeB, 2f, false);
                    break;
                case WeaponVisual.Barrier:
                    effects.Shockwave(heroPosition, BarrierColor, 4.4f, 0.4f);
                    effects.Sparkle(heroPosition, BarrierColor, 8, 0.9f, 1.4f, 0.18f);
                    break;
                case WeaponVisual.Boomerang:
                    PlayHeroOneShot(HeroThrow, 2f, false);
                    break;
                case WeaponVisual.Zone:
                    PlayHeroOneShot(HeroThrow, 1.8f, false);
                    effects.Shockwave(ArenaSpace.ToWorld(e.Point), PoisonColor, 3f, 0.35f);
                    break;
                case WeaponVisual.Freeze:
                    PlayHeroOneShot(HeroCast, 1.9f, false);
                    effects.Shockwave(heroPosition, FrostColor, 14f * RingQuadPerRadius, 0.65f);
                    effects.Flash(heroPosition + Vector3.up, FrostColor, 5f, 0.25f);
                    break;
                case WeaponVisual.Purge:
                    PlayHeroOneShot(HeroCast, 2f, false);
                    break;
                case WeaponVisual.BombRing:
                    PlayHeroOneShot(HeroThrow, 2f, false);
                    effects.Shockwave(heroPosition, FireballColor, 10f, 0.45f);
                    break;
                case WeaponVisual.FireballNova:
                case WeaponVisual.Stone:
                    PlayHeroOneShot(HeroCast, 2f, false);
                    break;
            }
        }

        /// <summary>
        /// A blast hit the ground. Strike weapons (lightning bolt, arrow volley) carry their catalog index; skill blasts
        /// carry −1: the frost burst (at the hero, announced by an AreaBurst SkillUsed in the same tick) or the
        /// fireball explosion.
        /// </summary>
        private void OnStrikeLanded(SurvivorEvent e, IReadOnlyList<SurvivorEvent> events, int index, Vector3 heroPosition)
        {
            Vector3 point = ArenaSpace.ToWorld(e.Point);
            float radius = Mathf.Max(0.3f, e.Value);
            if (OnM9StrikeLanded(e, events, index, heroPosition, point, radius))
            {
                return;
            }
            if (e.Id < 0)
            {
                if (IsFrostBurst(e, events, index))
                {
                    effects.Shockwave(heroPosition, FrostColor, radius * RingQuadPerRadius, 0.5f);
                    effects.Shockwave(heroPosition, Color.white, radius * RingQuadPerRadius * 0.6f, 0.3f);
                    effects.Flash(heroPosition + Vector3.up * 0.8f, FrostColor, radius * 1.2f, 0.25f);
                    effects.Sparkle(heroPosition, FrostColor, 22, radius * 0.7f, 1.4f, 0.26f);
                }
                else
                {
                    effects.Shockwave(point, FireballColor, radius * RingQuadPerRadius, 0.45f);
                    effects.Flash(point + Vector3.up * 0.8f, FireballColor, radius * 1.6f, 0.25f);
                    effects.Sparks(point + Vector3.up * 0.6f, Vector3.up, FireballColor, 12, 8f, 1.2f);
                    if (puffsLeft > 0)
                    {
                        puffsLeft--;
                        effects.Puff(point, SmokeColor, 8, 1f, radius, 0.8f, 0.8f);
                    }
                }
                return;
            }

            WeaponVisual visual = SurvivorViewLogic.WeaponVisualOf(e.Id);
            bool evolved = SurvivorViewLogic.IsEvolution(e.Id);
            if (visual == WeaponVisual.ArrowRain)
            {
                Color color = FxColor(e.Id, LightningColor);
                // A rain over the whole blast area: streaks falling steeply on a spiral that fills the radius, then a ring of the real radius.
                int arrows = Mathf.Clamp(Mathf.RoundToInt(radius * (evolved ? 4f : 3f)), 4, 14);
                for (int k = 0; k < arrows; k++)
                {
                    float a = k * 2.399963f + e.Point.X; // golden angle: evenly spread without a pattern
                    float r = radius * Mathf.Sqrt((k + 0.5f) / arrows) * 0.9f;
                    Vector3 land = point + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    Vector3 top = land + new Vector3(-0.8f, 4.5f, -0.8f);
                    Vector3 fall = land - top;
                    StartStreak(top, fall.normalized, fall.magnitude, color, evolved ? 0.5f : 0.38f, StreakUp(fall), false);
                }
                effects.Shockwave(point, color, radius * RingQuadPerRadius, 0.4f);
                if (puffsLeft > 0)
                {
                    puffsLeft--;
                    effects.Puff(point, DustColor, 5, 0.6f, radius, 0.5f, 0.3f);
                }
            }
            else
            {
                Color color = FxColor(e.Id, LightningColor);
                // Lightning: a vertical bolt from the sky, a flash and a ring of the real radius.
                Vector3 top = point + Vector3.up * LightningHeight;
                StartStreak(top, Vector3.down, LightningHeight, color, evolved ? 1.3f : 0.95f, StreakUp(Vector3.down), false);
                effects.Flash(point + Vector3.up * 0.8f, color, radius * 2.2f, 0.18f);
                effects.Shockwave(point, color, radius * RingQuadPerRadius, 0.35f);
                if (sparksLeft > 0)
                {
                    sparksLeft--;
                    effects.Sparks(point + Vector3.up * 0.3f, Vector3.up, color, 8, 7f, 1.1f);
                }
            }
        }

        private bool IsFrostBurst(SurvivorEvent e, IReadOnlyList<SurvivorEvent> events, int index)
        {
            Vec2 hero = sim.Hero.Position;
            float dx = e.Point.X - hero.X;
            float dy = e.Point.Y - hero.Y;
            if (dx * dx + dy * dy > 1f)
            {
                return false;
            }
            SkillDef[] skills = sim.Config.ClassDef != null ? sim.Config.ClassDef.ActiveSkills : null;
            if (skills == null)
            {
                return false;
            }
            for (int i = index + 1; i < events.Count; i++)
            {
                if (events[i].Type != SurvivorEventType.SkillUsed)
                {
                    continue;
                }
                int slot = (int)events[i].Value;
                if (slot >= 0 && slot < skills.Length && skills[slot] != null && skills[slot].Id == "frost-burst")
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Orientation hint for a streak quad: flat on the ground when horizontal, facing the camera otherwise.</summary>
        private Vector3 StreakUp(Vector3 direction)
        {
            Camera camera = ResolveCamera();
            Vector3 toCamera = camera != null ? -camera.transform.forward : Vector3.up;
            Vector3 up = Vector3.ProjectOnPlane(toCamera, direction.normalized);
            return up.sqrMagnitude > 1e-4f ? up.normalized : Vector3.back;
        }

        private void StartStreak(Vector3 origin, Vector3 direction, float length, Color color, float width, Vector3 up, bool sparks)
        {
            SpearFx spear = spears[nextSpear];
            nextSpear = (nextSpear + 1) % spears.Length;
            spear.Origin = origin;
            spear.Direction = direction;
            spear.Up = up;
            spear.Length = length;
            spear.Color = color;
            spear.Width = width;
            spear.Age = 0f;
            spear.Root.gameObject.SetActive(true);
            if (sparks && sparksLeft > 0)
            {
                sparksLeft--;
                effects.Sparks(origin + direction * length, direction, color, 5, 6f, 0.7f);
            }
        }

        private void OnChestOpened(SurvivorEvent e)
        {
            Vector3 point = ArenaSpace.ToWorld(e.Point);
            effects.Shockwave(point, GoldColor, 4f, 0.7f);
            effects.Flash(point + Vector3.up * 0.8f, GoldColor, 3.5f, 0.3f);
            effects.Sparkle(point, GoldColor, 26, 1f, 3f, 0.3f);
            string label = "RƯƠNG! +" + NumberText(Mathf.RoundToInt(e.Value)) + " vàng";
            effects.Text(point + Vector3.up * 2.6f, label, GoldColor, 1.3f, 1.6f);
            if (e.Id >= 0 && e.Extra > 0f)
            {
                Color color = SurvivorViewLogic.ItemColor(e.Id);
                effects.Text(point + Vector3.up * 1.9f, "Nâng cấp Lv " + NumberText(Mathf.RoundToInt(e.Extra)), color, 1.1f, 1.6f);
            }
        }

        private void OnMagnetPicked(Vector3 heroPosition)
        {
            effects.Shockwave(heroPosition, MagnetColor, 12f, 0.8f);
            effects.Shockwave(heroPosition, MagnetColor, 6f, 0.5f);
            effects.Sparkle(heroPosition, MagnetColor, 16, 0.8f, 2.2f, 0.24f);
            effects.Text(heroPosition + Vector3.up * 2.4f, "NAM CHÂM!", MagnetColor, 1.2f, 1.1f);
        }

        // ------------------------------------------------------------------ present

        private void PresentWeapons(float realDelta)
        {
            PresentSpears(realDelta);
            PresentAxes();
            PresentAura(realDelta);
            PresentWave();
            PresentSpit();
            PresentM9Weapons();
        }

        private void PresentSpears(float realDelta)
        {
            float step = sim.IsEnded ? realDelta : animationDelta;
            for (int i = 0; i < spears.Length; i++)
            {
                SpearFx spear = spears[i];
                if (spear.Age >= SpearLifetime)
                {
                    continue;
                }
                spear.Age += step;
                if (spear.Age >= SpearLifetime)
                {
                    spear.Root.gameObject.SetActive(false);
                    continue;
                }

                float t = spear.Age / SpearLifetime;
                float grow = Mathf.Clamp01(t / 0.35f);
                float length = spear.Length * (0.35f + 0.65f * grow);
                float alpha = t < 0.35f ? 1f : 1f - (t - 0.35f) / 0.65f;
                spear.Material.color = new Color(spear.Color.r, spear.Color.g, spear.Color.b, alpha);
                spear.Root.SetPositionAndRotation(spear.Origin + spear.Direction * (length * 0.5f), Quaternion.LookRotation(spear.Direction, spear.Up));
                spear.Root.localScale = new Vector3((0.55f + 0.25f * (1f - t)) * spear.Width, 1f, length);
            }
        }

        private void PresentAxes()
        {
            float spin = (Time.unscaledTime * 1080f) % 360f;
            for (int i = 0; i < axes.Length; i++)
            {
                AxeView view = axes[i];
                if (!view.Active)
                {
                    continue;
                }
                // Blend on the circle so the axes keep their orbit radius between ticks.
                Vector3 offset = Vector3.Slerp(view.Previous, view.Current, interpolationAlpha);
                view.Root.position = heroDisplayPosition + offset + Vector3.up * AxeHeight;
                view.Spinner.localRotation = Quaternion.Euler(0f, -spin, 0f);
            }
        }

        private void PresentAura(float realDelta)
        {
            float radius = sim.Hero.Alive ? sim.AuraRadius : 0f;
            bool show = radius > 0f;
            if (auraRoot.gameObject.activeSelf != show)
            {
                auraRoot.gameObject.SetActive(show);
            }
            if (!show)
            {
                return;
            }

            auraPulse = Mathf.Max(0f, auraPulse - realDelta * 3.5f);
            float breathe = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f);
            auraRoot.SetPositionAndRotation(heroDisplayPosition + Vector3.up * 0.05f, Quaternion.Euler(0f, (Time.unscaledTime * 25f) % 360f, 0f));
            auraDisc.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
            float ringSize = radius * RingQuadPerRadius * (1f + 0.04f * auraPulse);
            auraRing.localScale = new Vector3(ringSize, 1f, ringSize);
            auraDiscMaterial.color = new Color(auraColor.r, auraColor.g * 0.8f, auraColor.b * 0.5f, 0.16f + 0.05f * breathe + 0.22f * auraPulse);
            auraRingMaterial.color = new Color(auraColor.r, auraColor.g, auraColor.b, 0.35f + 0.1f * breathe + 0.4f * auraPulse);
        }

        private void PresentWave()
        {
            bool show = waveCurrent > 0f;
            if (waveRoot.gameObject.activeSelf != show)
            {
                waveRoot.gameObject.SetActive(show);
            }
            if (!show)
            {
                return;
            }

            float radius = Mathf.Lerp(wavePrevious, waveCurrent, interpolationAlpha);
            float max = Mathf.Max(0.1f, sim.ShockwaveMaxRadius);
            float fade = 1f - Mathf.Clamp01(radius / max);
            Vector3 center = ArenaSpace.ToWorld(sim.ShockwaveCenter, 0.08f);
            float size = Mathf.Max(0.1f, radius) * RingQuadPerRadius;
            waveRoot.SetPositionAndRotation(center, Quaternion.identity);
            waveRoot.localScale = new Vector3(size, 1f, size);
            waveMaterial.color = new Color(waveColor.r, waveColor.g, waveColor.b, 0.25f + 0.75f * fade);
        }

        private void PresentSpit()
        {
            for (int i = 0; i < spitViews.Length; i++)
            {
                SpitView view = spitViews[i];
                if (view == null || view.Id < 0)
                {
                    continue;
                }
                view.Root.localPosition = Vector3.LerpUnclamped(view.Previous, view.Current, interpolationAlpha);
            }
        }
    }
}
