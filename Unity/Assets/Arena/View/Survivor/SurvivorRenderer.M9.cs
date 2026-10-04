using System.Collections.Generic;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PersonalArena.View
{
    /// <summary>Persistent, pooled presentation for the M9 weapon and skill state exposed by Core.</summary>
    public sealed partial class SurvivorRenderer
    {
        private static readonly Color BarrierColor = new Color(0.35f, 0.82f, 1f, 0.82f);
        private static readonly Color PoisonColor = new Color(0.35f, 0.9f, 0.2f, 0.58f);
        private static readonly Color TrapColor = new Color(0.75f, 0.72f, 0.62f, 0.9f);
        private static readonly Color WallColor = new Color(1f, 0.35f, 0.08f, 0.72f);
        private static readonly Color WhirlColor = new Color(1f, 0.72f, 0.25f, 0.9f);

        private readonly M9View[] boomerangViews = new M9View[SurvivorSim.BoomerangCapacity];
        private readonly M9View[] zoneViews = new M9View[SurvivorSim.ZoneCapacity];
        private readonly M9View[] trapViews = new M9View[SurvivorSim.TrapCapacity];
        private readonly M9View[] wallViews = new M9View[SurvivorSim.WallCapacity];
        private Transform m9Root;
        private Transform barrierRoot;
        private Transform whirlRoot;
        private Material boomerangMaterial;
        private Material poisonMaterial;
        private Material trapMaterial;
        private Material wallMaterial;
        private Material barrierMaterial;
        private Material whirlMaterial;
        private SkillKind rememberedM9Skill;
        private float rememberedM9SkillUntil;
        private bool chainStrikeValid;
        private Vector3 chainStrikePoint;

        private sealed class M9View
        {
            public Transform Root;
            public Transform Spinner;
            public TrailRenderer Trail;
            public bool Active;
            public float Emit;
            public Vector3 Previous;
            public Vector3 Current;
        }

        private void BuildM9Weapons()
        {
            m9Root = CreateChild("M9 Weapons", actorsRoot);
            boomerangMaterial = Own(CreateEmissive("Boomerang", new Color(0.35f, 0.9f, 0.78f), new Color(0.2f, 1.1f, 0.7f), 0.75f, 0.65f));
            poisonMaterial = Own(FxAssets.Create("Poison Pool", FxAssets.RadialGlow, true));
            poisonMaterial.color = PoisonColor;
            trapMaterial = Own(FxAssets.Create("Caltrop Trap", FxAssets.Ring, true));
            trapMaterial.color = TrapColor;
            wallMaterial = Own(FxAssets.Create("Fire Wall", FxAssets.RadialGlow, true));
            wallMaterial.color = WallColor;
            barrierMaterial = Own(FxAssets.Create("Barrier Ring", FxAssets.Ring, true));
            barrierMaterial.color = BarrierColor;
            whirlMaterial = Own(CreateEmissive("Whirlwind Blade", new Color(0.9f, 0.82f, 0.68f), WhirlColor, 0.9f, 0.9f));

            for (int i = 0; i < boomerangViews.Length; i++)
            {
                M9View view = new M9View();
                view.Root = CreateChild("Boomerang " + i, m9Root);
                view.Spinner = CreateChild("Spinner", view.Root);
                GameObject wingA = CreatePrimitive("Wing A", PrimitiveType.Cube, view.Spinner, boomerangMaterial);
                wingA.transform.localPosition = new Vector3(0.18f, 0f, 0f);
                wingA.transform.localScale = new Vector3(0.42f, 0.06f, 0.13f);
                GameObject wingB = CreatePrimitive("Wing B", PrimitiveType.Cube, view.Spinner, boomerangMaterial);
                wingB.transform.localPosition = new Vector3(0f, 0f, 0.18f);
                wingB.transform.localScale = new Vector3(0.13f, 0.06f, 0.42f);
                view.Trail = effects.CreateTrail(view.Root, 0f, new Color(0.3f, 1f, 0.75f, 0.7f), 0.32f, 0.24f);
                view.Trail.emitting = false;
                view.Root.gameObject.SetActive(false);
                boomerangViews[i] = view;
            }
            BuildGroundViews(zoneViews, "Poison Pool", poisonMaterial);
            BuildGroundViews(trapViews, "Caltrop Trap", trapMaterial);
            BuildGroundViews(wallViews, "Fire Wall", wallMaterial);
            BuildTrapSpikes();

            barrierRoot = CreateFlatQuad("Barrier", heroRoot, barrierMaterial);
            barrierRoot.localPosition = new Vector3(0f, 0.06f, 0f);
            barrierRoot.gameObject.SetActive(false);
            BuildBarrierBubble();

            whirlRoot = CreateChild("Whirlwind", heroRoot);
            for (int i = 0; i < 3; i++)
            {
                GameObject blade = CreatePrimitive("Blade " + i, PrimitiveType.Cube, whirlRoot, whirlMaterial);
                float angle = i * 120f * Mathf.Deg2Rad;
                blade.transform.localPosition = new Vector3(Mathf.Cos(angle) * 1.25f, 0.85f, Mathf.Sin(angle) * 1.25f);
                blade.transform.localRotation = Quaternion.Euler(0f, -i * 120f, 0f);
                blade.transform.localScale = new Vector3(0.12f, 0.05f, 0.85f);
                Renderer renderer = blade.GetComponent<Renderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            whirlRoot.gameObject.SetActive(false);
        }

        private void BuildGroundViews(M9View[] views, string label, Material material)
        {
            for (int i = 0; i < views.Length; i++)
            {
                M9View view = new M9View();
                view.Root = CreateFlatQuad(label + " " + i, m9Root, material);
                view.Root.gameObject.SetActive(false);
                views[i] = view;
            }
        }

        private void HideAllM9Weapons()
        {
            HideM9Views(boomerangViews, true);
            HideM9Views(zoneViews, false);
            HideM9Views(trapViews, false);
            HideM9Views(wallViews, false);
            if (barrierRoot != null) barrierRoot.gameObject.SetActive(false);
            if (barrierBubble != null) barrierBubble.gameObject.SetActive(false);
            if (whirlRoot != null) whirlRoot.gameObject.SetActive(false);
        }

        private static void HideM9Views(M9View[] views, bool trails)
        {
            for (int i = 0; i < views.Length; i++)
            {
                M9View view = views[i];
                if (view == null) continue;
                view.Active = false;
                view.Root.gameObject.SetActive(false);
                if (trails && view.Trail != null)
                {
                    view.Trail.emitting = false;
                    view.Trail.Clear();
                }
            }
        }

        private void SyncM9Weapons()
        {
            IReadOnlyList<SurvivorBoomerang> boomerangs = sim.Boomerangs;
            for (int i = 0; i < boomerangViews.Length; i++)
            {
                SurvivorBoomerang state = boomerangs[i];
                M9View view = boomerangViews[i];
                SetMovingM9View(view, state.Active, ArenaSpace.ToWorld(state.Position, 0.85f));
                if (state.Active)
                {
                    float scale = Mathf.Max(0.65f, state.Radius * 1.6f);
                    view.Spinner.localScale = Vector3.one * scale;
                }
            }

            IReadOnlyList<SurvivorZone> zones = sim.Zones;
            for (int i = 0; i < zoneViews.Length; i++)
            {
                SurvivorZone state = zones[i];
                SetGroundM9View(zoneViews[i], state.Active, state.Position, state.Radius * 2.5f, state.Radius * 2.5f, Vector2.up);
            }
            IReadOnlyList<SurvivorTrap> traps = sim.Traps;
            for (int i = 0; i < trapViews.Length; i++)
            {
                SurvivorTrap state = traps[i];
                SetGroundM9View(trapViews[i], state.Active, state.Position, state.Radius * 2.5f, state.Radius * 2.5f, Vector2.up);
            }
            IReadOnlyList<SurvivorWall> walls = sim.Walls;
            for (int i = 0; i < wallViews.Length; i++)
            {
                SurvivorWall state = walls[i];
                SetGroundM9View(wallViews[i], state.Active, state.Position, state.Width, state.Depth, new Vector2(state.Direction.X, state.Direction.Y));
            }

            int charges = sim.BarrierCharges;
            barrierRoot.gameObject.SetActive(charges > 0 && sim.Hero.Alive);
            PresentBarrierBubble(charges, charges > 0 && sim.Hero.Alive);
            if (charges > 0)
            {
                float pulse = 1f + 0.05f * Mathf.Sin(animationClock * 6f);
                barrierRoot.localScale = Vector3.one * (3.2f + 0.35f * charges) * pulse;
            }
            whirlRoot.gameObject.SetActive(sim.Whirling);
        }

        private static void SetMovingM9View(M9View view, bool active, Vector3 position)
        {
            if (!active)
            {
                if (view.Active)
                {
                    view.Active = false;
                    view.Trail.emitting = false;
                    view.Trail.Clear();
                    view.Root.gameObject.SetActive(false);
                }
                return;
            }
            if (!view.Active)
            {
                view.Active = true;
                view.Previous = position;
                view.Current = position;
                view.Root.position = position;
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

        private static void SetGroundM9View(M9View view, bool active, PersonalArena.Core.Vec2 position, float width, float depth, Vector2 direction)
        {
            view.Active = active;
            view.Root.gameObject.SetActive(active);
            if (!active) return;
            view.Root.position = ArenaSpace.ToWorld(position, 0.035f);
            view.Root.localScale = new Vector3(Mathf.Max(0.2f, width), 1f, Mathf.Max(0.2f, depth));
            if (direction.sqrMagnitude > 0.01f)
            {
                view.Root.rotation = Quaternion.Euler(0f, Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg, 0f);
            }
        }

        private void PresentM9Weapons()
        {
            float spin = (Time.unscaledTime * 900f) % 360f;
            for (int i = 0; i < boomerangViews.Length; i++)
            {
                M9View view = boomerangViews[i];
                if (!view.Active) continue;
                view.Root.position = Vector3.LerpUnclamped(view.Previous, view.Current, interpolationAlpha);
                view.Spinner.localRotation = Quaternion.Euler(0f, spin + i * 37f, 0f);
            }
            if (whirlRoot.gameObject.activeSelf)
            {
                whirlRoot.localRotation = Quaternion.Euler(0f, -spin, 0f);
            }
            PresentM9Areas(Time.unscaledDeltaTime);
        }

        private void BeginM9Events()
        {
            chainStrikeValid = false;
        }

        private void RememberM9Skill(SkillKind kind)
        {
            rememberedM9Skill = kind;
            rememberedM9SkillUntil = sim.Time + 2.5f;
        }

        private bool OnM9StrikeLanded(SurvivorEvent e, IReadOnlyList<SurvivorEvent> events, int eventIndex,
            Vector3 heroPosition, Vector3 point, float radius)
        {
            if (e.Id >= 0)
            {
                WeaponVisual visual = SurvivorViewLogic.WeaponVisualOf(e.Id);
                Color color = FxColor(e.Id, FireballColor);
                switch (visual)
                {
                    case WeaponVisual.Bomb:
                    case WeaponVisual.BombRing:
                    case WeaponVisual.FireballNova:
                        effects.Shockwave(point, color, radius * RingQuadPerRadius, 0.45f);
                        Blast(point, color, radius);
                        effects.Sparks(point + Vector3.up * 0.4f, Vector3.up, color, 9, 7f, 1.1f);
                        return true;
                    case WeaponVisual.Retaliate:
                        effects.Shockwave(heroPosition, color, radius * RingQuadPerRadius, 0.4f);
                        effects.Sparks(heroPosition + Vector3.up * 0.6f, Vector3.up, color, 10, 6f, 1f);
                        // Spikes burst out of the ground around the hero.
                        effects.Crystals(heroPosition, SpikeColor, 9, Mathf.Max(1.2f, radius * 0.7f), 1.1f, 0.5f, 0.16f);
                        effects.Shards(heroPosition, SpikeColor, 8, 3.5f, 0.4f);
                        return true;
                    case WeaponVisual.Purge:
                        effects.Shockwave(point, Color.white, radius * RingQuadPerRadius, 0.7f);
                        effects.Rune(point, new Color(1f, 0.95f, 0.65f), radius * 2.4f, 0.9f);
                        effects.Flash(point + Vector3.up, new Color(1f, 0.95f, 0.65f), radius * 2f, 0.3f);
                        effects.Sparkle(point, Color.white, 20, radius * 0.8f, 2f, 0.24f);
                        return true;
                    case WeaponVisual.Stone:
                        StartStreak(heroPosition + Vector3.up * 0.9f, (point - heroPosition).normalized,
                            Mathf.Max(0.5f, Vector3.Distance(heroPosition, point)), color, 0.7f, Vector3.up, true);
                        effects.Flash(point + Vector3.up * 0.4f, color, 1.5f, 0.16f);
                        return true;
                    case WeaponVisual.Bounce:
                        effects.Sparkle(point, color, 5, 0.3f, 1f, 0.14f);
                        return true;
                }
                return false;
            }

            SkillDef skill = M9SkillInStep(events);
            SkillKind kind = skill != null ? skill.Kind
                : sim.Time <= rememberedM9SkillUntil ? rememberedM9Skill
                : SkillKind.None;
            if (skill != null)
            {
                RememberM9Skill(kind);
            }
            switch (kind)
            {
                case SkillKind.AreaBurst when skill != null && skill.Id == "war-cry":
                    effects.Shockwave(heroPosition, WhirlColor, radius * RingQuadPerRadius, 0.55f);
                    effects.Rune(heroPosition, WhirlColor, radius * 2.2f, 0.6f);
                    effects.Sparks(heroPosition + Vector3.up * 0.5f, Vector3.up, WhirlColor, 14, 8f, 1.2f);
                    return true;
                case SkillKind.Leap:
                    effects.Shockwave(point, WhirlColor, radius * RingQuadPerRadius, 0.5f);
                    effects.Puff(point, DustColor, 12, 1f, radius, 0.8f, 0.25f);
                    effects.Shards(point, new Color(0.55f, 0.5f, 0.45f), 10, 3.2f, 0.3f);
                    effects.Decal(point, new Color(0.06f, 0.05f, 0.04f, 0.7f), radius * 2.2f, 4f);
                    return true;
                case SkillKind.Whirlwind:
                    effects.Twirl(heroPosition + Vector3.up * 0.7f, WhirlColor, radius * 2.3f, 0.3f);
                    effects.Slash(heroPosition + Vector3.up * 0.8f, sim.Time * 900f, WhirlColor, false, radius, 0.12f);
                    return true;
                case SkillKind.Trap:
                    effects.Sparks(point + Vector3.up * 0.15f, Vector3.up, TrapColor, 4, 2f, 0.8f);
                    effects.Shards(point, SpikeColor, 5, 2.4f, 0.22f);
                    return true;
                case SkillKind.Wall:
                    effects.Flash(point + Vector3.up * 0.35f, WallColor, Mathf.Max(1f, e.Extra), 0.14f);
                    return true;
                case SkillKind.Chain:
                {
                    Vector3 from = chainStrikeValid ? chainStrikePoint : heroPosition + Vector3.up * 0.9f;
                    Vector3 to = point + Vector3.up * 0.7f;
                    Vector3 delta = to - from;
                    if (delta.sqrMagnitude > 0.01f)
                    {
                        effects.Bolt(from, to, LightningColor, 0.8f);
                        MarkElement(to, 0.8f, ElementLightning);
                        effects.Sparks(to, Vector3.up, LightningColor, 4, 5f, 1f);
                    }
                    effects.Flash(to, LightningColor, 1.4f, 0.14f);
                    chainStrikePoint = to;
                    chainStrikeValid = true;
                    return true;
                }
                default:
                    return false;
            }
        }

        private SkillDef M9SkillInStep(IReadOnlyList<SurvivorEvent> events)
        {
            SkillDef[] skills = sim.Config.ClassDef != null ? sim.Config.ClassDef.ActiveSkills : null;
            if (skills == null) return null;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type != SurvivorEventType.SkillUsed) continue;
                int slot = (int)events[i].Value;
                if (slot >= 0 && slot < skills.Length)
                {
                    SkillDef skill = skills[slot];
                    if (skill != null && (skill.Kind == SkillKind.Leap || skill.Kind == SkillKind.Whirlwind ||
                        skill.Kind == SkillKind.Trap || skill.Kind == SkillKind.Wall || skill.Kind == SkillKind.Chain ||
                        skill.Id == "war-cry"))
                    {
                        return skill;
                    }
                }
            }
            return null;
        }
    }
}
