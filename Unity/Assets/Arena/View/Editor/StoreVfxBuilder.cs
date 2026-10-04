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
            { "ElementalArrow", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/ElementalArrow.prefab" },
            { "SparksBlue", "Assets/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks explode blue.prefab" },
            { "SparksGreen", "Assets/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks explode green.prefab" },
            { "SparksPink", "Assets/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks explode pink.prefab" },
            { "SparksRed", "Assets/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks explode red.prefab" },
            { "SparksWhite", "Assets/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks explode white.prefab" },
            { "TwinkleBlue", "Assets/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks flashing blue.prefab" },
            { "TwinkleGreen", "Assets/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks flashing green.prefab" },
            { "TwinklePink", "Assets/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks flashing pink.prefab" },
            { "TwinkleRed", "Assets/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks flashing red.prefab" },
            { "TwinkleWhite", "Assets/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks flashing white.prefab" },
            { "TwinkleYellow", "Assets/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks flashing yellow.prefab" },
            { "SlashBlue", "Assets/Hovl Studio/Magic effects pack/Prefabs/Slash effects/Charge slash blue.prefab" },
            { "SlashPurple", "Assets/Hovl Studio/Magic effects pack/Prefabs/Slash effects/Charge slash purple.prefab" },
            { "SlashRed", "Assets/Hovl Studio/Magic effects pack/Prefabs/Slash effects/Charge slash red.prefab" },
            { "SlashElectro", "Assets/Hovl Studio/Magic effects pack/Prefabs/Slash effects/Electro slash.prefab" },
            { "SlashSnow", "Assets/Hovl Studio/Magic effects pack/Prefabs/Slash effects/Snow slash.prefab" },
            { "SlashStone", "Assets/Hovl Studio/Magic effects pack/Prefabs/Slash effects/Stone slash.prefab" },
            { "TwirlBlue", "Assets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/AoE slash blue.prefab" },
            { "TwirlGreen", "Assets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/AoE slash green.prefab" },
            { "TwirlOrange", "Assets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/AoE slash orange.prefab" },
            { "SmokePuff", "Assets/Hovl Studio/Magic effects pack/Prefabs/Smoke effects/Smoke puff.prefab" },
            { "LoveHit", "Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Love hit.prefab" },
            { "PortalBlue", "Assets/Hovl Studio/Magic effects pack/Prefabs/Portals/Portal blue.prefab" },
            { "PortalGreen", "Assets/Hovl Studio/Magic effects pack/Prefabs/Portals/Portal green.prefab" },
            { "PortalYellow", "Assets/Hovl Studio/Magic effects pack/Prefabs/Portals/Portal yellow.prefab" },
            { "ShieldPink", "Assets/Hovl Studio/Magic effects pack/Prefabs/Magic shields/Magic shield pink.prefab" },
            { "DebuffAura", "Assets/Hovl Studio/Magic effects pack/Prefabs/Character auras/Debuff.prefab" },
            { "LoveAura", "Assets/Hovl Studio/Magic effects pack/Prefabs/Character auras/Love aura.prefab" },
            { "PlexusAura", "Assets/Hovl Studio/Magic effects pack/Prefabs/Character auras/Plexus.prefab" },
            { "CrystalsCross", "Assets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Crystals crossfade.prefab" },
            { "CrystalBlue", "Assets/Hovl Studio/Magic effects pack/Prefabs/Environment/Crystal effect blue.prefab" },
            { "CrystalGreen", "Assets/Hovl Studio/Magic effects pack/Prefabs/Environment/Crystal effect green.prefab" },
            { "CrystalRed", "Assets/Hovl Studio/Magic effects pack/Prefabs/Environment/Crystal effect red.prefab" },
            { "DustGround", "Assets/Hovl Studio/Magic effects pack/Prefabs/Smoke effects/Dust ground.prefab" },
            { "SmokeGround", "Assets/Hovl Studio/Magic effects pack/Prefabs/Smoke effects/Smoke ground.prefab" },
            { "ElementalArrow2", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/ElementalArrow2.prefab" },
            { "LightningArrow", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/LigthningArrow.prefab" },
            { "Kunai2", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/Kunai2.prefab" },
            { "Kunai3", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/Kunai3.prefab" },
            { "Kunai4", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/Kunai4.prefab" },
            { "Kunai5", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/Kunai5.prefab" },
            { "ElementalBall2", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/ElementalBall2.prefab" },
            { "ElementalBall3", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/ElementalBall3.prefab" },
            { "ElementalBall4", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/ElementalBall4.prefab" },
            { "Explode2", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/Explode2.prefab" },
            { "Explode3", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/Explode3.prefab" },
            { "Explode4", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/Explode4.prefab" },
            { "Explode6", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/Explode6.prefab" },
            { "Explode8", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/Explode8.prefab" },
            { "Explode9", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/Explode9.prefab" },
            { "Explode10", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/Explode10.prefab" },
            { "Explode11", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/Explode11.prefab" },
            { "LightningBall2", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/LightningBall2.prefab" },
            { "LightningRotateBall", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/LightningRotateBall.prefab" },
            { "MagicCircleRelease", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/MagicCircleRelease.prefab" },
            { "MagicCube", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/MagicCube.prefab" },
            { "Portal2", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/Portal2.prefab" },
            { "RainbowExplode2", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/RainbowExplode2.prefab" },
            { "SummonCircle3", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/SummonMagicCircle3.prefab" },
            { "LaserFire", "Assets/52SpecialEffectPack/Effect/Effect(Shuriken)/LaserFire.prefab" }
        };

        // The second pack is authored about five times larger than this game's units.
        private static float BaseScale(string path)
        {
            return path.Contains("52SpecialEffectPack") ? 0.22f : 1f;
        }

        [MenuItem("Personal Arena/Rebuild Store Effect Set")]
        public static void Build()
        {
            List<string> names = new List<string>();
            List<GameObject> prefabs = new List<GameObject>();
            List<float> scales = new List<float>();
            for (int i = 0; i < Slots.GetLength(0); i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Slots[i, 1]);
                if (prefab != null)
                {
                    names.Add(Slots[i, 0]);
                    prefabs.Add(prefab);
                    scales.Add(BaseScale(Slots[i, 1]));
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
            set.Scales = scales.ToArray();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log("Store effect set: " + prefabs.Count + " of " + Slots.GetLength(0) + " effects.");
        }
    }
}
