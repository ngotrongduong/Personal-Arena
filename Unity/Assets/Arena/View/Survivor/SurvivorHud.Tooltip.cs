using System.Collections.Generic;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PersonalArena.View
{
    /// <summary>
    /// Hover tooltips of the item and skill slots (what it does, its numbers, how it evolves), the key help (H)
    /// and hiding the right column (Tab).
    /// </summary>
    public sealed partial class SurvivorHud
    {
        // Layout in canvas units (1920 x 1080): a hero card on the left, the clock in the middle and a right
        // column of three cards (AI info, training, menu), all one margin away from the screen edge.
        private const float Margin = 16f;
        private const float TopOffset = 26f;
        private const float CardGap = 8f;
        private const float HeroCardWidth = 400f;
        private const float HeroCardHeight = 288f;
        private const float SideWidth = 400f;
        private const float TooltipWidth = 390f;
        private const float ClockWidth = 240f;
        private const float ItemSlotSize = 54f;
        private const float OfferCardWidth = 284f;
        private static readonly Color MutedText = new Color(0.68f, 0.73f, 0.84f, 1f);
        private static readonly Color MenuButtonColor = new Color(0.17f, 0.21f, 0.32f, 1f);

        private RectTransform infoPanel;
        private RectTransform menuPanel;
        private GameObject helpPanel;
        private Text hintText;
        private bool sidePanelsHidden;
        private RectTransform tooltip;
        private Text tooltipTitle;
        private Text tooltipSubtitle;
        private Text tooltipBody;
        private readonly List<HudHoverTarget> hoverTargets = new List<HudHoverTarget>();
        private HudHoverTarget hovered;
        private HudHoverTarget pinnedHover;
        private int shownTooltipKey = int.MinValue;
        private SkillDef shownTooltipSkill;
        private bool trainingShown;

        public bool HelpOpen => helpPanel != null && helpPanel.activeSelf;

        /// <summary>H: shows or hides the list of keys.</summary>
        public void ToggleHelp()
        {
            EnsureBuilt();
            helpPanel.SetActive(!helpPanel.activeSelf);
            if (helpPanel.activeSelf)
            {
                helpPanel.transform.SetAsLastSibling();
            }
        }

        /// <summary>Tab: hides or shows the right column (AI info, training, menu), leaving the play field clear.</summary>
        public void ToggleSidePanels()
        {
            EnsureBuilt();
            sidePanelsHidden = !sidePanelsHidden;
            LayoutRightColumn();
        }

        /// <summary>The menu card sits under the training card, or right under the info card when there is none.</summary>
        private void LayoutRightColumn()
        {
            if (infoPanel == null || menuPanel == null)
            {
                return;
            }
            infoPanel.gameObject.SetActive(!sidePanelsHidden);
            menuPanel.gameObject.SetActive(!sidePanelsHidden);
            if (trainingPanel != null)
            {
                trainingPanel.SetActive(trainingShown && !sidePanelsHidden);
            }
            float top = TopOffset + InfoHeight + CardGap + (trainingShown ? TrainingHeight + CardGap : 0f);
            menuPanel.anchoredPosition = new Vector2(-Margin, -top);
        }

        private void BuildTooltip()
        {
            RectTransform panel = CreatePanel("Tooltip", canvasRoot, new Color(0.03f, 0.035f, 0.06f, 0.96f));
            SetRect(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TooltipWidth, 80f), new Vector2(0f, 1f));
            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 12, 14);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            tooltipTitle = CreateText("Title", panel, 19, TextAnchor.UpperLeft, Color.white);
            tooltipTitle.fontStyle = FontStyle.Bold;
            tooltipSubtitle = CreateText("Subtitle", panel, 13, TextAnchor.UpperLeft, MutedText);
            tooltipBody = CreateText("Body", panel, 15, TextAnchor.UpperLeft, new Color(0.88f, 0.9f, 0.96f));
            tooltipBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            tooltipBody.lineSpacing = 1.15f;
            tooltip = panel;
            panel.gameObject.SetActive(false);
        }

        private void AddHover(RectTransform target, HudHoverKind kind, int index)
        {
            target.GetComponent<Image>().raycastTarget = true;
            HudHoverTarget hover = target.gameObject.AddComponent<HudHoverTarget>();
            hover.Kind = kind;
            hover.Index = index;
            hover.Entered = OnHoverEntered;
            hover.Exited = OnHoverExited;
            hoverTargets.Add(hover);
        }

        /// <summary>
        /// Checks of the build (-hudDemo): step after step pins the tooltip of an item slot, then of a skill slot,
        /// then shows the key help, so screenshots can show them without a mouse.
        /// </summary>
        public void ShowForChecks(int step)
        {
            EnsureBuilt();
            pinnedHover = null;
            shownTooltipKey = int.MinValue;
            helpPanel.SetActive(false);
            if (step % 3 == 2)
            {
                ToggleHelp();
                return;
            }

            HudHoverKind kind = step % 3 == 0 ? HudHoverKind.Item : HudHoverKind.Skill;
            int index = step / 3;
            for (int i = 0; i < hoverTargets.Count; i++)
            {
                if (hoverTargets[i].Kind == kind && hoverTargets[i].Index == index)
                {
                    pinnedHover = hoverTargets[i];
                }
            }
        }

        private void OnHoverEntered(HudHoverTarget target)
        {
            hovered = target;
            shownTooltipKey = int.MinValue;
        }

        private void OnHoverExited(HudHoverTarget target)
        {
            if (hovered == target)
            {
                hovered = null;
            }
        }

        private void UpdateTooltip()
        {
            if (!built || tooltip == null)
            {
                return;
            }

            HudHoverTarget target = pinnedHover != null ? pinnedHover : hovered;
            bool show = target != null && sim != null && FillTooltip(target);
            if (tooltip.gameObject.activeSelf != show)
            {
                tooltip.gameObject.SetActive(show);
                if (show)
                {
                    tooltip.SetAsLastSibling();
                }
            }
            Mouse mouse = Mouse.current;
            if (!show || (pinnedHover == null && mouse == null))
            {
                return;
            }
            // On an overlay canvas a world position is a screen position.
            Vector2 screen = pinnedHover != null ? (Vector2)pinnedHover.transform.position : mouse.position.ReadValue();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvasRoot, screen, null, out Vector2 local))
            {
                return;
            }

            // The tooltip opens toward the middle of the screen, so it never leaves it.
            bool right = local.x > 0f;
            bool top = local.y > 0f;
            tooltip.pivot = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
            tooltip.anchoredPosition = local + new Vector2(right ? -18f : 18f, top ? -18f : 18f);
        }

        /// <summary>Writes the tooltip of the hovered slot; false when the slot is empty.</summary>
        private bool FillTooltip(HudHoverTarget target)
        {
            if (target.Kind == HudHoverKind.Item)
            {
                int item = shownItems[target.Index];
                int level = shownItemLevels[target.Index];
                ItemDef def = SurvivorCatalog.Get(item);
                if (def == null)
                {
                    return false;
                }
                int key = (item + 1) * 100 + level;
                if (key != shownTooltipKey)
                {
                    shownTooltipKey = key;
                    tooltipTitle.text = def.Name;
                    tooltipTitle.color = Color.Lerp(SurvivorViewLogic.ItemColor(item), Color.white, 0.35f);
                    tooltipSubtitle.text = SurvivorViewLogic.ItemKindLabel(def) + (def.MaxLevel > 1 ? "   Lv " + level + " / " + def.MaxLevel : string.Empty);
                    tooltipBody.text = SurvivorItemDetails.Tooltip(item, level);
                }
                return true;
            }

            SkillSlotView view = skillSlots[target.Index];
            SkillDef skill = view.Skill;
            if (!SurvivorViewLogic.SkillVisible(skill))
            {
                return false;
            }
            int skillKey = -1 - target.Index;
            if (skillKey != shownTooltipKey || !ReferenceEquals(skill, shownTooltipSkill))
            {
                shownTooltipKey = skillKey;
                shownTooltipSkill = skill;
                tooltipTitle.text = SurvivorViewLogic.SkillTitle(skill);
                tooltipTitle.color = Color.Lerp(view.Color, Color.white, 0.35f);
                tooltipSubtitle.text = "SKILL " + (target.Index + 1);
                tooltipBody.text = SurvivorItemDetails.SkillTooltip(skill);
            }
            return true;
        }
    }
}
