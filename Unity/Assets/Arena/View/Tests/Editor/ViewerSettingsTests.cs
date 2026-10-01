using NUnit.Framework;
using PersonalArena.View.Editor;

namespace PersonalArena.View.Tests
{
    public sealed class ViewerSettingsTests
    {
        [Test]
        public void DefaultsKeepTheWindowAndTheCurrentQuality()
        {
            ViewerSettings settings = ViewerSettings.Load(new MemorySoundStorage());

            Assert.That(settings.WindowMode, Is.EqualTo(ViewerWindowMode.Windowed));
            Assert.That(settings.Quality, Is.EqualTo(ViewerSettings.QualityHigh));
            Assert.That(settings.ShowFps, Is.False);
            Assert.That(ViewerSettings.Load(null).Quality, Is.EqualTo(ViewerSettings.QualityHigh));
        }

        [Test]
        public void SettingsSurviveARoundTrip()
        {
            MemorySoundStorage storage = new MemorySoundStorage();
            ViewerSettings saved = new ViewerSettings
            {
                WindowMode = ViewerWindowMode.Borderless, Quality = ViewerSettings.QualityLow, ShowFps = true
            };

            saved.Save(storage);
            ViewerSettings loaded = ViewerSettings.Load(storage);

            Assert.That(storage.SaveCount, Is.EqualTo(1));
            Assert.That(loaded.WindowMode, Is.EqualTo(ViewerWindowMode.Borderless));
            Assert.That(loaded.Quality, Is.EqualTo(ViewerSettings.QualityLow));
            Assert.That(loaded.ShowFps, Is.True);
            Assert.DoesNotThrow(() => saved.Save(null));
        }

        [Test]
        public void BadStoredValuesFallBackToTheDefaults()
        {
            MemorySoundStorage storage = new MemorySoundStorage();
            storage.SetInt(ViewerSettings.WindowModeKey, 7);
            storage.SetInt(ViewerSettings.QualityKey, -3);

            ViewerSettings settings = ViewerSettings.Load(storage);

            Assert.That(settings.WindowMode, Is.EqualTo(ViewerSettings.DefaultWindowMode));
            Assert.That(settings.Quality, Is.EqualTo(ViewerSettings.DefaultQuality));
            settings.Quality = ViewerSettings.QualityCount;
            Assert.That(settings.Quality, Is.EqualTo(ViewerSettings.DefaultQuality));
        }

        [Test]
        public void CycleWindowModeToggles()
        {
            ViewerSettings settings = new ViewerSettings();
            settings.CycleWindowMode();
            Assert.That(settings.WindowMode, Is.EqualTo(ViewerWindowMode.Borderless));
            settings.CycleWindowMode();
            Assert.That(settings.WindowMode, Is.EqualTo(ViewerWindowMode.Windowed));
        }

        [Test]
        public void LabelsAreVietnamese()
        {
            Assert.That(ViewerSettings.WindowModeLabel(ViewerWindowMode.Windowed), Is.EqualTo("Cửa sổ"));
            Assert.That(ViewerSettings.WindowModeLabel(ViewerWindowMode.Borderless), Is.EqualTo("Toàn màn hình"));
            Assert.That(ViewerSettings.QualityLabel(ViewerSettings.QualityLow), Is.EqualTo("Thấp"));
            Assert.That(ViewerSettings.QualityLabel(ViewerSettings.QualityMedium), Is.EqualTo("Vừa"));
            Assert.That(ViewerSettings.QualityLabel(ViewerSettings.QualityHigh), Is.EqualTo("Cao"));
            Assert.That(ViewerSettings.QualityLabel(99), Is.EqualTo("Cao"));
        }

        [Test]
        public void QualityChoicesMapOntoUnityLevels()
        {
            // The project's six default levels (Very Low .. Ultra): High keeps today's Ultra.
            Assert.That(ViewerSettings.UnityQualityLevel(ViewerSettings.QualityLow, 6), Is.EqualTo(1));
            Assert.That(ViewerSettings.UnityQualityLevel(ViewerSettings.QualityMedium, 6), Is.EqualTo(3));
            Assert.That(ViewerSettings.UnityQualityLevel(ViewerSettings.QualityHigh, 6), Is.EqualTo(5));
            Assert.That(ViewerSettings.UnityQualityLevel(ViewerSettings.QualityHigh, 3), Is.EqualTo(2));
            Assert.That(ViewerSettings.UnityQualityLevel(ViewerSettings.QualityLow, 1), Is.EqualTo(0));
            Assert.That(ViewerSettings.UnityQualityLevel(ViewerSettings.QualityMedium, 0), Is.EqualTo(0));
        }

        [Test]
        public void VolumeStepsStayOnTheTenPercentGrid()
        {
            Assert.That(ViewerSettings.StepVolume(0.8f, 1), Is.EqualTo(0.9f).Within(1e-6f));
            Assert.That(ViewerSettings.StepVolume(0.8f, -1), Is.EqualTo(0.7f).Within(1e-6f));
            Assert.That(ViewerSettings.StepVolume(1f, 1), Is.EqualTo(1f));
            Assert.That(ViewerSettings.StepVolume(0f, -1), Is.EqualTo(0f));
            Assert.That(ViewerSettings.StepVolume(0.84f, 1), Is.EqualTo(0.9f).Within(1e-6f));
            Assert.That(ViewerSettings.StepVolume(float.NaN, 1), Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(ViewerSettings.VolumeLabel(0.8f), Is.EqualTo("80%"));
            Assert.That(ViewerSettings.VolumeLabel(2f), Is.EqualTo("100%"));
            Assert.That(ViewerSettings.VolumeLabel(float.NaN), Is.EqualTo("0%"));
        }

        [Test]
        public void VersionLabelShowsTheBuildVersion()
        {
            Assert.That(ViewerSettings.VersionLabel("0.8.123"), Is.EqualTo("Personal Arena v0.8.123"));
            Assert.That(ViewerSettings.VersionLabel(" 0.8.0 "), Is.EqualTo("Personal Arena v0.8.0"));
            Assert.That(ViewerSettings.VersionLabel(""), Is.EqualTo("Personal Arena"));
            Assert.That(ViewerSettings.VersionLabel(null), Is.EqualTo("Personal Arena"));
        }

        [Test]
        public void BuildVersionComesFromTheCommitCount()
        {
            Assert.That(WatchBuild.VersionFromCommitCount("312\n"), Is.EqualTo("0.8.312"));
            Assert.That(WatchBuild.VersionFromCommitCount(" 7 "), Is.EqualTo("0.8.7"));
            Assert.That(WatchBuild.VersionFromCommitCount(null), Is.EqualTo("0.8.0"));
            Assert.That(WatchBuild.VersionFromCommitCount("fatal: not a git repository"), Is.EqualTo("0.8.0"));
            Assert.That(WatchBuild.VersionFromCommitCount("-4"), Is.EqualTo("0.8.0"));
        }
    }
}
