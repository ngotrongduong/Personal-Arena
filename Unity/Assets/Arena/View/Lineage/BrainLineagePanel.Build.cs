using System;
using System.Collections.Generic;
using System.Text;
using PersonalArena.Core.Meta;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PersonalArena.View
{
    /// <summary>Builds the panel: the lists and their rows, the rename box and the compare table.</summary>
    public sealed partial class BrainLineagePanel
    {
        // ------------------------------------------------------------------ build

        protected override void BuildContent(RectTransform card)
        {
            statusText = PlaceText("Status", card, 18, TextAnchor.UpperLeft, Muted, 34f, -74f, CardSize.x - 68f, 28f);
            statusText.supportRichText = false;

            Text branchesTitle = PlaceText("Branches Title", card, 22, TextAnchor.UpperLeft, Color.white, LeftX, -110f, LeftWidth, 30f, true);
            branchesTitle.text = "NHÁNH";
            branchContent = CreateScrollList("Branches", card, LeftX, ListTop, LeftWidth, BranchListHeight);
            branchesEmpty = PlaceText("Branches Empty", card, 17, TextAnchor.UpperLeft, Muted, LeftX + 14f, ListTop - 14f,
                LeftWidth - 40f, 80f, false, true);
            branchesEmpty.gameObject.SetActive(false);
            BuildRenameArea(card);

            versionsHeader = PlaceText("Versions Title", card, 22, TextAnchor.UpperLeft, Color.white, RightX, -110f, RightWidth, 30f, true);
            versionsHeader.supportRichText = false;
            versionContent = CreateScrollList("Versions", card, RightX, ListTop, RightWidth, VersionListHeight);
            versionsEmpty = PlaceText("Versions Empty", card, 17, TextAnchor.UpperLeft, Muted, RightX + 14f, ListTop - 14f,
                RightWidth - 40f, 60f, false, true);
            versionsEmpty.gameObject.SetActive(false);

            BuildCompare(card, RightX, ListTop - VersionListHeight - 14f, RightWidth, 396f);
        }

        /// <summary>A vertical scroll list (mouse wheel, drag, scrollbar); returns the content rect rows go into.</summary>
        private RectTransform CreateScrollList(string objectName, Transform parent, float x, float y, float width, float height)
        {
            GameObject root = CreateUiObject(objectName, parent);
            RectTransform rootRect = (RectTransform)root.transform;
            Place(rootRect, x, y, width, height);
            ScrollRect scroll = root.AddComponent<ScrollRect>();

            RectTransform viewport = CreateImage("Viewport", rootRect, new Color(0f, 0f, 0f, 0f), null, Image.Type.Simple);
            viewport.GetComponent<Image>().raycastTarget = true;
            SetStretch(viewport, 0f, ScrollbarWidth + ScrollbarGap, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = (RectTransform)CreateUiObject("Content", viewport).transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            RectTransform barRect = CreateImage("Scrollbar", rootRect, Track, UiSprites.RoundedSprite(), Image.Type.Sliced);
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 1f);
            barRect.anchoredPosition = Vector2.zero;
            barRect.sizeDelta = new Vector2(ScrollbarWidth, 0f);
            barRect.GetComponent<Image>().raycastTarget = true;
            RectTransform slidingArea = (RectTransform)CreateUiObject("Sliding Area", barRect).transform;
            SetStretch(slidingArea, 0f, 0f, 0f, 0f);
            RectTransform handle = CreateImage("Handle", slidingArea, Muted, UiSprites.RoundedSprite(), Image.Type.Sliced);
            SetStretch(handle, 0f, 0f, 0f, 0f);
            Image handleImage = handle.GetComponent<Image>();
            handleImage.raycastTarget = true;
            Scrollbar scrollbar = barRect.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 20f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        private void EnsureBranchRows(int count)
        {
            float width = LeftWidth - ScrollbarWidth - ScrollbarGap;
            while (branchRows.Count < count)
            {
                int i = branchRows.Count;
                BranchRow row = new BranchRow();
                RectTransform background = CreateCard("Branch " + (i + 1), branchContent, 0f, -i * (BranchRowHeight + RowGap), width, BranchRowHeight);
                row.Root = background.gameObject;
                row.Background = background.GetComponent<Image>();
                row.Background.raycastTarget = true;
                Button select = background.gameObject.AddComponent<Button>();
                select.transition = Selectable.Transition.None;
                select.targetGraphic = row.Background;
                select.onClick.AddListener(() => OnSelectBranch(row));

                row.Name = PlaceText("Name", background, 20, TextAnchor.UpperLeft, Color.white, 14f, -10f, width - 28f, 26f, true);
                row.Name.supportRichText = false;
                row.Badges = PlaceText("Badges", background, 15, TextAnchor.UpperLeft, Color.white, 14f, -38f, width - 28f, 22f);
                row.Info = PlaceText("Info", background, 15, TextAnchor.UpperLeft, Muted, 14f, -62f, width - 28f, 22f);
                row.Info.supportRichText = false;
                row.Parent = PlaceText("Parent", background, 15, TextAnchor.UpperLeft, Muted, 14f, -84f, width - 28f, 22f);
                row.Parent.supportRichText = false;

                float buttonWidth = (width - 28f - 3f * 6f) / 4f;
                row.Use = CreateButton("Use", background, "Dùng nhánh này", 13, 14f, -114f, buttonWidth, 42f, () => OnUseBranch(row));
                row.Rename = CreateButton("Rename", background, "Đổi tên", 13, 14f + (buttonWidth + 6f), -114f, buttonWidth, 42f, () => OnRenameBranch(row));
                row.Clone = CreateButton("Clone", background, "Nhân bản", 13, 14f + 2f * (buttonWidth + 6f), -114f, buttonWidth, 42f, () => OnCloneBranch(row));
                row.Snapshot = CreateButton("Snapshot", background, "Lưu phiên bản hiện tại", 13, 14f + 3f * (buttonWidth + 6f), -114f,
                    buttonWidth, 42f, () => OnSnapshotBranch(row));
                branchRows.Add(row);
            }
        }

        private void EnsureVersionRows(int count)
        {
            float width = RightWidth - ScrollbarWidth - ScrollbarGap;
            const float buttonWidth = 96f;
            const float buttonGap = 6f;
            float buttonsX = width - 14f - (5f * buttonWidth + 4f * buttonGap);
            while (versionRows.Count < count)
            {
                int i = versionRows.Count;
                VersionRow row = new VersionRow();
                RectTransform background = CreateCard("Version " + (i + 1), versionContent, 0f, -i * (VersionRowHeight + RowGap), width, VersionRowHeight);
                row.Root = background.gameObject;
                row.Background = background.GetComponent<Image>();

                float textWidth = buttonsX - 28f;
                row.Name = PlaceText("Name", background, 19, TextAnchor.UpperLeft, Color.white, 14f, -8f, textWidth, 26f, true);
                row.Name.supportRichText = false;
                row.Badges = PlaceText("Badges", background, 15, TextAnchor.UpperLeft, Color.white, 14f, -36f, textWidth, 22f);
                row.Stats = PlaceText("Stats", background, 15, TextAnchor.UpperLeft, Muted, 14f, -60f, textWidth, 40f, false, true);
                row.Stats.supportRichText = false;

                row.Watch = CreateButton("Watch", background, "Xem ngay", 14, buttonsX, -12f, buttonWidth, 40f, () => OnWatchVersion(row));
                row.Compare = CreateButton("Compare", background, "So sánh", 14, buttonsX + (buttonWidth + buttonGap), -12f, buttonWidth, 40f,
                    () => OnCompareVersion(row));
                row.Pin = CreateButton("Pin", background, "Ghim", 14, buttonsX + 2f * (buttonWidth + buttonGap), -12f, buttonWidth, 40f,
                    () => OnPinVersion(row));
                row.Rename = CreateButton("Rename", background, "Đổi tên", 14, buttonsX + 3f * (buttonWidth + buttonGap), -12f, buttonWidth, 40f,
                    () => OnRenameVersion(row));
                row.Fork = CreateButton("Fork", background, "Rẽ nhánh", 14, buttonsX + 4f * (buttonWidth + buttonGap), -12f, buttonWidth, 40f,
                    () => OnForkVersion(row));
                row.Reason = PlaceText("Reason", background, 13, TextAnchor.UpperRight, Muted, buttonsX, -58f,
                    5f * buttonWidth + 4f * buttonGap, 40f, false, true);
                row.Reason.supportRichText = false;
                versionRows.Add(row);
            }
        }

        private void BuildRenameArea(RectTransform card)
        {
            renameArea = CreateUiObject("Rename", card);
            RectTransform area = (RectTransform)renameArea.transform;
            Place(area, LeftX, ListTop - BranchListHeight - 8f, LeftWidth, 92f);

            renameLabel = PlaceText("Label", area, 15, TextAnchor.UpperLeft, Warn, 0f, 0f, LeftWidth, 40f, false, true);
            renameLabel.supportRichText = false;

            RectTransform rect = CreateImage("Field", area, Track, UiSprites.RoundedSprite(), Image.Type.Sliced);
            Place(rect, 0f, -44f, LeftWidth, 44f);
            Image background = rect.GetComponent<Image>();
            background.raycastTarget = true;

            Text text = CreateText("Text", rect, 18, TextAnchor.MiddleLeft, Color.white);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            SetStretch(text.rectTransform, 10f, 10f, 2f, 2f);

            Text placeholder = CreateText("Placeholder", rect, 17, TextAnchor.MiddleLeft, TextDisabled);
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.text = "Tên mới (tối đa " + LineageStore.MaxNameLength + " ký tự)";
            placeholder.horizontalOverflow = HorizontalWrapMode.Wrap;
            placeholder.verticalOverflow = VerticalWrapMode.Truncate;
            SetStretch(placeholder.rectTransform, 10f, 10f, 2f, 2f);

            renameField = rect.gameObject.AddComponent<InputField>();
            renameField.targetGraphic = background;
            renameField.textComponent = text;
            renameField.placeholder = placeholder;
            renameField.characterLimit = LineageStore.MaxNameLength;
            renameField.lineType = InputField.LineType.SingleLine;
            renameField.onEndEdit.AddListener(OnRenameEnded);
            renameArea.SetActive(false);
        }

        private void BuildCompare(RectTransform card, float x, float y, float width, float height)
        {
            RectTransform section = CreateCard("Compare", card, x, y, width, height);
            Header(section, "SO SÁNH HAI PHIÊN BẢN");

            compareHint = PlaceText("Hint", section, 17, TextAnchor.UpperLeft, Muted, 18f, -56f, width - 36f, 80f, false, true);
            compareHint.text = "Bấm \"So sánh\" ở hai phiên bản (có thể ở hai nhánh khác nhau) để so sánh chúng. " +
                "Bấm lần thứ ba sẽ thay cho lựa chọn cũ hơn.";

            compareBody = CreateUiObject("Body", section);
            RectTransform body = (RectTransform)compareBody.transform;
            SetStretch(body, 0f, 0f, 0f, 0f);

            compareNameA = PlaceText("Name A", body, 17, TextAnchor.UpperLeft, ColorA, 18f, -46f, width / 2f - 28f, 24f, true);
            compareNameA.supportRichText = false;
            compareNameB = PlaceText("Name B", body, 17, TextAnchor.UpperLeft, ColorB, width / 2f + 10f, -46f, width / 2f - 28f, 24f, true);
            compareNameB.supportRichText = false;

            const float labelX = 18f;
            const float valueAX = 250f;
            const float valueBX = 400f;
            Text metricHeading = PlaceText("Metric Heading", body, 15, TextAnchor.MiddleLeft, Muted, labelX, -78f, 220f, 24f, true);
            metricHeading.text = "Chỉ số (ô vàng: tốt hơn)";
            Text headingA = PlaceText("Heading A", body, 15, TextAnchor.MiddleLeft, ColorA, valueAX, -78f, 140f, 24f, true);
            headingA.text = "A";
            Text headingB = PlaceText("Heading B", body, 15, TextAnchor.MiddleLeft, ColorB, valueBX, -78f, 140f, 24f, true);
            headingB.text = "B";

            LineageMetric[] metrics = LineageCompare.Metrics;
            for (int i = 0; i < metrics.Length; i++)
            {
                float rowY = -106f - i * 29f;
                MetricRow row = new MetricRow { Metric = metrics[i] };
                Text label = PlaceText("Metric " + i, body, 16, TextAnchor.MiddleLeft, Color.white, labelX, rowY, 230f, 28f);
                label.text = LineageCompare.Label(metrics[i]) + (LineageCompare.LowerIsBetter(metrics[i]) ? " (thấp tốt hơn)" : string.Empty);
                row.ValueA = PlaceText("A " + i, body, 16, TextAnchor.MiddleLeft, Color.white, valueAX, rowY, 145f, 28f);
                row.ValueB = PlaceText("B " + i, body, 16, TextAnchor.MiddleLeft, Color.white, valueBX, rowY, 145f, 28f);
                metricRows.Add(row);
            }

            const float traitX = 580f;
            Text traitHeading = PlaceText("Trait Heading", body, 15, TextAnchor.MiddleLeft, Muted, traitX, -78f, 540f, 24f, true);
            traitHeading.text = "Cách chơi (thanh trên: A, thanh dưới: B)";
            for (int i = 0; i < BehaviorProfilePanel.TraitCount; i++)
            {
                float rowY = -106f - i * 36f;
                TraitCompareRow row = new TraitCompareRow();
                Text name = PlaceText("Trait " + i, body, 15, TextAnchor.MiddleLeft, Color.white, traitX, rowY, 190f, 30f);
                name.text = BehaviorProfilePanel.TraitName(i);
                CreateBar("Bar A " + i, body, traitX + 195f, rowY - 3f, TraitBarWidth, 11f, ColorA, out row.FillA);
                CreateBar("Bar B " + i, body, traitX + 195f, rowY - 17f, TraitBarWidth, 11f, ColorB, out row.FillB);
                row.Value = PlaceText("Value " + i, body, 14, TextAnchor.MiddleLeft, Muted, traitX + 452f, rowY, width - traitX - 452f - 12f, 30f);
                traitRows.Add(row);
            }

            compareBody.SetActive(false);
        }

        // ------------------------------------------------------------------ rows

        private abstract class ListRow
        {
            public GameObject Root;
            public Image Background;
        }

        private sealed class BranchRow : ListRow
        {
            public LineageBranch Branch;
            public Text Name;
            public Text Badges;
            public Text Info;
            public Text Parent;
            public UiButton Use;
            public UiButton Rename;
            public UiButton Clone;
            public UiButton Snapshot;
        }

        private sealed class VersionRow : ListRow
        {
            public LineageVersion Version;
            public Text Name;
            public Text Badges;
            public Text Stats;
            public Text Reason;
            public UiButton Watch;
            public UiButton Compare;
            public UiButton Pin;
            public UiButton Rename;
            public UiButton Fork;
        }

        private sealed class MetricRow
        {
            public LineageMetric Metric;
            public Text ValueA;
            public Text ValueB;
        }

        private sealed class TraitCompareRow
        {
            public Image FillA;
            public Image FillB;
            public Text Value;
        }
    }
}
