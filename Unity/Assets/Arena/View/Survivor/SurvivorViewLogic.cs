using PersonalArena.Core.Survivor;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>Kind of scenery model used to draw a survivor obstacle.</summary>
    public enum ObstacleKind
    {
        Grave,
        Tree,
        Rock
    }

    /// <summary>
    /// Pure presentation rules of the survivor viewer (no scene objects), kept here so they can be unit tested:
    /// clock text, stable obstacle model choice, item descriptions and end-of-run texts.
    /// </summary>
    public static class SurvivorViewLogic
    {
        /// <summary>Formats seconds as "mm:ss" (whole seconds, rounded down); invalid or negative values show "00:00".</summary>
        public static string FormatClock(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
            {
                return "00:00";
            }

            int whole = Mathf.FloorToInt(seconds);
            int minutes = whole / 60;
            int rest = whole % 60;
            return minutes.ToString("00") + ":" + rest.ToString("00");
        }

        /// <summary>Stable integer hash (same result on every run and platform) for model and yaw choices.</summary>
        public static uint Hash(int value, int salt = 0)
        {
            unchecked
            {
                uint x = (uint)value * 0x9E3779B1u ^ (uint)salt * 0x85EBCA77u;
                x ^= x >> 16;
                x *= 0x7FEB352Du;
                x ^= x >> 15;
                x *= 0x846CA68Bu;
                x ^= x >> 16;
                return x;
            }
        }

        /// <summary>Model index in [0, count) for an obstacle; the same obstacle always gets the same model. -1 when there are no models.</summary>
        public static int ObstacleModelIndex(int obstacleIndex, int count, int salt = 0)
        {
            if (count <= 0)
            {
                return -1;
            }

            return (int)(Hash(obstacleIndex, 7919 + salt) % (uint)count);
        }

        /// <summary>Grave, tree or rock for an obstacle: small ones are mostly graves, big ones mostly trees.</summary>
        public static ObstacleKind ObstacleKindFor(int obstacleIndex, float radius)
        {
            int roll = (int)(Hash(obstacleIndex, 104729) % 100u);
            if (radius < 0.8f)
            {
                return roll < 60 ? ObstacleKind.Grave : ObstacleKind.Rock;
            }
            if (radius < 1.2f)
            {
                return roll < 40 ? ObstacleKind.Grave : roll < 80 ? ObstacleKind.Tree : ObstacleKind.Rock;
            }
            return roll < 60 ? ObstacleKind.Tree : ObstacleKind.Rock;
        }

        /// <summary>Stable yaw in degrees for an obstacle model.</summary>
        public static float ObstacleYaw(int obstacleIndex)
        {
            return Hash(obstacleIndex, 15485863) % 360u;
        }

        /// <summary>Reach of the sword sweep at a level, matching the simulation's formula.</summary>
        public static float SweepRange(int level, float areaMultiplier)
        {
            int clamped = Mathf.Max(1, level);
            ItemDef sweep = SurvivorCatalog.Get(0);
            float baseRange = sweep != null ? sweep.BaseRange : 2.5f;
            float perLevel = sweep != null ? sweep.RangePerLevel : 0.1f;
            return baseRange * (1f + perLevel * (clamped - 1)) * (areaMultiplier > 0f ? areaMultiplier : 1f);
        }

        /// <summary>True when the sword sweep also hits behind the hero at this level.</summary>
        public static bool SweepHitsBehind(int level)
        {
            ItemDef sweep = SurvivorCatalog.Get(0);
            int from = sweep != null ? sweep.BackArcLevel : 5;
            return from > 0 && level >= from;
        }

        /// <summary>Short Vietnamese description of what picking an item at <paramref name="nextLevel"/> gives.</summary>
        public static string ItemDescription(int catalogIndex, int nextLevel)
        {
            bool isNew = nextLevel <= 1;
            switch (catalogIndex)
            {
                case 0:
                    if (isNew)
                    {
                        return "Chém vòng cung trước mặt";
                    }
                    return nextLevel >= 5 ? "+Sát thương, chém cả phía sau" : "+8 sát thương, +10% tầm";
                case 1:
                    if (isNew)
                    {
                        return "Đâm giáo xuyên thẳng vào quái gần";
                    }
                    return nextLevel == 3 || nextLevel == 5 ? "+10 sát thương, thêm 1 giáo" : "+10 sát thương";
                case 2:
                    if (isNew)
                    {
                        return "Rìu bay vòng quanh người";
                    }
                    return nextLevel == 2 || nextLevel == 4 ? "+4 sát thương, thêm 1 rìu" : "+4 sát thương";
                case 4:
                    return isNew ? "Vùng hào quang đốt quái sát bên" : "+2 sát thương, +10% vùng";
                case 5:
                    return isNew ? "Sóng chấn đẩy lùi quái xung quanh" : "+6 sát thương, hồi nhanh hơn";
                case 3:
                    if (isNew)
                    {
                        return "Ném búa vào quái gần";
                    }
                    return nextLevel >= 4 ? "+7 sát thương, ném 3 búa" : "+7 sát thương, ném 2 búa";
                case 6:
                    return "+10% máu tối đa";
                case 7:
                    return "+1 giáp (giảm sát thương)";
                case 8:
                    return "+8% sát thương";
                case 9:
                    return "+4% tỉ lệ chí mạng";
                case 10:
                    return "-6% thời gian hồi vũ khí";
                case 11:
                    return "+8% vùng ảnh hưởng";
                case 12:
                    return "+8% tốc độ chạy";
                case 13:
                    return "+25% tầm hút đồ";
                case 62:
                    return "Nhận ngay 25 vàng";
                case 63:
                    return "Hồi ngay 30 máu";
                default:
                    return string.Empty;
            }
        }

        /// <summary>Card level line: "MỚI" for a new item, "Lv N" for an upgrade, empty for one-off rewards.</summary>
        public static string LevelLabel(int catalogIndex, int nextLevel)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            if (def != null && def.Kind == ItemKind.Filler)
            {
                return string.Empty;
            }
            return nextLevel <= 1 ? "MỚI" : "Lv " + nextLevel;
        }

        /// <summary>Theme color of a survivor item, used for icon badges and card accents.</summary>
        public static Color ItemColor(int catalogIndex)
        {
            switch (catalogIndex)
            {
                case 0: return new Color(0.78f, 0.86f, 1f);
                case 1: return new Color(0.55f, 0.8f, 1f);
                case 2: return new Color(0.95f, 0.5f, 0.35f);
                case 3: return new Color(1f, 0.66f, 0.3f);
                case 4: return new Color(1f, 0.92f, 0.45f);
                case 5: return new Color(0.6f, 0.65f, 1f);
                case 6: return new Color(1f, 0.36f, 0.4f);
                case 7: return new Color(0.84f, 0.8f, 0.68f);
                case 8: return new Color(1f, 0.5f, 0.25f);
                case 9: return new Color(1f, 0.85f, 0.3f);
                case 10: return new Color(0.75f, 0.6f, 1f);
                case 11: return new Color(0.45f, 0.85f, 1f);
                case 12: return new Color(0.4f, 0.95f, 0.75f);
                case 13: return new Color(1f, 0.45f, 0.6f);
                case 62: return new Color(1f, 0.82f, 0.3f);
                case 63: return new Color(0.45f, 1f, 0.5f);
                default: return new Color(0.8f, 0.82f, 0.9f);
            }
        }

        /// <summary>Vietnamese end-of-run title.</summary>
        public static string EndTitle(EndReason reason)
        {
            switch (reason)
            {
                case EndReason.Won: return "CHIẾN THẮNG!";
                case EndReason.TimeUp: return "HẾT GIỜ";
                case EndReason.Expired: return "TRÙM CÒN SỐNG - HẾT GIỜ";
                case EndReason.Died: return "ĐÃ GỤC NGÃ";
                default: return string.Empty;
            }
        }

        /// <summary>Vietnamese cause of death for the end screen.</summary>
        public static string DeathCauseText(DeathCause cause)
        {
            switch (cause)
            {
                case DeathCause.Surrounded: return "bị bao vây";
                case DeathCause.Boss: return "bị Trùm hạ gục";
                case DeathCause.Brute: return "trúng đòn của Đồ tể";
                case DeathCause.Contact: return "bị quái cào";
                case DeathCause.Projectile: return "trúng đạn";
                case DeathCause.Explosion: return "bị Bom xác nổ";
                default: return "không rõ";
            }
        }

        /// <summary>Title color: gold for a win, red for a death, pale blue otherwise.</summary>
        public static Color EndColor(EndReason reason)
        {
            switch (reason)
            {
                case EndReason.Won: return new Color(1f, 0.86f, 0.4f);
                case EndReason.Died: return new Color(1f, 0.4f, 0.35f);
                default: return new Color(0.7f, 0.85f, 1f);
            }
        }
    }
}
