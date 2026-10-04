using PersonalArena.Core.Survivor;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>What an evolved weapon is made of: decides its trail, its mark on the ground and its colour.</summary>
    public enum EvolutionElement
    {
        None,
        Lightning,
        Fire,
        Ice,
        Wind,
        Earth,
        Holy,
        Arcane,
        Shadow,
        Poison,
        Star,
        Chaos,
        Meteor
    }

    public readonly struct EvolutionStyle
    {
        public readonly EvolutionElement Element;
        public readonly Color Color;

        public EvolutionStyle(EvolutionElement element, Color color)
        {
            Element = element;
            Color = color;
        }
    }

    /// <summary>
    /// The look of every evolved weapon (viewer only): each evolution has its own element and colour instead of a
    /// gold copy of its base weapon. Pure data, so EditMode tests can check that no evolution is left without a style.
    /// </summary>
    public static class SurvivorEvolutionStyles
    {
        private static readonly EvolutionStyle NoStyle = new EvolutionStyle(EvolutionElement.None, SurvivorViewLogic.EvolutionGold);

        /// <summary>Style of an evolution; <see cref="EvolutionElement.None"/> (gold) for anything else.</summary>
        public static EvolutionStyle Of(int catalogIndex)
        {
            ItemDef def = SurvivorCatalog.Get(catalogIndex);
            if (def == null || def.EvolvesFrom < 0)
            {
                return NoStyle;
            }
            switch (catalogIndex)
            {
                case 40: return Style(EvolutionElement.Lightning, 0.45f, 0.9f, 1f);   // Kiếm bão: storm wave crackling
                case 41: return Style(EvolutionElement.Fire, 1f, 0.42f, 0.1f);        // Thương rồng: dragon fire thrust
                case 42: return Style(EvolutionElement.Wind, 0.7f, 1f, 0.85f);        // Lốc rìu: whirlwind axes
                case 43: return Style(EvolutionElement.Lightning, 1f, 0.92f, 0.35f);  // Búa sấm: thunder hammer
                case 44: return Style(EvolutionElement.Holy, 1f, 0.95f, 0.7f);        // Hào quang thánh
                case 45: return Style(EvolutionElement.Earth, 0.85f, 0.55f, 0.28f);   // Động đất
                case 46: return Style(EvolutionElement.Arcane, 0.75f, 0.45f, 1f);     // Bão phép
                case 47: return Style(EvolutionElement.Fire, 1f, 0.8f, 0.2f);         // Vành mặt trời: sun fire
                case 48: return Style(EvolutionElement.Ice, 0.6f, 0.92f, 1f);         // Kỷ băng hà
                case 49: return Style(EvolutionElement.Holy, 1f, 0.9f, 0.5f);         // Thánh địa
                case 50: return Style(EvolutionElement.Lightning, 1f, 0.85f, 0.4f);   // Lôi thần: golden thunder
                case 51: return Style(EvolutionElement.Shadow, 1f, 0.22f, 0.3f);      // Tia hủy diệt: crimson ray
                case 52: return Style(EvolutionElement.Wind, 0.6f, 1f, 0.8f);         // Tên gió
                case 53: return Style(EvolutionElement.Fire, 1f, 0.5f, 0.15f);        // Quạt tên: flaming fan
                case 54: return Style(EvolutionElement.Holy, 1f, 0.95f, 0.6f);        // Thiên tiễn: arrows of light
                case 55: return Style(EvolutionElement.Star, 1f, 0.45f, 0.75f);       // Vũ điệu dao
                case 56: return Style(EvolutionElement.Shadow, 0.6f, 0.35f, 0.9f);    // Song đao ám sát
                case 57: return Style(EvolutionElement.Earth, 0.75f, 0.7f, 0.62f);    // Nỏ công thành: siege bolt
                case 112: return Style(EvolutionElement.Fire, 1f, 0.2f, 0.3f);        // Hỏa ngục: crimson hellfire
                case 113: return Style(EvolutionElement.Wind, 0.85f, 0.95f, 1f);      // Kiếm vô ảnh
                case 114: return Style(EvolutionElement.Earth, 0.7f, 0.5f, 0.32f);    // Búa núi
                case 115: return Style(EvolutionElement.Fire, 1f, 0.88f, 0.35f);      // Mưa bom: white-hot
                case 116: return Style(EvolutionElement.Fire, 1f, 0.25f, 0.18f);      // Cơn thịnh nộ
                case 117: return Style(EvolutionElement.Holy, 1f, 0.9f, 0.55f);       // Kết giới bất diệt
                case 118: return Style(EvolutionElement.Lightning, 0.5f, 0.95f, 1f);  // Boomerang bão
                case 119: return Style(EvolutionElement.Poison, 0.8f, 1f, 0.2f);      // Đầm độc: acid
                case 120: return Style(EvolutionElement.Chaos, 1f, 0.4f, 0.9f);       // Đạn hỗn loạn: every colour
                case 121: return Style(EvolutionElement.Shadow, 0.75f, 1f, 0.95f);    // Bóng ma tốc độ: ghost
                case 122: return Style(EvolutionElement.Arcane, 1f, 0.88f, 0.55f);    // Hành lang vĩnh cửu: clock face
                case 123: return Style(EvolutionElement.Star, 0.6f, 0.5f, 1f);        // Tinh vân
                case 124: return Style(EvolutionElement.Meteor, 1f, 0.5f, 0.15f);     // Thiên thạch
                case 125: return Style(EvolutionElement.Star, 1f, 0.75f, 0.9f);       // Vòng tay song
                case 126: return Style(EvolutionElement.Ice, 0.65f, 0.9f, 1f);        // Tứ phương
                case 127: return Style(EvolutionElement.Arcane, 0.9f, 0.5f, 1f);      // Đá hiền triết
                case 77: return Style(EvolutionElement.Holy, 1f, 0.93f, 0.62f);       // Guardian Spirits: golden spirits
                case 78: return Style(EvolutionElement.Fire, 1f, 0.38f, 0.2f);        // Razor Tempest: red-hot saws
                case 79: return Style(EvolutionElement.Ice, 0.8f, 0.96f, 1f);         // Glacier Crown: white ice
                case 80: return Style(EvolutionElement.Star, 0.62f, 0.78f, 1f);       // Starfall: blue-white stars
                default: return NoStyle;
            }
        }

        private static EvolutionStyle Style(EvolutionElement element, float r, float g, float b)
        {
            return new EvolutionStyle(element, new Color(r, g, b, 1f));
        }
    }
}
