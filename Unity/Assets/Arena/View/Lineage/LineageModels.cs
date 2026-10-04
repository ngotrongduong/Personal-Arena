using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>version.json of one saved brain (written last by Trainer/brain_lineage.py).</summary>
    [Serializable]
    public sealed class LineageVersionFile
    {
        public string id;
        public string run_id;
        public long step;
        public int schema_version;
        public string source;
        public string created_at;
        public bool evaluated;
        public float score;
        public bool champion;
        public bool passes_m4a;
        public ChampionSummary summary;
        public ChampionBehavior behavior;
        public string evaluated_at;
        public string brain_file;
        public bool has_checkpoint;
    }

    /// <summary>One entry of lineage/branches.json (written only by Python).</summary>
    [Serializable]
    public sealed class LineageBranchEntry
    {
        public string run_id;
        public string parent_run;
        public long parent_step;
        public string parent_version;
        public string created_at;
    }

    [Serializable]
    public sealed class LineageBranchesFile
    {
        public LineageBranchEntry[] branches;
    }

    [Serializable]
    public sealed class LineageVersionLabel
    {
        public string id;
        public string name;
        public bool pinned;
    }

    [Serializable]
    public sealed class LineageBranchLabel
    {
        public string run_id;
        public string name;
    }

    /// <summary>lineage/labels.json: the owner's names and pins (written only by the viewer).</summary>
    [Serializable]
    public sealed class LineageLabelsFile
    {
        public LineageVersionLabel[] versions = new LineageVersionLabel[0];
        public LineageBranchLabel[] branches = new LineageBranchLabel[0];
    }

    /// <summary>A saved brain version as the panel shows it.</summary>
    public sealed class LineageVersion
    {
        public string Id;
        public string RunId;
        public long Step;
        public int SchemaVersion;
        public string Source;
        public string CreatedAt;
        public bool Evaluated;
        public float Score;
        /// <summary>This brain became the champion when it was evaluated ("từng giỏi nhất").</summary>
        public bool WasChampion;
        public bool PassesM4A;
        /// <summary>Evaluation summary, or null when the version was never evaluated.</summary>
        public ChampionSummary Summary;
        public ChampionBehavior Behavior;
        public string EvaluatedAt;
        public string Directory;
        public string BrainPath;
        public string CheckpointPath;
        /// <summary>checkpoint.pt exists on disk now (needed to fork).</summary>
        public bool HasCheckpoint;
        /// <summary>It is the brain of the current champion.json ("GIỎI NHẤT").</summary>
        public bool IsCurrentChampion;
        public bool Pinned;
        /// <summary>The owner's name, or null.</summary>
        public string CustomName;
        public Dictionary<string, int> DeathCauses = new Dictionary<string, int>();

        public string Name => string.IsNullOrEmpty(CustomName) ? LineageStore.DefaultVersionName(Step) : CustomName;

        /// <summary>Has a usable evaluation (compare and the row numbers need it).</summary>
        public bool HasEvaluation => Evaluated && Summary != null && Summary.Runs > 0;
    }

    /// <summary>A training run the owner can watch, train or fork ("nhánh").</summary>
    public sealed class LineageBranch
    {
        public string RunId;
        public string Directory;
        /// <summary>False for a run that only survives through saved versions (folder gone or old schema).</summary>
        public bool OnDisk;
        public string ParentRun;
        public long ParentStep;
        public string ParentVersion;
        public string CreatedAt;
        public string CustomName;
        /// <summary>Newest numbered checkpoint step, else the latest.brain step, else 0.</summary>
        public long CurrentStep;
        /// <summary>&lt;run&gt;/&lt;Behavior&gt;/checkpoint.pt exists (TRAIN can continue it).</summary>
        public bool HasCheckpoint;
        /// <summary>&lt;run&gt;/&lt;Behavior&gt;/latest.brain, or null.</summary>
        public string LatestBrainPath;

        public string Name => string.IsNullOrEmpty(CustomName) ? LineageStore.DefaultBranchName(RunId) : CustomName;
        public bool HasParent => !string.IsNullOrEmpty(ParentRun);
    }

    /// <summary>Everything the lineage panel shows, read from disk in one go.</summary>
    public sealed class LineageIndex
    {
        public string RunsDirectory;
        public string Behavior;
        public string LineageDirectory;
        public readonly List<LineageBranch> Branches = new List<LineageBranch>();
        /// <summary>All versions, newest first.</summary>
        public readonly List<LineageVersion> Versions = new List<LineageVersion>();
        public LineageLabelsFile Labels = new LineageLabelsFile();

        public LineageBranch FindBranch(string runId)
        {
            if (string.IsNullOrEmpty(runId))
            {
                return null;
            }

            for (int i = 0; i < Branches.Count; i++)
            {
                if (string.Equals(Branches[i].RunId, runId, StringComparison.OrdinalIgnoreCase))
                {
                    return Branches[i];
                }
            }

            return null;
        }

        public LineageVersion FindVersion(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (int i = 0; i < Versions.Count; i++)
            {
                if (string.Equals(Versions[i].Id, id, StringComparison.Ordinal))
                {
                    return Versions[i];
                }
            }

            return null;
        }

        /// <summary>Versions of one run, newest first.</summary>
        public List<LineageVersion> VersionsOf(string runId)
        {
            List<LineageVersion> result = new List<LineageVersion>();
            for (int i = 0; i < Versions.Count; i++)
            {
                if (string.Equals(Versions[i].RunId, runId, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(Versions[i]);
                }
            }

            return result;
        }

        /// <summary>The branch's display name (label or default), also for runs that are not branches.</summary>
        public string BranchName(string runId)
        {
            LineageBranch branch = FindBranch(runId);
            return branch != null ? branch.Name : LineageStore.DefaultBranchName(runId);
        }
    }
}
