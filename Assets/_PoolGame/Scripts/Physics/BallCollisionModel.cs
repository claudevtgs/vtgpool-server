using UnityEngine;
using VTG.Pool.Core;

namespace VTG.Pool.Simulation
{
    /// <summary>Inputs for <see cref="BallCollisionModel"/>.</summary>
    public struct BallCollisionParameters
    {
        public float Restitution;
        public float Friction;
        public float Radius;
    }

    /// <summary>
    /// Ball-ball collision for equal solid spheres, independent of PhysX:
    /// swept time of impact and a single impulse with normal restitution and Coulomb friction at the
    /// contact point (collision-induced throw and spin transfer). Collisions are resolved pairwise and in
    /// time order by <see cref="PoolPhysicsSystem"/>, so a break propagates through the rack ball by ball
    /// instead of the rack acting as one rigid body.
    /// </summary>
    public static class BallCollisionModel
    {
        /// <summary>
        /// Earliest time in [0, maxTime] at which two moving spheres are <paramref name="contactDistance"/> apart
        /// while approaching. Touching/overlapping approaching balls return t = 0.
        /// </summary>
        public static bool TimeOfImpact(Vector3 positionA, Vector3 velocityA, Vector3 positionB, Vector3 velocityB,
            float contactDistance, float maxTime, out float time)
        {
            time = 0f;
            Vector3 d = positionB - positionA;
            Vector3 w = velocityB - velocityA;
            float approach = Vector3.Dot(d, w);
            if (approach >= 0f)
            {
                return false;
            }

            float c = d.sqrMagnitude - contactDistance * contactDistance;
            if (c <= 0f)
            {
                return true;
            }

            float a = w.sqrMagnitude;
            if (a < 1e-12f)
            {
                return false;
            }

            float b = 2f * approach;
            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                return false;
            }

            time = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
            return time >= 0f && time <= maxTime;
        }

        /// <summary>
        /// Resolves a contact between ball A at <paramref name="positionA"/> and ball B at <paramref name="positionB"/>.
        /// Returns false if the balls are not approaching. <paramref name="impulse"/> is the normal impulse per unit mass (m/s).
        /// </summary>
        public static bool Resolve(ref Vector3 velocityA, ref Vector3 angularA, ref Vector3 velocityB, ref Vector3 angularB,
            Vector3 positionA, Vector3 positionB, in BallCollisionParameters p, out float impulse)
        {
            impulse = 0f;
            Vector3 normal = positionB - positionA;
            float distance = normal.magnitude;
            if (distance < 1e-8f)
            {
                return false;
            }

            normal /= distance;
            float approachSpeed = Vector3.Dot(velocityA - velocityB, normal);
            if (approachSpeed <= 0f)
            {
                return false;
            }

            float radius = p.Radius;
            Vector3 offsetA = normal * radius;
            Vector3 offsetB = -normal * radius;

            // Contact-point slip before impact (tangential part only).
            Vector3 slip = (velocityA + Vector3.Cross(angularA, offsetA)) - (velocityB + Vector3.Cross(angularB, offsetB));
            slip -= Vector3.Dot(slip, normal) * normal;

            // Normal impulse (equal masses): each ball changes by (1+e)/2 of the approach speed.
            impulse = 0.5f * (1f + p.Restitution) * approachSpeed;
            velocityA -= normal * impulse;
            velocityB += normal * impulse;

            // Friction impulse that would stop the slip: both balls contribute 3.5/m of compliance -> 7/m.
            Vector3 friction = -slip / (2f * PoolConstants.SurfaceImpulseFactor);
            float maxFriction = p.Friction * impulse;
            float magnitude = friction.magnitude;
            if (magnitude > maxFriction && magnitude > 0f)
            {
                friction *= maxFriction / magnitude;
            }

            // Linear effect stays on the table plane (no jump from throw); angular effect is full 3D.
            Vector3 linearFriction = new Vector3(friction.x, 0f, friction.z);
            velocityA += linearFriction;
            velocityB -= linearFriction;
            float inverseInertia = 1f / (PoolConstants.SolidSphereInertiaFactor * radius * radius);
            angularA += Vector3.Cross(offsetA, friction) * inverseInertia;
            angularB += Vector3.Cross(offsetB, -friction) * inverseInertia;
            return true;
        }
    }
}
