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
    /// <summary>Automated screenshots for checks of the build (-screenshot, -openPanel, -endShot and friends).</summary>
    public sealed partial class SurvivorWatchController
    {
        private void UpdateScreenshots()
        {
            if (string.IsNullOrEmpty(screenshotPath))
            {
                return;
            }

            if (Time.unscaledTime >= quitAt)
            {
                quitAt = float.PositiveInfinity;
                Debug.Log("Screenshot mode finished.");
                Application.Quit();
                return;
            }

            float real = Time.realtimeSinceStartup;
            if (!string.IsNullOrEmpty(openPanel))
            {
                UpdatePanelScreenshot(real);
                return;
            }

            if (showProfile)
            {
                // Profile check: one shot of the open AI profile, then (optionally) quit.
                if (!highlightShotTaken && real > 6f)
                {
                    highlightShotTaken = true;
                    Capture(screenshotPath);
                    if (quitAfterScreenshot)
                    {
                        quitAt = Time.unscaledTime + 1.5f;
                    }
                }
                return;
            }

            // The level-up highlight over a grown horde; after a while any highlight will do.
            if (!highlightShotTaken && highlight.Current == PickHighlight.Phase.Highlight && highlight.Progress > 0.3f &&
                (sim.Time >= 45f || real > 70f))
            {
                highlightShotTaken = true;
                Capture(screenshotPath);
            }
            else if (!hordeShotTaken && !highlight.BlocksSim && !sim.IsEnded &&
                (sim.AliveEnemyCount >= 55 || sim.Time >= 120f || real > 100f))
            {
                hordeShotTaken = true;
                Capture(SiblingPath(screenshotPath, "_horde"));
            }
            else if (!highlightShotTaken && real > 130f)
            {
                highlightShotTaken = true;
                Capture(screenshotPath);
            }

            // -labelShot: the spectator tag above the hero, once it has been visible for a second.
            bool labelVisible = labeler.Current != SpectatorLabel.None && !highlight.BlocksSim && !sim.IsEnded;
            labelSeenSince = labelVisible ? Mathf.Min(labelSeenSince, real) : float.PositiveInfinity;
            if (labelShot && !labelShotTaken && ((labelVisible && real - labelSeenSince >= 1f && sim.Time >= 20f) || real > 200f))
            {
                labelShotTaken = true;
                Capture(SiblingPath(screenshotPath, "_label"));
            }

            // -endShot: the end screen with the reward and the run story.
            if (endShot && !endShotTaken && sim.IsEnded && endTimer >= 1.5f)
            {
                endShotTaken = true;
                Capture(SiblingPath(screenshotPath, "_end"));
            }

            // -enemyShot: the new enemies on screen (two of the three M7 kinds alive), or give up after a while.
            if (enemyShot && !enemyShotTaken && !highlight.BlocksSim && !sim.IsEnded &&
                (NewEnemyKindsAlive() >= 2 || real > EnemyShotGiveUpSeconds))
            {
                enemyShotTaken = true;
                Capture(SiblingPath(screenshotPath, "_enemies"));
            }

            bool done = highlightShotTaken && hordeShotTaken && (!labelShot || labelShotTaken) && (!endShot || endShotTaken) &&
                (!enemyShot || enemyShotTaken);
            if (done && quitAfterScreenshot && float.IsPositiveInfinity(quitAt))
            {
                quitAt = Time.unscaledTime + 1.5f;
            }
        }

        /// <summary>-openPanel character|farm|compare|lineage|settings|codex[:page[:entry]]: open that panel (farm: start a session), capture it, then optionally quit.</summary>
        private void UpdatePanelScreenshot(float real)
        {
            string panel = openPanel.Trim().ToLowerInvariant();
            if (!panelOpened && real > PanelOpenSeconds)
            {
                panelOpened = true;
                switch (panel)
                {
                    case "farm":
                        hud.ToggleFarmPanel();
                        farmPanel.StartFarm(farmRuns);
                        break;
                    case "compare":
                        hud.ToggleComparePanel();
                        break;
                    case "lineage":
                        hud.ToggleLineagePanel();
                        break;
                    case "settings":
                        hud.ToggleSettingsPanel();
                        break;
                    default:
                        if (panel.StartsWith("codex", StringComparison.Ordinal))
                        {
                            // codex[:page[:entry]] with page 0..3 (weapons, passives, evolutions, skills).
                            string[] parts = panel.Split(':');
                            int.TryParse(parts.Length > 1 ? parts[1] : "0", NumberStyles.Integer, CultureInfo.InvariantCulture, out int page);
                            int.TryParse(parts.Length > 2 ? parts[2] : "0", NumberStyles.Integer, CultureInfo.InvariantCulture, out int entry);
                            hud.ToggleCodexPanel();
                            hud.CodexPanel.ShowForChecks(page, entry);
                        }
                        else
                        {
                            hud.ToggleCharacterPanel();
                        }
                        break;
                }
            }

            float shotAt = panel == "farm" ? FarmShotSeconds : PanelShotSeconds;
            if (panelOpened && !highlightShotTaken && real > shotAt)
            {
                highlightShotTaken = true;
                Capture(screenshotPath);
                if (quitAfterScreenshot)
                {
                    quitAt = Time.unscaledTime + 1.5f;
                }
            }
        }

        /// <summary>How many of the M7 enemy kinds (exploder, ghost, necromancer) are alive in the run.</summary>
        private int NewEnemyKindsAlive()
        {
            bool exploder = false;
            bool ghost = false;
            bool necromancer = false;
            IReadOnlyList<SurvivorEnemy> enemies = sim.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                SurvivorEnemy enemy = enemies[i];
                if (enemy == null || !enemy.Active)
                {
                    continue;
                }
                exploder |= enemy.TypeIndex == SurvivorDefaults.ExploderTypeIndex;
                ghost |= enemy.TypeIndex == SurvivorDefaults.GhostTypeIndex;
                necromancer |= enemy.TypeIndex == SurvivorDefaults.NecromancerTypeIndex;
            }
            return (exploder ? 1 : 0) + (ghost ? 1 : 0) + (necromancer ? 1 : 0);
        }

        private void Capture(string path)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("Screenshot " + path + " at run time " + SurvivorViewLogic.FormatClock(sim.Time) + ", level " + sim.Level +
                ", enemies " + sim.AliveEnemyCount + ", phase " + highlight.Current + ".");
        }

        private static string SiblingPath(string path, string suffix)
        {
            string extension = Path.GetExtension(path);
            return Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, Path.GetFileNameWithoutExtension(path) + suffix + extension);
        }
    }
}
