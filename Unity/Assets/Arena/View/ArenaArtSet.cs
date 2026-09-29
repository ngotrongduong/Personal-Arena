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

        [Header("Hero")]
        public GameObject Hero;
        public GameObject HeroMainHand;
        public GameObject HeroOffHand;

        [Header("Walkers (picked by zombie index)")]
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

        [Header("Walker clips")]
        public AnimationClip WalkerIdle;
        public AnimationClip WalkerWalk;
        public AnimationClip WalkerAttack;
        public AnimationClip WalkerHit;
        public AnimationClip WalkerDeath;
        public AnimationClip WalkerSpawn;

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

        [Serializable]
        public struct WalkerLook
        {
            public GameObject Body;
            public GameObject MainHand;
            public GameObject OffHand;
        }
    }
}
