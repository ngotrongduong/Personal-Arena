using PersonalArena.Core;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>Conversions between the simulation floor and Unity world space.</summary>
    public static class ArenaSpace
    {
        public static Vector3 ToWorld(Vec2 position, float height = 0f)
        {
            return new Vector3(position.X, height, position.Y);
        }

        public static float YawDegrees(float facing)
        {
            return 90f - facing * Mathf.Rad2Deg;
        }

        public static Vec2 ToSim(Vector3 worldPosition)
        {
            return new Vec2(worldPosition.x, worldPosition.z);
        }
    }
}
