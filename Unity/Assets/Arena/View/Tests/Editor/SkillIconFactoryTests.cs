using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core;
using UnityEngine;

namespace PersonalArena.View.Tests
{
    public sealed class SkillIconFactoryTests
    {
        [Test]
        public void EverySkillOfEveryClass_GetsItsOwnIcon()
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (HeroClassDef hero in new[] { DefaultDefs.Warrior(), DefaultDefs.Mage(), DefaultDefs.Archer() })
            {
                for (int i = 0; i < hero.Skills.Length; i++)
                {
                    SkillDef skill = hero.Skills[i];
                    // Every skill ships a readable game-icons.net glyph (see SkillIconImporter).
                    Texture2D glyph = Resources.Load<Texture2D>(SkillIconFactory.GlyphFolder + skill.Id);
                    Assert.That(glyph, Is.Not.Null, skill.Id + " glyph image");
                    Assert.That(glyph.isReadable, Is.True, skill.Id + " glyph must be readable");

                    Sprite icon = SkillIconFactory.IconFor(skill, i);
                    Assert.That(icon, Is.Not.Null, skill.Id);
                    Assert.That(icon.texture.width, Is.EqualTo(SkillIconFactory.Size));
                    Assert.That(SkillIconFactory.IconFor(skill, i), Is.SameAs(icon), "icons are cached");

                    // The glyph must draw white-ish pixels, and each skill must look different.
                    Color32[] pixels = SkillIconFactory.DrawPixels(skill.Id, SkillIconFactory.ColorFor(skill, i));
                    int bright = 0;
                    System.Text.StringBuilder signature = new System.Text.StringBuilder();
                    for (int p = 0; p < pixels.Length; p++)
                    {
                        bool isBright = pixels[p].r > 220 && pixels[p].g > 200 && pixels[p].a > 200;
                        if (isBright)
                        {
                            bright++;
                        }
                        if (p % 97 == 0)
                        {
                            signature.Append(isBright ? '1' : '0');
                        }
                    }
                    Assert.That(bright, Is.GreaterThan(400), skill.Id + " glyph is visible");
                    Assert.That(seen.Add(signature.ToString()), Is.True, skill.Id + " icon is distinct");
                }
            }
        }

        [Test]
        public void EmptySlot_FallsBackToTheWarriorIcon()
        {
            Assert.That(SkillIconFactory.KeyFor(null, 2), Is.EqualTo("shield-block"));
            Assert.That(SkillIconFactory.IconFor(null, 0), Is.Not.Null);
        }
    }
}
