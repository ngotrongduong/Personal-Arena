using System;
using System.Collections.Generic;
using System.Text;
using PersonalArena.Core.Survivor;

namespace PersonalArena.View
{
    /// <summary>
    /// Vietnamese text of the post-run story (<see cref="RunChronicle"/>, data from Core). When a run has more
    /// entries than fit, the most important ones are kept (End, BossKilled, BossSpawned, NearDeath, ItemMaxed
    /// first) and shown in time order. Pure: EditMode tested.
    /// </summary>
    public static class ChronicleText
    {
        public const int DefaultMaxLines = 10;
        public const string EmptyText = "No events yet.";

        /// <summary>Lower is more important.</summary>
        public static int Priority(ChronicleKind kind)
        {
            switch (kind)
            {
                case ChronicleKind.End: return 0;
                case ChronicleKind.BossKilled: return 1;
                case ChronicleKind.BossSpawned: return 2;
                case ChronicleKind.NearDeath: return 3;
                case ChronicleKind.ItemMaxed: return 4;
                case ChronicleKind.StyleChange: return 5;
                case ChronicleKind.NewItem: return 6;
                case ChronicleKind.ChestOpened: return 7;
                case ChronicleKind.EliteKilled: return 8;
                default: return 9;
            }
        }

        /// <summary>Up to <paramref name="maxLines"/> story lines ("mm:ss  text"), one per line.</summary>
        public static string Format(RunChronicle chronicle, int maxLines = DefaultMaxLines)
        {
            if (chronicle == null || chronicle.Entries.Count == 0 || maxLines <= 0)
            {
                return EmptyText;
            }

            List<ChronicleEntry> picked = Select(chronicle.Entries, maxLines);
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < picked.Count; i++)
            {
                if (i > 0)
                {
                    text.Append('\n');
                }
                text.Append(Line(picked[i], chronicle.EndReason));
            }

            return text.ToString();
        }

        /// <summary>The most important <paramref name="max"/> entries (ties: earlier first), returned in time order.</summary>
        public static List<ChronicleEntry> Select(IReadOnlyList<ChronicleEntry> entries, int max)
        {
            List<ChronicleEntry> result = new List<ChronicleEntry>();
            if (entries == null || max <= 0)
            {
                return result;
            }

            List<int> order = new List<int>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                order.Add(i);
            }

            if (order.Count > max)
            {
                order.Sort((a, b) =>
                {
                    int byPriority = Priority(entries[a].Kind).CompareTo(Priority(entries[b].Kind));
                    return byPriority != 0 ? byPriority : a.CompareTo(b);
                });
                order.RemoveRange(max, order.Count - max);
                order.Sort((a, b) =>
                {
                    int byTime = entries[a].Time.CompareTo(entries[b].Time);
                    return byTime != 0 ? byTime : a.CompareTo(b);
                });
            }

            for (int i = 0; i < order.Count; i++)
            {
                result.Add(entries[order[i]]);
            }

            return result;
        }

        /// <summary>"mm:ss  text".</summary>
        public static string Line(ChronicleEntry entry, EndReason endReason)
        {
            return SurvivorViewLogic.FormatClock(entry.Time) + "  " + Describe(entry, endReason);
        }

        public static string Describe(ChronicleEntry entry, EndReason endReason)
        {
            switch (entry.Kind)
            {
                case ChronicleKind.NewItem:
                    return "Got " + ItemName(entry.ItemIndex);
                case ChronicleKind.ItemMaxed:
                    return ItemName(entry.ItemIndex) + " reached max level";
                case ChronicleKind.StyleChange:
                    string label = SpectatorLabels.DisplayName(entry.Label);
                    return string.IsNullOrEmpty(label) ? "Changed play style" : "Switched to " + label;
                case ChronicleKind.NearDeath:
                    return NearDeathText(entry);
                case ChronicleKind.EliteKilled:
                    return "Killed an elite";
                case ChronicleKind.ChestOpened:
                    return ChestText(entry);
                case ChronicleKind.BossSpawned:
                    return "The boss appeared";
                case ChronicleKind.BossKilled:
                    return "Boss killed!";
                case ChronicleKind.End:
                    return EndText(endReason, entry.Cause);
                default:
                    return string.Empty;
            }
        }

        private static string NearDeathText(ChronicleEntry entry)
        {
            float ratio = Math.Max(0f, Math.Min(1f, entry.Value));
            int percent = (int)Math.Round(ratio * 100f);
            if (percent == 0 && ratio > 0f)
            {
                percent = 1;
            }

            string cause = NearDeathCause(entry.Cause);
            return "Near death (" + percent + "% HP" + (cause.Length > 0 ? ", " + cause : string.Empty) + ")";
        }

        private static string NearDeathCause(DeathCause cause)
        {
            switch (cause)
            {
                case DeathCause.Surrounded: return "surrounded";
                case DeathCause.Boss: return "hit by the Boss";
                case DeathCause.Brute: return "hit by a Brute";
                case DeathCause.Contact: return "clawed by enemies";
                case DeathCause.Projectile: return "hit by a projectile";
                case DeathCause.Explosion: return "caught by an Exploder";
                default: return string.Empty;
            }
        }

        private static string ChestText(ChronicleEntry entry)
        {
            ItemDef def = SurvivorCatalog.Get(entry.ItemIndex);
            string gold = entry.Value > 0f ? "+" + MetaViewLogic.FormatGold((long)Math.Floor(entry.Value)) + " gold" : string.Empty;
            if (def != null && def.Kind != ItemKind.Filler && entry.Level > 0)
            {
                return "Opened a chest — " + def.Name + " level " + entry.Level + (gold.Length > 0 ? " (" + gold + ")" : string.Empty);
            }

            return "Opened a chest" + (gold.Length > 0 ? " — " + gold : string.Empty);
        }

        private static string EndText(EndReason reason, DeathCause cause)
        {
            switch (reason)
            {
                case EndReason.Won: return "Victory!";
                case EndReason.Died: return "Defeated (" + SurvivorViewLogic.DeathCauseText(cause) + ")";
                case EndReason.TimeUp: return "Time up";
                case EndReason.Expired: return "Boss still alive — time up";
                default: return "Run ended";
            }
        }

        private static string ItemName(int index)
        {
            ItemDef def = SurvivorCatalog.Get(index);
            return def != null && !string.IsNullOrEmpty(def.Name) ? def.Name : "gear";
        }
    }
}
