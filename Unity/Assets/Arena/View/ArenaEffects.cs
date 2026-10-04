using System.Collections.Generic;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// Pooled combat effects for the arena view: sparks, dust and smoke puffs, shockwave rings, flashes,
    /// sparkles, sword slashes, floating combat text, dash afterimages, stun stars, the block shield and trails.
    /// Everything runs on unscaled time so effects stay readable while the simulation is paused or sped up.
    /// </summary>
    public sealed partial class ArenaEffects : MonoBehaviour
    {
        private const int SlashPool = 6;
        private const int LabelPool = 48;
        private const int MaxGhosts = 14;

        private readonly List<SlashFx> slashes = new List<SlashFx>();
        private readonly List<Label> labels = new List<Label>();
        private readonly List<Ghost> ghosts = new List<Ghost>();
        private readonly Stack<Mesh> meshPool = new Stack<Mesh>();
        private readonly List<StunStars> stunStars = new List<StunStars>();
        private readonly List<ShieldGlow> shields = new List<ShieldGlow>();
        private readonly List<Object> owned = new List<Object>();
        private Color32[] whiteColors = new Color32[0];
        private static MaterialPropertyBlock slashBlock;

        private ParticleSystem sparks;
        private ParticleSystem puffs;
        private ParticleSystem rings;
        private ParticleSystem flashes;
        private ParticleSystem sparkles;
        private MaterialPropertyBlock block;
        private Font font;
        private Transform poolRoot;

        private void Awake()
        {
            EnsureReady();
        }

        private void EnsureReady()
        {
            if (sparks != null)
            {
                return;
            }

            block = new MaterialPropertyBlock();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            poolRoot = new GameObject("Effects").transform;
            poolRoot.SetParent(transform, false);

            sparks = FxAssets.CreateParticles("Sparks", poolRoot, FxAssets.SparkMaterial, 600, ParticleSystemRenderMode.Stretch);
            ParticleSystem.MainModule sparkMain = sparks.main;
            sparkMain.gravityModifier = 1.1f;
            ParticleSystem.LimitVelocityOverLifetimeModule sparkDrag = sparks.limitVelocityOverLifetime;
            sparkDrag.enabled = true;
            sparkDrag.drag = 2.2f;
            sparkDrag.multiplyDragByParticleSize = false;
            sparkDrag.multiplyDragByParticleVelocity = true;
            ParticleSystem.ColorOverLifetimeModule sparkColor = sparks.colorOverLifetime;
            sparkColor.enabled = true;
            Gradient sparkGradient = new Gradient();
            sparkGradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) });
            sparkColor.color = new ParticleSystem.MinMaxGradient(sparkGradient);
            FxAssets.SizeOverLifetime(sparks, 1f, 0.4f);

            puffs = FxAssets.CreateParticles("Puffs", poolRoot, FxAssets.SmokeMaterial, 400);
            ParticleSystem.LimitVelocityOverLifetimeModule puffDrag = puffs.limitVelocityOverLifetime;
            puffDrag.enabled = true;
            puffDrag.drag = 3f;
            puffDrag.multiplyDragByParticleSize = false;
            puffDrag.multiplyDragByParticleVelocity = true;
            ParticleSystem.RotationOverLifetimeModule puffSpin = puffs.rotationOverLifetime;
            puffSpin.enabled = true;
            puffSpin.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            FxAssets.FadeOverLifetime(puffs, 0.1f, 0.35f);
            FxAssets.SizeOverLifetime(puffs, 0.55f, 1.35f);

            rings = FxAssets.CreateParticles("Rings", poolRoot, FxAssets.RingMaterial, 60, ParticleSystemRenderMode.HorizontalBillboard);
            FxAssets.FadeOverLifetime(rings, 0.02f, 0.3f);
            ParticleSystem.SizeOverLifetimeModule ringSize = rings.sizeOverLifetime;
            ringSize.enabled = true;
            ringSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.15f, 0f, 4f), new Keyframe(0.35f, 0.8f, 1.2f, 1.2f), new Keyframe(1f, 1f, 0.1f, 0f)));

            flashes = FxAssets.CreateParticles("Flashes", poolRoot, FxAssets.GlowMaterial, 60);
            FxAssets.FadeOverLifetime(flashes, 0.01f, 0.2f);
            FxAssets.SizeOverLifetime(flashes, 0.7f, 1.3f);

            sparkles = FxAssets.CreateParticles("Sparkles", poolRoot, FxAssets.StarMaterial, 300);
            ParticleSystem.RotationOverLifetimeModule sparkleSpin = sparkles.rotationOverLifetime;
            sparkleSpin.enabled = true;
            sparkleSpin.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            FxAssets.FadeOverLifetime(sparkles, 0.1f, 0.5f);
            FxAssets.SizeOverLifetime(sparkles, 1f, 0.3f);
        }

        // ------------------------------------------------------------------ bursts

        /// <summary>Hot sparks thrown mostly along <paramref name="direction"/> (world), e.g. a weapon impact.</summary>
        public void Sparks(Vector3 position, Vector3 direction, Color color, int count, float speed = 6f, float spread = 0.8f)
        {
            EnsureReady();
            if (StoreSparks(position, color, count))
            {
                return;
            }
            Vector3 forward = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.up;
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector3 random = Random.insideUnitSphere * spread;
                Vector3 velocity = (forward + random + Vector3.up * 0.35f).normalized * (speed * Random.Range(0.45f, 1.15f));
                emit.position = position;
                emit.velocity = velocity;
                emit.startLifetime = Random.Range(0.22f, 0.5f);
                emit.startSize = Random.Range(0.05f, 0.11f);
                emit.startColor = Color.Lerp(color, Color.white, Random.Range(0f, 0.45f));
                sparks.Emit(emit, 1);
            }
        }

        /// <summary>Soft dust or smoke puffs; <paramref name="spread"/> is the horizontal speed of the ring.</summary>
        public void Puff(Vector3 position, Color color, int count, float size, float spread = 1.4f, float lifetime = 0.9f, float rise = 0.4f)
        {
            EnsureReady();
            if (StorePuff(position, color, count, size * 0.5f + spread * lifetime * 0.6f))
            {
                return;
            }
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(0.3f, 1f);
                emit.position = position + new Vector3(ring.x, 0f, ring.y) * (size * 0.25f);
                emit.velocity = new Vector3(ring.x * spread, rise * Random.Range(0.5f, 1.5f), ring.y * spread);
                emit.startLifetime = lifetime * Random.Range(0.75f, 1.25f);
                emit.startSize = size * Random.Range(0.7f, 1.3f);
                emit.rotation = Random.Range(0f, 360f);
                emit.startColor = color;
                puffs.Emit(emit, 1);
            }
        }

        /// <summary>Expanding ring flat on the floor.</summary>
        public void Shockwave(Vector3 position, Color color, float size, float lifetime = 0.45f)
        {
            EnsureReady();
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = new Vector3(position.x, position.y + 0.06f, position.z),
                velocity = Vector3.zero,
                startLifetime = lifetime * AreaLinger,
                startSize = size,
                startColor = color
            };
            rings.Emit(emit, 1);
        }

        /// <summary>Short bright glow billboard.</summary>
        public void Flash(Vector3 position, Color color, float size, float lifetime = 0.2f)
        {
            EnsureReady();
            if (StoreFlash(position, color, size))
            {
                return;
            }
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = Vector3.zero,
                startLifetime = lifetime,
                startSize = size,
                startColor = color
            };
            flashes.Emit(emit, 1);
        }

        /// <summary>Twinkling stars drifting upward (heals, spawns, pickups).</summary>
        public void Sparkle(Vector3 position, Color color, int count, float radius, float rise = 1.4f, float size = 0.22f)
        {
            EnsureReady();
            if (StoreTwinkle(position, color, count, radius))
            {
                return;
            }
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                emit.position = position + new Vector3(offset.x, Random.Range(0f, 0.6f), offset.y);
                emit.velocity = new Vector3(offset.x * 0.3f, rise * Random.Range(0.6f, 1.3f), offset.y * 0.3f);
                emit.startLifetime = Random.Range(0.6f, 1.1f);
                emit.startSize = size * Random.Range(0.6f, 1.2f);
                emit.rotation = Random.Range(0f, 360f);
                emit.startColor = color;
                sparkles.Emit(emit, 1);
            }
        }

        /// <summary>Glowing crescent sweeping in front of <paramref name="position"/> along <paramref name="yawDegrees"/>.</summary>
        public void Slash(Vector3 position, float yawDegrees, Color color, bool mirror, float reach = 1.3f, float lifetime = 0.2f)
        {
            EnsureReady();
            if (StoreSlash(position, yawDegrees, color, reach))
            {
                return;
            }
            SlashFx slash = null;
            for (int i = 0; i < slashes.Count; i++)
            {
                if (!slashes[i].Active)
                {
                    slash = slashes[i];
                    break;
                }
            }
            if (slash == null)
            {
                if (slashes.Count >= SlashPool)
                {
                    slash = slashes[0];
                    slashes.RemoveAt(0);
                    slashes.Add(slash);
                }
                else
                {
                    slash = new SlashFx();
                    GameObject slashObject = new GameObject("Slash");
                    slashObject.transform.SetParent(poolRoot, false);
                    slashObject.AddComponent<MeshFilter>().sharedMesh = FxAssets.SlashArc;
                    slash.Renderer = slashObject.AddComponent<MeshRenderer>();
                    slash.Renderer.sharedMaterial = FxAssets.GhostMaterial;
                    slash.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    slash.Renderer.receiveShadows = false;
                    slash.Transform = slashObject.transform;
                    slashes.Add(slash);
                }
            }

            slash.Active = true;
            slash.Age = 0f;
            slash.Lifetime = lifetime;
            slash.Position = position;
            slash.Yaw = yawDegrees;
            slash.Mirror = mirror;
            slash.Reach = reach;
            slash.Color = color;
            slash.Transform.gameObject.SetActive(true);
            UpdateSlash(slash);
        }

        /// <summary>Rising, fading world-space text (damage numbers, PARRY!, +30).</summary>
        public void Text(Vector3 position, string text, Color color, float scale = 1f, float lifetime = 1f)
        {
            EnsureReady();
            Label label = null;
            for (int i = 0; i < labels.Count; i++)
            {
                if (!labels[i].Active)
                {
                    label = labels[i];
                    break;
                }
            }
            if (label == null)
            {
                if (labels.Count >= LabelPool)
                {
                    label = labels[0];
                    labels.RemoveAt(0);
                    labels.Add(label);
                }
                else
                {
                    label = CreateLabel();
                    labels.Add(label);
                }
            }

            label.Active = true;
            label.Age = 0f;
            label.Lifetime = lifetime;
            label.Start = position + new Vector3(Random.Range(-0.15f, 0.15f), 0f, Random.Range(-0.15f, 0.15f));
            label.Scale = scale;
            label.Color = color;
            label.Text.text = text;
            label.Shadow.text = text;
            label.Transform.gameObject.SetActive(true);
            UpdateLabel(label, Camera.main);
        }

        /// <summary>Freezes the current pose of <paramref name="renderers"/> as a fading glowing silhouette.</summary>
        public void Afterimage(Renderer[] renderers, Color color, float lifetime = 0.32f)
        {
            EnsureReady();
            if (renderers == null)
            {
                return;
            }
            if (ghosts.Count >= MaxGhosts)
            {
                ReleaseGhost(ghosts[0]);
                ghosts.RemoveAt(0);
            }

            Ghost ghost = new Ghost { Color = color, Lifetime = lifetime };
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer source = renderers[i];
                if (source == null || !source.enabled || !source.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (source is SkinnedMeshRenderer skinned)
                {
                    if (skinned.sharedMesh == null)
                    {
                        continue;
                    }
                    Mesh baked = meshPool.Count > 0 ? meshPool.Pop() : new Mesh { name = "Afterimage" };
                    skinned.BakeMesh(baked, false);
                    // The Fx shader multiplies by vertex colour; baked meshes may have none, so force white.
                    int vertexCount = baked.vertexCount;
                    if (whiteColors.Length < vertexCount)
                    {
                        whiteColors = new Color32[vertexCount];
                        for (int c = 0; c < vertexCount; c++)
                        {
                            whiteColors[c] = new Color32(255, 255, 255, 255);
                        }
                    }
                    baked.SetColors(whiteColors, 0, vertexCount);
                    ghost.Parts.Add(new GhostPart { Mesh = baked, Matrix = skinned.transform.localToWorldMatrix, Pooled = true });
                }
                else if (source is MeshRenderer)
                {
                    MeshFilter filter = source.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null)
                    {
                        ghost.Parts.Add(new GhostPart { Mesh = filter.sharedMesh, Matrix = source.transform.localToWorldMatrix, Pooled = false });
                    }
                }
            }

            if (ghost.Parts.Count > 0)
            {
                ghosts.Add(ghost);
            }
        }

        /// <summary>Three stars circling above a stunned actor. Toggle with <see cref="StunStars.SetVisible"/>.</summary>
        public StunStars CreateStunStars(Transform parent, float height)
        {
            EnsureReady();
            StunStars stars = new StunStars();
            stars.Root = new GameObject("Stun Stars").transform;
            stars.Root.SetParent(parent, false);
            stars.Root.localPosition = new Vector3(0f, height, 0f);
            stars.Stars = new MeshRenderer[3];
            for (int i = 0; i < stars.Stars.Length; i++)
            {
                GameObject star = new GameObject("Star " + i);
                star.transform.SetParent(stars.Root, false);
                star.AddComponent<MeshFilter>().sharedMesh = FxAssets.FlatQuad;
                MeshRenderer renderer = star.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = FxAssets.StunStarMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                stars.Stars[i] = renderer;
            }
            stars.Phase = Random.Range(0f, 10f);
            // Start hidden. (SetVisible(false) alone is a no-op here because Visible already defaults to false,
            // which used to leave the unpositioned stars lying flat on every head as a white glint.)
            stars.Visible = false;
            stars.Root.gameObject.SetActive(false);
            stunStars.Add(stars);
            return stars;
        }

        /// <summary>Curved force-field in front of the hero shown while blocking; flashes on blocked hits.</summary>
        public ShieldGlow CreateShield(Transform parent)
        {
            EnsureReady();
            GameObject shieldObject = new GameObject("Block Shield");
            shieldObject.transform.SetParent(parent, false);
            shieldObject.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            shieldObject.AddComponent<MeshFilter>().sharedMesh = FxAssets.ShieldBand;
            MeshRenderer renderer = shieldObject.AddComponent<MeshRenderer>();
            Material material = FxAssets.Create("Block Shield", FxAssets.Ring, true);
            material.mainTexture = Texture2D.whiteTexture;
            owned.Add(material);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            ShieldGlow shield = new ShieldGlow { Transform = shieldObject.transform, Renderer = renderer, Material = material };
            shield.Apply();
            shields.Add(shield);
            return shield;
        }

        /// <summary>Glowing ribbon that follows <paramref name="parent"/> while its <c>emitting</c> flag is on.</summary>
        public TrailRenderer CreateTrail(Transform parent, float height, Color color, float width = 0.9f, float time = 0.24f)
        {
            GameObject trailObject = new GameObject("Trail");
            trailObject.transform.SetParent(parent, false);
            trailObject.transform.localPosition = new Vector3(0f, height, 0f);
            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = FxAssets.GhostMaterial;
            trail.time = time;
            trail.minVertexDistance = 0.08f;
            trail.widthMultiplier = width;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 0.25f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.45f, 0.4f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.alignment = LineAlignment.View;
            trail.emitting = false;
            return trail;
        }

        /// <summary>Drops every running effect (a new round starts).</summary>
        public void Clear()
        {
            EnsureReady();
            ClearVfx();
            ClearStore();
            sparks.Clear();
            puffs.Clear();
            rings.Clear();
            flashes.Clear();
            sparkles.Clear();
            for (int i = 0; i < slashes.Count; i++)
            {
                slashes[i].Active = false;
                slashes[i].Transform.gameObject.SetActive(false);
            }
            for (int i = 0; i < labels.Count; i++)
            {
                labels[i].Active = false;
                labels[i].Transform.gameObject.SetActive(false);
            }
            for (int i = 0; i < ghosts.Count; i++)
            {
                ReleaseGhost(ghosts[i]);
            }
            ghosts.Clear();
        }

        // ------------------------------------------------------------------ animation

        private void LateUpdate()
        {
            float delta = Time.unscaledDeltaTime;
            UpdateVfx(delta);
            UpdateStore(delta);
            Camera camera = Camera.main;

            for (int i = 0; i < slashes.Count; i++)
            {
                SlashFx slash = slashes[i];
                if (!slash.Active)
                {
                    continue;
                }
                slash.Age += delta;
                if (slash.Age >= slash.Lifetime)
                {
                    slash.Active = false;
                    slash.Transform.gameObject.SetActive(false);
                    continue;
                }
                UpdateSlash(slash);
            }

            for (int i = 0; i < labels.Count; i++)
            {
                Label label = labels[i];
                if (!label.Active)
                {
                    continue;
                }
                label.Age += delta;
                if (label.Age >= label.Lifetime)
                {
                    label.Active = false;
                    label.Transform.gameObject.SetActive(false);
                    continue;
                }
                UpdateLabel(label, camera);
            }

            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                Ghost ghost = ghosts[i];
                ghost.Age += delta;
                if (ghost.Age >= ghost.Lifetime)
                {
                    ReleaseGhost(ghost);
                    ghosts.RemoveAt(i);
                    continue;
                }
                float fade = 1f - ghost.Age / ghost.Lifetime;
                Color color = ghost.Color;
                color.a *= fade * fade;
                block.Clear();
                block.SetColor("_Color", color);
                for (int p = 0; p < ghost.Parts.Count; p++)
                {
                    GhostPart part = ghost.Parts[p];
                    for (int sub = 0; sub < part.Mesh.subMeshCount; sub++)
                    {
                        Graphics.DrawMesh(part.Mesh, part.Matrix, FxAssets.GhostMaterial, 0, null, sub, block, false, false);
                    }
                }
            }

            float time = Time.unscaledTime;
            for (int i = 0; i < stunStars.Count; i++)
            {
                StunStars stars = stunStars[i];
                if (stars.Root == null || !stars.Visible)
                {
                    continue;
                }
                for (int s = 0; s < stars.Stars.Length; s++)
                {
                    // Cartoon "dizzy" halo: gold stars circling the head, bobbing and wobbling as they go.
                    float angle = (time * 3.6f + stars.Phase) + s * Mathf.PI * 2f / stars.Stars.Length;
                    Transform star = stars.Stars[s].transform;
                    star.position = stars.Root.position + new Vector3(Mathf.Cos(angle) * 0.5f, 0.12f + Mathf.Sin(angle * 2f) * 0.08f, Mathf.Sin(angle) * 0.5f);
                    if (camera != null)
                    {
                        float wobble = Mathf.Sin(time * 5f + s * 2.1f) * 25f;
                        star.rotation = Quaternion.LookRotation(-camera.transform.forward, camera.transform.up) * Quaternion.Euler(0f, 0f, wobble) * Quaternion.Euler(90f, 0f, 0f);
                    }
                    float pulse = 0.9f + 0.1f * Mathf.Sin(time * 9f + s * 2f);
                    star.localScale = Vector3.one * (0.4f * pulse);
                    block.Clear();
                    block.SetColor("_Color", Color.white);
                    stars.Stars[s].SetPropertyBlock(block);
                }
            }

            for (int i = 0; i < shields.Count; i++)
            {
                ShieldGlow shield = shields[i];
                if (shield.Transform == null)
                {
                    continue;
                }
                shield.Intensity = Mathf.MoveTowards(shield.Intensity, shield.Blocking ? 1f : 0f, delta * (shield.Blocking ? 9f : 5f));
                shield.FlashAmount = Mathf.MoveTowards(shield.FlashAmount, 0f, delta * 4f);
                shield.Apply();
            }
        }

        private static void UpdateSlash(SlashFx slash)
        {
            float t = Mathf.Clamp01(slash.Age / slash.Lifetime);
            float eased = 1f - (1f - t) * (1f - t);
            float sweep = Mathf.Lerp(-55f, 45f, eased) * (slash.Mirror ? -1f : 1f);
            slash.Transform.SetPositionAndRotation(slash.Position,
                Quaternion.Euler(0f, slash.Yaw + sweep, 0f) * Quaternion.Euler(0f, 0f, slash.Mirror ? -14f : 14f));
            float grow = slash.Reach * Mathf.Lerp(0.8f, 1.08f, eased);
            slash.Transform.localScale = new Vector3(slash.Mirror ? -grow : grow, grow, grow);
            Color color = slash.Color;
            color.a *= t < 0.25f ? 1f : Mathf.Pow(Mathf.Clamp01(1f - (t - 0.25f) / 0.75f), 1.5f);
            if (slashBlock == null)
            {
                slashBlock = new MaterialPropertyBlock();
            }
            slashBlock.SetColor("_Color", color);
            slash.Renderer.SetPropertyBlock(slashBlock);
        }

        private Label CreateLabel()
        {
            Label label = new Label();
            GameObject root = new GameObject("Label");
            root.transform.SetParent(poolRoot, false);
            label.Transform = root.transform;

            GameObject shadowObject = new GameObject("Shadow");
            shadowObject.transform.SetParent(root.transform, false);
            shadowObject.transform.localPosition = new Vector3(0.025f, -0.025f, 0.01f);
            label.Shadow = ConfigureText(shadowObject);

            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(root.transform, false);
            label.Text = ConfigureText(textObject);
            return label;
        }

        private TextMesh ConfigureText(GameObject target)
        {
            TextMesh text = target.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = 64;
            text.characterSize = 0.075f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontStyle = FontStyle.Bold;
            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return text;
        }

        private static void UpdateLabel(Label label, Camera camera)
        {
            float t = Mathf.Clamp01(label.Age / label.Lifetime);
            float rise = 1f - (1f - t) * (1f - t);
            label.Transform.position = label.Start + new Vector3(0f, 0.2f + rise * 0.9f, 0f);
            float pop = t < 0.12f ? Mathf.Lerp(1.6f, 1f, t / 0.12f) : 1f;
            label.Transform.localScale = Vector3.one * (label.Scale * pop);
            if (camera != null)
            {
                label.Transform.rotation = camera.transform.rotation;
            }
            float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            Color color = label.Color;
            color.a *= alpha;
            label.Text.color = color;
            label.Shadow.color = new Color(0f, 0f, 0f, 0.75f * alpha);
        }

        private void ReleaseGhost(Ghost ghost)
        {
            for (int i = 0; i < ghost.Parts.Count; i++)
            {
                if (ghost.Parts[i].Pooled)
                {
                    meshPool.Push(ghost.Parts[i].Mesh);
                }
            }
            ghost.Parts.Clear();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < ghosts.Count; i++)
            {
                ReleaseGhost(ghosts[i]);
            }
            ghosts.Clear();
            while (meshPool.Count > 0)
            {
                Destroy(meshPool.Pop());
            }
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i] != null)
                {
                    Destroy(owned[i]);
                }
            }
        }

        private sealed class SlashFx
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public bool Active;
            public float Age;
            public float Lifetime;
            public Vector3 Position;
            public float Yaw;
            public bool Mirror;
            public float Reach;
            public Color Color;
        }

        private sealed class Label
        {
            public Transform Transform;
            public TextMesh Text;
            public TextMesh Shadow;
            public bool Active;
            public float Age;
            public float Lifetime;
            public Vector3 Start;
            public float Scale;
            public Color Color;
        }

        private sealed class Ghost
        {
            public readonly List<GhostPart> Parts = new List<GhostPart>();
            public Color Color;
            public float Age;
            public float Lifetime;
        }

        private struct GhostPart
        {
            public Mesh Mesh;
            public Matrix4x4 Matrix;
            public bool Pooled;
        }

        /// <summary>Handle to an orbiting stun-star group.</summary>
        public sealed class StunStars
        {
            internal Transform Root;
            internal MeshRenderer[] Stars;
            internal float Phase;
            internal bool Visible;

            public void SetVisible(bool visible)
            {
                if (Root == null || Visible == visible)
                {
                    return;
                }
                Visible = visible;
                Root.gameObject.SetActive(visible);
            }
        }

        /// <summary>Handle to the hero's block shield glow.</summary>
        public sealed class ShieldGlow
        {
            private static readonly Color BlockColor = new Color(0.35f, 0.7f, 1f);
            private static readonly Color HitColor = new Color(0.75f, 0.92f, 1f);

            internal Transform Transform;
            internal MeshRenderer Renderer;
            internal Material Material;
            internal float Intensity;
            internal float FlashAmount;
            internal Color FlashColor = HitColor;
            internal bool Blocking;

            public void SetBlocking(bool blocking)
            {
                Blocking = blocking;
            }

            /// <summary>Brightens and swells the shield (a hit was blocked or parried).</summary>
            public void Flash(Color color)
            {
                FlashColor = color;
                FlashAmount = 1f;
                Intensity = Mathf.Max(Intensity, 0.8f);
            }

            internal void Apply()
            {
                bool visible = Intensity > 0.01f || FlashAmount > 0.01f;
                if (Renderer.enabled != visible)
                {
                    Renderer.enabled = visible;
                }
                if (!visible)
                {
                    return;
                }
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 9f);
                Color color = Color.Lerp(BlockColor, FlashColor, FlashAmount);
                color.a = Mathf.Clamp01(Intensity * 0.55f * pulse + FlashAmount * 0.6f);
                Material.color = color;
                float swell = 1f + 0.18f * FlashAmount;
                Transform.localScale = new Vector3(swell, 1f + 0.08f * FlashAmount, swell) * Mathf.Lerp(0.85f, 1f, Intensity);
            }
        }
    }
}
