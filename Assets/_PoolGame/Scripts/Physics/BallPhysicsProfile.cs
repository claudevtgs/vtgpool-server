using UnityEngine;
using VTG.Pool.Core;

namespace VTG.Pool.Simulation
{
    /// <summary>
    /// All ball/cloth/cushion tuning in one asset so physics can be tuned without recompiling.
    /// Values are SI units. Defaults are based on published billiards measurements
    /// (Marlow, Alciatore "TP" notes, Mathavan et al.).
    /// </summary>
    [CreateAssetMenu(menuName = "VTG Pool/Ball Physics Profile", fileName = "BallPhysicsProfile")]
    public sealed class BallPhysicsProfile : ScriptableObject
    {
        [Header("Ball")]
        [Tooltip("Ball mass in kg.")]
        public float ballMass = PoolConstants.BallMass;

        [Tooltip("Ball radius in metres (diameter 0.05715).")]
        public float ballRadius = PoolConstants.BallRadius;

        [Tooltip("Bounce of a ball landing on the cloth after a jump (fraction of the downward speed).")]
        [Range(0f, 0.9f)] public float landingRestitution = 0.5f;

        [Tooltip("Landing speeds below this do not bounce (m/s).")]
        public float landingBounceThreshold = 0.25f;

        [Tooltip("Largest upward ball speed (m/s): a ball bouncing off the hard rail top cannot fly higher than ~0.45 m.")]
        public float maxUpwardSpeed = 3f;

        [Tooltip("Slate rebound of the downward part of a hard cushion impact (ball hop). 0 disables hops.")]
        [Range(0f, 0.8f)] public float cushionHopRebound = 0.15f;

        [Tooltip("Normal velocity change (m/s) below which a cushion impact never hops.")]
        public float cushionHopThreshold = 8f;

        [Tooltip("Ball-ball coefficient of restitution (applied through PM_Ball).")]
        [Range(0f, 1f)] public float ballRestitution = 0.93f;

        [Tooltip("Ball-ball friction (produces throw / spin transfer).")]
        [Range(0f, 0.3f)] public float ballBallFriction = 0.06f;

        [Header("Cloth")]
        [Tooltip("Kinetic friction between ball and cloth while the contact point slips.")]
        [Range(0.05f, 0.5f)] public float slidingFriction = 0.2f;

        [Tooltip("Rolling resistance coefficient (deceleration = coefficient * g).")]
        [Range(0.001f, 0.05f)] public float rollingResistance = 0.012f;

        [Tooltip("Spin (vertical axis) friction. Angular deceleration = 2.5 * coefficient * g / R.")]
        [Range(0.001f, 0.1f)] public float spinFriction = 0.012f;

        [Header("Cushion")]
        [Tooltip("Cushion normal coefficient of restitution at low impact speed.")]
        [Range(0.3f, 1f)] public float cushionRestitution = 0.85f;

        [Tooltip("Restitution lost per m/s of normal impact speed (harder hits rebound relatively less).")]
        [Range(0f, 0.1f)] public float cushionRestitutionSpeedLoss = 0.025f;

        [Tooltip("Lower bound for speed-dependent cushion restitution.")]
        [Range(0.2f, 1f)] public float cushionMinRestitution = 0.6f;

        [Tooltip("Coulomb friction between ball and cushion rubber (side spin / angle change).")]
        [Range(0f, 0.6f)] public float cushionTangentialFriction = 0.2f;

        [Header("Rest detection")]
        [Tooltip("Below this translational speed (m/s) a rolling ball is stopped.")]
        public float minimumLinearVelocity = 0.005f;

        [Tooltip("Below this vertical-axis spin (rad/s) spin is stopped.")]
        public float minimumAngularVelocity = 0.2f;

        [Tooltip("Contact-point slip speed (m/s) treated as pure rolling.")]
        public float slipSpeedEpsilon = 0.002f;

        [Tooltip("PhysX Rigidbody.sleepThreshold (mass-normalised kinetic energy).")]
        public float sleepThreshold = 0.0001f;

        [Header("PhysX")]
        [Tooltip("Rigidbody.maxAngularVelocity. A ball rolling at 10 m/s spins at ~350 rad/s.")]
        public float maxAngularVelocity = 1500f;

        [Tooltip("Collider contact offset (m). Small values avoid early contacts at this scale.")]
        public float contactOffset = 0.001f;

        [Tooltip("Per-body solver iterations.")]
        public int solverIterations = 10;

        [Tooltip("Per-body solver velocity iterations.")]
        public int solverVelocityIterations = 4;

        [Tooltip("Global Physics.bounceThreshold. Must be low or slow ball-ball hits become inelastic.")]
        public float bounceThreshold = 0.02f;

        [Tooltip("Vertical tolerance (m) for treating a ball as supported by the cloth.")]
        public float clothContactTolerance = 0.004f;

        [Header("Materials")]
        public PhysicsMaterial ballMaterial;

        /// <summary>Moment of inertia of a solid ball.</summary>
        public float MomentOfInertia => PoolConstants.SolidSphereInertiaFactor * ballMass * ballRadius * ballRadius;

        public ClothParameters ToClothParameters(float gravity)
        {
            return new ClothParameters
            {
                SlidingFriction = slidingFriction,
                RollingResistance = rollingResistance,
                SpinFriction = spinFriction,
                Gravity = gravity,
                Radius = ballRadius,
                MinLinearSpeed = minimumLinearVelocity,
                MinAngularSpeed = minimumAngularVelocity,
                SlipSpeedEpsilon = slipSpeedEpsilon
            };
        }

        public CushionParameters ToCushionParameters(float contactHeightAboveCentre, float restitutionScale, float frictionScale)
        {
            return new CushionParameters
            {
                Restitution = cushionRestitution * restitutionScale,
                RestitutionSpeedLoss = cushionRestitutionSpeedLoss,
                MinRestitution = cushionMinRestitution * restitutionScale,
                Friction = cushionTangentialFriction * frictionScale,
                Radius = ballRadius,
                ContactHeight = contactHeightAboveCentre,
                HopRebound = cushionHopRebound,
                HopThreshold = cushionHopThreshold
            };
        }

        private void OnValidate()
        {
            ballMass = Mathf.Max(0.01f, ballMass);
            ballRadius = Mathf.Max(0.005f, ballRadius);
            minimumLinearVelocity = Mathf.Max(0f, minimumLinearVelocity);
            minimumAngularVelocity = Mathf.Max(0f, minimumAngularVelocity);
            slipSpeedEpsilon = Mathf.Max(1e-5f, slipSpeedEpsilon);
            maxAngularVelocity = Mathf.Max(50f, maxAngularVelocity);
            contactOffset = Mathf.Clamp(contactOffset, 0.0001f, 0.01f);
            solverIterations = Mathf.Clamp(solverIterations, 1, 64);
            solverVelocityIterations = Mathf.Clamp(solverVelocityIterations, 1, 64);
            bounceThreshold = Mathf.Max(0.001f, bounceThreshold);
            clothContactTolerance = Mathf.Clamp(clothContactTolerance, 0.0005f, 0.02f);
            cushionMinRestitution = Mathf.Min(cushionMinRestitution, cushionRestitution);
        }
    }
}
