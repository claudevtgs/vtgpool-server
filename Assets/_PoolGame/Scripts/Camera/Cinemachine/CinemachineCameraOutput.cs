using Unity.Cinemachine;
using UnityEngine;

namespace VTG.Pool.CameraSystem
{
    /// <summary>
    /// Cinemachine camera output: one CinemachineCamera per mode, posed by CameraController every frame.
    /// The CinemachineBrain on the main camera blends when the active mode changes.
    /// Compiled only when com.unity.cinemachine is installed (VTG_POOL_CINEMACHINE).
    /// </summary>
    public sealed class CinemachineCameraOutput : MonoBehaviour, ICameraRigOutput
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float blendTime = 0.6f;

        private readonly CinemachineCamera[] cameras = new CinemachineCamera[System.Enum.GetValues(typeof(CameraMode)).Length];

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera != null)
            {
                CinemachineBrain brain = targetCamera.GetComponent<CinemachineBrain>();
                if (brain == null)
                {
                    brain = targetCamera.gameObject.AddComponent<CinemachineBrain>();
                }

                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blendTime);
            }

            for (int i = 0; i < cameras.Length; i++)
            {
                CameraMode mode = (CameraMode)i;
                Transform existing = transform.Find("CM_" + mode);
                GameObject cameraObject = existing != null ? existing.gameObject : new GameObject("CM_" + mode);
                cameraObject.transform.SetParent(transform, false);
                CinemachineCamera cinemachineCamera = cameraObject.GetComponent<CinemachineCamera>();
                if (cinemachineCamera == null)
                {
                    cinemachineCamera = cameraObject.AddComponent<CinemachineCamera>();
                }

                cameras[i] = cinemachineCamera;
            }
        }

        public void OnModeChanged(CameraMode previous, CameraMode next)
        {
            CinemachineCamera active = cameras[(int)next];
            if (active != null)
            {
                active.Prioritize();
            }
        }

        public void Apply(CameraMode activeMode, CameraMode mode, in CameraPose pose)
        {
            CinemachineCamera cinemachineCamera = cameras[(int)mode];
            if (cinemachineCamera == null)
            {
                return;
            }

            cinemachineCamera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            LensSettings lens = cinemachineCamera.Lens;
            lens.FieldOfView = pose.FieldOfView;
            lens.NearClipPlane = 0.02f;
            lens.FarClipPlane = 100f;
            cinemachineCamera.Lens = lens;
        }
    }
}
