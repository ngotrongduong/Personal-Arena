using System;
using System.Globalization;
using System.Text;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Meta
{
    /// <summary>
    /// GDD §3.1 "measure first": one CSV line per finished run (watch or farm). Core only formats;
    /// the View appends the lines to a file (writing <see cref="Header"/> first when the file is new).
    /// </summary>
    public static class EconomyLog
    {
        public const string WatchMode = "watch";
        public const string FarmMode = "farm";
        public const string TimeFormat = "yyyy-MM-ddTHH:mm:ssZ";

        public static string Header { get; } =
            "time,mode,class,tier,loadout,survived_s,end_reason,death_cause,level,kills,gold," +
            "gold_normal,gold_elite,gold_boss,gold_chest,gold_filler,total_xp,gold_per_min,stat_points";

        public static string Line(DateTime utc, string mode, string classId, int tier, string loadoutName, CharacterBuild build, SurvivorRunStats s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            CultureInfo inv = CultureInfo.InvariantCulture;
            StringBuilder line = new StringBuilder(160);
            DateTime time = utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : utc;
            line.Append(time.ToString(TimeFormat, inv)).Append(',');
            line.Append(Clean(mode)).Append(',');
            line.Append(Clean(classId)).Append(',');
            line.Append(tier.ToString(inv)).Append(',');
            line.Append(Clean(loadoutName)).Append(',');
            line.Append(Number(s.SurvivedSeconds)).Append(',');
            line.Append(s.EndReason.ToString()).Append(',');
            line.Append(s.DeathCause.ToString()).Append(',');
            line.Append(s.Level.ToString(inv)).Append(',');
            line.Append(s.Kills.ToString(inv)).Append(',');
            line.Append(Number(s.Gold)).Append(',');
            for (int i = 0; i < SurvivorSim.GoldSourceCount; i++)
            {
                float value = s.GoldBySource != null && i < s.GoldBySource.Length ? s.GoldBySource[i] : 0f;
                line.Append(Number(value)).Append(',');
            }
            line.Append(Number(s.TotalXp)).Append(',');
            float goldPerMinute = s.SurvivedSeconds > 0f ? s.Gold / (s.SurvivedSeconds / 60f) : 0f;
            line.Append(Number(goldPerMinute)).Append(',');
            for (int i = 0; i < StatInfo.UsedCount; i++)
            {
                if (i > 0) line.Append(';');
                int points = build != null && i < build.Points.Length ? build.Points[i] : 0;
                line.Append(points.ToString(inv));
            }
            return line.ToString();
        }

        private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>Commas, semicolons, quotes and line breaks become spaces so the CSV never breaks.</summary>
        private static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            StringBuilder clean = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                clean.Append(c == ',' || c == ';' || c == '"' || c == '\r' || c == '\n' ? ' ' : c);
            }
            return clean.ToString().Trim();
        }
    }
}
