using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PersonalArena.View.Editor
{
    /// <summary>Creates or refreshes the ArenaArtSet asset that maps the KayKit packs onto the arena renderer.</summary>
    public static class KayKitArtSetBuilder
    {
        public const string AssetPath = "Assets/Arena/View/Art/KayKitArtSet.asset";
        private const string Root = "Assets/ThirdParty/KayKit/";
        private const string Adventurers = Root + "Adventurers/";
        private const string Skeletons = Root + "Skeletons/";
        private const string Animations = Root + "Animations/Rig_Medium_";
        private const string Dungeon = Root + "Dungeon/";

        [MenuItem("Personal Arena/Rebuild KayKit Art Set")]
        public static void BuildFromMenu()
        {
            Selection.activeObject = EnsureArtSet();
        }

        public static ArenaArtSet EnsureArtSet()
        {
            ArenaArtSet art = AssetDatabase.LoadAssetAtPath<ArenaArtSet>(AssetPath);
            if (art == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                AssetDatabase.Refresh();
                art = ScriptableObject.CreateInstance<ArenaArtSet>();
                AssetDatabase.CreateAsset(art, AssetPath);
            }

            art.CharacterScale = 0.78f;
            art.DungeonScale = 0.5f;
            art.Hero = Model(Adventurers + "Characters/Knight.fbx");
            art.HeroMainHand = Model(Adventurers + "Weapons/sword_1handed.fbx");
            art.HeroOffHand = Model(Adventurers + "Weapons/shield_round.fbx");
            art.Heroes = new[]
            {
                Hero("warrior", "Knight", "sword_1handed", "shield_round", 1f),
                Hero("mage", "Mage", "staff", null, 1f),
                // KayKit rangers hold the bow in the left hand.
                Hero("archer", "Ranger", null, "bow", 1f)
            };
            // Indexed by zombie type: walker, runner, brute, spitter.
            art.Walkers = new[]
            {
                Walker("Skeleton_Minion", "Skeleton_Blade", null, 1f, false),
                Walker("Skeleton_Rogue", "Skeleton_Blade", null, 0.9f, true),
                Walker("Skeleton_Warrior", "Skeleton_Axe", "Skeleton_Shield_Small_A", 1.4f, false),
                Walker("Skeleton_Mage", "Skeleton_Staff", null, 1f, false)
            };

            art.HeroIdle = Clip("General", "Idle_A");
            art.HeroRun = Clip("MovementBasic", "Running_A");
            art.HeroBlock = Clip("CombatMelee", "Melee_Blocking");
            art.HeroStrikeA = Clip("CombatMelee", "Melee_1H_Attack_Slice_Diagonal");
            art.HeroStrikeB = Clip("CombatMelee", "Melee_1H_Attack_Slice_Horizontal");
            art.HeroKick = Clip("CombatMelee", "Melee_Unarmed_Attack_Kick");
            art.HeroDash = Clip("MovementAdvanced", "Dodge_Forward");
            art.HeroHit = Clip("General", "Hit_A");
            art.HeroDeath = Clip("General", "Death_A");
            art.HeroThrow = Clip("General", "Throw");
            art.HeroCast = Clip("General", "Use_Item");
            art.HeroDodgeBack = Clip("MovementAdvanced", "Dodge_Backward");

            art.WalkerIdle = Clip("Special", "Skeletons_Idle");
            art.WalkerWalk = Clip("Special", "Skeletons_Walking");
            art.WalkerAttack = Clip("CombatMelee", "Melee_1H_Attack_Chop");
            art.WalkerHit = Clip("General", "Hit_B");
            art.WalkerDeath = Clip("Special", "Skeletons_Death");
            art.WalkerSpawn = Clip("General", "Spawn_Ground");
            art.WalkerRun = Clip("MovementBasic", "Running_B");
            art.WalkerThrow = Clip("General", "Throw");

            art.FloorTile = Model(Dungeon + "floor_tile_large.fbx");
            art.OuterFloorTile = Model(Dungeon + "floor_dirt_large.fbx");
            art.Wall = Model(Dungeon + "wall.fbx");
            art.WallVariants = Models("wall_cracked", "wall_pillar", "wall_arched");
            art.WallDoorway = Model(Dungeon + "wall_doorway.fbx");
            art.LowBarrier = Model(Dungeon + "barrier.fbx");
            art.Pillar = Model(Dungeon + "pillar.fbx");
            art.Torch = Model(Dungeon + "torch_mounted.fbx");
            art.Banners = Models("banner_red", "banner_patternA_red", "banner_shield_red", "banner_blue");
            art.Props = Models("barrel_large", "barrel_small_stack", "crates_stacked", "box_stacked", "keg",
                "chest_gold", "rubble_half", "column", "sword_shield_broken", "floor_tile_large_rocks");

            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            return art;
        }

        private static ArenaArtSet.HeroLook Hero(string classId, string body, string mainHand, string offHand, float scale)
        {
            return new ArenaArtSet.HeroLook
            {
                ClassId = classId,
                Body = Model(Adventurers + "Characters/" + body + ".fbx"),
                MainHand = mainHand != null ? Model(Adventurers + "Weapons/" + mainHand + ".fbx") : null,
                OffHand = offHand != null ? Model(Adventurers + "Weapons/" + offHand + ".fbx") : null,
                Scale = scale
            };
        }

        private static ArenaArtSet.WalkerLook Walker(string body, string mainHand, string offHand, float scale, bool runs)
        {
            return new ArenaArtSet.WalkerLook
            {
                Body = Model(Skeletons + "Characters/" + body + ".fbx"),
                MainHand = mainHand != null ? Model(Skeletons + "Weapons/" + mainHand + ".fbx") : null,
                OffHand = offHand != null ? Model(Skeletons + "Weapons/" + offHand + ".fbx") : null,
                Scale = scale,
                Runs = runs
            };
        }

        private static GameObject[] Models(params string[] dungeonNames)
        {
            List<GameObject> models = new List<GameObject>(dungeonNames.Length);
            foreach (string name in dungeonNames)
            {
                models.Add(Model(Dungeon + name + ".fbx"));
            }
            return models.ToArray();
        }

        private static GameObject Model(string path)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
            {
                throw new InvalidOperationException("Missing KayKit model: " + path);
            }
            return model;
        }

        private static AnimationClip Clip(string pack, string clipName)
        {
            string path = Animations + pack + ".fbx";
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && clip.name == clipName)
                {
                    return clip;
                }
            }
            throw new InvalidOperationException($"Missing KayKit clip '{clipName}' in {path}");
        }
    }
}
