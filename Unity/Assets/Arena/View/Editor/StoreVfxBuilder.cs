using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PersonalArena.View.Editor
{
    /// <summary>
    /// Collects the chosen prefabs of the locally imported Asset Store packs (Hovl Studio "Magic Effects FREE",
    /// GAPH "52 Special Effects Pack") into the <see cref="StoreVfxSet"/> Resources asset. The packs and the asset
    /// stay outside version control; without them the viewer uses its built-in effects.
    /// </summary>
    public static class StoreVfxBuilder
    {
        private const string Folder = "Assets/ThirdPartyLocal/Resources";
        private const string AssetPath = Folder + "/" + StoreVfxSet.ResourceName + ".asset";

        private static readonly string[,] Slots =
        {
            { "FireExplosion", "Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Explosion.prefab" },
            { "GroundBlast", "Assets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Ground AOE explosion.prefab" },
            { "SmokeBlast", "Assets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Smoke AOE explosion.prefab" },
            { "RedBlast", "Assets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Red energy explosion.prefab" },
            { "SnowArea", "Assets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Snow AOE.prefab" },
            { "FreezeCircle", "Assets/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Freeze circle.prefab" },
            { "SnowHit", "Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Snow hit.prefab" },
            { "ElectroHit", "Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Electro hit.prefab" },
            { "HolyHit", "Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Holy hit.prefab" },
            { "StarHit", "Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Star hit.prefab" },
            { "StonesHit", "Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Stones hit.prefab" },
            { "GreenHit", "Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Green hit.prefab" },
            { "HealAura", "Assets/Hovl Studio/Magic effects pack/Prefabs/Character auras/Healing.prefab" },
            { "BuffAura", "Assets/Hovl Studio/Magic effects pack/Prefabs/Character auras/Buff.prefab" },
            { "LightningAura", "Assets/Hovl Studio/Magic effects pack/Prefabs/Character auras/Lightning aura.prefab" },
            { "StarAura", "Assets/Hovl Studio/Magic effects pack/Prefabs/Character auras/Star aura.prefab" },
            { "HealCircle", "Assets/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Healing circle.prefab" },
            { "MagicCircle", "Assets/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle.prefab" },
            { "MagicCircle2", "Assets/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle 2.prefab" },
            { "ShieldBlue", "Assets/Hovl Studio/Magic effects pack/Prefabs/Magic shields/Magic shield blue.prefab" },
            { "ShieldYellow", "Assets/Hovl Studio/Magic effects pack/Prefabs/Magic shields/Magic shield yellow.prefab" },
            { "Teleport", "Assets/Hovl Studio/Magic effects pack/Prefabs/Environment/Teleport.prefab" },
            { "PortalRed", "Assets/Hovl Studio/Magic effects pack/Prefabs/Portals/Portal red.prefab" },
            { "Meteors", "Assets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Meteors AOE.prefab" },
            { "CrystalsFront", "Assets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Crystals front attack.prefab" },
            { "SmokeVortex", "Assets/Hovl Studio/Magic effects pack/Prefabs/Smoke effects/Smoke vortex.prefab" },
            { "DustPuff", "Assets/Hovl Studio/Magic effects pack/Prefabs/Smoke effects/Dust puff.prefab" },
            { "SparksYellow", "Assets/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks explode yellow.prefab" },
            { "PlexusArea", "Assets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Plexus AoE.prefab" },
            { "LaserArea", "Assets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Laser AOE.prefab" },
            { "MagicBall", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/ElementalBall.prefab" },
            { "FireBall", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/FireBall.prefab" },
            { "IceBall", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/IceBall.prefab" },
            { "LightningBall", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/LightningBall.prefab" },
            { "SwampBall", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/SwampBall.prefab" },
            { "DeadBall", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/DeadBall.prefab" },
            { "BlueBall", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/BlueBall.prefab" },
            { "Explode", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/Explode.prefab" },
            { "Explode5", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/Explode5.prefab" },
            { "Explode7", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/Explode7.prefab" },
            { "PoisonExplode", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/PoisonExplode.prefab" },
            { "DeadExplode", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/DeadExplode.prefab" },
            { "RainbowExplode", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/RainbowExplode.prefab" },
            { "MagicCircleExplode", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/MagicCircleExplode.prefab" },
            { "SummonCircle", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/SummonMagicCircle.prefab" },
            { "SummonCircle2", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/SummonMagicCircle2.prefab" },
            { "RuneOfMagic", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/RuneOfMagic.prefab" },
            { "Swamp", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/Swamp.prefab" },
            { "IceCloud", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/IceCloud.prefab" },
            { "StormCloud", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/StormCloud.prefab" },
            { "Portal", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/Portal.prefab" },
            { "StarCore", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/StarCore.prefab" },
            { "Hole", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/Hole.prefab" },
            { "FlameEmission", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/FlameEmission.prefab" },
            { "Kunai", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/Kunai.prefab" },
            { "ElementalArrow", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/ElementalArrow.prefab" }
        };

        [MenuItem("Personal Arena/Rebuild Store Effect Set")]
        public static void Build()
        {
            List<string> names = new List<string>();
            List<GameObject> prefabs = new List<GameObject>();
            for (int i = 0; i < Slots.GetLength(0); i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Slots[i, 1]);
                if (prefab != null)
                {
                    names.Add(Slots[i, 0]);
                    prefabs.Add(prefab);
                }
                else if (Directory.Exists(Path.GetDirectoryName(Slots[i, 1])))
                {
                    Debug.LogWarning("Store effect set: missing " + Slots[i, 1]);
                }
            }
            if (prefabs.Count == 0)
            {
                Debug.Log("Store effect set: no pack imported, the viewer keeps its built-in effects.");
                return;
            }
            Directory.CreateDirectory(Folder);
            StoreVfxSet set = AssetDatabase.LoadAssetAtPath<StoreVfxSet>(AssetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<StoreVfxSet>();
                AssetDatabase.CreateAsset(set, AssetPath);
            }
            set.Names = names.ToArray();
            set.Prefabs = prefabs.ToArray();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log("Store effect set: " + prefabs.Count + " of " + Slots.GetLength(0) + " effects.");
        }
    }
}
