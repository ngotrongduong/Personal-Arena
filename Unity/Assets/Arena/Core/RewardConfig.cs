namespace PersonalArena.Core
{
    /// <summary>Weights used to turn simulation events into an RL reward.</summary>
    public sealed class RewardConfig
    {
        public float SurvivalPerSecond = 0.002f;
        public float DamageDealtPerHp = 0.01f;
        public float Kill = 1f;
        public float BackstabBonus = 0.5f;
        public float ParryBonus = 0.3f;
        public float KickHitBonus = 0.05f;
        public float HeroDamagedPerHp = -0.01f;
        public float FromBehindExtraPerHp = -0.005f;
        public float Death = -1f;
        public float WallBumpPenalty = -0.001f;
        public float SkillFailedPenalty = -0.0005f;
    }
}
