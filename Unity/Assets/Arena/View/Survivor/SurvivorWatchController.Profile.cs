using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PersonalArena.Core;
using PersonalArena.Core.Meta;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PersonalArena.View
{
    /// <summary>The owner's profile: the build of the next run and switching class.</summary>
    public sealed partial class SurvivorWatchController
    {
        /// <summary>The build of the next run: the active loadout and selected tier of the profile, fixed until the run ends.</summary>
        private CharacterBuild NextRunBuild()
        {
            CharacterProfile warrior = CurrentCharacter;
            runBuild = ProfileRules.ToBuild(warrior, store.Profile.SelectedTier);
            runLoadout = warrior.ActiveLoadout;
            runLoadoutName = MetaViewLogic.LoadoutName(warrior, runLoadout);
            return runBuild.Clone();
        }

        /// <summary>True when the profile's build (loadout, points or tier) differs from the run on screen.</summary>
        private bool IsBuildChangePending()
        {
            if (store == null || runBuild == null)
            {
                return false;
            }

            CharacterProfile warrior = CurrentCharacter;
            return warrior.ActiveLoadout != runLoadout ||
                !MetaViewLogic.SameBuild(runBuild, ProfileRules.ToBuild(warrior, store.Profile.SelectedTier));
        }

        private void OnProfileChanged()
        {
            // M7: "Chọn" in the character panel picked another class: watch that class's brain now.
            string selected = store != null ? store.SelectedClassId : classId;
            if (!string.Equals(selected, classId, StringComparison.Ordinal))
            {
                SwitchClass(selected);
                return;
            }

            RefreshInfo();
            characterPanel?.Refresh();
            comparePanel?.Refresh();
            if (training != null)
            {
                PollTraining();
            }

            // M6: the newest brain follows the active branch (profile BrainRunId).
            if (watchedVersion == null && !loadedIsChampion && string.IsNullOrWhiteSpace(brainFile))
            {
                PollBrain();
            }
        }

        /// <summary>The kit of a class (Core <see cref="SurvivorDefaults.ForClass"/>); an unknown id gives the Warrior.</summary>
        private static SurvivorClassDef ClassDefinition(string id)
        {
            return SurvivorDefaults.ForClass(id) ?? SurvivorDefaults.Warrior();
        }

        /// <summary>
        /// M7 class change: forget the old class's brain (newest, champion or saved version), point the lineage,
        /// history and profile panels at the new class, and start a fresh run with its kit and its brain.
        /// Running training is left alone; TRAIN offers to switch it.
        /// </summary>
        private void SwitchClass(string newClassId)
        {
            classId = string.IsNullOrEmpty(newClassId) ? ProfileRules.WarriorId : newClassId;
            watchedVersion = null;
            watchedBranchName = null;
            loadedIsVersion = false;
            loadedIsChampion = false;
            loadedChampion = null;
            loadedPath = null;
            loadedWriteTime = default;
            loadedBrainBytes = null;
            loadedBrainName = null;
            brainStatus = null;
            pilot.ClearBrain();
            recent.Clear();

            if (lineageBound && lineagePanel != null)
            {
                lineagePanel.Bind(store, runsDirectory, BehaviorName, () => trainingSnapshot);
            }
            if (!string.IsNullOrWhiteSpace(runsDirectory))
            {
                historyPanel?.SetRunDirectory(BrainLocator.FindNewestRunDirectory(runsDirectory, BehaviorName));
                profilePanel?.SetSource(runsDirectory, BehaviorName);
            }

            if (sim != null)
            {
                sim.Config.ClassDef = ClassDefinition(classId);
                RestartNow();
            }
            if (survivorAudio != null)
            {
                survivorAudio.OnClassSwitched();
            }

            PollBrain();
            if (training != null)
            {
                PollTraining();
            }

            characterPanel?.Refresh();
            comparePanel?.Refresh();
            ShowSwitchNotice("Đang xem " + ClassViewLogic.DisplayName(classId) +
                (pilot.Brain == null ? "\n" + ClassViewLogic.NoBrainText(classId, training != null) : string.Empty));
            switchNoticeUntil = Time.unscaledTime + ProfileNoticeSeconds;
        }
    }
}
