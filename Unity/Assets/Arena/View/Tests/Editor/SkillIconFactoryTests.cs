using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;

namespace PersonalArena.View.Tests
{
    public sealed class SkillIconFactoryTests
    {
        [Test]
        public void EverySkillOfEveryClass_GetsItsOwnIcon()
        {
            HashSet<string> seen = new HashSet<string>();
            Assert.That(SkillIconFactory.ClassSkillIds.Count, Is.EqualTo(3));
            foreach (IReadOnlyList<string> row in SkillIconFactory.ClassSkillIds)
            {
                Assert.That(row.Count, Is.EqualTo(4));
                for (int i = 0; i < row.Count; i++)
                {
                    SkillDef skill = new SkillDef { Id = row[i] };
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

        [TestCase("fireball")]
        [TestCase("mana-shield")]
        [TestCase("blink")]
        [TestCase("frost-burst")]
        [TestCase("power-shot")]
        [TestCase("roll-back")]
        [TestCase("kick")]
        [TestCase("shield-block")]
        [TestCase("dash")]
        public void EveryM7ClassSkill_HasAVisibleGlyph(string skillId)
        {
            Texture2D glyph = SkillIconFactory.LoadGlyph(skillId);
            Assert.That(glyph, Is.Not.Null, skillId + " needs its own glyph or an alias");
            Assert.That(glyph.isReadable, Is.True);
            Assert.That(BrightPixels(SkillIconFactory.DrawPixels(skillId, SkillIconFactory.ColorFor(new SkillDef { Id = skillId }, 0))),
                Is.GreaterThan(400), skillId + " glyph is visible");
        }

        [Test]
        public void EveryClassWeapon_HasAVisibleGlyph()
        {
            for (int item = SurvivorCatalog.MagicBoltIndex; item <= SurvivorCatalog.CrossbowIndex; item++)
            {
                string key = SkillIconFactory.ItemGlyphKey(item);
                Assert.That(key, Is.EqualTo(SurvivorCatalog.Get(item).Id));
                Texture2D glyph = SkillIconFactory.LoadGlyph(key);
                Assert.That(glyph, Is.Not.Null, key + " needs its own glyph or an alias");
                Assert.That(glyph.isReadable, Is.True, key);
                Assert.That(BrightPixels(SkillIconFactory.DrawPixels(key, SurvivorViewLogic.ItemColor(item))), Is.GreaterThan(400), key);
                Assert.That(SkillIconFactory.IconForItem(item), Is.Not.Null, key);
            }
            Assert.That(SkillIconFactory.LoadGlyph("no-such-glyph"), Is.Null);
            Assert.That(SkillIconFactory.LoadGlyph(null), Is.Null);
        }

        [Test]
        public void Evolutions_UseTheBaseGlyphInAGoldFrame()
        {
            for (int i = 0; i < SurvivorCatalog.EvolutionCount; i++)
            {
                int evolution = SurvivorCatalog.FirstEvolutionIndex + i;
                int weapon = SurvivorViewLogic.BaseWeapon(evolution);
                Assert.That(SkillIconFactory.ItemGlyphKey(evolution), Is.EqualTo(SkillIconFactory.ItemGlyphKey(weapon)), "item " + evolution);
                Sprite icon = SkillIconFactory.IconForItem(evolution);
                Assert.That(icon, Is.Not.Null, "item " + evolution);
                Assert.That(icon, Is.Not.SameAs(SkillIconFactory.IconForItem(weapon)), "item " + evolution);
            }

            Color theme = new Color(0.4f, 0.5f, 0.9f);
            int plainGold = GoldPixels(SkillIconFactory.DrawPixels("arrow", theme, false));
            int framedGold = GoldPixels(SkillIconFactory.DrawPixels("arrow", theme, true));
            Assert.That(framedGold, Is.GreaterThan(plainGold + 500), "the gold frame is drawn");
        }

        private static int BrightPixels(Color32[] pixels)
        {
            int bright = 0;
            foreach (Color32 c in pixels)
            {
                bright += c.r > 220 && c.g > 200 && c.a > 200 ? 1 : 0;
            }
            return bright;
        }

        private static int GoldPixels(Color32[] pixels)
        {
            int gold = 0;
            foreach (Color32 c in pixels)
            {
                gold += c.r > 200 && c.g > 150 && c.b < 170 && c.a > 200 ? 1 : 0;
            }
            return gold;
        }

        [Test]
        public void EmptySlot_FallsBackToTheWarriorIcon()
        {
            Assert.That(SkillIconFactory.KeyFor(null, 2), Is.EqualTo("shield-block"));
            Assert.That(SkillIconFactory.IconFor(null, 0), Is.Not.Null);
        }
    }
}
