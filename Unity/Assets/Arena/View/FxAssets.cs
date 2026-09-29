using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// Procedural textures, materials and meshes shared by the arena effects. Everything is generated once at
    /// runtime so the viewer needs no extra art files (the "PersonalArena/Fx" shader is forced into builds).
    /// </summary>
    public static class FxAssets
    {
        public const string ShaderName = "PersonalArena/Fx";

        private static Shader fxShader;
        private static Texture2D softDot;
        private static Texture2D ring;
        private static Texture2D spark;
        private static Texture2D star;
        private static Texture2D smoke;
        private static Texture2D radialGlow;
        private static Material additive;
        private static Material alpha;
        private static Material sparkMaterial;
        private static Material starMaterial;
        private static Material stunStarMaterial;
        private static Material ringMaterial;
        private static Material smokeMaterial;
        private static Material glowMaterial;
        private static Material ghostMaterial;
        private static Mesh quad;
        private static Mesh slashArc;
        private static Mesh shieldBand;

        public static Shader FxShader
        {
            get
            {
                if (fxShader == null)
                {
                    fxShader = Shader.Find(ShaderName);
                    if (fxShader == null)
                    {
                        Debug.LogWarning(ShaderName + " shader missing; falling back to Sprites/Default.");
                        fxShader = Shader.Find("Sprites/Default");
                    }
                }
                return fxShader;
            }
        }

        public static Texture2D SoftDot => softDot != null ? softDot : softDot = Radial("Fx Soft Dot", 64, r => Smooth(1f - r) * Smooth(1f - r));
        public static Texture2D Ring => ring != null ? ring : ring = Radial("Fx Ring", 128, r => Mathf.Clamp01(1f - Mathf.Abs(r - 0.8f) / 0.14f) * Smooth(Mathf.Clamp01((r - 0.3f) * 2f)));
        public static Texture2D Spark => spark != null ? spark : spark = BuildSpark();
        public static Texture2D Star => star != null ? star : star = BuildStar();
        public static Texture2D Smoke => smoke != null ? smoke : smoke = BuildSmoke();
        public static Texture2D RadialGlow => radialGlow != null ? radialGlow : radialGlow = Radial("Fx Radial Glow", 128, r => Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f));

        /// <summary>Additive soft dot: embers, glows, heal sparkles.</summary>
        public static Material Additive => additive != null ? additive : additive = Create("Fx Additive", SoftDot, true);

        /// <summary>Alpha-blended soft dot: dust.</summary>
        public static Material Alpha => alpha != null ? alpha : alpha = Create("Fx Alpha", SoftDot, false);

        public static Material SparkMaterial => sparkMaterial != null ? sparkMaterial : sparkMaterial = Create("Fx Spark", Spark, true);
        public static Material StarMaterial => starMaterial != null ? starMaterial : starMaterial = Create("Fx Star", Star, true);

        /// <summary>Alpha-blended cartoon five-point star (gold with a dark rim, colours baked in): the "dizzy" stun marker.</summary>
        public static Material StunStarMaterial => stunStarMaterial != null ? stunStarMaterial : stunStarMaterial = Create("Fx Stun Star", BuildStunStar(), false);
        public static Material RingMaterial => ringMaterial != null ? ringMaterial : ringMaterial = Create("Fx Ring", Ring, true);
        public static Material SmokeMaterial => smokeMaterial != null ? smokeMaterial : smokeMaterial = Create("Fx Smoke", Smoke, false);
        public static Material GlowMaterial => glowMaterial != null ? glowMaterial : glowMaterial = Create("Fx Glow", RadialGlow, true);

        /// <summary>White additive material (vertex colours carry the look): slash arcs, shield, trails, afterimages.</summary>
        public static Material GhostMaterial => ghostMaterial != null ? ghostMaterial : ghostMaterial = Create("Fx Ghost", Texture2D.whiteTexture, true);

        /// <summary>Unit quad lying flat on the XZ plane (faces up), centred on the origin.</summary>
        public static Mesh FlatQuad => quad != null ? quad : quad = BuildFlatQuad();

        /// <summary>Crescent sweep in front of the origin (+Z forward), vertex alpha fading toward both tips.</summary>
        public static Mesh SlashArc => slashArc != null ? slashArc : slashArc = BuildSlashArc();

        /// <summary>Curved vertical band in front of the origin used as the block shield glow.</summary>
        public static Mesh ShieldBand => shieldBand != null ? shieldBand : shieldBand = BuildShieldBand();

        /// <summary>
        /// Creates a stopped-then-playing particle system with no automatic emission, ready for
        /// <see cref="ParticleSystem.Emit(ParticleSystem.EmitParams, int)"/> bursts or for enabling its own emission.
        /// </summary>
        public static ParticleSystem CreateParticles(string name, Transform parent, Material material, int maxParticles,
            ParticleSystemRenderMode renderMode = ParticleSystemRenderMode.Billboard)
        {
            GameObject systemObject = new GameObject(name);
            systemObject.transform.SetParent(parent, false);
            ParticleSystem system = systemObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 5f;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startSpeed = 0f;
            main.startLifetime = 1f;
            main.startSize = 0.3f;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystemRenderer renderer = systemObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = renderMode;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.alignment = ParticleSystemRenderSpace.View;
            if (renderMode == ParticleSystemRenderMode.Stretch)
            {
                renderer.velocityScale = 0.045f;
                renderer.lengthScale = 1.2f;
            }

            system.Play();
            return system;
        }

        /// <summary>Alpha fades in quickly, holds, then fades out; colour stays white (start colour tints it).</summary>
        public static void FadeOverLifetime(ParticleSystem system, float fadeIn = 0.08f, float holdUntil = 0.45f)
        {
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(1f, holdUntil), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        /// <summary>Scales particle size from <paramref name="start"/> to <paramref name="end"/> (multipliers of start size).</summary>
        public static void SizeOverLifetime(ParticleSystem system, float start, float end)
        {
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, start, 1f, end));
        }

        public static Material Create(string name, Texture texture, bool additiveBlend)
        {
            Material material = new Material(FxShader) { name = name };
            material.mainTexture = texture;
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additiveBlend ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            material.renderQueue = 3000;
            return material;
        }

        private static float Smooth(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static Texture2D NewTexture(string name, int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        private static Texture2D Radial(string name, int size, System.Func<float, float> alphaAtRadius)
        {
            Texture2D texture = NewTexture(name, size);
            Color32[] pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(r >= 1f ? 0f : alphaAtRadius(r)));
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Texture2D BuildSpark()
        {
            // Bright streak along Y (particles stretch along their velocity) with a hot core.
            const int size = 64;
            Texture2D texture = NewTexture("Fx Spark", size);
            Color32[] pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - half) / half;
                    float dy = Mathf.Abs(y - half) / half;
                    float along = Smooth(1f - dy);
                    float across = Mathf.Pow(Mathf.Clamp01(1f - dx * 2.6f), 2f);
                    byte a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(along * across));
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Texture2D BuildStar()
        {
            // Four-pointed twinkle with a soft halo.
            const int size = 64;
            Texture2D texture = NewTexture("Fx Star", size);
            Color32[] pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - half) / half;
                    float dy = Mathf.Abs(y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float cross = Mathf.Clamp01(1f - dx * dy * 18f - r * 0.9f);
                    float halo = Mathf.Pow(Mathf.Clamp01(1f - r), 3f) * 0.6f;
                    byte a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(Mathf.Max(cross * cross, halo)));
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Texture2D BuildStunStar()
        {
            // Solid five-point star: bright gold core, warm gold body, dark orange outline so it reads on any floor.
            const int size = 128;
            Texture2D texture = NewTexture("Fx Stun Star", size);
            Color32[] pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            const float outer = 0.95f;
            const float inner = 0.42f;
            const float rim = 0.13f;
            Color gold = new Color(1f, 0.82f, 0.18f);
            Color core = new Color(1f, 0.97f, 0.62f);
            Color edge = new Color(0.55f, 0.22f, 0.02f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = (x - half) / half;
                    float py = (y - half) / half;
                    float r = Mathf.Sqrt(px * px + py * py);
                    float angle = Mathf.Atan2(px, py);
                    // Distance from centre to the star boundary along this direction (linear between tip and notch).
                    float sector = Mathf.PI * 2f / 5f;
                    float local = Mathf.Abs(Mathf.Repeat(angle + sector * 0.5f, sector) - sector * 0.5f) / (sector * 0.5f);
                    float boundary = Mathf.Lerp(outer, inner, local);
                    float inside = boundary - r;
                    float alpha = Mathf.Clamp01(inside * half * 0.5f + 0.5f);
                    Color color = inside < rim ? edge : Color.Lerp(gold, core, Mathf.Clamp01(1f - r / (boundary * 0.75f)));
                    pixels[y * size + x] = new Color32(
                        (byte)Mathf.RoundToInt(color.r * 255f),
                        (byte)Mathf.RoundToInt(color.g * 255f),
                        (byte)Mathf.RoundToInt(color.b * 255f),
                        (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Texture2D BuildSmoke()
        {
            // Lumpy puff: soft radial falloff modulated by value noise.
            const int size = 64;
            Texture2D texture = NewTexture("Fx Smoke", size);
            Color32[] pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float noise = Mathf.PerlinNoise(x * 0.11f + 3.1f, y * 0.11f + 7.7f) * 0.6f +
                        Mathf.PerlinNoise(x * 0.27f + 11f, y * 0.27f + 5f) * 0.4f;
                    float falloff = Smooth(1f - r);
                    byte a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(falloff * (0.45f + noise * 0.8f)));
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Mesh BuildFlatQuad()
        {
            Mesh mesh = new Mesh { name = "Fx Flat Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildSlashArc()
        {
            const int segments = 24;
            const float sweep = 150f;
            const float inner = 0.35f;
            const float outer = 1.25f;
            Vector3[] vertices = new Vector3[(segments + 1) * 2];
            Color[] colors = new Color[vertices.Length];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = Mathf.Deg2Rad * (-sweep * 0.5f + sweep * t);
                Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                // The leading tip (t = 1) is brightest; the tail fades out like a motion smear.
                float edge = Mathf.Sin(t * Mathf.PI);
                float intensity = Mathf.Pow(t, 1.6f) * (0.35f + 0.65f * edge);
                vertices[i * 2] = direction * inner;
                vertices[i * 2 + 1] = direction * outer;
                colors[i * 2] = new Color(1f, 1f, 1f, 0f);
                colors[i * 2 + 1] = new Color(1f, 1f, 1f, intensity);
                uvs[i * 2] = new Vector2(t, 0f);
                uvs[i * 2 + 1] = new Vector2(t, 1f);
                if (i < segments)
                {
                    int v = i * 2;
                    int k = i * 6;
                    triangles[k] = v;
                    triangles[k + 1] = v + 1;
                    triangles[k + 2] = v + 2;
                    triangles[k + 3] = v + 1;
                    triangles[k + 4] = v + 3;
                    triangles[k + 5] = v + 2;
                }
            }

            Mesh mesh = new Mesh { name = "Fx Slash Arc" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildShieldBand()
        {
            const int columns = 20;
            const int rows = 6;
            const float sweep = 130f;
            const float radius = 0.62f;
            const float height = 1.35f;
            Vector3[] vertices = new Vector3[(columns + 1) * (rows + 1)];
            Color[] colors = new Color[vertices.Length];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[columns * rows * 6];
            for (int row = 0; row <= rows; row++)
            {
                float v = row / (float)rows;
                for (int column = 0; column <= columns; column++)
                {
                    float u = column / (float)columns;
                    float angle = Mathf.Deg2Rad * (-sweep * 0.5f + sweep * u);
                    // Bulge outward in the middle so it reads as a dome of force, not a flat wall.
                    float bulge = radius + 0.12f * Mathf.Sin(v * Mathf.PI);
                    int index = row * (columns + 1) + column;
                    vertices[index] = new Vector3(Mathf.Sin(angle) * bulge, v * height, Mathf.Cos(angle) * bulge);
                    // Clamp: Sin(PI) is about -8.7e-8 in float, and Pow(negative, 0.8) is NaN, which drew black edges.
                    float fade = Mathf.Max(0f, Mathf.Sin(u * Mathf.PI) * Mathf.Sin(v * Mathf.PI));
                    colors[index] = new Color(1f, 1f, 1f, Mathf.Pow(fade, 0.8f));
                    uvs[index] = new Vector2(u, v);
                }
            }

            int t = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int a = row * (columns + 1) + column;
                    int b = a + columns + 1;
                    triangles[t++] = a;
                    triangles[t++] = b;
                    triangles[t++] = a + 1;
                    triangles[t++] = a + 1;
                    triangles[t++] = b;
                    triangles[t++] = b + 1;
                }
            }

            Mesh mesh = new Mesh { name = "Fx Shield Band" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
