using PersonalArena.Core;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// Tilted perspective camera. Small arenas are framed whole; large arenas follow the hero.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class TopDownCamera : MonoBehaviour
    {
        [SerializeField] private float fieldOfView = 34f;
        [SerializeField] private float pitch = 56f;
        [SerializeField] private float padding = 0.6f;
        [SerializeField] private float wallHeight = 2f;
        [SerializeField] private float followDistance = 24f;
        [SerializeField] private float followSmoothTime = 0.18f;

        private Camera arenaCamera;
        private ArenaSim sim;
        private Vector3 lookTarget;
        private Vector3 targetVelocity;
        private Vector3 cameraOffset;
        private bool followsHero;
        private float fittedAspect;

        public Camera Camera => arenaCamera != null ? arenaCamera : GetComponent<Camera>();

        public void Bind(ArenaSim arenaSim)
        {
            sim = arenaSim;
            EnsureCamera();
            if (sim == null)
            {
                return;
            }

            followsHero = Mathf.Max(sim.Config.Width, sim.Config.Height) > 30f;
            lookTarget = followsHero ? ArenaSpace.ToWorld(sim.Hero.Position) : ArenaCenter();
            Frame();
        }

        private void Awake()
        {
            EnsureCamera();
        }

        private void LateUpdate()
        {
            if (sim == null)
            {
                return;
            }

            if (!followsHero)
            {
                if (!Mathf.Approximately(fittedAspect, arenaCamera.aspect))
                {
                    Frame();
                }
                return;
            }

            Vector3 desired = ArenaSpace.ToWorld(sim.Hero.Position);
            desired.x = Mathf.Clamp(desired.x, 0f, sim.Config.Width);
            desired.z = Mathf.Clamp(desired.z, 0f, sim.Config.Height);
            lookTarget = Vector3.SmoothDamp(lookTarget, desired, ref targetVelocity, followSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            ApplyPose(lookTarget);
        }

        private void Frame()
        {
            arenaCamera.orthographic = false;
            arenaCamera.fieldOfView = fieldOfView;
            fittedAspect = arenaCamera.aspect;
            Vector3 direction = Quaternion.Euler(pitch, 0f, 0f) * Vector3.back;

            if (followsHero)
            {
                cameraOffset = direction * followDistance;
                ApplyPose(lookTarget);
                return;
            }

            // Binary-search the closest distance that keeps the whole arena (and its walls) in view.
            float near = 5f;
            float far = 200f;
            for (int i = 0; i < 24; i++)
            {
                float distance = 0.5f * (near + far);
                cameraOffset = direction * distance;
                ApplyPose(lookTarget);
                if (ArenaFits())
                {
                    far = distance;
                }
                else
                {
                    near = distance;
                }
            }
            cameraOffset = direction * far;
            ApplyPose(lookTarget);
        }

        private bool ArenaFits()
        {
            float minX = -padding;
            float maxX = sim.Config.Width + padding;
            float minZ = -padding;
            float maxZ = sim.Config.Height + padding;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new Vector3(
                    (corner & 1) == 0 ? minX : maxX,
                    (corner & 4) == 0 ? 0f : wallHeight,
                    (corner & 2) == 0 ? minZ : maxZ);
                Vector3 viewport = arenaCamera.WorldToViewportPoint(point);
                if (viewport.z <= 0f || viewport.x < 0.02f || viewport.x > 0.98f || viewport.y < 0.02f || viewport.y > 0.98f)
                {
                    return false;
                }
            }
            return true;
        }

        private Vector3 ArenaCenter()
        {
            return new Vector3(sim.Config.Width * 0.5f, 0f, sim.Config.Height * 0.5f);
        }

        private void EnsureCamera()
        {
            if (arenaCamera == null)
            {
                arenaCamera = GetComponent<Camera>();
                arenaCamera.clearFlags = CameraClearFlags.SolidColor;
                arenaCamera.backgroundColor = new Color(0.035f, 0.035f, 0.05f);
                arenaCamera.nearClipPlane = 0.3f;
                arenaCamera.farClipPlane = 300f;
            }
        }

        private void ApplyPose(Vector3 target)
        {
            transform.position = target + cameraOffset;
            transform.rotation = Quaternion.LookRotation(target - transform.position, Vector3.up);
        }
    }
}
