using System;
using System.Collections.Generic;
using System.Text;
using PersonalArena.Core.Meta;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PersonalArena.View
{
    /// <summary>
    /// Brain lineage panel "Lịch sử não" (key L, M6): branches on the left, the saved versions of the selected
    /// branch on the right, and a compare view of two versions at the bottom. Watch, rename, pin, fork and clone
    /// from here. The Python side (Trainer/brain_lineage.py) runs through <see cref="LineageCommand"/>; the viewer
    /// only writes labels.json and the profile's active branch.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed partial class BrainLineagePanel : MetaPanel
    {
        private const float LeftX = 30f;
        private const float LeftWidth = 560f;
        private const float RightX = 610f;
        private const float RightWidth = 1160f;
        private const float ListTop = -146f;
        private const float BranchListHeight = 734f;
        private const float VersionListHeight = 414f;
        private const float ScrollbarWidth = 12f;
        private const float ScrollbarGap = 6f;
        private const float BranchRowHeight = 166f;
        private const float VersionRowHeight = 104f;
        private const float RowGap = 8f;
        private const float RefreshSeconds = 1f;
        private const float ReloadSeconds = 15f;
        private const float TraitBarWidth = 250f;
        private const string SyncKind = "sync";
        private const string SnapshotKind = "snapshot";
        private const string ForkKind = "fork";

        private static readonly Color SelectedRow = new Color(0.13f, 0.2f, 0.33f, 1f);
        private static readonly Color ColorA = new Color(0.4f, 0.72f, 1f, 1f);
        private static readonly Color ColorB = new Color(1f, 0.62f, 0.3f, 1f);

        private enum RenameKind
        {
            None,
            Branch,
            Version
        }

        private ProfileStore store;
        private string runsDirectory;
        private string behavior = "Warrior";
        private Func<TrainingSnapshot> trainingSnapshot;
        private LineageCommand command;
        private string commandMissing;
        private LineageIndex index;
        private string selectedRunId;
        private readonly string[] comparePicks = new string[2];
        private int newestPick = -1;
        private string message;
        private Color messageColor = Color.white;
        private RenameKind renameKind;
        private string renameId;
        private bool hideRenameNextFrame;
        private float nextRefresh;
        private float nextReload;

        private Text statusText;
        private Text versionsHeader;
        private Text branchesEmpty;
        private Text versionsEmpty;
        private RectTransform branchContent;
        private RectTransform versionContent;
        private readonly List<BranchRow> branchRows = new List<BranchRow>();
        private readonly List<VersionRow> versionRows = new List<VersionRow>();
        private GameObject renameArea;
        private Text renameLabel;
        private InputField renameField;
        private GameObject compareBody;
        private Text compareHint;
        private Text compareNameA;
        private Text compareNameB;
        private readonly List<MetricRow> metricRows = new List<MetricRow>();
        private readonly List<TraitCompareRow> traitRows = new List<TraitCompareRow>();

        /// <summary>Raised after the active branch (profile BrainRunId) changed and was saved.</summary>
        public event Action ProfileChanged;

        /// <summary>"Xem ngay": the controller should watch this version (version, branch display name).</summary>
        public event Action<LineageVersion, string> WatchVersionRequested;

        /// <summary>Bound to a runs folder (the panel can only open then).</summary>
        public bool IsBound => store != null && !string.IsNullOrEmpty(runsDirectory);

        protected override string Title => "LỊCH SỬ NÃO";
        protected override Vector2 CardSize => new Vector2(1800f, 1000f);

        public void Bind(ProfileStore profileStore, string runs, string behaviorName, Func<TrainingSnapshot> training)
        {
            store = profileStore;
            runsDirectory = runs;
            string newBehavior = string.IsNullOrEmpty(behaviorName) ? "Warrior" : behaviorName;
            if (!string.Equals(newBehavior, behavior, StringComparison.Ordinal))
            {
                // M7 class switch: another class has its own branches; forget this one's picks.
                selectedRunId = null;
                comparePicks[0] = null;
                comparePicks[1] = null;
                newestPick = -1;
                message = null;
            }

            behavior = newBehavior;
            trainingSnapshot = training;
            command = null;
            if (!string.IsNullOrEmpty(runs))
            {
                try
                {
                    command = new LineageCommand(runs, behavior);
                }
                catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException ||
                    exception is System.IO.IOException || exception is System.Security.SecurityException)
                {
                    command = null;
                }
            }

            if (IsBuilt && IsOpen)
            {
                OnOpened();
            }
        }

        protected override void OnOpened()
        {
            CancelRename();
            hideRenameNextFrame = false;
            if (renameArea != null)
            {
                renameArea.SetActive(false);
            }

            message = null;
            Reload();
            string active = ActiveRunId();
            selectedRunId = index.FindBranch(active) != null
                ? index.FindBranch(active).RunId
                : index.Branches.Count > 0 ? index.Branches[0].RunId : null;
            ScrollToTop(branchContent);
            ScrollToTop(versionContent);
            Refresh();
            StartSync();
            nextRefresh = Time.unscaledTime + RefreshSeconds;
            nextReload = Time.unscaledTime + ReloadSeconds;
        }

        protected override void Update()
        {
            base.Update();

            if (command != null && command.Poll(out LineageResult result, out string kind, out object tag))
            {
                HandleResult(kind, result, tag);
            }

            if (hideRenameNextFrame)
            {
                hideRenameNextFrame = false;
                if (renameKind == RenameKind.None && renameArea != null)
                {
                    renameArea.SetActive(false);
                }
            }

            if (IsOpen && IsBuilt && Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + RefreshSeconds;
                if (Time.unscaledTime >= nextReload && renameKind == RenameKind.None)
                {
                    // Training keeps saving evaluated versions: re-read the store now and then.
                    nextReload = Time.unscaledTime + ReloadSeconds;
                    Reload();
                }

                Refresh();
            }
        }

        // ------------------------------------------------------------------ model

        private void Reload()
        {
            index = IsBound ? LineageStore.Load(runsDirectory, behavior) : new LineageIndex();
            commandMissing = command == null ? "Không tìm thấy thư mục huấn luyện." : command.MissingPiece();

            if (index.FindBranch(selectedRunId) == null)
            {
                string active = ActiveRunId();
                selectedRunId = index.FindBranch(active) != null
                    ? index.FindBranch(active).RunId
                    : index.Branches.Count > 0 ? index.Branches[0].RunId : null;
            }

            for (int i = 0; i < comparePicks.Length; i++)
            {
                if (comparePicks[i] != null && index.FindVersion(comparePicks[i]) == null)
                {
                    comparePicks[i] = null;
                }
            }

            if (newestPick >= 0 && comparePicks[newestPick] == null)
            {
                newestPick = comparePicks[1 - newestPick] != null ? 1 - newestPick : -1;
            }
        }

        private string ActiveRunId()
        {
            CharacterProfile character = PanelCharacter();
            return character != null ? character.BrainRunId : null;
        }

        /// <summary>The character whose branches this panel shows (the class of <see cref="behavior"/>).</summary>
        private CharacterProfile PanelCharacter()
        {
            if (store == null || store.Profile == null)
            {
                return null;
            }

            return ProfileRules.FindCharacter(store.Profile, ClassViewLogic.ClassIdOfBehavior(behavior)) ?? store.Selected;
        }

        /// <summary>The run the training service is training now, or null.</summary>
        private string TrainingRunId()
        {
            if (trainingSnapshot == null)
            {
                return null;
            }

            TrainingSnapshot snapshot = trainingSnapshot();
            bool sameClass = snapshot.Status != null &&
                !ClassViewLogic.IsOtherClass(ClassViewLogic.StatusBehavior(true, snapshot.Status.behavior), behavior);
            return snapshot.IsActive && sameClass && !string.IsNullOrEmpty(snapshot.Status.run_id)
                ? snapshot.Status.run_id
                : null;
        }

        private bool IsBusy => command != null && command.IsRunning;

        private void SetMessage(string text, Color color)
        {
            message = text;
            messageColor = color;
            RefreshStatus();
        }

        // ------------------------------------------------------------------ refresh

        private void Refresh()
        {
            if (!IsBuilt)
            {
                return;
            }

            RefreshStatus();
            if (!IsBound || index == null)
            {
                SetRowCount(branchRows, 0);
                SetRowCount(versionRows, 0);
                branchesEmpty.text = "Không tìm thấy thư mục huấn luyện (Trainer/runs).";
                branchesEmpty.gameObject.SetActive(true);
                versionsEmpty.gameObject.SetActive(false);
                versionsHeader.text = "PHIÊN BẢN";
                RefreshCompare();
                return;
            }

            bool commandsBlocked = IsBusy || commandMissing != null;
            RefreshBranches(ActiveRunId(), TrainingRunId(), commandsBlocked);
            RefreshVersions(commandsBlocked);
            RefreshCompare();
        }

        private void RefreshStatus()
        {
            if (statusText == null)
            {
                return;
            }

            if (IsBusy)
            {
                statusText.text = command.BusyText + "  (đã chạy " + BehaviorProfilePanel.FormatSeconds((float)command.ElapsedSeconds) + ")";
                statusText.color = Warn;
            }
            else if (!string.IsNullOrEmpty(message))
            {
                statusText.text = message;
                statusText.color = messageColor;
            }
            else if (commandMissing != null)
            {
                statusText.text = "Chỉ xem được: " + commandMissing + " Cần nó để lưu phiên bản, rẽ nhánh và nhân bản.";
                statusText.color = Muted;
            }
            else
            {
                statusText.text = "Mỗi phiên bản là một bộ não đã lưu. \"Xem ngay\" để xem nó chơi, \"Rẽ nhánh\" để huấn luyện tiếp theo hướng khác.";
                statusText.color = Muted;
            }
        }

        private void RefreshBranches(string activeRunId, string trainingRunId, bool commandsBlocked)
        {
            List<LineageBranch> branches = index.Branches;
            EnsureBranchRows(branches.Count);
            SetRowCount(branchRows, branches.Count);
            for (int i = 0; i < branches.Count; i++)
            {
                LineageBranch branch = branches[i];
                BranchRow row = branchRows[i];
                row.Branch = branch;
                bool selected = string.Equals(branch.RunId, selectedRunId, StringComparison.OrdinalIgnoreCase);
                bool active = string.Equals(branch.RunId, activeRunId, StringComparison.OrdinalIgnoreCase);
                bool training = string.Equals(branch.RunId, trainingRunId, StringComparison.OrdinalIgnoreCase);

                row.Background.color = selected ? SelectedRow : Panel;
                row.Name.text = branch.Name;
                row.Name.color = active ? Gold : Color.white;

                StringBuilder badges = new StringBuilder();
                if (active)
                {
                    AppendBadge(badges, "ĐANG DÙNG", Good);
                }
                if (training)
                {
                    AppendBadge(badges, "ĐANG HỌC", Warn);
                }
                if (selected)
                {
                    AppendBadge(badges, "ĐANG CHỌN", ColorA);
                }
                if (!branch.OnDisk)
                {
                    AppendBadge(badges, "chỉ còn phiên bản đã lưu", Muted);
                }
                row.Badges.text = badges.ToString();

                int versionCount = index.VersionsOf(branch.RunId).Count;
                string step = branch.CurrentStep > 0 ? LineageStore.DefaultVersionName(branch.CurrentStep) : "chưa học bước nào";
                row.Info.text = branch.RunId + " · " + step + " · " + versionCount + " phiên bản" +
                    (branch.OnDisk && !branch.HasCheckpoint ? " · chưa có checkpoint" : string.Empty);
                row.Parent.text = branch.HasParent
                    ? "rẽ từ " + index.BranchName(branch.ParentRun) + " · bước " + LineageStore.FormatStep(branch.ParentStep)
                    : "nhánh gốc";

                row.Use.Set(active ? "Đang dùng" : "Dùng nhánh này", active ? ButtonActive : ButtonGo, !active && branch.OnDisk);
                row.Rename.Set("Đổi tên", ButtonColor, true);
                row.Clone.Set("Nhân bản", ButtonColor, branch.OnDisk && !commandsBlocked);
                row.Snapshot.Set("Lưu phiên bản hiện tại", ButtonColor, branch.OnDisk && !commandsBlocked);
            }

            SetContentHeight(branchContent, branches.Count, BranchRowHeight);
            branchesEmpty.text = "Chưa có nhánh nào. Bấm HUẤN LUYỆN để bắt đầu nhánh đầu tiên.";
            branchesEmpty.gameObject.SetActive(branches.Count == 0);
        }

        private void RefreshVersions(bool commandsBlocked)
        {
            LineageBranch branch = index.FindBranch(selectedRunId);
            versionsHeader.text = branch == null ? "PHIÊN BẢN" : "PHIÊN BẢN CỦA \"" + branch.Name + "\"  (mới nhất ở trên)";
            List<LineageVersion> versions = branch == null ? new List<LineageVersion>() : index.VersionsOf(branch.RunId);
            EnsureVersionRows(versions.Count);
            SetRowCount(versionRows, versions.Count);

            for (int i = 0; i < versions.Count; i++)
            {
                LineageVersion version = versions[i];
                VersionRow row = versionRows[i];
                row.Version = version;
                int pick = PickSlot(version.Id);
                row.Background.color = pick >= 0 ? new Color(0.11f, 0.15f, 0.22f, 1f) : Panel;
                row.Name.text = version.Name;
                row.Name.color = version.IsCurrentChampion ? Gold : Color.white;

                StringBuilder badges = new StringBuilder();
                if (version.IsCurrentChampion)
                {
                    AppendBadge(badges, "GIỎI NHẤT", Gold);
                }
                else if (version.WasChampion)
                {
                    AppendBadge(badges, "từng giỏi nhất", Good);
                }
                if (version.Pinned)
                {
                    AppendBadge(badges, "ĐÃ GHIM", ColorA);
                }
                if (!version.HasCheckpoint)
                {
                    AppendBadge(badges, "chỉ xem", Muted);
                }
                if (pick >= 0)
                {
                    AppendBadge(badges, pick == 0 ? "SO SÁNH A" : "SO SÁNH B", pick == 0 ? ColorA : ColorB);
                }
                row.Badges.text = badges.ToString();

                string date = LineageStore.FormatDate(version.CreatedAt);
                string stats = LineageStore.DefaultVersionName(version.Step) + (date.Length > 0 ? " · " + date : string.Empty);
                if (version.HasEvaluation)
                {
                    stats += " · Sống " + MetricText(LineageMetric.MedianSurvival, version) +
                        " · Thắng " + MetricText(LineageMetric.WinRate, version) +
                        " · " + MetricText(LineageMetric.GoldPerMinute, version) + " vàng/phút" +
                        " · Cấp TB " + MetricText(LineageMetric.MeanLevel, version);
                }
                else
                {
                    stats += " · chưa chấm";
                }
                row.Stats.text = stats;

                bool oldSchema = version.SchemaVersion > 0 && version.SchemaVersion != BrainLocator.CurrentSchemaVersion;
                row.Watch.Set("Xem ngay", ButtonGo, !oldSchema);
                row.Compare.Set(pick < 0 ? "So sánh" : (pick == 0 ? "Bỏ A" : "Bỏ B"), pick < 0 ? ButtonColor : ButtonActive, true);
                row.Pin.Set(version.Pinned ? "Bỏ ghim" : "Ghim", ButtonColor, true);
                row.Rename.Set("Đổi tên", ButtonColor, true);
                row.Fork.Set("Rẽ nhánh", ButtonColor, version.HasCheckpoint && !oldSchema && !commandsBlocked);

                if (oldSchema)
                {
                    row.Reason.text = "Não này dùng kiểu dữ liệu cũ, không xem hay học tiếp được.";
                }
                else if (!version.HasCheckpoint)
                {
                    row.Reason.text = "Bản này chỉ còn não để xem, không học tiếp được";
                }
                else
                {
                    row.Reason.text = string.Empty;
                }
            }

            SetContentHeight(versionContent, versions.Count, VersionRowHeight);
            versionsEmpty.text = branch == null
                ? "Chọn một nhánh bên trái."
                : "Nhánh này chưa có phiên bản nào. Bấm \"Lưu phiên bản hiện tại\" để lưu bộ não hiện tại của nhánh.";
            versionsEmpty.gameObject.SetActive(versions.Count == 0);
        }

        private void RefreshCompare()
        {
            LineageVersion a = index != null ? index.FindVersion(comparePicks[0]) : null;
            LineageVersion b = index != null ? index.FindVersion(comparePicks[1]) : null;
            bool show = a != null || b != null;
            compareBody.SetActive(show);
            compareHint.gameObject.SetActive(!show);
            if (!show)
            {
                return;
            }

            compareNameA.text = a != null
                ? "A: " + a.Name + " (" + index.BranchName(a.RunId) + ")"
                : "A: bấm \"So sánh\" ở một phiên bản";
            compareNameB.text = b != null
                ? "B: " + b.Name + " (" + index.BranchName(b.RunId) + ")"
                : "B: bấm \"So sánh\" ở một phiên bản khác";

            for (int i = 0; i < metricRows.Count; i++)
            {
                MetricRow row = metricRows[i];
                int better = LineageCompare.Better(row.Metric, a, b);
                SetMetricCell(row.ValueA, row.Metric, a, better == -1);
                SetMetricCell(row.ValueB, row.Metric, b, better == 1);
            }

            for (int i = 0; i < traitRows.Count; i++)
            {
                TraitCompareRow row = traitRows[i];
                float valueA = a != null && a.HasEvaluation ? BehaviorProfilePanel.TraitValue(i, a.Behavior) : -1f;
                float valueB = b != null && b.HasEvaluation ? BehaviorProfilePanel.TraitValue(i, b.Behavior) : -1f;
                SetBar(row.FillA, TraitBarWidth, Mathf.Max(0f, valueA));
                SetBar(row.FillB, TraitBarWidth, Mathf.Max(0f, valueB));
                row.Value.text = TraitText(a, valueA) + " / " + TraitText(b, valueB);
            }
        }

        private static string TraitText(LineageVersion version, float value)
        {
            if (version == null)
            {
                return "—";
            }

            if (!version.HasEvaluation)
            {
                return "chưa chấm";
            }

            return value < 0f ? "—" : LineageCompare.Percent(value);
        }

        private static void SetMetricCell(Text cell, LineageMetric metric, LineageVersion version, bool better)
        {
            if (version == null)
            {
                cell.text = "—";
                cell.color = Muted;
                cell.fontStyle = FontStyle.Normal;
                return;
            }

            if (!LineageCompare.TryValue(metric, version, out float value))
            {
                cell.text = "chưa chấm";
                cell.color = Muted;
                cell.fontStyle = FontStyle.Italic;
                return;
            }

            cell.text = LineageCompare.Format(metric, value);
            cell.color = better ? Gold : Color.white;
            cell.fontStyle = better ? FontStyle.Bold : FontStyle.Normal;
        }

        private static string MetricText(LineageMetric metric, LineageVersion version)
        {
            return LineageCompare.TryValue(metric, version, out float value) ? LineageCompare.Format(metric, value) : "—";
        }

        private int PickSlot(string versionId)
        {
            for (int i = 0; i < comparePicks.Length; i++)
            {
                if (comparePicks[i] != null && string.Equals(comparePicks[i], versionId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void AppendBadge(StringBuilder builder, string text, Color color)
        {
            if (builder.Length > 0)
            {
                builder.Append("   ");
            }

            builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(color)).Append("><b>").Append(text).Append("</b></color>");
        }

        private static void SetContentHeight(RectTransform content, int rows, float rowHeight)
        {
            if (content == null)
            {
                return;
            }

            float height = rows <= 0 ? 0f : rows * rowHeight + (rows - 1) * RowGap;
            content.sizeDelta = new Vector2(content.sizeDelta.x, height);
        }

        private static void ScrollToTop(RectTransform content)
        {
            if (content != null)
            {
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
            }
        }

        private static void SetRowCount<T>(List<T> rows, int count) where T : ListRow
        {
            for (int i = 0; i < rows.Count; i++)
            {
                bool visible = i < count;
                if (rows[i].Root.activeSelf != visible)
                {
                    rows[i].Root.SetActive(visible);
                }
            }
        }
    }
}
