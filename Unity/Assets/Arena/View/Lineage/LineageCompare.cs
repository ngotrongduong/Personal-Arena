using System;
using System.Globalization;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>The numbers compared between two brain versions (in display order).</summary>
    public enum LineageMetric
    {
        MedianSurvival,
        P10Survival,
        WinRate,
        GoldPerMinute,
        XpPerMinute,
        MeanLevel,
        DamageTakenPerMinute,
        BossDamage,
        Score
    }

    /// <summary>Pure rules of the lineage compare view: labels, values, formatting and which side is better.</summary>
    public static class LineageCompare
    {
        public static readonly LineageMetric[] Metrics =
        {
            LineageMetric.MedianSurvival,
            LineageMetric.P10Survival,
            LineageMetric.WinRate,
            LineageMetric.GoldPerMinute,
            LineageMetric.XpPerMinute,
            LineageMetric.MeanLevel,
            LineageMetric.DamageTakenPerMinute,
            LineageMetric.BossDamage,
            LineageMetric.Score
        };

        public static string Label(LineageMetric metric)
        {
            switch (metric)
            {
                case LineageMetric.MedianSurvival: return "Sống (trung vị)";
                case LineageMetric.P10Survival: return "Sống (10% tệ nhất)";
                case LineageMetric.WinRate: return "Tỉ lệ thắng";
                case LineageMetric.GoldPerMinute: return "Vàng/phút";
                case LineageMetric.XpPerMinute: return "EXP/phút";
                case LineageMetric.MeanLevel: return "Cấp TB";
                case LineageMetric.DamageTakenPerMinute: return "Máu mất/phút";
                case LineageMetric.BossDamage: return "Sát thương trùm";
                case LineageMetric.Score: return "Điểm";
                default: return metric.ToString();
            }
        }

        /// <summary>Only damage taken is better when lower.</summary>
        public static bool LowerIsBetter(LineageMetric metric)
        {
            return metric == LineageMetric.DamageTakenPerMinute;
        }

        /// <summary>The metric of an evaluated version; false when the version has no evaluation.</summary>
        public static bool TryValue(LineageMetric metric, LineageVersion version, out float value)
        {
            value = 0f;
            if (version == null || !version.HasEvaluation)
            {
                return false;
            }

            ChampionSummary s = version.Summary;
            switch (metric)
            {
                case LineageMetric.MedianSurvival: value = s.MedianSurvivedSeconds; break;
                case LineageMetric.P10Survival: value = s.P10SurvivedSeconds; break;
                case LineageMetric.WinRate: value = s.WinRate; break;
                case LineageMetric.GoldPerMinute: value = s.GoldPerMinute; break;
                case LineageMetric.XpPerMinute: value = s.XpPerMinute; break;
                case LineageMetric.MeanLevel: value = s.MeanLevel; break;
                case LineageMetric.DamageTakenPerMinute: value = s.DamageTakenPerMinute; break;
                case LineageMetric.BossDamage: value = s.MeanBossDamageFraction; break;
                case LineageMetric.Score: value = version.Score; break;
                default: return false;
            }

            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static string Format(LineageMetric metric, float value)
        {
            switch (metric)
            {
                case LineageMetric.MedianSurvival:
                case LineageMetric.P10Survival:
                    return BehaviorProfilePanel.FormatSeconds(value);
                case LineageMetric.WinRate:
                case LineageMetric.BossDamage:
                    return Percent(value);
                case LineageMetric.MeanLevel:
                    return value.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');
                case LineageMetric.GoldPerMinute:
                case LineageMetric.XpPerMinute:
                case LineageMetric.DamageTakenPerMinute:
                    return value.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');
                default:
                    return Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>0..1 fraction as "45%".</summary>
        public static string Percent(float fraction)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(fraction) * 100f).ToString(CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>-1 when A is better, 1 when B is better, 0 for a tie.</summary>
        public static int Better(LineageMetric metric, float a, float b)
        {
            float tolerance = 1e-4f * Math.Max(1f, Math.Max(Math.Abs(a), Math.Abs(b)));
            if (Math.Abs(a - b) <= tolerance)
            {
                return 0;
            }

            bool aHigher = a > b;
            return LowerIsBetter(metric) ? (aHigher ? 1 : -1) : (aHigher ? -1 : 1);
        }

        /// <summary>Like <see cref="Better(LineageMetric, float, float)"/>; 0 when either side has no evaluation.</summary>
        public static int Better(LineageMetric metric, LineageVersion a, LineageVersion b)
        {
            return TryValue(metric, a, out float valueA) && TryValue(metric, b, out float valueB)
                ? Better(metric, valueA, valueB)
                : 0;
        }
    }
}
