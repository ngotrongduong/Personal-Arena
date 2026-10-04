using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// M12: which store effect (see <see cref="StoreFx"/>) plays for which moment. Every helper reports whether the
    /// effect played, so each call site keeps its built-in effect for machines without the packs.
    /// </summary>
    public sealed partial class SurvivorRenderer
    {
        /// <summary>Visual radius in metres of a store effect at scale 1, measured on the -vfxGallery shots.</summary>
        private static float StoreRadius(StoreFx slot)
        {
            switch (slot)
            {
                case StoreFx.GroundBlast: return 3.2f;
                case StoreFx.SmokeBlast: return 3.2f;
                case StoreFx.RedBlast: return 3.5f;
                case StoreFx.SnowArea: return 4.6f;
                case StoreFx.FreezeCircle: return 4.0f;
                case StoreFx.ElectroHit: return 0.7f;
                case StoreFx.HolyHit: return 0.7f;
                case StoreFx.StarHit: return 0.7f;
                case StoreFx.StonesHit: return 0.7f;
                case StoreFx.GreenHit: return 0.7f;
                case StoreFx.HealCircle: return 4.1f;
                case StoreFx.MagicCircle: return 1.3f;
                case StoreFx.MagicCircle2: return 1.6f;
                case StoreFx.Meteors: return 4.5f;
                case StoreFx.DustPuff: return 4.0f;
                case StoreFx.LightningBall: return 1.6f;
                case StoreFx.SwampBall: return 1.0f;
                case StoreFx.Explode: return 3.0f;
                case StoreFx.Explode5: return 2.2f;
                case StoreFx.Explode7: return 1.2f;
                case StoreFx.PoisonExplode: return 1.6f;
                case StoreFx.RainbowExplode: return 1.3f;
                case StoreFx.MagicCircleExplode: return 3.5f;
                case StoreFx.SummonCircle2: return 1.9f;
                case StoreFx.RuneOfMagic: return 1.0f;
                case StoreFx.IceCloud: return 2.5f;
                case StoreFx.Portal: return 0.7f;
                default: return 1.5f;
            }
        }

        /// <summary>A store effect sized to cover a gameplay radius.</summary>
        private bool StoreArea(StoreFx slot, Vector3 point, float radius, float lifetime = 2f)
        {
            if (!effects.Store(slot, point, radius / StoreRadius(slot), lifetime))
            {
                return false;
            }
            // The built-in rune and ground fill of the same moment would only cover it.
            effects.MarkStoreArea(point);
            return true;
        }

        private bool StoreAt(StoreFx slot, Vector3 point, float scale = 1f, float lifetime = 2f)
        {
            return effects.Store(slot, point, scale, lifetime);
        }
    }
}
