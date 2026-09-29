namespace PersonalArena.ML
{
    /// <summary>Summary emitted immediately before a HeroAgent episode ends.</summary>
    public readonly struct EpisodeStats
    {
        public readonly int Kills;
        public readonly int Backstabs;
        public readonly int Parries;
        public readonly float SurvivedSeconds;
        public readonly bool Died;
        public readonly float DamageTaken;
        public readonly int ZombieCount;
        public readonly bool Fell;
        public readonly int RingOuts;
        public readonly int PotionsPicked;

        public EpisodeStats(
            int kills,
            int backstabs,
            int parries,
            float survivedSeconds,
            bool died,
            float damageTaken,
            int zombieCount,
            bool fell = false,
            int ringOuts = 0,
            int potionsPicked = 0)
        {
            Kills = kills;
            Backstabs = backstabs;
            Parries = parries;
            SurvivedSeconds = survivedSeconds;
            Died = died;
            DamageTaken = damageTaken;
            ZombieCount = zombieCount;
            Fell = fell;
            RingOuts = ringOuts;
            PotionsPicked = potionsPicked;
        }
    }
}
