using UnityEngine;

namespace VTG.Pool.CameraSystem
{
    /// <summary>
    /// Fallback camera output (no Cinemachine): drives Camera.main directly and blends between modes.
    /// </summary>
    public sealed class DirectCameraOutput : MonoBehaviour, ICameraRigOutput
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float blendTime = 0.6f;

        private CameraPose blendFrom;
        private float blendElapsed = float.MaxValue;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        public void OnModeChanged(CameraMode previous, CameraMode next)
        {
            if (targetCamera == null)
            {
                return;
            }

            blendFrom = new CameraPose(targetCamera.transform.position, targetCamera.transform.rotation, targetCamera.fieldOfView);
            blendElapsed = 0f;
        }

        public void Apply(CameraMode activeMode, CameraMode mode, in CameraPose pose)
        {
            if (mode != activeMode || targetCamera == null)
            {
                return;
            }

            CameraPose result = pose;
            if (blendElapsed < blendTime)
            {
                blendElapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blendElapsed / blendTime));
                result.Position = Vector3.Lerp(blendFrom.Position, pose.Position, t);
                result.Rotation = Quaternion.Slerp(blendFrom.Rotation, pose.Rotation, t);
                result.FieldOfView = Mathf.Lerp(blendFrom.FieldOfView, pose.FieldOfView, t);
            }

            targetCamera.transform.SetPositionAndRotation(result.Position, result.Rotation);
            targetCamera.fieldOfView = result.FieldOfView;
        }
    }
}
