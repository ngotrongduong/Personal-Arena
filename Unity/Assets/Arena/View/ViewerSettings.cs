using System;

namespace PersonalArena.View
{
    /// <summary>How the viewer window is shown.</summary>
    public enum ViewerWindowMode
    {
        Windowed = 0,
        Borderless = 1
    }

    /// <summary>
    /// The viewer's display settings from the settings panel (O): window or borderless full screen, graphics quality
    /// (Low / Medium / High mapped onto the project's Unity quality levels) and the optional FPS counter. Kept in
    /// PlayerPrefs through <see cref="ISoundSettingsStorage"/>; bad stored values fall back to the defaults.
    /// </summary>
    public sealed class ViewerSettings
    {
        public const string WindowModeKey = "Viewer.WindowMode";
        public const string QualityKey = "Viewer.Quality";
        public const string ShowFpsKey = "Viewer.ShowFps";

        public const int QualityLow = 0;
        public const int QualityMedium = 1;
        public const int QualityHigh = 2;
        public const int QualityCount = 3;

        /// <summary>The project's current level (Ultra) is the "High" choice, so the default changes nothing.</summary>
        public const int DefaultQuality = QualityHigh;
        public const ViewerWindowMode DefaultWindowMode = ViewerWindowMode.Windowed;

        private static readonly string[] QualityLabels = { "Thấp", "Vừa", "Cao" };

        private int quality = DefaultQuality;
        private ViewerWindowMode windowMode = DefaultWindowMode;

        public ViewerWindowMode WindowMode
        {
            get => windowMode;
            set => windowMode = Enum.IsDefined(typeof(ViewerWindowMode), value) ? value : DefaultWindowMode;
        }

        /// <summary>0 = Low, 1 = Medium, 2 = High; out-of-range values become the default.</summary>
        public int Quality
        {
            get => quality;
            set => quality = value >= QualityLow && value < QualityCount ? value : DefaultQuality;
        }

        public bool ShowFps { get; set; }

        public static ViewerSettings Load(ISoundSettingsStorage storage)
        {
            ViewerSettings settings = new ViewerSettings();
            if (storage == null)
            {
                return settings;
            }

            settings.WindowMode = (ViewerWindowMode)storage.GetInt(WindowModeKey, (int)DefaultWindowMode);
            settings.Quality = storage.GetInt(QualityKey, DefaultQuality);
            settings.ShowFps = storage.GetInt(ShowFpsKey, 0) != 0;
            return settings;
        }

        public void Save(ISoundSettingsStorage storage)
        {
            if (storage == null)
            {
                return;
            }

            storage.SetInt(WindowModeKey, (int)windowMode);
            storage.SetInt(QualityKey, quality);
            storage.SetInt(ShowFpsKey, ShowFps ? 1 : 0);
            storage.Save();
        }

        /// <summary>The next window mode (the panel's toggle button).</summary>
        public void CycleWindowMode()
        {
            WindowMode = windowMode == ViewerWindowMode.Windowed ? ViewerWindowMode.Borderless : ViewerWindowMode.Windowed;
        }

        public static string WindowModeLabel(ViewerWindowMode mode)
        {
            return mode == ViewerWindowMode.Borderless ? "Toàn màn hình" : "Cửa sổ";
        }

        public static string QualityLabel(int quality)
        {
            return quality >= QualityLow && quality < QualityCount ? QualityLabels[quality] : QualityLabels[DefaultQuality];
        }

        /// <summary>
        /// The Unity quality level for a choice, given how many levels the project has: High is the top level,
        /// Medium about 60% up and Low about 20% up (with the six default levels: Low 1, Medium 3, High 5).
        /// </summary>
        public static int UnityQualityLevel(int quality, int levelCount)
        {
            if (levelCount <= 1)
            {
                return 0;
            }

            int top = levelCount - 1;
            switch (quality)
            {
                case QualityLow:
                    return (int)Math.Round(top * 0.2, MidpointRounding.AwayFromZero);
                case QualityMedium:
                    return (int)Math.Round(top * 0.6, MidpointRounding.AwayFromZero);
                default:
                    return top;
            }
        }

        /// <summary>Volume slider steps: one click of - or + moves 10%, kept inside 0..1 on the 10% grid.</summary>
        public static float StepVolume(float volume, int direction)
        {
            if (float.IsNaN(volume) || float.IsInfinity(volume))
            {
                volume = 0f;
            }

            double stepped = Math.Round(volume * 10.0, MidpointRounding.AwayFromZero) + Math.Sign(direction);
            return (float)(Math.Max(0.0, Math.Min(10.0, stepped)) / 10.0);
        }

        /// <summary>A volume as a percentage label ("80%").</summary>
        public static string VolumeLabel(float volume)
        {
            if (float.IsNaN(volume) || float.IsInfinity(volume))
            {
                volume = 0f;
            }

            int percent = (int)Math.Round(Math.Max(0f, Math.Min(1f, volume)) * 100.0, MidpointRounding.AwayFromZero);
            return percent + "%";
        }

        /// <summary>The corner label of the build: "Personal Arena v0.8.123".</summary>
        public static string VersionLabel(string version)
        {
            return string.IsNullOrWhiteSpace(version) ? "Personal Arena" : "Personal Arena v" + version.Trim();
        }
    }
}
