using System;
using PersonalArena.Core.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace PersonalArena.View
{
    /// <summary>
    /// Build comparison (key V): one row per loadout from <see cref="ProfileRules.Summarize"/> with its points,
    /// runs, win rate, median survival, gold per minute and best time. The best value of each column is
    /// highlighted and the active loadout is marked; a row button makes that loadout the active one.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class LoadoutComparePanel : MetaPanel
    {
        private const int Columns = 7;
        private const float TableX = 34f;
        private const float TableTop = -150f;
        private const float RowHeight = 92f;

        private static readonly string[] Headings =
        {
            "Bộ", "Điểm chỉ số", "Trận", "Tỉ lệ thắng", "Sống (trung vị)", "Vàng / phút", "Kỷ lục"
        };

        private static readonly float[] ColumnX = { 0f, 260f, 700f, 820f, 960f, 1120f, 1260f };
        private static readonly float[] ColumnWidth = { 250f, 430f, 110f, 130f, 150f, 130f, 110f };

        private ProfileStore store;
        private readonly Row[] rows = new Row[ProfileRules.LoadoutCount];
        private Text summaryText;

        /// <summary>Raised after a row made its loadout the active one (saved).</summary>
        public event Action ProfileChanged;

        protected override string Title => "SO SÁNH BUILD";
        protected override Vector2 CardSize => new Vector2(1600f, 820f);

        public void Bind(ProfileStore profileStore)
        {
            store = profileStore;
            Refresh();
        }

        public void Refresh()
        {
            if (!IsBuilt || !IsOpen || store == null)
            {
                return;
            }

            CharacterProfile warrior = store.Selected;
            if (warrior == null)
            {
                return;
            }

            LoadoutSummary[] summaries = new LoadoutSummary[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                summaries[i] = ProfileRules.Summarize(warrior.Loadouts[i]);
            }

            int bestRuns = BestIndex(summaries, s => s.Runs);
            int bestWinRate = BestIndex(summaries, s => s.WinRate);
            int bestMedian = BestIndex(summaries, s => s.MedianSeconds);
            int bestGold = BestIndex(summaries, s => s.GoldPerMinute);
            int bestTime = BestIndex(summaries, s => s.BestSeconds);
            int played = 0;

            for (int i = 0; i < rows.Length; i++)
            {
                Row row = rows[i];
                LoadoutSummary summary = summaries[i];
                bool active = i == warrior.ActiveLoadout;
                Loadout loadout = warrior.Loadouts[i];
                row.Background.color = active ? new Color(0.13f, 0.2f, 0.33f, 1f) : Panel;
                row.Cells[0].text = MetaViewLogic.LoadoutName(warrior, i) + (active ? "\n(đang dùng)" : string.Empty);
                row.Cells[0].color = active ? Gold : Color.white;
                row.Cells[1].text = MetaViewLogic.CompactPoints(loadout != null ? loadout.Points : null, 4);
                row.Cells[1].color = Color.white;
                row.Use.Set(active ? "Đang dùng" : "Dùng bộ này", active ? ButtonActive : ButtonColor, !active);

                if (summary.Runs <= 0)
                {
                    row.Cells[2].text = "chưa có trận";
                    row.Cells[2].color = Muted;
                    for (int c = 3; c < Columns; c++)
                    {
                        row.Cells[c].text = "—";
                        row.Cells[c].color = Muted;
                    }
                    continue;
                }

                played++;
                SetCell(row.Cells[2], summary.Runs.ToString(), i == bestRuns);
                SetCell(row.Cells[3], MetaViewLogic.Percent(summary.WinRate) + "\n(" + summary.Wins + " thắng)", i == bestWinRate);
                SetCell(row.Cells[4], SurvivorViewLogic.FormatClock(summary.MedianSeconds), i == bestMedian);
                SetCell(row.Cells[5], MetaViewLogic.FormatDecimal(summary.GoldPerMinute, "0.0"), i == bestGold);
                SetCell(row.Cells[6], SurvivorViewLogic.FormatClock(summary.BestSeconds), i == bestTime);
            }

            summaryText.text = played >= 2
                ? "Ô vàng là bộ tốt nhất ở cột đó. Trung vị tính trên tối đa " + ProfileRules.RecentCap + " trận gần nhất của mỗi bộ."
                : "Cho AI chơi (xem hoặc FARM VÀNG) với nhiều bộ khác nhau để so sánh. Ô vàng là bộ tốt nhất ở cột đó.";
        }

        protected override void OnOpened()
        {
            Refresh();
        }

        protected override void BuildContent(RectTransform card)
        {
            Text intro = PlaceText("Intro", card, 18, TextAnchor.UpperLeft, Muted, TableX, -76f, 1530f, 26f);
            intro.text = "Mỗi bộ chỉ số ghi lại các trận AI đã chơi với nó (xem và farm). Đổi bộ trong NHÂN VẬT (C) hoặc bấm \"Dùng bộ này\".";

            for (int c = 0; c < Columns; c++)
            {
                Text heading = PlaceText("Heading " + c, card, 17, TextAnchor.MiddleLeft, Muted, TableX + ColumnX[c] + 12f, -112f, ColumnWidth[c], 30f, true);
                heading.text = Headings[c];
            }

            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = BuildRow(card, i, TableTop - i * (RowHeight + 8f));
            }

            summaryText = PlaceText("Summary", card, 17, TextAnchor.UpperLeft, Muted, TableX, -676f, 1530f, 60f, false, true);
            Text footer = PlaceText("Footer", card, 16, TextAnchor.UpperLeft, Muted, TableX, -752f, 1530f, 40f, false, true);
            footer.text = "Vàng / phút = tổng vàng chia tổng thời gian chơi. Kỷ lục = trận sống lâu nhất.";
        }

        private Row BuildRow(Transform card, int index, float y)
        {
            Row row = new Row();
            RectTransform background = CreateCard("Row " + (index + 1), card, TableX, y, 1530f, RowHeight);
            row.Background = background.GetComponent<Image>();
            for (int c = 0; c < Columns; c++)
            {
                int size = c == 0 ? 20 : c == 1 ? 16 : 19;
                Text cell = PlaceText("Cell " + c, background, size, TextAnchor.MiddleLeft, Color.white,
                    ColumnX[c] + 12f, 0f, ColumnWidth[c], RowHeight, c == 0, c == 1);
                row.Cells[c] = cell;
            }

            int loadout = index;
            row.Use = CreateButton("Use", background, "Dùng bộ này", 15, 1380f, -24f, 136f, 44f, () => OnUse(loadout));
            return row;
        }

        private static void SetCell(Text cell, string text, bool best)
        {
            cell.text = text;
            cell.color = best ? Gold : Color.white;
            cell.fontStyle = best ? FontStyle.Bold : FontStyle.Normal;
        }

        /// <summary>Index of the loadout with the highest value among loadouts with runs (−1 when fewer than two have runs).</summary>
        private static int BestIndex(LoadoutSummary[] summaries, Func<LoadoutSummary, float> value)
        {
            int best = -1;
            int played = 0;
            for (int i = 0; i < summaries.Length; i++)
            {
                if (summaries[i].Runs <= 0)
                {
                    continue;
                }

                played++;
                if (best < 0 || value(summaries[i]) > value(summaries[best]))
                {
                    best = i;
                }
            }

            return played >= 2 ? best : -1;
        }

        private void OnUse(int loadout)
        {
            if (store != null && ProfileRules.TrySetActiveLoadout(store.Selected, loadout))
            {
                store.Save();
                ProfileChanged?.Invoke();
                Refresh();
            }
        }

        private sealed class Row
        {
            public Image Background;
            public readonly Text[] Cells = new Text[Columns];
            public UiButton Use;
        }
    }
}
