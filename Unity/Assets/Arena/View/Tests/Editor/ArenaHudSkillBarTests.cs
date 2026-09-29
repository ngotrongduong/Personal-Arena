using NUnit.Framework;
using PersonalArena.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PersonalArena.View.Tests
{
    public sealed class ArenaHudSkillBarTests
    {
        [Test]
        public void ReadySkills_AreNotCoveredByTheCooldownShade()
        {
            GameObject root = new GameObject("HUD Test");
            try
            {
                ArenaHud hud = root.AddComponent<ArenaHud>();
                ArenaSim sim = new ArenaSim(DefaultDefs.Warrior(), new ArenaConfig { ZombieCount = 0, Seed = 1 });
                hud.Bind(sim, null);

                int shades = 0;
                foreach (Image image in root.GetComponentsInChildren<Image>(true))
                {
                    if (image.name != "Cooldown")
                    {
                        continue;
                    }

                    shades++;
                    // Without a sprite a Filled Image ignores fillAmount and darkens the whole icon.
                    Assert.That(image.sprite, Is.Not.Null, "the cooldown shade needs a sprite to be fillable");
                    Assert.That(image.type, Is.EqualTo(Image.Type.Filled));
                    Assert.That(image.fillAmount, Is.EqualTo(0f), "a ready skill shows no shade");
                }
                Assert.That(shades, Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
