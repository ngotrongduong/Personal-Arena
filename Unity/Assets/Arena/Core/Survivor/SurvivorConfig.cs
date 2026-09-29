using System;

namespace PersonalArena.Core.Survivor
{
    public sealed class SurvivorConfig
    {
        public float MapHalfSize = 50f;
        public int ObstacleCount = 40;
        public float ObstacleRadiusMin = 0.5f;
        public float ObstacleRadiusMax = 1.5f;
        public float ObstacleClearRadius = 6f;
        public float RunSeconds = 900f;
        public float BossExpireSeconds = 1020f;
        public CharacterBuild Build = new CharacterBuild();
        public SurvivorClassDef ClassDef = SurvivorDefaults.Warrior();
        public SurvivorRewardConfig Rewards = new SurvivorRewardConfig();

        public void Validate()
        {
            if (!Finite(MapHalfSize) || MapHalfSize <= 10f) throw new ArgumentOutOfRangeException(nameof(MapHalfSize));
            if (ObstacleCount < 0 || ObstacleCount > 64) throw new ArgumentOutOfRangeException(nameof(ObstacleCount));
            if (!Finite(ObstacleRadiusMin) || !Finite(ObstacleRadiusMax) || ObstacleRadiusMin <= 0f || ObstacleRadiusMax < ObstacleRadiusMin) throw new ArgumentOutOfRangeException(nameof(ObstacleRadiusMin));
            if (!Finite(ObstacleClearRadius) || ObstacleClearRadius < 0f) throw new ArgumentOutOfRangeException(nameof(ObstacleClearRadius));
            if (!Finite(RunSeconds) || RunSeconds < 60f || RunSeconds > 900f) throw new ArgumentOutOfRangeException(nameof(RunSeconds));
            if (!Finite(BossExpireSeconds) || BossExpireSeconds < 900f) throw new ArgumentOutOfRangeException(nameof(BossExpireSeconds));
            if (Build == null || ClassDef == null || Rewards == null) throw new ArgumentNullException();
            Build.Validate();
            Rewards.Validate();
            if (ClassDef.ActiveSkills == null || ClassDef.ActiveSkills.Length != 4) throw new ArgumentException("Class needs four active skill slots.");
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct SurvivorInput
    {
        public const int MoveBranchSize = 9;
        public const int SkillBranchSize = 5;
        public const int PickBranchSize = 5;
        public readonly int Move;
        public readonly int Skill;
        public readonly int Pick;
        public SurvivorInput(int move, int skill, int pick) { Move = move; Skill = skill; Pick = pick; }
    }
}
