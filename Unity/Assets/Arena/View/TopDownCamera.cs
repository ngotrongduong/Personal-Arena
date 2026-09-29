using PersonalArena.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PersonalArena.View
{
    /// <summary>
    /// Free orbit camera for watching the arena. Drag to rotate, scroll to zoom, middle-drag (or Shift+drag) to pan.
    /// C cycles Whole arena / Follow warrior / Free; Home or Backspace resets the view.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class TopDownCamera : MonoBehaviour
    {
        public enum ViewMode
        {
            WholeArena,
            FollowHero,
            Free
        }

        public const string ControlsHint = "Drag rotate   Scroll or +/- zoom   Q/E spin\nMiddle-drag or Shift+drag move\nC camera mode   Home reset view";

        private const float MinPitch = 18f;
        private const float MaxPitch = 86f;
        private const float MinDistance = 6f;
        private const float MaxDistance = 95f;
        private const float DefaultPitch = 55f;
        private const float DefaultYaw = 0f;
        private const float FollowDistance = 17f;
        private const float RotateSpeed = 0.22f;
        private const float KeyRotateSpeed = 90f;
        private const float SmoothTime = 0.12f;
        private const float FollowSmoothTime = 0.2f;

        [SerializeField] private float fieldOfView = 38f;

        private Camera arenaCamera;
        private ArenaSim sim;
        private ViewMode mode = ViewMode.WholeArena;

        private float yaw = DefaultYaw;
        private float pitch = DefaultPitch;
        private float distance = 30f;
        private Vector3 target;
        private float desiredYaw = DefaultYaw;
        private float desiredPitch = DefaultPitch;
        private float desiredDistance = 30f;
        private Vector3 desiredTarget;
        private float yawVelocity;
        private float pitchVelocity;
        private float distanceVelocity;
        private Vector3 targetVelocity;

        private bool rotating;
        private bool panning;
        private Vector2 lastPointer;
        private float fittedAspect;
        private float fitDistance = 30f;
        private bool manualPlay;

        public Camera Camera => arenaCamera != null ? arenaCamera : GetComponent<Camera>();

        public ViewMode Mode => mode;

        public string ModeLabel
        {
            get
            {
                switch (mode)
                {
                    case ViewMode.FollowHero:
                        return "Camera: follow warrior";
                    case ViewMode.Free:
                        return "Camera: free";
                    default:
                        return "Camera: whole arena";
                }
            }
        }

        public void Bind(ArenaSim arenaSim)
        {
            bool first = sim == null;
            sim = arenaSim;
            EnsureCamera();
            if (sim == null)
            {
                return;
            }

            fitDistance = ComputeFitDistance(desiredYaw, desiredPitch);
            fittedAspect = arenaCamera.aspect;
            if (first)
            {
                ResetView();
                SnapToDesired();
            }
            else if (mode == ViewMode.FollowHero)
            {
                // New episode: jump straight to the warrior instead of sweeping across the arena.
                desiredTarget = HeroPosition();
                target = desiredTarget;
                targetVelocity = Vector3.zero;
            }
        }

        public void CycleMode()
        {
            switch (mode)
            {
                case ViewMode.WholeArena:
                    SetMode(ViewMode.FollowHero);
                    break;
                case ViewMode.FollowHero:
                    SetMode(ViewMode.Free);
                    break;
                default:
                    SetMode(ViewMode.WholeArena);
                    break;
            }
        }

        public void ResetView()
        {
            desiredYaw = DefaultYaw;
            desiredPitch = DefaultPitch;
            SetMode(ViewMode.WholeArena);
        }

        private void SetMode(ViewMode newMode)
        {
            mode = newMode;
            if (sim == null)
            {
                return;
            }

            switch (mode)
            {
                case ViewMode.WholeArena:
                    desiredTarget = ArenaCenter();
                    fitDistance = ComputeFitDistance(desiredYaw, desiredPitch);
                    desiredDistance = fitDistance;
                    break;
                case ViewMode.FollowHero:
                    desiredTarget = HeroPosition();
                    desiredDistance = Mathf.Min(FollowDistance, fitDistance);
                    break;
            }
        }

        private void Awake()
        {
            EnsureCamera();
            // Manual play uses Q/E and both mouse buttons for the warrior, so the camera keeps to middle-drag and scroll.
            manualPlay = FindFirstObjectByType<KeyboardArenaController>() != null;
        }

        private void LateUpdate()
        {
            if (sim == null)
            {
                return;
            }

            float delta = Time.unscaledDeltaTime;
            HandleInput(delta);

            if (!Mathf.Approximately(fittedAspect, arenaCamera.aspect))
            {
                fittedAspect = arenaCamera.aspect;
                fitDistance = ComputeFitDistance(desiredYaw, desiredPitch);
                if (mode == ViewMode.WholeArena)
                {
                    desiredDistance = fitDistance;
                }
            }

            if (mode == ViewMode.FollowHero)
            {
                desiredTarget = HeroPosition();
            }
            desiredTarget = ClampTarget(desiredTarget);

            yaw = Mathf.SmoothDampAngle(yaw, desiredYaw, ref yawVelocity, SmoothTime, Mathf.Infinity, delta);
            pitch = Mathf.SmoothDamp(pitch, desiredPitch, ref pitchVelocity, SmoothTime, Mathf.Infinity, delta);
            distance = Mathf.SmoothDamp(distance, desiredDistance, ref distanceVelocity, SmoothTime, Mathf.Infinity, delta);
            target = Vector3.SmoothDamp(target, desiredTarget, ref targetVelocity,
                mode == ViewMode.FollowHero ? FollowSmoothTime : SmoothTime, Mathf.Infinity, delta);
            ApplyPose();
        }

        private void HandleInput(float delta)
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            bool shift = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);

            if (keyboard != null)
            {
                if (keyboard.cKey.wasPressedThisFrame)
                {
                    CycleMode();
                }
                if (keyboard.homeKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame)
                {
                    ResetView();
                }
                if (!manualPlay && keyboard.qKey.isPressed)
                {
                    desiredYaw += KeyRotateSpeed * delta;
                }
                if (!manualPlay && keyboard.eKey.isPressed)
                {
                    desiredYaw -= KeyRotateSpeed * delta;
                }
                float zoomKeys = 0f;
                if (keyboard.equalsKey.isPressed || keyboard.numpadPlusKey.isPressed)
                {
                    zoomKeys -= 1f;
                }
                if (keyboard.minusKey.isPressed || keyboard.numpadMinusKey.isPressed)
                {
                    zoomKeys += 1f;
                }
                if (zoomKeys != 0f)
                {
                    Zoom(zoomKeys * 2.5f * delta);
                }
            }

            if (mouse == null)
            {
                rotating = false;
                panning = false;
                return;
            }

            Vector2 pointer = mouse.position.ReadValue();
            bool overUi = PointerOverUi();
            if (!rotating && !panning)
            {
                bool leftPressed = mouse.leftButton.wasPressedThisFrame;
                bool rightPressed = mouse.rightButton.wasPressedThisFrame;
                bool middlePressed = mouse.middleButton.wasPressedThisFrame;
                if (manualPlay)
                {
                    rotating = !overUi && middlePressed;
                    lastPointer = pointer;
                }
                else if (!overUi && (middlePressed || (leftPressed && shift)))
                {
                    panning = true;
                    lastPointer = pointer;
                }
                else if (!overUi && (leftPressed || rightPressed))
                {
                    rotating = true;
                    lastPointer = pointer;
                }
            }

            Vector2 moved = pointer - lastPointer;
            lastPointer = pointer;
            if (rotating)
            {
                bool held = manualPlay
                    ? mouse.middleButton.isPressed
                    : mouse.leftButton.isPressed || mouse.rightButton.isPressed;
                if (!held)
                {
                    rotating = false;
                }
                else
                {
                    desiredYaw += moved.x * RotateSpeed;
                    desiredPitch = Mathf.Clamp(desiredPitch - moved.y * RotateSpeed, MinPitch, MaxPitch);
                }
            }
            else if (panning)
            {
                if (!mouse.middleButton.isPressed && !mouse.leftButton.isPressed)
                {
                    panning = false;
                }
                else if (moved.sqrMagnitude > 0f)
                {
                    Pan(moved);
                }
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (!overUi && Mathf.Abs(scroll) > 0.01f)
            {
                // Windows reports 120 per wheel notch, other platforms about 1.
                float notches = Mathf.Abs(scroll) >= 20f ? scroll / 120f : scroll;
                Zoom(-Mathf.Clamp(notches, -4f, 4f) * 0.14f);
            }
        }

        private void Zoom(float logAmount)
        {
            desiredDistance = Mathf.Clamp(desiredDistance * Mathf.Exp(logAmount), MinDistance, MaxDistance);
        }

        private void Pan(Vector2 pixels)
        {
            if (mode != ViewMode.Free)
            {
                mode = ViewMode.Free;
            }

            // Move the look point so the ground under the cursor roughly follows the drag.
            float worldPerPixel = 2f * distance * Mathf.Tan(arenaCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) /
                Mathf.Max(1f, arenaCamera.pixelHeight);
            Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
            Vector3 right = heading * Vector3.right;
            Vector3 forward = heading * Vector3.forward;
            float verticalScale = 1f / Mathf.Max(0.35f, Mathf.Sin(pitch * Mathf.Deg2Rad));
            desiredTarget -= (right * pixels.x + forward * pixels.y * verticalScale) * worldPerPixel;
        }

        private static bool PointerOverUi()
        {
            EventSystem events = EventSystem.current;
            return events != null && events.IsPointerOverGameObject();
        }

        private float ComputeFitDistance(float viewYaw, float viewPitch)
        {
            if (sim == null)
            {
                return 30f;
            }

            // Binary-search the closest distance that keeps the whole platform rim in view.
            Vector3 center = ArenaCenter();
            float radius = sim.Config.Radius + 0.8f;
            Quaternion rotation = Quaternion.Euler(viewPitch, viewYaw, 0f);
            float near = MinDistance;
            float far = MaxDistance;
            for (int i = 0; i < 22; i++)
            {
                float candidate = 0.5f * (near + far);
                transform.SetPositionAndRotation(center - rotation * Vector3.forward * candidate, rotation);
                if (RimFits(center, radius))
                {
                    far = candidate;
                }
                else
                {
                    near = candidate;
                }
            }
            ApplyPose();
            return far;
        }

        private bool RimFits(Vector3 center, float radius)
        {
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2f / 24f;
                for (int level = 0; level < 2; level++)
                {
                    Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, level == 0 ? -0.6f : 1.8f, Mathf.Sin(angle) * radius);
                    Vector3 viewport = arenaCamera.WorldToViewportPoint(point);
                    if (viewport.z <= 0f || viewport.x < 0.03f || viewport.x > 0.97f || viewport.y < 0.1f || viewport.y > 0.9f)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private void SnapToDesired()
        {
            yaw = desiredYaw;
            pitch = desiredPitch;
            distance = desiredDistance;
            target = desiredTarget;
            yawVelocity = 0f;
            pitchVelocity = 0f;
            distanceVelocity = 0f;
            targetVelocity = Vector3.zero;
            ApplyPose();
        }

        private Vector3 ClampTarget(Vector3 point)
        {
            if (sim == null)
            {
                return point;
            }

            Vector3 center = ArenaCenter();
            Vector3 offset = point - center;
            offset.y = 0f;
            float limit = sim.Config.Radius * 1.25f;
            if (offset.sqrMagnitude > limit * limit)
            {
                offset = offset.normalized * limit;
            }
            return center + offset;
        }

        private Vector3 HeroPosition()
        {
            return sim != null ? ArenaSpace.ToWorld(sim.Hero.Position) : Vector3.zero;
        }

        private Vector3 ArenaCenter()
        {
            return ArenaSpace.ToWorld(sim.Config.Center);
        }

        private void EnsureCamera()
        {
            if (arenaCamera == null)
            {
                arenaCamera = GetComponent<Camera>();
                arenaCamera.orthographic = false;
                arenaCamera.fieldOfView = fieldOfView;
                arenaCamera.clearFlags = CameraClearFlags.SolidColor;
                arenaCamera.backgroundColor = new Color(0.05f, 0.035f, 0.085f);
                arenaCamera.nearClipPlane = 0.3f;
                arenaCamera.farClipPlane = 300f;
            }
        }

        private void ApplyPose()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(target - rotation * Vector3.forward * distance, rotation);
        }
    }
}
