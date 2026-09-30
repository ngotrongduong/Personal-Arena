using System;
using System.Collections.Generic;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Meta
{
    /// <summary>
    /// The saved out-of-run state (GDD §3.1–3.5). Plain [Serializable] classes with public fields only,
    /// so Unity JsonUtility can save and load them. Run <see cref="ProfileRules.Sanitize"/> after loading.
    /// </summary>
    [Serializable]
    public sealed class PlayerProfile
    {
        public int Version = 1;
        public long Gold;
        /// <summary>Highest tier the player may pick (1..10).</summary>
        public int UnlockedTier = 1;
        /// <summary>Tier picked for the next run (1..UnlockedTier).</summary>
        public int SelectedTier = 1;
        public string SelectedClassId = "warrior";
        /// <summary>A <see cref="TrainingFocusInfo.Id"/> value.</summary>
        public string TrainingFocus = "balanced";
        public List<CharacterProfile> Characters = new List<CharacterProfile>();
        public ProfileStats Stats = new ProfileStats();
    }

    [Serializable]
    public sealed class CharacterProfile
    {
        public string ClassId;
        /// <summary>Character level = total stat points owned.</summary>
        public int Level;
        /// <summary>0..4.</summary>
        public int ActiveLoadout;
        /// <summary>Always length <see cref="ProfileRules.LoadoutCount"/>.</summary>
        public Loadout[] Loadouts;
        /// <summary>Brain run the viewer loads for this class, e.g. "warrior-s001".</summary>
        public string BrainRunId;
    }

    [Serializable]
    public sealed class Loadout
    {
        public string Name;
        /// <summary>Length <see cref="StatInfo.SlotCount"/>; slots ≥ <see cref="StatInfo.UsedCount"/> stay 0.</summary>
        public int[] Points = new int[StatInfo.SlotCount];
        public LoadoutRecord Record = new LoadoutRecord();
    }

    [Serializable]
    public sealed class LoadoutRecord
    {
        public int Runs;
        public int Wins;
        public long TotalGold;
        public float TotalMinutes;
        public float BestSeconds;
        /// <summary>Survived seconds of the last <see cref="ProfileRules.RecentCap"/> runs, oldest first.</summary>
        public List<float> RecentSeconds = new List<float>();
    }

    [Serializable]
    public sealed class ProfileStats
    {
        public int Runs;
        public int Wins;
        public long GoldEarned;
        public float BestSeconds;
        public int FarmSessions;
        public int FarmRuns;
    }
}
