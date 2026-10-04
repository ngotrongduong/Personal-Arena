using System;
using UnityEngine;
using UnityEngine.UI;

namespace PersonalArena.View
{
    /// <summary>
    /// M8 settings (key O or the gear button): master / music / effects volume, mute and mute-in-background (the
    /// <see cref="SoundSettings"/> of <see cref="SurvivorAudio"/>), window or borderless full screen, graphics quality,
    /// the FPS counter, and "Thoát game" with a confirm step. Everything is saved in PlayerPrefs at once.
    /// </summary>
    public sealed class SettingsPanel : MetaPanel
    {
        private const float RefreshSeconds = 0.5f;
        private const float BarWidth = 300f;

        private SurvivorAudio audioSource;
        private ViewerSettings viewer;
        private ISoundSettingsStorage storage;
        private bool applyWindowMode = true;
        private float nextRefresh;
        private bool confirmingQuit;

        private readonly Image[] volumeFills = new Image[3];
        private readonly Text[] volumeValues = new Text[3];
        private readonly UiButton[] volumeMinus = new UiButton[3];
        private readonly UiButton[] volumePlus = new UiButton[3];
        private UiButton muteButton;
        private UiButton backgroundButton;
        private UiButton windowedButton;
        private UiButton borderlessButton;
        private readonly UiButton[] qualityButtons = new UiButton[ViewerSettings.QualityCount];
        private UiButton fpsButton;
        private UiButton quitButton;
        private UiButton confirmQuitButton;
        private UiButton cancelQuitButton;
        private Text confirmText;
        private Text soundNote;

        /// <summary>Raised after a display setting changed (the HUD shows or hides the FPS counter).</summary>
        public event Action DisplayChanged;

        protected override string Title => "SETTINGS";
        protected override Vector2 CardSize => new Vector2(980f, 720f);

        public ViewerSettings Viewer => viewer;

        /// <param name="sound">The viewer's audio (null in a scene without sound: the sound rows are disabled).</param>
        /// <param name="settings">The loaded display settings (changed and saved here).</param>
        /// <param name="prefs">Where the display settings are saved.</param>
        /// <param name="changeWindowMode">False for automated runs: the window mode is saved but never applied.</param>
        public void Bind(SurvivorAudio sound, ViewerSettings settings, ISoundSettingsStorage prefs, bool changeWindowMode)
        {
            audioSource = sound;
            viewer = settings ?? new ViewerSettings();
            storage = prefs;
            applyWindowMode = changeWindowMode;
            Refresh();
        }

        /// <summary>Applies the quality level and (when allowed) the window mode to the running player.</summary>
        public static void ApplyDisplay(ViewerSettings settings, bool changeWindowMode)
        {
            if (settings == null)
            {
                return;
            }

            int levels = QualitySettings.names != null ? QualitySettings.names.Length : 0;
            int level = ViewerSettings.UnityQualityLevel(settings.Quality, levels);
            if (levels > 0 && QualitySettings.GetQualityLevel() != level)
            {
                QualitySettings.SetQualityLevel(level, true);
            }

            if (!changeWindowMode || Application.isEditor)
            {
                return;
            }

            if (settings.WindowMode == ViewerWindowMode.Borderless)
            {
                Resolution display = Screen.currentResolution;
                Screen.SetResolution(display.width, display.height, FullScreenMode.FullScreenWindow);
            }
            else if (Screen.fullScreenMode != FullScreenMode.Windowed)
            {
                Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            }
        }

        protected override void OnOpened()
        {
            confirmingQuit = false;
            Refresh();
        }

        protected override void Update()
        {
            base.Update();
            if (IsOpen && Time.unscaledTime >= nextRefresh)
            {
                // M can mute while the panel is open.
                Refresh();
            }
        }

        protected override void BuildContent(RectTransform card)
        {
            Text soundHeader = PlaceText("Sound Header", card, 22, TextAnchor.UpperLeft, Gold, 40f, -84f, 600f, 30f, true);
            soundHeader.text = "Audio";
            string[] names = { "Master volume", "Music", "Effects" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                float y = -124f - i * 58f;
                Text label = PlaceText("Volume Label " + i, card, 20, TextAnchor.MiddleLeft, Color.white, 40f, y, 230f, 46f);
                label.text = names[i];
                volumeMinus[i] = CreateButton("Volume Minus " + i, card, "-", 26, 280f, y, 56f, 46f, () => StepVolume(index, -1));
                CreateBar("Volume Bar " + i, card, 352f, y - 15f, BarWidth, 16f, Good, out volumeFills[i]);
                volumePlus[i] = CreateButton("Volume Plus " + i, card, "+", 26, 668f, y, 56f, 46f, () => StepVolume(index, 1));
                volumeValues[i] = PlaceText("Volume Value " + i, card, 20, TextAnchor.MiddleLeft, Color.white, 740f, y, 120f, 46f, true);
            }

            muteButton = CreateButton("Mute", card, string.Empty, 18, 40f, -300f, 420f, 46f, ToggleMute);
            backgroundButton = CreateButton("Mute In Background", card, string.Empty, 18, 476f, -300f, 464f, 46f, ToggleMuteInBackground);
            soundNote = PlaceText("Sound Note", card, 16, TextAnchor.UpperLeft, Muted, 40f, -352f, 900f, 24f);

            Text displayHeader = PlaceText("Display Header", card, 22, TextAnchor.UpperLeft, Gold, 40f, -390f, 600f, 30f, true);
            displayHeader.text = "Display";

            Text windowLabel = PlaceText("Window Label", card, 20, TextAnchor.MiddleLeft, Color.white, 40f, -428f, 230f, 46f);
            windowLabel.text = "Display mode";
            windowedButton = CreateButton("Windowed", card, ViewerSettings.WindowModeLabel(ViewerWindowMode.Windowed), 18,
                280f, -428f, 200f, 46f, () => SetWindowMode(ViewerWindowMode.Windowed));
            borderlessButton = CreateButton("Borderless", card, ViewerSettings.WindowModeLabel(ViewerWindowMode.Borderless), 18,
                492f, -428f, 220f, 46f, () => SetWindowMode(ViewerWindowMode.Borderless));

            Text qualityLabel = PlaceText("Quality Label", card, 20, TextAnchor.MiddleLeft, Color.white, 40f, -486f, 230f, 46f);
            qualityLabel.text = "Graphics quality";
            for (int i = 0; i < ViewerSettings.QualityCount; i++)
            {
                int quality = i;
                qualityButtons[i] = CreateButton("Quality " + i, card, ViewerSettings.QualityLabel(i), 18,
                    280f + i * 144f, -486f, 132f, 46f, () => SetQuality(quality));
            }

            Text fpsLabel = PlaceText("Fps Label", card, 20, TextAnchor.MiddleLeft, Color.white, 40f, -544f, 230f, 46f);
            fpsLabel.text = "Show FPS";
            fpsButton = CreateButton("Fps", card, string.Empty, 18, 280f, -544f, 200f, 46f, ToggleFps);

            quitButton = CreateButton("Quit", card, "Quit game", 22, 40f, -626f, 260f, 56f, () => SetConfirm(true));
            confirmText = PlaceText("Confirm Text", card, 20, TextAnchor.MiddleLeft, Warn, 320f, -626f, 260f, 56f, true);
            confirmText.text = "Quit for sure?";
            confirmQuitButton = CreateButton("Confirm Quit", card, "Quit", 20, 560f, -626f, 170f, 56f, QuitGame);
            cancelQuitButton = CreateButton("Cancel Quit", card, "Stay", 20, 744f, -626f, 170f, 56f, () => SetConfirm(false));
            Refresh();
        }

        private SoundSettings Sound => audioSource != null ? audioSource.Settings : null;

        private void StepVolume(int index, int direction)
        {
            SoundSettings sound = Sound;
            if (sound == null)
            {
                return;
            }

            switch (index)
            {
                case 0:
                    sound.Master = ViewerSettings.StepVolume(sound.Master, direction);
                    break;
                case 1:
                    sound.Music = ViewerSettings.StepVolume(sound.Music, direction);
                    break;
                default:
                    sound.Effects = ViewerSettings.StepVolume(sound.Effects, direction);
                    break;
            }

            audioSource.SaveSettings();
            Refresh();
        }

        private void ToggleMute()
        {
            if (audioSource != null)
            {
                audioSource.ToggleMute();
            }
            Refresh();
        }

        private void ToggleMuteInBackground()
        {
            SoundSettings sound = Sound;
            if (sound == null)
            {
                return;
            }

            sound.MuteInBackground = !sound.MuteInBackground;
            audioSource.SaveSettings();
            Refresh();
        }

        private void SetWindowMode(ViewerWindowMode mode)
        {
            if (viewer == null || viewer.WindowMode == mode)
            {
                return;
            }

            viewer.WindowMode = mode;
            SaveAndApply();
        }

        private void SetQuality(int quality)
        {
            if (viewer == null || viewer.Quality == quality)
            {
                return;
            }

            viewer.Quality = quality;
            SaveAndApply();
        }

        private void ToggleFps()
        {
            if (viewer == null)
            {
                return;
            }

            viewer.ShowFps = !viewer.ShowFps;
            SaveAndApply();
        }

        private void SaveAndApply()
        {
            viewer.Save(storage);
            ApplyDisplay(viewer, applyWindowMode);
            DisplayChanged?.Invoke();
            Refresh();
        }

        private void SetConfirm(bool confirm)
        {
            confirmingQuit = confirm;
            Refresh();
        }

        private void QuitGame()
        {
            // OnApplicationQuit of the controller asks running training to stop and save, and books Auto Farm.
            Debug.Log("Quit from the settings panel.");
            Application.Quit();
        }

        private void Refresh()
        {
            nextRefresh = Time.unscaledTime + RefreshSeconds;
            if (!IsBuilt || muteButton == null)
            {
                return;
            }

            SoundSettings sound = Sound;
            bool hasSound = sound != null;
            float[] volumes = hasSound ? new[] { sound.Master, sound.Music, sound.Effects } : new[] { 0f, 0f, 0f };
            for (int i = 0; i < volumes.Length; i++)
            {
                SetBar(volumeFills[i], BarWidth, volumes[i]);
                volumeValues[i].text = hasSound ? ViewerSettings.VolumeLabel(volumes[i]) : "-";
                volumeMinus[i].Set(null, ButtonColor, hasSound && volumes[i] > 0f);
                volumePlus[i].Set(null, ButtonColor, hasSound && volumes[i] < 1f);
            }

            bool muted = hasSound && sound.Muted;
            muteButton.Set("Mute (M): " + (muted ? "ON" : "OFF"), muted ? ButtonStop : ButtonColor, hasSound);
            bool background = hasSound && sound.MuteInBackground;
            backgroundButton.Set("Mute in background: " + (background ? "ON" : "OFF"), background ? ButtonActive : ButtonColor, hasSound);
            soundNote.text = !hasSound ? "This build has no sound."
                : audioSource.ForcedSilent ? "Automated run (screenshots / checks): always silent." : string.Empty;

            ViewerSettings settings = viewer ?? new ViewerSettings();
            bool bound = viewer != null;
            windowedButton.Set(null, settings.WindowMode == ViewerWindowMode.Windowed ? ButtonActive : ButtonColor, bound);
            borderlessButton.Set(null, settings.WindowMode == ViewerWindowMode.Borderless ? ButtonActive : ButtonColor, bound);
            for (int i = 0; i < qualityButtons.Length; i++)
            {
                qualityButtons[i].Set(null, settings.Quality == i ? ButtonActive : ButtonColor, bound);
            }
            fpsButton.Set(settings.ShowFps ? "ON" : "OFF", settings.ShowFps ? ButtonActive : ButtonColor, bound);

            quitButton.Set(null, ButtonStop, !confirmingQuit);
            confirmText.gameObject.SetActive(confirmingQuit);
            confirmQuitButton.SetVisible(confirmingQuit);
            cancelQuitButton.SetVisible(confirmingQuit);
            if (confirmingQuit)
            {
                confirmQuitButton.Set(null, ButtonStop, true);
                cancelQuitButton.Set(null, ButtonColor, true);
            }
        }
    }
}
