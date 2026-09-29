using PersonalArena.Core;
using UnityEngine;

namespace PersonalArena.View
{
    [RequireComponent(typeof(Camera))]
    public sealed class TopDownCamera : MonoBehaviour
    {
        [SerializeField] private float padding = 2.5f;
        [SerializeField] private float followSize = 12f;
        [SerializeField] private float followSmoothTime = 0.18f;

        private Camera arenaCamera;
        private ArenaSim sim;
        private Vector3 lookTarget;
        private Vector3 targetVelocity;
        private Vector3 cameraOffset;
        private bool followsHero;

        public Camera Camera => arenaCamera != null ? arenaCamera : GetComponent<Camera>();

        public void Bind(ArenaSim arenaSim)
        {
            sim = arenaSim;
            EnsureCamera();
            if (sim == null)
            {
                return;
            }

            float width = sim.Config.Width;
            float height = sim.Config.Height;
            followsHero = Mathf.Max(width, height) > 30f;
            float aspect = Mathf.Max(0.1f, arenaCamera.aspect);
            arenaCamera.orthographic = true;
            arenaCamera.orthographicSize = followsHero
                ? Mathf.Min(followSize, Mathf.Min(height * 0.5f, width / (2f * aspect)))
                : Mathf.Max(height * 0.5f + padding, width / (2f * aspect) + padding);

            lookTarget = followsHero
                ? ArenaSpace.ToWorld(sim.Hero.Position)
                : new Vector3(width * 0.5f, 0f, height * 0.5f);
            float distance = Mathf.Max(14f, arenaCamera.orthographicSize * 1.8f);
            cameraOffset = new Vector3(0f, distance, -distance * 0.72f);
            ApplyPose(lookTarget);
        }

        private void Awake()
        {
            EnsureCamera();
        }

        private void LateUpdate()
        {
            if (sim == null || !followsHero)
            {
                return;
            }

            Vector3 desired = ArenaSpace.ToWorld(sim.Hero.Position);
            float verticalExtent = arenaCamera.orthographicSize;
            float horizontalExtent = verticalExtent * arenaCamera.aspect;
            desired.x = Mathf.Clamp(desired.x, horizontalExtent, Mathf.Max(horizontalExtent, sim.Config.Width - horizontalExtent));
            desired.z = Mathf.Clamp(desired.z, verticalExtent, Mathf.Max(verticalExtent, sim.Config.Height - verticalExtent));
            lookTarget = Vector3.SmoothDamp(lookTarget, desired, ref targetVelocity, followSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            ApplyPose(lookTarget);
        }

        private void EnsureCamera()
        {
            if (arenaCamera == null)
            {
                arenaCamera = GetComponent<Camera>();
                arenaCamera.clearFlags = CameraClearFlags.SolidColor;
                arenaCamera.backgroundColor = new Color(0.055f, 0.065f, 0.08f);
                arenaCamera.nearClipPlane = 0.1f;
                arenaCamera.farClipPlane = 250f;
            }
        }

        private void ApplyPose(Vector3 target)
        {
            transform.position = target + cameraOffset;
            transform.rotation = Quaternion.LookRotation(target - transform.position, Vector3.up);
        }
    }
}
