using System;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// Scenery and prop models for the survivor graveyard (KayKit Halloween Bits and Dungeon, CC0).
    /// Characters and animation clips come from <see cref="ArenaArtSet"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Personal Arena/Survivor Art Set")]
    public sealed class SurvivorArtSet : ScriptableObject
    {
        [Header("Obstacles (picked stably by obstacle index)")]
        public GameObject[] Graves = Array.Empty<GameObject>();
        public GameObject[] Trees = Array.Empty<GameObject>();
        public GameObject[] Rocks = Array.Empty<GameObject>();

        [Header("Scenery")]
        public GameObject Fence;
        public GameObject FencePillar;
        public GameObject FenceBroken;
        public GameObject[] OuterTrees = Array.Empty<GameObject>();
        [Tooltip("Large props placed outside the fence (crypt, shrine, lanterns, pumpkins).")]
        public GameObject[] Decor = Array.Empty<GameObject>();
        [Tooltip("Small props scattered inside the arena (bones, skulls, candles); purely visual.")]
        public GameObject[] SmallDecor = Array.Empty<GameObject>();
        public GameObject[] GroundPatches = Array.Empty<GameObject>();
        [Tooltip("Shared texture of the Halloween Bits models; all of them are drawn with one material.")]
        public Texture2D HalloweenTexture;

        [Header("Props")]
        public GameObject Chest;
        public GameObject HammerProp;

        [Header("Boss (bone lord)")]
        public GameObject BossBody;
        public GameObject BossMainHand;
        public GameObject BossOffHand;
        public float BossScale = 2.6f;
    }
}
