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
        private static readonly Color PurgeColor = new Color(1f, 0.95f, 0.65f);
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
        private void Blast(Vector3 point, Color color, float radius, StoreFx store = StoreFx.Explode)
        {
            if (Time.frameCount != blastFrame)
            {
                blastFrame = Time.frameCount;
                blastsThisFrame = 0;
            }
            MarkElement(point, radius, ElementFire);
            if (blastsThisFrame++ < BlastsPerFrame)
            {
                if (!StoreArea(store, point, radius, 1.6f))
                {
                    effects.Rune(point, color, radius * 2.3f, 0.6f);
                    effects.Explosion(point, color, radius);
                }
            }
            else
            {
                effects.Rune(point, color, radius * 2.3f, 0.6f);
                effects.Flash(point + Vector3.up * 0.7f, color, radius * 1.8f, 0.22f);
                effects.AreaFill(point, color, radius, 0.6f);
            }
        }

        private const int ElementNone = 0;
        private const int ElementFire = 1;
        private const int ElementIce = 2;
        private const int ElementPoison = 3;
        private const int ElementLightning = 4;
        private const float ElementMarkSeconds = 0.3f;

        private struct ElementMark
        {
            public Vector3 Point;
            public float RadiusSqr;
            public float Until;
            public int Element;
        }

        private struct PendingHit
        {
            public Vector3 Point;
            public float Height;
            public bool Kill;
            public bool Chilled;
        }

        private readonly ElementMark[] elementMarks = new ElementMark[16];
        private readonly PendingHit[] pendingHits = new PendingHit[24];
        private int nextElementMark;
        private int pendingHitCount;
        private Transform barrierBubble;
        private Transform barrierBubbleRing;
        private Transform barrierBubbleFill;
        private Material barrierBubbleRingMaterial;
        private Material barrierBubbleFillMaterial;

        /// <summary>Remembers that a fire, ice or lightning effect just covered this spot, so hits and deaths there match it.</summary>
        private void MarkElement(Vector3 point, float radius, int element)
        {
            float reach = radius + 0.6f;
            elementMarks[nextElementMark] = new ElementMark
            {
                Point = point, RadiusSqr = reach * reach, Until = Time.unscaledTime + ElementMarkSeconds, Element = element
            };
            nextElementMark = (nextElementMark + 1) % elementMarks.Length;
        }

        /// <summary>Hit and death effects wait one frame, so the blast of the same tick is known whatever the event order.</summary>
        private void QueueElementFx(Vector3 point, float height, bool kill, bool chilled)
        {
            if (pendingHitCount >= (kill ? pendingHits.Length : pendingHits.Length / 2))
            {
                return;
            }
            pendingHits[pendingHitCount++] = new PendingHit { Point = point, Height = height, Kill = kill, Chilled = chilled };
        }

        private int ElementAt(Vector3 point, bool chilled)
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < elementMarks.Length; i++)
            {
                ElementMark mark = elementMarks[i];
                if (mark.Until < now)
                {
                    continue;
                }
                Vector3 offset = point - mark.Point;
                offset.y = 0f;
                if (offset.sqrMagnitude <= mark.RadiusSqr)
                {
                    return mark.Element;
                }
            }
            for (int i = 0; i < wallViews.Length; i++)
            {
                if (!wallViews[i].Active)
                {
                    continue;
                }
                Vector3 local = wallViews[i].Root.InverseTransformPoint(point);
                if (Mathf.Abs(local.x) <= 0.6f && Mathf.Abs(local.z) <= 0.8f)
                {
                    return ElementFire;
                }
            }
            for (int i = 0; i < zoneViews.Length; i++)
            {
                if (!zoneViews[i].Active)
                {
                    continue;
                }
                Vector3 offset = point - zoneViews[i].Root.position;
                float reach = zoneViews[i].Root.localScale.x / 2.5f + 0.5f;
                if (offset.x * offset.x + offset.z * offset.z <= reach * reach)
                {
                    return ElementPoison;
                }
            }
            return chilled ? ElementIce : ElementNone;
        }

        private void ResolveElementFx()
        {
            for (int i = 0; i < pendingHitCount; i++)
            {
                PendingHit hit = pendingHits[i];
                Vector3 body = hit.Point + Vector3.up * (hit.Height * 0.5f);
                switch (ElementAt(hit.Point, hit.Chilled))
                {
                    case ElementFire:
                        effects.Flame(hit.Point, WallColor, hit.Kill ? 1.1f : 0.6f, hit.Kill ? 0.6f : 0.35f);
                        if (hit.Kill)
                        {
                            effects.Flame(hit.Point, WallColor, 0.8f, 0.45f);
                            effects.Smoke(body, new Color(0.12f, 0.11f, 0.11f, 0.45f), 1, 0.9f, 0.9f);
                        }
                        break;
                    case ElementIce:
                        effects.Shards(hit.Point, FrostColor, hit.Kill ? 6 : 2, hit.Kill ? 3f : 2f, hit.Kill ? 0.4f : 0.28f);
                        break;
                    case ElementPoison:
                        effects.Sparkle(body, PoisonColor, hit.Kill ? 6 : 2, 0.3f, 1.2f, 0.18f);
                        if (hit.Kill)
                        {
                            effects.Smoke(body, PoisonFumeColor, 2, 0.9f, 0.9f);
                        }
                        break;
                    case ElementLightning:
                        effects.Sparks(body, Vector3.up, LightningColor, hit.Kill ? 8 : 3, 6f, 0.8f);
                        if (hit.Kill)
                        {
                            effects.Flash(body, LightningColor, 1.6f, 0.15f);
                        }
                        break;
                    default:
                        if (hit.Kill)
                        {
                            effects.Smoke(body, new Color(0.25f, 0.22f, 0.26f, 0.35f), 1, 0.8f, 0.7f);
                        }
                        break;
                }
            }
            pendingHitCount = 0;
        }

        /// <summary>A bubble around the hero while the barrier weapon still has charges.</summary>
        private void BuildBarrierBubble()
        {
            barrierBubble = CreateChild("Barrier Bubble", heroRoot);
            barrierBubble.localPosition = new Vector3(0f, 0.95f, 0f);
            barrierBubbleRingMaterial = Own(FxAssets.Create("Barrier Bubble Ring", FxAssets.Ring, true));
            barrierBubbleFillMaterial = Own(FxAssets.Create("Barrier Bubble Fill", FxAssets.RadialGlow, true));
            barrierBubbleFill = CreateFlatQuad("Fill", barrierBubble, barrierBubbleFillMaterial);
            barrierBubbleRing = CreateFlatQuad("Ring", barrierBubble, barrierBubbleRingMaterial);
            barrierBubble.gameObject.SetActive(false);
        }

        private void PresentBarrierBubble(int charges, bool show)
        {
            // With the store packs the defence aura's shield bubble already shows the barrier.
            if (buffAuras[(int)BuffAura.Defense] != null && buffAuras[(int)BuffAura.Defense].Store != null)
            {
                show = false;
            }
            if (barrierBubble.gameObject.activeSelf != show)
            {
                barrierBubble.gameObject.SetActive(show);
            }
            if (!show)
            {
                return;
            }
            Camera camera = ResolveCamera();
            Vector3 toCamera = camera != null ? -camera.transform.forward : Vector3.up;
            barrierBubble.rotation = Quaternion.FromToRotation(Vector3.up, toCamera);
            float pulse = 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 5f);
            float radius = (1.2f + 0.07f * charges) * pulse;
            barrierBubbleRing.localScale = new Vector3(radius * RingQuadPerRadius, 1f, radius * RingQuadPerRadius);
            barrierBubbleFill.localScale = new Vector3(radius * 2.3f, 1f, radius * 2.3f);
            barrierBubbleRingMaterial.color = new Color(BarrierColor.r, BarrierColor.g, BarrierColor.b, 0.6f);
            barrierBubbleFillMaterial.color = new Color(BarrierColor.r, BarrierColor.g, BarrierColor.b, 0.16f);
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
            ResolveElementFx();
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
                // The evolved pool boils in its own acid colour, twice as busy.
                int source = sim.Zones[i].SourceIndex;
                bool evolved = source >= 0 && SurvivorViewLogic.IsEvolution(source);
                Color acid = evolved ? SurvivorEvolutionStyles.Of(source).Color : PoisonColor;
                Color fume = evolved ? new Color(acid.r * 0.7f, acid.g * 0.7f, acid.b * 0.7f, 0.36f) : PoisonFumeColor;
                view.Emit += delta * Mathf.Clamp(size * 1.6f, 2f, 9f) * (evolved ? 2f : 1f);
                while (view.Emit >= 1f)
                {
                    view.Emit -= 1f;
                    Vector2 spot = Random.insideUnitCircle * (size * 0.3f);
                    Vector3 at = view.Root.position + new Vector3(spot.x, 0.1f, spot.y);
                    effects.Smoke(at, fume, 1, Mathf.Max(0.7f, size * 0.3f), 1.1f);
                    effects.Sparkle(at, acid, evolved ? 2 : 1, 0.1f, evolved ? 1.8f : 0.9f, evolved ? 0.24f : 0.16f);
                }
            }
        }

        /// <summary>
        /// -fxDemo: plays every new effect once on a grid around <paramref name="center"/> so one screenshot shows them all.
        /// </summary>
        public void PlayStoreGallery(Vector3 center, int page)
        {
            effects.PlayStoreGallery(center, page);
        }

        public void PlayFxDemo(Vector3 center)
        {
            const float step = 5f;
            Vector3 At(int column, int row) => center + new Vector3((column - 1.5f) * step, 0f, (0.5f - row) * step);

            Blast(At(0, 0), FireballColor, 2f);
            StoreArea(StoreFx.FreezeCircle, At(1, 0), 2.2f, 1.6f);
            StoreArea(StoreFx.LightningBall, At(3, 0) + Vector3.up * 0.5f, 1.5f, 0.7f);
            StoreArea(StoreFx.RedBlast, At(1, 1), 2.2f, 1.6f);
            StoreArea(StoreFx.RainbowExplode, At(0, 1) + Vector3.up * 0.6f, 2f, 2f);
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
            OnMagnetPicked(center + new Vector3(0f, 0f, 9f));
            PlayM11Demo(center);
        }
    }
}
