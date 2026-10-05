using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using VTG.Pool.Aiming;
using VTG.Pool.Balls;
using VTG.Pool.Inputs;
using VTG.Pool.Table;

namespace VTG.Pool.CameraSystem
{
    /// <summary>
    /// Computes poses for every camera mode (Cue, Tactical, Top, Cinematic) from the cue ball, aim and
    /// user orbit/zoom, and hands them to an <see cref="ICameraRigOutput"/> (Cinemachine or fallback).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class CameraController : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSource;
        [SerializeField] private MonoBehaviour outputBehaviour;
        [SerializeField] private AimSystem aimSystem;
        [SerializeField] private TableBuilder table;
        [SerializeField] private CameraMode startMode = CameraMode.Cue;

        [Header("Cue camera")]
        [SerializeField] private float cueDistance = 0.85f;
        [SerializeField] private Vector2 cueDistanceRange = new Vector2(0.3f, 2.2f);
        [SerializeField] private float cuePitch = 14f;
        [SerializeField] private Vector2 cuePitchRange = new Vector2(3f, 70f);
        [SerializeField] private float cueLookAhead = 0.25f;
        [SerializeField] private float cueFov = 45f;

        [Header("Tactical camera")]
        [SerializeField] private float tacticalDistance = 2.1f;
        [SerializeField] private Vector2 tacticalDistanceRange = new Vector2(0.8f, 4f);
        [SerializeField] private float tacticalPitch = 48f;
        [SerializeField] private Vector2 tacticalPitchRange = new Vector2(15f, 85f);
        [SerializeField] private float tacticalFov = 45f;

        [Header("Top camera")]
        [SerializeField] private float topFov = 40f;
        [SerializeField] private float topMargin = 0.12f;
        [SerializeField, Range(0f, 0.4f), Tooltip("Fraction of the screen height covered by HUD at the top (kept clear of the table).")]
        private float topHudInset = 0.15f;
        [SerializeField, Range(0f, 0.4f), Tooltip("Fraction of the screen height covered by HUD at the bottom.")]
        private float bottomHudInset = 0.1f;

        [Header("Cinematic camera")]
        [SerializeField] private float cinematicOrbitSpeed = 8f;
        [SerializeField] private float cinematicDistance = 3.2f;
        [SerializeField] private float cinematicHeight = 1.4f;

        [Header("Orbit camera (free look around the table)")]
        [SerializeField] private float orbitDistance = 2.6f;
        [SerializeField] private Vector2 orbitDistanceRange = new Vector2(0.9f, 5f);
        [SerializeField] private float orbitPitch = 38f;
        [SerializeField] private Vector2 orbitPitchRange = new Vector2(8f, 88f);
        [SerializeField, Tooltip("Degrees per screen pixel when dragging.")] private float orbitDragSensitivity = 0.25f;
        [SerializeField] private float orbitFov = 45f;

        [Header("Smoothing")]
        [SerializeField] private float pivotSmoothTime = 0.12f;

        private IShotInput input;
        private ICameraRigOutput output;
        private Camera mainCamera;
        private Vector3 pivot;
        private Vector3 pivotVelocity;
        private float tacticalYawOffset;
        private float cinematicYaw;
        private bool initialized;
        private bool pivotInitialized;
        private float orbitYaw;
        private float orbitYawTarget;
        private float orbitPitchTarget;
        private float orbitDistanceTarget;
        private bool orbitTargetsInitialized;

        public CameraMode ActiveMode { get; private set; }

        /// <summary>
        /// Orbit mode: a plain drag (left mouse / one finger) turns the camera. On for spectators, whose pointer does not
        /// aim; players orbit with the look input (right drag / two fingers) instead.
        /// </summary>
        public bool OrbitWithPrimaryDrag { get; set; }

        /// <summary>Orbit yaw / pitch (degrees) and distance (m) the orbit camera is heading for.</summary>
        public Vector3 OrbitState => new Vector3(orbitYawTarget, orbitPitchTarget, orbitDistanceTarget);

        /// <summary>Turns the orbit camera (degrees) and zooms (fraction, + = closer). Used by input and tests.</summary>
        public void OrbitBy(Vector2 degrees, float zoom = 0f)
        {
            EnsureOrbitTargets();
            orbitYawTarget += degrees.x;
            orbitPitchTarget = Mathf.Clamp(orbitPitchTarget - degrees.y, orbitPitchRange.x, orbitPitchRange.y);
            orbitDistanceTarget = Mathf.Clamp(orbitDistanceTarget * (1f - zoom), orbitDistanceRange.x, orbitDistanceRange.y);
        }

        private void EnsureOrbitTargets()
        {
            if (orbitTargetsInitialized)
            {
                return;
            }

            orbitTargetsInitialized = true;
            orbitYawTarget = orbitYaw = table != null ? table.transform.eulerAngles.y + 90f : 0f;
            orbitPitchTarget = orbitPitch;
            orbitDistanceTarget = orbitDistance;
        }

        /// <summary>When set, the Cinematic mode uses this pose instead of the slow orbit (shot cinematics).</summary>
        public System.Func<CameraPose> CinematicOverride { get; set; }

        /// <summary>Default camera for this scene (player setting). Applied immediately if already running.</summary>
        public void SetStartMode(CameraMode mode)
        {
            startMode = mode;
            if (initialized)
            {
                SetMode(mode);
            }
            else
            {
                ActiveMode = mode;
            }
        }

        public void SetMode(CameraMode mode)
        {
            if (mode == ActiveMode && initialized)
            {
                return;
            }

            CameraMode previous = ActiveMode;
            ActiveMode = mode;
            output?.OnModeChanged(previous, mode);
        }

        private void Awake()
        {
            if (inputSource is IShotInput shotInput)
            {
                input = shotInput;
            }

            output = outputBehaviour as ICameraRigOutput;
            if (output == null)
            {
                output = GetComponent<ICameraRigOutput>();
            }

            if (output == null)
            {
                output = gameObject.AddComponent<DirectCameraOutput>();
            }

            mainCamera = Camera.main;
            ActiveMode = startMode;
            if (GetComponent<ShotCinematics>() == null)
            {
                gameObject.AddComponent<ShotCinematics>().Configure(this, null);
            }

            if (GetComponent<Replay.ShotReplay>() == null)
            {
                gameObject.AddComponent<Replay.ShotReplay>();
            }
        }

        private void Start()
        {
            output.OnModeChanged(ActiveMode, ActiveMode);
            initialized = true;
        }

        private void Update()
        {
            if (input == null)
            {
                return;
            }

            if (input.CameraCuePressed) SetMode(CameraMode.Cue);
            if (input.CameraTacticalPressed) SetMode(CameraMode.Tactical);
            if (input.CameraTopPressed) SetMode(CameraMode.Top);
            if (Keyboard.current != null && (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame)) SetMode(CameraMode.Orbit);

            Vector2 look = input.LookDeltaDegrees;
            float zoom = input.ZoomDelta;
            switch (ActiveMode)
            {
                case CameraMode.Cue:
                    cuePitch = Mathf.Clamp(cuePitch - look.y, cuePitchRange.x, cuePitchRange.y);
                    cueDistance = Mathf.Clamp(cueDistance * (1f - zoom), cueDistanceRange.x, cueDistanceRange.y);
                    break;
                case CameraMode.Orbit:
                    OrbitBy(look + PrimaryDragDegrees(), zoom);
                    break;
                case CameraMode.Tactical:
                    tacticalYawOffset += look.x;
                    tacticalPitch = Mathf.Clamp(tacticalPitch - look.y, tacticalPitchRange.x, tacticalPitchRange.y);
                    tacticalDistance = Mathf.Clamp(tacticalDistance * (1f - zoom), tacticalDistanceRange.x, tacticalDistanceRange.y);
                    break;
            }
        }

        private void LateUpdate()
        {
            Vector3 target = FocusPoint();
            if (pivotInitialized)
            {
                pivot = Vector3.SmoothDamp(pivot, target, ref pivotVelocity, pivotSmoothTime);
            }
            else
            {
                pivot = target;
                pivotInitialized = true;
            }

            if (output == null)
            {
                // Non-serialized references are lost if scripts reload during Play mode: re-acquire.
                output = outputBehaviour as ICameraRigOutput ?? GetComponent<ICameraRigOutput>();
                if (output == null)
                {
                    return;
                }
            }

            float aimYaw = aimSystem != null ? aimSystem.AimYawDegrees : 0f;
            cinematicYaw += cinematicOrbitSpeed * Time.deltaTime;

            output.Apply(ActiveMode, CameraMode.Cue, CuePose(aimYaw));
            output.Apply(ActiveMode, CameraMode.Tactical, TacticalPose(aimYaw));
            output.Apply(ActiveMode, CameraMode.Top, TopPose());
            output.Apply(ActiveMode, CameraMode.Cinematic, CinematicPose());
            output.Apply(ActiveMode, CameraMode.Orbit, OrbitPose());
        }

        /// <summary>Spectators: left-drag / one-finger drag over the game (not over UI) turns the orbit camera.</summary>
        private Vector2 PrimaryDragDegrees()
        {
            if (!OrbitWithPrimaryDrag)
            {
                return Vector2.zero;
            }

            Vector2 delta = Vector2.zero;
            if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            {
                int active = 0;
                foreach (var touch in Touchscreen.current.touches)
                {
                    if (touch.press.isPressed) active++;
                }

                var primary = Touchscreen.current.primaryTouch;
                if (active == 1 && primary.press.isPressed && !OverUI(primary.touchId.ReadValue()))
                {
                    delta = primary.delta.ReadValue();
                }
            }

            if (delta == Vector2.zero && Mouse.current != null && Mouse.current.leftButton.isPressed && !OverUI(-1))
            {
                delta = Mouse.current.delta.ReadValue();
            }

            // Pixels → degrees, independent of resolution (reference 1080 p).
            float scale = orbitDragSensitivity * 1080f / Mathf.Max(360f, Screen.height);
            return delta * scale;
        }

        private static bool OverUI(int pointerId)
        {
            EventSystem events = EventSystem.current;
            return events != null && (pointerId >= 0 ? events.IsPointerOverGameObject(pointerId) : events.IsPointerOverGameObject());
        }

        private Vector3 FocusPoint()
        {
            PoolBall cueBall = aimSystem != null ? aimSystem.CueBall : null;
            if (cueBall != null && !cueBall.IsPocketed)
            {
                return cueBall.transform.position;
            }

            return table != null ? table.SurfaceCenter : Vector3.zero;
        }

        private CameraPose CuePose(float aimYaw)
        {
            Quaternion rotation = Quaternion.Euler(cuePitch, aimYaw, 0f);
            Vector3 aimForward = Quaternion.Euler(0f, aimYaw, 0f) * Vector3.forward;
            Vector3 focus = pivot + aimForward * cueLookAhead;
            Vector3 position = focus - rotation * Vector3.forward * (cueDistance + cueLookAhead);
            return new CameraPose(position, rotation, cueFov);
        }

        private CameraPose TacticalPose(float aimYaw)
        {
            Quaternion rotation = Quaternion.Euler(tacticalPitch, aimYaw + tacticalYawOffset, 0f);
            Vector3 position = pivot - rotation * Vector3.forward * tacticalDistance;
            return new CameraPose(position, rotation, tacticalFov);
        }

        private CameraPose TopPose()
        {
            Vector3 center = table != null ? table.SurfaceCenter : Vector3.zero;
            float halfLength = table != null && table.Geometry != null ? table.Geometry.HalfLength : 1.27f;
            float halfWidth = table != null && table.Geometry != null ? table.Geometry.HalfWidth : 0.635f;
            float aspect = mainCamera != null ? mainCamera.aspect : 16f / 9f;

            // Long axis horizontal on screen: it must fit the horizontal FOV, the short axis the vertical FOV.
            float verticalHalf = Mathf.Tan(topFov * 0.5f * Mathf.Deg2Rad);
            float horizontalHalf = verticalHalf * aspect;
            // The short axis must fit the screen band between the top and bottom HUD.
            float band = Mathf.Max(0.2f, 1f - topHudInset - bottomHudInset);
            float height = Mathf.Max((halfLength + topMargin) / horizontalHalf, (halfWidth + topMargin) / (verticalHalf * band));
            Quaternion rotation = Quaternion.Euler(90f, -90f, 0f);

            // Centre the table in that band: shift the camera along its screen-up axis.
            float visibleHalfHeight = height * verticalHalf;
            Vector3 screenUp = rotation * Vector3.up;
            Vector3 shift = screenUp * (visibleHalfHeight * (topHudInset - bottomHudInset));
            return new CameraPose(center + Vector3.up * height + shift, rotation, topFov);
        }

        private CameraPose OrbitPose()
        {
            EnsureOrbitTargets();
            float smoothing = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
            orbitYaw = Mathf.LerpAngle(orbitYaw, orbitYawTarget, smoothing);
            orbitPitch = Mathf.Lerp(orbitPitch, orbitPitchTarget, smoothing);
            orbitDistance = Mathf.Lerp(orbitDistance, orbitDistanceTarget, smoothing);
            Vector3 center = table != null ? table.SurfaceCenter : Vector3.zero;
            Quaternion rotation = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
            Vector3 position = center - rotation * Vector3.forward * orbitDistance;
            float floor = (table != null ? table.SurfaceHeight : 0f) + 0.08f;
            if (position.y < floor)
            {
                position.y = floor;
                rotation = Quaternion.LookRotation(center - position, Vector3.up);
            }

            return new CameraPose(position, rotation, orbitFov);
        }

        private CameraPose CinematicPose()
        {
            if (CinematicOverride != null)
            {
                return CinematicOverride();
            }

            Vector3 center = table != null ? table.SurfaceCenter : Vector3.zero;
            Quaternion orbit = Quaternion.Euler(0f, cinematicYaw, 0f);
            Vector3 position = center + orbit * new Vector3(0f, cinematicHeight, -cinematicDistance);
            Quaternion rotation = Quaternion.LookRotation(center - position, Vector3.up);
            return new CameraPose(position, rotation, 40f);
        }
    }
}
