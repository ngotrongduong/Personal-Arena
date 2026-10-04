using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PersonalArena.View
{
    /// <summary>What a hoverable HUD element shows in the tooltip.</summary>
    public enum HudHoverKind
    {
        Item,
        Skill
    }

    /// <summary>Tells the HUD when the pointer enters or leaves one of its slots (item or skill).</summary>
    public sealed class HudHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public HudHoverKind Kind;
        public int Index;
        public Action<HudHoverTarget> Entered;
        public Action<HudHoverTarget> Exited;

        public void OnPointerEnter(PointerEventData eventData)
        {
            Entered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Exited?.Invoke(this);
        }

        private void OnDisable()
        {
            Exited?.Invoke(this);
        }
    }
}
