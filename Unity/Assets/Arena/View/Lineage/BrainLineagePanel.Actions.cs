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

            string error = command.Start(SyncKind, "sync", "Updating the brain history...", LineageCommand.DefaultTimeoutSeconds);
            if (error != null)
            {
                SetMessage("Could not update the brain history: " + error, Warn);
            }
        }

        private void StartCommand(string kind, string arguments, string busyText, double timeout, object tag)
        {
            if (command == null)
            {
                SetMessage("Training folder not found.", Bad);
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
                result = new LineageResult { ok = false, error = "The brain history tool did not answer." };
            }

            if (!result.ok)
            {
                string prefix = kind == SyncKind
                    ? "Could not update the brain history: "
                    : kind == SnapshotKind ? "Could not save the version: " : "Could not create the branch: ";
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
                        ? "Saved version \"" + saved.Name + "\"" + (saved.HasEvaluation ? " and scored it." : " (not scored yet).")
                        : "New version saved.";
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
                SetMessage("The branch was created but its folder name could not be read.", Warn);
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

            string text = "Created branch \"" + index.BranchName(run) + "\" and made it the active branch." + TrainingNote(run);
            if (!string.IsNullOrEmpty(labelError))
            {
                text += " " + labelError;
            }
            if (!saved)
            {
                text += " The profile could not be saved.";
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
                ? " Applies from the next TRAIN."
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
            string text = "Selected \"" + branch.Name + "\" as the active branch.";
            if (LineageStore.TrainRunId(runsDirectory, behavior, branch.RunId) == null)
            {
                text += " This branch has no training checkpoint (checkpoint.pt), so TRAIN will continue the newest branch.";
            }
            else
            {
                string note = TrainingNote(branch.RunId);
                text += note.Length > 0 ? note : " The next TRAIN will continue this branch.";
            }
            if (!saved)
            {
                text += " The profile could not be saved.";
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

            string newName = LineageStore.CleanName(branch.Name + " (copy)");
            StartCommand(ForkKind, "fork --run-id " + branch.RunId, "Cloning \"" + branch.Name + "\"...",
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
            StartCommand(SnapshotKind, "snapshot --run-id " + branch.RunId, "Saving and scoring... (a few minutes)",
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
                    ? "Unpinned \"" + version.Name + "\"."
                    : "Pinned \"" + version.Name + "\": it will never be deleted automatically.", Good);
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
                SetMessage("Cannot fork from this version (unexpected folder name).", Bad);
                return;
            }

            string newName = LineageStore.CleanName("Fork of " + version.Name);
            StartCommand(ForkKind, "fork --version " + version.Id, "Creating a branch from \"" + version.Name + "\"...",
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
            renameLabel.text = (kind == RenameKind.Branch ? "Rename branch \"" : "Rename version \"") + currentName +
                "\": type a new name and press Enter (Esc cancels, empty restores the default name).";
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
