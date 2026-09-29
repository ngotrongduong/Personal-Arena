using System;
using System.Collections.Generic;
using PersonalArena.Core;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// Draws the HUD skill icons at runtime from signed distance shapes, so the game needs no icon
    /// image assets: a colored rounded badge with a glowing white glyph per skill id.
    /// </summary>
    public static class SkillIconFactory
    {
        public const int Size = 128;
        private const float Pixel = 2f / Size;
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
            string key = KeyFor(skill, index);
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
            texture.SetPixels32(DrawPixels(key, ColorFor(skill, index)));
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
            Func<Vector2, float> glyph = GlyphFor(key);
            Color32[] pixels = new Color32[Size * Size];
            Color glow = Color.Lerp(theme, Color.white, 0.35f);
            Color outline = theme * 0.12f;
            outline.a = 1f;
            Color tint = Color.Lerp(Color.white, theme, 0.5f);
            Vector2 shadowOffset = new Vector2(0.035f, -0.045f);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) * Pixel - 1f, (y + 0.5f) * Pixel - 1f);
                    float badge = RoundedBox(p, Vector2.zero, new Vector2(0.94f, 0.94f), 0.24f);
                    float badgeAlpha = Coverage(badge);
                    if (badgeAlpha <= 0f)
                    {
                        pixels[y * Size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    // Badge: dark gradient lit from above with a bright rim.
                    float light = Mathf.Clamp01((p - new Vector2(0f, 0.35f)).magnitude / 1.4f);
                    Color color = Color.Lerp(theme * 0.62f, theme * 0.14f, light);
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(badge + 0.04f) / 0.04f);
                    color = Color.Lerp(color, glow, rim * 0.75f);

                    float distance = glyph(p);
                    if (distance > 0f)
                    {
                        color = Color.Lerp(color, glow, Mathf.Exp(-distance * 7f) * 0.55f);
                    }
                    color = Color.Lerp(color, Color.black, Coverage(glyph(p - shadowOffset) - 0.02f) * 0.5f);
                    color = Color.Lerp(color, outline, Coverage(distance - 0.035f));
                    Color fill = Color.Lerp(Color.white, tint, Mathf.Clamp01((0.8f - p.y) / 1.6f));
                    color = Color.Lerp(color, fill, Coverage(distance));
                    color.a = badgeAlpha;
                    pixels[y * Size + x] = color;
                }
            }

            return pixels;
        }

        private static Func<Vector2, float> GlyphFor(string key)
        {
            switch (key)
            {
                case "spear-strike": return SpearStrike;
                case "kick": return Kick;
                case "shield-block": return Shield;
                case "dash": return Dash;
                case "fireball": return Fireball;
                case "frost-nova": return FrostNova;
                case "mana-shield": return ManaShield;
                case "blink": return Blink;
                case "arrow": return p => Arrow(p, new Vector2(-0.62f, -0.62f), new Vector2(0.66f, 0.66f), true);
                case "piercing-arrow": return PiercingArrow;
                case "leap-back": return LeapBack;
                case "concussive-arrow": return ConcussiveArrow;
                default: return p => Circle(p, Vector2.zero, 0.35f);
            }
        }

        private static float SpearStrike(Vector2 p)
        {
            float d = Segment(p, new Vector2(-0.64f, -0.64f), new Vector2(0.32f, 0.32f), 0.065f);
            d = Mathf.Min(d, Triangle(p, new Vector2(0.74f, 0.74f), new Vector2(0.16f, 0.44f), new Vector2(0.44f, 0.16f)));
            d = Mathf.Min(d, Segment(p, new Vector2(0.02f, 0.3f), new Vector2(0.3f, 0.02f), 0.045f));
            d = Mathf.Min(d, Arc(p, new Vector2(0.3f, -0.3f), 0.78f, 0.035f, 112f, 158f));
            return Mathf.Min(d, Arc(p, new Vector2(0.3f, -0.3f), 0.6f, 0.03f, 118f, 152f));
        }

        private static float Kick(Vector2 p)
        {
            Vector2 q = Rotate(p - new Vector2(-0.12f, 0f), -30f);
            float d = RoundedBox(q, new Vector2(-0.2f, 0.25f), new Vector2(0.16f, 0.42f), 0.07f);
            d = Mathf.Min(d, RoundedBox(q, new Vector2(0.08f, -0.2f), new Vector2(0.36f, 0.15f), 0.08f));
            d = Mathf.Min(d, Segment(p, new Vector2(0.56f, 0.36f), new Vector2(0.68f, 0.58f), 0.04f));
            d = Mathf.Min(d, Segment(p, new Vector2(0.62f, 0.12f), new Vector2(0.86f, 0.18f), 0.04f));
            return Mathf.Min(d, Segment(p, new Vector2(0.58f, -0.12f), new Vector2(0.76f, -0.3f), 0.04f));
        }

        private static float Shield(Vector2 p)
        {
            float shape = Mathf.Max(Circle(p, new Vector2(-0.42f, 0.25f), 0.95f), Circle(p, new Vector2(0.42f, 0.25f), 0.95f));
            shape = Mathf.Max(shape, p.y - 0.62f);
            float rimBand = Mathf.Max(shape, -(shape + 0.12f));
            float cross = Mathf.Min(RoundedBox(p, new Vector2(0f, 0.04f), new Vector2(0.05f, 0.5f), 0.02f),
                RoundedBox(p, new Vector2(0f, 0.22f), new Vector2(0.4f, 0.05f), 0.02f));
            cross = Mathf.Min(cross, Circle(p, new Vector2(0f, 0.22f), 0.14f));
            return Mathf.Min(rimBand, Mathf.Max(cross, shape + 0.19f));
        }

        private static float Dash(Vector2 p)
        {
            float d = Chevron(p, new Vector2(-0.14f, 0f));
            d = Mathf.Min(d, Chevron(p, new Vector2(0.22f, 0f)));
            d = Mathf.Min(d, Segment(p, new Vector2(-0.84f, 0.26f), new Vector2(-0.42f, 0.26f), 0.045f));
            d = Mathf.Min(d, Segment(p, new Vector2(-0.92f, 0f), new Vector2(-0.38f, 0f), 0.045f));
            return Mathf.Min(d, Segment(p, new Vector2(-0.84f, -0.26f), new Vector2(-0.42f, -0.26f), 0.045f));
        }

        private static float Chevron(Vector2 p, Vector2 offset)
        {
            return Mathf.Min(Segment(p, offset + new Vector2(0f, 0.42f), offset + new Vector2(0.32f, 0f), 0.085f),
                Segment(p, offset + new Vector2(0.32f, 0f), offset + new Vector2(0f, -0.42f), 0.085f));
        }

        private static float Fireball(Vector2 p)
        {
            Vector2 core = new Vector2(0.2f, -0.2f);
            float d = Circle(p, core, 0.34f);
            d = SmoothMin(d, Triangle(p, new Vector2(-0.68f, 0.68f), new Vector2(0.43f, 0.03f), new Vector2(-0.03f, -0.43f)), 0.12f);
            d = SmoothMin(d, Triangle(p, new Vector2(-0.2f, 0.8f), new Vector2(0.3f, 0.14f), new Vector2(0.02f, 0.02f)), 0.1f);
            d = SmoothMin(d, Triangle(p, new Vector2(-0.8f, 0.2f), new Vector2(-0.02f, 0.02f), new Vector2(-0.14f, -0.3f)), 0.1f);
            // A hot hollow core reads as fire rather than a plain blob.
            return Mathf.Max(d, -Circle(p, core + new Vector2(0.04f, -0.04f), 0.12f));
        }

        private static float FrostNova(Vector2 p)
        {
            float d = Circle(p, Vector2.zero, 0.13f);
            for (int arm = 0; arm < 6; arm++)
            {
                Vector2 direction = Direction(90f + arm * 60f);
                d = Mathf.Min(d, Segment(p, Vector2.zero, direction * 0.66f, 0.05f));
                Vector2 fork = direction * 0.4f;
                d = Mathf.Min(d, Segment(p, fork, fork + Rotate(direction, 45f) * 0.2f, 0.04f));
                d = Mathf.Min(d, Segment(p, fork, fork + Rotate(direction, -45f) * 0.2f, 0.04f));
            }
            return d;
        }

        private static float ManaShield(Vector2 p)
        {
            float d = Ring(p, Vector2.zero, 0.62f, 0.07f);
            d = Mathf.Min(d, Arc(p, Vector2.zero, 0.46f, 0.04f, 110f, 160f));
            return Mathf.Min(d, Star4(p, Vector2.zero, 0.32f, 0.1f));
        }

        private static float Blink(Vector2 p)
        {
            float d = Star4(p, Vector2.zero, 0.46f, 0.13f);
            d = Mathf.Min(d, Arc(p, Vector2.zero, 0.7f, 0.05f, 25f, 155f));
            d = Mathf.Min(d, Arc(p, Vector2.zero, 0.7f, 0.05f, 205f, 335f));
            d = Mathf.Min(d, Circle(p, new Vector2(0.56f, -0.58f), 0.07f));
            return Mathf.Min(d, Circle(p, new Vector2(-0.58f, 0.56f), 0.07f));
        }

        private static float PiercingArrow(Vector2 p)
        {
            float arrow = Arrow(p, new Vector2(-0.84f, 0f), new Vector2(0.84f, 0f), true);
            float bodies = Mathf.Min(Segment(p, new Vector2(-0.18f, -0.46f), new Vector2(-0.18f, 0.46f), 0.1f),
                Segment(p, new Vector2(0.24f, -0.46f), new Vector2(0.24f, 0.46f), 0.1f));
            return Mathf.Min(arrow, Mathf.Max(bodies, -(arrow - 0.06f)));
        }

        private static float LeapBack(Vector2 p)
        {
            float d = Arc(p, new Vector2(0.08f, -0.12f), 0.5f, 0.075f, 0f, 180f);
            d = Mathf.Min(d, Triangle(p, new Vector2(-0.42f, -0.66f), new Vector2(-0.7f, -0.2f), new Vector2(-0.14f, -0.2f)));
            d = Mathf.Min(d, Circle(p, new Vector2(0.58f, -0.5f), 0.12f));
            return Mathf.Min(d, Segment(p, new Vector2(0.3f, -0.78f), new Vector2(0.84f, -0.78f), 0.04f));
        }

        private static float ConcussiveArrow(Vector2 p)
        {
            float d = Arrow(p, new Vector2(-0.86f, 0f), new Vector2(0.26f, 0f), true);
            Vector2 burst = new Vector2(0.56f, 0f);
            d = Mathf.Min(d, Circle(p, burst, 0.11f));
            for (int ray = 0; ray < 8; ray++)
            {
                if (ray == 4)
                {
                    continue; // The arrow comes from this side.
                }
                Vector2 direction = Direction(ray * 45f);
                d = Mathf.Min(d, Segment(p, burst + direction * 0.2f, burst + direction * 0.36f, 0.04f));
            }
            return d;
        }

        private static float Arrow(Vector2 p, Vector2 from, Vector2 to, bool fletching)
        {
            Vector2 direction = (to - from).normalized;
            Vector2 side = new Vector2(-direction.y, direction.x);
            const float headLength = 0.34f;
            const float headHalf = 0.2f;
            float d = Segment(p, from, to - direction * headLength * 0.6f, 0.05f);
            d = Mathf.Min(d, Triangle(p, to, to - direction * headLength + side * headHalf, to - direction * headLength - side * headHalf));
            if (fletching)
            {
                for (int feather = 0; feather < 2; feather++)
                {
                    Vector2 root = from + direction * (0.22f + feather * 0.13f);
                    Vector2 tip = from + direction * (0.06f + feather * 0.13f);
                    d = Mathf.Min(d, Segment(p, root, tip + side * 0.16f, 0.035f));
                    d = Mathf.Min(d, Segment(p, root, tip - side * 0.16f, 0.035f));
                }
            }
            return d;
        }

        private static float Star4(Vector2 p, Vector2 center, float radius, float width)
        {
            float d = float.MaxValue;
            for (int point = 0; point < 4; point++)
            {
                Vector2 direction = Direction(90f * point);
                Vector2 side = new Vector2(-direction.y, direction.x);
                d = Mathf.Min(d, Triangle(p, center + direction * radius, center + side * width, center - side * width));
            }
            return d;
        }

        private static float Coverage(float distance)
        {
            return Mathf.Clamp01(0.5f - distance / Pixel);
        }

        private static float Circle(Vector2 p, Vector2 center, float radius)
        {
            return (p - center).magnitude - radius;
        }

        private static float Ring(Vector2 p, Vector2 center, float radius, float halfWidth)
        {
            return Mathf.Abs((p - center).magnitude - radius) - halfWidth;
        }

        private static float Arc(Vector2 p, Vector2 center, float radius, float halfWidth, float fromDegrees, float toDegrees)
        {
            Vector2 q = p - center;
            float angle = Mathf.Repeat(Mathf.Atan2(q.y, q.x) * Mathf.Rad2Deg - fromDegrees, 360f);
            if (angle <= toDegrees - fromDegrees)
            {
                return Mathf.Abs(q.magnitude - radius) - halfWidth;
            }
            Vector2 start = center + Direction(fromDegrees) * radius;
            Vector2 end = center + Direction(toDegrees) * radius;
            return Mathf.Min((p - start).magnitude, (p - end).magnitude) - halfWidth;
        }

        private static float Segment(Vector2 p, Vector2 a, Vector2 b, float radius)
        {
            Vector2 pa = p - a;
            Vector2 ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - radius;
        }

        private static float RoundedBox(Vector2 p, Vector2 center, Vector2 half, float radius)
        {
            Vector2 q = p - center;
            Vector2 d = new Vector2(Mathf.Abs(q.x), Mathf.Abs(q.y)) - half + new Vector2(radius, radius);
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - radius;
        }

        private static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 e0 = b - a;
            Vector2 e1 = c - b;
            Vector2 e2 = a - c;
            Vector2 v0 = p - a;
            Vector2 v1 = p - b;
            Vector2 v2 = p - c;
            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            Vector2 d = Vector2.Min(Vector2.Min(
                    new Vector2(Vector2.Dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
                    new Vector2(Vector2.Dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
                new Vector2(Vector2.Dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x)));
            return -Mathf.Sqrt(d.x) * Mathf.Sign(d.y);
        }

        private static float SmoothMin(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        private static Vector2 Direction(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
    }
}
