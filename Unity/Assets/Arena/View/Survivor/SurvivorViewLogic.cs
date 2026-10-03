using PersonalArena.Core;
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

    /// <summary>How the renderer draws a weapon when it fires (evolutions use their base weapon's visual).</summary>
    public enum WeaponVisual
    {
        None,
        /// <summary>Sword sweep crescent (0).</summary>
        Sweep,
        /// <summary>Spear streaks (1).</summary>
        Spear,
        /// <summary>Orbiting axes (2).</summary>
        OrbitAxe,
        /// <summary>Thrown hammer projectiles (3).</summary>
        Hammer,
        /// <summary>Golden aura disc (4).</summary>
        Aura,
        /// <summary>Expanding ring (5).</summary>
        Shockwave,
        MagicBolt,
        FireOrb,
        FrostNova,
        HolyField,
        Lightning,
        ArcaneBeam,
        Arrow,
        MultiShot,
        ArrowRain,
        OrbitKnife,
        Dagger,
        Crossbow
    }

    /// <summary>Model of a hero projectile.</summary>
    public enum ProjectileLook
    {
        Hammer,
        MagicBolt,
        Arrow,
        Fireball,
        PowerShot
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

        /// <summary>Reach of any sweep weapon (sword, dagger and their evolutions) at a level, matching the simulation.</summary>
        public static float SweepRange(int catalogIndex, int level, float areaMultiplier)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            if (def == null)
            {
                return SweepRange(level, areaMultiplier);
            }
            int clamped = Mathf.Max(1, level);
            return def.BaseRange * (1f + def.RangePerLevel * (clamped - 1)) * (areaMultiplier > 0f ? areaMultiplier : 1f);
        }

        /// <summary>True when a sweep weapon also hits behind the hero at this level (evolutions from level 1).</summary>
        public static bool SweepHitsBehind(int catalogIndex, int level)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            return def != null && def.BackArcLevel > 0 && level >= def.BackArcLevel;
        }

        /// <summary>Length of a thrust weapon's streak (spear, arcane beam, crossbow, evolutions), matching the simulation.</summary>
        public static float ThrustLength(int catalogIndex, float areaMultiplier)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            float range = def != null && def.BaseRange > 0f ? def.BaseRange : 5f;
            return range * (areaMultiplier > 0f ? areaMultiplier : 1f);
        }

        /// <summary>Short Vietnamese description of what picking an item at <paramref name="nextLevel"/> gives.</summary>
        public static string ItemDescription(int catalogIndex, int nextLevel)
        {
            bool isNew = nextLevel <= 1;
            if (IsEvolution(catalogIndex))
            {
                return EvolutionDescription(catalogIndex);
            }
            if (catalogIndex >= SurvivorCatalog.MagicBoltIndex && catalogIndex <= SurvivorCatalog.CrossbowIndex)
            {
                return ClassWeaponDescription(catalogIndex, nextLevel);
            }
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
            if (IsEvolution(catalogIndex))
            {
                return Color.Lerp(ItemColor(BaseWeapon(catalogIndex)), EvolutionGold, 0.45f);
            }
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
                // Mage: blue, fire, ice, holy gold, electric, arcane purple.
                case 14: return new Color(0.45f, 0.62f, 1f);
                case 15: return new Color(1f, 0.52f, 0.2f);
                case 16: return new Color(0.62f, 0.92f, 1f);
                case 17: return new Color(1f, 0.9f, 0.52f);
                case 18: return new Color(0.72f, 0.82f, 1f);
                case 19: return new Color(0.78f, 0.45f, 1f);
                // Archer: greens and woods.
                case 20: return new Color(0.55f, 0.86f, 0.4f);
                case 21: return new Color(0.42f, 0.75f, 0.36f);
                case 22: return new Color(0.68f, 0.92f, 0.5f);
                case 23: return new Color(0.72f, 0.82f, 0.66f);
                case 24: return new Color(0.82f, 0.62f, 0.4f);
                case 25: return new Color(0.7f, 0.5f, 0.3f);
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

        // ------------------------------------------------------------------ M7: class weapons and evolutions

        /// <summary>Gold used for evolution frames, tints and the evolution toast.</summary>
        public static readonly Color EvolutionGold = new Color(1f, 0.82f, 0.3f);

        /// <summary>How much larger an evolved weapon is drawn than its base weapon.</summary>
        public const float EvolutionScale = 1.3f;

        /// <summary>True for an evolution row (40..57) of the catalog.</summary>
        public static bool IsEvolution(int catalogIndex)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            return def != null && def.EvolvesFrom >= 0;
        }

        /// <summary>The base weapon of an evolution, or the index itself for a normal item.</summary>
        public static int BaseWeapon(int catalogIndex)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            return def != null && def.EvolvesFrom >= 0 ? def.EvolvesFrom : catalogIndex;
        }

        /// <summary>Display name of an item ("Kiếm bão"); empty for an unknown index.</summary>
        public static string ItemName(int catalogIndex)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            return def != null ? def.Name : string.Empty;
        }

        /// <summary>HUD toast when a weapon evolves: "TIẾN HÓA: Kiếm bão".</summary>
        public static string EvolvedToast(int catalogIndex)
        {
            string name = ItemName(catalogIndex);
            return string.IsNullOrEmpty(name) ? "TIẾN HÓA!" : "TIẾN HÓA: " + name;
        }

        /// <summary>Visual of a weapon; an evolution draws like its base weapon.</summary>
        public static WeaponVisual WeaponVisualOf(int catalogIndex)
        {
            switch (BaseWeapon(catalogIndex))
            {
                case 0: return WeaponVisual.Sweep;
                case 1: return WeaponVisual.Spear;
                case 2: return WeaponVisual.OrbitAxe;
                case 3: return WeaponVisual.Hammer;
                case 4: return WeaponVisual.Aura;
                case 5: return WeaponVisual.Shockwave;
                case SurvivorCatalog.MagicBoltIndex: return WeaponVisual.MagicBolt;
                case SurvivorCatalog.FireOrbIndex: return WeaponVisual.FireOrb;
                case SurvivorCatalog.FrostNovaIndex: return WeaponVisual.FrostNova;
                case SurvivorCatalog.HolyFieldIndex: return WeaponVisual.HolyField;
                case SurvivorCatalog.LightningIndex: return WeaponVisual.Lightning;
                case SurvivorCatalog.ArcaneBeamIndex: return WeaponVisual.ArcaneBeam;
                case SurvivorCatalog.ArrowIndex: return WeaponVisual.Arrow;
                case SurvivorCatalog.MultiShotIndex: return WeaponVisual.MultiShot;
                case SurvivorCatalog.ArrowRainIndex: return WeaponVisual.ArrowRain;
                case SurvivorCatalog.OrbitKnifeIndex: return WeaponVisual.OrbitKnife;
                case SurvivorCatalog.DaggerIndex: return WeaponVisual.Dagger;
                case SurvivorCatalog.CrossbowIndex: return WeaponVisual.Crossbow;
                default: return WeaponVisual.None;
            }
        }

        /// <summary>
        /// Model of a hero projectile from its source: a weapon catalog index, or −1 − slot for a skill projectile
        /// (the skill id of that slot in <paramref name="classDef"/> picks fireball or power shot).
        /// </summary>
        public static ProjectileLook ProjectileLookOf(int sourceIndex, SurvivorClassDef classDef)
        {
            if (sourceIndex < 0)
            {
                int slot = -1 - sourceIndex;
                SkillDef[] skills = classDef != null ? classDef.ActiveSkills : null;
                string id = skills != null && slot < skills.Length && skills[slot] != null ? skills[slot].Id : null;
                switch (id)
                {
                    case "fireball": return ProjectileLook.Fireball;
                    case "power-shot": return ProjectileLook.PowerShot;
                    default: return ProjectileLook.MagicBolt;
                }
            }

            switch (WeaponVisualOf(sourceIndex))
            {
                case WeaponVisual.MagicBolt: return ProjectileLook.MagicBolt;
                case WeaponVisual.Arrow:
                case WeaponVisual.MultiShot: return ProjectileLook.Arrow;
                default: return ProjectileLook.Hammer;
            }
        }

        /// <summary>Art walker model of an enemy type: exploder = minion, ghost = rogue, necromancer = skeleton mage.</summary>
        public static int EnemyWalkerIndex(int typeIndex)
        {
            switch (typeIndex)
            {
                case 1: return 1;
                case 2: return 2;
                case 3: return 3;
                case SurvivorDefaults.ExploderTypeIndex: return 0;
                case SurvivorDefaults.GhostTypeIndex: return 1;
                case SurvivorDefaults.NecromancerTypeIndex: return 3;
                default: return 0;
            }
        }

        /// <summary>True when the class's block covers every direction (the mage's mana shield bubble).</summary>
        public static bool BlockIsBubble(SurvivorClassDef classDef)
        {
            if (classDef == null || classDef.ActiveSkills == null)
            {
                return false;
            }
            foreach (SkillDef skill in classDef.ActiveSkills)
            {
                if (skill != null && skill.Kind == SkillKind.Block && skill.BlockAllDirections)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Vietnamese skill button title; unknown ids show the id.</summary>
        public static string SkillTitle(SkillDef skill)
        {
            if (skill == null)
            {
                return string.Empty;
            }
            switch (skill.Id)
            {
                case "kick": return "Đá";
                case "shield-block": return "Đỡ khiên";
                case "dash": return "Lướt";
                case "fireball": return "Cầu lửa";
                case "mana-shield": return "Khiên phép";
                case "blink": return "Dịch chuyển";
                case "frost-burst": return "Nổ băng";
                case "power-shot": return "Bắn mạnh";
                case "roll-back": return "Lộn lùi";
                default: return skill.Id;
            }
        }

        /// <summary>One-line Vietnamese description of a skill; empty for unknown ids.</summary>
        public static string SkillDescription(string skillId)
        {
            switch (skillId)
            {
                case "kick": return "Đá văng và làm choáng quái trước mặt";
                case "shield-block": return "Giơ khiên chặn đòn phía trước, đỡ đúng lúc thì phản đòn";
                case "dash": return "Lướt nhanh theo hướng chạy";
                case "fireball": return "Ném cầu lửa nổ trúng nhiều quái";
                case "mana-shield": return "Bọc khiên phép chặn đòn mọi hướng";
                case "blink": return "Dịch chuyển tức thời một đoạn ngắn";
                case "frost-burst": return "Nổ băng quanh người, làm choáng quái";
                case "power-shot": return "Bắn mũi tên mạnh xuyên nhiều quái";
                case "roll-back": return "Lộn ra sau để giữ khoảng cách";
                default: return string.Empty;
            }
        }

        /// <summary>True when a skill slot is shown on the HUD (a real skill, not the empty "none" slot).</summary>
        public static bool SkillVisible(SkillDef skill) => skill != null && skill.Kind != SkillKind.None;

        /// <summary>Distance between two skill slots in the bottom HUD bar.</summary>
        public const float SkillSlotSpacing = 144f;

        /// <summary>Width of the skill bar holding <paramref name="shown"/> slots (3 slots = the old 456 px).</summary>
        public static float SkillPanelWidth(int shown) => Mathf.Max(1, shown) * SkillSlotSpacing + 24f;

        /// <summary>Centre x of the <paramref name="column"/>-th of <paramref name="shown"/> centred skill slots.</summary>
        public static float SkillSlotX(int column, int shown) => (column - (Mathf.Max(1, shown) - 1) * 0.5f) * SkillSlotSpacing;

        /// <summary>Name line of the vitals panel, e.g. "PHÁP SƯ  (AI điều khiển)"; unknown classes read as the Warrior.</summary>
        public static string HeroNameLine(string classId) => ClassViewLogic.UpperName(classId) + "  (AI điều khiển)";

        private static string ClassWeaponDescription(int catalogIndex, int nextLevel)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            if (def == null)
            {
                return string.Empty;
            }
            if (nextLevel <= 1)
            {
                return NewWeaponText(catalogIndex);
            }

            string text = "+" + Mathf.RoundToInt(def.DamagePerLevel) + " sát thương";
            if (catalogIndex == SurvivorCatalog.DaggerIndex)
            {
                return def.BackArcLevel > 0 && nextLevel >= def.BackArcLevel ? "+Sát thương, chém cả phía sau" : text + ", +10% tầm";
            }
            if (catalogIndex == SurvivorCatalog.FrostNovaIndex)
            {
                return text + ", hồi nhanh hơn";
            }
            if (catalogIndex == SurvivorCatalog.HolyFieldIndex)
            {
                return text + ", +10% vùng";
            }
            if (CountGrows(def, nextLevel))
            {
                text += ", thêm 1 " + CountUnit(catalogIndex);
            }
            return text;
        }

        private static string NewWeaponText(int catalogIndex)
        {
            switch (catalogIndex)
            {
                case SurvivorCatalog.MagicBoltIndex: return "Bắn tia phép vào quái gần";
                case SurvivorCatalog.FireOrbIndex: return "Cầu lửa bay vòng quanh người";
                case SurvivorCatalog.FrostNovaIndex: return "Vòng băng lan ra, làm choáng quái";
                case SurvivorCatalog.HolyFieldIndex: return "Vùng ánh sáng thánh đốt quái quanh người";
                case SurvivorCatalog.LightningIndex: return "Sét đánh xuống quái ngẫu nhiên gần đó";
                case SurvivorCatalog.ArcaneBeamIndex: return "Bắn tia ma thuật xuyên thẳng hàng quái";
                case SurvivorCatalog.ArrowIndex: return "Bắn tên xuyên 1 quái";
                case SurvivorCatalog.MultiShotIndex: return "Bắn chùm 3 mũi tên tỏa ra";
                case SurvivorCatalog.ArrowRainIndex: return "Mưa tên trút xuống chỗ quái đông nhất";
                case SurvivorCatalog.OrbitKnifeIndex: return "Dao bay vòng quanh người";
                case SurvivorCatalog.DaggerIndex: return "Chém nhanh nửa vòng trước mặt";
                case SurvivorCatalog.CrossbowIndex: return "Bắn nỏ xuyên thẳng, sát thương lớn";
                default: return string.Empty;
            }
        }

        private static string EvolutionDescription(int catalogIndex)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            ItemDef baseDef = def != null ? SurvivorCatalog.Get(def.EvolvesFrom) : null;
            if (baseDef == null)
            {
                return string.Empty;
            }
            string text = "Tiến hóa từ " + baseDef.Name + ": sát thương x1,5, tầm xa hơn";
            if (baseDef.CountByLevel.Count > 0)
            {
                text += ", thêm 1 " + CountUnit(baseDef.CatalogIndex);
            }
            return text;
        }

        private static bool CountGrows(ItemDef def, int nextLevel)
        {
            if (def.CountByLevel.Count == 0 || nextLevel < 2)
            {
                return false;
            }
            int now = def.CountByLevel[Mathf.Min(nextLevel - 2, def.CountByLevel.Count - 1)];
            int next = def.CountByLevel[Mathf.Min(nextLevel - 1, def.CountByLevel.Count - 1)];
            return next > now;
        }

        private static string CountUnit(int catalogIndex)
        {
            switch (catalogIndex)
            {
                case 1: return "giáo";
                case 2: return "rìu";
                case 3: return "búa";
                case SurvivorCatalog.MagicBoltIndex: return "tia";
                case SurvivorCatalog.FireOrbIndex: return "cầu lửa";
                case SurvivorCatalog.LightningIndex: return "tia sét";
                case SurvivorCatalog.ArcaneBeamIndex: return "tia";
                case SurvivorCatalog.ArrowIndex: return "mũi tên";
                case SurvivorCatalog.MultiShotIndex: return "mũi tên";
                case SurvivorCatalog.ArrowRainIndex: return "loạt tên";
                case SurvivorCatalog.OrbitKnifeIndex: return "dao";
                case SurvivorCatalog.CrossbowIndex: return "mũi nỏ";
                default: return "đòn";
            }
        }
    }
}
