using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>Small runtime-generated sprites shared by the viewer UI.</summary>
    public static class UiSprites
    {
        private const int TextureSize = 64;
        private const float RoundedRadius = 16f;
        private static Sprite roundedSprite;
        private static Sprite circleSprite;
        private static Sprite softGlowSprite;

        /// <summary>A white, anti-aliased rounded rectangle suitable for a sliced Image.</summary>
        public static Sprite RoundedSprite()
        {
            if (roundedSprite == null)
            {
                Texture2D texture = CreateTexture("UI Rounded Rectangle", RoundedAlpha);
                Vector4 border = new Vector4(RoundedRadius, RoundedRadius, RoundedRadius, RoundedRadius);
                roundedSprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
                roundedSprite.name = "UI Rounded Rectangle";
            }

            return roundedSprite;
        }

        /// <summary>A white, softly anti-aliased circle.</summary>
        public static Sprite Circle()
        {
            if (circleSprite == null)
            {
                Texture2D texture = CreateTexture("UI Circle", CircleAlpha);
                circleSprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Vector4.zero);
                circleSprite.name = "UI Circle";
            }

            return circleSprite;
        }

        /// <summary>A white radial gradient for subtle highlights and shadows.</summary>
        public static Sprite SoftGlow()
        {
            if (softGlowSprite == null)
            {
                Texture2D texture = CreateTexture("UI Soft Glow", GlowAlpha);
                softGlowSprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Vector4.zero);
                softGlowSprite.name = "UI Soft Glow";
            }

            return softGlowSprite;
        }

        private static Texture2D CreateTexture(string textureName, System.Func<float, float, float> alpha)
        {
            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float a = Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f));
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static float RoundedAlpha(float x, float y)
        {
            float centre = TextureSize * 0.5f;
            float inner = centre - RoundedRadius;
            float dx = Mathf.Max(Mathf.Abs(x - centre) - inner, 0f);
            float dy = Mathf.Max(Mathf.Abs(y - centre) - inner, 0f);
            float distance = Mathf.Sqrt(dx * dx + dy * dy) - RoundedRadius;
            return Mathf.Clamp01(0.5f - distance);
        }

        private static float CircleAlpha(float x, float y)
        {
            float centre = TextureSize * 0.5f;
            float dx = x - centre;
            float dy = y - centre;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(centre - distance);
        }

        private static float GlowAlpha(float x, float y)
        {
            float centre = TextureSize * 0.5f;
            float dx = x - centre;
            float dy = y - centre;
            float distance = Mathf.Sqrt(dx * dx + dy * dy) / centre;
            float strength = Mathf.Clamp01(1f - distance);
            return strength * strength;
        }
    }
}
