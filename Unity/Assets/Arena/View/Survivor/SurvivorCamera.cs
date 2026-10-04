using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PersonalArena.View
{
    /// <summary>
    /// Diagonal top-down follow camera for the survivor viewer: it trails the hero smoothly and the mouse
    /// wheel zooms in and out (ignored while the pointer is over the HUD).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(100)] // Follows the hero after SurvivorRenderer has placed it this frame.
    public sealed class SurvivorCamera : MonoBehaviour
    {
        public const float MinimumDistance = 12f;
        public const float MaximumDistance = 45f;
        // How far (as a share of the camera distance) the view centre stays inside the fence. Less along Z:
        // the skill bar covers the bottom of the screen.
        private const float EdgeMarginX = 0.3f;
        private const float EdgeMarginZ = 0.2f;

        [SerializeField] private SurvivorRenderer target;
        [SerializeField, Range(30f, 80f)] private float pitch = 55f;
        [SerializeField] private float yaw = 0f;
        [SerializeField, Range(MinimumDistance, MaximumDistance)] private float distance = 24f;
        [SerializeField] private float followSmoothTime = 0.16f;

        private Camera cameraComponent;
        private Vector3 focus;
        private Vector3 focusVelocity;
        private float zoomTarget;
        private bool snapped;

        public Camera ViewCamera => cameraComponent;

        public void SetTarget(SurvivorRenderer renderer)
        {
            target = renderer;
            snapped = false;
        }

        private void Awake()
        {
            cameraComponent = GetComponent<Camera>();
            cameraComponent.fieldOfView = 38f;
            cameraComponent.nearClipPlane = 0.3f;
            cameraComponent.farClipPlane = 300f;
            cameraComponent.clearFlags = CameraClearFlags.SolidColor;
            cameraComponent.backgroundColor = RenderSettings.fog ? RenderSettings.fogColor : new Color(0.04f, 0.04f, 0.07f);
            zoomTarget = distance;
            if (target == null)
            {
                target = GetComponentInParent<SurvivorRenderer>();
            }
        }

        private void LateUpdate()
        {
            ReadZoom();
            float delta = Time.unscaledDeltaTime;
            distance = Mathf.Lerp(distance, zoomTarget, 1f - Mathf.Exp(-10f * delta));

            Vector3 goal = target != null ? target.HeroWorldPosition : Vector3.zero;
            // Near the fence the view stops short of the hero, so less of the screen shows the empty outside.
            float limitX = SurvivorRenderer.MapHalfExtent - EdgeMarginX * distance;
            float limitZ = SurvivorRenderer.MapHalfExtent - EdgeMarginZ * distance;
            goal.x = Mathf.Clamp(goal.x, -limitX, limitX);
            goal.z = Mathf.Clamp(goal.z, -limitZ, limitZ);
            if (!snapped || (goal - focus).sqrMagnitude > 400f)
            {
                focus = goal;
                focusVelocity = Vector3.zero;
                snapped = true;
            }
            else
            {
                focus = Vector3.SmoothDamp(focus, goal, ref focusVelocity, followSmoothTime, Mathf.Infinity, delta);
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 lookAt = focus + new Vector3(0f, 0.8f, 0f);
            transform.SetPositionAndRotation(lookAt - rotation * Vector3.forward * distance, rotation);
        }

        private void ReadZoom()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f)
            {
                return;
            }

            EventSystem events = EventSystem.current;
            if (events != null && events.IsPointerOverGameObject())
            {
                return;
            }

            // Windows reports 120 per wheel notch; some devices report 1.
            float notches = Mathf.Abs(scroll) >= 20f ? scroll / 120f : scroll;
            zoomTarget = Mathf.Clamp(zoomTarget * Mathf.Pow(0.88f, notches), MinimumDistance, MaximumDistance);
        }
    }
}
