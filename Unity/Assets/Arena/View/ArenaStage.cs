using System.Collections.Generic;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// The round arena: a floating stone platform over a misty abyss, ringed by brazier pillars and drifting
    /// rock islands. Built procedurally at runtime (stone textures, platform and rock meshes, particles) and
    /// animated here: fire flicker, bobbing islands, the pulsing edge warning and camera-aware fog.
    /// </summary>
    public sealed class ArenaStage : MonoBehaviour
    {
        private const int Segments = 128;
        private const float TextureWorldSize = 4f;
        private const float EdgeBand = 0.55f;
        private const float SideDepth = 1.3f;
        private const float UndersideDepth = 10f;
        private const int BrazierCount = 8;
        private const int IslandCount = 11;

        private static readonly Color FogColor = new Color(0.05f, 0.035f, 0.085f);
        private static readonly Color FireColor = new Color(1f, 0.56f, 0.24f);
        private static readonly Color EdgeGlowColor = new Color(1f, 0.32f, 0.12f);

        private static Texture2D stoneAlbedo;
        private static Texture2D stoneNormal;

        private readonly List<Light> fireLights = new List<Light>();
        private readonly List<Floater> floaters = new List<Floater>();
        private readonly List<Object> owned = new List<Object>();
        private Material edgeGlowMaterial;
        private Vector3 center;
        private float radius;

        public Vector3 Center => center;
        public float Radius => radius;

        /// <summary>Builds the stage under <paramref name="parent"/> around <paramref name="arenaCenter"/> (world units).</summary>
        public static ArenaStage Build(ArenaArtSet art, Transform parent, Vector3 arenaCenter, float arenaRadius)
        {
            GameObject root = new GameObject("Arena Stage");
            root.transform.SetParent(parent, false);
            ArenaStage stage = root.AddComponent<ArenaStage>();
            stage.center = arenaCenter;
            stage.radius = arenaRadius;
            stage.BuildAll(art);
            return stage;
        }

        private void BuildAll(ArenaArtSet art)
        {
            Shader standard = Shader.Find("Standard");
            System.Random random = new System.Random(4242);

            Material floor = Own(new Material(standard) { name = "Arena Stone Floor" });
            floor.mainTexture = StoneAlbedo;
            floor.color = new Color(0.86f, 0.84f, 0.88f);
            floor.SetTexture("_BumpMap", StoneNormal);
            floor.SetFloat("_BumpScale", 0.9f);
            floor.EnableKeyword("_NORMALMAP");
            floor.SetFloat("_Glossiness", 0.18f);

            Material edge = Own(new Material(floor) { name = "Arena Edge Stone" });
            edge.color = new Color(0.55f, 0.5f, 0.52f);

            Material rock = Own(new Material(standard) { name = "Arena Rock" });
            rock.mainTexture = StoneAlbedo;
            rock.color = new Color(0.34f, 0.3f, 0.34f);
            rock.SetTexture("_BumpMap", StoneNormal);
            rock.SetFloat("_BumpScale", 1.4f);
            rock.EnableKeyword("_NORMALMAP");
            rock.SetFloat("_Glossiness", 0.05f);

            Material inlay = Own(new Material(floor) { name = "Arena Inlay" });
            inlay.color = new Color(0.45f, 0.42f, 0.5f);

            AddMesh("Floor", Own(BuildDisc(radius - EdgeBand, 0f)), floor, Vector3.zero, true);
            AddMesh("Edge Stones", Own(BuildAnnulus(radius - EdgeBand, radius, 0.02f, 1f)), edge, Vector3.zero, true);
            AddMesh("Inlay Ring", Own(BuildAnnulus(radius * 0.5f - 0.12f, radius * 0.5f + 0.12f, 0.008f, 1f)), inlay, Vector3.zero, true);
            AddMesh("Center Inlay", Own(BuildAnnulus(1.1f, 1.35f, 0.008f, 1f)), inlay, Vector3.zero, true);
            AddMesh("Cliff", Own(BuildCliff(radius, 1.37f)), rock, Vector3.zero, false);

            edgeGlowMaterial = Own(FxAssets.Create("Arena Edge Glow", Texture2D.whiteTexture, true));
            AddMesh("Edge Warning", Own(BuildGlowBand(radius - 1.1f, radius - 0.03f, 0.03f)), edgeGlowMaterial, Vector3.zero, false);

            BuildAbyss();
            BuildBraziers(art, rock, random);
            BuildIslands(art, rock, random);
            QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, BrazierCount + 2);
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            for (int i = 0; i < fireLights.Count; i++)
            {
                Light light = fireLights[i];
                if (light != null)
                {
                    float noise = Mathf.PerlinNoise(i * 7.31f, time * 3.4f);
                    light.intensity = 2.3f * (0.78f + 0.38f * noise);
                }
            }

            for (int i = 0; i < floaters.Count; i++)
            {
                Floater floater = floaters[i];
                if (floater.Transform == null)
                {
                    continue;
                }
                float phase = time * floater.Speed + floater.Phase;
                floater.Transform.localPosition = floater.Base + new Vector3(0f, Mathf.Sin(phase) * floater.Amplitude, 0f);
                floater.Transform.localRotation = Quaternion.Euler(Mathf.Sin(phase * 0.7f) * floater.Tilt, floater.Yaw + time * floater.Spin, Mathf.Cos(phase * 0.6f) * floater.Tilt);
            }

            if (edgeGlowMaterial != null)
            {
                float pulse = 0.55f + 0.45f * Mathf.Sin(time * 2.4f);
                Color glow = EdgeGlowColor;
                glow.a = 0.22f + 0.2f * pulse;
                edgeGlowMaterial.color = glow;
            }

            TuneFog();
        }

        /// <summary>Fog follows the camera distance so the arena stays crisp and the abyss always fades to violet.</summary>
        private void TuneFog()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            float distance = Vector3.Distance(camera.transform.position, center);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = Mathf.Max(4f, distance - radius * 0.2f);
            RenderSettings.fogEndDistance = distance + radius + 34f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = FogColor;
        }

        private void BuildAbyss()
        {
            // A deep violet glow far below gives the pit depth; the rest is fog.
            GameObject glow = new GameObject("Abyss Glow");
            glow.transform.SetParent(transform, false);
            glow.transform.position = center + new Vector3(0f, -26f, 0f);
            glow.transform.localScale = new Vector3(radius * 6f, 1f, radius * 6f);
            glow.AddComponent<MeshFilter>().sharedMesh = FxAssets.FlatQuad;
            MeshRenderer glowRenderer = glow.AddComponent<MeshRenderer>();
            Material glowMaterial = Own(FxAssets.Create("Abyss Glow", FxAssets.RadialGlow, true));
            glowMaterial.color = new Color(0.45f, 0.16f, 0.62f, 0.55f);
            glowRenderer.sharedMaterial = glowMaterial;
            glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glowRenderer.receiveShadows = false;

            // Embers drifting up out of the dark.
            ParticleSystem embers = FxAssets.CreateParticles("Abyss Embers", transform, FxAssets.Additive, 400);
            embers.transform.position = center + new Vector3(0f, -14f, 0f);
            ParticleSystem.MainModule main = embers.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 11f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.16f, 0.9f), new Color(1f, 0.75f, 0.3f, 0.9f));
            ParticleSystem.ShapeModule shape = embers.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius + 14f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.EmissionModule emission = embers.emission;
            emission.enabled = true;
            emission.rateOverTime = 34f;
            ParticleSystem.NoiseModule noise = embers.noise;
            noise.enabled = true;
            noise.strength = 0.7f;
            noise.frequency = 0.25f;
            FxAssets.FadeOverLifetime(embers, 0.15f, 0.6f);
            ParticleSystem.VelocityOverLifetimeModule rise = embers.velocityOverLifetime;
            rise.enabled = true;
            rise.space = ParticleSystemSimulationSpace.World;
            // Circle shapes push particles outward in their plane; the rise comes from here (all axes share one curve mode).
            rise.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            rise.y = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            rise.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

            // Slow violet mist curling around the underside of the platform.
            ParticleSystem mist = FxAssets.CreateParticles("Abyss Mist", transform, FxAssets.SmokeMaterial, 120);
            mist.transform.position = center + new Vector3(0f, -5f, 0f);
            ParticleSystem.MainModule mistMain = mist.main;
            mistMain.startLifetime = new ParticleSystem.MinMaxCurve(12f, 18f);
            mistMain.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            mistMain.startSize = new ParticleSystem.MinMaxCurve(radius * 0.5f, radius * 0.9f);
            mistMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            mistMain.startColor = new ParticleSystem.MinMaxGradient(new Color(0.28f, 0.2f, 0.42f, 0.28f), new Color(0.16f, 0.12f, 0.28f, 0.36f));
            ParticleSystem.ShapeModule mistShape = mist.shape;
            mistShape.enabled = true;
            mistShape.shapeType = ParticleSystemShapeType.Circle;
            mistShape.radius = radius + 6f;
            mistShape.radiusThickness = 0.55f;
            mistShape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.EmissionModule mistEmission = mist.emission;
            mistEmission.enabled = true;
            mistEmission.rateOverTime = 5f;
            ParticleSystem.RotationOverLifetimeModule mistSpin = mist.rotationOverLifetime;
            mistSpin.enabled = true;
            mistSpin.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            FxAssets.FadeOverLifetime(mist, 0.25f, 0.6f);
            mist.Simulate(12f, true, true);
            mist.Play();
            embers.Simulate(8f, true, true);
            embers.Play();
        }

        private void BuildBraziers(ArenaArtSet art, Material rock, System.Random random)
        {
            Transform group = new GameObject("Braziers").transform;
            group.SetParent(transform, false);
            Mesh chunk = Own(BuildRockChunk(1.25f, 2.4f, random));
            for (int i = 0; i < BrazierCount; i++)
            {
                float angle = (i + 0.5f) / BrazierCount * Mathf.PI * 2f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 basePosition = center + direction * (radius + 2.3f) + new Vector3(0f, -0.35f, 0f);

                Transform holder = new GameObject("Brazier " + i).transform;
                holder.SetParent(group, false);
                holder.localPosition = basePosition;
                AddMeshTo(holder, "Rock", chunk, rock, Vector3.zero, true);

                float pillarTop = 1.9f;
                if (art != null && art.Pillar != null)
                {
                    float s = art.DungeonScale;
                    GameObject pillar = Instantiate(art.Pillar, holder, false);
                    pillar.name = "Pillar";
                    pillar.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                    pillar.transform.localScale = new Vector3(s * 0.8f, s * 0.6f, s * 0.8f);
                    pillarTop = 0.05f + 4f * s * 0.6f;
                }
                else
                {
                    GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(column.GetComponent<Collider>());
                    column.transform.SetParent(holder, false);
                    column.transform.localPosition = new Vector3(0f, 0.95f, 0f);
                    column.transform.localScale = new Vector3(0.5f, 0.95f, 0.5f);
                    column.GetComponent<Renderer>().sharedMaterial = rock;
                }

                GameObject bowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(bowl.GetComponent<Collider>());
                bowl.name = "Bowl";
                bowl.transform.SetParent(holder, false);
                bowl.transform.localPosition = new Vector3(0f, pillarTop + 0.06f, 0f);
                bowl.transform.localScale = new Vector3(0.75f, 0.08f, 0.75f);
                bowl.GetComponent<Renderer>().sharedMaterial = rock;

                Vector3 firePosition = new Vector3(0f, pillarTop + 0.16f, 0f);
                BuildFire(holder, firePosition);

                GameObject lightObject = new GameObject("Fire Light");
                lightObject.transform.SetParent(holder, false);
                lightObject.transform.localPosition = firePosition + new Vector3(0f, 0.6f, 0f) - direction * 0.6f;
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = FireColor;
                light.range = 10f;
                light.intensity = 2.3f;
                light.shadows = LightShadows.None;
                fireLights.Add(light);

                floaters.Add(new Floater
                {
                    Transform = holder,
                    Base = basePosition,
                    Amplitude = 0.06f,
                    Speed = 0.7f,
                    Phase = i * 1.3f,
                    Yaw = 0f,
                    Spin = 0f,
                    Tilt = 0.6f
                });
            }
        }

        private static void BuildFire(Transform parent, Vector3 localPosition)
        {
            ParticleSystem flames = FxAssets.CreateParticles("Flames", parent, FxAssets.Additive, 80);
            flames.transform.localPosition = localPosition;
            ParticleSystem.MainModule main = flames.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.5f, 0.16f, 0.85f), new Color(1f, 0.78f, 0.35f, 0.85f));
            ParticleSystem.ShapeModule shape = flames.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.18f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.EmissionModule emission = flames.emission;
            emission.enabled = true;
            emission.rateOverTime = 38f;
            FxAssets.SizeOverLifetime(flames, 1f, 0.15f);
            ParticleSystem.ColorOverLifetimeModule color = flames.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.12f), 0.45f), new GradientColorKey(new Color(0.6f, 0.1f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem sparks = FxAssets.CreateParticles("Fire Sparks", parent, FxAssets.Additive, 30);
            sparks.transform.localPosition = localPosition + new Vector3(0f, 0.3f, 0f);
            ParticleSystem.MainModule sparkMain = sparks.main;
            sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            sparkMain.startColor = new Color(1f, 0.7f, 0.3f, 1f);
            ParticleSystem.ShapeModule sparkShape = sparks.shape;
            sparkShape.enabled = true;
            sparkShape.shapeType = ParticleSystemShapeType.Cone;
            sparkShape.angle = 20f;
            sparkShape.radius = 0.12f;
            sparkShape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.EmissionModule sparkEmission = sparks.emission;
            sparkEmission.enabled = true;
            sparkEmission.rateOverTime = 5f;
            ParticleSystem.NoiseModule noise = sparks.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 1.2f;
            FxAssets.FadeOverLifetime(sparks, 0.05f, 0.5f);
        }

        private void BuildIslands(ArenaArtSet art, Material rock, System.Random random)
        {
            Transform group = new GameObject("Floating Islands").transform;
            group.SetParent(transform, false);
            for (int i = 0; i < IslandCount; i++)
            {
                float angle = (i + (float)random.NextDouble() * 0.6f) / IslandCount * Mathf.PI * 2f;
                float distance = radius + 7f + (float)random.NextDouble() * 18f;
                float size = 1.2f + (float)random.NextDouble() * 2.6f;
                Vector3 position = center + new Vector3(Mathf.Cos(angle) * distance, -2.5f - (float)random.NextDouble() * 9f, Mathf.Sin(angle) * distance);

                Transform holder = new GameObject("Island " + i).transform;
                holder.SetParent(group, false);
                holder.localPosition = position;
                AddMeshTo(holder, "Rock", Own(BuildRockChunk(size, size * (1.6f + (float)random.NextDouble()), random)), rock, Vector3.zero, false);

                if (art != null && art.Props.Length > 0 && random.NextDouble() < 0.55)
                {
                    GameObject prop = Instantiate(art.Props[random.Next(art.Props.Length)], holder, false);
                    prop.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                    prop.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                    prop.transform.localScale = Vector3.one * art.DungeonScale;
                }
                else if (art != null && art.Pillar != null && random.NextDouble() < 0.5)
                {
                    // Broken column stumps hint at a ruined temple that once surrounded the arena.
                    GameObject pillar = Instantiate(art.Pillar, holder, false);
                    pillar.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                    pillar.transform.localRotation = Quaternion.Euler((float)random.NextDouble() * 14f - 7f, (float)random.NextDouble() * 360f, 0f);
                    pillar.transform.localScale = new Vector3(art.DungeonScale, art.DungeonScale * (0.35f + (float)random.NextDouble() * 0.5f), art.DungeonScale);
                }

                floaters.Add(new Floater
                {
                    Transform = holder,
                    Base = position,
                    Amplitude = 0.25f + (float)random.NextDouble() * 0.35f,
                    Speed = 0.25f + (float)random.NextDouble() * 0.3f,
                    Phase = (float)random.NextDouble() * 10f,
                    Yaw = (float)random.NextDouble() * 360f,
                    Spin = ((float)random.NextDouble() - 0.5f) * 3f
                });
            }
        }

        private void AddMesh(string name, Mesh mesh, Material material, Vector3 localPosition, bool receiveShadows)
        {
            GameObject meshObject = AddMeshTo(transform, name, mesh, material, localPosition, receiveShadows);
            meshObject.transform.position = center + localPosition;
        }

        private static GameObject AddMeshTo(Transform parent, string name, Mesh mesh, Material material, Vector3 localPosition, bool receiveShadows)
        {
            GameObject meshObject = new GameObject(name);
            meshObject.transform.SetParent(parent, false);
            meshObject.transform.localPosition = localPosition;
            meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.receiveShadows = receiveShadows;
            bool transparent = material.renderQueue >= 3000;
            renderer.shadowCastingMode = transparent ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            return meshObject;
        }

        private T Own<T>(T asset) where T : Object
        {
            owned.Add(asset);
            return asset;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i] != null)
                {
                    Destroy(owned[i]);
                }
            }
            owned.Clear();
        }

        // ---------------------------------------------------------------- meshes

        /// <summary>Flat disc (y = 0) with planar UVs so the stone texture is continuous across pieces.</summary>
        private static Mesh BuildDisc(float discRadius, float height)
        {
            int rings = Mathf.Max(4, Mathf.CeilToInt(discRadius / 1.5f));
            List<Vector3> vertices = new List<Vector3> { new Vector3(0f, height, 0f) };
            List<int> triangles = new List<int>();
            for (int ring = 1; ring <= rings; ring++)
            {
                float r = discRadius * ring / rings;
                for (int s = 0; s < Segments; s++)
                {
                    float angle = s / (float)Segments * Mathf.PI * 2f;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * r, height, Mathf.Sin(angle) * r));
                }
            }

            for (int s = 0; s < Segments; s++)
            {
                triangles.Add(0);
                triangles.Add(1 + (s + 1) % Segments);
                triangles.Add(1 + s);
            }
            for (int ring = 1; ring < rings; ring++)
            {
                int inner = 1 + (ring - 1) * Segments;
                int outer = 1 + ring * Segments;
                for (int s = 0; s < Segments; s++)
                {
                    int next = (s + 1) % Segments;
                    triangles.Add(inner + s);
                    triangles.Add(inner + next);
                    triangles.Add(outer + s);
                    triangles.Add(inner + next);
                    triangles.Add(outer + next);
                    triangles.Add(outer + s);
                }
            }

            return Finish("Arena Disc", vertices, triangles, true);
        }

        private static Mesh BuildAnnulus(float innerRadius, float outerRadius, float height, float alpha)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            for (int s = 0; s < Segments; s++)
            {
                float angle = s / (float)Segments * Mathf.PI * 2f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices.Add(direction * innerRadius + new Vector3(0f, height, 0f));
                vertices.Add(direction * outerRadius + new Vector3(0f, height, 0f));
            }
            for (int s = 0; s < Segments; s++)
            {
                int a = s * 2;
                int b = ((s + 1) % Segments) * 2;
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(a + 1);
                triangles.Add(b);
                triangles.Add(b + 1);
                triangles.Add(a + 1);
            }
            return Finish("Arena Ring", vertices, triangles, true);
        }

        /// <summary>Additive band whose vertex alpha rises toward the edge: a warm "danger" glow on the rim.</summary>
        private static Mesh BuildGlowBand(float innerRadius, float outerRadius, float height)
        {
            Vector3[] vertices = new Vector3[Segments * 2];
            Color[] colors = new Color[vertices.Length];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[Segments * 6];
            for (int s = 0; s < Segments; s++)
            {
                float angle = s / (float)Segments * Mathf.PI * 2f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[s * 2] = direction * innerRadius + new Vector3(0f, height, 0f);
                vertices[s * 2 + 1] = direction * outerRadius + new Vector3(0f, height, 0f);
                colors[s * 2] = new Color(1f, 1f, 1f, 0f);
                colors[s * 2 + 1] = Color.white;
                uvs[s * 2] = new Vector2(0.5f, 0.5f);
                uvs[s * 2 + 1] = new Vector2(0.5f, 0.5f);
                int a = s * 2;
                int b = ((s + 1) % Segments) * 2;
                int t = s * 6;
                triangles[t] = a;
                triangles[t + 1] = b;
                triangles[t + 2] = a + 1;
                triangles[t + 3] = b;
                triangles[t + 4] = b + 1;
                triangles[t + 5] = a + 1;
            }
            Mesh mesh = new Mesh { name = "Arena Edge Glow" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Rocky cliff face under the rim tapering into a jagged point far below (a floating island).</summary>
        private static Mesh BuildCliff(float topRadius, float seed)
        {
            const int layers = 9;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();
            int columns = Segments + 1;
            float circumference = Mathf.PI * 2f * topRadius;
            for (int layer = 0; layer <= layers; layer++)
            {
                float t = layer / (float)layers;
                float y;
                float layerRadius;
                if (layer == 0)
                {
                    y = 0.02f;
                    layerRadius = topRadius;
                }
                else if (layer == 1)
                {
                    y = -SideDepth;
                    layerRadius = topRadius + 0.08f;
                }
                else
                {
                    float u = (layer - 1) / (float)(layers - 1);
                    y = -SideDepth - u * UndersideDepth;
                    layerRadius = Mathf.Lerp(topRadius + 0.08f, topRadius * 0.08f, Mathf.Pow(u, 0.8f));
                }

                for (int s = 0; s <= Segments; s++)
                {
                    float angle = s / (float)Segments * Mathf.PI * 2f;
                    float jagged = layer == 0 ? 0f : (Mathf.PerlinNoise(s * 0.21f + seed, layer * 0.9f) - 0.5f) * (layer == 1 ? 0.25f : 1.6f * t + 0.3f);
                    float r = Mathf.Max(0.1f, layerRadius + jagged);
                    vertices.Add(new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r));
                    uvs.Add(new Vector2(s / (float)Segments * circumference / TextureWorldSize, y / TextureWorldSize));
                }
            }

            for (int layer = 0; layer < layers; layer++)
            {
                for (int s = 0; s < Segments; s++)
                {
                    int a = layer * columns + s;
                    int b = a + columns;
                    triangles.Add(a);
                    triangles.Add(a + 1);
                    triangles.Add(b);
                    triangles.Add(a + 1);
                    triangles.Add(b + 1);
                    triangles.Add(b);
                }
            }

            Mesh mesh = new Mesh { name = "Arena Cliff" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A small floating rock: flat top, jagged tapering underside.</summary>
        private static Mesh BuildRockChunk(float topRadius, float depth, System.Random random)
        {
            const int sides = 14;
            const int layers = 5;
            float seed = (float)random.NextDouble() * 50f;
            List<Vector3> vertices = new List<Vector3> { Vector3.zero };
            List<Vector2> uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            List<int> triangles = new List<int>();
            for (int layer = 0; layer <= layers; layer++)
            {
                float t = layer / (float)layers;
                float y = layer == 0 ? 0f : -depth * Mathf.Pow(t, 0.9f);
                float layerRadius = topRadius * (layer == 0 ? 1f : Mathf.Lerp(1.05f, 0.1f, Mathf.Pow(t, 0.75f)));
                for (int s = 0; s <= sides; s++)
                {
                    float angle = s / (float)sides * Mathf.PI * 2f;
                    float noise = (Mathf.PerlinNoise(s % sides * 0.7f + seed, layer * 0.8f + seed) - 0.5f) * topRadius * 0.45f;
                    float r = Mathf.Max(0.05f, layerRadius + noise);
                    vertices.Add(new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r));
                    uvs.Add(new Vector2(s / (float)sides * 2f, y / 2f));
                }
            }

            int columns = sides + 1;
            for (int s = 0; s < sides; s++)
            {
                triangles.Add(0);
                triangles.Add(1 + s + 1);
                triangles.Add(1 + s);
            }
            for (int layer = 0; layer < layers; layer++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = 1 + layer * columns + s;
                    int b = a + columns;
                    triangles.Add(a);
                    triangles.Add(a + 1);
                    triangles.Add(b);
                    triangles.Add(a + 1);
                    triangles.Add(b + 1);
                    triangles.Add(b);
                }
            }

            Mesh mesh = new Mesh { name = "Floating Rock" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh Finish(string name, List<Vector3> vertices, List<int> triangles, bool planarUv)
        {
            Mesh mesh = new Mesh { name = name };
            if (vertices.Count > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            mesh.SetVertices(vertices);
            if (planarUv)
            {
                List<Vector2> uvs = new List<Vector2>(vertices.Count);
                List<Vector3> normals = new List<Vector3>(vertices.Count);
                List<Vector4> tangents = new List<Vector4>(vertices.Count);
                for (int i = 0; i < vertices.Count; i++)
                {
                    uvs.Add(new Vector2(vertices[i].x / TextureWorldSize, vertices[i].z / TextureWorldSize));
                    normals.Add(Vector3.up);
                    tangents.Add(new Vector4(1f, 0f, 0f, -1f));
                }
                mesh.SetUVs(0, uvs);
                mesh.SetNormals(normals);
                mesh.SetTangents(tangents);
            }
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------------------------------------------------------- textures

        private static Texture2D StoneAlbedo
        {
            get
            {
                if (stoneAlbedo == null)
                {
                    BuildStoneTextures();
                }
                return stoneAlbedo;
            }
        }

        private static Texture2D StoneNormal
        {
            get
            {
                if (stoneNormal == null)
                {
                    BuildStoneTextures();
                }
                return stoneNormal;
            }
        }

        /// <summary>
        /// Tileable flagstone texture: jittered Voronoi slabs with dark grout, per-slab colour variation,
        /// tileable value-noise wear, and a matching normal map derived from the height.
        /// </summary>
        private static void BuildStoneTextures()
        {
            const int size = 256;
            const int cells = 4;
            System.Random random = new System.Random(77);
            Vector2[,] points = new Vector2[cells, cells];
            float[,] tone = new float[cells, cells];
            float[,] warmth = new float[cells, cells];
            for (int x = 0; x < cells; x++)
            {
                for (int y = 0; y < cells; y++)
                {
                    points[x, y] = new Vector2(x + 0.15f + (float)random.NextDouble() * 0.7f, y + 0.15f + (float)random.NextDouble() * 0.7f);
                    tone[x, y] = 0.82f + (float)random.NextDouble() * 0.3f;
                    warmth[x, y] = (float)random.NextDouble();
                }
            }

            float[] height = new float[size * size];
            Color32[] albedo = new Color32[size * size];
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float u = px / (float)size * cells;
                    float v = py / (float)size * cells;
                    float nearest = float.MaxValue;
                    float second = float.MaxValue;
                    int nearestX = 0;
                    int nearestY = 0;
                    int cellX = Mathf.FloorToInt(u);
                    int cellY = Mathf.FloorToInt(v);
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        for (int oy = -1; oy <= 1; oy++)
                        {
                            int gx = cellX + ox;
                            int gy = cellY + oy;
                            int wx = ((gx % cells) + cells) % cells;
                            int wy = ((gy % cells) + cells) % cells;
                            Vector2 point = points[wx, wy] + new Vector2(gx - wx, gy - wy);
                            float distance = Vector2.Distance(point, new Vector2(u, v));
                            if (distance < nearest)
                            {
                                second = nearest;
                                nearest = distance;
                                nearestX = wx;
                                nearestY = wy;
                            }
                            else if (distance < second)
                            {
                                second = distance;
                            }
                        }
                    }

                    float edge = second - nearest;
                    float slab = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((edge - 0.03f) / 0.09f));
                    float detail = TileNoise(u * 4f, v * 4f, cells * 4) * 0.6f + TileNoise(u * 11f, v * 11f, cells * 11) * 0.4f;
                    float wear = TileNoise(u * 1.5f + 7f, v * 1.5f + 3f, Mathf.RoundToInt(cells * 1.5f));
                    height[py * size + px] = slab * (0.75f + 0.25f * detail) - wear * 0.12f * slab;

                    float shade = tone[nearestX, nearestY] * (0.78f + 0.3f * detail) * Mathf.Lerp(0.32f, 1f, slab);
                    float warm = warmth[nearestX, nearestY];
                    Color color = new Color(
                        0.43f + 0.05f * warm,
                        0.41f + 0.02f * warm,
                        0.42f - 0.03f * warm) * shade;
                    albedo[py * size + px] = color;
                }
            }

            Color32[] normals = new Color32[size * size];
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float left = height[py * size + (px + size - 1) % size];
                    float right = height[py * size + (px + 1) % size];
                    float down = height[((py + size - 1) % size) * size + px];
                    float up = height[((py + 1) % size) * size + px];
                    Vector3 normal = new Vector3((left - right) * 3.2f, (down - up) * 3.2f, 1f).normalized;
                    normals[py * size + px] = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f);
                }
            }

            stoneAlbedo = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Arena Stone", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            stoneAlbedo.SetPixels32(albedo);
            stoneAlbedo.Apply(true, true);
            // Linear, uncompressed normal map. Alpha stays 1 so UnpackNormalmapRGorAG (x = r * a) reads plain RGB.
            stoneNormal = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { name = "Arena Stone Normal", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            stoneNormal.SetPixels32(normals);
            stoneNormal.Apply(true, true);
        }

        /// <summary>Periodic value noise in [0,1] that repeats every <paramref name="period"/> units.</summary>
        private static float TileNoise(float x, float y, int period)
        {
            period = Mathf.Max(1, period);
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(x0, y0, period);
            float b = Hash(x0 + 1, y0, period);
            float c = Hash(x0, y0 + 1, period);
            float d = Hash(x0 + 1, y0 + 1, period);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Hash(int x, int y, int period)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }

        private sealed class Floater
        {
            public Transform Transform;
            public Vector3 Base;
            public float Amplitude;
            public float Speed;
            public float Phase;
            public float Yaw;
            public float Spin;
            public float Tilt = 3f;
        }
    }
}
