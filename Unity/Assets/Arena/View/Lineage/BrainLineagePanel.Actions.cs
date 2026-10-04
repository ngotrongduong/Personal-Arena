using System;
using System.Collections.Generic;
using System.Text;
using PersonalArena.Core.Meta;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PersonalArena.View
{
    /// <summary>Commands sent to the lineage tool, and the branch, version and rename actions.</summary>
    public sealed partial class BrainLineagePanel
    {
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
            CharacterProfile warrior = PanelCharacter();
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
    }
}
