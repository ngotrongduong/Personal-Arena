using System;
using PersonalArena.Core;

namespace PersonalArena.ML
{
    /// <summary>Creates fresh class definitions and ML behavior names from stable class IDs.</summary>
    public static class ClassRegistry
    {
        public const string WarriorId = "warrior";

        public static HeroClassDef Create(string classId)
        {
            if (string.Equals(classId, WarriorId, StringComparison.OrdinalIgnoreCase))
            {
                return DefaultDefs.Warrior();
            }

            throw new ArgumentException($"Unknown hero class ID '{classId}'.", nameof(classId));
        }

        public static string BehaviorName(string classId)
        {
            if (string.Equals(classId, WarriorId, StringComparison.OrdinalIgnoreCase))
            {
                return "Warrior";
            }

            throw new ArgumentException($"Unknown hero class ID '{classId}'.", nameof(classId));
        }
    }
}
