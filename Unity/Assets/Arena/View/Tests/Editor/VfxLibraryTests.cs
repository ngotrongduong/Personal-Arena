using NUnit.Framework;
using UnityEngine;

namespace PersonalArena.View.Tests
{
    public sealed class VfxLibraryTests
    {
        [Test]
        public void EveryEffectTexture_Loads()
        {
            foreach (string name in VfxLibrary.TextureNames)
            {
                Texture2D texture = Resources.Load<Texture2D>(VfxLibrary.ResourceFolder + name);
                Assert.That(texture, Is.Not.Null, name);
            }
        }
    }
}
