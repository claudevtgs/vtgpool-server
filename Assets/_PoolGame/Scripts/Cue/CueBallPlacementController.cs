using System;
using UnityEngine;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Inputs;
using VTG.Pool.Rules;
using VTG.Pool.Table;

namespace VTG.Pool.Cue
{
    /// <summary>
    /// Ball in hand: lets the player position the cue ball (pointer or stick) within the allowed area,
    /// then confirm. Placement can be resumed (BallInHand action) until the shot is taken.
    /// Validity: on the cloth, not over a pocket, not overlapping a ball, behind the head string if required.
    /// </summary>
    public sealed class CueBallPlacementController : MonoBehaviour
    {
        [SerializeField] private ShotController shotController;
        [SerializeField] private TableBuilder table;
        [SerializeField] private MonoBehaviour inputSource;
        [SerializeField] private Camera viewCamera;
        [SerializeField, Tooltip("Stick/key placement speed (m/s).")] private float moveSpeed = 0.6f;

        private IShotInput input;
        private Vector3 target;
        private bool startWhenAiming;
        private Vector2 previousPointer;
        private bool hasPreviousPointer;

        /// <summary>
        /// Player setting: how far the ball moves per pointer movement (1 = the ball follows the pointer on the cloth).
        /// The ball is moved by the pointer's motion, not snapped under it, so low values give fine placement.
        /// </summary>
        public float PointerSensitivity { get; set; } = 1f;

        /// <summary>Ball-in-hand rights for the current turn (None when not granted).</summary>
        public BallInHandMode Mode { get; private set; }

        public bool IsPlacing { get; private set; }

        public event Action PlacementChanged;

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

            if (viewCamera == null)
            {
                viewCamera = Camera.main;
            }
        }

        private void OnEnable()
        {
            PoolEvents.ShotStarted += HandleShotStarted;
        }

        private void OnDisable()
        {
            PoolEvents.ShotStarted -= HandleShotStarted;
        }

        /// <summary>Grants ball in hand for the coming turn; placement starts when the controller is back in Aiming.</summary>
        public void Grant(BallInHandMode mode)
        {
            if (mode == BallInHandMode.None)
            {
                Clear();
                return;
            }

            Mode = mode;
            startWhenAiming = true;
            shotController.ShotInputBlocked = true;
            PlacementChanged?.Invoke();
        }

        /// <summary>Removes ball-in-hand rights (placing the ball where it is if valid).</summary>
        public void Clear()
        {
            if (IsPlacing)
            {
                PoolBall cue = shotController.CueBall;
                cue.PlaceAt(IsValid(target) ? target : FindValidSpot());
                IsPlacing = false;
            }

            Mode = BallInHandMode.None;
            startWhenAiming = false;
            shotController.ShotInputBlocked = false;
            PlacementChanged?.Invoke();
        }

        public void BeginPlacement()
        {
            if (Mode == BallInHandMode.None || IsPlacing)
            {
                return;
            }

            PoolBall cue = shotController.CueBall;
            Vector3 start = cue.IsPocketed || !IsValid(cue.Position) ? FindValidSpot() : cue.Position;
            target = start;
            cue.Hold(start);
            IsPlacing = true;
            startWhenAiming = false;
            shotController.ShotInputBlocked = true;
            PlacementChanged?.Invoke();
        }

        /// <summary>Drops the ball at the current target if it is a legal position.</summary>
        public bool TryConfirm()
        {
            if (!IsPlacing || !IsValid(target))
            {
                return false;
            }

            shotController.CueBall.PlaceAt(target);
            IsPlacing = false;
            shotController.ShotInputBlocked = false;
            PlacementChanged?.Invoke();
            return true;
        }

        /// <summary>Moves the held ball toward a requested position (used by input, UI and tests).</summary>
        public bool TryMoveTo(Vector3 position)
        {
            if (!IsPlacing)
            {
                return false;
            }

            position = ClampToArea(position);
            if (!IsValid(position))
            {
                return false;
            }

            target = position;
            shotController.CueBall.MoveHeld(position);
            return true;
        }

        private void Update()
        {
            if (Mode == BallInHandMode.None || shotController.Phase != ShotPhase.Aiming)
            {
                return;
            }

            if (startWhenAiming)
            {
                BeginPlacement();
            }

            if (input == null || shotController.InputOwnedElsewhere || Time.timeScale <= 0f)
            {
                return;
            }

            if (!IsPlacing)
            {
                if (input.BallInHandPressed)
                {
                    BeginPlacement();
                }

                return;
            }

            Vector2 pointer = input.PointerPosition;
            if (input.PointerMoved && !input.PointerPressed && hasPreviousPointer && viewCamera != null
                && TryGetPointerOnTable(previousPointer, out Vector3 from) && TryGetPointerOnTable(pointer, out Vector3 to))
            {
                float scale = PointerSensitivity * (input.PrecisionHeld ? 0.3f : 1f);
                Vector3 delta = (to - from) * scale;
                delta.y = 0f;
                if (!TryMoveTo(target + delta))
                {
                    TryMoveTo(target + new Vector3(delta.x, 0f, 0f));
                    TryMoveTo(target + new Vector3(0f, 0f, delta.z));
                }
            }

            previousPointer = pointer;
            hasPreviousPointer = true;

            Vector2 move = input.MoveAxis;
            if (move.sqrMagnitude > 0.0001f && viewCamera != null)
            {
                Vector3 forward = viewCamera.transform.forward;
                forward.y = 0f;
                forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                Vector3 delta = (right * move.x + forward * move.y) * (moveSpeed * Time.deltaTime);
                if (!TryMoveTo(target + delta))
                {
                    // Slide along an obstacle: try each axis separately.
                    TryMoveTo(target + right * Vector3.Dot(delta, right));
                    TryMoveTo(target + forward * Vector3.Dot(delta, forward));
                }
            }

            if (input.PlacePressed)
            {
                TryConfirm();
            }
        }

        private void HandleShotStarted(Match.ShotRecord record)
        {
            Mode = BallInHandMode.None;
            IsPlacing = false;
            startWhenAiming = false;
            PlacementChanged?.Invoke();
        }

        private bool TryGetPointerOnTable(Vector2 screenPoint, out Vector3 point)
        {
            float radius = shotController.CueBall.Radius;
            Ray ray = viewCamera.ScreenPointToRay(screenPoint);
            var plane = new Plane(Vector3.up, new Vector3(0f, table.SurfaceHeight + radius, 0f));
            if (plane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = default;
            return false;
        }

        /// <summary>True if the cue ball may be placed at this centre position.</summary>
        public bool IsValid(Vector3 position)
        {
            PoolBall cue = shotController.CueBall;
            float radius = cue.Radius;
            Vector3 local = table.transform.InverseTransformPoint(position);
            TableGeometry geometry = table.Geometry;
            const float margin = 0.001f;
            if (Mathf.Abs(local.x) > geometry.HalfWidth - radius - margin || Mathf.Abs(local.z) > geometry.HalfLength - radius - margin)
            {
                return false;
            }

            if (Mode == BallInHandMode.BehindHeadString && local.z > -geometry.playLength * 0.25f)
            {
                return false;
            }

            var pockets = table.PocketManager.Pockets;
            for (int i = 0; i < pockets.Count; i++)
            {
                if (pockets[i].IsOverHole(position))
                {
                    return false;
                }
            }

            var balls = BallRegistry.Balls;
            float minDistance = 2f * radius + 0.0005f;
            for (int i = 0; i < balls.Count; i++)
            {
                PoolBall other = balls[i];
                if (other == cue || other.IsPocketed)
                {
                    continue;
                }

                Vector3 delta = other.Position - position;
                delta.y = 0f;
                if (delta.sqrMagnitude < minDistance * minDistance)
                {
                    return false;
                }
            }

            return true;
        }

        private Vector3 ClampToArea(Vector3 position)
        {
            float radius = shotController.CueBall.Radius;
            TableGeometry geometry = table.Geometry;
            Vector3 local = table.transform.InverseTransformPoint(position);
            float maxX = geometry.HalfWidth - radius - 0.0015f;
            float maxZ = geometry.HalfLength - radius - 0.0015f;
            float limitZ = Mode == BallInHandMode.BehindHeadString ? -geometry.playLength * 0.25f : maxZ;
            local.x = Mathf.Clamp(local.x, -maxX, maxX);
            local.z = Mathf.Clamp(local.z, -maxZ, limitZ);
            local.y = geometry.surfaceHeight + radius;
            return table.transform.TransformPoint(local);
        }

        /// <summary>Head spot, or the nearest free position to it.</summary>
        private Vector3 FindValidSpot()
        {
            float radius = shotController.CueBall.Radius;
            Vector3 spot = table.HeadSpot + Vector3.up * radius;
            for (int ring = 0; ring < 40; ring++)
            {
                for (int k = 0; k < 12; k++)
                {
                    float angle = k * Mathf.PI / 6f;
                    Vector3 candidate = ClampToArea(spot + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (ring * radius));
                    if (IsValid(candidate))
                    {
                        return candidate;
                    }

                    if (ring == 0)
                    {
                        break;
                    }
                }
            }

            return spot;
        }
    }
}
