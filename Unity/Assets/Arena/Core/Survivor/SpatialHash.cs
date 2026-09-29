using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    public sealed class SpatialHash
    {
        public const float CellSize = 2f;
        private readonly float halfSize;
        private readonly int side;
        private readonly int[] heads;
        private readonly int[] next;
        private readonly int[] usedCells;
        private int usedCount;

        public SpatialHash(float mapHalfSize, int capacity)
        {
            halfSize = mapHalfSize;
            side = (int)MathF.Ceiling(mapHalfSize * 2f / CellSize);
            heads = new int[side * side];
            next = new int[capacity];
            usedCells = new int[capacity];
            Array.Fill(heads, -1);
            Array.Fill(next, -1);
        }

        public int[] Heads => heads;
        public int[] Next => next;
        public int Side => side;
        internal int[] UsedCells => usedCells;
        internal int UsedCount => usedCount;

        public void Rebuild(SurvivorEnemy[] enemies, int count)
        {
            for (int i = 0; i < usedCount; i++) heads[usedCells[i]] = -1;
            usedCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (!enemies[i].Active) continue;
                int cell = Cell(enemies[i].Position);
                if (heads[cell] < 0) usedCells[usedCount++] = cell;
                next[i] = heads[cell];
                heads[cell] = i;
            }
        }

        public int MinCell(float coordinate) => ClampCell((int)MathF.Floor((coordinate + halfSize) / CellSize));
        public int CellIndex(int x, int y) => y * side + x;

        private int Cell(Vec2 point) => CellIndex(MinCell(point.X), MinCell(point.Y));
        private int ClampCell(int value) => value < 0 ? 0 : value >= side ? side - 1 : value;
    }
}
