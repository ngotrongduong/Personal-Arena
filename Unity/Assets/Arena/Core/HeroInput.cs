using System;

namespace PersonalArena.Core
{
    /// <summary>Discrete hero command for one simulation tick.</summary>
    public readonly struct HeroInput
    {
        /// <summary>Number of values of each discrete action branch (Move, Turn, Skill).</summary>
        public const int MoveBranchSize = 9;
        public const int TurnBranchSize = 3;
        public const int SkillBranchSize = 5;

        public readonly int Move;
        public readonly int Turn;
        public readonly int Skill;

        public HeroInput(int move, int turn, int skill)
        {
            Move = move;
            Turn = turn;
            Skill = skill;
        }

        /// <summary>
        /// Builds an input from axis signs. dx/dy are -1, 0 or 1 in arena space (+x right, +y up);
        /// they map to Move 1..8 (1 = +x, then counter-clockwise every 45 degrees), 0 when both are 0.
        /// Turn +1 is counter-clockwise.
        /// </summary>
        public static HeroInput FromAxes(int dx, int dy, int turn, int skill)
        {
            return new HeroInput(MoveFromAxes(dx, dy), Math.Sign(turn), skill);
        }

        /// <summary>Maps axis signs to a Move value (0 = stay, 1..8 = directions).</summary>
        public static int MoveFromAxes(int dx, int dy)
        {
            dx = Math.Sign(dx);
            dy = Math.Sign(dy);
            if (dx == 0 && dy == 0)
                return 0;

            // Octant index 0..7 counter-clockwise from +x.
            int octant = (dx, dy) switch
            {
                (1, 0) => 0,
                (1, 1) => 1,
                (0, 1) => 2,
                (-1, 1) => 3,
                (-1, 0) => 4,
                (-1, -1) => 5,
                (0, -1) => 6,
                _ => 7,
            };
            return octant + 1;
        }

        /// <summary>
        /// Turn sign (-1, 0, 1) that rotates <paramref name="facing"/> toward <paramref name="targetAngle"/>
        /// (radians), or 0 when already within <paramref name="deadZone"/> radians.
        /// </summary>
        public static int TurnToward(float facing, float targetAngle, float deadZone)
        {
            float diff = targetAngle - facing;
            while (diff > MathF.PI) diff -= 2f * MathF.PI;
            while (diff < -MathF.PI) diff += 2f * MathF.PI;
            if (MathF.Abs(diff) <= deadZone)
                return 0;
            return diff > 0f ? 1 : -1;
        }

        /// <summary>Action branch index for Turn (-1,0,1 -> 0,1,2).</summary>
        public static int TurnToBranch(int turn) => Math.Sign(turn) + 1;

        /// <summary>Turn value for an action branch index (0,1,2 -> -1,0,1).</summary>
        public static int BranchToTurn(int branch) => branch - 1;
    }
}
