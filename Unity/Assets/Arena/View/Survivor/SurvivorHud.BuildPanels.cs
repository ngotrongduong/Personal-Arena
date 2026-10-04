using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PersonalArena.View
{
    /// <summary>Builds the corner widgets, the meta buttons and panels, and the training panel.</summary>
    public sealed partial class SurvivorHud
    {
        /// <summary>
        /// M8 bottom corners: the FPS counter (left, off by default), the build version and the settings gear (right).
        /// </summary>
        private void BuildCornerWidgets()
        {
            fpsText = CreateText("Fps Counter", canvasRoot, 16, TextAnchor.LowerLeft, new Color(0.75f, 0.9f, 0.75f, 0.95f));
            AddShadow(fpsText);
            SetRect(fpsText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(20f, 14f), new Vector2(200f, 24f), Vector2.zero);
            fpsText.gameObject.SetActive(false);

            versionText = CreateText("Version", canvasRoot, 14, TextAnchor.LowerRight, new Color(0.7f, 0.74f, 0.82f, 0.85f));
            AddShadow(versionText);
            SetRect(versionText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-84f, 16f), new Vector2(320f, 22f), new Vector2(1f, 0f));
            versionText.text = ViewerSettings.VersionLabel(Application.version);

            RectTransform gear = CreatePanel("Settings Button", canvasRoot, PanelColor);
            SetRect(gear, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 14f), new Vector2(52f, 52f), new Vector2(1f, 0f));
            Image gearBackground = gear.GetComponent<Image>();
            gearBackground.raycastTarget = true;
            Button gearButton = gear.gameObject.AddComponent<Button>();
            gearButton.targetGraphic = gearBackground;
            ColorBlock colors = gearButton.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.colorMultiplier = 1.3f;
            gearButton.colors = colors;
            gearButton.onClick.AddListener(UiSounds.Click);
            gearButton.onClick.AddListener(ToggleSettingsPanel);
            BuildGearIcon(gear, new Color(0.86f, 0.89f, 0.95f, 1f));
        }

        /// <summary>A gear drawn from UI shapes (the legacy font has no gear glyph): eight teeth, a ring and a hole.</summary>
        private static void BuildGearIcon(RectTransform parent, Color color)
        {
            // Four bars through the centre give the eight teeth.
            for (int i = 0; i < 4; i++)
            {
                GameObject tooth = CreateUiObject("Tooth " + i, parent);
                Image toothImage = tooth.AddComponent<Image>();
                toothImage.color = color;
                toothImage.raycastTarget = false;
                SetRect(toothImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(9f, 36f), new Vector2(0.5f, 0.5f));
                toothImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
            }

            GameObject ring = CreateUiObject("Ring", parent);
            Image ringImage = ring.AddComponent<Image>();
            ringImage.sprite = UiSprites.Circle();
            ringImage.color = color;
            ringImage.raycastTarget = false;
            SetRect(ringImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f), new Vector2(0.5f, 0.5f));

            GameObject hole = CreateUiObject("Hole", parent);
            Image holeImage = hole.AddComponent<Image>();
            holeImage.sprite = UiSprites.Circle();
            holeImage.color = new Color(0.08f, 0.1f, 0.14f, 1f);
            holeImage.raycastTarget = false;
            SetRect(holeImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 12f), new Vector2(0.5f, 0.5f));
        }

        /// <summary>NHÂN VẬT / FARM VÀNG / SO SÁNH BUILD buttons under the info panel (always shown).</summary>
        private void BuildMetaButtons()
        {
            RectTransform panel = CreatePanel("M5 Buttons", canvasRoot, PanelColor);
            SetRect(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, MetaButtonsTop), new Vector2(430f, 60f), new Vector2(1f, 1f));

            Button character = CreateButton("Character Button", panel, new Color(0.2f, 0.42f, 0.72f, 1f), new Vector2(18f, -8f), new Vector2(118f, 44f),
                14, out _, out Text characterLabel);
            characterLabel.text = "NHÂN VẬT\n(C)";
            character.onClick.AddListener(ToggleCharacterPanel);

            Button farm = CreateButton("Farm Button", panel, new Color(0.62f, 0.48f, 0.12f, 1f), new Vector2(142f, -8f), new Vector2(118f, 44f),
                14, out _, out Text farmLabel);
            farmLabel.text = "FARM VÀNG\n(F)";
            farm.onClick.AddListener(ToggleFarmPanel);

            Button compare = CreateButton("Compare Button", panel, new Color(0.2f, 0.5f, 0.48f, 1f), new Vector2(266f, -8f), new Vector2(146f, 44f),
                14, out _, out Text compareLabel);
            compareLabel.text = "SO SÁNH BUILD\n(V)";
            compare.onClick.AddListener(ToggleComparePanel);
        }

        private void BuildMetaPanels()
        {
            characterPanel = GetComponent<CharacterPanel>();
            if (characterPanel == null)
            {
                characterPanel = gameObject.AddComponent<CharacterPanel>();
            }
            characterPanel.Build(canvasRoot, font);

            farmPanel = GetComponent<AutoFarmPanel>();
            if (farmPanel == null)
            {
                farmPanel = gameObject.AddComponent<AutoFarmPanel>();
            }
            farmPanel.Build(canvasRoot, font);

            comparePanel = GetComponent<LoadoutComparePanel>();
            if (comparePanel == null)
            {
                comparePanel = gameObject.AddComponent<LoadoutComparePanel>();
            }
            comparePanel.Build(canvasRoot, font);

            lineagePanel = GetComponent<BrainLineagePanel>();
            if (lineagePanel == null)
            {
                lineagePanel = gameObject.AddComponent<BrainLineagePanel>();
            }
            lineagePanel.Build(canvasRoot, font);

            settingsPanel = GetComponent<SettingsPanel>();
            if (settingsPanel == null)
            {
                settingsPanel = gameObject.AddComponent<SettingsPanel>();
            }
            settingsPanel.Build(canvasRoot, font);
        }

        private void BuildTrainingPanel()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }

            RectTransform panel = CreatePanel("Training", canvasRoot, PanelColor);
            SetRect(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, TrainingTop), new Vector2(430f, TrainingHeight), new Vector2(1f, 1f));
            trainingPanel = panel.gameObject;

            Text title = CreateText("Title", panel, 20, TextAnchor.UpperLeft, GoldText);
            title.text = "HUẤN LUYỆN AI";
            title.fontStyle = FontStyle.Bold;
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(394f, 26f), new Vector2(0f, 1f));

            trainingButton = CreateButton("Train Button", panel, new Color(0.2f, 0.6f, 0.32f, 1f), new Vector2(18f, -44f), new Vector2(394f, 54f),
                24, out trainingButtonImage, out trainingButtonLabel);
            trainingButton.onClick.AddListener(() => TrainingButtonClicked?.Invoke());

            powerButton = CreateButton("Power Button", panel, new Color(0.17f, 0.21f, 0.32f, 1f), new Vector2(18f, -106f), new Vector2(394f, 36f),
                16, out _, out powerLabel);
            powerLabel.fontStyle = FontStyle.Normal;
            powerButton.onClick.AddListener(() => TrainingPowerClicked?.Invoke());

            // Room for the status plus the M5 build/focus line (and the "applies next TRAIN" note).
            trainingText = CreateText("Status", panel, 15, TextAnchor.UpperLeft, new Color(0.9f, 0.93f, 0.97f));
            trainingText.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetRect(trainingText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -150f), new Vector2(394f, 156f), new Vector2(0f, 1f));

            trainingGraphCaption = CreateText("Graph Caption", panel, 14, TextAnchor.UpperLeft, new Color(0.7f, 0.76f, 0.84f));
            SetRect(trainingGraphCaption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -310f), new Vector2(394f, 20f), new Vector2(0f, 1f));

            RectTransform graph = CreatePanel("Reward Graph", panel, new Color(0.08f, 0.09f, 0.13f, 1f));
            SetRect(graph, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -332f), new Vector2(394f, TrainingGraphHeight), new Vector2(0f, 1f));
            float slot = 394f / TrainingBarCount;
            for (int i = 0; i < TrainingBarCount; i++)
            {
                GameObject barObject = CreateUiObject("Bar " + i, graph);
                Image bar = barObject.AddComponent<Image>();
                bar.raycastTarget = false;
                SetRect(bar.rectTransform, Vector2.zero, Vector2.zero, new Vector2(i * slot + 0.5f, 0f), new Vector2(slot - 1f, 0f), Vector2.zero);
                trainingBars[i] = bar;
                barObject.SetActive(false);
            }

            Button dataButton = CreateButton("Training Data Button", panel, new Color(0.36f, 0.25f, 0.62f, 1f), new Vector2(18f, -406f), new Vector2(194f, 40f),
                17, out _, out Text dataLabel);
            dataLabel.text = "BIỂU ĐỒ HỌC  (G)";
            dataButton.onClick.AddListener(ToggleHistoryPanel);

            Button profileButton = CreateButton("Profile Button", panel, new Color(0.62f, 0.4f, 0.14f, 1f), new Vector2(218f, -406f), new Vector2(194f, 40f),
                17, out _, out Text profileLabel);
            profileLabel.text = "HỒ SƠ AI  (P)";
            profileButton.onClick.AddListener(ToggleProfilePanel);

            Button lineageButton = CreateButton("Lineage Button", panel, new Color(0.18f, 0.44f, 0.5f, 1f), new Vector2(18f, -452f), new Vector2(394f, 40f),
                17, out _, out Text lineageLabel);
            lineageLabel.text = "LỊCH SỬ NÃO  (L)";
            lineageButton.onClick.AddListener(ToggleLineagePanel);

            EnsureAnalysisPanels();
            trainingPanel.SetActive(false);
        }

        /// <summary>
        /// Builds the training charts (G) and AI profile (P) panels once. The training panel needs them; a viewer
        /// with a fixed -brain but a runs folder builds them too, so G and P still open.
        /// </summary>
        public void EnsureAnalysisPanels()
        {
            EnsureBuilt();
            if (historyPanel == null)
            {
                historyPanel = GetComponent<TrainingHistoryPanel>();
                if (historyPanel == null)
                {
                    historyPanel = gameObject.AddComponent<TrainingHistoryPanel>();
                }
                historyPanel.Build(canvasRoot, font);
            }

            if (profilePanel == null)
            {
                profilePanel = GetComponent<BehaviorProfilePanel>();
                if (profilePanel == null)
                {
                    profilePanel = gameObject.AddComponent<BehaviorProfilePanel>();
                }
                profilePanel.Build(canvasRoot, font);
            }
        }
    }
}
