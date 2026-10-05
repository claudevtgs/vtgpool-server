using UnityEngine;

namespace VTG.Pool.UI
{
    /// <summary>Slow cinematic orbit around a point (main menu background).</summary>
    public sealed class MenuCameraOrbit : MonoBehaviour
    {
        [SerializeField] private Vector3 center = new Vector3(0f, 0.76f, 0f);
        [SerializeField] private float radius = 2.6f;
        [SerializeField] private float height = 1.35f;
        [SerializeField] private float degreesPerSecond = 5f;
        [SerializeField] private float startAngle = 210f;

        private float angle;
        private float blendDuration;
        private float blendTime;
        private Vector3 blendFromPosition;
        private Quaternion blendFromRotation;
        private float blendFromFov;
        private Camera view;

        public void Configure(Vector3 orbitCenter) => center = orbitCenter;

        private void Start()
        {
            angle = startAngle;
            view = GetComponent<Camera>();
        }

        /// <summary>Eases from the camera's current pose into the orbit (after the intro).</summary>
        public void BlendIn(float seconds)
        {
            blendDuration = seconds;
            blendTime = 0f;
            blendFromPosition = transform.position;
            blendFromRotation = transform.rotation;
            view = view != null ? view : GetComponent<Camera>();
            blendFromFov = view != null ? view.fieldOfView : 42f;
        }

        private void LateUpdate()
        {
            angle += degreesPerSecond * Time.unscaledDeltaTime;
            float radians = angle * Mathf.Deg2Rad;
            Vector3 position = center + new Vector3(Mathf.Sin(radians) * radius, height, Mathf.Cos(radians) * radius * 1.3f);
            Quaternion rotation = Quaternion.LookRotation(center + Vector3.up * 0.05f - position, Vector3.up);
            if (blendTime < blendDuration)
            {
                blendTime += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, blendTime / blendDuration);
                position = Vector3.Lerp(blendFromPosition, position, t);
                rotation = Quaternion.Slerp(blendFromRotation, rotation, t);
                if (view != null) view.fieldOfView = Mathf.Lerp(blendFromFov, 42f, t);
            }

            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
