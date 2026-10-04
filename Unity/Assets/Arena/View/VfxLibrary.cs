using System.Collections.Generic;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// Effect textures from the Kenney Particle Pack (CC0), loaded by name from
    /// <c>ThirdParty/KenneyParticles/Resources/Vfx</c>, and one shared material per texture and blend mode.
    /// A missing texture falls back to the built-in soft glow, so an effect is never invisible or pink.
    /// </summary>
    public static class VfxLibrary
    {
        public const string ResourceFolder = "Vfx/";

        /// <summary>Every texture the effects use; a test checks that each one loads.</summary>
        public static readonly string[] TextureNames =
        {
            "muzzle_02", "fire_01", "smoke_07", "dirt_02", "scorch_03", "light_01", "twirl_01", "spark_07",
            "circle_05", "magic_03", "star_09",
            "magic_01", "magic_02", "circle_01", "circle_03", "light_03", "twirl_02", "twirl_03", "star_06"
        };

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        public static Texture2D Texture(string name)
        {
            Texture2D texture = Resources.Load<Texture2D>(ResourceFolder + name);
            return texture != null ? texture : FxAssets.RadialGlow;
        }

        public static bool Has(string name)
        {
            return Resources.Load<Texture2D>(ResourceFolder + name) != null;
        }

        /// <summary>Glowing (additive) material: fire, light, magic, sparks.</summary>
        public static Material Additive(string name)
        {
            return Shared(name, true);
        }

        /// <summary>Alpha-blended material: smoke, dirt, dark marks.</summary>
        public static Material Blended(string name)
        {
            return Shared(name, false);
        }

        private static Material Shared(string name, bool additive)
        {
            string key = (additive ? "+" : "=") + name;
            if (!Materials.TryGetValue(key, out Material material) || material == null)
            {
                material = FxAssets.Create("Vfx " + key, Texture(name), additive);
                material.hideFlags = HideFlags.HideAndDontSave;
                Materials[key] = material;
            }
            return material;
        }
    }
}
