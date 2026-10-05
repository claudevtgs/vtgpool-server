using UnityEngine;

namespace VTG.Pool.CameraSystem
{
    /// <summary>Camera modes (MASTER_PROMPT section 22).</summary>
    public enum CameraMode
    {
        Cue,
        Tactical,
        Top,
        Cinematic,

        /// <summary>Free orbit around the table (spectators; players can pick it too).</summary>
        Orbit
    }

    /// <summary>A desired camera pose computed by <see cref="CameraController"/>.</summary>
    public struct CameraPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float FieldOfView;

        public CameraPose(Vector3 position, Quaternion rotation, float fieldOfView)
        {
            Position = position;
            Rotation = rotation;
            FieldOfView = fieldOfView;
        }
    }

    /// <summary>
    /// Applies camera poses to actual cameras. Implemented by the Cinemachine output (preferred) and by
    /// a direct fallback, so camera logic does not depend on a specific camera package.
    /// </summary>
    public interface ICameraRigOutput
    {
        /// <summary>Called every LateUpdate with the pose of each mode; the active mode is shown.</summary>
        void Apply(CameraMode activeMode, CameraMode mode, in CameraPose pose);

        /// <summary>Called when the active mode changes (start a blend).</summary>
        void OnModeChanged(CameraMode previous, CameraMode next);
    }
}
