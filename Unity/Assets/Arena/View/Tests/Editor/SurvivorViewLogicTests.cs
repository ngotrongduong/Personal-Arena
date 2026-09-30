using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core.Survivor;
using UnityEngine;

namespace PersonalArena.View.Tests
{
    public sealed class SurvivorViewLogicTests
    {
        private static readonly int[] CatalogItems = { 0, 3, 6, 7, 9, 12, 62, 63 };

        [TestCase(0f, "00:00")]
        [TestCase(0.99f, "00:00")]
        [TestCase(59.9f, "00:59")]
        [TestCase(60f, "01:00")]
        [TestCase(61f, "01:01")]
        [TestCase(599.5f, "09:59")]
        [TestCase(900f, "15:00")]
        [TestCase(1020f, "17:00")]
        [TestCase(-3f, "00:00")]
        [TestCase(float.NaN, "00:00")]
        [TestCase(float.PositiveInfinity, "00:00")]
        public void FormatClock_ShowsWholeMinutesAndSeconds(float seconds, string expected)
        {
            Assert.That(SurvivorViewLogic.FormatClock(seconds), Is.EqualTo(expected));
        }

        [Test]
        public void ObstacleModelIndex_IsStableAndInRange()
        {
            for (int count = 1; count <= 7; count++)
            {
                for (int obstacle = 0; obstacle < 64; obstacle++)
                {
                    int first = SurvivorViewLogic.ObstacleModelIndex(obstacle, count);
                    Assert.That(first, Is.InRange(0, count - 1));
                    Assert.That(SurvivorViewLogic.ObstacleModelIndex(obstacle, count), Is.EqualTo(first), "same obstacle, same model");
                }
            }
        }

        [Test]
        public void ObstacleModelIndex_HasNoModelWithoutModels()
        {
            Assert.That(SurvivorViewLogic.ObstacleModelIndex(3, 0), Is.EqualTo(-1));
            Assert.That(SurvivorViewLogic.ObstacleModelIndex(3, -2), Is.EqualTo(-1));
        }

        [Test]
        public void ObstacleModelIndex_UsesEveryModel()
        {
            HashSet<int> used = new HashSet<int>();
            for (int obstacle = 0; obstacle < 64; obstacle++)
            {
                used.Add(SurvivorViewLogic.ObstacleModelIndex(obstacle, 6));
            }
            Assert.That(used.Count, Is.EqualTo(6), "64 obstacles should spread over all 6 models");
        }

        [Test]
        public void ObstacleKind_IsStableAndFitsTheSize()
        {
            int smallTrees = 0;
            int bigGraves = 0;
            HashSet<ObstacleKind> mediumKinds = new HashSet<ObstacleKind>();
            for (int obstacle = 0; obstacle < 64; obstacle++)
            {
                ObstacleKind small = SurvivorViewLogic.ObstacleKindFor(obstacle, 0.6f);
                Assert.That(SurvivorViewLogic.ObstacleKindFor(obstacle, 0.6f), Is.EqualTo(small));
                smallTrees += small == ObstacleKind.Tree ? 1 : 0;
                bigGraves += SurvivorViewLogic.ObstacleKindFor(obstacle, 1.6f) == ObstacleKind.Grave ? 1 : 0;
                mediumKinds.Add(SurvivorViewLogic.ObstacleKindFor(obstacle, 1f));
                float yaw = SurvivorViewLogic.ObstacleYaw(obstacle);
                Assert.That(yaw, Is.InRange(0f, 359f));
                Assert.That(SurvivorViewLogic.ObstacleYaw(obstacle), Is.EqualTo(yaw));
            }
            Assert.That(smallTrees, Is.Zero, "small obstacles are never trees");
            Assert.That(bigGraves, Is.Zero, "big obstacles are never graves");
            Assert.That(mediumKinds.Count, Is.EqualTo(3), "medium obstacles mix all kinds");
        }

        [Test]
        public void EveryCatalogItem_HasTextsAndAnIconGlyph()
        {
            foreach (int item in CatalogItems)
            {
                ItemDef def = SurvivorCatalog.Get(item);
                Assert.That(def, Is.Not.Null, "catalog item " + item);
                Assert.That(SurvivorViewLogic.ItemDescription(item, 1), Is.Not.Empty, def.Id);
                Texture2D glyph = Resources.Load<Texture2D>(SkillIconFactory.GlyphFolder + def.Id);
                Assert.That(glyph, Is.Not.Null, def.Id + " glyph image");
                Assert.That(glyph.isReadable, Is.True, def.Id + " glyph must be readable");
                Sprite icon = SkillIconFactory.IconForKey(def.Id, SurvivorViewLogic.ItemColor(item));
                Assert.That(icon, Is.Not.Null, def.Id);
            }
        }

        [Test]
        public void LevelLabel_MarksNewItemsUpgradesAndRewards()
        {
            Assert.That(SurvivorViewLogic.LevelLabel(3, 1), Is.EqualTo("MỚI"));
            Assert.That(SurvivorViewLogic.LevelLabel(3, 4), Is.EqualTo("Lv 4"));
            Assert.That(SurvivorViewLogic.LevelLabel(62, 1), Is.Empty, "one-off rewards have no level");
        }

        [Test]
        public void SweepRange_GrowsWithLevelAndArea()
        {
            float first = SurvivorViewLogic.SweepRange(1, 1f);
            Assert.That(first, Is.GreaterThan(0f));
            Assert.That(SurvivorViewLogic.SweepRange(5, 1f), Is.GreaterThan(first));
            Assert.That(SurvivorViewLogic.SweepRange(1, 1.5f), Is.EqualTo(first * 1.5f).Within(1e-4f));
            Assert.That(SurvivorViewLogic.SweepRange(0, 0f), Is.EqualTo(first).Within(1e-4f), "invalid inputs fall back");
        }

        [Test]
        public void EndTexts_CoverEveryEnding()
        {
            foreach (EndReason reason in new[] { EndReason.Died, EndReason.Won, EndReason.TimeUp, EndReason.Expired })
            {
                Assert.That(SurvivorViewLogic.EndTitle(reason), Is.Not.Empty, reason.ToString());
            }
            Assert.That(SurvivorViewLogic.EndTitle(EndReason.None), Is.Empty);
            foreach (DeathCause cause in new[] { DeathCause.Surrounded, DeathCause.Boss, DeathCause.Brute, DeathCause.Contact, DeathCause.Projectile })
            {
                Assert.That(SurvivorViewLogic.DeathCauseText(cause), Is.Not.EqualTo(SurvivorViewLogic.DeathCauseText(DeathCause.None)), cause.ToString());
            }
        }
    }
}
