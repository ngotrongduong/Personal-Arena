using System;
using PersonalArena.Core.Survivor;

namespace PersonalArena.ML
{
    /// <summary>Creates fresh class definitions and ML behavior names from stable class IDs.</summary>
    public static class ClassRegistry
    {
        public const string WarriorId = "warrior";
        public const string MageId = "mage";
        public const string ArcherId = "archer";

        /// <summary>Every playable class ID in HUD/selection order.</summary>
        public static readonly string[] ClassIds = { WarriorId, MageId, ArcherId };

        /// <summary>Survivor kit for a class; only the warrior has one until M7.</summary>
        public static SurvivorClassDef Create(string classId)
        {
            return SurvivorEnvFactory.CreateClass(Normalize(classId));
        }

        /// <summary>True when the class can already be trained in Survivor mode.</summary>
        public static bool HasSurvivorKit(string classId)
        {
            return string.Equals(classId, WarriorId, StringComparison.OrdinalIgnoreCase);
        }

        public static string BehaviorName(string classId)
        {
            switch (Normalize(classId))
            {
                case WarriorId:
                    return "Warrior";
                case MageId:
                    return "Mage";
                default:
                    return "Archer";
            }
        }

        public static bool IsKnown(string classId)
        {
            return IndexOf(classId) >= 0;
        }

        public static int IndexOf(string classId)
        {
            for (int i = 0; i < ClassIds.Length; i++)
            {
                if (string.Equals(classId, ClassIds[i], StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string Normalize(string classId)
        {
            int index = IndexOf(classId);
            if (index < 0)
            {
                throw new ArgumentException($"Unknown hero class ID '{classId}'.", nameof(classId));
            }

            return ClassIds[index];
        }
    }
}
