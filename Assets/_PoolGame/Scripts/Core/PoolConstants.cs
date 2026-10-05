namespace VTG.Pool.Core
{
    /// <summary>
    /// Real-world reference values (SI units, 1 Unity unit = 1 m).
    /// Tunable values live in ScriptableObject profiles; these are only defaults and invariants.
    /// </summary>
    public static class PoolConstants
    {
        /// <summary>Standard pool ball diameter (2.25 in).</summary>
        public const float BallDiameter = 0.05715f;

        /// <summary>Standard pool ball radius.</summary>
        public const float BallRadius = BallDiameter * 0.5f;

        /// <summary>Nominal pool ball mass (~6 oz).</summary>
        public const float BallMass = 0.17f;

        /// <summary>Target physics rate (Hz).</summary>
        public const float TargetPhysicsHz = 120f;

        /// <summary>Target fixed timestep in seconds.</summary>
        public const float TargetFixedTimestep = 1f / TargetPhysicsHz;

        /// <summary>Moment-of-inertia factor of a solid sphere (I = k * m * r^2).</summary>
        public const float SolidSphereInertiaFactor = 0.4f;

        /// <summary>
        /// Effective-mass factor of an impulse applied at the surface of a solid sphere,
        /// tangential to the surface: 1 + m r^2 / I = 3.5.
        /// </summary>
        public const float SurfaceImpulseFactor = 1f + 1f / SolidSphereInertiaFactor;
    }
}
