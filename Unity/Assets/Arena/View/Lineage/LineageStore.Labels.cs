using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>Names and pins the viewer writes (labels.json), saved through a backup file.</summary>
    public static partial class LineageStore
    {
        // ------------------------------------------------------------------ labels (viewer writes)

        public static LineageLabelsFile ReadLabels(string lineageDirectory)
        {
            if (string.IsNullOrEmpty(lineageDirectory))
            {
                return new LineageLabelsFile();
            }

            return ParseLabels(ReadText(Path.Combine(lineageDirectory, LabelsFileName)));
        }

        /// <summary>
        /// Reads labels.json before an edit. False when the file exists but cannot be read or parsed
        /// (locked, half-written, damaged): saving then would drop every other name and pin.
        /// </summary>
        private static bool TryReadLabelsForEdit(string lineageDirectory, out LineageLabelsFile labels, out string error)
        {
            error = null;
            string path = Path.Combine(lineageDirectory, LabelsFileName);
            string text = ReadText(path);
            bool unreadable = text == null
                ? SafeFileExists(path)
                : !string.IsNullOrWhiteSpace(text) && ParseJson<LineageLabelsFile>(text) == null;
            if (unreadable)
            {
                labels = null;
                error = "Không đọc được tên/ghim đã lưu (labels.json) nên chưa đổi gì. Thử lại sau giây lát.";
                return false;
            }

            labels = ParseLabels(text);
            return true;
        }

        /// <summary>Renames a version (an empty name restores the default). Re-reads labels.json first, keeps other entries.</summary>
        public static bool RenameVersion(string runsDirectory, string behavior, string versionId, string name, out string error)
        {
            return UpdateVersionLabel(runsDirectory, behavior, versionId, label => label.name = CleanName(name) ?? string.Empty, out error);
        }

        /// <summary>Pins or unpins a version (a pinned version is never pruned by the trainer).</summary>
        public static bool SetPinned(string runsDirectory, string behavior, string versionId, bool pinned, out string error)
        {
            return UpdateVersionLabel(runsDirectory, behavior, versionId, label => label.pinned = pinned, out error);
        }

        /// <summary>Renames a branch (an empty name restores the default). Keeps other entries.</summary>
        public static bool RenameBranch(string runsDirectory, string behavior, string runId, string name, out string error)
        {
            error = null;
            string lineage = LineageDirectory(runsDirectory, behavior);
            if (lineage == null || string.IsNullOrEmpty(runId))
            {
                error = "Không tìm thấy thư mục lịch sử não.";
                return false;
            }

            if (!TryReadLabelsForEdit(lineage, out LineageLabelsFile labels, out error))
            {
                return false;
            }

            List<LineageBranchLabel> list = new List<LineageBranchLabel>();
            string clean = CleanName(name);
            for (int i = 0; i < labels.branches.Length; i++)
            {
                LineageBranchLabel label = labels.branches[i];
                if (label == null || string.IsNullOrEmpty(label.run_id) ||
                    string.Equals(label.run_id, runId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                list.Add(label);
            }

            if (clean != null)
            {
                list.Add(new LineageBranchLabel { run_id = runId, name = clean });
            }

            labels.branches = list.ToArray();
            return SaveLabels(lineage, labels, out error);
        }

        private static bool UpdateVersionLabel(string runsDirectory, string behavior, string versionId,
            Action<LineageVersionLabel> change, out string error)
        {
            error = null;
            string lineage = LineageDirectory(runsDirectory, behavior);
            if (lineage == null || string.IsNullOrEmpty(versionId))
            {
                error = "Không tìm thấy thư mục lịch sử não.";
                return false;
            }

            if (!TryReadLabelsForEdit(lineage, out LineageLabelsFile labels, out error))
            {
                return false;
            }

            List<LineageVersionLabel> list = new List<LineageVersionLabel>();
            LineageVersionLabel target = null;
            for (int i = 0; i < labels.versions.Length; i++)
            {
                LineageVersionLabel label = labels.versions[i];
                if (label == null || string.IsNullOrEmpty(label.id))
                {
                    continue;
                }

                if (string.Equals(label.id, versionId, StringComparison.Ordinal))
                {
                    if (target != null)
                    {
                        continue; // drop duplicates
                    }

                    target = label;
                }

                list.Add(label);
            }

            if (target == null)
            {
                target = new LineageVersionLabel { id = versionId, name = string.Empty };
                list.Add(target);
            }

            change(target);
            target.name = target.name ?? string.Empty;
            if (string.IsNullOrEmpty(target.name) && !target.pinned)
            {
                list.Remove(target);
            }

            labels.versions = list.ToArray();
            return SaveLabels(lineage, labels, out error);
        }

        /// <summary>
        /// Atomic write of labels.json: a temp file of its own, then File.Replace (move when new). When
        /// File.Replace is refused, the old file becomes labels.json.bak until the new one is in place.
        /// </summary>
        public static bool SaveLabels(string lineageDirectory, LineageLabelsFile labels, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(lineageDirectory) || labels == null)
            {
                error = "Không tìm thấy thư mục lịch sử não.";
                return false;
            }

            string path = Path.Combine(lineageDirectory, LabelsFileName);
            string temp = path + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
            try
            {
                System.IO.Directory.CreateDirectory(lineageDirectory);
                labels.versions = labels.versions ?? new LineageVersionLabel[0];
                labels.branches = labels.branches ?? new LineageBranchLabel[0];
                byte[] bytes = Utf8.GetBytes(JsonUtility.ToJson(labels, true));
                using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temp, path, null, true);
                    }
                    catch (Exception exception) when (exception is IOException || exception is PlatformNotSupportedException ||
                        exception is UnauthorizedAccessException || exception is NotSupportedException)
                    {
                        ReplaceThroughBackup(temp, path);
                    }
                }
                else
                {
                    File.Move(temp, path);
                }

                return true;
            }
            catch (Exception exception) when (IsIoProblem(exception))
            {
                TryDelete(temp);
                Debug.LogWarning("Could not save " + path + ": " + exception.Message);
                error = "Không lưu được tên/ghim: " + exception.Message;
                return false;
            }
        }
    }
}
