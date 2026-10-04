using System;
using System.Collections.Generic;
using PersonalArena.Core.Meta;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.UI;

namespace PersonalArena.View
{
    /// <summary>
    /// Auto Farm panel (key F): the AI plays hidden, fast matches with the owner's build on one background
    /// thread (<see cref="AutoFarmRunner"/>) and the gold goes to the wallet. The session keeps running while
    /// the panel is closed; it is booked on the main thread when it ends, and on quit (cancel, wait ≤ 2 s,
    /// book what finished).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class AutoFarmPanel : MetaPanel
    {
        public const int QuitWaitMilliseconds = 2000;
        private static readonly int[] RunChoices = { 10, 25, 50, 100 };
        private const float ProgressWidth = 1030f;
        private const float RefreshSeconds = 0.25f;

        private ProfileStore store;
        private Func<byte[]> brainBytes;
        private Func<string> brainName;
        private AutoFarmRunner runner;
        private int runnerLoadout;
        private string runnerLoadoutName;
        private int choiceIndex;
        private FarmSummary lastSummary;
        private string startError;
        private bool shutDown;
        private float nextRefresh;

        private Text buildText;
        private readonly UiButton[] choiceButtons = new UiButton[RunChoices.Length];
        private UiButton startButton;
        private Text reasonText;
        private Image progressFill;
        private Text progressText;
        private Text resultText;
        private Text errorText;

        /// <summary>Raised after a finished session was booked into the profile (and saved).</summary>
        public event Action ProfileChanged;

        protected override string Title => "AUTO GOLD FARM";
        protected override Vector2 CardSize => new Vector2(1100f, 760f);

        public bool IsFarming => runner != null && !runner.IsRecorded;

        /// <summary>One short status line for the HUD while a session runs, else null.</summary>
        public string StatusLine
        {
            get
            {
                if (runner == null || runner.IsRecorded)
                {
                    return null;
                }

                return "Gold farm: " + runner.Completed + "/" + runner.RunCount + " runs · +" +
                    MetaViewLogic.FormatGold((long)Math.Floor(runner.TotalGold)) + " gold";
            }
        }

        /// <param name="brain">Bytes of the brain file the viewer currently plays with, or null.</param>
        /// <param name="brainLabel">Short name of that brain for the panel.</param>
        public void Bind(ProfileStore profileStore, Func<byte[]> brain, Func<string> brainLabel)
        {
            store = profileStore;
            brainBytes = brain;
            brainName = brainLabel;
            Refresh();
        }

        /// <summary>Starts a session with <paramref name="runs"/> matches (used by the F panel and screenshot mode).</summary>
        public bool StartFarm(int runs)
        {
            if (runner != null && !runner.IsRecorded)
            {
                return false;
            }

            startError = null;
            string reason = StartBlockReason();
            if (reason != null)
            {
                startError = reason;
                Refresh();
                return false;
            }

            CharacterProfile warrior = store.Selected;
            PlayerProfile profile = store.Profile;
            CharacterBuild build = ProfileRules.ToBuild(warrior, profile.SelectedTier);
            int baseSeed = Environment.TickCount & 0x3fffffff;
            try
            {
                // M7: the matches use the selected class's kit (the brain the viewer loaded is that class's brain).
                runner = new AutoFarmRunner(brainBytes(), build, Mathf.Clamp(runs, 1, 100), baseSeed, 900f, warrior.ClassId);
            }
            catch (ArgumentException exception)
            {
                runner = null;
                startError = "Cannot farm with this brain: " + exception.Message;
                Refresh();
                return false;
            }

            runnerLoadout = warrior.ActiveLoadout;
            runnerLoadoutName = MetaViewLogic.LoadoutName(warrior, runnerLoadout);
            lastSummary = null;
            runner.Start();
            Refresh();
            return true;
        }

        public void StopFarm()
        {
            runner?.Cancel();
            Refresh();
        }

        /// <summary>Quit path: cancel, wait up to 2 s for the thread, then book what finished. Safe to call twice.</summary>
        public void ShutDown()
        {
            if (shutDown)
            {
                return;
            }

            shutDown = true;
            if (runner == null || runner.IsRecorded)
            {
                return;
            }

            runner.Cancel();
            runner.Wait(QuitWaitMilliseconds);
            Book();
        }

        protected override void OnOpened()
        {
            Refresh();
        }

        protected override void Update()
        {
            base.Update();
            if (runner != null && !runner.IsRecorded && runner.IsDone)
            {
                Book();
            }

            if (IsOpen && Time.unscaledTime >= nextRefresh)
            {
                Refresh();
            }
        }

        private void OnApplicationQuit()
        {
            ShutDown();
        }

        protected override void BuildContent(RectTransform card)
        {
            Text intro = PlaceText("Intro", card, 18, TextAnchor.UpperLeft, Muted, 34f, -76f, 1030f, 50f, false, true);
            intro.text = "The AI plays hidden runs (not drawn, as fast as possible) with the loadout and tier you selected. " +
                "The gold goes to your real wallet. You can keep watching the AI play while it farms.";
            buildText = PlaceText("Build", card, 18, TextAnchor.UpperLeft, Color.white, 34f, -134f, 1030f, 50f, false, true);

            Text countLabel = PlaceText("Count Label", card, 19, TextAnchor.MiddleLeft, Color.white, 34f, -196f, 130f, 48f, true);
            countLabel.text = "Runs:";
            for (int i = 0; i < RunChoices.Length; i++)
            {
                int index = i;
                choiceButtons[i] = CreateButton("Runs " + RunChoices[i], card, RunChoices[i] + " runs", 18,
                    170f + i * 128f, -196f, 118f, 48f, () => OnChoose(index));
            }

            startButton = CreateButton("Start", card, "Start", 24, 34f, -266f, 360f, 64f, OnStartStop);
            reasonText = PlaceText("Reason", card, 17, TextAnchor.MiddleLeft, Warn, 412f, -266f, 650f, 64f, false, true);

            CreateBar("Progress", card, 34f, -352f, ProgressWidth, 26f, Good, out progressFill);
            progressText = PlaceText("Progress Text", card, 19, TextAnchor.UpperLeft, Color.white, 34f, -390f, 1030f, 56f, false, true);

            Text resultHeader = PlaceText("Result Header", card, 20, TextAnchor.UpperLeft, Gold, 34f, -462f, 1030f, 28f, true);
            resultHeader.text = "Latest farm result";
            resultText = PlaceText("Result", card, 19, TextAnchor.UpperLeft, Color.white, 34f, -496f, 1030f, 110f, false, true);
            errorText = PlaceText("Error", card, 18, TextAnchor.UpperLeft, Bad, 34f, -614f, 1030f, 60f, false, true);

            Text footer = PlaceText("Footer", card, 16, TextAnchor.UpperLeft, Muted, 34f, -700f, 1030f, 44f, false, true);
            footer.text = "The farm uses the brain loaded when you press Start, even if a newer one arrives. Quitting the game stops the farm; finished runs still count.";
        }

        private void Refresh()
        {
            nextRefresh = Time.unscaledTime + RefreshSeconds;
            if (!IsBuilt || store == null)
            {
                return;
            }

            PlayerProfile profile = store.Profile;
            CharacterProfile warrior = store.Selected;
            bool running = runner != null && !runner.IsRecorded;

            string brain = brainName != null ? brainName() : null;
            if (running)
            {
                buildText.text = "Farming " + ClassViewLogic.DisplayName(runner.ClassId) + " with loadout \"" + runnerLoadoutName + "\" at tier " + runner.Tier + " (" +
                    MetaViewLogic.TierGoldText(runner.Tier) + ").";
            }
            else if (warrior != null)
            {
                int tier = Mathf.Clamp(profile.SelectedTier, 1, ProfileRules.MaxTier);
                buildText.text = ClassViewLogic.DisplayName(warrior.ClassId) + "   Loadout: " + MetaViewLogic.LoadoutName(warrior, warrior.ActiveLoadout) + " (" +
                    MetaViewLogic.CompactPoints(warrior.Loadouts[warrior.ActiveLoadout].Points, 3) + ")   Tier " + tier + " (" +
                    MetaViewLogic.TierGoldText(tier) + ")" + (string.IsNullOrEmpty(brain) ? string.Empty : "\nBrain: " + brain);
            }

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                choiceButtons[i].Set(null, i == choiceIndex ? ButtonActive : ButtonColor, !running);
            }

            string reason = running ? null : StartBlockReason();
            if (running)
            {
                bool stopping = runner.IsCancelled;
                startButton.Set(stopping ? "Stopping..." : "Stop", ButtonStop, !stopping);
                reasonText.text = stopping ? "The current run will finish, then the farm stops." : string.Empty;
            }
            else
            {
                startButton.Set("Start", ButtonGo, reason == null);
                reasonText.text = reason ?? startError ?? string.Empty;
            }

            if (runner != null)
            {
                int completed = runner.Completed;
                SetBar(progressFill, ProgressWidth, runner.RunCount > 0 ? (float)completed / runner.RunCount : 0f);
                float left = runner.EstimatedSecondsLeft;
                progressText.text = "Finished " + completed + "/" + runner.RunCount + " runs     Gold: +" +
                    MetaViewLogic.FormatGold((long)Math.Floor(runner.TotalGold)) +
                    (running
                        ? "     Time left ~" + (left < 0f ? "estimating..." : MetaViewLogic.FormatDuration(left))
                        : "     Time " + MetaViewLogic.FormatDuration(runner.ElapsedSeconds));
            }
            else
            {
                SetBar(progressFill, ProgressWidth, 0f);
                progressText.text = "No farm yet in this session.";
            }

            resultText.text = lastSummary != null
                ? MetaViewLogic.FarmSummaryText(lastSummary) + "\nWallet: " + MetaViewLogic.FormatGold(profile.Gold) + " gold"
                : running ? "Farming... the result shows when it is done." : "None yet.";

            // FarmSession.Error: shown as soon as the worker stops on it (also part of the summary).
            string error = runner != null && runner.IsDone ? runner.Error : null;
            errorText.text = string.IsNullOrEmpty(error) ? string.Empty : "Farm error: " + error;
        }

        /// <summary>Why a session cannot start now, or null.</summary>
        private string StartBlockReason()
        {
            if (store == null || store.Selected == null)
            {
                return "The profile has not been read yet.";
            }

            byte[] bytes = brainBytes != null ? brainBytes() : null;
            if (bytes == null || bytes.Length == 0)
            {
                return "No brain to farm with yet. Press TRAIN AI first, then wait for the AI to save its first brain.";
            }

            return null;
        }

        private void OnChoose(int index)
        {
            if (runner != null && !runner.IsRecorded)
            {
                return;
            }

            choiceIndex = Mathf.Clamp(index, 0, RunChoices.Length - 1);
            Refresh();
        }

        private void OnStartStop()
        {
            if (runner != null && !runner.IsRecorded)
            {
                StopFarm();
            }
            else
            {
                StartFarm(RunChoices[choiceIndex]);
            }
        }

        /// <summary>Books the session into the profile on the main thread, saves, and writes one economy line per match.</summary>
        private void Book()
        {
            if (runner == null || runner.IsRecorded || store == null)
            {
                return;
            }

            // Book into the character whose kit the session played, even if the owner switched class meanwhile.
            CharacterProfile warrior = ProfileRules.FindCharacter(store.Profile, runner.ClassId) ?? store.Selected;
            List<string> lines = new List<string>();
            lastSummary = runner.Record(store.Profile, warrior, runnerLoadout, runnerLoadoutName, lines);
            store.Save();
            store.AppendEconomyLines(lines);
            if (!string.IsNullOrEmpty(lastSummary.Error))
            {
                Debug.LogWarning("Auto Farm stopped: " + lastSummary.Error);
            }

            Debug.Log("Auto Farm booked " + lastSummary.Runs + " runs, +" + lastSummary.Gold + " gold.");
            ProfileChanged?.Invoke();
            Refresh();
        }
    }
}
