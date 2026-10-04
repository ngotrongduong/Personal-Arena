using PersonalArena.Core.Survivor;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// Evolved weapons look like what they became, not like a gold copy of the base weapon: each one has an element
    /// (<see cref="SurvivorEvolutionStyles"/>) that decides its colour, what it leaves behind while flying and the
    /// mark it makes where it really hits. Viewer only: nothing here reads back into the simulation.
    /// </summary>
    public sealed partial class SurvivorRenderer
    {
        private const int EvolutionMarksPerFrame = 6;
        private const float EvolutionTrailRate = 20f;

        private static readonly Color ScorchColor = new Color(0.05f, 0.04f, 0.03f, 0.7f);
        private static readonly Color CrackColor = new Color(0.12f, 0.09f, 0.06f, 0.75f);

        private readonly System.Collections.Generic.Dictionary<int, Gradient> evolutionTrails =
            new System.Collections.Generic.Dictionary<int, Gradient>();
        private int evolutionMarkFrame = -1;
        private int evolutionMarksLeft;
        private int boomerangLook = -1;

        /// <summary>The colour of an evolved weapon; the chaos one never keeps the same colour twice.</summary>
        private static Color EvolutionColor(int catalogIndex)
        {
            EvolutionStyle style = SurvivorEvolutionStyles.Of(catalogIndex);
            return style.Element == EvolutionElement.Chaos ? Color.HSVToRGB(Random.value, 0.75f, 1f) : style.Color;
        }

        private Gradient EvolutionTrailGradient(int catalogIndex)
        {
            if (!evolutionTrails.TryGetValue(catalogIndex, out Gradient gradient))
            {
                Color color = SurvivorEvolutionStyles.Of(catalogIndex).Color;
                color.a = 0.7f;
                gradient = TrailGradient(color);
                evolutionTrails[catalogIndex] = gradient;
            }
            return gradient;
        }

        /// <summary>One piece of what an evolved weapon leaves behind in the air (called many times along its path).</summary>
        private void EvolutionTrail(int catalogIndex, Vector3 position)
        {
            EvolutionStyle style = SurvivorEvolutionStyles.Of(catalogIndex);
            Color color = style.Color;
            switch (style.Element)
            {
                case EvolutionElement.Lightning:
                    if (Random.value < 0.45f)
                    {
                        Vector3 jump = new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-0.4f, 0.5f), Random.Range(-0.8f, 0.8f));
                        effects.Bolt(position, position + jump, color, 0.3f);
                    }
                    else
                    {
                        effects.Sparkle(position, color, 1, 0.2f, 0.3f, 0.2f);
                    }
                    break;
                case EvolutionElement.Fire:
                case EvolutionElement.Meteor:
                    effects.Flame(position - Vector3.up * 0.3f, color, 0.6f, 0.3f, false);
                    break;
                case EvolutionElement.Ice:
                    effects.Shards(position - Vector3.up * 0.3f, color, 1, 0.8f, 0.28f);
                    break;
                case EvolutionElement.Wind:
                    if (Random.value < 0.3f)
                    {
                        effects.Twirl(position, color, 1.1f, 0.25f);
                    }
                    else
                    {
                        effects.Sparkle(position, color, 1, 0.3f, 0.1f, 0.16f);
                    }
                    break;
                case EvolutionElement.Earth:
                    effects.Shards(position - Vector3.up * 0.3f, color, 1, 1.2f, 0.22f);
                    break;
                case EvolutionElement.Shadow:
                case EvolutionElement.Poison:
                    if (Random.value < 0.35f)
                    {
                        effects.Smoke(position, new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, 0.4f), 1, 0.55f, 0.5f);
                    }
                    else
                    {
                        effects.Sparkle(position, color, 1, 0.2f, 0.5f, 0.2f);
                    }
                    break;
                case EvolutionElement.Chaos:
                    effects.Sparkle(position, Color.HSVToRGB(Random.value, 0.75f, 1f), 1, 0.25f, 0.6f, 0.26f);
                    break;
                default:
                    // Holy, arcane, star: motes of light rising off the weapon.
                    effects.Sparkle(position, color, 1, 0.22f, 0.9f, 0.24f);
                    break;
            }
        }

        /// <summary>The element along a thrust, beam or slash: a few trail pieces spread over the real reach.</summary>
        private void EvolutionLine(int catalogIndex, Vector3 origin, Vector3 direction, float length)
        {
            if (!TakeEvolutionMark())
            {
                return;
            }
            const int Steps = 5;
            for (int k = 1; k <= Steps; k++)
            {
                EvolutionTrail(catalogIndex, origin + direction * (length * k / Steps));
            }
        }

        /// <summary>
        /// The mark of an evolved weapon where it really hits, sized to the real radius: one element effect on the
        /// ground, never on the hero's body.
        /// </summary>
        private void EvolutionMark(int catalogIndex, Vector3 point, float radius)
        {
            EvolutionStyle style = SurvivorEvolutionStyles.Of(catalogIndex);
            if (style.Element == EvolutionElement.None || !TakeEvolutionMark())
            {
                return;
            }
            point.y = 0f;
            radius = Mathf.Max(0.5f, radius);
            Color color = style.Color;
            switch (style.Element)
            {
                case EvolutionElement.Lightning:
                    // A cage of bolts on the edge of the area.
                    for (int k = 0; k < 3; k++)
                    {
                        float angle = (k * 120f + Random.Range(0f, 60f)) * Mathf.Deg2Rad;
                        Vector3 land = point + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (radius * 0.75f);
                        effects.Bolt(land + new Vector3(0.4f, LightningHeight, 0.2f), land, color, 0.8f);
                    }
                    break;
                case EvolutionElement.Fire:
                {
                    int flames = Mathf.Clamp(Mathf.RoundToInt(radius * 2f), 3, 8);
                    for (int k = 0; k < flames; k++)
                    {
                        Vector2 spot = Random.insideUnitCircle * (radius * 0.8f);
                        effects.Flame(point + new Vector3(spot.x, 0f, spot.y), color, Mathf.Clamp(radius * 0.6f, 0.7f, 1.6f), 0.7f, false);
                    }
                    effects.Decal(point, ScorchColor, radius * 2f, 3f);
                    break;
                }
                case EvolutionElement.Meteor:
                {
                    // The rock falls out of the sky onto the blast.
                    Vector3 top = point + new Vector3(-3f, 9f, -3f);
                    Vector3 fall = point - top;
                    StartStreak(top, fall.normalized, fall.magnitude, color, 0.9f, StreakUp(fall), false);
                    effects.Shards(point, new Color(0.35f, 0.25f, 0.2f), 8, 3.5f, 0.5f);
                    effects.Decal(point, ScorchColor, radius * 2.2f, 4f);
                    break;
                }
                case EvolutionElement.Ice:
                    effects.Crystals(point, color, Mathf.Clamp(Mathf.RoundToInt(radius * 4f), 5, 18), radius * 0.9f, 2.1f, 3f);
                    effects.Decal(point, FrostMarkColor, radius * 2.2f, 4f);
                    break;
                case EvolutionElement.Wind:
                    effects.Twirl(point + Vector3.up * 0.4f, color, radius * 2.3f, 0.5f);
                    break;
                case EvolutionElement.Earth:
                    effects.Shards(point, color, Mathf.Clamp(Mathf.RoundToInt(radius * 4f), 6, 16), 3.5f, 0.5f);
                    effects.Crystals(point, new Color(0.45f, 0.36f, 0.28f), Mathf.Clamp(Mathf.RoundToInt(radius * 3f), 4, 12), radius * 0.85f, 1.1f, 1.6f, 0.5f);
                    effects.Decal(point, CrackColor, radius * 2.1f, 4f);
                    break;
                case EvolutionElement.Holy:
                    effects.Rune(point, color, radius * 2.3f, 0.9f);
                    effects.Sparkle(point, color, Mathf.Clamp(Mathf.RoundToInt(radius * 4f), 6, 16), radius * 0.8f, 2.6f, 0.24f);
                    break;
                case EvolutionElement.Arcane:
                    effects.Rune(point, color, radius * 2.3f, 0.8f);
                    effects.Shockwave(point, color, radius * RingQuadPerRadius, 0.4f);
                    break;
                case EvolutionElement.Shadow:
                    effects.Smoke(point + Vector3.up * 0.3f, new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, 0.45f),
                        Mathf.Clamp(Mathf.RoundToInt(radius * 2f), 2, 6), Mathf.Max(0.8f, radius * 0.8f), 0.9f);
                    effects.Shockwave(point, color, radius * RingQuadPerRadius, 0.3f);
                    break;
                case EvolutionElement.Poison:
                    effects.Smoke(point + Vector3.up * 0.2f, new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, 0.4f),
                        Mathf.Clamp(Mathf.RoundToInt(radius * 2f), 2, 6), Mathf.Max(0.8f, radius * 0.7f), 1.2f);
                    effects.Decal(point, new Color(color.r * 0.4f, color.g * 0.5f, color.b * 0.2f, 0.6f), radius * 2f, 3f);
                    break;
                case EvolutionElement.Star:
                    effects.Sparkle(point, color, Mathf.Clamp(Mathf.RoundToInt(radius * 5f), 6, 18), radius * 0.9f, 1.8f, 0.26f);
                    effects.Sparkle(point, Color.white, 5, radius * 0.5f, 2.4f, 0.18f);
                    break;
                case EvolutionElement.Chaos:
                    effects.Shockwave(point, Color.HSVToRGB(Random.value, 0.75f, 1f), radius * RingQuadPerRadius, 0.3f);
                    for (int k = 0; k < 6; k++)
                    {
                        effects.Sparkle(point, Color.HSVToRGB(Random.value, 0.75f, 1f), 1, radius * 0.7f, 1.6f, 0.24f);
                    }
                    break;
            }
        }

        private bool TakeEvolutionMark()
        {
            if (Time.frameCount != evolutionMarkFrame)
            {
                evolutionMarkFrame = Time.frameCount;
                evolutionMarksLeft = EvolutionMarksPerFrame;
            }
            if (evolutionMarksLeft <= 0)
            {
                return false;
            }
            evolutionMarksLeft--;
            return true;
        }

        /// <summary>The evolved boomerang gets its element's trail colour as soon as it is owned.</summary>
        private void SetBoomerangLook(int catalogIndex)
        {
            if (catalogIndex == boomerangLook)
            {
                return;
            }
            boomerangLook = catalogIndex;
            Gradient gradient = SurvivorViewLogic.IsEvolution(catalogIndex)
                ? EvolutionTrailGradient(catalogIndex)
                : TrailGradient(new Color(0.3f, 1f, 0.75f, 0.7f));
            for (int i = 0; i < boomerangViews.Length; i++)
            {
                boomerangViews[i].Trail.colorGradient = gradient;
            }
        }

        /// <summary>-fxDemo: every evolution's mark on a grid, so one screenshot shows that they all differ.</summary>
        private void PlayEvolutionDemo(Vector3 center)
        {
            int shown = 0;
            evolutionMarkFrame = -1;
            for (int index = 0; index < SurvivorCatalog.CatalogSize; index++)
            {
                if (!SurvivorViewLogic.IsEvolution(index))
                {
                    continue;
                }
                Vector3 at = center + new Vector3(-13.2f + (shown % 12) * 2.4f, 0f, -4.5f - (shown / 12) * 2.6f);
                evolutionMarksLeft = EvolutionMarksPerFrame;
                EvolutionMark(index, at, 1.1f);
                shown++;
            }
        }
    }
}
