using System;
using System.Collections.Generic;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.UI;

namespace PersonalArena.View
{
    /// <summary>
    /// The codex (K): every weapon, passive, evolution and skill as a grid of icons, for one class or for all.
    /// Clicking an icon shows what it does, its numbers at every level, its evolution recipe and its classes.
    /// What the hero of the run on screen owns is marked with its level.
    /// </summary>
    public sealed class CodexPanel : MetaPanel
    {
        private const int Columns = 8;
        private const int Rows = 6;
        private const float TileWidth = 118f;
        private const float TileHeight = 122f;
        private const float Left = 34f;
        private const float Top = -128f;
        private static readonly string[] TabNames = { "WEAPONS", "PASSIVES", "EVOLUTIONS", "SKILLS" };
        private static readonly Color OwnedColor = new Color(0.3f, 0.27f, 0.14f, 1f);

        private sealed class Tile
        {
            public UiButton Button;
            public Image Icon;
            public Text Owned;
        }

        private readonly Tile[] tiles = new Tile[Columns * Rows];
        private readonly UiButton[] tabButtons = new UiButton[TabNames.Length];
        private readonly List<int> items = new List<int>();
        private readonly List<CodexSkill> skills = new List<CodexSkill>();
        private UiButton[] classButtons;
        private Func<SurvivorSim> simSource;
        private CodexTab tab;
        private string classId;
        private int selected;
        private Text countText;
        private Image detailIcon;
        private Text detailName;
        private Text detailKind;
        private Text detailOwned;
        private Text detailBody;

        protected override string Title => "CODEX";
        protected override Vector2 CardSize => new Vector2(1660f, 940f);

        /// <summary>The run on screen: the codex opens on its class and marks what its hero owns.</summary>
        public void Bind(Func<SurvivorSim> source)
        {
            simSource = source;
        }

        /// <summary>Checks of the build: shows a page (0..3) with its <paramref name="entry"/>-th tile selected.</summary>
        public void ShowForChecks(int page, int entry)
        {
            tab = (CodexTab)Mathf.Clamp(page, 0, TabNames.Length - 1);
            selected = Mathf.Max(0, entry);
            if (IsBuilt)
            {
                Refresh();
            }
        }

        protected override void OnOpened()
        {
            SurvivorSim sim = simSource != null ? simSource() : null;
            if (sim != null && sim.Config.ClassDef != null)
            {
                classId = sim.Config.ClassDef.Id;
            }
            Refresh();
        }

        protected override void BuildContent(RectTransform card)
        {
            Text hint = PlaceText("Hint", card, 17, TextAnchor.UpperLeft, Muted, Left, -70f, 900f, 24f);
            hint.text = "Everything the AI can pick. Click an icon for its numbers, upgrades and evolution recipe.";

            const float gridWidth = Columns * TileWidth + 18f;
            const float gridHeight = Rows * TileHeight + 18f;
            const float tabWidth = 150f;
            // Pages on the left above the grid, the class filter on the right.
            for (int i = 0; i < TabNames.Length; i++)
            {
                int page = i;
                tabButtons[i] = CreateButton("Tab " + TabNames[i], card, TabNames[i], 15, Left + i * (tabWidth + 8f), Top, tabWidth, 38f, () => SelectTab(page));
            }
            string[] classIds = ClassViewLogic.ClassIds;
            classButtons = new UiButton[classIds.Length + 1];
            float classLeft = CardSize.x - Left - (classIds.Length + 1) * 118f + 8f;
            for (int i = 0; i <= classIds.Length; i++)
            {
                string id = i == 0 ? null : classIds[i - 1];
                string label = i == 0 ? "ALL" : ClassViewLogic.UpperName(id);
                classButtons[i] = CreateButton("Class " + label, card, label, 14, classLeft + i * 118f, Top, 110f, 38f, () => SelectClass(id));
            }

            RectTransform grid = CreateCard("Grid", card, Left, Top - 48f, gridWidth, gridHeight);
            for (int i = 0; i < tiles.Length; i++)
            {
                tiles[i] = CreateTile(grid, i);
            }
            countText = PlaceText("Count", card, 14, TextAnchor.UpperLeft, Muted, Left + 4f, Top - 52f - gridHeight, gridWidth, 20f);

            float detailLeft = Left + gridWidth + 16f;
            float detailWidth = CardSize.x - detailLeft - Left;
            RectTransform detail = CreateCard("Detail", card, detailLeft, Top - 48f, detailWidth, gridHeight);
            detailIcon = CreateImage("Icon", detail, Color.white, null, Image.Type.Simple).GetComponent<Image>();
            detailIcon.preserveAspect = true;
            Place(detailIcon.rectTransform, 20f, -20f, 84f, 84f);
            detailName = PlaceText("Name", detail, 26, TextAnchor.MiddleLeft, Color.white, 120f, -18f, detailWidth - 140f, 34f, true);
            detailName.resizeTextForBestFit = true;
            detailName.resizeTextMinSize = 16;
            detailName.resizeTextMaxSize = 26;
            detailName.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailName.verticalOverflow = VerticalWrapMode.Truncate;
            detailKind = PlaceText("Kind", detail, 14, TextAnchor.UpperLeft, Muted, 120f, -56f, detailWidth - 140f, 20f);
            detailOwned = PlaceText("Owned", detail, 15, TextAnchor.UpperLeft, Gold, 120f, -80f, detailWidth - 140f, 22f, true);
            RectTransform rule = CreateImage("Rule", detail, new Color(1f, 1f, 1f, 0.08f), null, Image.Type.Simple);
            Place(rule, 20f, -118f, detailWidth - 40f, 2f);
            detailBody = PlaceText("Body", detail, 16, TextAnchor.UpperLeft, new Color(0.88f, 0.9f, 0.96f), 20f, -134f, detailWidth - 40f, gridHeight - 150f, false, true);
            detailBody.lineSpacing = 1.18f;
            detailBody.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private Tile CreateTile(RectTransform grid, int index)
        {
            Tile tile = new Tile();
            int column = index % Columns;
            int row = index / Columns;
            tile.Button = CreateButton("Tile " + index, grid, string.Empty, 12, 12f + column * TileWidth, -12f - row * TileHeight,
                TileWidth - 6f, TileHeight - 6f, () => Select(index));
            RectTransform root = (RectTransform)tile.Button.Button.transform;

            // The button's label becomes the name under the icon.
            Text name = tile.Button.Label;
            name.alignment = TextAnchor.UpperCenter;
            name.fontStyle = FontStyle.Normal;
            name.lineSpacing = 0.9f;
            SetRect(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 4f), new Vector2(-8f, 32f), new Vector2(0.5f, 0f));

            tile.Icon = CreateImage("Icon", root, Color.white, null, Image.Type.Simple).GetComponent<Image>();
            tile.Icon.preserveAspect = true;
            SetRect(tile.Icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(66f, 66f), new Vector2(0.5f, 1f));

            tile.Owned = CreateText("Owned", root, 12, TextAnchor.UpperRight, Gold);
            tile.Owned.fontStyle = FontStyle.Bold;
            SetRect(tile.Owned.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -6f), new Vector2(50f, 16f), new Vector2(1f, 1f));
            return tile;
        }

        private void SelectTab(int page)
        {
            tab = (CodexTab)page;
            selected = 0;
            Refresh();
        }

        private void SelectClass(string id)
        {
            classId = id;
            selected = 0;
            Refresh();
        }

        private void Select(int index)
        {
            selected = index;
            Refresh();
        }

        private void Refresh()
        {
            SurvivorSim sim = simSource != null ? simSource() : null;
            SurvivorInventory inventory = sim != null ? sim.Inventory : null;
            for (int i = 0; i < tabButtons.Length; i++)
            {
                tabButtons[i].Set(null, (int)tab == i ? ButtonActive : ButtonColor, true);
            }
            for (int i = 0; i < classButtons.Length; i++)
            {
                string id = i == 0 ? null : ClassViewLogic.ClassIds[i - 1];
                classButtons[i].Set(null, id == classId ? ButtonActive : ButtonColor, true);
            }

            items.Clear();
            skills.Clear();
            if (tab == CodexTab.Skills)
            {
                skills.AddRange(CodexLogic.Skills(classId));
            }
            else
            {
                items.AddRange(CodexLogic.Items(tab, classId));
            }
            int count = tab == CodexTab.Skills ? skills.Count : items.Count;
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, count - 1));

            int owned = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                Tile tile = tiles[i];
                bool shown = i < count;
                tile.Button.SetVisible(shown);
                if (!shown)
                {
                    continue;
                }

                int level = 0;
                if (tab == CodexTab.Skills)
                {
                    CodexSkill entry = skills[i];
                    tile.Icon.sprite = SkillIconFactory.IconFor(entry.Skill, entry.Slot);
                    tile.Button.Set(SurvivorViewLogic.SkillTitle(entry.Skill), i == selected ? ButtonActive : ButtonColor, true);
                }
                else
                {
                    ItemDef def = SurvivorCatalog.Get(items[i]);
                    level = inventory != null ? inventory.Level(items[i]) : 0;
                    tile.Icon.sprite = SkillIconFactory.IconForItem(items[i]);
                    tile.Button.Set(def.Name, i == selected ? ButtonActive : level > 0 ? OwnedColor : ButtonColor, true);
                }
                owned += level > 0 ? 1 : 0;
                tile.Owned.text = OwnedLabel(tab == CodexTab.Skills ? null : SurvivorCatalog.Get(items[i]), level);
            }

            countText.text = count + (count == 1 ? " entry" : " entries") + (owned > 0 ? "   ·   the hero on screen owns " + owned : string.Empty);
            FillDetail(count, inventory);
        }

        private void FillDetail(int count, SurvivorInventory inventory)
        {
            bool any = count > 0;
            detailIcon.enabled = any;
            if (!any)
            {
                detailName.text = string.Empty;
                detailKind.text = string.Empty;
                detailOwned.text = string.Empty;
                detailBody.text = "Nothing on this page for this class.";
                return;
            }

            if (tab == CodexTab.Skills)
            {
                CodexSkill entry = skills[selected];
                detailIcon.sprite = SkillIconFactory.IconFor(entry.Skill, entry.Slot);
                detailName.text = SurvivorViewLogic.SkillTitle(entry.Skill);
                detailName.color = Color.Lerp(SkillIconFactory.ColorFor(entry.Skill, entry.Slot), Color.white, 0.35f);
                detailKind.text = "SKILL";
                detailOwned.text = string.Empty;
                detailBody.text = CodexLogic.Details(entry);
                return;
            }

            int item = items[selected];
            ItemDef def = SurvivorCatalog.Get(item);
            int level = inventory != null ? inventory.Level(item) : 0;
            detailIcon.sprite = SkillIconFactory.IconForItem(item);
            detailName.text = def.Name;
            detailName.color = Color.Lerp(SurvivorViewLogic.ItemColor(item), Color.white, 0.35f);
            detailKind.text = SurvivorViewLogic.ItemKindLabel(def) + (def.MaxLevel > 1 ? "   ·   " + def.MaxLevel + " levels" : string.Empty);
            detailOwned.text = level > 0 ? "The hero on screen owns it: " + OwnedLabel(def, level) : string.Empty;
            detailBody.text = CodexLogic.Details(item);
        }

        private static string OwnedLabel(ItemDef def, int level)
        {
            if (def == null || level <= 0)
            {
                return string.Empty;
            }
            return def.MaxLevel <= 1 ? "OWNED" : level >= def.MaxLevel ? "MAX" : "Lv " + level;
        }
    }
}
