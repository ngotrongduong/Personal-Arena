using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// M12b: the built-in primitives (sparks, puffs, flashes, slashes, runes...) hand over to a store effect of the
    /// matching colour when the packs are installed. Each helper reports whether it played, so the primitive keeps
    /// its own particles as the fallback (no packs, pool busy, or too many this frame).
    /// </summary>
    public sealed partial class ArenaEffects
    {
        private enum Tone
        {
            Red,
            Orange,
            Yellow,
            Green,
            Cyan,
            Blue,
            Purple,
            Pink,
            White,
            Dark
        }

        private const int StoreSmallPerFrame = 4;
        private int storeSmallFrame = -1;
        private int storeSmallThisFrame;
        private int storeAreaFrame = -1;
        private Vector3 storeAreaPoint;
        private int storeBurstFrame = -1;
        private int storeBurstCount;
        private readonly Vector3[] storeBurstPoints = new Vector3[StoreSmallPerFrame];

        private static Tone ToneOf(Color color)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            if (value < 0.3f)
            {
                return Tone.Dark;
            }
            if (saturation < 0.22f)
            {
                return Tone.White;
            }
            float degrees = hue * 360f;
            if (degrees < 16f || degrees >= 340f)
            {
                return Tone.Red;
            }
            if (degrees < 40f)
            {
                return Tone.Orange;
            }
            if (degrees < 68f)
            {
                return Tone.Yellow;
            }
            if (degrees < 160f)
            {
                return Tone.Green;
            }
            if (degrees < 200f)
            {
                return Tone.Cyan;
            }
            if (degrees < 255f)
            {
                return Tone.Blue;
            }
            return degrees < 295f ? Tone.Purple : Tone.Pink;
        }

        /// <summary>A small, frequent store effect: only a few may start in one frame.</summary>
        private bool StoreSmall(StoreFx slot, Vector3 position, float scale, float lifetime, float yawDegrees = 0f)
        {
            if (Time.frameCount != storeSmallFrame)
            {
                storeSmallFrame = Time.frameCount;
                storeSmallThisFrame = 0;
            }
            if (storeSmallThisFrame >= StoreSmallPerFrame || !Store(slot, position, scale, lifetime, yawDegrees))
            {
                return false;
            }
            storeSmallThisFrame++;
            return true;
        }

        /// <summary>Remembers where a store ground effect was just drawn, so the same call site's rune and soft fill are not drawn on top.</summary>
        public void MarkStoreArea(Vector3 position)
        {
            storeAreaFrame = Time.frameCount;
            storeAreaPoint = position;
        }

        private bool StoreAreaDrawnAt(Vector3 position)
        {
            if (storeAreaFrame != Time.frameCount)
            {
                return false;
            }
            Vector3 offset = position - storeAreaPoint;
            return offset.x * offset.x + offset.z * offset.z < 1f;
        }

        /// <summary>
        /// A small coloured burst of <paramref name="radius"/> metres. Call sites often stack sparks, a flash and
        /// sparkles on one point; only the first of them in a frame becomes a burst, the rest are swallowed.
        /// </summary>
        private bool StoreBurst(Vector3 position, Color color, float radius)
        {
            if (!HasStore(StoreFx.Explode2))
            {
                return false;
            }
            if (storeBurstFrame != Time.frameCount)
            {
                storeBurstFrame = Time.frameCount;
                storeBurstCount = 0;
            }
            for (int i = 0; i < storeBurstCount; i++)
            {
                if ((storeBurstPoints[i] - position).sqrMagnitude < 1.2f)
                {
                    return true;
                }
            }
            StoreFx slot;
            float slotRadius;
            switch (ToneOf(color))
            {
                case Tone.Red:
                case Tone.Orange: slot = StoreFx.Explode2; slotRadius = 1f; break;
                case Tone.Yellow: slot = StoreFx.Explode3; slotRadius = 2.5f; break;
                case Tone.Green: slot = StoreFx.Explode11; slotRadius = 1.2f; break;
                case Tone.Cyan: slot = StoreFx.Explode6; slotRadius = 1.5f; break;
                case Tone.Blue:
                case Tone.Purple: slot = StoreFx.Explode4; slotRadius = 1f; break;
                case Tone.Pink: slot = StoreFx.RainbowExplode2; slotRadius = 1f; break;
                case Tone.Dark: slot = StoreFx.StonesHit; slotRadius = 0.7f; break;
                default: slot = StoreFx.Explode10; slotRadius = 1.3f; break;
            }
            if (!StoreSmall(slot, position, radius / slotRadius, 1.2f))
            {
                return false;
            }
            storeBurstPoints[storeBurstCount++] = position;
            return true;
        }

        private bool StoreSparks(Vector3 position, Color color, int count)
        {
            return count >= 4 && StoreBurst(position, color, Mathf.Clamp(0.3f + count * 0.05f, 0.45f, 1f));
        }

        private bool StoreTwinkle(Vector3 position, Color color, int count, float radius)
        {
            return count >= 5 && StoreBurst(position + Vector3.up * 0.3f, color, Mathf.Clamp(radius * 0.9f, 0.5f, 2f));
        }

        private bool StoreFlash(Vector3 position, Color color, float size)
        {
            return size >= 1f && StoreBurst(position, color, Mathf.Clamp(size * 0.3f, 0.5f, 2.5f));
        }

        private bool StoreShards(Vector3 position, Color color, int count, float size)
        {
            return count >= 5 && StoreBurst(position + Vector3.up * 0.3f, color, Mathf.Clamp(0.5f + size, 0.6f, 1.5f));
        }

        private bool StorePuff(Vector3 position, Color color, int count, float reach)
        {
            if (count < 4)
            {
                return false;
            }
            Color.RGBToHSV(color, out _, out float saturation, out _);
            if (saturation > 0.5f)
            {
                // A coloured puff (poison, magic) rather than dust or smoke.
                return StoreBurst(position, color, Mathf.Clamp(reach, 0.5f, 2f));
            }
            bool dust = color.r > color.b + 0.06f;
            return StoreSmall(dust ? StoreFx.DustPuff : StoreFx.SmokePuff, position, Mathf.Clamp(reach, 0.6f, 5f) / PuffRadius, 1.4f);
        }

        private bool StoreSlash(Vector3 position, float yawDegrees, Color color, float reach)
        {
            StoreFx slot;
            switch (ToneOf(color))
            {
                case Tone.Red:
                case Tone.Orange:
                case Tone.Yellow: slot = StoreFx.SlashRed; break;
                case Tone.Purple:
                case Tone.Pink: slot = StoreFx.SlashPurple; break;
                // The snow, stone and electro slashes are only scattered specks at this camera distance.
                default: slot = StoreFx.SlashBlue; break;
            }
            return StoreSmall(slot, position, reach / SlashReach, 0.9f, yawDegrees + SlashYaw);
        }

        private bool StoreTwirl(Vector3 position, Color color, float size)
        {
            StoreFx slot;
            switch (ToneOf(color))
            {
                case Tone.Red:
                case Tone.Orange:
                case Tone.Yellow: slot = StoreFx.TwirlOrange; break;
                case Tone.Green: slot = StoreFx.TwirlGreen; break;
                default: slot = StoreFx.TwirlBlue; break;
            }
            return StoreSmall(slot, position, size * 0.5f / TwirlRadius, 1.2f);
        }

        private bool StoreRune(Vector3 position, Color color, float radius, float lifetime)
        {
            if (!HasStore(StoreFx.MagicCircle))
            {
                return false;
            }
            if (StoreAreaDrawnAt(position))
            {
                return true;
            }
            StoreFx slot;
            float slotRadius;
            switch (ToneOf(color))
            {
                case Tone.Cyan: slot = StoreFx.FreezeCircle; slotRadius = 4f; break;
                case Tone.Green: slot = StoreFx.HealCircle; slotRadius = 4.1f; break;
                case Tone.Blue:
                case Tone.Purple:
                case Tone.Pink: slot = StoreFx.MagicCircle; slotRadius = 1.3f; break;
                default: slot = StoreFx.MagicCircle2; slotRadius = 1.6f; break;
            }
            if (!Store(slot, position, radius / slotRadius, Mathf.Max(1.2f, lifetime * AreaLinger)))
            {
                return false;
            }
            MarkStoreArea(position);
            return true;
        }

        private bool StoreFlame(Vector3 position, float size, float lifetime)
        {
            return size >= 0.6f && StoreSmall(StoreFx.FlameEmission, position, size / FlameSize, Mathf.Max(0.5f, lifetime));
        }

        private bool StoreSmoke(Vector3 position, Color color, float size, float lifetime)
        {
            if (color.g > color.r + 0.15f)
            {
                // Poison fumes.
                return StoreSmall(StoreFx.SwampBall, position, size * 0.6f, Mathf.Max(0.8f, lifetime));
            }
            return StoreSmall(StoreFx.SmokePuff, position, size / PuffRadius, Mathf.Max(0.8f, lifetime));
        }

        private bool StoreCrystals(Vector3 position, Color color, int count, float radius, float lifetime)
        {
            if (count < 8)
            {
                return false;
            }
            Tone tone = ToneOf(color);
            if (tone != Tone.Cyan && tone != Tone.Blue)
            {
                return false;
            }
            return Store(StoreFx.CrystalsCross, position, Mathf.Max(radius, 1.5f) / 4.5f, Mathf.Max(1.5f, lifetime));
        }

        // Sizes measured on the -vfxGallery shots.
        private const float PuffRadius = 4f;
        private const float SlashReach = 0.8f;
        private const float SlashYaw = 0f;
        private const float TwirlRadius = 1.3f;
        private const float FlameSize = 0.7f;
    }
}
