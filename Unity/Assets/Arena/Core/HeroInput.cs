namespace PersonalArena.Core
{
    /// <summary>Discrete hero command for one simulation tick.</summary>
    public readonly struct HeroInput
    {
        public readonly int Move;
        public readonly int Turn;
        public readonly int Skill;

        public HeroInput(int move, int turn, int skill)
        {
            Move = move;
            Turn = turn;
            Skill = skill;
        }
    }
}
