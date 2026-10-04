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
            SetRect(fpsText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(Margin + 4f, 34f), new Vector2(200f, 24f), Vector2.zero);
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

        /// <summary>The menu card of the right column: one button per panel, with its key.</summary>
        private void BuildMetaButtons()
        {
            RectTransform panel = CreatePanel("Menu", canvasRoot, PanelColor);
            SetRect(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Margin, 0f), new Vector2(SideWidth, MenuHeight), new Vector2(1f, 1f));
            menuPanel = panel;

            CreateMenuButton(panel, 0, 0, "CHARACTER   C", ToggleCharacterPanel);
            CreateMenuButton(panel, 1, 0, "GOLD FARM   F", ToggleFarmPanel);
            CreateMenuButton(panel, 2, 0, "COMPARE   V", ToggleComparePanel);
            CreateMenuButton(panel, 0, 1, "CHARTS   G", ToggleHistoryPanel);
            CreateMenuButton(panel, 1, 1, "AI PROFILE   P", ToggleProfilePanel);
            CreateMenuButton(panel, 2, 1, "HISTORY   L", ToggleLineagePanel);
            LayoutRightColumn();
        }

        private void CreateMenuButton(Transform panel, int column, int row, string label, UnityEngine.Events.UnityAction onClick)
        {
            const float gap = 6f;
            const float height = 30f;
            float width = (SideWidth - 24f - 2f * gap) / 3f;
            Button button = CreateButton(label, panel, MenuButtonColor, new Vector2(12f + column * (width + gap), -10f - row * (height + gap)),
                new Vector2(width, height), 13, out _, out Text text);
            text.text = label;
            button.onClick.AddListener(onClick);
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
            const float inner = SideWidth - 28f;
            RectTransform panel = CreatePanel("Training", canvasRoot, PanelColor);
            SetRect(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Margin, -(TopOffset + InfoHeight + CardGap)),
                new Vector2(SideWidth, TrainingHeight), new Vector2(1f, 1f));
            trainingPanel = panel.gameObject;

            trainingButton = CreateButton("Train Button", panel, new Color(0.2f, 0.6f, 0.32f, 1f), new Vector2(14f, -12f), new Vector2(inner, 44f),
                21, out trainingButtonImage, out trainingButtonLabel);
            trainingButton.onClick.AddListener(() => TrainingButtonClicked?.Invoke());

            powerButton = CreateButton("Power Button", panel, MenuButtonColor, new Vector2(14f, -62f), new Vector2(inner, 28f),
                13, out _, out powerLabel);
            powerLabel.fontStyle = FontStyle.Normal;
            powerButton.onClick.AddListener(() => TrainingPowerClicked?.Invoke());

            // The status, plus the build/focus line and the "applies next TRAIN" note.
            trainingText = CreateText("Status", panel, 14, TextAnchor.UpperLeft, new Color(0.9f, 0.93f, 0.97f));
            trainingText.horizontalOverflow = HorizontalWrapMode.Wrap;
            trainingText.verticalOverflow = VerticalWrapMode.Truncate;
            trainingText.lineSpacing = 1.1f;
            SetRect(trainingText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -98f), new Vector2(inner - 4f, 108f), new Vector2(0f, 1f));

            trainingGraphCaption = CreateText("Graph Caption", panel, 12, TextAnchor.UpperLeft, MutedText);
            SetRect(trainingGraphCaption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -208f), new Vector2(inner - 4f, 16f), new Vector2(0f, 1f));

            RectTransform graph = CreatePanel("Reward Graph", panel, new Color(0.08f, 0.09f, 0.13f, 1f));
            SetRect(graph, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -226f), new Vector2(inner, TrainingGraphHeight), new Vector2(0f, 1f));
            float slot = inner / TrainingBarCount;
            for (int i = 0; i < TrainingBarCount; i++)
            {
                GameObject barObject = CreateUiObject("Bar " + i, graph);
                Image bar = barObject.AddComponent<Image>();
                bar.raycastTarget = false;
                SetRect(bar.rectTransform, Vector2.zero, Vector2.zero, new Vector2(i * slot + 0.5f, 0f), new Vector2(slot - 1f, 0f), Vector2.zero);
                trainingBars[i] = bar;
                barObject.SetActive(false);
            }

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
