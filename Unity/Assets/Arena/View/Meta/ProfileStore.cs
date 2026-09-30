using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using PersonalArena.Core.Meta;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>How <see cref="ProfileStore.Load"/> obtained the profile.</summary>
    public enum ProfileLoadOutcome
    {
        /// <summary>No profile file yet: a fresh profile.</summary>
        New,
        /// <summary>profile.json was read.</summary>
        Loaded,
        /// <summary>profile.json was missing or corrupt; profile.bak.json was read.</summary>
        RecoveredFromBackup,
        /// <summary>profile.json was corrupt and no backup could be read: a fresh profile.</summary>
        RecoveredNew,
        /// <summary>profile.json exists but could not be read (locked, no access): a fresh profile in memory, saves blocked.</summary>
        ReadError
    }

    /// <summary>
    /// Loads and saves the owner's <see cref="PlayerProfile"/> (profile.json) and appends the economy CSV
    /// beside it. JsonUtility only; saves are atomic (tmp file, then File.Replace with a backup).
    /// IO problems are logged once per kind and never thrown. Main thread only.
    /// </summary>
    public sealed class ProfileStore
    {
        public const string DefaultFileName = "profile.json";
        public const string EconomyFileName = "economy_log.csv";
        public const string CorruptTimeFormat = "yyyyMMdd-HHmmss";

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        private readonly HashSet<string> loggedProblems = new HashSet<string>();
        private readonly string stem;
        private readonly string extension;
        private bool saveBlocked;

        /// <param name="directory">Folder of the profile (created on the first save).</param>
        /// <param name="fileName">Profile file name; the backup is "&lt;stem&gt;.bak&lt;ext&gt;".</param>
        public ProfileStore(string directory, string fileName = DefaultFileName)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("A profile directory is required.", nameof(directory));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = DefaultFileName;
            }

            DirectoryPath = Path.GetFullPath(directory);
            stem = Path.GetFileNameWithoutExtension(fileName);
            extension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".json";
            }

            ProfilePath = Path.Combine(DirectoryPath, stem + extension);
            BackupPath = Path.Combine(DirectoryPath, stem + ".bak" + extension);
            TempPath = ProfilePath + ".tmp";
            EconomyPath = Path.Combine(DirectoryPath, EconomyFileName);
            Profile = ProfileRules.NewProfile();
        }

        public string DirectoryPath { get; }
        public string ProfilePath { get; }
        public string BackupPath { get; }
        public string TempPath { get; }
        public string EconomyPath { get; }

        /// <summary>The profile in memory (never null). Change it through ProfileRules, then call <see cref="Save"/>.</summary>
        public PlayerProfile Profile { get; private set; }

        /// <summary>The Warrior of the profile (Sanitize guarantees it exists).</summary>
        public CharacterProfile Warrior => ProfileRules.FindCharacter(Profile, ProfileRules.WarriorId);

        public ProfileLoadOutcome LastLoad { get; private set; } = ProfileLoadOutcome.New;

        /// <summary>Where the last corrupt profile was kept, or null.</summary>
        public string CorruptCopyPath { get; private set; }

        /// <summary>True when saving is refused so an unreadable real profile is never overwritten.</summary>
        public bool SaveBlocked => saveBlocked;

        /// <summary>
        /// The store for this process: <paramref name="overridePath"/> (the -profile argument) when given — a
        /// folder, or a file path whose folder and name are used — else <paramref name="defaultDirectory"/>.
        /// An unusable override falls back to <paramref name="fallbackDirectory"/> (never the real profile).
        /// </summary>
        public static ProfileStore Create(string overridePath, string defaultDirectory, string fallbackDirectory)
        {
            if (string.IsNullOrWhiteSpace(overridePath))
            {
                return new ProfileStore(defaultDirectory);
            }

            try
            {
                string full = Path.GetFullPath(overridePath.Trim());
                bool endsWithSeparator = full.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                    full.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal);
                if (endsWithSeparator || Directory.Exists(full) || string.IsNullOrEmpty(Path.GetExtension(full)))
                {
                    return new ProfileStore(full);
                }

                string folder = Path.GetDirectoryName(full);
                return new ProfileStore(string.IsNullOrEmpty(folder) ? fallbackDirectory : folder, Path.GetFileName(full));
            }
            catch (Exception exception) when (IsIoProblem(exception))
            {
                Debug.LogWarning("Unusable -profile path '" + overridePath + "' (" + exception.Message + "); using " + fallbackDirectory + ".");
                return new ProfileStore(fallbackDirectory);
            }
        }

        /// <summary>
        /// Reads the profile. Missing → new (or the backup when only it exists). Corrupt → the bad file is kept
        /// as &lt;stem&gt;.corrupt-yyyyMMdd-HHmmss.json and the backup (else a new profile) is used and saved.
        /// Unreadable → a new profile in memory and saves are blocked. Always sanitized.
        /// </summary>
        public ProfileLoadOutcome Load()
        {
            saveBlocked = false;
            CorruptCopyPath = null;
            ProfileLoadOutcome outcome;
            bool saveNow = false;
            PlayerProfile loaded;
            switch (TryRead(ProfilePath, out loaded))
            {
                case ReadResult.Ok:
                    outcome = ProfileLoadOutcome.Loaded;
                    break;
                case ReadResult.Missing:
                    if (TryRead(BackupPath, out loaded) == ReadResult.Ok)
                    {
                        outcome = ProfileLoadOutcome.RecoveredFromBackup;
                        saveNow = true;
                    }
                    else
                    {
                        loaded = ProfileRules.NewProfile();
                        outcome = ProfileLoadOutcome.New;
                    }
                    break;
                case ReadResult.Corrupt:
                    if (!KeepCorruptCopy())
                    {
                        // The only copy of the bad file could not be kept aside: never overwrite it.
                        saveBlocked = true;
                    }
                    if (TryRead(BackupPath, out loaded) == ReadResult.Ok)
                    {
                        outcome = ProfileLoadOutcome.RecoveredFromBackup;
                    }
                    else
                    {
                        loaded = ProfileRules.NewProfile();
                        outcome = ProfileLoadOutcome.RecoveredNew;
                    }
                    saveNow = true;
                    break;
                default:
                    loaded = ProfileRules.NewProfile();
                    outcome = ProfileLoadOutcome.ReadError;
                    saveBlocked = true;
                    break;
            }

            ProfileRules.Sanitize(loaded);
            Profile = loaded;
            LastLoad = outcome;
            if (saveNow)
            {
                Save();
            }

            return outcome;
        }

        /// <summary>Atomic save: profile.json.tmp, then File.Replace keeping profile.bak.json (move when new). False on failure.</summary>
        public bool Save()
        {
            if (saveBlocked)
            {
                LogOnce("blocked", "Profile save skipped: " + ProfilePath + " could not be read, so it is not overwritten.");
                return false;
            }

            try
            {
                Directory.CreateDirectory(DirectoryPath);
                byte[] bytes = Utf8.GetBytes(JsonUtility.ToJson(Profile, true));
                using (FileStream stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (File.Exists(ProfilePath))
                {
                    try
                    {
                        File.Replace(TempPath, ProfilePath, BackupPath, true);
                    }
                    catch (Exception exception) when (exception is IOException || exception is PlatformNotSupportedException ||
                        exception is UnauthorizedAccessException || exception is NotSupportedException)
                    {
                        // Some file systems have no ReplaceFile: keep the old file as the backup, then move.
                        File.Copy(ProfilePath, BackupPath, true);
                        File.Delete(ProfilePath);
                        File.Move(TempPath, ProfilePath);
                    }
                }
                else
                {
                    File.Move(TempPath, ProfilePath);
                }

                return true;
            }
            catch (Exception exception) when (IsIoProblem(exception))
            {
                LogOnce("save", "Could not save the profile " + ProfilePath + ": " + exception.Message);
                return false;
            }
        }

        /// <summary>Appends one line to economy_log.csv, writing <see cref="EconomyLog.Header"/> first when the file is new or empty.</summary>
        public bool AppendEconomyLine(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return false;
            }

            return AppendEconomyLines(new[] { line });
        }

        /// <summary>Appends several lines in one write (Auto Farm).</summary>
        public bool AppendEconomyLines(IReadOnlyList<string> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(DirectoryPath);
                StringBuilder text = new StringBuilder();
                FileInfo info = new FileInfo(EconomyPath);
                if (!info.Exists || info.Length == 0)
                {
                    text.Append(EconomyLog.Header).Append('\n');
                }

                for (int i = 0; i < lines.Count; i++)
                {
                    if (!string.IsNullOrEmpty(lines[i]))
                    {
                        text.Append(lines[i]).Append('\n');
                    }
                }

                File.AppendAllText(EconomyPath, text.ToString(), Utf8);
                return true;
            }
            catch (Exception exception) when (IsIoProblem(exception))
            {
                LogOnce("economy", "Could not write the economy log " + EconomyPath + ": " + exception.Message);
                return false;
            }
        }

        private enum ReadResult
        {
            Missing,
            Ok,
            Corrupt,
            IoError
        }

        private ReadResult TryRead(string path, out PlayerProfile profile)
        {
            profile = null;
            string json;
            try
            {
                if (!File.Exists(path))
                {
                    return ReadResult.Missing;
                }

                json = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception exception) when (IsIoProblem(exception))
            {
                LogOnce("read", "Could not read " + path + ": " + exception.Message);
                return ReadResult.IoError;
            }

            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                return ReadResult.Corrupt;
            }

            try
            {
                profile = JsonUtility.FromJson<PlayerProfile>(json);
            }
            catch (Exception)
            {
                // JsonUtility reports bad JSON with ArgumentException; anything it throws means "not a profile".
                profile = null;
            }

            return profile == null ? ReadResult.Corrupt : ReadResult.Ok;
        }

        /// <summary>Moves the corrupt profile aside (copies it when the move fails). False when no copy could be kept.</summary>
        private bool KeepCorruptCopy()
        {
            string target = CorruptPath();
            try
            {
                File.Move(ProfilePath, target);
                CorruptCopyPath = target;
                Debug.LogWarning("The profile " + ProfilePath + " was unreadable; kept it as " + target + ".");
                return true;
            }
            catch (Exception exception) when (IsIoProblem(exception))
            {
                try
                {
                    File.Copy(ProfilePath, target, false);
                    CorruptCopyPath = target;
                    Debug.LogWarning("The profile " + ProfilePath + " was unreadable; copied it to " + target + ".");
                    return true;
                }
                catch (Exception copyException) when (IsIoProblem(copyException))
                {
                    LogOnce("corrupt", "Could not keep the unreadable profile " + ProfilePath + ": " + exception.Message + " / " + copyException.Message);
                    return false;
                }
            }
        }

        private string CorruptPath()
        {
            string time = DateTime.Now.ToString(CorruptTimeFormat, CultureInfo.InvariantCulture);
            string path = Path.Combine(DirectoryPath, stem + ".corrupt-" + time + extension);
            for (int n = 2; File.Exists(path) && n < 1000; n++)
            {
                path = Path.Combine(DirectoryPath, stem + ".corrupt-" + time + "-" + n.ToString(CultureInfo.InvariantCulture) + extension);
            }

            return path;
        }

        private void LogOnce(string kind, string message)
        {
            if (loggedProblems.Add(kind))
            {
                Debug.LogWarning(message);
            }
        }

        private static bool IsIoProblem(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException ||
                exception is NotSupportedException || exception is ArgumentException;
        }
    }
}
