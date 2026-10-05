using UnityEngine;
using VTG.Pool.Core;
using VTG.Pool.Simulation;
using VTG.Pool.Table;

namespace VTG.Pool.Balls
{
    /// <summary>
    /// A single pool ball. Owns its Rigidbody/SphereCollider and per-ball physics state.
    /// Cloth friction is applied by <see cref="PoolPhysicsSystem"/>; cushion impacts are resolved
    /// here with <see cref="CushionResponseModel"/>. Contains no rule logic.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    [DisallowMultipleComponent]
    public sealed class PoolBall : MonoBehaviour
    {
        /// <summary>Gap (m) at which a cushion contact counts as touching.</summary>
        private const float CushionTouchTolerance = 0.0015f;

        [SerializeField] private BallDefinition definition;
        [SerializeField] private BallPhysicsProfile profile;
        [SerializeField, Tooltip("Used only when no BallDefinition is assigned.")]
        private int fallbackId = -1;

        private Rigidbody body;
        private SphereCollider sphere;
        private BallPhysicsProfile configuredProfile;
        private Collider ignoredBed;
        private bool cushionResolvedThisStep;
        private float stepDeltaTime = 1f / 120f;

        /// <summary>Ball number (0 = cue ball).</summary>
        public int BallId => definition != null ? definition.BallNumber : fallbackId;

        public BallType BallType => definition != null ? definition.BallType : (fallbackId == 0 ? BallType.Cue : BallType.Solid);

        public BallDefinition Definition => definition;

        public bool IsCueBall => BallType == BallType.Cue;

        public Rigidbody Body => body;

        public SphereCollider Collider => sphere;

        public BallPhysicsProfile Profile => configuredProfile != null ? configuredProfile : profile;

        public BallState CurrentState { get; private set; } = BallState.Stationary;

        public bool IsPocketed { get; private set; }

        /// <summary>Pocket whose hole the ball is currently over (or captured by).</summary>
        public Pocket CurrentPocket { get; private set; }

        public bool IsOverPocketHole => CurrentPocket != null && !IsPocketed;

        public Vector3 Position => body.position;

        public Vector3 LinearVelocity => body.linearVelocity;

        public Vector3 AngularVelocity => body.angularVelocity;

        public float Radius => Profile != null ? Profile.ballRadius : PoolConstants.BallRadius;

        /// <summary>Velocity entering the current PhysX step (used for impact models).</summary>
        public Vector3 PreStepLinearVelocity { get; private set; }

        public Vector3 PreStepAngularVelocity { get; private set; }

        /// <summary>Position at the start of the current PhysX step.</summary>
        public Vector3 PreStepPosition { get; private set; }

        public bool IsMoving => CurrentState != BallState.Stationary && CurrentState != BallState.Pocketed;

        /// <summary>Ball is being positioned by the player (ball in hand): kinematic, no collider, not simulated.</summary>
        public bool IsHeld { get; private set; }

        private void Awake()
        {
            CacheComponents();
            if (profile != null)
            {
                Configure(profile);
            }
        }

        private void OnEnable()
        {
            CacheComponents();
            BallRegistry.Register(this);
        }

        private void OnDisable()
        {
            BallRegistry.Unregister(this);
        }

        private void CacheComponents()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            if (sphere == null)
            {
                sphere = GetComponent<SphereCollider>();
            }
        }

        public void SetDefinition(BallDefinition newDefinition, BallPhysicsProfile newProfile)
        {
            definition = newDefinition;
            profile = newProfile;
        }

        public bool IsConfiguredWith(BallPhysicsProfile candidate) => configuredProfile == candidate;

        /// <summary>Applies Rigidbody/collider settings from the profile (MASTER_PROMPT section 9).</summary>
        public void Configure(BallPhysicsProfile newProfile)
        {
            if (newProfile == null)
            {
                return;
            }

            CacheComponents();
            configuredProfile = newProfile;
            body.mass = newProfile.ballMass;
            body.useGravity = true;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.maxAngularVelocity = newProfile.maxAngularVelocity;
            body.sleepThreshold = newProfile.sleepThreshold;
            body.solverIterations = newProfile.solverIterations;
            body.solverVelocityIterations = newProfile.solverVelocityIterations;

            float scale = Mathf.Max(1e-4f, transform.lossyScale.x);
            sphere.radius = newProfile.ballRadius / scale;
            sphere.contactOffset = newProfile.contactOffset;
            if (newProfile.ballMaterial != null)
            {
                sphere.sharedMaterial = newProfile.ballMaterial;
            }

            gameObject.layer = IsCueBall ? PoolLayers.CueBall : PoolLayers.Ball;
        }

        /// <summary>Called by the physics system at the start of every fixed step.</summary>
        /// <summary>Ball-centre height when resting on the cloth (set by the physics system every step).</summary>
        internal float RestHeight { get; set; } = float.NaN;

        internal void BeginStep(float deltaTime)
        {
            cushionResolvedThisStep = false;
            stepDeltaTime = deltaTime;
        }

        internal void SetState(BallState state)
        {
            CurrentState = state;
        }

        internal void RecordPreStep()
        {
            PreStepPosition = body.position;
            PreStepLinearVelocity = body.linearVelocity;
            PreStepAngularVelocity = body.angularVelocity;
        }

        /// <summary>Adds a cue strike (velocity change) to the ball.</summary>
        public void ApplyStrike(Vector3 linearVelocity, Vector3 angularVelocity)
        {
            if (IsPocketed)
            {
                return;
            }

            body.WakeUp();
            body.linearVelocity += linearVelocity;
            body.angularVelocity += angularVelocity;
            CurrentState = BallState.Sliding;
            RecordPreStep();
        }

        /// <summary>Places the ball at rest on the table (used by racks, presets and resets).</summary>
        public void PlaceAt(Vector3 position)
        {
            CacheComponents();
            ClearPocketState();
            IsHeld = false;
            sphere.enabled = true;
            body.isKinematic = false;
            body.position = position;
            body.rotation = Quaternion.identity;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            CurrentState = BallState.Stationary;
            PreStepPosition = position;
            PreStepLinearVelocity = Vector3.zero;
            PreStepAngularVelocity = Vector3.zero;
        }

        /// <summary>Picks the ball up for placement (ball in hand). Use <see cref="MoveHeld"/>, then <see cref="PlaceAt"/>.</summary>
        public void Hold(Vector3 position)
        {
            PlaceAt(position);
            IsHeld = true;
            body.isKinematic = true;
            sphere.enabled = false;
        }

        public void MoveHeld(Vector3 position)
        {
            if (!IsHeld)
            {
                return;
            }

            body.position = position;
            transform.position = position;
        }

        /// <summary>Ball centre is over a pocket hole: stop colliding with the bed so it can drop.</summary>
        internal void EnterPocketHole(Pocket pocket, Collider bed)
        {
            CurrentPocket = pocket;
            if (bed != null && ignoredBed != bed)
            {
                Physics.IgnoreCollision(sphere, bed, true);
                ignoredBed = bed;
            }
        }

        /// <summary>Ball rolled back out over the slate before dropping.</summary>
        internal void ExitPocketHole()
        {
            CurrentPocket = null;
            RestoreBedCollision();
        }

        internal void MarkPocketed(Pocket pocket)
        {
            CurrentPocket = pocket;
            IsPocketed = true;
            CurrentState = BallState.Pocketed;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            sphere.enabled = false;
        }

        private void ClearPocketState()
        {
            IsPocketed = false;
            CurrentPocket = null;
            RestoreBedCollision();
        }

        private void RestoreBedCollision()
        {
            if (ignoredBed != null)
            {
                Physics.IgnoreCollision(sphere, ignoredBed, false);
                ignoredBed = null;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsPocketed)
            {
                return;
            }

            // Ball-ball contacts are resolved (and reported) by PoolPhysicsSystem; PhysX ignores them.
            Collider other = collision.collider;
            if (other.TryGetComponent(out CushionSurface surface))
            {
                ResolveCushionContact(collision, surface);
            }
        }

        /// <summary>
        /// PhysX reports contacts before shapes touch (contact distance / CCD), so cushion contacts are
        /// re-evaluated every step until the ball actually reaches the surface.
        /// </summary>
        private void OnCollisionStay(Collision collision)
        {
            if (!IsPocketed && collision.collider.TryGetComponent(out CushionSurface surface))
            {
                ResolveCushionContact(collision, surface);
            }
        }

        private void ResolveCushionContact(Collision collision, CushionSurface surface)
        {
            BallPhysicsProfile activeProfile = Profile;
            if (activeProfile == null)
            {
                return;
            }

            // Closest contact of this pair.
            ContactPoint contact = collision.GetContact(0);
            for (int i = 1; i < collision.contactCount; i++)
            {
                ContactPoint candidate = collision.GetContact(i);
                if (candidate.separation < contact.separation)
                {
                    contact = candidate;
                }
            }

            // PhysX's contact normal (sphere vs box face or edge) passes through the ball centre. Flatten it and
            // orient it toward the ball. Do not derive it from body.position: that is the post-step position,
            // while the contact point was generated before integration, which skews the normal at speed.
            Vector3 normal = contact.normal;
            normal.y = 0f;
            Vector3 toBall = PreStepPosition - contact.point;
            toBall.y = 0f;
            if (normal.sqrMagnitude < 1e-10f)
            {
                normal = toBall;
            }
            else if (Vector3.Dot(normal, toBall) < 0f)
            {
                normal = -normal;
            }

            normal.Normalize();

            // Only resolve in the step in which the ball actually reaches the surface:
            // gap <= approach distance this step (+ small tolerance). Earlier (speculative) contacts are ignored.
            Vector3 incoming = cushionResolvedThisStep ? body.linearVelocity : PreStepLinearVelocity;
            float approachSpeed = -Vector3.Dot(incoming, normal);
            if (approachSpeed <= 0f || contact.separation > approachSpeed * stepDeltaTime + CushionTouchTolerance)
            {
                return;
            }

            // First cushion contact this step uses the pre-step velocity (PhysX has already zeroed the
            // approach). Further contacts in the same step (corner / jaw point) chain on the result.
            Vector3 linear = cushionResolvedThisStep ? body.linearVelocity : PreStepLinearVelocity;
            Vector3 angular = cushionResolvedThisStep ? body.angularVelocity : PreStepAngularVelocity;

            // Vertical motion is PhysX's (gravity during this step); the cushion model only changes horizontal motion.
            linear.y = body.linearVelocity.y;

            CushionParameters parameters = surface.BuildParameters(activeProfile);
            parameters.BallLift = float.IsNaN(RestHeight) ? 0f : PreStepPosition.y - RestHeight;
            if (CushionResponseModel.Resolve(ref linear, ref angular, normal, parameters, out float impactSpeed))
            {
                body.linearVelocity = linear;
                body.angularVelocity = angular;
                cushionResolvedThisStep = true;
                PoolEvents.RaiseCushionHit(new CushionHitInfo(this, surface, impactSpeed, contact.point));
            }
        }
    }
}
