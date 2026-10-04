using UnityEngine;
using UnityEngine.Rendering;

namespace PersonalArena.View
{
    /// <summary>
    /// M10: lasting area effects (burning fire walls, bubbling poison pools, spike traps), a per-frame cap on
    /// big blasts, and the -fxDemo showcase.
    /// </summary>
    public sealed partial class SurvivorRenderer
    {
        private const int BlastsPerFrame = 5;
        private const int SpikesPerTrap = 7;
        private static readonly Color SpikeColor = new Color(0.72f, 0.74f, 0.8f);
        private static readonly Color PoisonFumeColor = new Color(0.35f, 0.85f, 0.3f, 0.32f);
        private static readonly Color FrostMarkColor = new Color(0.7f, 0.9f, 1f, 0.45f);
        private static readonly Color HolyRuneColor = new Color(1f, 0.95f, 0.65f);
        private static readonly Color ArrowShaftColor = new Color(0.62f, 0.45f, 0.26f);
        private const int FrostChipsPerFrame = 4;
        private int frostChipFrame = -1;
        private int frostChipsThisFrame;
        private static readonly Color EmberColor = new Color(1f, 0.6f, 0.2f);

        private Material spikeMaterial;
        private int blastFrame = -1;
        private int blastsThisFrame;

        /// <summary>A full explosion, or only a flash once this frame already drew several (bomb rings fire many at once).</summary>
        private void Blast(Vector3 point, Color color, float radius)
        {
            if (Time.frameCount != blastFrame)
            {
                blastFrame = Time.frameCount;
                blastsThisFrame = 0;
            }
            if (blastsThisFrame++ < BlastsPerFrame)
            {
                effects.Explosion(point, color, radius);
            }
            else
            {
                effects.Flash(point + Vector3.up * 0.7f, color, radius * 1.8f, 0.22f);
            }
        }

        /// <summary>A slowed (chilled) enemy keeps growing small ice crystals at its feet.</summary>
        private void PresentChill(Vector3 position, float heightScale, float delta)
        {
            if (Random.value > delta * 2.5f)
            {
                return;
            }
            if (Time.frameCount != frostChipFrame)
            {
                frostChipFrame = Time.frameCount;
                frostChipsThisFrame = 0;
            }
            if (frostChipsThisFrame++ < FrostChipsPerFrame)
            {
                Vector2 spot = Random.insideUnitCircle * 0.35f;
                effects.Crystals(position + new Vector3(spot.x, 0f, spot.y), FrostColor, 1, 0f, 0.55f * heightScale, 0.7f);
            }
        }

        /// <summary>Metal spikes standing on every trap disc.</summary>
        private void BuildTrapSpikes()
        {
            Mesh spike = Own(BuildGemMesh());
            spikeMaterial = Own(CreateEmissive("Trap Spike", SpikeColor, SpikeColor * 0.25f, 0.9f, 0.85f));
            for (int i = 0; i < trapViews.Length; i++)
            {
                Transform root = trapViews[i].Root;
                for (int k = 0; k < SpikesPerTrap; k++)
                {
                    GameObject spikeObject = new GameObject("Spike " + k);
                    spikeObject.transform.SetParent(root, false);
                    float angle = k * Mathf.PI * 2f / (SpikesPerTrap - 1);
                    float reach = k == 0 ? 0f : 0.2f;
                    spikeObject.transform.localPosition = new Vector3(Mathf.Cos(angle) * reach, 0.16f, Mathf.Sin(angle) * reach);
                    spikeObject.transform.localRotation = Quaternion.Euler(k == 0 ? 0f : 18f, -angle * Mathf.Rad2Deg + 90f, 0f);
                    spikeObject.transform.localScale = new Vector3(0.09f, 0.42f, 0.09f);
                    spikeObject.AddComponent<MeshFilter>().sharedMesh = spike;
                    MeshRenderer spikeRenderer = spikeObject.AddComponent<MeshRenderer>();
                    spikeRenderer.sharedMaterial = spikeMaterial;
                    spikeRenderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }

        /// <summary>Fire walls keep burning and poison pools keep fuming for as long as Core keeps them alive.</summary>
        private void PresentM9Areas(float delta)
        {
            for (int i = 0; i < wallViews.Length; i++)
            {
                M9View view = wallViews[i];
                if (!view.Active)
                {
                    continue;
                }
                Vector3 scale = view.Root.localScale;
                view.Emit += delta * Mathf.Clamp(scale.x * 7f, 8f, 60f);
                while (view.Emit >= 1f)
                {
                    view.Emit -= 1f;
                    Vector3 at = view.Root.TransformPoint(new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.35f, 0.35f)));
                    effects.Flame(at, WallColor, Random.Range(0.9f, 1.5f));
                    if (Random.value < 0.12f)
                    {
                        effects.Sparks(at + Vector3.up * 0.8f, Vector3.up, EmberColor, 1, 3f, 0.9f);
                    }
                    if (Random.value < 0.05f)
                    {
                        effects.Smoke(at + Vector3.up * 1.4f, new Color(0.12f, 0.11f, 0.11f, 0.4f), 1, 1.1f, 1.1f);
                    }
                }
            }

            for (int i = 0; i < zoneViews.Length; i++)
            {
                M9View view = zoneViews[i];
                if (!view.Active)
                {
                    continue;
                }
                float size = view.Root.localScale.x;
                view.Emit += delta * Mathf.Clamp(size * 1.6f, 2f, 9f);
                while (view.Emit >= 1f)
                {
                    view.Emit -= 1f;
                    Vector2 spot = Random.insideUnitCircle * (size * 0.3f);
                    Vector3 at = view.Root.position + new Vector3(spot.x, 0.1f, spot.y);
                    effects.Smoke(at, PoisonFumeColor, 1, Mathf.Max(0.7f, size * 0.3f), 1.1f);
                    effects.Sparkle(at, PoisonColor, 1, 0.1f, 0.9f, 0.16f);
                }
            }
        }

        /// <summary>
        /// -fxDemo: plays every new effect once on a grid around <paramref name="center"/> so one screenshot shows them all.
        /// </summary>
        public void PlayFxDemo(Vector3 center)
        {
            const float step = 5f;
            Vector3 At(int column, int row) => center + new Vector3((column - 1.5f) * step, 0f, (0.5f - row) * step);

            effects.Explosion(At(0, 0), FireballColor, 2f);
            for (int i = 0; i < 40; i++)
            {
                effects.Flame(At(1, 0) + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-0.3f, 0.3f)), WallColor, Random.Range(0.9f, 1.5f));
            }
            effects.Shards(At(2, 0), FrostColor, 14, 3f, 0.6f);
            effects.Crystals(At(2, 0), FrostColor, 8, 1.6f, 1.5f, 2f);
            effects.Decal(At(2, 0), new Color(0.7f, 0.9f, 1f, 0.5f), 4.5f, 3f);
            effects.Bolt(At(3, 0) + Vector3.up * 7f, At(3, 0), LightningColor, 1.1f);
            effects.Bolt(At(3, 0) + new Vector3(-2f, 0.8f, 0f), At(3, 0) + new Vector3(2f, 0.8f, -1f), LightningColor, 0.8f);
            effects.Rune(At(0, 1), new Color(1f, 0.95f, 0.65f), 4.5f, 1.2f);
            effects.Twirl(At(1, 1) + Vector3.up * 0.7f, WhirlColor, 4f, 0.6f);
            for (int i = 0; i < 6; i++)
            {
                Vector2 spot = Random.insideUnitCircle * 1.2f;
                effects.Smoke(At(2, 1) + new Vector3(spot.x, 0.1f, spot.y), PoisonFumeColor, 1, 1f, 1.1f);
            }
            effects.Shards(At(3, 1), SpikeColor, 8, 2.4f, 0.45f);
            effects.Decal(At(3, 1), new Color(0.05f, 0.04f, 0.03f, 0.75f), 4f, 3f);
        }
    }
}
