using System.Collections.Generic;
using UnityEngine;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Table;

namespace VTG.Pool.Simulation
{
    /// <summary>
    /// Billiards-specific physics layered on PhysX.
    /// PhysX handles static contacts (slate support, cushions, jaws, liners) with a frictionless,
    /// non-bouncy cloth material. Before every PhysX step this system applies the cloth model (sliding,
    /// rolling, spin decay, stopping) and resolves ball-ball collisions itself (swept, pairwise, in time
    /// order via <see cref="BallCollisionModel"/>): PhysX's simultaneous solver makes a rack behave like one
    /// rigid body and bounces the cue ball back on the break. Cushion impacts are resolved by
    /// <see cref="PoolBall"/> through <see cref="CushionResponseModel"/>.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PoolPhysicsSystem : MonoBehaviour
    {
        [SerializeField] private BallPhysicsProfile profile;
        [SerializeField] private PocketManager pocketManager;
        [SerializeField, Tooltip("Height of the cloth surface (world Y).")]
        private float surfaceHeight = 0.76f;

        [SerializeField, Tooltip("Apply bounce threshold / solver settings from the profile at startup.")]
        private bool applyGlobalPhysicsSettings = true;

        private const int MaxBallCollisionsPerStep = 256;

        /// <summary>Ball-ball collisions resolved in the last step (diagnostics).</summary>
        public int LastStepBallCollisions { get; private set; }

        /// <summary>Steps in which the per-step collision budget ran out (diagnostics).</summary>
        public int CollisionBudgetExhausted { get; private set; }

        private readonly List<PoolBall> activeBalls = new List<PoolBall>(16);
        private Vector3[] positions = new Vector3[16];
        private Vector3[] velocities = new Vector3[16];
        private Vector3[] angulars = new Vector3[16];
        private bool[] touched = new bool[16];
        private bool allBallsAtRest = true;
        private int movingBallCount;

        public BallPhysicsProfile Profile => profile;

        public float SurfaceHeight => surfaceHeight;

        public PocketManager PocketManager => pocketManager;

        /// <summary>True when every ball on the table is stationary (evaluated each physics step).</summary>
        public bool AllBallsAtRest => allBallsAtRest;

        public int MovingBallCount => movingBallCount;

        /// <summary>Number of physics steps simulated since startup.</summary>
        public long StepCount { get; private set; }

        public void Configure(BallPhysicsProfile newProfile, PocketManager newPocketManager, float newSurfaceHeight)
        {
            profile = newProfile;
            pocketManager = newPocketManager;
            surfaceHeight = newSurfaceHeight;
        }

        private void Awake()
        {
            if (profile == null)
            {
                Debug.LogError("[PoolPhysicsSystem] No BallPhysicsProfile assigned.", this);
                enabled = false;
                return;
            }

            if (applyGlobalPhysicsSettings)
            {
                Physics.bounceThreshold = profile.bounceThreshold;
                Physics.defaultSolverIterations = profile.solverIterations;
                Physics.defaultSolverVelocityIterations = profile.solverVelocityIterations;
                Physics.defaultMaxAngularSpeed = profile.maxAngularVelocity;
            }

            // Ball-ball contacts are resolved by this system, never by PhysX.
            Physics.IgnoreLayerCollision(PoolLayers.Ball, PoolLayers.Ball, true);
            Physics.IgnoreLayerCollision(PoolLayers.Ball, PoolLayers.CueBall, true);
            Physics.IgnoreLayerCollision(PoolLayers.CueBall, PoolLayers.CueBall, true);
        }

        private void FixedUpdate()
        {
            if (Physics.simulationMode == SimulationMode.FixedUpdate)
            {
                PreStep(Time.fixedDeltaTime);
            }
        }

        /// <summary>
        /// Manually advances the simulation (requires Physics.simulationMode = Script).
        /// Used by tests and future prediction.
        /// </summary>
        public void Simulate(float deltaTime)
        {
            PreStep(deltaTime);
            Physics.Simulate(deltaTime);
        }

        /// <summary>Applies the cloth model and pocket logic for the upcoming PhysX step.</summary>
        public void PreStep(float deltaTime)
        {
            if (profile == null)
            {
                return;
            }

            if (pocketManager != null)
            {
                pocketManager.Step();
            }

            ClothParameters cloth = profile.ToClothParameters(-Physics.gravity.y);
            float restHeight = surfaceHeight + profile.ballRadius;
            int moving = 0;

            IReadOnlyList<PoolBall> balls = BallRegistry.Balls;
            for (int i = 0; i < balls.Count; i++)
            {
                PoolBall ball = balls[i];
                if (!ball.IsConfiguredWith(profile))
                {
                    ball.Configure(profile);
                }

                ball.RestHeight = restHeight;
                ball.BeginStep(deltaTime);
                if (ball.IsPocketed || ball.IsHeld)
                {
                    continue;
                }

                Rigidbody body = ball.Body;
                if (body.linearVelocity.y > profile.maxUpwardSpeed)
                {
                    // PhysX contacts with the wooden rail top use the ball's bounciness; keep flights believable.
                    Vector3 limited = body.linearVelocity;
                    limited.y = profile.maxUpwardSpeed;
                    body.linearVelocity = limited;
                }

                BallState state;
                bool onCloth = !ball.IsOverPocketHole && Mathf.Abs(body.position.y - restHeight) <= profile.clothContactTolerance;
                if (onCloth && ball.CurrentState == BallState.Stationary && body.IsSleeping())
                {
                    state = BallState.Stationary;
                }
                else if (onCloth)
                {
                    Vector3 linear = body.linearVelocity;
                    Vector3 angular = body.angularVelocity;

                    // Landing after a jump: the bed contact stopped the fall; give back part of it as a bounce.
                    float landing = -ball.PreStepLinearVelocity.y;
                    if (ball.CurrentState == BallState.Airborne && landing > profile.landingBounceThreshold && linear.y < landing * 0.5f)
                    {
                        linear.y = landing * profile.landingRestitution;
                    }

                    ClothMotionPhase phase = ClothFrictionModel.Step(ref linear, ref angular, cloth, deltaTime);
                    state = ToBallState(phase);
                    if (state == BallState.Stationary)
                    {
                        linear = Vector3.zero;
                        angular = Vector3.zero;
                    }

                    body.linearVelocity = linear;
                    body.angularVelocity = angular;
                }
                else
                {
                    state = BallState.Airborne;
                }

                ball.SetState(state);
            }

            SolveBallCollisions(deltaTime);

            for (int i = 0; i < balls.Count; i++)
            {
                PoolBall ball = balls[i];
                if (ball.IsPocketed || ball.IsHeld)
                {
                    continue;
                }

                ball.RecordPreStep();
                if (ball.CurrentState != BallState.Stationary)
                {
                    moving++;
                }
            }

            movingBallCount = moving;
            allBallsAtRest = moving == 0;
            StepCount++;
        }

        /// <summary>
        /// Event-driven ball-ball collisions over one step: find the earliest impact, advance all balls to it,
        /// resolve that pair, repeat. Balls involved are re-positioned so that PhysX's integration with the
        /// new velocity ends at the event-driven end position.
        /// </summary>
        private void SolveBallCollisions(float deltaTime)
        {
            activeBalls.Clear();
            IReadOnlyList<PoolBall> balls = BallRegistry.Balls;
            for (int i = 0; i < balls.Count; i++)
            {
                PoolBall ball = balls[i];
                if (!ball.IsPocketed && !ball.IsHeld && ball.isActiveAndEnabled)
                {
                    activeBalls.Add(ball);
                }
            }

            int count = activeBalls.Count;
            if (count < 2)
            {
                return;
            }

            EnsureCapacity(count);
            for (int i = 0; i < count; i++)
            {
                Rigidbody body = activeBalls[i].Body;
                positions[i] = body.position;
                velocities[i] = body.linearVelocity;
                angulars[i] = body.angularVelocity;
                touched[i] = false;
            }

            float contactDistance = 2f * profile.ballRadius;
            var parameters = new BallCollisionParameters
            {
                Restitution = profile.ballRestitution,
                Friction = profile.ballBallFriction,
                Radius = profile.ballRadius
            };

            SeparateOverlaps(count, contactDistance);

            float remaining = deltaTime;
            int resolved = 0;
            for (int collisions = 0; collisions < MaxBallCollisionsPerStep; collisions++)
            {
                float earliest = float.MaxValue;
                int first = -1;
                int second = -1;
                for (int i = 0; i < count - 1; i++)
                {
                    for (int j = i + 1; j < count; j++)
                    {
                        if (BallCollisionModel.TimeOfImpact(positions[i], velocities[i], positions[j], velocities[j], contactDistance, remaining, out float time)
                            && time < earliest)
                        {
                            earliest = time;
                            first = i;
                            second = j;
                        }
                    }
                }

                if (first < 0)
                {
                    break;
                }

                for (int i = 0; i < count; i++)
                {
                    positions[i] += velocities[i] * earliest;
                }

                remaining -= earliest;
                if (!BallCollisionModel.Resolve(ref velocities[first], ref angulars[first], ref velocities[second], ref angulars[second],
                        positions[first], positions[second], parameters, out float impulse))
                {
                    break;
                }

                touched[first] = true;
                touched[second] = true;
                resolved++;
                RaiseBallHit(activeBalls[first], activeBalls[second], impulse * profile.ballMass, (positions[first] + positions[second]) * 0.5f);
            }

            LastStepBallCollisions = resolved;
            if (resolved >= MaxBallCollisionsPerStep)
            {
                CollisionBudgetExhausted++;
            }

            for (int i = 0; i < count; i++)
            {
                if (!touched[i])
                {
                    continue;
                }

                PoolBall ball = activeBalls[i];
                Rigidbody body = ball.Body;
                // A ball on the cloth driven down by a collision from above (a hopping ball) rebounds off the slate.
                if (velocities[i].y < -profile.landingBounceThreshold && positions[i].y <= surfaceHeight + profile.ballRadius + profile.clothContactTolerance)
                {
                    velocities[i].y = -velocities[i].y * profile.landingRestitution;
                }

                Vector3 end = positions[i] + velocities[i] * remaining;
                body.position = end - velocities[i] * deltaTime;
                body.linearVelocity = velocities[i];
                body.angularVelocity = angulars[i];
                body.WakeUp();
                if (ball.CurrentState == BallState.Stationary && velocities[i].sqrMagnitude > profile.minimumLinearVelocity * profile.minimumLinearVelocity)
                {
                    ball.SetState(BallState.Sliding);
                }
            }
        }

        /// <summary>Pushes apart balls that overlap (e.g. after a re-spot) without changing their velocities.</summary>
        private void SeparateOverlaps(int count, float contactDistance)
        {
            for (int i = 0; i < count - 1; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    Vector3 delta = positions[j] - positions[i];
                    float distance = delta.magnitude;
                    if (distance < contactDistance - 0.0001f && distance > 1e-6f)
                    {
                        Vector3 push = delta / distance * ((contactDistance - distance) * 0.5f);
                        positions[i] -= push;
                        positions[j] += push;
                        touched[i] = true;
                        touched[j] = true;
                    }
                }
            }
        }

        private static void RaiseBallHit(PoolBall a, PoolBall b, float impulse, Vector3 point)
        {
            if (b.BallId < a.BallId)
            {
                PoolBall swap = a;
                a = b;
                b = swap;
            }

            PoolEvents.RaiseBallHit(new BallHitInfo(a, b, impulse, point));
        }

        private void EnsureCapacity(int count)
        {
            if (positions.Length >= count)
            {
                return;
            }

            int size = Mathf.NextPowerOfTwo(count);
            positions = new Vector3[size];
            velocities = new Vector3[size];
            angulars = new Vector3[size];
            touched = new bool[size];
        }

        private static BallState ToBallState(ClothMotionPhase phase)
        {
            switch (phase)
            {
                case ClothMotionPhase.Sliding: return BallState.Sliding;
                case ClothMotionPhase.Rolling: return BallState.Rolling;
                case ClothMotionPhase.Spinning: return BallState.Spinning;
                default: return BallState.Stationary;
            }
        }
    }
}
