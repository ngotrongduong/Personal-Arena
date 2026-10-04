using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// Effect prefabs from Unity Asset Store packs that live only on the build machine (their licence does not
    /// allow putting them in the public repository). Built by the editor's StoreVfxBuilder into a Resources
    /// asset outside version control; when it is missing every effect falls back to the built-in ones.
    /// </summary>
    public sealed class StoreVfxSet : ScriptableObject
    {
        public const string ResourceName = "StoreVfxSet";
        public string[] Names = new string[0];
        public GameObject[] Prefabs = new GameObject[0];
    }
}
