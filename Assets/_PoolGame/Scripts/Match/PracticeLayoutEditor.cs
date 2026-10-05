using System;
using UnityEngine;
using VTG.Pool.Balls;
using VTG.Pool.CameraSystem;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Inputs;
using VTG.Pool.Table;

namespace VTG.Pool.Match
{
    /// <summary>
    /// Practice "arrange balls" mode: pick any ball with the pointer (mouse or finger), drag it to a free spot
    /// on the cloth, drop it into a pocket to take it off the table. Switches to the top camera while active.
    /// Shooting is blocked (<see cref="ShotController.LayoutEditing"/>) until editing ends.
    /// </summary>
    public sealed class PracticeLayoutEditor : MonoBehaviour
    {
        [SerializeField] private PracticeSession session;
        [SerializeField] private ShotController shotController;
        [SerializeField] private TableBuilder table;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private MonoBehaviour inputSource;
        [SerializeField] private Camera viewCamera;
        [SerializeField, Tooltip("Pick radius around a ball, in ball radii.")] private float pickRadius = 2.2f;

        private IShotInput input;
        private CameraMode previousCamera;
        private PoolBall held;
        private Vector3 lastValid;
        private bool overPocket;

        public bool IsEditing { get; private set; }

        public PoolBall HeldBall => held;

        public bool CanEdit => session != null && session.IsActive && shotController != null && shotController.Phase == ShotPhase.Aiming;

        public event Action EditingChanged;

        public void Configure(PracticeSession practice, ShotController shot, TableBuilder tableBuilder, CameraController cameras, MonoBehaviour inputBehaviour, Camera camera)
        {
            session = practice;
            shotController = shot;
            table = tableBuilder;
            cameraController = cameras;
            inputSource = inputBehaviour;
            viewCamera = camera;
            input = inputSource as IShotInput;
        }

        private void Awake()
        {
            input ??= inputSource as IShotInput;
        }

        public void SetEditing(bool editing)
        {
            if (editing == IsEditing || (editing && !CanEdit))
            {
                return;
            }

            if (!editing)
            {
                Drop();
            }

            IsEditing = editing;
            shotController.LayoutEditing = editing;
            if (editing)
            {
                // Any pending ball in hand is settled; the cue ball can be dragged like the others.
                session.TurnManager.Placement.Clear();
                if (cameraController != null)
                {
                    previousCamera = cameraController.ActiveMode;
                    cameraController.SetMode(CameraMode.Top);
                }
            }
            else
            {
                if (cameraController != null)
                {
                    cameraController.SetMode(previousCamera);
                }

                session.LayoutChanged();
            }

            EditingChanged?.Invoke();
        }

        private void Update()
        {
            if (!IsEditing)
            {
                return;
            }

            if (!CanEdit)
            {
                SetEditing(false);
                return;
            }

            if (input == null || Time.timeScale <= 0f)
            {
                return;
            }

            if (input.PointerPressed && TryGetPointerOnTable(out Vector3 point))
            {
                TryPick(point);
            }

            if (held == null)
            {
                return;
            }

            if (input.PointerHeld)
            {
                if (TryGetPointerOnTable(out Vector3 drag))
                {
                    DragTo(drag);
                }
            }
            else
            {
                Drop();
            }
        }

        /// <summary>Picks up the ball nearest to a point on the table (within the pick radius).</summary>
        public bool TryPick(Vector3 point)
        {
            if (!IsEditing || held != null)
            {
                return false;
            }

            PoolBall best = null;
            float bestDistance = float.MaxValue;
            var balls = BallRegistry.Balls;
            for (int i = 0; i < balls.Count; i++)
            {
                PoolBall ball = balls[i];
                if (!PracticeSession.IsOnTable(ball))
                {
                    continue;
                }

                Vector3 delta = ball.Position - point;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance < ball.Radius * pickRadius && distance < bestDistance)
                {
                    best = ball;
                    bestDistance = distance;
                }
            }

            if (best == null)
            {
                return false;
            }

            held = best;
            lastValid = best.Position;
            overPocket = false;
            held.Hold(lastValid);
            return true;
        }

        /// <summary>Moves the held ball toward a point: it follows only through legal positions; over a pocket it hovers there.</summary>
        public void DragTo(Vector3 point)
        {
            if (held == null)
            {
                return;
            }

            float radius = held.Radius;
            Pocket pocket = PocketUnder(point);
            overPocket = pocket != null && !held.IsCueBall;
            if (overPocket)
            {
                held.MoveHeld(new Vector3(pocket.Center.x, table.SurfaceHeight + radius, pocket.Center.z));
                return;
            }

            Vector3 target = ClampToCloth(point, radius);
            if (IsFree(target, held))
            {
                lastValid = target;
            }

            held.MoveHeld(lastValid);
        }

        /// <summary>Releases the held ball: placed at its last legal position, or taken off the table if over a pocket.</summary>
        public void Drop()
        {
            if (held == null)
            {
                return;
            }

            PoolBall ball = held;
            held = null;
            ball.PlaceAt(lastValid);
            if (overPocket)
            {
                session.RemoveBall(ball);
            }

            overPocket = false;
        }

        private bool TryGetPointerOnTable(out Vector3 point)
        {
            Camera camera = viewCamera != null ? viewCamera : Camera.main;
            point = default;
            if (camera == null || table == null)
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(input.PointerPosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, table.SurfaceHeight + PoolConstants.BallRadius, 0f));
            if (!plane.Raycast(ray, out float distance))
            {
                return false;
            }

            point = ray.GetPoint(distance);
            return true;
        }

        private Pocket PocketUnder(Vector3 point)
        {
            var pockets = table.PocketManager.Pockets;
            for (int i = 0; i < pockets.Count; i++)
            {
                if (pockets[i].IsOverHole(point))
                {
                    return pockets[i];
                }
            }

            return null;
        }

        private Vector3 ClampToCloth(Vector3 point, float radius)
        {
            TableGeometry geometry = table.Geometry;
            Vector3 local = table.transform.InverseTransformPoint(point);
            float maxX = geometry.HalfWidth - radius - 0.0015f;
            float maxZ = geometry.HalfLength - radius - 0.0015f;
            local.x = Mathf.Clamp(local.x, -maxX, maxX);
            local.z = Mathf.Clamp(local.z, -maxZ, maxZ);
            local.y = geometry.surfaceHeight + radius;
            return table.transform.TransformPoint(local);
        }

        private bool IsFree(Vector3 position, PoolBall ignore)
        {
            if (PocketUnder(position) != null)
            {
                return false;
            }

            var balls = BallRegistry.Balls;
            float minDistance = 2f * ignore.Radius + 0.0005f;
            for (int i = 0; i < balls.Count; i++)
            {
                PoolBall other = balls[i];
                if (other == ignore || other.IsPocketed)
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
    }
}
