using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.View.Tests
{
    public sealed class SoundThrottleTests
    {
        private const float Frame = 1f / 60f;
        // The viewer runs at most 48 sim steps per rendered frame (x8 on a slow frame).
        private const int StepsPerFrame = 48;

        [Test]
        public void HordeAtSpeedEightStaysUnderEveryCap()
        {
            SoundMixer mixer = new SoundMixer(seed: 3);
            SurvivorClassDef kit = SurvivorDefaults.Warrior();
            List<SurvivorEvent> step = HordeStep();
            List<SoundRequest> requests = new List<SoundRequest>();
            int ordinaryLimit = SoundThrottle.DefaultGlobalVoices - SoundThrottle.ReservedForImportant;
            int[] hitsPerSecond = new int[11];
            int mostVoices = 0;

            for (int frame = 0; frame < 600; frame++)
            {
                float now = frame * Frame;
                int hitsBefore = mixer.Stats.Played(SoundCue.Hit);
                for (int s = 0; s < StepsPerFrame; s++)
                {
                    SoundCueMap.MapStep(step, kit, requests);
                    foreach (SoundRequest request in requests)
                    {
                        mixer.TryPlay(request.Cue, now, 5f, 0.5f, out _);
                    }
                }

                int voices = mixer.Throttle.ActiveVoices(now);
                mostVoices = System.Math.Max(mostVoices, voices);
                Assert.That(voices, Is.LessThanOrEqualTo(ordinaryLimit), "frame " + frame);
                Assert.That(mixer.Throttle.ActiveVoices(SoundCue.Hit, now), Is.LessThanOrEqualTo(SoundCueInfo.For(SoundCue.Hit).MaxVoices));
                Assert.That(mixer.Throttle.ActiveVoices(SoundCue.EnemyKill, now), Is.LessThanOrEqualTo(SoundCueInfo.For(SoundCue.EnemyKill).MaxVoices));
                Assert.That(mixer.Throttle.ActiveVoices(SoundCue.Gem, now), Is.LessThanOrEqualTo(SoundCueInfo.For(SoundCue.Gem).MaxVoices));
                // One rendered frame shares one real time: a cue starts at most once per frame.
                Assert.That(mixer.Stats.Played(SoundCue.Hit) - hitsBefore, Is.LessThanOrEqualTo(1));
                hitsPerSecond[(int)now] += mixer.Stats.Played(SoundCue.Hit) - hitsBefore;
            }

            long requested = 0;
            long played = 0;
            foreach (SoundCue cue in (SoundCue[])System.Enum.GetValues(typeof(SoundCue)))
            {
                requested += mixer.Stats.Requested(cue);
                played += mixer.Stats.Played(cue);
            }

            Assert.That(requested, Is.GreaterThan(1000000), "the horde asks for a lot");
            // At most about 24 voices / shortest hold per second; in practice far fewer.
            Assert.That(played, Is.LessThan(600 / 60 * 200));
            Assert.That(mostVoices, Is.GreaterThan(0));
            for (int second = 0; second < 10; second++)
            {
                Assert.That(hitsPerSecond[second], Is.LessThanOrEqualTo(21), "hits in second " + second);
                Assert.That(hitsPerSecond[second], Is.GreaterThan(0), "hits are still heard in second " + second);
            }
        }

        [Test]
        public void ImportantCuesAreNeverDroppedUnderAFullMix()
        {
            SoundMixer mixer = new SoundMixer(seed: 5);
            SurvivorClassDef kit = SurvivorDefaults.Warrior();
            List<SurvivorEvent> step = HordeStep();
            List<SoundRequest> requests = new List<SoundRequest>();
            SoundCue[] important =
            {
                SoundCue.LevelUp, SoundCue.CardsShown, SoundCue.CardPick, SoundCue.Chest, SoundCue.Magnet,
                SoundCue.Evolution, SoundCue.BossSpawn, SoundCue.BossKill, SoundCue.HeroDeath, SoundCue.VictoryJingle,
                SoundCue.UiClick
            };

            for (int frame = 0; frame < 120; frame++)
            {
                float now = frame * Frame;
                for (int s = 0; s < StepsPerFrame; s++)
                {
                    SoundCueMap.MapStep(step, kit, requests);
                    foreach (SoundRequest request in requests)
                    {
                        mixer.TryPlay(request.Cue, now, 5f, 0.5f, out _);
                    }
                }

                if (frame % 10 == 9)
                {
                    SoundCue cue = important[(frame / 10) % important.Length];
                    Assert.That(mixer.TryPlay(cue, now, 0f, 0.5f, out SoundPlay play), Is.True, cue + " at frame " + frame);
                    Assert.That(play.Important, Is.True);
                }
            }

            foreach (SoundCue cue in important)
            {
                Assert.That(SoundCueInfo.For(cue).Important, Is.True, cue.ToString());
            }
        }

        [Test]
        public void ACueWaitsForItsMinimumInterval()
        {
            SoundThrottle throttle = new SoundThrottle();
            float interval = SoundCueInfo.For(SoundCue.Hit).MinInterval;

            Assert.That(throttle.TryStart(SoundCue.Hit, 1f), Is.True);
            Assert.That(throttle.TryStart(SoundCue.Hit, 1f), Is.False);
            Assert.That(throttle.TryStart(SoundCue.Hit, 1f + interval * 0.5f), Is.False);
            Assert.That(throttle.TryStart(SoundCue.Hit, 1f + interval * 1.01f), Is.True);
            // Another cue has its own interval.
            Assert.That(throttle.TryStart(SoundCue.EnemyKill, 1f), Is.True);
        }

        [Test]
        public void VoicesEndAfterTheirHoldTime()
        {
            // Explosions ring longer than their interval, so the voice cap is what stops the fourth one.
            SoundThrottle throttle = new SoundThrottle();
            SoundCueInfo info = SoundCueInfo.For(SoundCue.Explosion);
            Assert.That(info.HoldSeconds, Is.GreaterThan(info.MaxVoices * info.MinInterval * 1.01f));
            float now = 2f;
            for (int i = 0; i < info.MaxVoices; i++)
            {
                Assert.That(throttle.TryStart(SoundCue.Explosion, now), Is.True);
                now += info.MinInterval * 1.01f;
            }

            Assert.That(throttle.ActiveVoices(SoundCue.Explosion, now), Is.EqualTo(info.MaxVoices));
            Assert.That(throttle.TryStart(SoundCue.Explosion, now), Is.False, "per-cue voice cap");
            float later = now + info.HoldSeconds + 0.01f;
            Assert.That(throttle.ActiveVoices(later), Is.EqualTo(0));
            Assert.That(throttle.TryStart(SoundCue.Explosion, later), Is.True);
        }

        [Test]
        public void TheGlobalCapKeepsRoomForImportantCues()
        {
            SoundThrottle throttle = new SoundThrottle(8);
            SoundCue[] ordinary = { SoundCue.Hit, SoundCue.EnemyKill, SoundCue.Gem, SoundCue.Coin, SoundCue.CritHit, SoundCue.SwordSwing };
            int started = 0;
            foreach (SoundCue cue in ordinary)
            {
                if (throttle.TryStart(cue, 0f))
                {
                    started++;
                }
            }

            Assert.That(started, Is.EqualTo(8 - SoundThrottle.ReservedForImportant));
            Assert.That(throttle.TryStart(SoundCue.LevelUp, 0f), Is.True);
            Assert.That(throttle.TryStart(SoundCue.Chest, 0f), Is.True);
            Assert.That(throttle.ActiveVoices(0f), Is.EqualTo(started + 2));
        }

        [Test]
        public void NoneAndBadTimesNeverPlay()
        {
            SoundThrottle throttle = new SoundThrottle();
            Assert.That(throttle.TryStart(SoundCue.None, 0f), Is.False);
            Assert.That(throttle.TryStart(SoundCue.Hit, float.NaN), Is.False);
            Assert.That(() => new SoundThrottle(SoundThrottle.ReservedForImportant), Throws.Exception);
        }

        [Test]
        public void ResetForgetsVoicesAndIntervals()
        {
            SoundThrottle throttle = new SoundThrottle();
            Assert.That(throttle.TryStart(SoundCue.HeroHurt, 1f), Is.True);
            Assert.That(throttle.TryStart(SoundCue.HeroHurt, 1f), Is.False);
            throttle.Reset();
            Assert.That(throttle.ActiveVoices(1f), Is.EqualTo(0));
            Assert.That(throttle.TryStart(SoundCue.HeroHurt, 1f), Is.True);
        }

        /// <summary>One sim step of a 250-enemy horde: many hits, kills and gems, a few volleys.</summary>
        private static List<SurvivorEvent> HordeStep()
        {
            List<SurvivorEvent> events = new List<SurvivorEvent>();
            for (int i = 0; i < 30; i++)
            {
                events.Add(new SurvivorEvent(SurvivorEventType.DamageDealt, 12f, point: new Vec2(i * 0.3f, 4f)));
            }
            for (int i = 0; i < 4; i++)
            {
                events.Add(new SurvivorEvent(SurvivorEventType.Crit, 30f, point: new Vec2(-i, 2f)));
            }
            for (int i = 0; i < 8; i++)
            {
                events.Add(new SurvivorEvent(SurvivorEventType.EnemyKilled, point: new Vec2(i, -3f)));
            }
            for (int i = 0; i < 10; i++)
            {
                events.Add(new SurvivorEvent(SurvivorEventType.XpCollected, 1f));
            }
            for (int i = 0; i < 2; i++)
            {
                events.Add(new SurvivorEvent(SurvivorEventType.GoldCollected, 1f, point: new Vec2(1f, i + 1f)));
            }
            events.Add(new SurvivorEvent(SurvivorEventType.WeaponFired, id: SurvivorCatalog.SweepIndex, point: new Vec2(1f, 0f)));
            events.Add(new SurvivorEvent(SurvivorEventType.WeaponFired, id: SurvivorCatalog.OrbitAxeIndex, point: new Vec2(1f, 0f)));
            events.Add(new SurvivorEvent(SurvivorEventType.WeaponFired, id: SurvivorCatalog.ThrownHammerIndex, point: new Vec2(0f, 1f)));
            events.Add(new SurvivorEvent(SurvivorEventType.HeroDamaged, 4f));
            events.Add(new SurvivorEvent(SurvivorEventType.Blocked, 4f));
            return events;
        }
    }
}
