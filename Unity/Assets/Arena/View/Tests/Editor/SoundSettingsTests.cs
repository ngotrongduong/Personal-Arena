using NUnit.Framework;

namespace PersonalArena.View.Tests
{
    public sealed class SoundSettingsTests
    {
        [Test]
        public void DefaultsWhenNothingIsStored()
        {
            SoundSettings settings = SoundSettings.Load(new MemorySoundStorage());

            Assert.That(settings.Master, Is.EqualTo(SoundSettings.DefaultMaster));
            Assert.That(settings.Music, Is.EqualTo(SoundSettings.DefaultMusic));
            Assert.That(settings.Effects, Is.EqualTo(SoundSettings.DefaultEffects));
            Assert.That(settings.Muted, Is.False);
            Assert.That(settings.MuteInBackground, Is.True, "the viewer runs for hours in the background");
            Assert.That(SoundSettings.Load(null).MuteInBackground, Is.True);
        }

        [Test]
        public void SettingsSurviveARoundTrip()
        {
            MemorySoundStorage storage = new MemorySoundStorage();
            SoundSettings saved = new SoundSettings
            {
                Master = 0.7f, Music = 0.2f, Effects = 0.9f, Muted = true, MuteInBackground = false
            };

            saved.Save(storage);
            SoundSettings loaded = SoundSettings.Load(storage);

            Assert.That(storage.SaveCount, Is.EqualTo(1));
            Assert.That(storage.HasKey(SoundSettings.MutedKey), Is.True);
            Assert.That(loaded.Master, Is.EqualTo(0.7f));
            Assert.That(loaded.Music, Is.EqualTo(0.2f));
            Assert.That(loaded.Effects, Is.EqualTo(0.9f));
            Assert.That(loaded.Muted, Is.True);
            Assert.That(loaded.MuteInBackground, Is.False);
        }

        [Test]
        public void BadVolumesAreClampedOrReplacedByTheDefault()
        {
            MemorySoundStorage storage = new MemorySoundStorage();
            storage.SetFloat(SoundSettings.MasterKey, 3f);
            storage.SetFloat(SoundSettings.MusicKey, -1f);
            storage.SetFloat(SoundSettings.EffectsKey, float.NaN);

            SoundSettings settings = SoundSettings.Load(storage);

            Assert.That(settings.Master, Is.EqualTo(1f));
            Assert.That(settings.Music, Is.EqualTo(0f));
            Assert.That(settings.Effects, Is.EqualTo(SoundSettings.DefaultEffects));
            settings.Music = float.PositiveInfinity;
            Assert.That(settings.Music, Is.EqualTo(SoundSettings.DefaultMusic));
        }

        [Test]
        public void ListenerIsSilentWhenMutedForcedOrInTheBackground()
        {
            SoundSettings settings = new SoundSettings { Master = 0.8f };

            Assert.That(settings.ListenerVolume(true, false), Is.EqualTo(0.8f));
            Assert.That(settings.ListenerVolume(false, false), Is.EqualTo(0f), "background, mute-in-background on");
            Assert.That(settings.ListenerVolume(true, true), Is.EqualTo(0f), "automated run");

            settings.MuteInBackground = false;
            Assert.That(settings.ListenerVolume(false, false), Is.EqualTo(0.8f));

            settings.Muted = true;
            Assert.That(settings.ListenerVolume(true, false), Is.EqualTo(0f));
        }

        [Test]
        public void BusesUseTheirSlider()
        {
            SoundSettings settings = new SoundSettings { Music = 0.3f, Effects = 0.6f };
            Assert.That(settings.BusVolume(SoundBus.Music), Is.EqualTo(0.3f));
            Assert.That(settings.BusVolume(SoundBus.Effects), Is.EqualTo(0.6f));
        }
    }
}
