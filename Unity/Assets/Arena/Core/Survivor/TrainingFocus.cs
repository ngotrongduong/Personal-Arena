namespace PersonalArena.Core.Survivor
{
    /// <summary>What a TRAIN session rewards a bit more (GDD §3.4). Balanced = the default rewards.</summary>
    public enum TrainingFocus { Balanced, Survival, Gold, Boss, Offense }

    public static class TrainingFocusInfo
    {
        public const int Count = 5;

        public static string Id(TrainingFocus focus)
        {
            switch (focus)
            {
                case TrainingFocus.Survival: return "survival";
                case TrainingFocus.Gold: return "gold";
                case TrainingFocus.Boss: return "boss";
                case TrainingFocus.Offense: return "offense";
                default: return "balanced";
            }
        }

        /// <summary>Parses an id (case-insensitive, trimmed). Null, empty or unknown → false and Balanced.</summary>
        public static bool TryParse(string id, out TrainingFocus focus)
        {
            focus = TrainingFocus.Balanced;
            if (string.IsNullOrWhiteSpace(id)) return false;
            string key = id.Trim();
            for (int i = 0; i < Count; i++)
            {
                TrainingFocus candidate = (TrainingFocus)i;
                if (string.Equals(key, Id(candidate), System.StringComparison.OrdinalIgnoreCase)) { focus = candidate; return true; }
            }
            return false;
        }

        public static string DisplayName(TrainingFocus focus)
        {
            switch (focus)
            {
                case TrainingFocus.Survival: return "Sống sót";
                case TrainingFocus.Gold: return "Vàng";
                case TrainingFocus.Boss: return "Boss";
                case TrainingFocus.Offense: return "Tấn công";
                default: return "Cân bằng";
            }
        }
    }
}
