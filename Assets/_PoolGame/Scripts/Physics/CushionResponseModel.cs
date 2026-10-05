using UnityEngine;
using VTG.Pool.Core;

namespace VTG.Pool.Simulation
{
    /// <summary>Inputs for <see cref="CushionResponseModel"/>.</summary>
    public struct CushionParameters
    {
        /// <summary>Normal restitution at zero impact speed.</summary>
        public float Restitution;

        /// <summary>Restitution lost per m/s of normal impact speed.</summary>
        public float RestitutionSpeedLoss;

        /// <summary>Lower clamp of the speed-dependent restitution.</summary>
        public float MinRestitution;

        /// <summary>Coulomb friction coefficient between ball and rubber.</summary>
        public float Friction;

        public float Radius;

        /// <summary>Height of the cushion contact point above the ball centre (m) for a ball resting on the cloth.</summary>
        public float ContactHeight;

        /// <summary>How far the ball centre is above its resting height (m): a hopping ball meets the nose lower.</summary>
        public float BallLift;

        /// <summary>Slate rebound of the downward part of a hard cushion impulse (0 = never hop).</summary>
        public float HopRebound;

        /// <summary>Normal velocity change (m/s) below which a cushion impact never makes the ball hop.</summary>
        public float HopThreshold;
    }

    /// <summary>
    /// Single-impulse cushion model (simplified Han/Mathavan). Replaces PhysX's mirror reflection:
    /// - speed-dependent normal restitution,
    /// - Coulomb friction at the actual contact point (nose above the ball centre),
    /// - spin interaction: side spin changes the exit angle, top/back spin changes rebound spin.
    /// Vertical: the nose sits above the ball centre, so the impulse drives the ball into the slate; on very hard
    /// impacts the slate throws part of that back (a hop). A ball that is already in the air meets the nose below
    /// its centre and is deflected upward — that is how balls leave the table. Otherwise no upward bounce, and
    /// downward velocity is preserved so balls dropping into a pocket keep falling when they touch the pocket back.
    /// </summary>
    public static class CushionResponseModel
    {
        /// <summary>Restitution when an airborne ball strikes the rail edge below its centre.</summary>
        public const float EdgeRestitution = 0.3f;

        /// <summary>Largest upward speed (m/s) the rail edge can add to an airborne ball.</summary>
        public const float MaxEdgeLift = 2.4f;

        /// <summary>Speed restitution for a given normal impact speed.</summary>
        public static float EffectiveRestitution(in CushionParameters p, float impactSpeed)
        {
            return Mathf.Clamp(p.Restitution - p.RestitutionSpeedLoss * impactSpeed, p.MinRestitution, 1f);
        }

        /// <summary>
        /// Resolves an impact. <paramref name="normal"/> points from the cushion into the table.
        /// Returns false if the ball is not approaching the cushion.
        /// </summary>
        public static bool Resolve(ref Vector3 linear, ref Vector3 angular, Vector3 normal, in CushionParameters p, out float impactSpeed)
        {
            normal.y = 0f;
            float normalLength = normal.magnitude;
            if (normalLength < 1e-6f)
            {
                impactSpeed = 0f;
                return false;
            }

            normal /= normalLength;
            float approach = Vector3.Dot(linear, normal);
            impactSpeed = -approach;
            if (approach > -1e-4f)
            {
                return false;
            }

            float radius = p.Radius;
            float sinTheta = Mathf.Clamp((p.ContactHeight - Mathf.Max(0f, p.BallLift)) / radius, -0.9f, 0.9f);
            float cosTheta = Mathf.Sqrt(1f - sinTheta * sinTheta);

            // Contact point relative to the ball centre and the contact normal pointing into the ball.
            Vector3 contactOffset = radius * (-normal * cosTheta + Vector3.up * sinTheta);
            Vector3 contactNormal = -contactOffset / radius;

            float restitution = EffectiveRestitution(p, impactSpeed);
            float normalDeltaV = (1f + restitution) * impactSpeed;

            // Slip of the contact point in the contact tangent plane (before impact).
            Vector3 slip = linear + Vector3.Cross(angular, contactOffset);
            slip -= Vector3.Dot(slip, contactNormal) * contactNormal;

            // Impulse (per unit mass) that would stop the slip, limited by Coulomb friction.
            Vector3 tangentialDeltaV = -slip / PoolConstants.SurfaceImpulseFactor;
            float maxTangential = p.Friction * normalDeltaV;
            float tangentialMagnitude = tangentialDeltaV.magnitude;
            if (tangentialMagnitude > maxTangential && tangentialMagnitude > 0f)
            {
                tangentialDeltaV *= maxTangential / tangentialMagnitude;
            }

            float verticalBefore = linear.y;
            linear += normal * normalDeltaV + tangentialDeltaV;

            if (sinTheta >= 0f)
            {
                // Nose above the centre: pressed into the slate. Hard impacts hop; a falling ball keeps falling.
                float hop = p.HopRebound * Mathf.Max(0f, normalDeltaV - p.HopThreshold) * sinTheta;
                linear.y = verticalBefore < -0.05f ? verticalBefore : Mathf.Max(Mathf.Min(verticalBefore, 0f), hop);
            }
            else
            {
                // Airborne ball meeting the nose below its centre: the impulse acts along the contact normal, which
                // points up and into the table — the ball is lifted and, if high enough, rides over the rail.
                linear = linear - normal * normalDeltaV;
                Vector3 push = normal * cosTheta - Vector3.up * sinTheta;
                float along = -Vector3.Dot(linear, push);
                if (along > 0f)
                {
                    // The rail edge is hard and the hit glancing: little restitution, and the lift is capped so a
                    // ball leaving the table flies like a real one (tens of centimetres, not metres).
                    float lift = linear.y;
                    linear += push * ((1f + EdgeRestitution) * along);
                    linear.y = Mathf.Min(linear.y, Mathf.Max(lift, MaxEdgeLift));
                }
            }

            // dw = r x (m dv) / I = r x dv / (0.4 R^2)
            angular += Vector3.Cross(contactOffset, tangentialDeltaV) / (PoolConstants.SolidSphereInertiaFactor * radius * radius);
            return true;
        }
    }
}
