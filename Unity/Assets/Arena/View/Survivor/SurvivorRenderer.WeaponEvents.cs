using System.Collections.Generic;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PersonalArena.View
{
    /// <summary>Effects for the events of the newer weapons: shots, strikes, streaks, chests and the magnet.</summary>
    public sealed partial class SurvivorRenderer
    {
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
                        if (evolved)
                        {
                            EvolutionLine(e.Id, heroPosition + Vector3.up * SpearHeight, spearDirection, length);
                        }
                    }
                    break;
                }
                case WeaponVisual.OrbitAxe:
                case WeaponVisual.FireOrb:
                case WeaponVisual.OrbitKnife:
                    // The orbiting blades are the effect; nothing extra on the hero.
                    break;
                case WeaponVisual.Aura:
                case WeaponVisual.HolyField:
                    auraPulse = 1f;
                    if (evolved && sim.AuraRadius > 0f)
                    {
                        // The holy aura writes its circle on the ground it really burns.
                        EvolutionMark(e.Id, heroPosition, sim.AuraRadius);
                    }
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
                        effects.Shards(center, FrostColor, 10, 3f, 0.55f);
                        effects.Crystals(center, FrostColor, 9, 2.6f, 1.4f, 2.6f);
                        MarkElement(center, 4f, ElementIce);
                        effects.Decal(center, FrostMarkColor, 7f, 4f);
                        if (!StoreArea(StoreFx.FreezeCircle, center, 3.5f, 1.6f))
                        {
                            effects.AreaFill(center, FrostColor, 3.5f, 1f);
                        }
                        PlayHeroOneShot(HeroCast, 1.8f, false);
                    }
                    else
                    {
                        effects.Puff(center, DustColor, 10, 0.9f, 2.4f, 0.6f, 0.3f);
                        if (!StoreArea(StoreFx.GroundBlast, center, 3f, 1.6f))
                        {
                            effects.AreaFill(center, color, 3f, 0.7f);
                        }
                        PlayHeroOneShot(HeroKick, 1.6f, false);
                    }
                    if (evolved)
                    {
                        EvolutionMark(e.Id, center, visual == WeaponVisual.FrostNova ? 3.5f : 3f);
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
                    effects.Shockwave(heroPosition, evolved ? EvolutionColor(e.Id) : BarrierColor, 4.4f, 0.4f);
                    if (evolved)
                    {
                        EvolutionMark(e.Id, heroPosition, 1.9f);
                    }
                    break;
                case WeaponVisual.Boomerang:
                    PlayHeroOneShot(HeroThrow, 2f, false);
                    break;
                case WeaponVisual.Zone:
                    PlayHeroOneShot(HeroThrow, 1.8f, false);
                    effects.Shockwave(ArenaSpace.ToWorld(e.Point), evolved ? EvolutionColor(e.Id) : PoisonColor, 3f, 0.35f);
                    break;
                case WeaponVisual.Freeze:
                    PlayHeroOneShot(HeroCast, 1.9f, false);
                    effects.Shockwave(heroPosition, FrostColor, 14f * RingQuadPerRadius, 0.65f);
                    effects.Flash(heroPosition + Vector3.up, FrostColor, 5f, 0.25f);
                    effects.Shards(heroPosition, FrostColor, 18, 4.5f, 0.6f);
                    effects.Crystals(heroPosition, FrostColor, 22, 9f, 1.6f, 3f);
                    MarkElement(heroPosition, 14f, ElementIce);
                    effects.Decal(heroPosition, FrostMarkColor, 20f, 5f);
                    effects.AreaFill(heroPosition, FrostColor, 14f, 1.4f);
                    StoreArea(StoreFx.SnowArea, heroPosition, 8f, 2.2f);
                    if (evolved)
                    {
                        // The eternal corridor: a clock face over the whole frozen field.
                        EvolutionMark(e.Id, heroPosition, 9f);
                    }
                    break;
                case WeaponVisual.Purge:
                    PlayHeroOneShot(HeroCast, 2f, false);
                    break;
                case WeaponVisual.BombRing:
                    PlayHeroOneShot(HeroThrow, 2f, false);
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
                    effects.Shards(heroPosition, FrostColor, 16, 4f, 0.6f);
                    effects.Crystals(heroPosition, FrostColor, 16, radius * 0.9f, 1.6f, 3f);
                    MarkElement(heroPosition, radius, ElementIce);
                    effects.Decal(heroPosition, FrostMarkColor, radius * 2.4f, 5f);
                    // No rune here: the frost burst is ice only (ring, crystals, snow).
                    if (!StoreArea(StoreFx.SnowArea, heroPosition, radius, 2f))
                    {
                        effects.AreaFill(heroPosition, FrostColor, radius, 1.2f);
                    }
                }
                else
                {
                    Blast(point, FireballColor, radius);
                }
                return;
            }

            WeaponVisual visual = SurvivorViewLogic.WeaponVisualOf(e.Id);
            bool evolved = SurvivorViewLogic.IsEvolution(e.Id);
            if (visual == WeaponVisual.ArrowRain)
            {
                Color color = FxColor(e.Id, LightningColor);
                // A rain over the whole blast area: only arrows, falling steeply on a spiral that fills the radius.
                int arrows = Mathf.Clamp(Mathf.RoundToInt(radius * (evolved ? 5f : 4f)), 6, 16);
                for (int k = 0; k < arrows; k++)
                {
                    float a = k * 2.399963f + e.Point.X; // golden angle: evenly spread without a pattern
                    float r = radius * Mathf.Sqrt((k + 0.5f) / arrows) * 0.9f;
                    Vector3 land = point + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    Vector3 top = land + new Vector3(-0.8f, 4.5f, -0.8f);
                    Vector3 fall = land - top;
                    StartStreak(top, fall.normalized, fall.magnitude, color, evolved ? 0.5f : 0.38f, StreakUp(fall), false);
                    // The arrow stays stuck in the ground for a moment.
                    effects.Crystals(land, ArrowShaftColor, 1, 0f, 0.9f, 1.3f, 0.07f);
                }
                if (evolved)
                {
                    EvolutionMark(e.Id, point, radius);
                }
            }
            else
            {
                Color color = FxColor(e.Id, LightningColor);
                // Lightning: bolts from the sky (no orb), a flash and a ring of the real radius.
                Vector3 top = point + Vector3.up * LightningHeight;
                effects.Bolt(top, point, color, evolved ? 1.7f : 1.3f);
                effects.Bolt(top + new Vector3(0.6f, 0f, 0.3f), point, color, 0.7f);
                MarkElement(point, radius, ElementLightning);
                effects.Decal(point, new Color(0.05f, 0.05f, 0.08f, 0.6f), radius * 1.6f, 2.5f);
                effects.Flash(point + Vector3.up * 0.8f, color, radius * 2.8f, 0.25f);
                effects.Shockwave(point, color, radius * RingQuadPerRadius, 0.35f);
                effects.AreaFill(point, color, radius, 0.7f);
                effects.Bolt(point + new Vector3(-radius, 0.3f, 0f), point + new Vector3(radius, 0.3f, 0f), color, 0.6f);
                effects.Bolt(point + new Vector3(0f, 0.3f, -radius), point + new Vector3(0f, 0.3f, radius), color, 0.6f);
                if (evolved)
                {
                    EvolutionMark(e.Id, point, radius);
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
            if (!StoreArea(StoreFx.RainbowExplode, point + Vector3.up * 0.6f, 2.2f, 2f))
            {
                effects.Sparkle(point, GoldColor, 26, 1f, 3f, 0.3f);
            }
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
            effects.Shockwave(heroPosition, MagnetColor, 26f, 0.9f);
            effects.Shockwave(heroPosition, MagnetColor, 14f, 0.7f);
            effects.Shockwave(heroPosition, Color.white, 7f, 0.5f);
            effects.Twirl(heroPosition + Vector3.up * 0.4f, MagnetColor, 16f, 0.8f);
            effects.Twirl(heroPosition + Vector3.up * 0.9f, Color.white, 8f, 0.6f);
            effects.Rune(heroPosition, MagnetColor, 13f, 1f);
            effects.AreaFill(heroPosition, MagnetColor, 9f, 1.1f);
            effects.Flash(heroPosition + Vector3.up, MagnetColor, 6f, 0.3f);
            // Streaks rushing in from all around, like everything being pulled to the hero.
            for (int i = 0; i < 20; i++)
            {
                float angle = i * Mathf.PI * 2f / 20f;
                Vector3 rim = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float reach = 6f + 4f * (i % 3);
                effects.Sparks(heroPosition + rim * reach + Vector3.up * 0.6f, -rim, MagnetColor, 3, reach * 2.6f, 0.08f);
            }
            effects.Sparkle(heroPosition, MagnetColor, 34, 2.4f, 2.6f, 0.3f);
            effects.Text(heroPosition + Vector3.up * 2.4f, "NAM CHÂM!", MagnetColor, 1.2f, 1.1f);
        }
    }
}
