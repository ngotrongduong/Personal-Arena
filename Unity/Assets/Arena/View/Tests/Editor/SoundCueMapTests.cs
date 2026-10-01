using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.View.Tests
{
    public sealed class SoundCueMapTests
    {
        private static readonly Vec2 Somewhere = new Vec2(3f, -4f);

        [Test]
        public void WarriorWeaponsHaveTheirOwnSounds()
        {
            Assert.That(SoundCueMap.WeaponCue(SurvivorCatalog.SweepIndex), Is.EqualTo(SoundCue.SwordSwing));
            Assert.That(SoundCueMap.WeaponCue(SurvivorCatalog.SpearThrustIndex), Is.EqualTo(SoundCue.SpearThrust));
            Assert.That(SoundCueMap.WeaponCue(SurvivorCatalog.OrbitAxeIndex), Is.EqualTo(SoundCue.AxeWhirl));
            Assert.That(SoundCueMap.WeaponCue(SurvivorCatalog.ThrownHammerIndex), Is.EqualTo(SoundCue.HammerThrow));
            Assert.That(SoundCueMap.WeaponCue(SurvivorCatalog.ShockwaveIndex), Is.EqualTo(SoundCue.Shockwave));
        }

        [Test]
        public void MageAndArcherWeaponsHaveTheirOwnSounds()
        {
            Assert.That(SoundCueMap.WeaponCue(SurvivorCatalog.MagicBoltIndex), Is.EqualTo(SoundCue.MagicBolt));
            Assert.That(SoundCueMap.WeaponCue(SurvivorCatalog.FireOrbIndex), Is.EqualTo(SoundCue.FireOrb));
            Assert.That(SoundCueMap.WeaponCue(SurvivorCatalog.FrostNovaIndex), Is.EqualTo(SoundCue.FrostNova));
            Assert.That(SoundCueMap.WeaponCue(SurvivorCatalog.ArcaneBeamIndex), Is.EqualTo(SoundCue.ArcaneBeam));
            Assert.That(SoundCueMap.WeaponCue(SurvivorCatalog.ArrowIndex), Is.EqualTo(SoundCue.ArrowShot));
            Assert.That(SoundCueMap.WeaponCue(SurvivorCatalog.MultiShotIndex), Is.EqualTo(SoundCue.MultiShot));
            Assert.That(SoundCueMap.WeaponCue(SurvivorCatalog.OrbitKnifeIndex), Is.EqualTo(SoundCue.KnifeWhirl));
            Assert.That(SoundCueMap.WeaponCue(SurvivorCatalog.DaggerIndex), Is.EqualTo(SoundCue.DaggerSlash));
            Assert.That(SoundCueMap.WeaponCue(SurvivorCatalog.CrossbowIndex), Is.EqualTo(SoundCue.CrossbowShot));
        }

        [Test]
        public void EvolutionsSoundLikeTheirBaseWeapon()
        {
            int[] bases =
            {
                SurvivorCatalog.SweepIndex, SurvivorCatalog.ThrownHammerIndex, SurvivorCatalog.MagicBoltIndex,
                SurvivorCatalog.ArcaneBeamIndex, SurvivorCatalog.ArrowIndex, SurvivorCatalog.CrossbowIndex
            };
            foreach (int weapon in bases)
            {
                int evolution = SurvivorCatalog.EvolutionOf(weapon);
                Assert.That(evolution, Is.GreaterThanOrEqualTo(0), "weapon " + weapon + " has an evolution");
                Assert.That(SoundCueMap.WeaponCue(evolution), Is.EqualTo(SoundCueMap.WeaponCue(weapon)), "evolution " + evolution);
            }
        }

        [Test]
        public void AurasStrikesAndUnknownWeaponsAreSilentWhenFired()
        {
            Assert.That(SoundCueMap.WeaponCue(SurvivorCatalog.AuraIndex), Is.EqualTo(SoundCue.None));
            Assert.That(SoundCueMap.WeaponCue(SurvivorCatalog.HolyFieldIndex), Is.EqualTo(SoundCue.None));
            Assert.That(SoundCueMap.WeaponCue(SurvivorCatalog.LightningIndex), Is.EqualTo(SoundCue.None));
            Assert.That(SoundCueMap.WeaponCue(SurvivorCatalog.ArrowRainIndex), Is.EqualTo(SoundCue.None));
            Assert.That(SoundCueMap.WeaponCue(-5), Is.EqualTo(SoundCue.None));
            Assert.That(SoundCueMap.WeaponCue(9999), Is.EqualTo(SoundCue.None));
        }

        [Test]
        public void StrikesSoundWhereTheyLand()
        {
            SurvivorClassDef mage = SurvivorDefaults.Mage();
            int lightningEvolution = SurvivorCatalog.EvolutionOf(SurvivorCatalog.LightningIndex);
            int rainEvolution = SurvivorCatalog.EvolutionOf(SurvivorCatalog.ArrowRainIndex);
            Assert.That(SoundCueMap.StrikeCue(SurvivorCatalog.LightningIndex, mage), Is.EqualTo(SoundCue.LightningStrike));
            Assert.That(SoundCueMap.StrikeCue(lightningEvolution, mage), Is.EqualTo(SoundCue.LightningStrike));
            Assert.That(SoundCueMap.StrikeCue(SurvivorCatalog.ArrowRainIndex, SurvivorDefaults.Archer()), Is.EqualTo(SoundCue.ArrowRain));
            Assert.That(SoundCueMap.StrikeCue(rainEvolution, SurvivorDefaults.Archer()), Is.EqualTo(SoundCue.ArrowRain));
            // id −1 = skill slot 0: the Mage fireball blast.
            Assert.That(SoundCueMap.StrikeCue(-1, mage), Is.EqualTo(SoundCue.Explosion));
            // The Mage frost burst (AreaBurst) in slot 3 is id −4.
            Assert.That(SoundCueMap.StrikeCue(-4, mage), Is.EqualTo(SoundCue.FrostBurst));
        }

        [Test]
        public void EverySkillOfEveryClassHasASound()
        {
            SurvivorClassDef[] kits = { SurvivorDefaults.Warrior(), SurvivorDefaults.Mage(), SurvivorDefaults.Archer() };
            foreach (SurvivorClassDef kit in kits)
            {
                for (int slot = 0; slot < kit.ActiveSkills.Length; slot++)
                {
                    SkillDef skill = kit.ActiveSkills[slot];
                    if (skill == null || skill.Kind == SkillKind.None)
                    {
                        continue;
                    }

                    SurvivorEvent used = new SurvivorEvent(SurvivorEventType.SkillUsed, slot);
                    Assert.That(SoundCueMap.Map(used, kit), Is.Not.EqualTo(SoundCue.None), kit.Id + " " + skill.Id);
                }
            }
        }

        [Test]
        public void SkillsMapByIdThenByKind()
        {
            SurvivorClassDef warrior = SurvivorDefaults.Warrior();
            SurvivorClassDef mage = SurvivorDefaults.Mage();
            SurvivorClassDef archer = SurvivorDefaults.Archer();
            Assert.That(SoundCueMap.Map(new SurvivorEvent(SurvivorEventType.SkillUsed, 0), warrior), Is.EqualTo(SoundCue.Kick));
            Assert.That(SoundCueMap.Map(new SurvivorEvent(SurvivorEventType.SkillUsed, 1), warrior), Is.EqualTo(SoundCue.ShieldUp));
            Assert.That(SoundCueMap.Map(new SurvivorEvent(SurvivorEventType.SkillUsed, 2), warrior), Is.EqualTo(SoundCue.Dash));
            Assert.That(SoundCueMap.Map(new SurvivorEvent(SurvivorEventType.SkillUsed, 0), mage), Is.EqualTo(SoundCue.Fireball));
            Assert.That(SoundCueMap.Map(new SurvivorEvent(SurvivorEventType.SkillUsed, 1), mage), Is.EqualTo(SoundCue.ManaShield));
            Assert.That(SoundCueMap.Map(new SurvivorEvent(SurvivorEventType.SkillUsed, 2), mage), Is.EqualTo(SoundCue.Blink));
            Assert.That(SoundCueMap.Map(new SurvivorEvent(SurvivorEventType.SkillUsed, 3), mage), Is.EqualTo(SoundCue.FrostBurst));
            Assert.That(SoundCueMap.Map(new SurvivorEvent(SurvivorEventType.SkillUsed, 0), archer), Is.EqualTo(SoundCue.PowerShot));
            Assert.That(SoundCueMap.Map(new SurvivorEvent(SurvivorEventType.SkillUsed, 1), archer), Is.EqualTo(SoundCue.Dash));
            Assert.That(SoundCueMap.Map(new SurvivorEvent(SurvivorEventType.SkillUsed, 9), archer), Is.EqualTo(SoundCue.None));
            Assert.That(SoundCueMap.Map(new SurvivorEvent(SurvivorEventType.SkillUsed, 0), null), Is.EqualTo(SoundCue.None));
        }

        [Test]
        public void CombatAndProgressEventsMapToTheirCues()
        {
            SurvivorClassDef kit = SurvivorDefaults.Warrior();
            Assert.That(Cue(SurvivorEventType.DamageDealt, kit), Is.EqualTo(SoundCue.Hit));
            Assert.That(Cue(SurvivorEventType.Crit, kit), Is.EqualTo(SoundCue.CritHit));
            Assert.That(Cue(SurvivorEventType.EnemyKilled, kit), Is.EqualTo(SoundCue.EnemyKill));
            Assert.That(Cue(SurvivorEventType.EliteKilled, kit), Is.EqualTo(SoundCue.EliteKill));
            Assert.That(Cue(SurvivorEventType.BossDamaged, kit), Is.EqualTo(SoundCue.BossHit));
            Assert.That(Cue(SurvivorEventType.BossKilled, kit), Is.EqualTo(SoundCue.BossKill));
            Assert.That(Cue(SurvivorEventType.HeroDamaged, kit), Is.EqualTo(SoundCue.HeroHurt));
            Assert.That(Cue(SurvivorEventType.Blocked, kit), Is.EqualTo(SoundCue.ShieldBlock));
            Assert.That(Cue(SurvivorEventType.Parry, kit), Is.EqualTo(SoundCue.Parry));
            Assert.That(Cue(SurvivorEventType.HeroDied, kit), Is.EqualTo(SoundCue.HeroDeath));
            Assert.That(Cue(SurvivorEventType.EnemyExploded, kit), Is.EqualTo(SoundCue.Explosion));
            Assert.That(Cue(SurvivorEventType.EnemySummoned, kit), Is.EqualTo(SoundCue.Summon));
            Assert.That(Cue(SurvivorEventType.XpCollected, kit), Is.EqualTo(SoundCue.Gem));
            Assert.That(Cue(SurvivorEventType.GoldCollected, kit), Is.EqualTo(SoundCue.Coin));
            Assert.That(Cue(SurvivorEventType.Healed, kit), Is.EqualTo(SoundCue.Heal));
            Assert.That(Cue(SurvivorEventType.MagnetPicked, kit), Is.EqualTo(SoundCue.Magnet));
            Assert.That(Cue(SurvivorEventType.ChestOpened, kit), Is.EqualTo(SoundCue.Chest));
            Assert.That(Cue(SurvivorEventType.LevelUp, kit), Is.EqualTo(SoundCue.LevelUp));
            Assert.That(Cue(SurvivorEventType.ItemPicked, kit), Is.EqualTo(SoundCue.CardPick));
            Assert.That(Cue(SurvivorEventType.WeaponEvolved, kit), Is.EqualTo(SoundCue.Evolution));
            Assert.That(Cue(SurvivorEventType.EliteSpawned, kit), Is.EqualTo(SoundCue.EliteSpawn));
            Assert.That(Cue(SurvivorEventType.BossSpawned, kit), Is.EqualTo(SoundCue.BossSpawn));
        }

        [Test]
        public void SpawnsOffersFailuresAndRunEndEventsAreSilent()
        {
            SurvivorClassDef kit = SurvivorDefaults.Warrior();
            Assert.That(Cue(SurvivorEventType.EnemySpawned, kit), Is.EqualTo(SoundCue.None));
            Assert.That(Cue(SurvivorEventType.OfferShown, kit), Is.EqualTo(SoundCue.None));
            Assert.That(Cue(SurvivorEventType.SkillFailed, kit), Is.EqualTo(SoundCue.None));
            Assert.That(Cue(SurvivorEventType.RunWon, kit), Is.EqualTo(SoundCue.None));
            Assert.That(Cue(SurvivorEventType.RunTimeUp, kit), Is.EqualTo(SoundCue.None));
            Assert.That(Cue(SurvivorEventType.RunExpired, kit), Is.EqualTo(SoundCue.None));
        }

        [Test]
        public void FrostBurstIsHeardOnceNotAlsoAsAnExplosion()
        {
            SurvivorClassDef mage = SurvivorDefaults.Mage();
            List<SurvivorEvent> events = new List<SurvivorEvent>
            {
                new SurvivorEvent(SurvivorEventType.DamageDealt, 10f, point: Somewhere),
                // The area burst lands at the hero (id −1) right before its SkillUsed (slot 3).
                new SurvivorEvent(SurvivorEventType.StrikeLanded, 3f, id: -1, point: new Vec2(1f, 1f)),
                new SurvivorEvent(SurvivorEventType.SkillUsed, 3f, 5f)
            };
            List<SoundRequest> output = new List<SoundRequest>();

            SoundCueMap.MapStep(events, mage, output);

            Assert.That(output.ConvertAll(r => r.Cue), Is.EqualTo(new[] { SoundCue.Hit, SoundCue.FrostBurst }));
        }

        [Test]
        public void FireballBlastStaysAnExplosion()
        {
            SurvivorClassDef mage = SurvivorDefaults.Mage();
            List<SurvivorEvent> events = new List<SurvivorEvent>
            {
                new SurvivorEvent(SurvivorEventType.StrikeLanded, 2f, id: -1, point: Somewhere),
                new SurvivorEvent(SurvivorEventType.SkillUsed, 1f)
            };
            List<SoundRequest> output = new List<SoundRequest>();

            SoundCueMap.MapStep(events, mage, output);

            Assert.That(output.ConvertAll(r => r.Cue), Is.EqualTo(new[] { SoundCue.Explosion, SoundCue.ManaShield }));
            Assert.That(output[0].AtHero, Is.False);
            Assert.That(output[0].Point, Is.EqualTo(Somewhere));
        }

        [Test]
        public void MapStepClearsTheOutputAndSkipsSilentEvents()
        {
            List<SoundRequest> output = new List<SoundRequest> { new SoundRequest(SoundCue.Hit, default, true) };
            List<SurvivorEvent> events = new List<SurvivorEvent>
            {
                new SurvivorEvent(SurvivorEventType.EnemySpawned, point: Somewhere),
                new SurvivorEvent(SurvivorEventType.XpCollected, 3f)
            };

            SoundCueMap.MapStep(events, SurvivorDefaults.Warrior(), output);

            Assert.That(output.Count, Is.EqualTo(1));
            Assert.That(output[0].Cue, Is.EqualTo(SoundCue.Gem));
            Assert.That(output[0].AtHero, Is.True);

            SoundCueMap.MapStep(null, null, output);
            Assert.That(output, Is.Empty);
        }

        [Test]
        public void WorldPointsOnlyForEventsThatHaveOne()
        {
            Assert.That(SoundCueMap.HasWorldPoint(new SurvivorEvent(SurvivorEventType.EnemyKilled, point: Somewhere)), Is.True);
            Assert.That(SoundCueMap.HasWorldPoint(new SurvivorEvent(SurvivorEventType.StrikeLanded, point: Somewhere)), Is.True);
            // Weapon volleys carry an aim direction, not a position.
            Assert.That(SoundCueMap.HasWorldPoint(new SurvivorEvent(SurvivorEventType.WeaponFired, point: new Vec2(0f, 1f))), Is.False);
            Assert.That(SoundCueMap.HasWorldPoint(new SurvivorEvent(SurvivorEventType.GoldCollected)), Is.False);
        }

        [Test]
        public void JinglesFollowTheEndReason()
        {
            Assert.That(SoundCueMap.JingleFor(EndReason.Won), Is.EqualTo(SoundCue.VictoryJingle));
            Assert.That(SoundCueMap.JingleFor(EndReason.Died), Is.EqualTo(SoundCue.DefeatJingle));
            Assert.That(SoundCueMap.JingleFor(EndReason.Expired), Is.EqualTo(SoundCue.DefeatJingle));
            Assert.That(SoundCueMap.JingleFor(EndReason.TimeUp), Is.EqualTo(SoundCue.EndJingle));
            Assert.That(SoundCueMap.JingleFor(EndReason.None), Is.EqualTo(SoundCue.None));
        }

        [Test]
        public void EveryPlayableCueHasClipsInTheSoundSetTable()
        {
            HashSet<SoundCue> covered = new HashSet<SoundCue>();
            foreach (KeyValuePair<SoundCue, string[]> entry in PersonalArena.View.Editor.SurvivorSoundSetBuilder.Table)
            {
                Assert.That(entry.Value, Is.Not.Empty, entry.Key.ToString());
                foreach (string file in entry.Value)
                {
                    Assert.That(file, Does.EndWith(".ogg"), entry.Key.ToString());
                }
                Assert.That(covered.Add(entry.Key), Is.True, "listed once: " + entry.Key);
            }

            foreach (SoundCue cue in (SoundCue[])System.Enum.GetValues(typeof(SoundCue)))
            {
                if (cue == SoundCue.None || cue == SoundCue.MusicRun || cue == SoundCue.MusicBoss)
                {
                    continue;
                }
                Assert.That(covered.Contains(cue), Is.True, "clips for " + cue);
            }
        }

        private static SoundCue Cue(SurvivorEventType type, SurvivorClassDef kit)
        {
            return SoundCueMap.Map(new SurvivorEvent(type, 1f, point: Somewhere), kit);
        }
    }
}
