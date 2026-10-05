using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace VTG.Pool.Inputs
{
    /// <summary>
    /// Reads the "Gameplay" map of PoolInput.inputactions and exposes it as <see cref="IShotInput"/>.
    /// Converts device units (pixels, stick rate) into degrees/units per frame so consumers stay
    /// device-agnostic. Runs early so every consumer sees the same frame snapshot.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class PoolInputReader : MonoBehaviour, IShotInput
    {
        [SerializeField] private InputActionAsset actions;

        [Header("Aim")]
        [SerializeField, Tooltip("Degrees per mouse/touch pixel.")] private float pointerAimSensitivity = 0.08f;
        [SerializeField, Tooltip("Degrees per second at full stick / key.")] private float stickAimSpeed = 45f;
        [SerializeField, Range(0.02f, 1f)] private float precisionScale = 0.12f;

        [Header("Camera")]
        [SerializeField] private float pointerLookSensitivity = 0.25f;
        [SerializeField] private float stickLookSpeed = 120f;
        [SerializeField, Tooltip("Zoom units per wheel notch.")] private float wheelZoomStep = 0.12f;
        [SerializeField] private float buttonZoomSpeed = 2f;

        [Header("Spin")]
        [SerializeField, Tooltip("Unit-disc units per second.")] private float spinSpeed = 1.2f;
        [SerializeField, Tooltip("Cue elevation degrees per second (raise / lower keys).")] private float elevationSpeed = 35f;

        [Header("Touch")]
        [SerializeField, Tooltip("Degrees per touch point (1/160 inch) of horizontal drag.")] private float touchAimSensitivity = 0.12f;
        [SerializeField, Tooltip("Zoom units per touch point of pinch.")] private float pinchZoomSensitivity = 0.004f;
        [SerializeField, Tooltip("Camera degrees per touch point of two-finger drag.")] private float twoFingerLookSensitivity = 0.3f;

        private InputAction aim;
        private InputAction look;
        private InputAction zoom;
        private InputAction setPower;
        private InputAction shoot;
        private InputAction spin;
        private InputAction cameraTop;
        private InputAction cameraCue;
        private InputAction cameraTactical;
        private InputAction cancel;
        private InputAction pause;
        private InputAction precision;
        private InputAction resetSpin;
        private InputAction point;
        private InputAction placeCueBall;
        private InputAction ballInHand;
        private InputAction cueElevation;
        private Vector2 lastPointerPosition;
        private bool pointerInitialized;
        private bool pressStartedOverUI;
        private bool multiTouchGesture;
        private Vector2 lastTouchA;
        private Vector2 lastTouchB;
        private bool hadTwoTouches;

        public float AimDeltaDegrees { get; private set; }
        public Vector2 LookDeltaDegrees { get; private set; }
        public float ZoomDelta { get; private set; }
        public Vector2 SpinDelta { get; private set; }
        public float ElevationDelta { get; private set; }
        public bool ResetSpinPressed { get; private set; }
        public bool PrecisionHeld { get; private set; }
        public bool PowerPressed { get; private set; }
        public bool PowerHeld { get; private set; }
        public bool PowerReleased { get; private set; }
        public float StrokeDelta { get; private set; }
        public bool StrokeAvailable { get; private set; }
        public float AnalogPower { get; private set; } = -1f;
        public bool ShootPressed { get; private set; }
        public bool CancelPressed { get; private set; }
        public bool PausePressed { get; private set; }
        public bool CameraCuePressed { get; private set; }
        public bool CameraTacticalPressed { get; private set; }
        public bool CameraTopPressed { get; private set; }
        public Vector2 PointerPosition { get; private set; }
        public bool PointerMoved { get; private set; }
        public bool PlacePressed { get; private set; }
        public bool BallInHandPressed { get; private set; }
        public Vector2 MoveAxis { get; private set; }
        public bool PointerOverUI { get; private set; }
        public bool PointerPressed { get; private set; }
        public bool PointerHeld { get; private set; }
        public bool PointerReleased { get; private set; }

        /// <summary>A quick tap of a finger outside UI this frame (touch screens only), at <see cref="PointerPosition"/>.</summary>
        public bool TouchTapped { get; private set; }

        /// <summary>The most recent pointer press came from a touch screen.</summary>
        public bool LastInputWasTouch { get; private set; }

        /// <summary>Player setting: multiplier on pointer aim/look sensitivity.</summary>
        public float SensitivityScale { get; set; } = 1f;

        /// <summary>Set by UI or tools to suppress gameplay input (e.g. while a menu is open).</summary>
        public bool Suppressed { get; set; }

        public void SetActions(InputActionAsset asset)
        {
            actions = asset;
        }

        private void Awake()
        {
            if (actions == null)
            {
                Debug.LogError("[PoolInputReader] No InputActionAsset assigned.", this);
                enabled = false;
                return;
            }

            InputActionMap map = actions.FindActionMap("Gameplay", true);
            aim = map.FindAction("Aim", true);
            look = map.FindAction("Look", true);
            zoom = map.FindAction("Zoom", true);
            setPower = map.FindAction("SetPower", true);
            shoot = map.FindAction("Shoot", true);
            spin = map.FindAction("Spin", true);
            cameraTop = map.FindAction("CameraTop", true);
            cameraCue = map.FindAction("CameraCue", true);
            cameraTactical = map.FindAction("CameraTactical", true);
            cancel = map.FindAction("Cancel", true);
            pause = map.FindAction("Pause", true);
            precision = map.FindAction("Precision", true);
            resetSpin = map.FindAction("ResetSpin", true);
            point = map.FindAction("Point", true);
            placeCueBall = map.FindAction("PlaceCueBall", true);
            ballInHand = map.FindAction("BallInHand", true);
            cueElevation = map.FindAction("CueElevation", false);
        }

        private void OnEnable()
        {
            if (actions != null)
            {
                actions.FindActionMap("Gameplay", true).Enable();
            }
        }

        private void OnDisable()
        {
            if (actions != null)
            {
                actions.FindActionMap("Gameplay", true).Disable();
            }
        }

        private void Update()
        {
            if (aim == null)
            {
                return;
            }

            if (Suppressed)
            {
                // Menus still need Pause/Cancel to close themselves.
                ClearFrame();
                PausePressed = pause.WasPressedThisFrame();
                CancelPressed = cancel.WasPressedThisFrame();
                return;
            }

            float dt = Time.unscaledDeltaTime;
            EventSystem eventSystem = EventSystem.current;
            PointerOverUI = eventSystem != null && eventSystem.IsPointerOverGameObject();
            UpdatePointerPress();
            // A drag that started on a widget (spin pad, power slider) never turns into aiming/looking.
            bool pointerBlocked = PointerOverUI || (pressStartedOverUI && Pointer.current != null && Pointer.current.press.isPressed);
            PrecisionHeld = precision.IsPressed();
            float precisionFactor = PrecisionHeld ? precisionScale : 1f;

            // Aim (x axis only). Pointer deltas are per frame; sticks/keys are rates.
            Vector2 aimValue = aim.ReadValue<Vector2>();
            bool aimFromPointer = IsPointer(aim);
            float aimDegrees;
            if (!aimFromPointer)
            {
                aimDegrees = aimValue.x * stickAimSpeed * dt;
            }
            else if (pointerBlocked || multiTouchGesture)
            {
                aimDegrees = 0f;
            }
            else if (aim.activeControl.device is Touchscreen)
            {
                aimDegrees = aimValue.x * TouchPointsPerPixel() * touchAimSensitivity * SensitivityScale;
            }
            else
            {
                aimDegrees = aimValue.x * pointerAimSensitivity * SensitivityScale;
            }

            AimDeltaDegrees = aimDegrees * precisionFactor;

            Vector2 lookValue = look.ReadValue<Vector2>();
            LookDeltaDegrees = IsPointer(look) ? (pointerBlocked ? Vector2.zero : lookValue * pointerLookSensitivity * SensitivityScale) : lookValue * (stickLookSpeed * dt);

            float zoomValue = zoom.ReadValue<float>();
            // Wheel units differ per platform/version (1 or 120 per notch): use one step per event.
            ZoomDelta = IsPointer(zoom) ? (PointerOverUI ? 0f : 1f) * Mathf.Sign(zoomValue) * (Mathf.Abs(zoomValue) > 0.001f ? wheelZoomStep : 0f) : zoomValue * buttonZoomSpeed * dt;
            UpdateTwoFingerGesture();

            Vector2 spinValue = spin.ReadValue<Vector2>();
            SpinDelta = spinValue * (spinSpeed * dt) * (PrecisionHeld ? 0.35f : 1f);
            ElevationDelta = cueElevation != null ? cueElevation.ReadValue<float>() * elevationSpeed * dt * (PrecisionHeld ? 0.3f : 1f) : 0f;
            MoveAxis = Vector2.ClampMagnitude(spinValue + (aimFromPointer ? Vector2.zero : aimValue), 1f);

            Vector2 pointer = point.ReadValue<Vector2>();
            PointerPosition = pointer;
            bool insideView = pointer.x >= 0f && pointer.y >= 0f && pointer.x <= Screen.width && pointer.y <= Screen.height;
            // The first sample only establishes the baseline (otherwise frame 1 reads as a jump from (0,0)).
            PointerMoved = pointerInitialized && insideView && !PointerOverUI && (pointer - lastPointerPosition).sqrMagnitude > 0.25f;
            lastPointerPosition = pointer;
            pointerInitialized = true;
            PlacePressed = placeCueBall.WasPressedThisFrame() && !(PointerOverUI && IsPointer(placeCueBall));
            BallInHandPressed = ballInHand.WasPressedThisFrame();
            ResetSpinPressed = resetSpin.WasPressedThisFrame();

            PowerPressed = setPower.WasPressedThisFrame();
            PowerHeld = setPower.IsPressed();
            PowerReleased = setPower.WasReleasedThisFrame();
            Mouse mouse = Mouse.current;
            StrokeAvailable = PowerHeld && mouse != null && setPower.activeControl != null && setPower.activeControl.device is Keyboard;
            StrokeDelta = StrokeAvailable && Screen.height > 0 ? mouse.delta.ReadValue().y / Screen.height * SensitivityScale : 0f;
            AnalogPower = setPower.activeControl is UnityEngine.InputSystem.Controls.AxisControl axis && !(setPower.activeControl.device is Keyboard)
                ? axis.ReadValue()
                : -1f;

            ShootPressed = shoot.WasPressedThisFrame();
            CancelPressed = cancel.WasPressedThisFrame();
            PausePressed = pause.WasPressedThisFrame();
            CameraCuePressed = cameraCue.WasPressedThisFrame();
            CameraTacticalPressed = cameraTactical.WasPressedThisFrame();
            CameraTopPressed = cameraTop.WasPressedThisFrame();
        }

        private void UpdatePointerPress()
        {
            Pointer pointer = Pointer.current;
            bool pressed = pointer != null && pointer.press.wasPressedThisFrame;
            bool released = pointer != null && pointer.press.wasReleasedThisFrame;
            if (pressed)
            {
                pressStartedOverUI = PointerOverUI;
                LastInputWasTouch = pointer is Touchscreen;
            }

            PointerPressed = pressed && !pressStartedOverUI;
            PointerHeld = pointer != null && pointer.press.isPressed && !pressStartedOverUI;
            PointerReleased = released && !pressStartedOverUI;

            Touchscreen touchscreen = Touchscreen.current;
            TouchTapped = touchscreen != null && touchscreen.primaryTouch.tap.wasPressedThisFrame && !PointerOverUI && !pressStartedOverUI && !multiTouchGesture;
        }

        /// <summary>Two fingers: pinch zooms, dragging both orbits the camera. Aiming stays locked until all fingers lift.</summary>
        private void UpdateTwoFingerGesture()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                return;
            }

            int count = 0;
            Vector2 a = default;
            Vector2 b = default;
            var touches = touchscreen.touches;
            for (int i = 0; i < touches.Count && count < 2; i++)
            {
                if (touches[i].press.isPressed)
                {
                    if (count == 0) a = touches[i].position.ReadValue();
                    else b = touches[i].position.ReadValue();
                    count++;
                }
            }

            if (count >= 2)
            {
                if (hadTwoTouches && !PointerOverUI)
                {
                    TouchGestures.TwoFinger(lastTouchA, lastTouchB, a, b, out float spread, out Vector2 pan);
                    float points = TouchPointsPerPixel();
                    ZoomDelta += spread * points * pinchZoomSensitivity;
                    LookDeltaDegrees += pan * (points * twoFingerLookSensitivity);
                }

                lastTouchA = a;
                lastTouchB = b;
                hadTwoTouches = true;
                multiTouchGesture = true;
                AimDeltaDegrees = 0f;
                return;
            }

            hadTwoTouches = false;
            if (count == 0)
            {
                multiTouchGesture = false;
            }
        }

        /// <summary>Converts screen pixels to density-independent touch points (1/160 inch).</summary>
        private static float TouchPointsPerPixel()
        {
            float dpi = Screen.dpi;
            return dpi > 1f ? 160f / dpi : 1f;
        }

        private void ClearFrame()
        {
            AimDeltaDegrees = 0f;
            LookDeltaDegrees = Vector2.zero;
            ZoomDelta = 0f;
            SpinDelta = Vector2.zero;
            ElevationDelta = 0f;
            ResetSpinPressed = false;
            PrecisionHeld = false;
            PowerPressed = false;
            PowerHeld = false;
            PowerReleased = false;
            StrokeDelta = 0f;
            StrokeAvailable = false;
            AnalogPower = -1f;
            ShootPressed = false;
            CancelPressed = false;
            PausePressed = false;
            CameraCuePressed = false;
            CameraTacticalPressed = false;
            CameraTopPressed = false;
            PointerMoved = false;
            PlacePressed = false;
            BallInHandPressed = false;
            MoveAxis = Vector2.zero;
            PointerPressed = false;
            PointerHeld = false;
            PointerReleased = false;
            TouchTapped = false;
        }

        private static bool IsPointer(InputAction action)
        {
            InputControl control = action.activeControl;
            if (control == null)
            {
                return false;
            }

            InputDevice device = control.device;
            return device is Pointer;
        }
    }
}
