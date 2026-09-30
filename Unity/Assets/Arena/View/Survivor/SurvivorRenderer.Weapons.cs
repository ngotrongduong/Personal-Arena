using System.Collections.Generic;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PersonalArena.View
{
    /// <summary>
    /// M4C weapon and loot visuals: spear fans, orbiting axes, the aura disc, the shockwave ring,
    /// Spitter spit and the chest / magnet pickups' bursts. Everything is pooled and built once.
    /// </summary>
    public sealed partial class SurvivorRenderer
    {
        private const int SpearPool = 6;
        private const int AxePool = 3;
        private const int SpitPrewarm = 16;
        private const float SpearLifetime = 0.24f;
        private const float SpearLength = 5f;
        private const float SpearHeight = 0.9f;
        private const float AxeHeight = 0.95f;
        private const float SpitHeight = 0.9f;
        // The ring texture peaks at 80 % of its half size, so a ring of radius R needs a quad of 2.5 R.
        private const float RingQuadPerRadius = 2.5f;

        private static readonly Color SpearColor = new Color(0.55f, 0.85f, 1f, 1f);
        private static readonly Color AxeTrailColor = new Color(1f, 0.55f, 0.35f, 0.55f);
        private static readonly Color AuraColor = new Color(1f, 0.88f, 0.4f, 1f);
        private static readonly Color WaveColor = new Color(0.6f, 0.68f, 1f, 1f);
        private static readonly Color SpitColor = new Color(0.55f, 1f, 0.3f, 1f);
        private static readonly Color MagnetColor = new Color(0.5f, 0.7f, 1f, 1f);

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
        private int nextSpear;

        private sealed class SpearFx
        {
            public Transform Root;
            public Material Material;
            public Vector3 Origin;
            public Vector3 Direction;
            public float Length;
            public float Age = SpearLifetime;
        }

        private sealed class AxeView
        {
            public Transform Root;
            public Transform Spinner;
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
            GameObject root = new GameObject("Orbit Axe");
            root.transform.SetParent(weaponRoot, false);
            view.Root = root.transform;
            view.Spinner = CreateChild("Spinner", view.Root);

            if (survivorArt != null && survivorArt.AxeProp != null)
            {
                GameObject prop = Instantiate(survivorArt.AxeProp, view.Spinner, false);
                prop.name = survivorArt.AxeProp.name;
                // The KayKit axe stands along +Y; lay it flat so it sweeps like a thrown blade.
                prop.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                prop.transform.localPosition = new Vector3(0f, 0f, -0.25f);
                prop.transform.localScale = Vector3.one * 1.35f;
            }
            else
            {
                GameObject handle = CreatePrimitive("Handle", PrimitiveType.Cylinder, view.Spinner, hammerHandleMaterial);
                handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                handle.transform.localScale = new Vector3(0.08f, 0.32f, 0.08f);
                GameObject blade = CreatePrimitive("Blade", PrimitiveType.Cube, view.Spinner, axeFallbackMaterial);
                blade.transform.localPosition = new Vector3(0.18f, 0f, 0.26f);
                blade.transform.localScale = new Vector3(0.36f, 0.06f, 0.3f);
            }
            Renderer[] renderers = view.Spinner.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            }

            view.Trail = effects.CreateTrail(view.Root, 0f, AxeTrailColor, 0.55f, 0.18f);
            view.Trail.emitting = false;
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
            if (auraRoot != null)
            {
                auraRoot.gameObject.SetActive(false);
            }
            if (waveRoot != null)
            {
                waveRoot.gameObject.SetActive(false);
            }
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

        private void SyncWeapons()
        {
            if (sim == null)
            {
                return;
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

        /// <summary>Weapon ids 1, 2, 4 and 5 (the sweep and the hammer stay in <see cref="OnWeaponFired"/>).</summary>
        private void OnNewWeaponFired(SurvivorEvent e, Vector3 heroPosition, Vector3 direction)
        {
            switch (e.Id)
            {
                case 1:
                {
                    PlayHeroOneShot((strikeCount & 1) == 1 ? HeroStrikeB : HeroStrikeA, 2f, false);
                    strikeCount++;
                    int n = Mathf.Max(1, Mathf.RoundToInt(e.Extra));
                    float length = SpearLength * Mathf.Max(0.1f, sim.DerivedStats.AreaMul);
                    for (int k = 0; k < n; k++)
                    {
                        float angle = SurvivorCatalog.ThrustAngleOffset(k, n);
                        // Positive offsets turn counter-clockwise in sim space (x, y) = world (x, z).
                        Vector3 spearDirection = Quaternion.AngleAxis(-angle * Mathf.Rad2Deg, Vector3.up) * direction;
                        StartSpear(heroPosition, spearDirection, length);
                    }
                    break;
                }
                case 2:
                    effects.Sparkle(heroPosition, AxeTrailColor, 8, 1.6f, 1.2f, 0.2f);
                    break;
                case 4:
                    auraPulse = 1f;
                    break;
                case 5:
                {
                    Vector3 center = ArenaSpace.ToWorld(e.Point);
                    effects.Flash(center + Vector3.up * 0.6f, WaveColor, 3f, 0.18f);
                    effects.Puff(center, DustColor, 10, 0.9f, 2.4f, 0.6f, 0.3f);
                    PlayHeroOneShot(HeroKick, 1.6f, false);
                    break;
                }
            }
        }

        private void StartSpear(Vector3 heroPosition, Vector3 direction, float length)
        {
            SpearFx spear = spears[nextSpear];
            nextSpear = (nextSpear + 1) % spears.Length;
            spear.Origin = heroPosition + Vector3.up * SpearHeight;
            spear.Direction = direction;
            spear.Length = length;
            spear.Age = 0f;
            spear.Root.gameObject.SetActive(true);
            if (sparksLeft > 0)
            {
                sparksLeft--;
                effects.Sparks(spear.Origin + direction * length, direction, SpearColor, 5, 6f, 0.7f);
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
                spear.Material.color = new Color(SpearColor.r, SpearColor.g, SpearColor.b, alpha);
                spear.Root.SetPositionAndRotation(spear.Origin + spear.Direction * (length * 0.5f), Quaternion.LookRotation(spear.Direction, Vector3.up));
                spear.Root.localScale = new Vector3(0.55f + 0.25f * (1f - t), 1f, length);
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
            auraDiscMaterial.color = new Color(AuraColor.r, AuraColor.g * 0.8f, AuraColor.b * 0.5f, 0.16f + 0.05f * breathe + 0.22f * auraPulse);
            auraRingMaterial.color = new Color(AuraColor.r, AuraColor.g, AuraColor.b, 0.35f + 0.1f * breathe + 0.4f * auraPulse);
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
            waveMaterial.color = new Color(WaveColor.r, WaveColor.g, WaveColor.b, 0.25f + 0.75f * fade);
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
