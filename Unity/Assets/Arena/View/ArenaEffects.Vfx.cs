using System.Collections.Generic;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// M10 effects built on the Kenney Particle Pack textures (<see cref="VfxLibrary"/>): flames, fireballs, smoke,
    /// debris, ground decals, rune circles, twirls, 3D shards and lightning bolts. All pooled; every call is one
    /// or a few <see cref="ParticleSystem.Emit(ParticleSystem.EmitParams, int)"/> bursts.
    /// </summary>
    public sealed partial class ArenaEffects
    {
        private const int BoltPool = 14;
        private const int BoltPoints = 7;
        private const float BoltLifetime = 0.28f;

        private ParticleSystem flames;
        private ParticleSystem fireballs;
        private ParticleSystem smokes;
        private ParticleSystem debris;
        private ParticleSystem decals;
        private ParticleSystem runes;
        private ParticleSystem twirls;
        private ParticleSystem shards;
        private ParticleSystem crystals;
        private readonly List<BoltFx> bolts = new List<BoltFx>();
        private int nextBolt;

        private sealed class BoltFx
        {
            public LineRenderer Line;
            public Color Color;
            public float Age;
            public bool Active;
        }

        private void EnsureVfx()
        {
            if (flames != null)
            {
                return;
            }

            EnsureReady();
            flames = FxAssets.CreateParticles("Flames", poolRoot, VfxLibrary.Additive("muzzle_02"), 700);
            FxAssets.FadeOverLifetime(flames, 0.12f, 0.4f);
            FxAssets.SizeOverLifetime(flames, 1f, 0.35f);

            fireballs = FxAssets.CreateParticles("Fireballs", poolRoot, VfxLibrary.Additive("fire_01"), 160);
            FxAssets.FadeOverLifetime(fireballs, 0.03f, 0.45f);
            FxAssets.SizeOverLifetime(fireballs, 0.45f, 1.35f);

            smokes = FxAssets.CreateParticles("Smoke", poolRoot, VfxLibrary.Blended("smoke_07"), 260);
            ParticleSystem.RotationOverLifetimeModule smokeSpin = smokes.rotationOverLifetime;
            smokeSpin.enabled = true;
            smokeSpin.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            FxAssets.FadeOverLifetime(smokes, 0.15f, 0.4f);
            FxAssets.SizeOverLifetime(smokes, 0.6f, 1.6f);

            debris = FxAssets.CreateParticles("Debris", poolRoot, VfxLibrary.Blended("dirt_02"), 260);
            ParticleSystem.MainModule debrisMain = debris.main;
            debrisMain.gravityModifier = 2.4f;
            FxAssets.FadeOverLifetime(debris, 0.02f, 0.7f);

            decals = FxAssets.CreateParticles("Decals", poolRoot, VfxLibrary.Blended("scorch_03"), 120, ParticleSystemRenderMode.HorizontalBillboard);
            FxAssets.FadeOverLifetime(decals, 0.03f, 0.6f);

            runes = FxAssets.CreateParticles("Runes", poolRoot, VfxLibrary.Additive("light_01"), 60, ParticleSystemRenderMode.HorizontalBillboard);
            ParticleSystem.RotationOverLifetimeModule runeSpin = runes.rotationOverLifetime;
            runeSpin.enabled = true;
            runeSpin.z = 0.9f;
            FxAssets.FadeOverLifetime(runes, 0.12f, 0.65f);

            twirls = FxAssets.CreateParticles("Twirls", poolRoot, VfxLibrary.Additive("twirl_01"), 60, ParticleSystemRenderMode.HorizontalBillboard);
            ParticleSystem.RotationOverLifetimeModule twirlSpin = twirls.rotationOverLifetime;
            twirlSpin.enabled = true;
            twirlSpin.z = -9f;
            FxAssets.FadeOverLifetime(twirls, 0.1f, 0.5f);

            Material shardMaterial = FxAssets.Create("Shard", Texture2D.whiteTexture, false);
            owned.Add(shardMaterial);
            shards = FxAssets.CreateParticles("Shards", poolRoot, shardMaterial, 300, ParticleSystemRenderMode.Mesh);
            Mesh crystal = BuildCrystalMesh();
            owned.Add(crystal);
            ParticleSystemRenderer shardRenderer = shards.GetComponent<ParticleSystemRenderer>();
            shardRenderer.mesh = crystal;
            shardRenderer.alignment = ParticleSystemRenderSpace.World;
            ParticleSystem.MainModule shardMain = shards.main;
            shardMain.gravityModifier = 2.2f;
            shardMain.startRotation3D = true;
            ParticleSystem.RotationOverLifetimeModule shardSpin = shards.rotationOverLifetime;
            shardSpin.enabled = true;
            shardSpin.separateAxes = true;
            shardSpin.x = new ParticleSystem.MinMaxCurve(-5f, 5f);
            shardSpin.y = new ParticleSystem.MinMaxCurve(-5f, 5f);
            shardSpin.z = new ParticleSystem.MinMaxCurve(-5f, 5f);
            FxAssets.FadeOverLifetime(shards, 0.02f, 0.75f);
            FxAssets.SizeOverLifetime(shards, 1f, 0.5f);

            crystals = FxAssets.CreateParticles("Crystals", poolRoot, shardMaterial, 160, ParticleSystemRenderMode.Mesh);
            ParticleSystemRenderer crystalRenderer = crystals.GetComponent<ParticleSystemRenderer>();
            crystalRenderer.mesh = crystal;
            crystalRenderer.alignment = ParticleSystemRenderSpace.World;
            ParticleSystem.MainModule crystalMain = crystals.main;
            crystalMain.startRotation3D = true;
            ParticleSystem.SizeOverLifetimeModule crystalSize = crystals.sizeOverLifetime;
            crystalSize.enabled = true;
            crystalSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.15f), new Keyframe(0.1f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0.25f)));

            Material boltMaterial = FxAssets.Create("Bolt", FxAssets.SoftDot, true);
            owned.Add(boltMaterial);
            for (int i = 0; i < BoltPool; i++)
            {
                GameObject boltObject = new GameObject("Bolt " + i);
                boltObject.transform.SetParent(poolRoot, false);
                LineRenderer line = boltObject.AddComponent<LineRenderer>();
                line.sharedMaterial = boltMaterial;
                line.positionCount = BoltPoints;
                line.useWorldSpace = true;
                line.textureMode = LineTextureMode.Stretch;
                line.alignment = LineAlignment.View;
                line.numCapVertices = 0;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;
                bolts.Add(new BoltFx { Line = line });
            }
        }

        /// <summary>Fireball, flame tongues, smoke, flying dirt and a scorch mark: a blast of the given radius.</summary>
        public void Explosion(Vector3 point, Color color, float radius, bool smoke = true)
        {
            EnsureVfx();
            float size = Mathf.Max(0.6f, radius);
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < 4; i++)
            {
                Vector3 offset = Random.insideUnitSphere * (size * 0.3f);
                emit.position = point + new Vector3(offset.x, 0.5f + Mathf.Abs(offset.y), offset.z);
                emit.velocity = offset * 1.5f + Vector3.up * 0.8f;
                emit.startLifetime = Random.Range(0.45f, 0.7f);
                emit.startSize = size * Random.Range(1.5f, 2.2f);
                emit.rotation = Random.Range(0f, 360f);
                emit.startColor = Color.Lerp(color, Color.white, 0.35f);
                fireballs.Emit(emit, 1);
            }
            int tongues = Mathf.Clamp(Mathf.RoundToInt(size * 4f), 5, 12);
            for (int i = 0; i < tongues; i++)
            {
                Vector2 ring = Random.insideUnitCircle * (size * 0.7f);
                Flame(point + new Vector3(ring.x, 0.1f, ring.y), color, size * Random.Range(0.5f, 0.9f), 0.45f);
            }
            int chunks = Mathf.Clamp(Mathf.RoundToInt(size * 3f), 4, 9);
            emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < chunks; i++)
            {
                Vector2 ring = Random.insideUnitCircle.normalized;
                emit.position = point + Vector3.up * 0.2f;
                emit.velocity = new Vector3(ring.x, Random.Range(1.6f, 3f), ring.y) * (size * Random.Range(1.2f, 2.2f));
                emit.startLifetime = Random.Range(0.5f, 0.8f);
                emit.startSize = Random.Range(0.25f, 0.5f);
                emit.rotation = Random.Range(0f, 360f);
                emit.startColor = new Color(0.32f, 0.26f, 0.2f, 1f);
                debris.Emit(emit, 1);
            }
            if (smoke)
            {
                Smoke(point + Vector3.up * 0.6f, new Color(0.16f, 0.15f, 0.15f, 0.7f), 3, size * 1.4f, 1.3f);
            }
            Decal(point, new Color(0.05f, 0.04f, 0.03f, 0.75f), size * 2.4f, 5f);
            Flash(point + Vector3.up * 0.7f, color, size * 3.2f, 0.22f);
        }

        /// <summary>One flame tongue rising from <paramref name="position"/>; call repeatedly for a burning area.</summary>
        public void Flame(Vector3 position, Color color, float size, float lifetime = 0.7f)
        {
            EnsureVfx();
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = position + Vector3.up * (size * 0.45f),
                velocity = new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0.9f, 1.8f), Random.Range(-0.25f, 0.25f)),
                startLifetime = lifetime * Random.Range(0.7f, 1.2f),
                startSize = size * Random.Range(0.8f, 1.25f),
                startColor = Color.Lerp(color, new Color(1f, 0.92f, 0.55f), Random.Range(0f, 0.5f))
            };
            flames.Emit(emit, 1);
        }

        /// <summary>Thick slow smoke (or coloured fumes) drifting up.</summary>
        public void Smoke(Vector3 position, Color color, int count, float size, float lifetime = 1.2f)
        {
            EnsureVfx();
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector2 ring = Random.insideUnitCircle * (size * 0.3f);
                emit.position = position + new Vector3(ring.x, 0f, ring.y);
                emit.velocity = new Vector3(ring.x * 0.4f, Random.Range(0.6f, 1.3f), ring.y * 0.4f);
                emit.startLifetime = lifetime * Random.Range(0.8f, 1.25f);
                emit.startSize = size * Random.Range(0.7f, 1.2f);
                emit.rotation = Random.Range(0f, 360f);
                emit.startColor = color;
                smokes.Emit(emit, 1);
            }
        }

        /// <summary>A mark lying on the ground that fades out (scorch, frost, stain).</summary>
        public void Decal(Vector3 position, Color color, float size, float lifetime)
        {
            EnsureVfx();
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = new Vector3(position.x, position.y + 0.045f, position.z),
                velocity = Vector3.zero,
                startLifetime = lifetime,
                startSize = size,
                rotation = Random.Range(0f, 360f),
                startColor = color
            };
            decals.Emit(emit, 1);
        }

        /// <summary>A slowly turning circle of light on the ground.</summary>
        public void Rune(Vector3 position, Color color, float size, float lifetime)
        {
            EnsureVfx();
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = new Vector3(position.x, position.y + 0.07f, position.z),
                velocity = Vector3.zero,
                startLifetime = lifetime,
                startSize = size,
                rotation = Random.Range(0f, 360f),
                startColor = color
            };
            runes.Emit(emit, 1);
        }

        /// <summary>A fast spinning swirl lying flat (whirlwinds, spins, dashes).</summary>
        public void Twirl(Vector3 position, Color color, float size, float lifetime = 0.35f)
        {
            EnsureVfx();
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = Vector3.zero,
                startLifetime = lifetime,
                startSize = size,
                rotation = Random.Range(0f, 360f),
                startColor = color
            };
            twirls.Emit(emit, 1);
        }

        /// <summary>Solid crystal chips thrown up and out, tumbling and falling (ice, spikes, stone).</summary>
        public void Shards(Vector3 position, Color color, int count, float speed, float size)
        {
            EnsureVfx();
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(0.4f, 1f);
                emit.position = position + new Vector3(ring.x * 0.3f, 0.3f, ring.y * 0.3f);
                emit.velocity = new Vector3(ring.x * speed, speed * Random.Range(0.8f, 1.5f), ring.y * speed);
                emit.startLifetime = Random.Range(0.55f, 0.9f);
                emit.startSize3D = new Vector3(0.5f, 1f, 0.5f) * (size * Random.Range(0.6f, 1.3f));
                emit.rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
                emit.startColor = Color.Lerp(color, Color.white, Random.Range(0f, 0.5f));
                shards.Emit(emit, 1);
            }
        }

        /// <summary>Crystals that shoot up out of the ground in a patch, stand for a while, then shrink away (ice, stone).</summary>
        public void Crystals(Vector3 position, Color color, int count, float radius, float height, float lifetime, float thickness = 0.38f)
        {
            EnsureVfx();
            float lean = 22f / Mathf.Max(radius, 0.1f);
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector2 ring = i == 0 ? Vector2.zero : Random.insideUnitCircle * radius;
                float tall = height * Random.Range(0.55f, 1.15f) * (i == 0 ? 1.25f : 1f);
                emit.position = position + new Vector3(ring.x, tall * 0.3f, ring.y);
                emit.velocity = Vector3.zero;
                emit.startLifetime = lifetime * Random.Range(0.8f, 1.1f);
                emit.startSize3D = new Vector3(tall * thickness, tall, tall * thickness);
                // Lean away from the centre like a cluster.
                emit.rotation3D = new Vector3(ring.y * lean, Random.Range(0f, 360f), -ring.x * lean);
                emit.startColor = Color.Lerp(color, Color.white, Random.Range(0.1f, 0.6f));
                crystals.Emit(emit, 1);
            }
        }

        /// <summary>A jagged lightning bolt between two points that flickers out.</summary>
        public void Bolt(Vector3 from, Vector3 to, Color color, float width = 0.7f)
        {
            EnsureVfx();
            BoltFx bolt = bolts[nextBolt];
            nextBolt = (nextBolt + 1) % bolts.Count;
            Vector3 along = to - from;
            Vector3 side = Vector3.Cross(along.normalized, Vector3.up);
            if (side.sqrMagnitude < 1e-3f)
            {
                side = Vector3.right;
            }
            float jitter = Mathf.Min(0.9f, along.magnitude * 0.12f);
            for (int i = 0; i < BoltPoints; i++)
            {
                float t = i / (float)(BoltPoints - 1);
                Vector3 point = from + along * t;
                if (i > 0 && i < BoltPoints - 1)
                {
                    point += side.normalized * Random.Range(-jitter, jitter) + Vector3.up * Random.Range(-jitter, jitter) * 0.4f;
                }
                bolt.Line.SetPosition(i, point);
            }
            bolt.Line.widthMultiplier = width * 1.7f;
            color = Color.Lerp(color, Color.white, 0.45f);
            bolt.Color = color;
            bolt.Age = 0f;
            bolt.Active = true;
            bolt.Line.startColor = color;
            bolt.Line.endColor = color;
            bolt.Line.enabled = true;
        }

        private void UpdateVfx(float delta)
        {
            for (int i = 0; i < bolts.Count; i++)
            {
                BoltFx bolt = bolts[i];
                if (!bolt.Active)
                {
                    continue;
                }
                bolt.Age += delta;
                if (bolt.Age >= BoltLifetime)
                {
                    bolt.Active = false;
                    bolt.Line.enabled = false;
                    continue;
                }
                // A flicker rather than a smooth fade reads as electricity.
                float alpha = (1f - bolt.Age / BoltLifetime) * (0.65f + 0.35f * Mathf.Sin(bolt.Age * 150f));
                Color color = new Color(bolt.Color.r, bolt.Color.g, bolt.Color.b, bolt.Color.a * alpha);
                bolt.Line.startColor = color;
                bolt.Line.endColor = color;
            }
        }

        private void ClearVfx()
        {
            if (flames == null)
            {
                return;
            }
            flames.Clear();
            fireballs.Clear();
            smokes.Clear();
            debris.Clear();
            decals.Clear();
            runes.Clear();
            twirls.Clear();
            shards.Clear();
            crystals.Clear();
            for (int i = 0; i < bolts.Count; i++)
            {
                bolts[i].Active = false;
                bolts[i].Line.enabled = false;
            }
        }

        /// <summary>A six-sided crystal, one unit tall, pointed at both ends.</summary>
        private static Mesh BuildCrystalMesh()
        {
            const int sides = 5;
            Vector3[] vertices = new Vector3[sides + 2];
            int[] triangles = new int[sides * 6];
            vertices[sides] = new Vector3(0f, 0.5f, 0f);
            vertices[sides + 1] = new Vector3(0f, -0.5f, 0f);
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                vertices[i] = new Vector3(Mathf.Cos(angle) * 0.5f, -0.12f, Mathf.Sin(angle) * 0.5f);
                int next = (i + 1) % sides;
                triangles[i * 6] = i;
                triangles[i * 6 + 1] = sides;
                triangles[i * 6 + 2] = next;
                triangles[i * 6 + 3] = next;
                triangles[i * 6 + 4] = sides + 1;
                triangles[i * 6 + 5] = i;
            }
            Mesh mesh = new Mesh { name = "Fx Crystal" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = new Vector2[vertices.Length];
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
