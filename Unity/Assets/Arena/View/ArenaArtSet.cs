using System;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// Optional art pack for <see cref="ArenaRenderer"/>. When assigned, the renderer swaps its primitive
    /// placeholders for rigged characters, animation clips and modular dungeon pieces.
    /// </summary>
    [CreateAssetMenu(menuName = "Personal Arena/Arena Art Set")]
    public sealed class ArenaArtSet : ScriptableObject
    {
        [Header("Scale")]
        public float CharacterScale = 0.62f;
        public float DungeonScale = 0.5f;

        [Header("Hero (fallback look when a class has no entry in Heroes)")]
        public GameObject Hero;
        public GameObject HeroMainHand;
        public GameObject HeroOffHand;

        [Header("Hero looks by class id (warrior, mage, archer)")]
        public HeroLook[] Heroes = Array.Empty<HeroLook>();

        [Header("Zombie looks by ZombieTypeDef.TypeIndex (walker, runner, brute, spitter)")]
        public WalkerLook[] Walkers = Array.Empty<WalkerLook>();

        [Header("Hero clips")]
        public AnimationClip HeroIdle;
        public AnimationClip HeroRun;
        public AnimationClip HeroBlock;
        public AnimationClip HeroStrikeA;
        public AnimationClip HeroStrikeB;
        public AnimationClip HeroKick;
        public AnimationClip HeroDash;
        public AnimationClip HeroHit;
        public AnimationClip HeroDeath;
        public AnimationClip HeroThrow;
        public AnimationClip HeroCast;
        public AnimationClip HeroDodgeBack;

        [Header("Walker clips")]
        public AnimationClip WalkerIdle;
        public AnimationClip WalkerWalk;
        public AnimationClip WalkerAttack;
        public AnimationClip WalkerHit;
        public AnimationClip WalkerDeath;
        public AnimationClip WalkerSpawn;
        public AnimationClip WalkerRun;
        public AnimationClip WalkerThrow;

        [Header("Dungeon")]
        public GameObject FloorTile;
        public GameObject OuterFloorTile;
        public GameObject Wall;
        public GameObject[] WallVariants = Array.Empty<GameObject>();
        public GameObject WallDoorway;
        public GameObject LowBarrier;
        public GameObject Pillar;
        public GameObject Torch;
        public GameObject[] Banners = Array.Empty<GameObject>();
        public GameObject[] Props = Array.Empty<GameObject>();

        public bool HasCharacters => Hero != null && HeroIdle != null;

        /// <summary>Look for a hero class; falls back to the Hero fields when the class has no entry.</summary>
        public HeroLook HeroFor(string classId)
        {
            for (int i = 0; i < Heroes.Length; i++)
            {
                if (Heroes[i].Body != null && string.Equals(Heroes[i].ClassId, classId, StringComparison.OrdinalIgnoreCase))
                {
                    return Heroes[i];
                }
            }

            return new HeroLook { ClassId = classId, Body = Hero, MainHand = HeroMainHand, OffHand = HeroOffHand, Scale = 1f };
        }

        /// <summary>Look for a zombie type; unknown types wrap around the list.</summary>
        public WalkerLook WalkerFor(int typeIndex)
        {
            if (Walkers.Length == 0)
            {
                return default;
            }

            int index = typeIndex < 0 ? 0 : typeIndex % Walkers.Length;
            return Walkers[index];
        }

        [Serializable]
        public struct HeroLook
        {
            public string ClassId;
            public GameObject Body;
            public GameObject MainHand;
            public GameObject OffHand;
            public float Scale;
        }

        [Serializable]
        public struct WalkerLook
        {
            public GameObject Body;
            public GameObject MainHand;
            public GameObject OffHand;
            [Tooltip("Body scale relative to CharacterScale; 0 means 1.")]
            public float Scale;
            [Tooltip("Use the running cycle instead of the shambling walk.")]
            public bool Runs;
        }
    }
}
