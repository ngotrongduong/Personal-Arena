using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PersonalArena.View.Editor
{
    /// <summary>Creates or refreshes the SurvivorArtSet asset (graveyard scenery from KayKit Halloween Bits and Dungeon, CC0).</summary>
    public static class SurvivorArtSetBuilder
    {
        public const string AssetPath = "Assets/Arena/View/Art/SurvivorArtSet.asset";
        private const string Root = "Assets/ThirdParty/KayKit/";
        private const string Halloween = Root + "Halloween/";
        private const string Dungeon = Root + "Dungeon/";
        private const string Skeletons = Root + "Skeletons/";

        [MenuItem("Personal Arena/Rebuild Survivor Art Set")]
        public static void BuildFromMenu()
        {
            Selection.activeObject = EnsureArtSet();
        }

        public static SurvivorArtSet EnsureArtSet()
        {
            SurvivorArtSet art = AssetDatabase.LoadAssetAtPath<SurvivorArtSet>(AssetPath);
            if (art == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                AssetDatabase.Refresh();
                art = ScriptableObject.CreateInstance<SurvivorArtSet>();
                AssetDatabase.CreateAsset(art, AssetPath);
            }

            art.Graves = HalloweenModels("grave_A", "grave_B", "gravestone", "gravemarker_A", "gravemarker_B", "grave_A_destroyed");
            art.Trees = HalloweenModels("tree_dead_large", "tree_dead_medium", "tree_dead_large_decorated", "tree_dead_small");
            art.Rocks = new[] { Model(Dungeon + "rubble_large.fbx"), Model(Dungeon + "rubble_half.fbx") };

            art.Fence = HalloweenModel("fence");
            art.FencePillar = HalloweenModel("fence_pillar");
            art.FenceBroken = HalloweenModel("fence_broken");
            art.OuterTrees = HalloweenModels("tree_pine_orange_large", "tree_pine_yellow_large", "tree_pine_orange_medium",
                "tree_pine_yellow_medium", "tree_dead_large", "tree_dead_medium");
            art.Decor = HalloweenModels("crypt", "shrine_candles", "lantern_standing", "pumpkin_orange_jackolantern",
                "pumpkin_yellow_jackolantern", "coffin_decorated", "post_skull", "post_lantern", "bench_decorated");
            art.SmallDecor = HalloweenModels("bone_A", "bone_B", "bone_C", "skull", "skull_candle", "candle_triple",
                "candle_melted", "pumpkin_orange_small", "pumpkin_yellow_small", "ribcage");
            art.GroundPatches = HalloweenModels("floor_dirt", "floor_dirt_small", "floor_dirt_grave");
            art.HalloweenTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Halloween + "halloweenbits_texture.png");
            if (art.HalloweenTexture == null)
            {
                throw new InvalidOperationException("Missing KayKit Halloween texture.");
            }

            art.Chest = Model(Dungeon + "chest_gold.fbx");
            art.HammerProp = null; // No hammer model in the packs; the renderer builds one procedurally.
            art.AxeProp = Model(Root + "Adventurers/Weapons/axe_1handed.fbx");

            art.BossBody = Model(Skeletons + "Characters/Skeleton_Warrior.fbx");
            art.BossMainHand = Model(Skeletons + "Weapons/Skeleton_Axe.fbx");
            art.BossOffHand = Model(Skeletons + "Weapons/Skeleton_Shield_Small_A.fbx");
            art.BossScale = 2.6f;

            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            return art;
        }

        private static GameObject[] HalloweenModels(params string[] names)
        {
            List<GameObject> models = new List<GameObject>(names.Length);
            foreach (string name in names)
            {
                models.Add(HalloweenModel(name));
            }
            return models.ToArray();
        }

        private static GameObject HalloweenModel(string name)
        {
            return Model(Halloween + name + ".obj");
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
    }
}
