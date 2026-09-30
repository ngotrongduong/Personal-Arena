using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalArena.Core.Survivor;

namespace PersonalArena.ML
{
    /// <summary>
    /// The owner's build, tier and Training Focus passed by the trainer after <c>--env-args</c>
    /// (<c>--owner-build a,b,... --owner-tier N --training-focus id</c>, see Trainer/arena_trainer.py).
    /// Bad values never stop training: they are clamped or ignored, and each problem is listed in
    /// <see cref="Warnings"/>.
    /// </summary>
    public sealed class OwnerTrainingArgs
    {
        public const string BuildArgument = "--owner-build";
        public const string TierArgument = "--owner-tier";
        public const string FocusArgument = "--training-focus";

        /// <summary>The owner's build at the owner's tier, or null for random builds only.</summary>
        public CharacterBuild OwnBuild { get; private set; }
        public TrainingFocus Focus { get; private set; } = TrainingFocus.Balanced;
        public List<string> Warnings { get; } = new List<string>();

        public static OwnerTrainingArgs Parse(string[] args)
        {
            OwnerTrainingArgs result = new OwnerTrainingArgs();
            string build = Value(args, BuildArgument);
            string tier = Value(args, TierArgument);
            string focus = Value(args, FocusArgument);

            if (build != null)
            {
                result.OwnBuild = ParseBuild(build, result.Warnings);
                if (result.OwnBuild != null)
                {
                    result.OwnBuild.Tier = ParseTier(tier, result.Warnings);
                }
            }

            if (focus != null)
            {
                if (TrainingFocusInfo.TryParse(focus, out TrainingFocus parsed))
                {
                    result.Focus = parsed;
                }
                else
                {
                    result.Warnings.Add($"{FocusArgument} '{focus}' is unknown, using balanced.");
                }
            }

            return result;
        }

        /// <summary>One log line describing what training will use.</summary>
        public string Describe()
        {
            string focus = "focus " + TrainingFocusInfo.Id(Focus);
            if (OwnBuild == null)
            {
                return "Owner training: random builds only, " + focus + ".";
            }

            return "Owner training: owner build " + string.Join(",", OwnBuild.Points) + " at tier " + OwnBuild.Tier +
                " (level " + OwnBuild.Level + "), " + focus + ".";
        }

        private static CharacterBuild ParseBuild(string text, List<string> warnings)
        {
            string[] parts = text.Split(',');
            if (parts.Length != StatInfo.SlotCount)
            {
                warnings.Add($"{BuildArgument} needs {StatInfo.SlotCount} values, got {parts.Length}; ignored.");
                return null;
            }

            CharacterBuild build = new CharacterBuild();
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int points))
                {
                    warnings.Add($"{BuildArgument} value '{parts[i]}' is not an integer; ignored.");
                    return null;
                }

                int cap = i < StatInfo.UsedCount ? StatInfo.Cap((StatId)i) : 0;
                int clamped = Math.Max(0, Math.Min(cap, points));
                if (clamped != points)
                {
                    warnings.Add($"{BuildArgument} stat {(StatId)i} = {points} clamped to {clamped}.");
                }

                build.Points[i] = clamped;
            }

            return build;
        }

        private static int ParseTier(string text, List<string> warnings)
        {
            if (text == null)
            {
                return 1;
            }

            if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int tier))
            {
                warnings.Add($"{TierArgument} '{text}' is not an integer, using 1.");
                return 1;
            }

            int clamped = Math.Max(1, Math.Min(10, tier));
            if (clamped != tier)
            {
                warnings.Add($"{TierArgument} {tier} clamped to {clamped}.");
            }

            return clamped;
        }

        private static string Value(string[] args, string key)
        {
            if (args == null)
            {
                return null;
            }

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
