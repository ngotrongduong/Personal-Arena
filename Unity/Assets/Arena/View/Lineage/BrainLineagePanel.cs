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
    public sealed class BrainLineagePanel : MetaPanel
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
            behavior = string.IsNullOrEmpty(behaviorName) ? "Warrior" : behaviorName;
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
            CharacterProfile warrior = store != null ? store.Warrior : null;
            return warrior != null ? warrior.BrainRunId : null;
        }

        /// <summary>The run the training service is training now, or null.</summary>
        private string TrainingRunId()
        {
            if (trainingSnapshot == null)
            {
                return null;
            }

            TrainingSnapshot snapshot = trainingSnapshot();
            return snapshot.IsActive && snapshot.Status != null && !string.IsNullOrEmpty(snapshot.Status.run_id)
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

        // ------------------------------------------------------------------ commands

        private void StartSync()
        {
            if (command == null || command.IsRunning || commandMissing != null)
            {
                return;
            }

            string error = command.Start(SyncKind, "sync", "Đang cập nhật lịch sử não...", LineageCommand.DefaultTimeoutSeconds);
            if (error != null)
            {
                SetMessage("Chưa cập nhật được lịch sử não: " + error, Warn);
            }
        }

        private void StartCommand(string kind, string arguments, string busyText, double timeout, object tag)
        {
            if (command == null)
            {
                SetMessage("Không tìm thấy thư mục huấn luyện.", Bad);
                return;
            }

            string error = command.Start(kind, arguments, busyText, timeout, tag);
            if (error != null)
            {
                SetMessage(error, Bad);
            }
            else
            {
                message = null;
            }

            Refresh();
        }

        private void HandleResult(string kind, LineageResult result, object tag)
        {
            if (result == null)
            {
                result = new LineageResult { ok = false, error = "Lệnh lịch sử não không trả lời." };
            }

            if (!result.ok)
            {
                string prefix = kind == SyncKind
                    ? "Chưa cập nhật được lịch sử não: "
                    : kind == SnapshotKind ? "Không lưu được phiên bản: " : "Không tạo được nhánh mới: ";
                Reload();
                SetMessage(prefix + result.error, Bad);
                Refresh();
                return;
            }

            switch (kind)
            {
                case ForkKind:
                    CompleteFork(result, tag as string);
                    break;
                case SnapshotKind:
                {
                    Reload();
                    LineageVersion saved = index.FindVersion(result.id);
                    string text = saved != null
                        ? "Đã lưu phiên bản \"" + saved.Name + "\"" + (saved.HasEvaluation ? " và chấm điểm xong." : " (chưa chấm điểm).")
                        : "Đã lưu phiên bản mới.";
                    if (!string.IsNullOrEmpty(result.warning))
                    {
                        text += " " + result.warning;
                    }

                    SetMessage(text, string.IsNullOrEmpty(result.warning) ? Good : Warn);
                    break;
                }
                default:
                    Reload();
                    break;
            }

            Refresh();
        }

        private void CompleteFork(LineageResult result, string newName)
        {
            string run = result.run_id;
            if (!TrainingServiceClient.IsSafeRunId(run))
            {
                Reload();
                SetMessage("Đã tạo nhánh mới nhưng không đọc được tên thư mục của nó.", Warn);
                return;
            }

            string labelError = null;
            if (!string.IsNullOrEmpty(newName))
            {
                LineageStore.RenameBranch(runsDirectory, behavior, run, newName, out labelError);
            }

            bool saved = SetActiveBranch(run);
            Reload();
            if (index.FindBranch(run) != null)
            {
                selectedRunId = index.FindBranch(run).RunId;
                ScrollToTop(versionContent);
            }

            string text = "Đã tạo nhánh mới \"" + index.BranchName(run) + "\" và chọn làm nhánh đang dùng." + TrainingNote(run);
            if (!string.IsNullOrEmpty(labelError))
            {
                text += " " + labelError;
            }
            if (!saved)
            {
                text += " Không lưu được hồ sơ.";
            }
            if (!string.IsNullOrEmpty(result.warning))
            {
                text += " " + result.warning;
            }

            SetMessage(text, saved && string.IsNullOrEmpty(result.warning) ? Good : Warn);
        }

        /// <summary>Sets the profile's active branch, saves it and tells the controller. False when the save failed.</summary>
        private bool SetActiveBranch(string runId)
        {
            CharacterProfile warrior = store != null ? store.Warrior : null;
            if (warrior == null)
            {
                return false;
            }

            warrior.BrainRunId = runId;
            bool saved = store.Save();
            ProfileChanged?.Invoke();
            return saved;
        }

        /// <summary>" Áp dụng ở lần HUẤN LUYỆN sau." while training runs another branch, else "".</summary>
        private string TrainingNote(string runId)
        {
            string training = TrainingRunId();
            return training != null && !string.Equals(training, runId, StringComparison.OrdinalIgnoreCase)
                ? " Áp dụng ở lần HUẤN LUYỆN sau."
                : string.Empty;
        }

        // ------------------------------------------------------------------ branch actions

        private void OnSelectBranch(BranchRow row)
        {
            if (row.Branch == null || string.Equals(row.Branch.RunId, selectedRunId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            selectedRunId = row.Branch.RunId;
            ScrollToTop(versionContent);
            Refresh();
        }

        private void OnUseBranch(BranchRow row)
        {
            LineageBranch branch = row.Branch;
            if (branch == null || !branch.OnDisk)
            {
                return;
            }

            bool saved = SetActiveBranch(branch.RunId);
            string text = "Đã chọn \"" + branch.Name + "\" làm nhánh đang dùng.";
            if (LineageStore.TrainRunId(runsDirectory, behavior, branch.RunId) == null)
            {
                text += " Nhánh này chưa có bản lưu huấn luyện (checkpoint.pt), nên HUẤN LUYỆN sẽ học tiếp nhánh mới nhất.";
            }
            else
            {
                string note = TrainingNote(branch.RunId);
                text += note.Length > 0 ? note : " Lần HUẤN LUYỆN sau sẽ học tiếp nhánh này.";
            }
            if (!saved)
            {
                text += " Không lưu được hồ sơ.";
            }

            selectedRunId = branch.RunId;
            SetMessage(text, saved ? Good : Warn);
            Refresh();
        }

        private void OnRenameBranch(BranchRow row)
        {
            if (row.Branch != null)
            {
                BeginRename(RenameKind.Branch, row.Branch.RunId, row.Branch.Name);
            }
        }

        private void OnCloneBranch(BranchRow row)
        {
            LineageBranch branch = row.Branch;
            if (branch == null || !branch.OnDisk || !TrainingServiceClient.IsSafeRunId(branch.RunId))
            {
                return;
            }

            string newName = LineageStore.CleanName(branch.Name + " (bản sao)");
            StartCommand(ForkKind, "fork --run-id " + branch.RunId, "Đang nhân bản \"" + branch.Name + "\"...",
                LineageCommand.DefaultTimeoutSeconds, newName);
        }

        private void OnSnapshotBranch(BranchRow row)
        {
            LineageBranch branch = row.Branch;
            if (branch == null || !branch.OnDisk || !TrainingServiceClient.IsSafeRunId(branch.RunId))
            {
                return;
            }

            selectedRunId = branch.RunId;
            StartCommand(SnapshotKind, "snapshot --run-id " + branch.RunId, "Đang lưu và chấm điểm... (vài phút)",
                LineageCommand.EvaluateTimeoutSeconds, null);
        }

        // ------------------------------------------------------------------ version actions

        private void OnWatchVersion(VersionRow row)
        {
            LineageVersion version = row.Version;
            if (version == null || index == null)
            {
                return;
            }

            WatchVersionRequested?.Invoke(version, index.BranchName(version.RunId));
            SetOpen(false);
        }

        private void OnCompareVersion(VersionRow row)
        {
            LineageVersion version = row.Version;
            if (version == null)
            {
                return;
            }

            for (int i = 0; i < comparePicks.Length; i++)
            {
                if (string.Equals(comparePicks[i], version.Id, StringComparison.Ordinal))
                {
                    comparePicks[i] = null;
                    if (newestPick == i)
                    {
                        newestPick = comparePicks[1 - i] != null ? 1 - i : -1;
                    }

                    Refresh();
                    return;
                }
            }

            int slot = comparePicks[0] == null ? 0 : comparePicks[1] == null ? 1 : (newestPick == 0 ? 1 : 0);
            comparePicks[slot] = version.Id;
            newestPick = slot;
            Refresh();
        }

        private void OnPinVersion(VersionRow row)
        {
            LineageVersion version = row.Version;
            if (version == null)
            {
                return;
            }

            if (!LineageStore.SetPinned(runsDirectory, behavior, version.Id, !version.Pinned, out string error))
            {
                SetMessage(error, Bad);
            }
            else
            {
                SetMessage(version.Pinned
                    ? "Đã bỏ ghim \"" + version.Name + "\"."
                    : "Đã ghim \"" + version.Name + "\": bản này sẽ không bao giờ bị tự xoá.", Good);
            }

            Reload();
            Refresh();
        }

        private void OnRenameVersion(VersionRow row)
        {
            if (row.Version != null)
            {
                BeginRename(RenameKind.Version, row.Version.Id, row.Version.Name);
            }
        }

        private void OnForkVersion(VersionRow row)
        {
            LineageVersion version = row.Version;
            if (version == null || !version.HasCheckpoint)
            {
                return;
            }

            if (!TrainingServiceClient.IsSafeRunId(version.Id))
            {
                SetMessage("Không rẽ nhánh được từ bản này (tên thư mục lạ).", Bad);
                return;
            }

            string newName = LineageStore.CleanName("Nhánh từ " + version.Name);
            StartCommand(ForkKind, "fork --version " + version.Id, "Đang tạo nhánh mới từ \"" + version.Name + "\"...",
                LineageCommand.DefaultTimeoutSeconds, newName);
        }

        // ------------------------------------------------------------------ rename

        private void BeginRename(RenameKind kind, string id, string currentName)
        {
            if (renameArea == null || renameField == null)
            {
                return;
            }

            renameKind = kind;
            renameId = id;
            hideRenameNextFrame = false;
            renameArea.SetActive(true);
            renameLabel.text = (kind == RenameKind.Branch ? "Đổi tên nhánh \"" : "Đổi tên phiên bản \"") + currentName +
                "\": gõ tên mới rồi bấm Enter (Esc để huỷ, để trống là tên mặc định).";
            renameField.SetTextWithoutNotify(currentName ?? string.Empty);
            renameField.Select();
            renameField.ActivateInputField();
        }

        private void CancelRename()
        {
            renameKind = RenameKind.None;
            renameId = null;
            hideRenameNextFrame = true;
        }

        private void OnRenameEnded(string text)
        {
            if (renameKind == RenameKind.None)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            bool cancelled = renameField.wasCanceled || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame);
            RenameKind kind = renameKind;
            string id = renameId;
            CancelRename();
            if (cancelled || !IsBound)
            {
                return;
            }

            string clean = LineageStore.CleanName(text);
            bool ok;
            string error;
            if (kind == RenameKind.Branch)
            {
                if (clean == LineageStore.DefaultBranchName(id))
                {
                    clean = null;
                }

                ok = LineageStore.RenameBranch(runsDirectory, behavior, id, clean ?? string.Empty, out error);
            }
            else
            {
                LineageVersion version = index != null ? index.FindVersion(id) : null;
                if (version != null && clean == LineageStore.DefaultVersionName(version.Step))
                {
                    clean = null;
                }

                ok = LineageStore.RenameVersion(runsDirectory, behavior, id, clean ?? string.Empty, out error);
            }

            if (!ok)
            {
                SetMessage(error, Bad);
            }

            Reload();
            Refresh();
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
