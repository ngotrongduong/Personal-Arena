using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.UI;

namespace PersonalArena.View.Tests
{
    public sealed class SurvivorViewLogicTests
    {
        private static readonly int[] CatalogItems = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 62, 63 };

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
            Assert.That(SurvivorViewLogic.LevelLabel(3, 1), Is.EqualTo("NEW"));
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
            Assert.That(SurvivorViewLogic.DeathCauseText(DeathCause.Explosion), Is.EqualTo("blown up by an Exploder"));
        }

        // ------------------------------------------------------------------ M7

        [TestCase(14, WeaponVisual.MagicBolt)]
        [TestCase(15, WeaponVisual.FireOrb)]
        [TestCase(16, WeaponVisual.FrostNova)]
        [TestCase(17, WeaponVisual.HolyField)]
        [TestCase(18, WeaponVisual.Lightning)]
        [TestCase(19, WeaponVisual.ArcaneBeam)]
        [TestCase(20, WeaponVisual.Arrow)]
        [TestCase(21, WeaponVisual.MultiShot)]
        [TestCase(22, WeaponVisual.ArrowRain)]
        [TestCase(23, WeaponVisual.OrbitKnife)]
        [TestCase(24, WeaponVisual.Dagger)]
        [TestCase(25, WeaponVisual.Crossbow)]
        [TestCase(0, WeaponVisual.Sweep)]
        [TestCase(3, WeaponVisual.Hammer)]
        public void ClassWeapons_AndTheirEvolutions_HaveAVisual(int weapon, WeaponVisual visual)
        {
            Assert.That(SurvivorViewLogic.WeaponVisualOf(weapon), Is.EqualTo(visual));
            Assert.That(SurvivorViewLogic.IsEvolution(weapon), Is.False);
            Assert.That(SurvivorViewLogic.BaseWeapon(weapon), Is.EqualTo(weapon));

            int evolution = SurvivorCatalog.EvolutionOf(weapon);
            Assert.That(evolution, Is.InRange(SurvivorCatalog.FirstEvolutionIndex,
                SurvivorCatalog.FirstEvolutionIndex + SurvivorCatalog.EvolutionCount - 1));
            Assert.That(SurvivorViewLogic.IsEvolution(evolution), Is.True);
            Assert.That(SurvivorViewLogic.BaseWeapon(evolution), Is.EqualTo(weapon));
            Assert.That(SurvivorViewLogic.WeaponVisualOf(evolution), Is.EqualTo(visual), "an evolution draws like its base weapon");
        }

        [Test]
        public void NonWeapons_HaveNoVisual()
        {
            Assert.That(SurvivorViewLogic.WeaponVisualOf(6), Is.EqualTo(WeaponVisual.None));
            Assert.That(SurvivorViewLogic.WeaponVisualOf(62), Is.EqualTo(WeaponVisual.None));
            Assert.That(SurvivorViewLogic.WeaponVisualOf(-1), Is.EqualTo(WeaponVisual.None));
            Assert.That(SurvivorViewLogic.IsEvolution(-1), Is.False);
            Assert.That(SurvivorViewLogic.BaseWeapon(99), Is.EqualTo(99));
        }

        [Test]
        public void EveryEvolution_HasItsOwnElementAndColour_NotGold()
        {
            int evolutions = 0;
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int item = 0; item < SurvivorCatalog.CatalogSize; item++)
            {
                ItemDef definition = SurvivorCatalog.Get(item);
                EvolutionStyle style = SurvivorEvolutionStyles.Of(item);
                if (definition == null || definition.EvolvesFrom < 0)
                {
                    Assert.That(style.Element, Is.EqualTo(EvolutionElement.None), "item " + item);
                    continue;
                }
                evolutions++;
                Assert.That(style.Element, Is.Not.EqualTo(EvolutionElement.None), definition.Name);
                Assert.That(style.Color, Is.Not.EqualTo(SurvivorViewLogic.EvolutionGold), definition.Name);
                // No two evolutions share both element and colour.
                Assert.That(seen.Add(style.Element + "/" + style.Color), Is.True, definition.Name);
            }
            Assert.That(evolutions, Is.EqualTo(38));
        }

        [Test]
        public void BuffChip_ShowsNameAndSecondsLeft_AndHidesWhenOver()
        {
            Assert.That(SurvivorViewLogic.BuffChipText(BuffKind.Rage, 9.2f), Is.EqualTo("RAGE  10s"));
            Assert.That(SurvivorViewLogic.BuffChipText(BuffKind.Shield, 0.1f), Is.EqualTo("SHIELD  1s"));
            Assert.That(SurvivorViewLogic.BuffChipText(BuffKind.Haste, 3f), Is.EqualTo("HASTE  3s"));
            Assert.That(SurvivorViewLogic.BuffChipText(BuffKind.Haste, 0f), Is.Null);
            Assert.That(SurvivorViewLogic.BuffChipText(BuffKind.Rage, float.NaN), Is.Null);
            Assert.That(SurvivorViewLogic.BuffSeconds(BuffKind.Rage), Is.EqualTo(SurvivorSim.RageSeconds));
            Assert.That(SurvivorViewLogic.BuffSeconds(BuffKind.Shield), Is.EqualTo(SurvivorSim.ShieldSeconds));
        }

        [Test]
        public void ProjectileLook_FollowsTheSourceWeaponOrSkill()
        {
            SurvivorClassDef mage = SurvivorDefaults.Mage();
            SurvivorClassDef archer = SurvivorDefaults.Archer();

            Assert.That(SurvivorViewLogic.ProjectileLookOf(-1, mage), Is.EqualTo(ProjectileLook.Fireball), "mage slot 0 = fireball");
            Assert.That(SurvivorViewLogic.ProjectileLookOf(-1, archer), Is.EqualTo(ProjectileLook.PowerShot), "archer slot 0 = power shot");
            Assert.That(SurvivorViewLogic.ProjectileLookOf(-2, mage), Is.EqualTo(ProjectileLook.MagicBolt), "unknown skill projectiles fall back");
            Assert.That(SurvivorViewLogic.ProjectileLookOf(-1, null), Is.EqualTo(ProjectileLook.MagicBolt));
            Assert.That(SurvivorViewLogic.ProjectileLookOf(-9, archer), Is.EqualTo(ProjectileLook.MagicBolt));

            Assert.That(SurvivorViewLogic.ProjectileLookOf(SurvivorCatalog.MagicBoltIndex, mage), Is.EqualTo(ProjectileLook.MagicBolt));
            Assert.That(SurvivorViewLogic.ProjectileLookOf(SurvivorCatalog.EvolutionOf(SurvivorCatalog.MagicBoltIndex), mage),
                Is.EqualTo(ProjectileLook.MagicBolt));
            Assert.That(SurvivorViewLogic.ProjectileLookOf(SurvivorCatalog.ArrowIndex, archer), Is.EqualTo(ProjectileLook.Arrow));
            Assert.That(SurvivorViewLogic.ProjectileLookOf(SurvivorCatalog.MultiShotIndex, archer), Is.EqualTo(ProjectileLook.Arrow));
            Assert.That(SurvivorViewLogic.ProjectileLookOf(SurvivorCatalog.EvolutionOf(SurvivorCatalog.ArrowIndex), archer),
                Is.EqualTo(ProjectileLook.Arrow));
            Assert.That(SurvivorViewLogic.ProjectileLookOf(0, SurvivorDefaults.Warrior()), Is.EqualTo(ProjectileLook.SwordWave), "the sword throws a wave");
            Assert.That(SurvivorViewLogic.ProjectileLookOf(SurvivorCatalog.EvolutionOf(0), SurvivorDefaults.Warrior()), Is.EqualTo(ProjectileLook.SwordWave));
            Assert.That(SurvivorViewLogic.ProjectileLookOf(SurvivorCatalog.ThrownHammerIndex, SurvivorDefaults.Warrior()),
                Is.EqualTo(ProjectileLook.Hammer));
        }

        [Test]
        public void NewEnemies_UseAnExistingWalkerModel()
        {
            Assert.That(SurvivorViewLogic.EnemyWalkerIndex(SurvivorDefaults.ExploderTypeIndex), Is.EqualTo(0));
            Assert.That(SurvivorViewLogic.EnemyWalkerIndex(SurvivorDefaults.GhostTypeIndex), Is.EqualTo(1));
            Assert.That(SurvivorViewLogic.EnemyWalkerIndex(SurvivorDefaults.NecromancerTypeIndex), Is.EqualTo(3));
            for (int type = 0; type < SurvivorDefaults.EnemyTypeCount; type++)
            {
                Assert.That(SurvivorViewLogic.EnemyWalkerIndex(type), Is.InRange(0, 3), "type " + type);
            }
            Assert.That(SurvivorViewLogic.EnemyWalkerIndex(1), Is.EqualTo(1));
            Assert.That(SurvivorViewLogic.EnemyWalkerIndex(3), Is.EqualTo(3));
        }

        [Test]
        public void OnlyTheMageShieldIsABubble()
        {
            Assert.That(SurvivorViewLogic.BlockIsBubble(SurvivorDefaults.Mage()), Is.True);
            Assert.That(SurvivorViewLogic.BlockIsBubble(SurvivorDefaults.Warrior()), Is.False);
            Assert.That(SurvivorViewLogic.BlockIsBubble(SurvivorDefaults.Archer()), Is.False);
            Assert.That(SurvivorViewLogic.BlockIsBubble(null), Is.False);
        }

        [Test]
        public void EvolvedToast_NamesTheEvolution()
        {
            Assert.That(SurvivorViewLogic.EvolvedToast(40), Is.EqualTo("EVOLVED: Storm Blade"));
            Assert.That(SurvivorViewLogic.EvolvedToast(SurvivorCatalog.EvolutionOf(SurvivorCatalog.CrossbowIndex)),
                Is.EqualTo("EVOLVED: Siege Crossbow"));
            Assert.That(SurvivorViewLogic.EvolvedToast(-5), Is.EqualTo("EVOLVED!"));
            Assert.That(SurvivorViewLogic.EvolvedToast(40), Does.Not.Contain("★"));
        }

        [TestCase("warrior")]
        [TestCase("mage")]
        [TestCase("archer")]
        public void EveryClassSkill_HasATitleAndDescription(string classId)
        {
            SurvivorClassDef kit = SurvivorDefaults.ForClass(classId);
            Assert.That(kit, Is.Not.Null);
            int shown = 0;
            foreach (var skill in kit.ActiveSkills)
            {
                if (!SurvivorViewLogic.SkillVisible(skill))
                {
                    Assert.That(skill.Id, Is.EqualTo("none"));
                    continue;
                }
                shown++;
                string title = SurvivorViewLogic.SkillTitle(skill);
                Assert.That(title, Is.Not.Empty, skill.Id);
                Assert.That(title, Is.Not.EqualTo(skill.Id), skill.Id + " needs a title");
                Assert.That(SurvivorViewLogic.SkillDescription(skill.Id), Is.Not.Empty, skill.Id);
            }
            Assert.That(shown, Is.EqualTo(classId == "archer" ? 5 : 6));
            Assert.That(SurvivorViewLogic.SkillVisible(null), Is.False);
            Assert.That(SurvivorViewLogic.SkillTitle(null), Is.Empty);
            Assert.That(SurvivorViewLogic.SkillDescription("unknown"), Is.Empty);
        }

        [Test]
        public void SkillBar_CentresUpToSixSlots()
        {
            Assert.That(SurvivorViewLogic.SkillPanelWidth(3), Is.EqualTo(352f));
            Assert.That(SurvivorViewLogic.SkillPanelWidth(4), Is.EqualTo(464f));
            Assert.That(SurvivorViewLogic.SkillPanelWidth(0), Is.EqualTo(SurvivorViewLogic.SkillPanelWidth(1)));
            Assert.That(SurvivorViewLogic.SkillSlotX(1, 3), Is.EqualTo(0f));
            Assert.That(SurvivorViewLogic.SkillSlotX(0, 3), Is.EqualTo(-112f));
            Assert.That(SurvivorViewLogic.SkillSlotX(2, 3), Is.EqualTo(112f));
            Assert.That(SurvivorViewLogic.SkillSlotX(0, 4), Is.EqualTo(-168f));
            Assert.That(SurvivorViewLogic.SkillSlotX(3, 4), Is.EqualTo(168f));
            Assert.That(SurvivorViewLogic.SkillPanelWidth(5), Is.EqualTo(576f));
            Assert.That(SurvivorViewLogic.SkillPanelWidth(6), Is.EqualTo(688f));
            Assert.That(SurvivorViewLogic.SkillSlotX(0, 6), Is.EqualTo(-280f));
            Assert.That(SurvivorViewLogic.SkillSlotX(5, 6), Is.EqualTo(280f));
        }

        [Test]
        public void HeroNameLine_NamesTheSelectedClass()
        {
            Assert.That(SurvivorViewLogic.HeroNameLine("mage"), Is.EqualTo("MAGE  (AI controlled)"));
            Assert.That(SurvivorViewLogic.HeroNameLine("archer"), Is.EqualTo("ARCHER  (AI controlled)"));
            Assert.That(SurvivorViewLogic.HeroNameLine("warrior"), Is.EqualTo("WARRIOR  (AI controlled)"));
        }

        [Test]
        public void Hud_RebindsSixSkillsAndCompactsArcherWithoutMovingCooldowns()
        {
            GameObject root = new GameObject("Schema v5 HUD test");
            try
            {
                SurvivorHud hud = root.AddComponent<SurvivorHud>();
                foreach (string classId in new[] { "warrior", "archer", "mage", "warrior" })
                {
                    SurvivorClassDef kit = SurvivorDefaults.ForClass(classId);
                    SurvivorSim sim = new SurvivorSim(new SurvivorConfig { ClassDef = kit }, 7);
                    sim.Hero.SkillCooldowns[4] = kit.ActiveSkills[4].Cooldown * 0.5f;
                    sim.Hero.SkillCooldowns[5] = kit.ActiveSkills[5].Cooldown * 0.25f;
                    hud.Bind(sim, null);
                    Transform canvas = root.transform.Find("Survivor HUD Canvas");
                    RectTransform panel = canvas.Find("Skills").GetComponent<RectTransform>();
                    int shown = classId == "archer" ? 5 : 6;
                    int column = 0;
                    for (int slot = 0; slot < SurvivorInput.SkillSlotCount; slot++)
                    {
                        RectTransform view = panel.Find("Skill " + (slot + 1)).GetComponent<RectTransform>();
                        bool visible = kit.ActiveSkills[slot].Kind != PersonalArena.Core.SkillKind.None;
                        Assert.That(view.gameObject.activeSelf, Is.EqualTo(visible), classId + " slot " + slot);
                        if (!visible) continue;
                        Assert.That(view.anchoredPosition.x, Is.EqualTo(SurvivorViewLogic.SkillSlotX(column++, shown)));
                        float expected = slot == 4 ? 0.5f : slot == 5 ? 0.25f : 0f;
                        Assert.That(view.Find("Cooldown").GetComponent<Image>().fillAmount, Is.EqualTo(expected).Within(0.001f));
                    }
                    Assert.That(column, Is.EqualTo(shown));
                    Assert.That(panel.sizeDelta.x, Is.EqualTo(SurvivorViewLogic.SkillPanelWidth(shown)));
                    RectTransform vitals = canvas.Find("Vitals").GetComponent<RectTransform>();
                    foreach (string row in new[] { "Weapon", "Passive" })
                    {
                        for (int slot = 1; slot <= 6; slot++)
                        {
                            RectTransform item = vitals.Find(row + " " + slot).GetComponent<RectTransform>();
                            Assert.That(item.gameObject.activeSelf, Is.True);
                            Assert.That(item.anchoredPosition.x + item.sizeDelta.x, Is.LessThan(vitals.sizeDelta.x));
                            Assert.That(-item.anchoredPosition.y + item.sizeDelta.y, Is.LessThan(vitals.sizeDelta.y));
                        }
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase("warrior", 1920, 1080)]
        [TestCase("mage", 1920, 1080)]
        [TestCase("archer", 1920, 1080)]
        [TestCase("warrior", 1280, 720)]
        [TestCase("mage", 1280, 720)]
        [TestCase("archer", 1280, 720)]
        public void Hud_FitsBothTargetResolutions(string classId, int width, int height)
        {
            GameObject root = new GameObject("HUD layout test");
            RenderTexture target = null;
            Texture2D shot = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                SurvivorHud hud = root.AddComponent<SurvivorHud>();
                hud.Bind(new SurvivorSim(new SurvivorConfig { ClassDef = SurvivorDefaults.ForClass(classId) }, 7), null);
                Canvas canvas = root.GetComponentInChildren<Canvas>();
                GameObject cameraObject = new GameObject("HUD test camera", typeof(Camera));
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
                target = new RenderTexture(width, height, 24);
                camera.targetTexture = target;
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                canvas.scaleFactor = width / 1920f;
                Canvas.ForceUpdateCanvases();
                RectTransform skills = canvas.transform.Find("Skills").GetComponent<RectTransform>();
                RectTransform vitals = canvas.transform.Find("Vitals").GetComponent<RectTransform>();
                Rect skillBounds = ScreenBounds(skills, camera);
                Rect vitalBounds = ScreenBounds(vitals, camera);
                Assert.That(skillBounds.xMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(skillBounds.xMax, Is.LessThanOrEqualTo(width));
                Assert.That(skillBounds.yMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(skillBounds.yMax, Is.LessThanOrEqualTo(height));
                Assert.That(skillBounds.Overlaps(vitalBounds), Is.False);
                for (int slot = 1; slot <= 6; slot++)
                {
                    foreach (string row in new[] { "Weapon", "Passive" })
                    {
                        Rect bounds = ScreenBounds(vitals.Find(row + " " + slot).GetComponent<RectTransform>(), camera);
                        Assert.That(vitalBounds.Contains(bounds.min), Is.True);
                        Assert.That(vitalBounds.Contains(bounds.max), Is.True);
                    }
                }

                // Opt-in review artifacts; ordinary test runs do not write screenshots.
                string[] args = System.Environment.GetCommandLineArgs();
                int capture = System.Array.IndexOf(args, "-hudCaptureDirectory");
                if (capture >= 0 && capture + 1 < args.Length)
                {
                    camera.Render();
                    RenderTexture.active = target;
                    shot = new Texture2D(width, height, TextureFormat.RGB24, false);
                    shot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    shot.Apply();
                    System.IO.Directory.CreateDirectory(args[capture + 1]);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(args[capture + 1],
                        classId + "-" + width + "x" + height + ".png"), shot.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(root);
                if (shot != null) Object.DestroyImmediate(shot);
                if (target != null) Object.DestroyImmediate(target);
            }
        }

        private static Rect ScreenBounds(RectTransform rect, Camera camera)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        [Test]
        public void DaggerSweep_MatchesTheSimulation()
        {
            ItemDef dagger = SurvivorCatalog.Get(SurvivorCatalog.DaggerIndex);
            Assert.That(SurvivorViewLogic.SweepRange(SurvivorCatalog.DaggerIndex, 1, 1f), Is.EqualTo(dagger.BaseRange).Within(1e-4f));
            Assert.That(SurvivorViewLogic.SweepRange(SurvivorCatalog.DaggerIndex, 3, 1.5f),
                Is.EqualTo(dagger.BaseRange * (1f + dagger.RangePerLevel * 2) * 1.5f).Within(1e-4f));
            Assert.That(SurvivorViewLogic.SweepRange(SurvivorCatalog.DaggerIndex, 1, 1f), Is.LessThan(SurvivorViewLogic.SweepRange(0, 1, 1f)));
            Assert.That(SurvivorViewLogic.SweepHitsBehind(SurvivorCatalog.DaggerIndex, 4), Is.False);
            Assert.That(SurvivorViewLogic.SweepHitsBehind(SurvivorCatalog.DaggerIndex, 5), Is.True);

            int twin = SurvivorCatalog.EvolutionOf(SurvivorCatalog.DaggerIndex);
            Assert.That(SurvivorViewLogic.SweepHitsBehind(twin, 1), Is.True, "evolved sweeps hit behind from level 1");
            Assert.That(SurvivorViewLogic.SweepHitsBehind(SurvivorCatalog.EvolutionOf(0), 1), Is.True);
            Assert.That(SurvivorViewLogic.SweepRange(twin, 1, 1f), Is.EqualTo(SurvivorCatalog.Get(twin).BaseRange).Within(1e-4f));
            Assert.That(SurvivorViewLogic.SweepRange(0, 3, 1.2f), Is.EqualTo(SurvivorViewLogic.SweepRange(3, 1.2f)).Within(1e-4f),
                "the sword keeps its old reach");
        }

        [Test]
        public void ThrustLength_UsesTheWeaponRange()
        {
            Assert.That(SurvivorViewLogic.ThrustLength(SurvivorCatalog.ArcaneBeamIndex, 1f), Is.EqualTo(7f).Within(1e-4f));
            Assert.That(SurvivorViewLogic.ThrustLength(SurvivorCatalog.CrossbowIndex, 2f), Is.EqualTo(18f).Within(1e-4f));
            int doomRay = SurvivorCatalog.EvolutionOf(SurvivorCatalog.ArcaneBeamIndex);
            Assert.That(SurvivorViewLogic.ThrustLength(doomRay, 1f), Is.GreaterThan(7f));
            Assert.That(SurvivorViewLogic.ThrustLength(-1, 0f), Is.EqualTo(5f).Within(1e-4f), "unknown weapons fall back");
        }

        [Test]
        public void ClassWeaponsAndEvolutions_HaveDescriptionsAndColors()
        {
            Color fallback = SurvivorViewLogic.ItemColor(-1);
            for (int item = SurvivorCatalog.MagicBoltIndex; item <= SurvivorCatalog.CrossbowIndex; item++)
            {
                Assert.That(SurvivorCatalog.Get(item), Is.Not.Null, "item " + item);
                for (int level = 1; level <= 5; level++)
                {
                    Assert.That(SurvivorViewLogic.ItemDescription(item, level), Is.Not.Empty, "item " + item + " level " + level);
                }
                Assert.That(SurvivorViewLogic.ItemDescription(item, 1), Is.Not.EqualTo(SurvivorViewLogic.ItemDescription(item, 2)),
                    "item " + item + ": a new weapon reads differently from an upgrade");
                Assert.That(SurvivorViewLogic.ItemColor(item), Is.Not.EqualTo(fallback), "item " + item);
                Assert.That(SurvivorViewLogic.LevelLabel(item, 1), Is.EqualTo("NEW"));
            }

            for (int item = 0; item < SurvivorCatalog.CatalogSize; item++)
            {
                ItemDef definition = SurvivorCatalog.Get(item);
                if (definition == null || definition.EvolvesFrom < 0) continue;
                Assert.That(SurvivorViewLogic.IsEvolution(item), Is.True, "item " + item);
                Assert.That(SurvivorViewLogic.ItemName(item), Is.Not.Empty, "item " + item);
                Assert.That(SurvivorViewLogic.ItemDescription(item, 1), Is.Not.Empty, "item " + item);
                Assert.That(SurvivorViewLogic.ItemColor(item), Is.Not.EqualTo(SurvivorViewLogic.ItemColor(SurvivorViewLogic.BaseWeapon(item))),
                    "item " + item + ": evolutions are tinted gold");
            }
        }
    }
}
