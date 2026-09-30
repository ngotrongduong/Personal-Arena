using PersonalArena.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PersonalArena.View
{
    /// <summary>Widget construction of <see cref="SurvivorHud"/> (same look as the arena HUD).</summary>
    public sealed partial class SurvivorHud
    {
        private const int TrainingBarCount = 64;
        private const float TrainingGraphHeight = 64f;
        private const int CardCount = PickHighlight.MaximumOffers;

        private static readonly Color PanelColor = new Color(0.05f, 0.055f, 0.085f, 0.86f);
        private static readonly Color PanelEdge = new Color(0.55f, 0.6f, 0.85f, 0.18f);
        private static readonly Color TrackColor = new Color(0.1f, 0.1f, 0.14f, 1f);
        private static readonly Color HpColor = new Color(0.86f, 0.18f, 0.2f, 1f);
        private static readonly Color HpLowColor = new Color(1f, 0.35f, 0.2f, 1f);
        private static readonly Color EnergyColor = new Color(0.2f, 0.58f, 1f, 1f);
        private static readonly Color XpColor = new Color(0.3f, 0.82f, 1f, 1f);
        private static readonly Color BossColor = new Color(0.72f, 0.12f, 0.2f, 1f);
        private static readonly Color GoldText = new Color(1f, 0.86f, 0.45f, 1f);

        private sealed class ItemSlotView
        {
            public Image Frame;
            public Image Icon;
            public Text Level;
        }

        private sealed class SkillSlotView
        {
            public SkillDef Skill;
            public Color Color;
            public Image Icon;
            public Image Accent;
            public Image Glow;
            public Image Shade;
            public Text Title;
            public Text Cooldown;
            public float LastCooldown;
            public float Flash;
        }

        private sealed class OfferCard
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Glow;
            public Image Border;
            public Image Shade;
            public Image Icon;
            public Text Kind;
            public Text Name;
            public Text Level;
            public Text Description;
            public Color Accent;
            public float BaseX;
        }

        private Font font;
        private Transform canvasRoot;

        private RectTransform xpFill;
        private Text levelText;
        private Text clockText;
        private Text clockGoal;
        private GameObject bossPanel;
        private RectTransform bossFill;
        private Text bossText;

        private RectTransform hpFill;
        private RectTransform hpTrail;
        private Image hpFillImage;
        private Text hpText;
        private RectTransform energyFill;
        private Text energyText;
        private Text goldText;
        private Text killsText;
        private readonly ItemSlotView[] itemSlots = new ItemSlotView[ItemSlots * 2];
        private readonly SkillSlotView[] skillSlots = new SkillSlotView[SkillSlots];

        private Text helpText;
        private Text infoText;
        private GameObject pausePanel;
        private GameObject noticePanel;
        private Text noticeText;

        private GameObject offerPanel;
        private Text offerStatus;
        private readonly OfferCard[] cards = new OfferCard[CardCount];

        private GameObject endPanel;
        private Text endTitle;
        private Text endCause;
        private Text endStats;
        private Text endItems;
        private Text endFooter;

        private GameObject trainingPanel;
        private Button trainingButton;
        private Image trainingButtonImage;
        private Text trainingButtonLabel;
        private Button powerButton;
        private Text powerLabel;
        private Text trainingText;
        private Text trainingGraphCaption;
        private readonly Image[] trainingBars = new Image[TrainingBarCount];
        private TrainingHistoryPanel historyPanel;

        private void EnsureBuilt()
        {
            if (built)
            {
                return;
            }
            built = true;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject canvasObject = CreateUiObject("Survivor HUD Canvas", transform);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            canvasRoot = canvasObject.transform;

            BuildTop();
            BuildVitals();
            BuildSkills();
            BuildSidePanels();
            BuildOffer();
            BuildEnd();
        }

        private void BuildTop()
        {
            // Full-width EXP bar along the top edge.
            RectTransform xpTrack = CreateSliced("EXP Bar", canvasRoot, new Color(0.04f, 0.05f, 0.08f, 0.92f));
            xpTrack.anchorMin = new Vector2(0f, 1f);
            xpTrack.anchorMax = new Vector2(1f, 1f);
            xpTrack.pivot = new Vector2(0.5f, 1f);
            xpTrack.anchoredPosition = new Vector2(0f, -4f);
            xpTrack.sizeDelta = new Vector2(-8f, 26f);
            RectTransform xpInner = CreateUiObject("Inner", xpTrack).GetComponent<RectTransform>();
            SetStretch(xpInner, 3f, 3f, 3f, 3f);
            xpFill = CreateSliced("Fill", xpInner, XpColor);
            SetStretch(xpFill, 0f, 0f, 0f, 0f);
            RectTransform xpShine = CreateSliced("Shine", xpFill, new Color(1f, 1f, 1f, 0.2f));
            xpShine.anchorMin = new Vector2(0f, 0.55f);
            xpShine.anchorMax = Vector2.one;
            xpShine.offsetMin = new Vector2(2f, 0f);
            xpShine.offsetMax = new Vector2(-2f, -1f);
            levelText = CreateText("Level", xpTrack, 19, TextAnchor.MiddleCenter, Color.white);
            levelText.fontStyle = FontStyle.Bold;
            AddShadow(levelText);
            SetStretch(levelText.rectTransform, 0f, 0f, 0f, 0f);

            RectTransform clock = CreatePanel("Clock", canvasRoot, PanelColor);
            SetRect(clock, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(300f, 90f), new Vector2(0.5f, 1f));
            clockText = CreateText("Clock Text", clock, 46, TextAnchor.UpperCenter, Color.white);
            clockText.fontStyle = FontStyle.Bold;
            AddShadow(clockText);
            SetRect(clockText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(290f, 54f), new Vector2(0.5f, 1f));
            clockGoal = CreateText("Clock Goal", clock, 15, TextAnchor.UpperCenter, new Color(0.75f, 0.8f, 0.92f));
            SetRect(clockGoal.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(290f, 22f), new Vector2(0.5f, 1f));

            RectTransform boss = CreatePanel("Boss", canvasRoot, PanelColor);
            SetRect(boss, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -138f), new Vector2(780f, 52f), new Vector2(0.5f, 1f));
            bossPanel = boss.gameObject;
            CreateBar(boss, "Boss Bar", new Vector2(10f, -9f), 760f, 34f, BossColor, out bossFill, out _, out bossText);
            bossPanel.SetActive(false);
        }

        private void BuildVitals()
        {
            RectTransform vitals = CreatePanel("Vitals", canvasRoot, PanelColor);
            SetRect(vitals, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -40f), new Vector2(440f, 304f), new Vector2(0f, 1f));
            Text heroName = CreateText("Name", vitals, 17, TextAnchor.UpperLeft, GoldText);
            heroName.text = "CHIẾN BINH  (AI điều khiển)";
            heroName.fontStyle = FontStyle.Bold;
            SetRect(heroName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -10f), new Vector2(400f, 22f), new Vector2(0f, 1f));

            CreateBar(vitals, "HP Bar", new Vector2(18f, -36f), 404f, 36f, HpColor, out hpFill, out hpTrail, out hpText);
            hpFillImage = hpFill.GetComponent<Image>();
            CreateBar(vitals, "Energy Bar", new Vector2(18f, -78f), 404f, 24f, EnergyColor, out energyFill, out RectTransform energyTrail, out energyText);
            energyTrail.gameObject.SetActive(false);
            energyText.fontSize = 15;

            RectTransform coin = CreateUiObject("Gold Icon", vitals).GetComponent<RectTransform>();
            Image coinImage = coin.gameObject.AddComponent<Image>();
            coinImage.sprite = UiSprites.Circle();
            coinImage.color = new Color(1f, 0.8f, 0.25f);
            coinImage.raycastTarget = false;
            SetRect(coin, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -112f), new Vector2(20f, 20f), new Vector2(0f, 1f));
            goldText = CreateText("Gold", vitals, 20, TextAnchor.MiddleLeft, GoldText);
            goldText.fontStyle = FontStyle.Bold;
            SetRect(goldText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -110f), new Vector2(150f, 24f), new Vector2(0f, 1f));
            Text killsLabel = CreateText("Kills Label", vitals, 16, TextAnchor.MiddleLeft, new Color(0.75f, 0.8f, 0.9f));
            killsLabel.text = "HẠ GỤC";
            SetRect(killsLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(210f, -110f), new Vector2(80f, 24f), new Vector2(0f, 1f));
            killsText = CreateText("Kills", vitals, 20, TextAnchor.MiddleLeft, Color.white);
            killsText.fontStyle = FontStyle.Bold;
            SetRect(killsText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(290f, -110f), new Vector2(130f, 24f), new Vector2(0f, 1f));

            Text weaponsLabel = CreateText("Weapons Label", vitals, 13, TextAnchor.UpperLeft, new Color(0.65f, 0.7f, 0.8f));
            weaponsLabel.text = "VŨ KHÍ";
            SetRect(weaponsLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -144f), new Vector2(120f, 18f), new Vector2(0f, 1f));
            Text passivesLabel = CreateText("Passives Label", vitals, 13, TextAnchor.UpperLeft, new Color(0.65f, 0.7f, 0.8f));
            passivesLabel.text = "BỊ ĐỘNG";
            SetRect(passivesLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -226f), new Vector2(120f, 18f), new Vector2(0f, 1f));
            for (int i = 0; i < ItemSlots; i++)
            {
                itemSlots[i] = CreateItemSlot(vitals, "Weapon " + (i + 1), new Vector2(20f + i * 64f, -162f));
                itemSlots[ItemSlots + i] = CreateItemSlot(vitals, "Passive " + (i + 1), new Vector2(20f + i * 64f, -244f));
            }
        }

        private ItemSlotView CreateItemSlot(Transform parent, string objectName, Vector2 position)
        {
            RectTransform frame = CreateSliced(objectName, parent, new Color(1f, 1f, 1f, 0.06f));
            SetRect(frame, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(54f, 54f), new Vector2(0f, 1f));
            GameObject iconObject = CreateUiObject("Icon", frame);
            Image icon = iconObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = false;
            SetStretch(icon.rectTransform, 3f, 3f, 3f, 3f);
            Text level = CreateText("Level", frame, 14, TextAnchor.LowerRight, Color.white);
            level.fontStyle = FontStyle.Bold;
            Outline outline = level.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            SetStretch(level.rectTransform, 0f, 3f, 0f, 1f);
            return new ItemSlotView { Frame = frame.GetComponent<Image>(), Icon = icon, Level = level };
        }

        private void BuildSkills()
        {
            RectTransform skills = CreatePanel("Skills", canvasRoot, PanelColor);
            SetRect(skills, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(456f, 150f), new Vector2(0.5f, 0f));
            for (int i = 0; i < SkillSlots; i++)
            {
                skillSlots[i] = CreateSkillSlot(skills, i, -144f + i * 144f);
            }
        }

        private SkillSlotView CreateSkillSlot(Transform parent, int index, float x)
        {
            SkillSlotView view = new SkillSlotView { LastCooldown = 0f };
            RectTransform slot = CreatePanel("Skill " + (index + 1), parent, new Color(0.1f, 0.11f, 0.16f, 1f));
            SetRect(slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(132f, 132f), new Vector2(0.5f, 0.5f));

            RectTransform glow = CreateSliced("Glow", slot, Color.clear);
            SetStretch(glow, 0f, 0f, 0f, 0f);
            view.Glow = glow.GetComponent<Image>();

            RectTransform accent = CreateSliced("Accent", slot, Color.white);
            accent.anchorMin = new Vector2(0f, 1f);
            accent.anchorMax = new Vector2(1f, 1f);
            accent.pivot = new Vector2(0.5f, 1f);
            accent.anchoredPosition = new Vector2(0f, -4f);
            accent.sizeDelta = new Vector2(-24f, 4f);
            view.Accent = accent.GetComponent<Image>();

            GameObject iconObject = CreateUiObject("Icon", slot);
            view.Icon = iconObject.AddComponent<Image>();
            view.Icon.preserveAspect = true;
            view.Icon.raycastTarget = false;
            SetRect(view.Icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(72f, 72f), new Vector2(0.5f, 1f));

            view.Title = CreateText("Title", slot, 17, TextAnchor.UpperCenter, Color.white);
            view.Title.fontStyle = FontStyle.Bold;
            SetRect(view.Title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -92f), new Vector2(0f, 24f), new Vector2(0.5f, 1f));

            GameObject shadeObject = CreateUiObject("Cooldown", slot);
            view.Shade = shadeObject.AddComponent<Image>();
            // A Filled Image needs a sprite: without one Unity ignores fillAmount and covers the rect.
            view.Shade.sprite = UiSprites.RoundedSprite();
            view.Shade.color = new Color(0.02f, 0.02f, 0.05f, 0.62f);
            view.Shade.type = Image.Type.Filled;
            view.Shade.fillMethod = Image.FillMethod.Radial360;
            view.Shade.fillOrigin = (int)Image.Origin360.Top;
            view.Shade.fillClockwise = false;
            view.Shade.fillAmount = 0f;
            view.Shade.raycastTarget = false;
            SetRect(view.Shade.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(72f, 72f), new Vector2(0.5f, 1f));

            view.Cooldown = CreateText("Cooldown Time", slot, 28, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.35f));
            view.Cooldown.fontStyle = FontStyle.Bold;
            Outline outline = view.Cooldown.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            SetRect(view.Cooldown.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(128f, 72f), new Vector2(0.5f, 1f));
            return view;
        }

        private void BuildSidePanels()
        {
            RectTransform help = CreatePanel("Help", canvasRoot, new Color(0.03f, 0.035f, 0.055f, 0.62f));
            SetRect(help, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -356f), new Vector2(440f, 60f), new Vector2(0f, 1f));
            VerticalLayoutGroup helpLayout = help.gameObject.AddComponent<VerticalLayoutGroup>();
            helpLayout.padding = new RectOffset(16, 14, 10, 12);
            helpLayout.childControlWidth = true;
            helpLayout.childControlHeight = true;
            helpLayout.childForceExpandWidth = true;
            helpLayout.childForceExpandHeight = false;
            ContentSizeFitter helpFitter = help.gameObject.AddComponent<ContentSizeFitter>();
            helpFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            helpText = CreateText("Help Text", help, 15, TextAnchor.UpperLeft, new Color(0.8f, 0.83f, 0.9f));
            helpText.horizontalOverflow = HorizontalWrapMode.Wrap;
            helpText.lineSpacing = 1.1f;

            RectTransform info = CreatePanel("Info", canvasRoot, PanelColor);
            SetRect(info, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -40f), new Vector2(430f, 190f), new Vector2(1f, 1f));
            infoText = CreateText("Info Text", info, 16, TextAnchor.UpperLeft, new Color(0.9f, 0.93f, 0.97f));
            infoText.lineSpacing = 1.1f;
            SetStretch(infoText.rectTransform, 18f, 14f, 12f, 10f);

            RectTransform pause = CreatePanel("Paused", canvasRoot, new Color(0.03f, 0.03f, 0.05f, 0.8f));
            SetRect(pause, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(380f, 96f), new Vector2(0.5f, 0.5f));
            Text pauseText = CreateText("Label", pause, 46, TextAnchor.MiddleCenter, GoldText);
            pauseText.text = "TẠM DỪNG";
            pauseText.fontStyle = FontStyle.Bold;
            SetStretch(pauseText.rectTransform, 0f, 0f, 0f, 0f);
            pausePanel = pause.gameObject;
            pausePanel.SetActive(false);

            RectTransform notice = CreatePanel("Notice", canvasRoot, new Color(0.03f, 0.035f, 0.06f, 0.9f));
            SetRect(notice, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(760f, 150f), new Vector2(0.5f, 0.5f));
            noticeText = CreateText("Label", notice, 24, TextAnchor.MiddleCenter, Color.white);
            noticeText.horizontalOverflow = HorizontalWrapMode.Wrap;
            noticeText.lineSpacing = 1.15f;
            SetStretch(noticeText.rectTransform, 24f, 24f, 12f, 12f);
            noticePanel = notice.gameObject;
            noticePanel.SetActive(false);
        }

        private void BuildOffer()
        {
            GameObject dimObject = CreateUiObject("Level Up", canvasRoot);
            Image dim = dimObject.AddComponent<Image>();
            dim.color = new Color(0.01f, 0.01f, 0.03f, 0.55f);
            dim.raycastTarget = false;
            RectTransform dimRect = dim.rectTransform;
            SetStretch(dimRect, 0f, 0f, 0f, 0f);
            offerPanel = dimObject;

            Text title = CreateText("Title", dimRect, 52, TextAnchor.MiddleCenter, GoldText);
            title.text = "LÊN CẤP!";
            title.fontStyle = FontStyle.Bold;
            Outline titleOutline = title.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0.25f, 0.12f, 0f, 0.9f);
            titleOutline.effectDistance = new Vector2(2f, -2f);
            SetRect(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 260f), new Vector2(900f, 70f), new Vector2(0.5f, 0.5f));

            offerStatus = CreateText("Status", dimRect, 30, TextAnchor.MiddleCenter, Color.white);
            offerStatus.fontStyle = FontStyle.Bold;
            AddShadow(offerStatus);
            SetRect(offerStatus.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -250f), new Vector2(1000f, 44f), new Vector2(0.5f, 0.5f));

            for (int i = 0; i < CardCount; i++)
            {
                cards[i] = CreateCard(dimRect, i);
            }
            offerPanel.SetActive(false);
        }

        private OfferCard CreateCard(Transform parent, int index)
        {
            OfferCard card = new OfferCard();
            GameObject rootObject = CreateUiObject("Card " + (index + 1), parent);
            card.Root = rootObject.GetComponent<RectTransform>();
            SetRect(card.Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(256f, 340f), new Vector2(0.5f, 0.5f));
            card.Group = rootObject.AddComponent<CanvasGroup>();
            card.Group.interactable = false;
            card.Group.blocksRaycasts = false;

            RectTransform glow = CreateUiObject("Glow", card.Root).GetComponent<RectTransform>();
            card.Glow = glow.gameObject.AddComponent<Image>();
            card.Glow.sprite = UiSprites.SoftGlow();
            card.Glow.raycastTarget = false;
            card.Glow.color = Color.clear;
            SetStretch(glow, -40f, -40f, -40f, -40f);

            RectTransform border = CreateSliced("Border", card.Root, Color.white);
            SetStretch(border, -4f, -4f, -4f, -4f);
            card.Border = border.GetComponent<Image>();

            RectTransform body = CreateSliced("Body", card.Root, new Color(0.07f, 0.075f, 0.11f, 0.98f));
            SetStretch(body, 0f, 0f, 0f, 0f);

            card.Kind = CreateText("Kind", card.Root, 14, TextAnchor.UpperCenter, new Color(0.65f, 0.7f, 0.82f));
            SetRect(card.Kind.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(240f, 20f), new Vector2(0.5f, 1f));

            GameObject iconObject = CreateUiObject("Icon", card.Root);
            card.Icon = iconObject.AddComponent<Image>();
            card.Icon.preserveAspect = true;
            card.Icon.raycastTarget = false;
            SetRect(card.Icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(108f, 108f), new Vector2(0.5f, 1f));

            card.Name = CreateText("Name", card.Root, 24, TextAnchor.UpperCenter, Color.white);
            card.Name.fontStyle = FontStyle.Bold;
            SetRect(card.Name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -156f), new Vector2(240f, 32f), new Vector2(0.5f, 1f));

            card.Level = CreateText("Level", card.Root, 20, TextAnchor.UpperCenter, GoldText);
            card.Level.fontStyle = FontStyle.Bold;
            SetRect(card.Level.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -192f), new Vector2(240f, 26f), new Vector2(0.5f, 1f));

            card.Description = CreateText("Description", card.Root, 17, TextAnchor.UpperCenter, new Color(0.85f, 0.88f, 0.95f));
            card.Description.horizontalOverflow = HorizontalWrapMode.Wrap;
            card.Description.lineSpacing = 1.1f;
            SetRect(card.Description.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -228f), new Vector2(220f, 100f), new Vector2(0.5f, 1f));

            // Darkens the cards that were not chosen while keeping them opaque (a see-through card shows the busy world).
            RectTransform shade = CreateSliced("Shade", card.Root, Color.clear);
            SetStretch(shade, -4f, -4f, -4f, -4f);
            card.Shade = shade.GetComponent<Image>();
            return card;
        }

        private void BuildEnd()
        {
            RectTransform end = CreatePanel("End Panel", canvasRoot, new Color(0.035f, 0.035f, 0.06f, 0.94f));
            SetRect(end, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(620f, 540f), new Vector2(0.5f, 0.5f));
            endPanel = end.gameObject;
            RectTransform accent = CreatePanel("Accent", end, new Color(1f, 0.8f, 0.4f, 0.8f));
            SetRect(accent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(340f, 3f), new Vector2(0.5f, 1f));
            endTitle = CreateText("Title", end, 40, TextAnchor.UpperCenter, GoldText);
            endTitle.fontStyle = FontStyle.Bold;
            SetRect(endTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(600f, 52f), new Vector2(0.5f, 1f));
            endCause = CreateText("Cause", end, 20, TextAnchor.UpperCenter, new Color(1f, 0.7f, 0.65f));
            SetRect(endCause.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(600f, 26f), new Vector2(0.5f, 1f));
            endStats = CreateText("Stats", end, 26, TextAnchor.UpperCenter, Color.white);
            endStats.lineSpacing = 1.15f;
            SetRect(endStats.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -134f), new Vector2(600f, 150f), new Vector2(0.5f, 1f));
            endItems = CreateText("Items", end, 18, TextAnchor.UpperCenter, new Color(0.8f, 0.85f, 0.95f));
            endItems.lineSpacing = 1.15f;
            SetRect(endItems.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -310f), new Vector2(600f, 150f), new Vector2(0.5f, 1f));
            endFooter = CreateText("Footer", end, 18, TextAnchor.LowerCenter, new Color(0.65f, 0.7f, 0.82f));
            SetRect(endFooter.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(600f, 26f), new Vector2(0.5f, 0f));
            endPanel.SetActive(false);
        }

        private void BuildTrainingPanel()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }

            RectTransform panel = CreatePanel("Training", canvasRoot, PanelColor);
            SetRect(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -242f), new Vector2(430f, 420f), new Vector2(1f, 1f));
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

            trainingText = CreateText("Status", panel, 16, TextAnchor.UpperLeft, new Color(0.9f, 0.93f, 0.97f));
            SetRect(trainingText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -152f), new Vector2(394f, 116f), new Vector2(0f, 1f));

            trainingGraphCaption = CreateText("Graph Caption", panel, 14, TextAnchor.UpperLeft, new Color(0.7f, 0.76f, 0.84f));
            SetRect(trainingGraphCaption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -270f), new Vector2(394f, 20f), new Vector2(0f, 1f));

            RectTransform graph = CreatePanel("Reward Graph", panel, new Color(0.08f, 0.09f, 0.13f, 1f));
            SetRect(graph, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -292f), new Vector2(394f, TrainingGraphHeight), new Vector2(0f, 1f));
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

            Button dataButton = CreateButton("Training Data Button", panel, new Color(0.36f, 0.25f, 0.62f, 1f), new Vector2(18f, -366f), new Vector2(394f, 40f),
                18, out _, out Text dataLabel);
            dataLabel.text = "DỮ LIỆU HUẤN LUYỆN  (biểu đồ)  G";
            dataButton.onClick.AddListener(() => historyPanel?.Toggle());

            historyPanel = GetComponent<TrainingHistoryPanel>();
            if (historyPanel == null)
            {
                historyPanel = gameObject.AddComponent<TrainingHistoryPanel>();
            }
            historyPanel.Build(canvasRoot, font);

            trainingPanel.SetActive(false);
        }

        private Button CreateButton(string objectName, Transform parent, Color color, Vector2 position, Vector2 size, int fontSize,
            out Image image, out Text label)
        {
            RectTransform rect = CreatePanel(objectName, parent, color);
            SetRect(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size, new Vector2(0f, 1f));
            image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            Shadow shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -3f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            colors.colorMultiplier = 1.2f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            label = CreateText("Label", rect, fontSize, TextAnchor.MiddleCenter, Color.white);
            label.fontStyle = FontStyle.Bold;
            SetStretch(label.rectTransform, 6f, 6f, 0f, 0f);
            return button;
        }

        private void CreateBar(Transform parent, string barName, Vector2 position, float width, float height, Color fillColor,
            out RectTransform fill, out RectTransform trail, out Text label)
        {
            RectTransform background = CreatePanel(barName, parent, TrackColor);
            SetRect(background, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(width, height), new Vector2(0f, 1f));

            RectTransform inner = CreateUiObject("Inner", background).GetComponent<RectTransform>();
            SetStretch(inner, 3f, 3f, 3f, 3f);

            trail = CreateSliced("Trail", inner, new Color(1f, 0.92f, 0.75f, 0.55f));
            SetStretch(trail, 0f, 0f, 0f, 0f);

            fill = CreateSliced("Fill", inner, fillColor);
            SetStretch(fill, 0f, 0f, 0f, 0f);

            RectTransform shine = CreateSliced("Shine", fill, new Color(1f, 1f, 1f, 0.16f));
            shine.anchorMin = new Vector2(0f, 0.55f);
            shine.anchorMax = Vector2.one;
            shine.offsetMin = new Vector2(2f, 0f);
            shine.offsetMax = new Vector2(-2f, -2f);

            label = CreateText("Label", background, Mathf.RoundToInt(Mathf.Clamp(height * 0.52f, 14f, 19f)), TextAnchor.MiddleCenter, Color.white);
            label.fontStyle = FontStyle.Bold;
            AddShadow(label);
            SetStretch(label.rectTransform, 0f, 0f, 0f, 0f);
        }

        private static void AddShadow(Text text)
        {
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        private static RectTransform CreatePanel(string objectName, Transform parent, Color color)
        {
            RectTransform rect = CreateSliced(objectName, parent, color);
            if (color.a > 0.5f && color.r + color.g + color.b < 0.6f)
            {
                Outline outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = PanelEdge;
                outline.effectDistance = new Vector2(1f, -1f);
            }
            return rect;
        }

        private static RectTransform CreateSliced(string objectName, Transform parent, Color color)
        {
            GameObject panelObject = CreateUiObject(objectName, parent);
            Image image = panelObject.AddComponent<Image>();
            image.sprite = UiSprites.RoundedSprite();
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1.6f;
            image.color = color;
            image.raycastTarget = false;
            return image.rectTransform;
        }

        private Text CreateText(string objectName, Transform parent, int size, TextAnchor alignment, Color color)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetStretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
