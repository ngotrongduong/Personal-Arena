using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// -perfLog &lt;file.csv&gt;: a smoothness check of the built viewer. Every 5 real seconds it appends one line
    /// (sim time, enemies alive, average FPS, worst frame in ms, the hero's weapon ids). With -perfShots N it
    /// also saves a screenshot next to the log every N sim seconds. -perfSeconds S quits after S real seconds
    /// (default 300). The window stays as launched.
    /// </summary>
    public sealed partial class SurvivorWatchController
    {
        private const float PerfWindowSeconds = 5f;

        private string perfLogPath;
        private float perfShotEvery;
        private float perfQuitAfter = 300f;
        private float perfNextShot;
        private float perfClock;
        private float perfWorst;
        private int perfFrames;
        private int perfShotIndex;
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
            File.WriteAllText(perfLogPath, "simTime,enemies,avgFps,worstMs,weapons\n");
        }

        private void UpdatePerfLog()
        {
            if (string.IsNullOrEmpty(perfLogPath))
            {
                return;
            }

            float delta = Time.unscaledDeltaTime;
            perfFrames++;
            perfClock += delta;
            perfWorst = Mathf.Max(perfWorst, delta);
            if (perfClock >= PerfWindowSeconds)
            {
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

            if (Time.realtimeSinceStartup >= perfQuitAfter)
            {
                perfLogPath = null;
                Application.Quit();
            }
        }
    }
}
