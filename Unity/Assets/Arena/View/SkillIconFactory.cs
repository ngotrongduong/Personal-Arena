using System.Collections.Generic;
using PersonalArena.Core;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// Builds the HUD skill icons at runtime: a colored rounded badge drawn in code, with the skill's
    /// white glyph from game-icons.net on top (ThirdParty/GameIcons, CC BY 3.0, see CREDITS.md).
    /// </summary>
    public static class SkillIconFactory
    {
        public const int Size = 128;
        /// <summary>Resources folder of the glyphs: one white-on-transparent PNG named after each skill id.</summary>
        public const string GlyphFolder = "SkillIcons/";
        private const float Pixel = 2f / Size;
        // Half-size of the glyph box in badge space (the badge itself reaches 0.94).
        private const float GlyphHalf = 0.64f;
        private const int Supersample = 4;
        private static readonly string[] SlotDefaults = { "spear-strike", "kick", "shield-block", "dash" };
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>Icon key for a skill; an empty slot falls back to the warrior skill of that slot.</summary>
        public static string KeyFor(SkillDef skill, int index)
        {
            return skill != null && !string.IsNullOrEmpty(skill.Id)
                ? skill.Id
                : SlotDefaults[Mathf.Clamp(index, 0, SlotDefaults.Length - 1)];
        }

        /// <summary>Theme color of a skill, used for its badge, HUD accent and glow.</summary>
        public static Color ColorFor(SkillDef skill, int index)
        {
            switch (KeyFor(skill, index))
            {
                case "spear-strike": return new Color(0.78f, 0.84f, 1f);
                case "kick": return new Color(1f, 0.6f, 0.25f);
                case "shield-block": return new Color(0.4f, 0.7f, 1f);
                case "dash": return new Color(0.35f, 0.9f, 1f);
                case "fireball": return new Color(1f, 0.45f, 0.15f);
                case "frost-nova": return new Color(0.55f, 0.85f, 1f);
                case "mana-shield": return new Color(0.72f, 0.48f, 1f);
                case "blink": return new Color(0.9f, 0.52f, 1f);
                case "arrow": return new Color(0.55f, 0.88f, 0.4f);
                case "piercing-arrow": return new Color(1f, 0.85f, 0.35f);
                case "leap-back": return new Color(0.4f, 0.92f, 0.72f);
                case "concussive-arrow": return new Color(1f, 0.55f, 0.35f);
                default: return new Color(0.8f, 0.82f, 0.9f);
            }
        }

        public static Sprite IconFor(SkillDef skill, int index)
        {
            return IconForKey(KeyFor(skill, index), ColorFor(skill, index));
        }

        /// <summary>Icon for any glyph key in <see cref="GlyphFolder"/> (e.g. a survivor item id) on a badge of <paramref name="theme"/>.</summary>
        public static Sprite IconForKey(string key, Color theme)
        {
            key = key ?? string.Empty;
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "Skill Icon " + key,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(DrawPixels(key, theme));
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
            sprite.name = texture.name;
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Renders one icon into Size x Size pixels (row 0 is the bottom).</summary>
        public static Color32[] DrawPixels(string key, Color theme)
        {
            float[] glyph = GlyphMask(key);
            float[] outlineMask = Dilate(glyph, 2);
            float[] glowMask = Blur(Blur(glyph, 4), 4);
            Color32[] pixels = new Color32[Size * Size];
            Color glow = Color.Lerp(theme, Color.white, 0.35f);
            Color outline = theme * 0.12f;
            outline.a = 1f;
            Color tint = Color.Lerp(Color.white, theme, 0.5f);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    Vector2 p = new Vector2((x + 0.5f) * Pixel - 1f, (y + 0.5f) * Pixel - 1f);
                    float badge = RoundedBox(p, new Vector2(0.94f, 0.94f), 0.24f);
                    float badgeAlpha = Coverage(badge);
                    if (badgeAlpha <= 0f)
                    {
                        pixels[i] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    // Badge: dark gradient lit from above with a bright rim.
                    float light = Mathf.Clamp01((p - new Vector2(0f, 0.35f)).magnitude / 1.4f);
                    Color color = Color.Lerp(theme * 0.62f, theme * 0.14f, light);
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(badge + 0.04f) / 0.04f);
                    color = Color.Lerp(color, glow, rim * 0.75f);

                    // Glyph: soft glow, drop shadow down-right, dark outline, then the white fill.
                    color = Color.Lerp(color, glow, Mathf.Clamp01(glowMask[i] * 1.4f) * 0.55f);
                    color = Color.Lerp(color, Color.black, Sample(glyph, x - 2, y + 3) * 0.5f);
                    color = Color.Lerp(color, outline, outlineMask[i]);
                    Color fill = Color.Lerp(Color.white, tint, Mathf.Clamp01((0.8f - p.y) / 1.6f));
                    color = Color.Lerp(color, fill, glyph[i]);
                    color.a = badgeAlpha;
                    pixels[i] = color;
                }
            }

            return pixels;
        }

        /// <summary>Glyph coverage (0..1) per icon pixel; a plain dot when the glyph image is missing.</summary>
        private static float[] GlyphMask(string key)
        {
            float[] mask = new float[Size * Size];
            Texture2D source = Resources.Load<Texture2D>(GlyphFolder + key);
            if (source == null || !source.isReadable)
            {
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        Vector2 p = new Vector2((x + 0.5f) * Pixel - 1f, (y + 0.5f) * Pixel - 1f);
                        mask[y * Size + x] = Coverage(p.magnitude - 0.35f);
                    }
                }
                return mask;
            }

            Color32[] texels = source.GetPixels32();
            int width = source.width;
            int height = source.height;
            float step = Pixel / Supersample;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Box-filter the texels under this pixel: the glyph is scaled down about 3x.
                    int sum = 0;
                    for (int sy = 0; sy < Supersample; sy++)
                    {
                        float v = (y * Pixel + (sy + 0.5f) * step - 1f + GlyphHalf) / (2f * GlyphHalf);
                        for (int sx = 0; sx < Supersample; sx++)
                        {
                            float u = (x * Pixel + (sx + 0.5f) * step - 1f + GlyphHalf) / (2f * GlyphHalf);
                            if (u >= 0f && u < 1f && v >= 0f && v < 1f)
                            {
                                sum += texels[(int)(v * height) * width + (int)(u * width)].a;
                            }
                        }
                    }
                    mask[y * Size + x] = sum / (255f * Supersample * Supersample);
                }
            }
            return mask;
        }

        private static float Sample(float[] mask, int x, int y)
        {
            return x < 0 || y < 0 || x >= Size || y >= Size ? 0f : mask[y * Size + x];
        }

        /// <summary>Grows the mask by a round brush, for the glyph outline.</summary>
        private static float[] Dilate(float[] mask, int radius)
        {
            float[] result = new float[mask.Length];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float best = 0f;
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            if (dx * dx + dy * dy <= radius * radius + 1)
                            {
                                best = Mathf.Max(best, Sample(mask, x + dx, y + dy));
                            }
                        }
                    }
                    result[y * Size + x] = best;
                }
            }
            return result;
        }

        /// <summary>Separable box blur, for the glow around the glyph.</summary>
        private static float[] Blur(float[] mask, int radius)
        {
            float[] horizontal = new float[mask.Length];
            float[] result = new float[mask.Length];
            float scale = 1f / (2 * radius + 1);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float sum = 0f;
                    for (int d = -radius; d <= radius; d++)
                    {
                        sum += Sample(mask, x + d, y);
                    }
                    horizontal[y * Size + x] = sum * scale;
                }
            }
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float sum = 0f;
                    for (int d = -radius; d <= radius; d++)
                    {
                        sum += Sample(horizontal, x, y + d);
                    }
                    result[y * Size + x] = sum * scale;
                }
            }
            return result;
        }

        private static float Coverage(float distance)
        {
            return Mathf.Clamp01(0.5f - distance / Pixel);
        }

        private static float RoundedBox(Vector2 p, Vector2 half, float radius)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + new Vector2(radius, radius);
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - radius;
        }
    }
}
