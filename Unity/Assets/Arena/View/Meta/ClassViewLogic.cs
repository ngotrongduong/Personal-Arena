using System;
using PersonalArena.Core.Meta;

namespace PersonalArena.View
{
    /// <summary>What a class card of the shop shows.</summary>
    public enum ClassShopState
    {
        /// <summary>Owned and the selected (watched and trained) class.</summary>
        Selected,
        /// <summary>Owned; "Chọn" makes it the selected class.</summary>
        Owned,
        /// <summary>Not owned and the wallet holds the price.</summary>
        Buyable,
        /// <summary>Not owned and gold is short.</summary>
        TooExpensive,
        /// <summary>Unknown class id.</summary>
        Unavailable
    }

    /// <summary>What the TRAIN button does right now.</summary>
    public enum TrainButtonAction
    {
        /// <summary>Nothing (training runs outside the game, the machine cannot train, or it is saving).</summary>
        None,
        Start,
        Stop,
        /// <summary>Training runs for another class: stop it (saving a checkpoint), then start the selected class.</summary>
        SwitchClass
    }

    /// <summary>
    /// M7 pure helpers for the selected class: behavior and display names, the class shop texts, the TRAIN
    /// button decision when another class is training, and the "no brain" hint. Rules stay in
    /// <see cref="ProfileRules"/>; this only maps and formats.
    /// </summary>
    public static class ClassViewLogic
    {
        /// <summary>Every class in shop order.</summary>
        public static readonly string[] ClassIds = { ProfileRules.WarriorId, ProfileRules.MageId, ProfileRules.ArcherId };

        /// <summary>ML-Agents behavior name of a class (same as PersonalArena.ML.ClassRegistry); unknown ids give the Warrior.</summary>
        public static string BehaviorName(string classId)
        {
            switch (Normalize(classId))
            {
                case ProfileRules.MageId:
                    return "Mage";
                case ProfileRules.ArcherId:
                    return "Archer";
                default:
                    return "Warrior";
            }
        }

        /// <summary>The class id of a behavior name ("Mage" → "mage"); null for an unknown behavior.</summary>
        public static string ClassIdOfBehavior(string behavior)
        {
            if (string.IsNullOrEmpty(behavior))
            {
                return null;
            }

            foreach (string id in ClassIds)
            {
                if (string.Equals(BehaviorName(id), behavior.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return id;
                }
            }

            return null;
        }

        /// <summary>"Chiến binh", "Pháp sư", "Cung thủ".</summary>
        public static string DisplayName(string classId)
        {
            switch (Normalize(classId))
            {
                case ProfileRules.MageId:
                    return "Mage";
                case ProfileRules.ArcherId:
                    return "Archer";
                default:
                    return "Warrior";
            }
        }

        /// <summary>The display name in capitals for headers ("PHÁP SƯ").</summary>
        public static string UpperName(string classId)
        {
            switch (Normalize(classId))
            {
                case ProfileRules.MageId:
                    return "MAGE";
                case ProfileRules.ArcherId:
                    return "ARCHER";
                default:
                    return "WARRIOR";
            }
        }

        /// <summary>One line on the class's play style for the shop.</summary>
        public static string PlayStyle(string classId)
        {
            switch (Normalize(classId))
            {
                case ProfileRules.MageId:
                    return "Ranged spells and blast areas, low HP; has a magic shield, a blink and a frost nova.";
                case ProfileRules.ArcherId:
                    return "Shoots from afar and runs fast; rolls back to keep distance and pierces lines of enemies.";
                default:
                    return "Sturdy melee fighter with heavy armor; blocks, dashes and kicks to break out of a crowd.";
            }
        }

        /// <summary>The selected class id of the profile (the Warrior when unset or not owned).</summary>
        public static string SelectedClassId(PlayerProfile profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.SelectedClassId) ||
                ProfileRules.FindCharacter(profile, profile.SelectedClassId) == null)
            {
                return ProfileRules.WarriorId;
            }

            return profile.SelectedClassId;
        }

        /// <summary>The character record of the selected class (the Warrior when the selection is not owned).</summary>
        public static CharacterProfile SelectedCharacter(PlayerProfile profile)
        {
            if (profile == null)
            {
                return null;
            }

            return ProfileRules.FindCharacter(profile, SelectedClassId(profile)) ??
                ProfileRules.FindCharacter(profile, ProfileRules.WarriorId);
        }

        // ------------------------------------------------------------------ shop

        public static ClassShopState ShopState(PlayerProfile profile, string classId)
        {
            if (!ProfileRules.IsClassPlayable(classId))
            {
                return ClassShopState.Unavailable;
            }
            if (ProfileRules.OwnsClass(profile, classId))
            {
                return SelectedClassId(profile) == classId ? ClassShopState.Selected : ClassShopState.Owned;
            }

            long gold = profile != null ? profile.Gold : 0;
            return gold >= ProfileRules.ClassPrice(classId) ? ClassShopState.Buyable : ClassShopState.TooExpensive;
        }

        /// <summary>"Buy (1,500 gold)".</summary>
        public static string BuyLabel(string classId)
        {
            long price = Math.Max(0, ProfileRules.ClassPrice(classId));
            return "Buy (" + MetaViewLogic.FormatGold(price) + " gold)";
        }

        /// <summary>The state line under a class card.</summary>
        public static string ShopStatus(PlayerProfile profile, string classId)
        {
            switch (ShopState(profile, classId))
            {
                case ClassShopState.Selected:
                    return "Active";
                case ClassShopState.Owned:
                    return "Owned — press Select to watch and train it";
                case ClassShopState.Buyable:
                    return "Price " + MetaViewLogic.FormatGold(ProfileRules.ClassPrice(classId)) + " gold";
                case ClassShopState.TooExpensive:
                    long gold = profile != null ? profile.Gold : 0;
                    return "Short by " + MetaViewLogic.FormatGold(ProfileRules.ClassPrice(classId) - gold) + " gold";
                default:
                    return "Locked";
            }
        }

        /// <summary>Whether the buy button is shown, and enabled.</summary>
        public static bool ShowsBuy(ClassShopState state) => state == ClassShopState.Buyable || state == ClassShopState.TooExpensive;

        public static bool CanBuy(ClassShopState state) => state == ClassShopState.Buyable;

        /// <summary>Whether the "Chọn" button is enabled.</summary>
        public static bool CanSelect(ClassShopState state) => state == ClassShopState.Owned;

        /// <summary>The "Chọn" button label.</summary>
        public static string SelectLabel(ClassShopState state) => state == ClassShopState.Selected ? "Active" : "Select";

        // ------------------------------------------------------------------ training

        /// <summary>
        /// What TRAIN does: stop the running training of the selected class, stop-then-start when another class
        /// trains (<paramref name="runningBehavior"/> from the service status), else start when the service allows it.
        /// </summary>
        public static TrainButtonAction TrainAction(TrainingState state, string runningBehavior, string selectedBehavior)
        {
            switch (state)
            {
                case TrainingState.Starting:
                case TrainingState.Training:
                    return IsOtherClass(runningBehavior, selectedBehavior) ? TrainButtonAction.SwitchClass : TrainButtonAction.Stop;
                case TrainingState.Stopping:
                case TrainingState.External:
                case TrainingState.Unavailable:
                    return TrainButtonAction.None;
                default:
                    return TrainButtonAction.Start;
            }
        }

        /// <summary>True when a known running behavior is not the selected one (an unknown or empty one counts as the same).</summary>
        public static bool IsOtherClass(string runningBehavior, string selectedBehavior)
        {
            return !string.IsNullOrEmpty(runningBehavior) && !string.IsNullOrEmpty(selectedBehavior) &&
                !string.Equals(runningBehavior.Trim(), selectedBehavior.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The behavior a written service status names. A status without one comes from a pre-M7 service, which only
        /// ever trained the Warrior. No status yet (null) stays unknown.
        /// </summary>
        public static string StatusBehavior(bool hasStatus, string behavior)
        {
            if (!hasStatus) return null;
            return string.IsNullOrWhiteSpace(behavior) ? "Warrior" : behavior.Trim();
        }

        /// <summary>
        /// The --run-id TRAIN passes: the active branch when it can resume (<paramref name="resumableRunId"/>);
        /// else the character's own run id ("mage-s001") when the class has no current run yet and that folder does
        /// not exist, so a bought class's first run carries the profile's name; else null (the service then picks
        /// this class's newest run).
        /// </summary>
        public static string TrainRunId(string resumableRunId, string profileRunId, string classId, bool profileRunExists,
            bool classHasCurrentRun)
        {
            if (!string.IsNullOrEmpty(resumableRunId))
            {
                return resumableRunId;
            }

            if (profileRunExists || classHasCurrentRun || !TrainingServiceClient.IsSafeRunId(profileRunId))
            {
                return null;
            }

            return profileRunId.StartsWith(Normalize(classId) + "-", StringComparison.OrdinalIgnoreCase) ? profileRunId : null;
        }

        /// <summary>The TRAIN label while another class trains: "HUẤN LUYỆN PHÁP SƯ".</summary>
        public static string SwitchTrainLabel(string classId) => "TRAIN " + UpperName(classId);

        /// <summary>Notice while switching training to another class.</summary>
        public static string SwitchNotice(string runningBehavior, string classId)
        {
            string from = ClassIdOfBehavior(runningBehavior);
            return "Saving the progress of " + (from != null ? DisplayName(from) : runningBehavior) +
                ", then switching to train " + DisplayName(classId) + "...";
        }

        /// <summary>Training panel line when the running training is for another class than the watched one.</summary>
        public static string OtherClassTrainingLine(string runningBehavior, string classId)
        {
            string from = ClassIdOfBehavior(runningBehavior);
            return "Training " + (from != null ? DisplayName(from) : runningBehavior) + ", not " + DisplayName(classId) +
                ".\nPress to switch to " + DisplayName(classId) + ".";
        }

        /// <summary>"Chưa có não Mage — bấm HUẤN LUYỆN AI" (the Warrior keeps its older wording).</summary>
        public static string NoBrainText(string classId, bool canTrain)
        {
            string id = Normalize(classId);
            if (id == ProfileRules.WarriorId)
            {
                return canTrain
                    ? "The Warrior has no brain yet, so it stands idle.\nPress TRAIN AI to start teaching it."
                    : "Waiting for the AI to save its first brain...";
            }

            return "No brain for " + BehaviorName(id) + " (" + DisplayName(id) + ")" +
                (canTrain ? " yet — press TRAIN AI\nto start teaching it." : ".\nWaiting for the AI to save its first brain...");
        }

        /// <summary>The info header: "AI PHÁP SƯ".</summary>
        public static string AiTitle(string classId) => "AI " + UpperName(classId);

        private static string Normalize(string classId)
        {
            if (string.IsNullOrEmpty(classId))
            {
                return ProfileRules.WarriorId;
            }

            foreach (string id in ClassIds)
            {
                if (string.Equals(id, classId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return id;
                }
            }

            return ProfileRules.WarriorId;
        }
    }
}
