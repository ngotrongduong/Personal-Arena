using System;
using PersonalArena.Core.Meta;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.UI;

namespace PersonalArena.View
{
    /// <summary>
    /// Character panel (key C): level purchase, the 5 loadouts with their stat points, the difficulty tier,
    /// the Training Focus and the class shop. Every change goes through <see cref="ProfileRules"/>, is saved at
    /// once, and applies from the next watched run and the next TRAIN.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class CharacterPanel : MetaPanel
    {
        private const float LeftX = 34f;
        private const float LeftWidth = 940f;
        private const float RightX = 1000f;
        private const float RightWidth = 566f;
        private const float RowTop = -150f;
        private const float RowHeight = 38f;
        private const float StatBarWidth = 200f;

        private ProfileStore store;
        private Func<bool> changePending;
        private bool saveFailed;

        private Text headerText;
        private Text walletText;
        private Text pendingText;
        private UiButton buyButton;
        private Text buyReason;

        private readonly UiButton[] loadoutTabs = new UiButton[ProfileRules.LoadoutCount];
        private InputField nameField;
        private UiButton resetButton;
        private Text unspentText;
        private readonly StatRow[] statRows = new StatRow[StatInfo.UsedCount];

        private UiButton tierDown;
        private UiButton tierUp;
        private Text tierLabel;
        private Text tierUnlocked;
        private Text tierGold;
        private Text tierRules;

        private readonly UiButton[] focusButtons = new UiButton[TrainingFocusInfo.Count];
        private readonly Text[] focusHints = new Text[TrainingFocusInfo.Count];

        // M7 class shop: one card per class (name, state, play style, Mua, Chọn).
        private readonly ShopCard[] shopCards = new ShopCard[ClassViewLogic.ClassIds.Length];

        /// <summary>Raised after the profile changed and was saved (the viewer refreshes its wallet line).</summary>
        public event Action ProfileChanged;

        protected override string Title => "CHARACTER & LOADOUTS";
        protected override Vector2 CardSize => new Vector2(1600f, 1000f);

        /// <summary>The profile to edit; <paramref name="pending"/> tells whether the next run will differ from the current one.</summary>
        public void Bind(ProfileStore profileStore, Func<bool> pending)
        {
            store = profileStore;
            changePending = pending;
            Refresh();
        }

        /// <summary>Re-reads the profile (wallet or level may have changed outside the panel).</summary>
        public void Refresh()
        {
            if (!IsBuilt || !IsOpen || store == null)
            {
                return;
            }

            PlayerProfile profile = store.Profile;
            CharacterProfile warrior = store.Selected;
            if (warrior == null)
            {
                return;
            }

            int loadout = Mathf.Clamp(warrior.ActiveLoadout, 0, ProfileRules.LoadoutCount - 1);
            int unspent = ProfileRules.UnspentPoints(warrior, loadout);

            headerText.text = ClassViewLogic.UpperName(warrior.ClassId) + "  Lv " + warrior.Level;
            walletText.text = "Wallet: " + MetaViewLogic.FormatGold(profile.Gold) + " gold     Unspent points: " + unspent +
                "     Active loadout: " + MetaViewLogic.LoadoutName(warrior, loadout);
            bool pending = changePending != null && changePending();
            pendingText.text = saveFailed ? "The profile could not be saved" : pending ? "Applies from the next run" : string.Empty;

            RefreshBuy(profile, warrior);
            RefreshLoadouts(warrior, loadout, unspent);
            RefreshTier(profile);
            RefreshFocus(profile);
            RefreshShop(profile);
        }

        protected override void OnOpened()
        {
            Refresh();
        }

        protected override void BuildContent(RectTransform card)
        {
            headerText = PlaceText("Character", card, 30, TextAnchor.UpperLeft, Gold, LeftX, -78f, 600f, 38f, true);
            walletText = PlaceText("Wallet", card, 19, TextAnchor.UpperLeft, Color.white, LeftX, -120f, 610f, 26f);
            pendingText = PlaceText("Pending", card, 18, TextAnchor.UpperRight, Warn, RightX, -26f, RightWidth - 70f, 26f, true);

            buyButton = CreateButton("Buy Level", card, "Buy level", 20, 660f, -74f, 314f, 48f, OnBuyLevel);
            buyReason = PlaceText("Buy Reason", card, 15, TextAnchor.UpperLeft, Warn, 660f, -126f, 314f, 22f);

            BuildLoadoutSection(card);
            BuildTierSection(card);
            BuildFocusSection(card);
            BuildShopSection(card);

            Text footer = PlaceText("Footer", card, 17, TextAnchor.UpperLeft, Muted, LeftX, -960f, 1530f, 26f);
            footer.text = "Changes apply from the next run you watch. The AI learns this build at the next TRAIN. " +
                "Each character has its own level, loadouts and brain.";
        }

        // ------------------------------------------------------------------ building

        private void BuildLoadoutSection(RectTransform card)
        {
            RectTransform section = CreateCard("Loadouts", card, LeftX, -160f, LeftWidth, 680f);
            // Rows: RowTop + 13 · RowHeight ends near -640 inside this card.
            Header(section, "Loadouts (5, each with its own points)", 900f);

            for (int i = 0; i < loadoutTabs.Length; i++)
            {
                int index = i;
                loadoutTabs[i] = CreateButton("Loadout " + (i + 1), section, ProfileRules.DefaultLoadoutName(i), 16,
                    18f + i * 182f, -50f, 174f, 40f, () => OnSelectLoadout(index));
            }

            Text nameLabel = PlaceText("Name Label", section, 17, TextAnchor.MiddleLeft, Muted, 18f, -102f, 90f, 36f);
            nameLabel.text = "Name:";
            nameField = CreateNameField(section, 104f, -102f, 300f, 36f);
            resetButton = CreateButton("Reset Points", section, "Reset points (free)", 16, 420f, -102f, 230f, 36f, OnResetPoints);
            unspentText = PlaceText("Unspent", section, 18, TextAnchor.MiddleLeft, Gold, 666f, -102f, 260f, 36f, true);

            for (int i = 0; i < statRows.Length; i++)
            {
                statRows[i] = BuildStatRow(section, (StatId)i, RowTop - i * RowHeight);
            }
        }

        private StatRow BuildStatRow(Transform parent, StatId stat, float y)
        {
            StatRow row = new StatRow { Stat = stat };
            row.Name = PlaceText("Stat " + stat, parent, 18, TextAnchor.MiddleLeft, Color.white, 18f, y, 170f, 32f, true);
            row.Name.text = MetaViewLogic.StatName(stat);
            row.Effect = PlaceText("Effect", parent, 15, TextAnchor.MiddleLeft, Muted, 192f, y, 300f, 32f);
            row.Effect.text = MetaViewLogic.StatEffect(stat);
            CreateBar("Bar", parent, 500f, y - 10f, StatBarWidth, 12f, Good, out row.Fill);
            row.Value = PlaceText("Value", parent, 17, TextAnchor.MiddleCenter, Color.white, 708f, y, 96f, 32f);
            row.Minus = CreateButton("Minus", parent, "−", 22, 812f, y, 50f, 32f, () => OnRemovePoint(stat));
            row.Plus = CreateButton("Plus", parent, "+", 22, 870f, y, 50f, 32f, () => OnAddPoint(stat));
            return row;
        }

        private void BuildTierSection(RectTransform card)
        {
            RectTransform section = CreateCard("Tier", card, RightX, -160f, RightWidth, 250f);
            Header(section, "Difficulty tier", 520f);
            tierDown = CreateButton("Tier Down", section, "<", 24, 18f, -50f, 54f, 44f, () => OnChangeTier(-1));
            tierLabel = PlaceText("Tier", section, 24, TextAnchor.MiddleCenter, Color.white, 76f, -50f, 150f, 44f, true);
            tierUp = CreateButton("Tier Up", section, ">", 24, 230f, -50f, 54f, 44f, () => OnChangeTier(1));
            tierUnlocked = PlaceText("Unlocked", section, 16, TextAnchor.MiddleLeft, Muted, 300f, -50f, 250f, 44f, false, true);
            tierGold = PlaceText("Gold", section, 18, TextAnchor.UpperLeft, Gold, 18f, -104f, 530f, 24f, true);
            tierRules = PlaceText("Rules", section, 16, TextAnchor.UpperLeft, Color.white, 18f, -134f, 530f, 108f, false, true);
        }

        private void BuildFocusSection(RectTransform card)
        {
            RectTransform section = CreateCard("Focus", card, RightX, -422f, RightWidth, 240f);
            Header(section, "Training focus", 520f);
            for (int i = 0; i < TrainingFocusInfo.Count; i++)
            {
                TrainingFocus focus = (TrainingFocus)i;
                focusButtons[i] = CreateButton("Focus " + focus, section, TrainingFocusInfo.DisplayName(focus), 15,
                    18f + i * 107f, -48f, 101f, 40f, () => OnSelectFocus(focus));
                focusHints[i] = PlaceText("Hint " + focus, section, 14, TextAnchor.UpperLeft, Muted, 18f, -98f - i * 27f, 530f, 24f);
                focusHints[i].text = TrainingFocusInfo.DisplayName(focus) + ": " + MetaViewLogic.FocusHint(focus);
            }
        }

        private void BuildShopSection(RectTransform card)
        {
            RectTransform section = CreateCard("Shop", card, RightX, -674f, RightWidth, 276f);
            Header(section, "Character shop", 520f);
            string[] ids = ClassViewLogic.ClassIds;
            const float columnWidth = 176f;
            for (int i = 0; i < ids.Length; i++)
            {
                string classId = ids[i];
                float x = 12f + i * (columnWidth + 8f);
                ShopCard shop = new ShopCard { ClassId = classId };
                RectTransform box = CreateImage("Class " + classId, section, Track, UiSprites.RoundedSprite(), Image.Type.Sliced);
                Place(box, x, -46f, columnWidth, 222f);
                shop.Name = PlaceText("Name", box, 18, TextAnchor.UpperLeft, Color.white, 8f, -6f, columnWidth - 16f, 24f, true);
                shop.Name.text = ClassViewLogic.DisplayName(classId);
                shop.Status = PlaceText("Status", box, 14, TextAnchor.UpperLeft, Muted, 8f, -32f, columnWidth - 16f, 20f, false, true);
                shop.Style = PlaceText("Style", box, 13, TextAnchor.UpperLeft, Muted, 8f, -54f, columnWidth - 16f, 72f, false, true);
                shop.Style.verticalOverflow = VerticalWrapMode.Truncate;
                shop.Style.text = ClassViewLogic.PlayStyle(classId);
                shop.Buy = CreateButton("Buy", box, ClassViewLogic.BuyLabel(classId), 14, 8f, -130f, columnWidth - 16f, 38f, () => OnBuyClass(classId));
                shop.Select = CreateButton("Select", box, "Select", 15, 8f, -174f, columnWidth - 16f, 38f, () => OnSelectClass(classId));
                shopCards[i] = shop;
            }
        }

        private InputField CreateNameField(Transform parent, float x, float y, float width, float height)
        {
            RectTransform rect = CreateImage("Name Field", parent, Track, UiSprites.RoundedSprite(), Image.Type.Sliced);
            Place(rect, x, y, width, height);
            Image background = rect.GetComponent<Image>();
            background.raycastTarget = true;

            Text text = CreateText("Text", rect, 18, TextAnchor.MiddleLeft, Color.white);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            SetStretch(text.rectTransform, 10f, 10f, 2f, 2f);

            Text placeholder = CreateText("Placeholder", rect, 17, TextAnchor.MiddleLeft, TextDisabled);
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.text = "Loadout name (up to " + ProfileRules.MaxNameLength + " characters)";
            placeholder.horizontalOverflow = HorizontalWrapMode.Wrap;
            placeholder.verticalOverflow = VerticalWrapMode.Truncate;
            SetStretch(placeholder.rectTransform, 10f, 10f, 2f, 2f);

            InputField field = rect.gameObject.AddComponent<InputField>();
            field.targetGraphic = background;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.characterLimit = ProfileRules.MaxNameLength;
            field.lineType = InputField.LineType.SingleLine;
            field.onEndEdit.AddListener(OnRenameSubmitted);
            return field;
        }

        // ------------------------------------------------------------------ refresh

        private void RefreshBuy(PlayerProfile profile, CharacterProfile warrior)
        {
            if (warrior.Level >= ProfileRules.MaxCharacterLevel)
            {
                buyButton.Set("Max level reached", ButtonColor, false);
                buyReason.text = "Level " + ProfileRules.MaxCharacterLevel + " is the highest level.";
                return;
            }

            long cost = ProfileRules.LevelCost(warrior.Level);
            bool affordable = profile.Gold >= cost;
            buyButton.Set("Buy level " + (warrior.Level + 1) + " — " + MetaViewLogic.FormatGold(cost) + " gold", ButtonGo, affordable);
            buyReason.text = affordable
                ? "Each level gives 1 stat point in every loadout."
                : "Short by " + MetaViewLogic.FormatGold(cost - profile.Gold) + " gold. Watch the AI play or use GOLD FARM to earn more.";
            buyReason.color = affordable ? Muted : Warn;
        }

        private void RefreshLoadouts(CharacterProfile warrior, int active, int unspent)
        {
            for (int i = 0; i < loadoutTabs.Length; i++)
            {
                loadoutTabs[i].Set(MetaViewLogic.LoadoutName(warrior, i), i == active ? ButtonActive : ButtonColor, true);
            }

            if (!nameField.isFocused)
            {
                nameField.SetTextWithoutNotify(MetaViewLogic.LoadoutName(warrior, active));
            }

            Loadout loadout = warrior.Loadouts[active];
            bool anySpent = ProfileRules.SpentPoints(loadout) > 0;
            resetButton.Set(null, ButtonStop, anySpent);
            unspentText.text = "Unspent points: " + unspent;
            unspentText.color = unspent > 0 ? Gold : Muted;

            for (int i = 0; i < statRows.Length; i++)
            {
                StatRow row = statRows[i];
                int points = loadout != null && loadout.Points != null && i < loadout.Points.Length ? loadout.Points[i] : 0;
                int cap = StatInfo.Cap(row.Stat);
                row.Value.text = points + " / " + cap;
                row.Value.color = points > 0 ? Color.white : Muted;
                SetBar(row.Fill, StatBarWidth, cap > 0 ? (float)points / cap : 0f);
                row.Minus.Set(null, ButtonColor, points > 0);
                row.Plus.Set(null, ButtonGo, unspent > 0 && points < cap);
            }
        }

        private void RefreshTier(PlayerProfile profile)
        {
            int tier = Mathf.Clamp(profile.SelectedTier, 1, ProfileRules.MaxTier);
            tierLabel.text = "Tier " + tier;
            tierDown.Set(null, ButtonColor, tier > 1);
            tierUp.Set(null, ButtonColor, tier < profile.UnlockedTier);
            tierUnlocked.text = profile.UnlockedTier >= ProfileRules.MaxTier
                ? "All tiers unlocked"
                : "Unlocked up to tier " + profile.UnlockedTier + ". Win at tier " + profile.UnlockedTier + " to unlock the next.";
            tierGold.text = MetaViewLogic.TierGoldText(tier);
            tierRules.text = MetaViewLogic.TierRulesText(tier);
        }

        private void RefreshFocus(PlayerProfile profile)
        {
            TrainingFocusInfo.TryParse(profile.TrainingFocus, out TrainingFocus selected);
            for (int i = 0; i < focusButtons.Length; i++)
            {
                bool isSelected = (int)selected == i;
                focusButtons[i].Set(null, isSelected ? ButtonActive : ButtonColor, true);
                focusHints[i].color = isSelected ? Color.white : Muted;
                focusHints[i].fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private void RefreshShop(PlayerProfile profile)
        {
            foreach (ShopCard shop in shopCards)
            {
                if (shop == null)
                {
                    continue;
                }

                ClassShopState state = ClassViewLogic.ShopState(profile, shop.ClassId);
                shop.Name.color = state == ClassShopState.Selected ? Gold : Color.white;
                shop.Status.text = ClassViewLogic.ShopStatus(profile, shop.ClassId);
                shop.Status.color = state == ClassShopState.TooExpensive ? Warn
                    : state == ClassShopState.Selected ? Good : Muted;

                bool showBuy = ClassViewLogic.ShowsBuy(state);
                shop.Buy.SetVisible(showBuy);
                if (showBuy)
                {
                    shop.Buy.Set(ClassViewLogic.BuyLabel(shop.ClassId), ButtonGo, ClassViewLogic.CanBuy(state));
                }

                bool owned = state == ClassShopState.Selected || state == ClassShopState.Owned;
                shop.Select.SetVisible(owned);
                if (owned)
                {
                    shop.Select.Set(ClassViewLogic.SelectLabel(state), state == ClassShopState.Selected ? ButtonActive : ButtonGo,
                        ClassViewLogic.CanSelect(state));
                }
            }
        }

        // ------------------------------------------------------------------ actions

        private void OnBuyClass(string classId)
        {
            if (store != null && ProfileRules.TryBuyClass(store.Profile, classId))
            {
                UiSounds.Purchase();
                Commit();
            }
            else
            {
                UiSounds.Refused();
            }
        }

        private void OnSelectClass(string classId)
        {
            if (store != null && store.Profile.SelectedClassId != classId && ProfileRules.TrySelectClass(store.Profile, classId))
            {
                Commit();
            }
        }

        private void OnBuyLevel()
        {
            if (store != null && ProfileRules.TryBuyLevel(store.Profile, store.Selected))
            {
                UiSounds.Purchase();
                Commit();
            }
            else
            {
                UiSounds.Refused();
            }
        }

        private void OnSelectLoadout(int index)
        {
            if (store != null && ProfileRules.TrySetActiveLoadout(store.Selected, index))
            {
                Commit();
            }
        }

        private void OnRenameSubmitted(string name)
        {
            if (store == null)
            {
                return;
            }

            CharacterProfile warrior = store.Selected;
            if (warrior != null && ProfileRules.TryRename(warrior, warrior.ActiveLoadout, name))
            {
                Commit();
            }
            else
            {
                // Empty or invalid: show the saved name again.
                nameField.SetTextWithoutNotify(MetaViewLogic.LoadoutName(warrior, warrior != null ? warrior.ActiveLoadout : 0));
            }
        }

        private void OnResetPoints()
        {
            if (store == null)
            {
                return;
            }

            CharacterProfile warrior = store.Selected;
            ProfileRules.ResetPoints(warrior, warrior.ActiveLoadout);
            Commit();
        }

        private void OnAddPoint(StatId stat)
        {
            if (store != null && ProfileRules.TryAddPoint(store.Selected, store.Selected.ActiveLoadout, stat))
            {
                Commit();
            }
            else
            {
                UiSounds.Refused();
            }
        }

        private void OnRemovePoint(StatId stat)
        {
            if (store != null && ProfileRules.TryRemovePoint(store.Selected, store.Selected.ActiveLoadout, stat))
            {
                Commit();
            }
        }

        private void OnChangeTier(int direction)
        {
            if (store == null)
            {
                return;
            }

            PlayerProfile profile = store.Profile;
            int tier = Mathf.Clamp(profile.SelectedTier + direction, 1, Mathf.Clamp(profile.UnlockedTier, 1, ProfileRules.MaxTier));
            if (tier != profile.SelectedTier)
            {
                profile.SelectedTier = tier;
                Commit();
            }
        }

        private void OnSelectFocus(TrainingFocus focus)
        {
            if (store == null)
            {
                return;
            }

            string id = TrainingFocusInfo.Id(focus);
            if (store.Profile.TrainingFocus != id)
            {
                store.Profile.TrainingFocus = id;
                Commit();
            }
        }

        private void Commit()
        {
            saveFailed = !store.Save();
            ProfileChanged?.Invoke();
            Refresh();
        }

        private sealed class ShopCard
        {
            public string ClassId;
            public Text Name;
            public Text Status;
            public Text Style;
            public UiButton Buy;
            public UiButton Select;
        }

        private sealed class StatRow
        {
            public StatId Stat;
            public Text Name;
            public Text Effect;
            public Image Fill;
            public Text Value;
            public UiButton Minus;
            public UiButton Plus;
        }
    }
}
