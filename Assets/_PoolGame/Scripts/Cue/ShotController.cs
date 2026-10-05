using System;
using UnityEngine;
using VTG.Pool.Aiming;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Inputs;
using VTG.Pool.Match;
using VTG.Pool.Simulation;
using VTG.Pool.Table;

namespace VTG.Pool.Cue
{
    /// <summary>
    /// Shot flow state machine: AIM -> SPIN -> POWER (hold) -> FINE AIM -> SHOOT -> BALLS MOVING ->
    /// WAIT FOR REST -> RESOLVE -> AIM. Input is locked while any ball moves.
    /// Turn/rule evaluation subscribes to PoolEvents.BallsStopped (TurnManager); no rule logic lives here.
    /// </summary>
    public sealed class ShotController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PoolPhysicsSystem physicsSystem;
        [SerializeField] private AimSystem aimSystem;
        [SerializeField] private CueBallSpinController spinController;
        [SerializeField] private CueController cueController;
        [SerializeField] private CueStrikeProfile strikeProfile;
        [SerializeField] private TableBuilder table;
        [SerializeField] private PoolBall cueBall;
        [SerializeField] private MonoBehaviour inputSource;

        [Header("Flow")]
        [SerializeField, Tooltip("Seconds of holding to go from 0 to full power.")]
        private float powerChargeTime = 1.6f;

        [SerializeField] private float minShotPower = 0.02f;
        [SerializeField, Tooltip("Minimum time balls are considered moving after a strike.")]
        private float minMovingTime = 0.2f;

        [SerializeField, Tooltip("Safety: force-stop balls after this many seconds.")]
        private float maxShotDuration = 60f;

        [SerializeField] private bool animateCue = true;

        [Header("Mouse stroke")]
        [SerializeField, Tooltip("Cue travel per screen height of mouse movement (m).")]
        private float strokeMetersPerScreen = 0.45f;
        [SerializeField, Tooltip("Longest back swing (m).")]
        private float strokeMaxDrawBack = 0.28f;
        [SerializeField, Tooltip("Forward mouse speed for full power (screen heights per second).")]
        private float strokeFullSpeed = 2.4f;
        [SerializeField, Tooltip("Back swing needed before a forward push counts as a stroke (m).")]
        private float strokeArmDistance = 0.025f;

        [SerializeField, Tooltip("Practice/physics test: put a pocketed cue ball back on the head spot. Matches use ball-in-hand instead.")]
        private bool autoRespawnCueBall = true;

        private IShotInput input;
        private ShotParameters pendingShot;
        private ShotRecord currentRecord;
        private Action strikeCallback;
        private float phaseTime;
        private int shotIndex;

        public ShotPhase Phase { get; private set; } = ShotPhase.Preparing;

        /// <summary>Power being charged during PowerSelection (0..1).</summary>
        public float CurrentPower { get; private set; }

        /// <summary>Power of the previous shot; used by the Shoot action.</summary>
        public float LastPower { get; private set; } = 0.4f;

        public ShotRecord LastShot { get; private set; }

        public PoolBall CueBall => cueBall;

        public CueStrikeProfile StrikeProfile => strikeProfile;
        public TableBuilder Table => table;
        public bool ComputerOwnsInput { get; set; }
        private bool placementInputBlocked;
        private bool externalCharge;
        private bool mouseStroke;
        private bool strokeArmed;
        private float strokeDraw;
        private float strokeSpeed;

        /// <summary>Player setting: while the power key is held, the mouse moves the cue (pull back, push through).</summary>
        public bool MouseStrokeEnabled { get; set; } = true;

        /// <summary>A mouse stroke is in progress.</summary>
        public bool Stroking => mouseStroke && Phase == ShotPhase.PowerSelection;

        /// <summary>Current back swing of a mouse stroke (m).</summary>
        public float StrokeDrawBack => strokeDraw;

        public event Action<ShotPhase> PhaseChanged;

        public bool CanShoot => Phase == ShotPhase.Aiming || Phase == ShotPhase.PowerSelection;

        /// <summary>Blocks player shot input (aim/spin/power) while another system owns input, e.g. ball-in-hand placement.</summary>
        public bool ShotInputBlocked { get => placementInputBlocked || ComputerOwnsInput || LayoutEditing || NetworkLocked || PresentationLocked; set => placementInputBlocked = value; }

        /// <summary>A replay or other presentation is on screen: no shooting until it ends.</summary>
        public bool PresentationLocked { get; set; }

        /// <summary>Online: the other player owns the table (their turn, or waiting for the host's result).</summary>
        public bool NetworkLocked { get; set; }

        /// <summary>Online: show the opponent's cue while they aim (their aim arrives over the network).</summary>
        public bool ShowRemoteCue { get; set; }

        /// <summary>Someone other than the local player drives the shot (AI or network).</summary>
        public bool InputOwnedElsewhere => ComputerOwnsInput || NetworkLocked;

        /// <summary>Practice layout editor owns the pointer (balls are being arranged); no shooting.</summary>
        public bool LayoutEditing { get; set; }

        /// <summary>True while power is driven by an on-screen control (touch slider) instead of hold-to-charge.</summary>
        public bool ExternalCharge => externalCharge;

        public bool AutoRespawnCueBall
        {
            get => autoRespawnCueBall;
            set => autoRespawnCueBall = value;
        }

        public void SetInput(IShotInput shotInput)
        {
            input = shotInput;
        }

        private void Awake()
        {
            if (input == null && inputSource is IShotInput shotInput)
            {
                input = shotInput;
            }

            strikeCallback = OnCueContact;
        }

        private void Start()
        {
            if (cueBall == null)
            {
                cueBall = BallRegistry.FindCueBall();
            }

            if (aimSystem != null)
            {
                aimSystem.CueBall = cueBall;
            }

            SetPhase(ShotPhase.Aiming);
        }

        private void Update()
        {
            phaseTime += Time.deltaTime;
            switch (Phase)
            {
                case ShotPhase.Aiming:
                    UpdateAiming();
                    break;
                case ShotPhase.PowerSelection:
                    UpdatePowerSelection();
                    break;
                case ShotPhase.BallsMoving:
                    UpdateBallsMoving();
                    break;
            }
        }

        private void LateUpdate()
        {
            bool aiming = CanShoot && !ShotInputBlocked;
            if (aimSystem != null)
            {
                aimSystem.InputEnabled = aiming;
                aimSystem.GuidesVisible = aiming;
            }

            if (spinController != null)
            {
                spinController.InputEnabled = aiming;
            }

            UpdateCurvedPathPreview(aiming);
            if (cueController == null || cueBall == null)
            {
                return;
            }

            if ((aiming || (ShowRemoteCue && CanShoot && !placementInputBlocked)) && !cueBall.IsPocketed)
            {
                cueController.Visible = true;
                float drawBack = Phase == ShotPhase.PowerSelection ? CurrentPower : 0f;
                cueController.Align(cueBall.transform.position, cueBall.Radius, aimSystem.AimDirection, TipOffsetFraction(), drawBack,
                    spinController != null ? spinController.Elevation : 0f);
            }
            else if (!cueController.IsStroking && Phase != ShotPhase.Shooting && (Phase != ShotPhase.BallsMoving || phaseTime > 0.5f))
            {
                cueController.Visible = false;
            }
        }

        private void UpdateAiming()
        {
            if (input == null || ShotInputBlocked)
            {
                return;
            }

            if (input.PowerPressed)
            {
                CurrentPower = 0f;
                mouseStroke = MouseStrokeEnabled && input.StrokeAvailable;
                strokeArmed = false;
                strokeDraw = 0f;
                strokeSpeed = 0f;
                SetPhase(ShotPhase.PowerSelection);
            }
            else if (input.ShootPressed)
            {
                BeginShot(LastPower);
            }
        }

        private void UpdatePowerSelection()
        {
            if (ShotInputBlocked || Time.timeScale <= 0f)
            {
                CurrentPower = 0f;
                externalCharge = false;
                SetPhase(ShotPhase.Aiming);
                return;
            }

            if (externalCharge)
            {
                // Power comes from SetExternalCharge; the control releases or cancels the shot.
                return;
            }
            if (input == null)
            {
                return;
            }

            if (input.CancelPressed)
            {
                CurrentPower = 0f;
                mouseStroke = false;
                SetPhase(ShotPhase.Aiming);
                return;
            }

            if (mouseStroke)
            {
                UpdateMouseStroke();
                return;
            }

            CurrentPower = Mathf.Clamp01(CurrentPower + Time.deltaTime / Mathf.Max(0.1f, powerChargeTime));
            if (input.PowerReleased || !input.PowerHeld)
            {
                if (CurrentPower >= minShotPower)
                {
                    BeginShot(CurrentPower);
                }
                else
                {
                    SetPhase(ShotPhase.Aiming);
                }
            }
        }

        private void UpdateBallsMoving()
        {
            if (phaseTime < minMovingTime)
            {
                return;
            }

            if (physicsSystem.AllBallsAtRest)
            {
                ResolveShot();
            }
            else if (phaseTime > maxShotDuration)
            {
                Debug.LogWarning($"[ShotController] Shot exceeded {maxShotDuration}s; forcing balls to rest.");
                ForceAllBallsToRest();
                ResolveShot();
            }
        }

        /// <summary>
        /// Mouse stroke: pulling the mouse back draws the cue back, pushing it forward drives the cue through the ball.
        /// The shot fires when the tip comes back to the ball while moving forward; its power comes from the hand speed
        /// at that moment (like a real stroke). Releasing the key before contact cancels.
        /// </summary>
        private void UpdateMouseStroke()
        {
            float dt = Mathf.Max(1e-4f, Time.unscaledDeltaTime);
            float delta = input.StrokeDelta;
            float previous = strokeDraw;
            strokeDraw = Mathf.Clamp(strokeDraw - delta * strokeMetersPerScreen, 0f, strokeMaxDrawBack);
            if (strokeDraw >= strokeArmDistance)
            {
                strokeArmed = true;
            }

            // Forward hand speed (screen heights / s), smoothed over a few frames; pulling back resets it.
            float instant = delta / dt;
            strokeSpeed = instant > 0f ? Mathf.Lerp(strokeSpeed, instant, 0.6f) : 0f;
            CurrentPower = strokeDraw / strokeMaxDrawBack;

            if (strokeArmed && strokeDraw <= 0f && previous > 0f && strokeSpeed > 0f)
            {
                float normalized = Mathf.Clamp01(strokeSpeed / strokeFullSpeed);
                float power = strikeProfile != null ? strikeProfile.PowerForCueSpeed(normalized * strikeProfile.EvaluateCueSpeed(1f)) : normalized;
                mouseStroke = false;
                if (power >= minShotPower)
                {
                    BeginShot(power, false);
                }
                else
                {
                    CurrentPower = 0f;
                    SetPhase(ShotPhase.Aiming);
                }

                return;
            }

            if (input.PowerReleased || !input.PowerHeld)
            {
                mouseStroke = false;
                CurrentPower = 0f;
                SetPhase(ShotPhase.Aiming);
            }
        }

        /// <summary>Starts charging power from an on-screen control (touch power slider). False if a shot is not allowed now.</summary>
        public bool BeginExternalCharge()
        {
            if (Phase != ShotPhase.Aiming || ShotInputBlocked || Time.timeScale <= 0f || cueBall == null || cueBall.IsPocketed || cueBall.IsHeld)
            {
                return false;
            }

            externalCharge = true;
            CurrentPower = 0f;
            SetPhase(ShotPhase.PowerSelection);
            return true;
        }

        /// <summary>Sets the power (0..1) of an external charge; the cue draws back accordingly.</summary>
        public void SetExternalCharge(float power)
        {
            if (externalCharge && Phase == ShotPhase.PowerSelection)
            {
                CurrentPower = Mathf.Clamp01(power);
            }
        }

        /// <summary>Ends an external charge: shoots with the current power, or cancels (also when the power is too low).</summary>
        public bool ReleaseExternalCharge(bool shoot)
        {
            if (!externalCharge || Phase != ShotPhase.PowerSelection)
            {
                externalCharge = false;
                return false;
            }

            externalCharge = false;
            if (shoot && CurrentPower >= minShotPower)
            {
                BeginShot(CurrentPower);
                return Phase != ShotPhase.PowerSelection;
            }

            CurrentPower = 0f;
            SetPhase(ShotPhase.Aiming);
            return false;
        }

        /// <summary>Starts a shot with the current aim and spin.</summary>
        public void BeginShot(float power) => BeginShot(power, animateCue);

        /// <summary>Starts a shot; <paramref name="animate"/> = false strikes at once (the player stroked the cue).</summary>
        private void BeginShot(float power, bool animate)
        {
            if (!CanShoot || ShotInputBlocked || Time.timeScale <= 0f || cueBall == null || cueBall.IsPocketed || cueBall.IsHeld)
            {
                return;
            }

            pendingShot = new ShotParameters(aimSystem.AimDirection, power, spinController != null ? spinController.TipOffset : Vector2.zero,
                spinController != null ? spinController.Elevation : 0f);
            LastPower = pendingShot.Power;
            if (animate && cueController != null)
            {
                SetPhase(ShotPhase.Shooting);
                cueController.PlayStroke(pendingShot.Power, strikeCallback);
            }
            else
            {
                ExecuteStrike(pendingShot);
            }
        }

        /// <summary>
        /// Executes a fully specified shot immediately (no cue animation). Used by physics presets,
        /// tests, AI and replay. Returns false if a shot is not allowed now.
        /// </summary>
        public bool ExecuteShot(ShotParameters shot)
        {
            if (!CanShoot || Time.timeScale <= 0f || cueBall == null || cueBall.IsPocketed || cueBall.IsHeld || placementInputBlocked)
            {
                return false;
            }

            aimSystem.SetAimDirection(shot.AimDirection);
            if (spinController != null)
            {
                spinController.SetTipOffset(shot.TipOffset);
                spinController.SetElevation(shot.CueElevation);
            }

            LastPower = shot.Power;
            ExecuteStrike(shot);
            return true;
        }

        /// <summary>Returns the controller to Aiming (used after a table reset).</summary>
        public void ResetToAiming()
        {
            if (cueController != null)
            {
                cueController.CancelStroke();
            }

            currentRecord = null;
            externalCharge = false;
            if (cueBall == null)
            {
                cueBall = BallRegistry.FindCueBall();
                if (aimSystem != null)
                {
                    aimSystem.CueBall = cueBall;
                }
            }

            SetPhase(ShotPhase.Aiming);
        }

        private void OnCueContact()
        {
            ExecuteStrike(pendingShot);
        }

        private void ExecuteStrike(ShotParameters shot)
        {
            BallPhysicsProfile profile = cueBall.Profile;
            float cueSpeed = strikeProfile.EvaluateCueSpeed(shot.Power);
            StrikeResult strike = CueStrikeModel.Compute(shot, cueSpeed, profile.ballMass, profile.ballRadius, strikeProfile.ToSettings());

            currentRecord = new ShotRecord
            {
                ShotIndex = ++shotIndex,
                PlayerId = 0,
                CueBallPosition = cueBall.Position,
                AimDirection = shot.AimDirection,
                Power = shot.Power,
                CueOffset = shot.TipOffset,
                CueElevation = shot.CueElevation,
                InitialCueVelocity = strike.LinearVelocity,
                InitialAngularVelocity = strike.AngularVelocity,
                StartTime = Time.time
            };

            cueBall.ApplyStrike(strike.LinearVelocity, strike.AngularVelocity);
            PoolEvents.RaiseShotStarted(currentRecord);
            SetPhase(ShotPhase.BallsMoving);
        }

        private void ResolveShot()
        {
            SetPhase(ShotPhase.ResolvingShot);
            if (currentRecord != null)
            {
                currentRecord.Duration = Time.time - currentRecord.StartTime;
                LastShot = currentRecord;
                PoolEvents.RaiseBallsStopped(currentRecord);
                currentRecord = null;
            }

            if (autoRespawnCueBall && cueBall != null && cueBall.IsPocketed)
            {
                RespawnCueBall();
            }

            // A BallsStopped subscriber (TurnManager) may have ended the game.
            if (Phase == ShotPhase.ResolvingShot)
            {
                SetPhase(ShotPhase.Aiming);
            }
        }

        /// <summary>Stops all shot input until <see cref="ResetToAiming"/> (match over).</summary>
        public void EnterGameOver()
        {
            if (cueController != null)
            {
                cueController.Visible = false;
            }

            SetPhase(ShotPhase.GameOver);
        }

        /// <summary>Milestone 1 scratch handling: put the cue ball back on the head spot (rules come in Milestone 2).</summary>
        private void RespawnCueBall()
        {
            if (table == null)
            {
                return;
            }

            float radius = cueBall.Radius;
            int mask = (1 << PoolLayers.Ball) | (1 << PoolLayers.CueBall);
            Vector3 spot = table.HeadSpot + Vector3.up * radius;
            for (int i = 0; i < 20; i++)
            {
                float offset = (i % 2 == 0 ? 1f : -1f) * ((i + 1) / 2) * radius * 2.2f;
                Vector3 candidate = spot + Vector3.right * offset;
                if (!Physics.CheckSphere(candidate, radius * 1.02f, mask, QueryTriggerInteraction.Ignore))
                {
                    cueBall.PlaceAt(candidate);
                    return;
                }
            }

            cueBall.PlaceAt(spot);
        }

        private static void ForceAllBallsToRest()
        {
            var balls = BallRegistry.Balls;
            for (int i = 0; i < balls.Count; i++)
            {
                if (!balls[i].IsPocketed)
                {
                    balls[i].Body.linearVelocity = Vector3.zero;
                    balls[i].Body.angularVelocity = Vector3.zero;
                }
            }
        }

        [Header("Massé preview")]
        [SerializeField, Tooltip("Cue elevation (deg) from which the aim guide shows the predicted curved path.")]
        private float curvedPathMinElevation = 8f;

        private readonly System.Collections.Generic.List<Vector3> previewPoints = new System.Collections.Generic.List<Vector3>(128);
        private readonly System.Collections.Generic.List<Vector3> previewObstacles = new System.Collections.Generic.List<Vector3>(16);

        /// <summary>Elevated cue: predicts the curved path with the cloth model and hands it to the aim guides.</summary>
        private void UpdateCurvedPathPreview(bool aiming)
        {
            if (aimSystem == null)
            {
                return;
            }

            float elevation = spinController != null ? spinController.Elevation : 0f;
            if (!aiming || cueBall == null || cueBall.IsPocketed || table == null || strikeProfile == null || elevation < curvedPathMinElevation)
            {
                if (aimSystem.HasCurvedPath)
                {
                    aimSystem.ClearCurvedPath();
                }

                return;
            }

            float power = Phase == ShotPhase.PowerSelection ? Mathf.Max(CurrentPower, minShotPower) : LastPower;
            var shot = new ShotParameters(aimSystem.AimDirection, power, spinController.TipOffset, elevation);
            BallPhysicsProfile profile = cueBall.Profile;
            StrikeResult strike = CueStrikeModel.Compute(shot, strikeProfile.EvaluateCueSpeed(power), profile.ballMass, profile.ballRadius, strikeProfile.ToSettings());
            previewObstacles.Clear();
            var balls = BallRegistry.Balls;
            for (int i = 0; i < balls.Count; i++)
            {
                if (balls[i] != cueBall && !balls[i].IsPocketed && balls[i].isActiveAndEnabled)
                {
                    previewObstacles.Add(balls[i].Position);
                }
            }

            TableGeometry geometry = table.Geometry;
            var extents = new Vector2(geometry.HalfWidth - profile.ballRadius, geometry.HalfLength - profile.ballRadius);
            var flight = new FlightParameters
            {
                RestHeight = table.SurfaceHeight + profile.ballRadius,
                Gravity = -Physics.gravity.y,
                LandingRestitution = profile.landingRestitution,
                LandingThreshold = profile.landingBounceThreshold
            };
            CuePathPredictor.End end = CuePathPredictor.Predict(cueBall.Position, strike.LinearVelocity, strike.AngularVelocity,
                profile.ToClothParameters(-Physics.gravity.y), flight, extents, table.transform.worldToLocalMatrix, previewObstacles, previewPoints, out _);
            aimSystem.SetCurvedPath(previewPoints, end == CuePathPredictor.End.Ball);
        }

        private Vector2 TipOffsetFraction()
        {
            Vector2 offset = spinController != null ? spinController.TipOffset : Vector2.zero;
            return offset * (strikeProfile != null ? strikeProfile.maxTipOffsetFraction : 0.5f);
        }

        private void SetPhase(ShotPhase phase)
        {
            Phase = phase;
            phaseTime = 0f;
            PhaseChanged?.Invoke(phase);
        }
    }
}
