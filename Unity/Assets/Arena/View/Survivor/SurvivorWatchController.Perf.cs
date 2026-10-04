using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// -perfLog &lt;file.csv&gt;: a smoothness check of the built viewer. Every 5 real seconds it appends one line
    /// (sim time, enemies alive, average FPS, worst frame in ms, the hero's weapon ids); the launch itself and
    /// every frame over 50 ms go to *_startup.csv with the steps behind them (<see cref="PerfTrace"/>). With -perfShots N it
    /// also saves a screenshot next to the log every N sim seconds. -perfSeconds S quits after S real seconds
    /// (default 300). The window stays as launched.
    /// </summary>
    public sealed partial class SurvivorWatchController
    {
        private const float PerfWindowSeconds = 5f;
        private const float SlowFrameSeconds = 0.05f;
        private const int LaunchFrames = 3;

        private string perfLogPath;
        private float perfShotEvery;
        private float perfQuitAfter = 300f;
        private float perfNextShot;
        private float perfClock;
        private float perfWorst;
        private int perfFrames;
        private int perfShotIndex;
        private bool fxDemo;
        private float fxDemoAt = -1f;
        private int fxDemoShots;
        private int fxDemoRound;
        private bool vfxGallery;
        private readonly StringBuilder perfLine = new StringBuilder(160);

        private void BeginPerfLog()
        {
            perfLogPath = CommandLineValue("-perfLog");
            if (string.IsNullOrEmpty(perfLogPath))
            {
                return;
            }

            float.TryParse(CommandLineValue("-perfShots"), NumberStyles.Float, CultureInfo.InvariantCulture, out perfShotEvery);
            if (float.TryParse(CommandLineValue("-perfSeconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds) &&
                seconds > 0f)
            {
                perfQuitAfter = seconds;
            }
            perfNextShot = perfShotEvery;
            vfxGallery = HasArgument("-vfxGallery");
            fxDemo = vfxGallery || HasArgument("-fxDemo");
            if (vfxGallery && int.TryParse(CommandLineValue("-vfxGalleryFrom"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int firstPage))
            {
                fxDemoRound = Mathf.Max(0, firstPage);
            }
            // -cameraAt x,z[,distance]: hold the camera on a map point (e.g. a corner) for the screenshots.
            string[] cameraAt = (CommandLineValue("-cameraAt") ?? string.Empty).Split(',');
            if (cameraAt.Length >= 2 &&
                float.TryParse(cameraAt[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float cameraX) &&
                float.TryParse(cameraAt[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float cameraZ))
            {
                float cameraDistance = 24f;
                if (cameraAt.Length >= 3)
                {
                    float.TryParse(cameraAt[2], NumberStyles.Float, CultureInfo.InvariantCulture, out cameraDistance);
                }
                followCamera.LookAtForChecks(new Vector3(cameraX, 0f, cameraZ), cameraDistance);
            }
            File.WriteAllText(perfLogPath, "simTime,enemies,avgFps,worstMs,weapons\n");
            File.WriteAllText(SiblingPath(perfLogPath, "_startup"), "step,atMs,tookMs\n");
        }

        private void UpdatePerfLog()
        {
            if (string.IsNullOrEmpty(perfLogPath))
            {
                return;
            }

            float delta = Time.unscaledDeltaTime;
            // Unity reports the launch (engine start, scene load) as the delta of frame 1 and the length of frame 1
            // two frames later. Those are loading time, not stutter: they go to the start-up trace only.
            bool launching = Time.frameCount <= LaunchFrames;
            if (launching || delta > SlowFrameSeconds)
            {
                PerfTrace.Mark("frame " + Time.frameCount.ToString(CultureInfo.InvariantCulture) + " delta " +
                    (delta * 1000f).ToString("0", CultureInfo.InvariantCulture));
            }
            if (!launching)
            {
                perfFrames++;
                perfClock += delta;
                perfWorst = Mathf.Max(perfWorst, delta);
            }
            if (perfClock >= PerfWindowSeconds)
            {
                string trace = PerfTrace.Drain();
                if (trace.Length > 0)
                {
                    File.AppendAllText(SiblingPath(perfLogPath, "_startup"), trace);
                }
                perfLine.Length = 0;
                perfLine.Append(sim.Time.ToString("0", CultureInfo.InvariantCulture)).Append(',')
                    .Append(sim.AliveEnemyCount).Append(',')
                    .Append((perfFrames / perfClock).ToString("0.0", CultureInfo.InvariantCulture)).Append(',')
                    .Append((perfWorst * 1000f).ToString("0", CultureInfo.InvariantCulture)).Append(',');
                for (int i = 0; i < sim.Inventory.WeaponCount; i++)
                {
                    perfLine.Append(i == 0 ? "" : " ").Append(sim.Inventory.WeaponAt(i));
                }
                perfLine.Append('\n');
                File.AppendAllText(perfLogPath, perfLine.ToString());
                perfFrames = 0;
                perfClock = 0f;
                perfWorst = 0f;
            }

            if (perfShotEvery > 0f && !sim.IsEnded && !highlight.BlocksSim)
            {
                if (sim.Time < perfNextShot - perfShotEvery)
                {
                    // A new run started.
                    perfNextShot = perfShotEvery;
                }
                if (sim.Time >= perfNextShot)
                {
                    perfNextShot += perfShotEvery;
                    perfShotIndex++;
                    Capture(SiblingPath(Path.ChangeExtension(perfLogPath, ".png"),
                        "_" + perfShotIndex.ToString("00", CultureInfo.InvariantCulture)));
                }
            }

            // -fxDemo: every new effect at once around the hero, captured shortly after it starts and again mid-way.
            if (fxDemo && !sim.IsEnded && !highlight.BlocksSim)
            {
                float real = Time.realtimeSinceStartup;
                if (fxDemoAt < 0f && sim.Time >= 4f)
                {
                    fxDemoAt = real;
                    fxDemoShots = 0;
                    if (vfxGallery)
                    {
                        survivorRenderer.PlayStoreGallery(survivorRenderer.HeroWorldPosition, fxDemoRound);
                    }
                    else
                    {
                        survivorRenderer.PlayFxDemo(survivorRenderer.HeroWorldPosition);
                        hud.BuffDemo = true;
                    }
                }
                else if (fxDemoAt >= 0f && fxDemoShots < 2 && real - fxDemoAt >= (fxDemoShots == 0 ? (vfxGallery ? 0.15f : 0.05f) : (vfxGallery ? 0.5f : 0.3f)))
                {
                    fxDemoShots++;
                    Capture(SiblingPath(Path.ChangeExtension(perfLogPath, ".png"),
                        "_fx" + fxDemoRound.ToString(CultureInfo.InvariantCulture) + (fxDemoShots == 1 ? "a" : "b")));
                }
                else if (fxDemoAt >= 0f && fxDemoShots >= 2 && real - fxDemoAt >= (vfxGallery ? 2.6f : 2.5f) && fxDemoRound < (vfxGallery ? (ArenaEffects.StoreCount - 1) / ArenaEffects.GalleryPerPage : 1))
                {
                    fxDemoRound++;
                    fxDemoAt = -1f;
                }
            }

            if (Time.realtimeSinceStartup >= perfQuitAfter)
            {
                perfLogPath = null;
                Application.Quit();
            }
        }
    }
}
