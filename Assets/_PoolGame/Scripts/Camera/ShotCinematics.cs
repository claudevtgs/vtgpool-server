using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Match;
using VTG.Pool.Table;

namespace VTG.Pool.CameraSystem
{
    /// <summary>
    /// Broadcast-style camera during shots (presentation only; physics is untouched apart from short slow-motion
    /// moments). Shots:
    /// - break: low side angle on the rack; on impact slow motion, a short shake and a punch-in, then a crane up over
    ///   the spread;
    /// - chase: low three-quarter angle behind the cue ball;
    /// - target: tracking dolly beside the first object ball hit, leaving lead room in front of it;
    /// - pocket: behind the pocket looking back at the ball, slow push-in, slow motion before the drop;
    /// - overview: high three-quarter "TV" angle over the table, slowly drifting, until everything stops;
    /// - victory: crane orbit.
    /// The look-at point and the camera position are damped separately (no rotation lag), a faint handheld drift
    /// keeps it alive, the frame never dips into the cloth or the rails, 2.39:1 letterbox bars slide in and a
    /// depth-of-field volume keeps the followed ball sharp. Any key or click skips. "Shot camera" setting turns it off.
    /// </summary>
    public sealed class ShotCinematics : MonoBehaviour
    {
        private enum Shot
        {
            None,
            Break,
            Spread,
            Chase,
            Target,
            Pocket,
            Overview,
            Victory
        }

        [SerializeField, Range(0.05f, 1f)] private float breakSlowMotion = 0.3f;
        [SerializeField] private float breakSlowMotionSeconds = 0.8f;
        [SerializeField, Range(0.05f, 1f)] private float pocketSlowMotion = 0.4f;
        [SerializeField] private float pocketSlowMotionSeconds = 0.7f;
        [SerializeField, Tooltip("Largest angle between an object ball's path and a pocket for the pocket cam (deg).")]
        private float pocketConeDegrees = 5f;
        [SerializeField, Tooltip("Handheld drift amplitude (degrees).")]
        private float handheld = 0.35f;
        [SerializeField, Tooltip("Letterbox: target aspect of the picture between the bars.")]
        private float letterboxAspect = 2.39f;

        private CameraController cameras;
        private ShotController shot;
        private TableBuilder table;
        private TurnManager turns;

        private Shot phase;
        private CameraMode previousMode;
        private PoolBall follow;
        private Pocket pocket;
        private bool pocketChecked;
        private float phaseTime;
        private float slowUntil;
        private float slowScale = 1f;
        private float baseScale = 1f;
        private bool slowedPocket;
        private Vector3 position;
        private Vector3 lookPoint;
        private Vector3 velocity;
        private Vector3 lookVelocity;
        private float fov = 45f;
        private bool poseInitialized;
        private bool cut;
        private bool moneySlowed;
        private float victoryAngle;
        private float trackSide = 1f;
        private float shake;
        private float punch;
        private float noiseSeed;

        // Presentation layers.
        private RectTransform barTop;
        private RectTransform barBottom;
        private float letterbox;
        private Volume dofVolume;
        private DepthOfField dof;
        private float focusDistance = 1f;

        /// <summary>Player setting.</summary>
        public static bool Enabled { get; set; } = true;

        public bool Active => phase != Shot.None;

        /// <summary>Letterbox coverage 0..1 (tests / diagnostics).</summary>
        public float Letterbox => letterbox;

        public void Configure(CameraController cameraController, ShotController shotController)
        {
            cameras = cameraController;
            shot = shotController;
        }

        private void Start()
        {
            if (shot == null) shot = FindAnyObjectByType<ShotController>();
            if (cameras == null) cameras = GetComponent<CameraController>();
            table = FindAnyObjectByType<TableBuilder>();
            turns = FindAnyObjectByType<TurnManager>();
            noiseSeed = Random.value * 100f;
            BuildLetterbox();
            BuildDepthOfField();
        }

        private void OnEnable()
        {
            PoolEvents.ShotStarted += HandleShotStarted;
            PoolEvents.BallHit += HandleBallHit;
            PoolEvents.BallPocketed += HandleBallPocketed;
            PoolEvents.GameWon += HandleGameWon;
        }

        private void OnDisable()
        {
            PoolEvents.ShotStarted -= HandleShotStarted;
            PoolEvents.BallHit -= HandleBallHit;
            PoolEvents.BallPocketed -= HandleBallPocketed;
            PoolEvents.GameWon -= HandleGameWon;
            End();
            letterbox = 0f;
            ApplyPresentation();
        }

        private void OnDestroy()
        {
            if (barTop != null) Destroy(barTop.root.gameObject);
            if (dofVolume != null)
            {
                Destroy(dofVolume.profile);
                Destroy(dofVolume.gameObject);
            }
        }

        // ------------------------------------------------------------ presentation layers

        private void BuildLetterbox()
        {
            // Scene root (not under the camera rig): a plain overlay canvas on the default layer.
            var canvasObject = new GameObject("CinematicLetterbox", typeof(RectTransform));
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // under the HUD (10) and the replay overlay
            barTop = Bar(canvasObject.transform, "Top", 1f);
            barBottom = Bar(canvasObject.transform, "Bottom", 0f);
            ApplyPresentation();
        }

        private static RectTransform Bar(Transform parent, string name, float edge)
        {
            var bar = new GameObject("Bar" + name, typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(parent, false);
            var rect = bar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, edge);
            rect.anchorMax = new Vector2(1f, edge);
            rect.pivot = new Vector2(0.5f, edge);
            rect.anchoredPosition = Vector2.zero;
            Image image = bar.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            return rect;
        }

        private void BuildDepthOfField()
        {
            var volumeObject = new GameObject("CinematicDepthOfField");
            volumeObject.transform.SetParent(transform, false);
            dofVolume = volumeObject.AddComponent<Volume>();
            dofVolume.isGlobal = true;
            dofVolume.priority = 50f;
            dofVolume.weight = 0f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            dof = profile.Add<DepthOfField>(true);
            // Bokeh on desktop; the cheaper Gaussian (background only) on phones and in browsers.
            bool light = Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer;
            dof.mode.Override(light ? DepthOfFieldMode.Gaussian : DepthOfFieldMode.Bokeh);
            dof.focalLength.Override(55f);
            dof.aperture.Override(3.2f);
            dof.gaussianMaxRadius.Override(1.2f);
            dof.highQualitySampling.Override(false);
            dofVolume.profile = profile;
        }

        private void ApplyPresentation()
        {
            if (barTop != null)
            {
                // Bars sized from the screen so the picture between them is letterboxAspect wide.
                Rect area = ((RectTransform)barTop.parent).rect;
                float screenAspect = area.height > 1f ? area.width / area.height : Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
                float cover = Mathf.Clamp01(1f - screenAspect / letterboxAspect) * 0.5f;
                float eased = letterbox * letterbox * (3f - 2f * letterbox);
                Vector2 size = new Vector2(0f, 0f);
                foreach (RectTransform bar in new[] { barTop, barBottom })
                {
                    bar.anchorMin = new Vector2(0f, bar == barTop ? 1f - cover * eased : 0f);
                    bar.anchorMax = new Vector2(1f, bar == barTop ? 1f : cover * eased);
                    bar.offsetMin = size;
                    bar.offsetMax = size;
                    bar.gameObject.SetActive(eased > 0.001f);
                }
            }

            if (dofVolume != null)
            {
                dofVolume.weight = letterbox;
                dofVolume.enabled = letterbox > 0.001f;
                dof.focusDistance.Override(focusDistance);
                dof.gaussianStart.Override(focusDistance + 0.35f);
                dof.gaussianEnd.Override(focusDistance + 2.2f);
            }
        }

        // ------------------------------------------------------------ events

        private void HandleGameWon(int winner, string message)
        {
            Replay.ShotReplay.RunAfterReplay(this, StartVictory);
        }

        private void StartVictory()
        {
            if (!Enabled || cameras == null || table == null || turns == null || turns.State == null || !turns.State.IsGameOver)
            {
                return;
            }

            if (!Active)
            {
                previousMode = cameras.ActiveMode == CameraMode.Cinematic ? CameraMode.Cue : cameras.ActiveMode;
                poseInitialized = false;
            }

            Vector3 eye = Camera.main != null ? Camera.main.transform.position - table.SurfaceCenter : -table.transform.forward;
            victoryAngle = Mathf.Atan2(eye.x, eye.z) * Mathf.Rad2Deg;
            SetPhase(Shot.Victory, false);
            cameras.CinematicOverride = ComputePose;
            cameras.SetMode(CameraMode.Cinematic);
        }

        /// <summary>The ball that decides the rack: the 8, the 9, or the last ball in practice (-1 if none).</summary>
        private int MoneyBall()
        {
            if (turns == null || turns.State == null)
            {
                return -1;
            }

            if (turns.Rules is Rules.Practice.PracticeRuleSet)
            {
                return turns.State.BallsOnTable.Count == 1 ? turns.State.BallsOnTable.First() : -1;
            }

            return turns.State.Mode == Rules.GameMode.NineBall ? 9 : 8;
        }

        /// <summary>The deciding ball rolling into a pocket: cut to the pocket and slow right down.</summary>
        private void WatchMoneyBall()
        {
            if (moneySlowed || phase == Shot.Break)
            {
                return;
            }

            int number = MoneyBall();
            PoolBall money = number > 0 ? BallRegistry.Balls.FirstOrDefault(b => b.BallId == number) : null;
            if (money == null || money.IsPocketed || Speed(money) < 0.25f)
            {
                return;
            }

            foreach (Pocket candidate in table.PocketManager.Pockets)
            {
                Vector3 to = Flat(candidate.Center - money.Position);
                if (to.magnitude < 0.32f && Vector3.Dot(to.normalized, Flat(money.LinearVelocity).normalized) > 0.8f)
                {
                    moneySlowed = true;
                    follow = money;
                    pocket = candidate;
                    SetPhase(Shot.Pocket, true);
                    slowedPocket = true;
                    SlowMotion(0.22f, 1.1f);
                    return;
                }
            }
        }

        private void HandleShotStarted(ShotRecord record)
        {
            if (!Enabled || cameras == null || shot == null || table == null || shot.LayoutEditing || Active)
            {
                return;
            }

            bool isBreak = turns != null && turns.State != null ? turns.State.IsBreakShot : record.IsBreakShot;
            previousMode = cameras.ActiveMode == CameraMode.Cinematic ? CameraMode.Cue : cameras.ActiveMode;
            follow = null;
            pocket = null;
            pocketChecked = false;
            slowedPocket = false;
            moneySlowed = false;
            poseInitialized = false;
            shake = 0f;
            punch = 0f;
            SetPhase(isBreak ? Shot.Break : Shot.Chase, true);
            cameras.CinematicOverride = ComputePose;
            cameras.SetMode(CameraMode.Cinematic);
        }

        private void HandleBallHit(BallHitInfo info)
        {
            if (!Active || shot.CueBall == null || !info.Involves(shot.CueBall) || follow != null)
            {
                return;
            }

            follow = info.Other(shot.CueBall);
            if (phase == Shot.Break)
            {
                SlowMotion(breakSlowMotion, breakSlowMotionSeconds);
                shake = Mathf.Clamp01((info.Impulse / 0.17f) / 6f) * 0.9f + 0.2f;
                punch = 1f;
            }
            else if (phase == Shot.Chase)
            {
                // Track from the side the camera is already on, so the move reads as one continuous dolly.
                Vector3 path = Direction(follow, Flat(follow.Position - shot.CueBall.Position));
                Vector3 lateral = Vector3.Cross(Vector3.up, path);
                trackSide = Vector3.Dot(Flat(position - follow.Position), lateral) >= 0f ? 1f : -1f;
                shake = Mathf.Clamp01((info.Impulse / 0.17f) / 8f) * 0.35f;
                SetPhase(Shot.Target, false);
            }
        }

        private void HandleBallPocketed(PoolBall ball, Pocket into)
        {
            if (!Active)
            {
                return;
            }

            if (ball == follow && (phase == Shot.Pocket || phase == Shot.Target))
            {
                shake = Mathf.Max(shake, 0.25f);
                phaseTime = Mathf.Max(phaseTime, 10f); // move on to the overview shortly
            }
        }

        private void SetPhase(Shot next, bool cutTo)
        {
            phase = next;
            phaseTime = 0f;
            cut = cutTo;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            letterbox = Mathf.MoveTowards(letterbox, Active ? 1f : 0f, dt / 0.35f);
            ApplyPresentation();
            if (!Active)
            {
                return;
            }

            phaseTime += dt;
            shake = Mathf.MoveTowards(shake, 0f, dt * 1.6f);
            punch = Mathf.MoveTowards(punch, 0f, dt * 1.4f);
            UpdateSlowMotion();

            bool skip = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                        || (Pointer.current != null && Pointer.current.press.wasPressedThisFrame);
            if (phase == Shot.Victory)
            {
                if ((skip && phaseTime > 0.3f) || phaseTime > 4.5f || shot.Phase == ShotPhase.Aiming)
                {
                    End();
                }

                return;
            }

            if ((skip && phaseTime > 0.15f) || shot.Phase == ShotPhase.Aiming || shot.Phase == ShotPhase.GameOver || shot.Phase == ShotPhase.Preparing)
            {
                End();
                return;
            }

            WatchMoneyBall();

            switch (phase)
            {
                case Shot.Break:
                    if (phaseTime > 1.5f) SetPhase(Shot.Spread, false);
                    break;
                case Shot.Spread:
                    if (phaseTime > 2.2f) SetPhase(Shot.Overview, false);
                    break;
                case Shot.Chase:
                    if (phaseTime > 1.2f && Speed(shot.CueBall) < 0.3f) SetPhase(Shot.Overview, false);
                    break;
                case Shot.Target:
                    if (!pocketChecked && follow != null && Speed(follow) > 0.15f)
                    {
                        pocketChecked = true;
                        pocket = PocketAhead(follow);
                        if (pocket != null) SetPhase(Shot.Pocket, true);
                    }

                    if (follow == null || follow.IsPocketed || phaseTime > 3f || (phaseTime > 0.4f && Speed(follow) < 0.2f)) SetPhase(Shot.Overview, false);
                    break;
                case Shot.Pocket:
                    if (!slowedPocket && follow != null && !follow.IsPocketed && Flat(pocket.Center - follow.Position).magnitude < 0.3f)
                    {
                        slowedPocket = true;
                        SlowMotion(pocketSlowMotion, pocketSlowMotionSeconds);
                    }

                    if (follow == null || phaseTime > 3.5f || (follow.IsPocketed && phaseTime > 0.8f) || (phaseTime > 0.5f && Speed(follow) < 0.1f)) SetPhase(Shot.Overview, false);
                    break;
            }
        }

        // ------------------------------------------------------------ framing

        private CameraPose ComputePose()
        {
            ComputeTarget(out Vector3 targetPosition, out Vector3 targetLook, out float targetFov, out float smooth);
            float dt = Time.unscaledDeltaTime;
            if (!poseInitialized || cut)
            {
                position = targetPosition;
                lookPoint = targetLook;
                fov = targetFov;
                velocity = Vector3.zero;
                lookVelocity = Vector3.zero;
                poseInitialized = true;
                cut = false;
            }
            else
            {
                // Operator feel: the head (look point) reacts faster than the dolly (position).
                position = Vector3.SmoothDamp(position, targetPosition, ref velocity, smooth, Mathf.Infinity, dt);
                lookPoint = Vector3.SmoothDamp(lookPoint, targetLook, ref lookVelocity, smooth * 0.45f, Mathf.Infinity, dt);
                fov = Mathf.Lerp(fov, targetFov, 1f - Mathf.Exp(-3f * dt));
            }

            position = KeepClear(position);
            Vector3 forward = lookPoint - position;
            if (forward.sqrMagnitude < 1e-6f)
            {
                forward = table.transform.forward;
            }

            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);

            // Handheld drift + impact shake (rotation only, so the framing stays put).
            float t = Time.unscaledTime;
            float drift = phase == Shot.Victory ? handheld * 0.5f : handheld;
            float jolt = shake * shake * 2.2f;
            float pitch = (Mathf.PerlinNoise(noiseSeed, t * 0.35f) - 0.5f) * 2f * drift + (Mathf.PerlinNoise(noiseSeed + 3f, t * 22f) - 0.5f) * 2f * jolt;
            float yaw = (Mathf.PerlinNoise(noiseSeed + 7f, t * 0.3f) - 0.5f) * 2f * drift + (Mathf.PerlinNoise(noiseSeed + 11f, t * 22f) - 0.5f) * 2f * jolt;
            float roll = (Mathf.PerlinNoise(noiseSeed + 13f, t * 0.25f) - 0.5f) * drift + (Mathf.PerlinNoise(noiseSeed + 17f, t * 18f) - 0.5f) * jolt;
            rotation *= Quaternion.Euler(pitch, yaw, roll);

            focusDistance = Mathf.Max(0.1f, Vector3.Distance(position, FocusTarget(targetLook)));
            return new CameraPose(position, rotation, fov * (1f - 0.12f * punch * punch));
        }

        /// <summary>What should be sharp: the followed ball, else the cue ball, else the look point.</summary>
        private Vector3 FocusTarget(Vector3 fallback)
        {
            if (phase == Shot.Overview || phase == Shot.Victory || phase == Shot.Spread)
            {
                return table.SurfaceCenter;
            }

            if (follow != null && !follow.IsPocketed) return follow.Position;
            if (phase == Shot.Pocket && pocket != null) return pocket.Center;
            if (shot.CueBall != null && !shot.CueBall.IsPocketed) return shot.CueBall.Position;
            return fallback;
        }

        /// <summary>Never inside the cloth or the rails: over the table footprint stay above the cushions.</summary>
        private Vector3 KeepClear(Vector3 point)
        {
            if (table == null || table.Geometry == null)
            {
                return point;
            }

            Vector3 local = table.transform.InverseTransformPoint(point);
            float surface = table.SurfaceHeight;
            bool overTable = Mathf.Abs(local.x) < table.Geometry.HalfWidth + 0.2f && Mathf.Abs(local.z) < table.Geometry.HalfLength + 0.2f;
            float floor = overTable ? surface + 0.075f : surface - 0.25f;
            if (point.y < floor)
            {
                point.y = floor;
            }

            return point;
        }

        private void ComputeTarget(out Vector3 cameraPosition, out Vector3 lookAt, out float fieldOfView, out float smooth)
        {
            Vector3 up = Vector3.up;
            Vector3 center = table.SurfaceCenter;
            PoolBall cue = shot.CueBall;
            Vector3 cuePosition = cue != null ? cue.Position : center;
            Vector3 along = table.transform.forward;
            Vector3 side = table.transform.right;
            switch (phase)
            {
                case Shot.Break:
                {
                    // Low, from the side of the rack, slight push-in while the cue ball travels.
                    Vector3 rack = table.FootSpot + up * 0.03f;
                    float push = Mathf.Clamp01(phaseTime / 1.5f);
                    cameraPosition = rack + side * Mathf.Lerp(0.95f, 0.8f, push) - along * 0.38f + up * 0.12f;
                    lookAt = rack - along * 0.1f;
                    fieldOfView = 34f;
                    smooth = 0.5f;
                    return;
                }
                case Shot.Spread:
                {
                    // Crane up and back over the rack end to watch the balls scatter.
                    float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phaseTime / 2f));
                    Vector3 rack = table.FootSpot;
                    cameraPosition = rack + side * Mathf.Lerp(0.8f, 0.55f, rise) + along * Mathf.Lerp(-0.38f, 0.55f, rise) + up * Mathf.Lerp(0.12f, 1.25f, rise);
                    lookAt = Vector3.Lerp(rack, center, rise * 0.7f);
                    fieldOfView = Mathf.Lerp(36f, 52f, rise);
                    smooth = 0.35f;
                    return;
                }
                case Shot.Chase:
                {
                    // Low three-quarter behind the cue ball, slightly off the line so the path reads.
                    Vector3 direction = Direction(cue, Flat(cuePosition - center) * -1f);
                    Vector3 lateral = Vector3.Cross(up, direction);
                    cameraPosition = cuePosition - direction * 0.6f + lateral * 0.12f + up * 0.13f;
                    lookAt = cuePosition + direction * 0.7f;
                    fieldOfView = 44f;
                    smooth = 0.14f;
                    return;
                }
                case Shot.Target:
                {
                    // Tracking dolly beside the ball, a little behind it, lead room in front.
                    Vector3 ball = follow != null ? follow.Position : cuePosition;
                    Vector3 direction = Direction(follow, Flat(ball - cuePosition));
                    Vector3 lateral = Vector3.Cross(up, direction) * trackSide;
                    cameraPosition = ball + lateral * 0.55f - direction * 0.3f + up * 0.17f;
                    lookAt = ball + direction * 0.28f;
                    fieldOfView = 42f;
                    smooth = 0.16f;
                    return;
                }
                case Shot.Pocket:
                {
                    // Behind the pocket, looking back up the ball's path; slow push-in.
                    Vector3 ball = follow != null && !follow.IsPocketed ? follow.Position : pocket.Center;
                    Vector3 toBall = Flat(ball - pocket.Center);
                    toBall = toBall.sqrMagnitude > 1e-6f ? toBall.normalized : -Flat(pocket.Center - center).normalized;
                    float push = Mathf.Clamp01(phaseTime / 2.5f);
                    cameraPosition = pocket.Center - toBall * Mathf.Lerp(0.36f, 0.28f, push) + up * 0.1f;
                    lookAt = Vector3.Lerp(ball, pocket.Center, 0.35f) + up * 0.01f;
                    fieldOfView = Mathf.Lerp(40f, 33f, push);
                    smooth = 0.25f;
                    return;
                }
                case Shot.Victory:
                {
                    // Slow crane-orbit around the table.
                    victoryAngle += 22f * Time.unscaledDeltaTime;
                    float radians = victoryAngle * Mathf.Deg2Rad;
                    float rise = Mathf.Clamp01(phaseTime / 4f);
                    cameraPosition = center + new Vector3(Mathf.Sin(radians) * 2.3f, Mathf.Lerp(0.55f, 1.5f, rise), Mathf.Cos(radians) * 2.3f);
                    lookAt = center + up * 0.05f;
                    fieldOfView = 44f;
                    smooth = 0.6f;
                    return;
                }
                default:
                {
                    // TV angle: high three-quarter from the long side the action is on, drifting slowly.
                    Vector3 local = table.transform.InverseTransformPoint(cuePosition);
                    float sideSign = local.x >= 0f ? 1f : -1f;
                    float drift = Mathf.Sin(phaseTime * 0.25f) * 0.25f;
                    Vector3 action = Vector3.Lerp(center, cuePosition, 0.3f);
                    cameraPosition = center + side * (1.55f * sideSign) + along * (Mathf.Clamp(local.z, -0.6f, 0.6f) * 0.5f + drift) + up * 1.55f;
                    lookAt = action;
                    fieldOfView = 50f;
                    smooth = 0.55f;
                    return;
                }
            }
        }

        /// <summary>Pocket the ball is heading straight for (within the cone), or null.</summary>
        private Pocket PocketAhead(PoolBall ball)
        {
            Vector3 heading = Flat(ball.LinearVelocity);
            if (heading.sqrMagnitude < 1e-4f)
            {
                return null;
            }

            heading.Normalize();
            Pocket best = null;
            float bestAngle = pocketConeDegrees;
            foreach (Pocket candidate in table.PocketManager.Pockets)
            {
                Vector3 to = Flat(candidate.Center - ball.Position);
                float angle = Vector3.Angle(heading, to);
                if (angle < bestAngle && to.magnitude < 2.6f)
                {
                    best = candidate;
                    bestAngle = angle;
                }
            }

            return best;
        }

        private void SlowMotion(float scale, float seconds)
        {
            if (Time.timeScale <= 0f)
            {
                return;
            }

            if (slowUntil <= 0f)
            {
                baseScale = Time.timeScale;
            }

            slowScale = scale;
            slowUntil = Time.unscaledTime + seconds;
            Time.timeScale = baseScale * scale;
        }

        private void UpdateSlowMotion()
        {
            if (slowUntil > 0f && Time.unscaledTime >= slowUntil)
            {
                RestoreTime();
            }
        }

        private void RestoreTime()
        {
            // Only undo our own change (a pause or another system may have set the scale meanwhile).
            if (slowUntil > 0f && Mathf.Approximately(Time.timeScale, baseScale * slowScale))
            {
                Time.timeScale = baseScale;
            }

            slowUntil = 0f;
        }

        /// <summary>Stops the cinematic and returns to the player's camera.</summary>
        public void End()
        {
            RestoreTime();
            if (phase == Shot.None)
            {
                return;
            }

            phase = Shot.None;
            follow = null;
            shake = 0f;
            punch = 0f;
            if (cameras != null)
            {
                cameras.CinematicOverride = null;
                if (cameras.ActiveMode == CameraMode.Cinematic)
                {
                    cameras.SetMode(previousMode);
                }
            }
        }

        private static float Speed(PoolBall ball) => ball != null && !ball.IsPocketed ? Flat(ball.LinearVelocity).magnitude : 0f;

        private static Vector3 Direction(PoolBall ball, Vector3 fallback)
        {
            Vector3 velocity = ball != null ? Flat(ball.LinearVelocity) : Vector3.zero;
            if (velocity.sqrMagnitude > 0.01f)
            {
                return velocity.normalized;
            }

            return fallback.sqrMagnitude > 1e-6f ? fallback.normalized : Vector3.forward;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
