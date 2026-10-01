using System;
using NUnit.Framework;

namespace PersonalArena.View.Tests
{
    public sealed class SoundMixerTests
    {
        [Test]
        public void GemPitchClimbsTheScaleWhileGemsKeepComing()
        {
            GemCombo combo = new GemCombo();
            float previous = 0f;
            float now = 10f;
            for (int i = 0; i < 7; i++)
            {
                float pitch = combo.Next(now);
                Assert.That(pitch, Is.GreaterThan(previous), "gem " + i);
                previous = pitch;
                now += 0.1f;
            }

            Assert.That(previous, Is.EqualTo((float)Math.Pow(2.0, GemCombo.TopSemitones / 12.0)).Within(1e-4f));
            Assert.That(combo.Next(now), Is.EqualTo(previous).Within(1e-4f), "stays on the top note");
            Assert.That(combo.Step, Is.EqualTo(8));
        }

        [Test]
        public void GemPitchStartsAgainAfterAPause()
        {
            GemCombo combo = new GemCombo();
            Assert.That(combo.Next(1f), Is.EqualTo(1f));
            Assert.That(combo.Next(1.2f), Is.GreaterThan(1f));
            Assert.That(combo.Next(1.2f + GemCombo.ResetSeconds + 0.05f), Is.EqualTo(1f), "gap resets");
            Assert.That(combo.Next(2.1f), Is.GreaterThan(1f));
            Assert.That(combo.Next(0.5f), Is.EqualTo(1f), "time going back resets");
            combo.Reset();
            Assert.That(combo.Step, Is.EqualTo(0));
            Assert.That(combo.Next(0.6f), Is.EqualTo(1f));
        }

        [Test]
        public void MixerGivesGemsTheComboPitch()
        {
            SoundMixer mixer = new SoundMixer(seed: 9);
            Assert.That(mixer.TryPlay(SoundCue.Gem, 1f, 0f, 0.5f, out SoundPlay first), Is.True);
            Assert.That(mixer.TryPlay(SoundCue.Gem, 1.1f, 0f, 0.5f, out SoundPlay second), Is.True);
            Assert.That(first.Pitch, Is.EqualTo(SoundCueInfo.For(SoundCue.Gem).Pitch).Within(1e-4f));
            Assert.That(second.Pitch, Is.GreaterThan(first.Pitch));
        }

        [Test]
        public void FarSoundsAreQuieterAndPannedButStayAudible()
        {
            Assert.That(SoundMixer.DistanceGain(0f), Is.EqualTo(1f));
            Assert.That(SoundMixer.DistanceGain(SoundMixer.NearDistance), Is.EqualTo(1f));
            float middle = SoundMixer.DistanceGain((SoundMixer.NearDistance + SoundMixer.FarDistance) * 0.5f);
            Assert.That(middle, Is.LessThan(1f).And.GreaterThan(SoundMixer.FarGain));
            Assert.That(SoundMixer.DistanceGain(SoundMixer.FarDistance), Is.EqualTo(SoundMixer.FarGain).Within(1e-5f));
            Assert.That(SoundMixer.DistanceGain(500f), Is.EqualTo(SoundMixer.FarGain));
            Assert.That(SoundMixer.DistanceGain(float.NaN), Is.EqualTo(1f));

            Assert.That(SoundMixer.PanFromViewport(0.5f), Is.EqualTo(0f));
            Assert.That(SoundMixer.PanFromViewport(1f), Is.EqualTo(SoundMixer.MaxPan).Within(1e-5f));
            Assert.That(SoundMixer.PanFromViewport(-3f), Is.EqualTo(-SoundMixer.MaxPan).Within(1e-5f));
            Assert.That(SoundMixer.PanFromViewport(float.NaN), Is.EqualTo(0f));
        }

        [Test]
        public void SpatialCuesFadeWithDistanceAndHeroCuesDoNot()
        {
            SoundMixer mixer = new SoundMixer(seed: 2);
            Assert.That(mixer.TryPlay(SoundCue.EnemyKill, 1f, 2f, 0.5f, out SoundPlay near), Is.True);
            Assert.That(mixer.TryPlay(SoundCue.EnemyKill, 2f, 40f, 0.9f, out SoundPlay far), Is.True);
            Assert.That(far.Volume, Is.LessThan(near.Volume * 0.5f));
            Assert.That(far.Pan, Is.GreaterThan(0f));

            Assert.That(mixer.TryPlay(SoundCue.LevelUp, 3f, 40f, 0.9f, out SoundPlay levelUp), Is.True);
            Assert.That(levelUp.Volume, Is.EqualTo(SoundCueInfo.For(SoundCue.LevelUp).Volume).Within(1e-4f));
            Assert.That(levelUp.Pan, Is.EqualTo(0f));
        }

        [Test]
        public void JitterStaysWithinItsRange()
        {
            SoundMixer mixer = new SoundMixer(seed: 4);
            SoundCueInfo info = SoundCueInfo.For(SoundCue.SwordSwing);
            for (int i = 0; i < 200; i++)
            {
                Assert.That(mixer.TryPlay(SoundCue.SwordSwing, i, 0f, 0.5f, out SoundPlay play), Is.True);
                Assert.That(play.Pitch, Is.InRange(info.Pitch * (1f - info.PitchJitter) - 1e-4f, info.Pitch * (1f + info.PitchJitter) + 1e-4f));
                Assert.That(play.Volume, Is.InRange(0f, 1f));
                Assert.That(mixer.PickClip(3), Is.InRange(0, 2));
            }
            Assert.That(mixer.PickClip(1), Is.EqualTo(0));
            Assert.That(mixer.PickClip(0), Is.EqualTo(0));
        }

        [Test]
        public void StatsCountRequestsAndPlaysForTheAudioLog()
        {
            SoundMixer mixer = new SoundMixer(seed: 6);
            mixer.TryPlay(SoundCue.Hit, 1f, 0f, 0.5f, out _);
            mixer.TryPlay(SoundCue.Hit, 1f, 0f, 0.5f, out _);
            mixer.TryPlay(SoundCue.LevelUp, 1f, 0f, 0.5f, out _);
            mixer.CountForced(SoundCue.MusicRun);
            mixer.TryPlay(SoundCue.None, 1f, 0f, 0.5f, out _);
            mixer.Stats.CountMissingClip();

            Assert.That(mixer.Stats.Requested(SoundCue.Hit), Is.EqualTo(2));
            Assert.That(mixer.Stats.Played(SoundCue.Hit), Is.EqualTo(1));
            Assert.That(mixer.Stats.Played(SoundCue.LevelUp), Is.EqualTo(1));
            Assert.That(mixer.Stats.Played(SoundCue.MusicRun), Is.EqualTo(1));
            Assert.That(mixer.Stats.Requested(SoundCue.None), Is.EqualTo(0));

            string log = mixer.Stats.Format("seed 23");
            StringAssert.StartsWith("# Personal Arena audio log", log);
            StringAssert.Contains("seed 23", log);
            StringAssert.Contains("Hit 2 1", log);
            StringAssert.Contains("LevelUp 1 1", log);
            StringAssert.Contains("MusicRun 1 1", log);
            StringAssert.Contains("total 4 3", log);
            StringAssert.Contains("missingClipCues 1", log);
            StringAssert.DoesNotContain("Gem ", log);
        }

        [Test]
        public void ResetVoicesKeepsTheCounts()
        {
            SoundMixer mixer = new SoundMixer(seed: 8);
            Assert.That(mixer.TryPlay(SoundCue.HeroHurt, 1f, 0f, 0.5f, out _), Is.True);
            Assert.That(mixer.TryPlay(SoundCue.HeroHurt, 1f, 0f, 0.5f, out _), Is.False);
            mixer.ResetVoices();
            Assert.That(mixer.TryPlay(SoundCue.HeroHurt, 1f, 0f, 0.5f, out _), Is.True);
            Assert.That(mixer.Stats.Requested(SoundCue.HeroHurt), Is.EqualTo(3));
            Assert.That(mixer.Stats.Played(SoundCue.HeroHurt), Is.EqualTo(2));
        }
    }
}
