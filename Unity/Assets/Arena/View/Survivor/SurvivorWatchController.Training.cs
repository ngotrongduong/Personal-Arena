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
    /// <summary>The TRAIN button, its power setting and the training status text.</summary>
    public sealed partial class SurvivorWatchController
    {
        private void OnTrainingButton()
        {
            if (training == null)
            {
                return;
            }

            restartWithNewPower = false;
            switch (ClassViewLogic.TrainAction(trainingSnapshot.State, RunningBehavior, BehaviorName))
            {
                case TrainButtonAction.Stop:
                    pendingClassStart = false;
                    training.RequestStop();
                    stopRequestedAt = Time.unscaledTime;
                    trainingNotice = null;
                    break;
                case TrainButtonAction.SwitchClass:
                    // M7: another class is training. Stop it gracefully (it saves a checkpoint), then start this class.
                    pendingClassStart = true;
                    switchFromBehavior = RunningBehavior;
                    training.RequestStop();
                    stopRequestedAt = Time.unscaledTime;
                    trainingNotice = null;
                    break;
                case TrainButtonAction.Start:
                    pendingClassStart = false;
                    StartTraining();
                    break;
            }

            PollTraining();
        }

        private void StartTraining()
        {
            // The owner's build and tier (once the selected character has a level or a tier above 1) and the training focus.
            OwnerTraining owner = MetaViewLogic.OwnerTrainingFor(store.Profile);
            // M6: continue the active branch when it has a checkpoint. M7: a class's first training uses its
            // character's run id (mage-s001); otherwise null and the service picks this class's newest run.
            CharacterProfile character = CurrentCharacter;
            string profileRunId = character != null ? character.BrainRunId : null;
            string runId = ClassViewLogic.TrainRunId(
                LineageStore.TrainRunId(runsDirectory, BehaviorName, profileRunId),
                profileRunId,
                classId,
                TrainingServiceClient.IsSafeRunId(profileRunId) && Directory.Exists(Path.Combine(runsDirectory, profileRunId)),
                BrainLocator.FindNewestRunDirectory(runsDirectory, BehaviorName) != null);
            trainingNotice = training.Start(System.Diagnostics.Process.GetCurrentProcess().Id, Powers[powerIndex], BehaviorName, owner, runId);
            startedTraining = trainingNotice == null;
            if (startedTraining)
            {
                startedOwner = owner;
            }
            stopRequestedAt = float.NegativeInfinity;
        }

        private void OnPowerButton()
        {
            if (training == null)
            {
                return;
            }

            powerIndex = (powerIndex + 1) % Powers.Length;
            PlayerPrefs.SetInt(PowerPreference, powerIndex);
            PlayerPrefs.Save();
            // Running training picks up the new power after a save-and-restart (not when it trains another
            // class: a restart would start the class on screen instead; the power applies to the next TRAIN).
            if (trainingSnapshot.IsActive && trainingSnapshot.State != TrainingState.Stopping && !TrainingOtherClass)
            {
                restartWithNewPower = true;
                training.RequestStop();
                stopRequestedAt = Time.unscaledTime;
            }

            PollTraining();
        }

        private static string PowerLabel(TrainingPower power)
        {
            return "Power: " + power.Name + " - " + power.Fighters + " arenas at once     change >";
        }

        private void PollTraining()
        {
            nextTrainingPoll = Time.unscaledTime + TrainingPollSeconds;
            trainingSnapshot = training.Read();
            if (restartWithNewPower && !trainingSnapshot.IsActive)
            {
                restartWithNewPower = false;
                if (trainingSnapshot.State == TrainingState.Stopped || trainingSnapshot.State == TrainingState.Idle)
                {
                    StartTraining();
                    trainingSnapshot = training.Read();
                }
            }

            if (pendingClassStart && !trainingSnapshot.IsActive)
            {
                // M7: the other class's training has saved and quit; start the class on screen.
                pendingClassStart = false;
                if (trainingSnapshot.State == TrainingState.Stopped || trainingSnapshot.State == TrainingState.Idle)
                {
                    StartTraining();
                    trainingSnapshot = training.Read();
                }
            }

            TrainingStatus status = trainingSnapshot.Status;
            bool otherClass = TrainingOtherClass;
            if (historyPanel != null && trainingSnapshot.IsActive && !otherClass && status != null && !string.IsNullOrEmpty(status.run_id))
            {
                string activeRun = Path.Combine(runsDirectory, status.run_id);
                if (Directory.Exists(activeRun))
                {
                    historyPanel.SetRunDirectory(activeRun);
                }
            }

            CultureInfo culture = CultureInfo.InvariantCulture;
            bool stopAsked = Time.unscaledTime - stopRequestedAt < StopFeedbackSeconds;
            string lastRun = status != null && status.step > 0
                ? "\nLast time " + status.run_id + ": " + status.step.ToString("N0", culture) + " steps"
                : string.Empty;
            string text;
            switch (trainingSnapshot.State)
            {
                case TrainingState.Unavailable:
                    hud.SetTrainingButton("TRAIN AI", false, TrainColor);
                    text = "This machine cannot train yet:\n" + training.MissingPiece();
                    break;
                case TrainingState.Starting when otherClass:
                case TrainingState.Training when otherClass:
                    // M7: another class trains; TRAIN stops it (saving) and then trains the class on screen.
                    hud.SetTrainingButton(stopAsked ? "STOPPING..." : ClassViewLogic.SwitchTrainLabel(classId), !stopAsked, TrainColor);
                    text = ClassViewLogic.OtherClassTrainingLine(RunningBehavior, classId) +
                        (status.step > 0 ? "\n" + status.run_id + ": step " + status.step.ToString("N0", culture) : string.Empty);
                    break;
                case TrainingState.Starting:
                    hud.SetTrainingButton(stopAsked ? "STOPPING..." : "STOP TRAINING", !stopAsked, StopColor);
                    text = status != null && status.message != null && status.message.StartsWith("The trainer crashed", StringComparison.Ordinal)
                        ? "The trainer crashed and will restart by itself.\nProgress is kept."
                        : "Starting... loading the training arenas\n(about a minute). " + ClassViewLogic.DisplayName(classId) +
                          " here will\nupdate itself as the AI improves.";
                    break;
                case TrainingState.Training:
                    hud.SetTrainingButton(stopAsked ? "STOPPING..." : "STOP TRAINING", !stopAsked, StopColor);
                    text = "Training " + status.run_id +
                        "\nStep " + status.step.ToString("N0", culture) +
                        (status.has_reward ? "    Avg reward " + status.mean_reward.ToString("0.0", culture) : string.Empty) +
                        (status.num_envs > 0
                            ? "\n" + (status.num_envs * status.arena_agents) + " arenas training at once (" +
                              status.num_envs + " game x " + status.arena_agents + ")" + (status.cpu ? " on CPU" : string.Empty)
                            : string.Empty) +
                        "\nThis session " + FormatDuration(status.session_seconds);
                    break;
                case TrainingState.Stopping:
                    hud.SetTrainingButton("SAVING...", false, StopColor);
                    text = "Saving the AI's progress, please wait...";
                    break;
                case TrainingState.External:
                    hud.SetTrainingButton("TRAINING (EXTERNAL)", false, StopColor);
                    text = "Training was started outside the game.\n" + ClassViewLogic.DisplayName(classId) +
                        " here still updates itself\nwhenever a new brain is saved.";
                    break;
                case TrainingState.Stopped:
                    hud.SetTrainingButton("TRAIN AI", true, TrainColor);
                    text = "Stopped; progress saved.\nPress to continue training." + lastRun;
                    break;
                case TrainingState.Error:
                    hud.SetTrainingButton("TRAIN AI", true, TrainColor);
                    text = "Error: " + (status != null ? status.message : string.Empty) + "\nPress to retry.";
                    break;
                default:
                    hud.SetTrainingButton("TRAIN AI", true, TrainColor);
                    text = "Press to let the AI keep learning in the\nbackground while you watch it play." + lastRun;
                    break;
            }

            if (trainingSnapshot.State != TrainingState.Unavailable && trainingSnapshot.State != TrainingState.External && !otherClass)
            {
                text += "\n" + TrainingChoicesText(trainingSnapshot.State, status);
            }

            if (restartWithNewPower)
            {
                text = "Changing power to " + Powers[powerIndex].Name + ":\nsaving progress, then restarting...";
            }
            if (pendingClassStart)
            {
                text = ClassViewLogic.SwitchNotice(switchFromBehavior, classId);
            }
            if (!string.IsNullOrEmpty(trainingNotice))
            {
                text = trainingNotice;
            }

            hud.SetTrainingPower(PowerLabel(Powers[powerIndex]), trainingSnapshot.State != TrainingState.External &&
                trainingSnapshot.State != TrainingState.Unavailable && trainingSnapshot.State != TrainingState.Stopping);
            hud.SetTrainingText(text);
            float[] rewards = status != null ? status.rewards : null;
            string caption = rewards != null && rewards.Length > 0 && status.steps != null && status.steps.Length > 0
                ? "Avg reward, steps 0 - " + FormatSteps(status.steps[status.steps.Length - 1]) + " (higher = better)"
                : "The reward chart appears once training starts";
            hud.SetTrainingGraph(rewards, caption);
        }

        /// <summary>
        /// The build line of the training panel: what the running session learns (as reported by the service),
        /// plus a note when the owner's choices changed since; otherwise what the next session will learn.
        /// </summary>
        private string TrainingChoicesText(TrainingState state, TrainingStatus status)
        {
            OwnerTraining current = MetaViewLogic.OwnerTrainingFor(store.Profile);
            bool running = state == TrainingState.Starting || state == TrainingState.Training;
            if (running && status != null)
            {
                string line = MetaViewLogic.TrainingBuildLine(status.owner_build, status.owner_tier, status.training_focus);
                if (MetaViewLogic.TrainingChoicesDiffer(status.owner_build, status.owner_tier, status.training_focus, startedOwner, current))
                {
                    line += "\nBuild/focus changes apply from the next TRAIN";
                }

                return line;
            }

            return MetaViewLogic.TrainingBuildLine(current.Points != null, current.Tier, current.FocusId);
        }

        private static string FormatDuration(float seconds)
        {
            int whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return whole >= 3600
                ? (whole / 3600) + "h " + (whole % 3600 / 60).ToString("00") + "m"
                : (whole / 60) + "m " + (whole % 60).ToString("00") + "s";
        }

        private static string FormatSteps(long steps)
        {
            CultureInfo culture = CultureInfo.InvariantCulture;
            if (steps >= 1000000)
            {
                return (steps / 1e6).ToString("0.0", culture) + "M";
            }
            return steps >= 1000 ? (steps / 1000).ToString(culture) + "k" : steps.ToString(culture);
        }
    }
}
