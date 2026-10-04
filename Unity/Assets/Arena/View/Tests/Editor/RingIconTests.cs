using NUnit.Framework;
using PersonalArena.Core.Survivor;
using UnityEngine;

namespace PersonalArena.View.Tests
{
    public sealed class RingIconTests
    {
        private static readonly int[] Rings =
        {
            SurvivorCatalog.SpiritOrbsIndex, SurvivorCatalog.SawRingIndex, SurvivorCatalog.FrostHaloIndex, SurvivorCatalog.CometIndex
        };

        [Test]
        public void EveryRing_HasItsOwnGlyph_AndItsEvolutionSharesIt()
        {
            foreach (int ring in Rings)
            {
                string key = SkillIconFactory.ItemGlyphKey(ring);
                Texture2D glyph = Resources.Load<Texture2D>(SkillIconFactory.GlyphFolder + key);
                Assert.That(glyph, Is.Not.Null, key + " has no glyph of its own");
                Assert.That(glyph.isReadable, Is.True, key);
                Assert.That(SkillIconFactory.GlyphAlias(key), Is.Null, key + " no longer borrows another glyph");
                Assert.That(SkillIconFactory.ItemGlyphKey(SurvivorCatalog.EvolutionOf(ring)), Is.EqualTo(key));
                Assert.That(SkillIconFactory.IconForItem(ring), Is.Not.Null);
            }
        }

        [Test]
        public void EveryRingEvolution_HasAStyleAndASummary()
        {
            foreach (int ring in Rings)
            {
                int evolution = SurvivorCatalog.EvolutionOf(ring);
                Assert.That(SurvivorEvolutionStyles.Of(evolution).Element, Is.Not.EqualTo(EvolutionElement.None), "evolution " + evolution);
                Assert.That(SurvivorItemDetails.Summary(evolution), Does.Contain("wider circle"));
                Assert.That(SurvivorItemDetails.Evolution(ring), Does.StartWith("Evolves into " + SurvivorCatalog.Get(evolution).Name));
                Assert.That(CodexLogic.Details(evolution), Does.Contain("RECIPE"));
            }
        }
    }
}
