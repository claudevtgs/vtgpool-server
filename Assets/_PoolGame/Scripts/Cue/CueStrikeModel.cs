using UnityEngine;
using VTG.Pool.Core;

namespace VTG.Pool.Cue
{
    /// <summary>Cue parameters used by <see cref="CueStrikeModel"/>.</summary>
    public struct CueStrikeSettings
    {
        public float CueMass;
        public float TipRestitution;
        public float MaxTipOffsetFraction;
        public float SquirtAtMaxOffsetDegrees;

        /// <summary>Vertical rebound of the ball off the slate when the cue drives it down (0 = no jump shots).</summary>
        public float SlateRebound;

        /// <summary>From this elevation the cue starts pinning the ball to the cloth (massé, no jump)...</summary>
        public float PinStartDegrees;

        /// <summary>...and from this one it is fully pinned.</summary>
        public float PinEndDegrees;
    }

    /// <summary>Linear and angular velocity given to the cue ball by a strike.</summary>
    public struct StrikeResult
    {
        public Vector3 LinearVelocity;
        public Vector3 AngularVelocity;

        /// <summary>Tip contact offset in units of ball radius (x = right, y = up).</summary>
        public Vector2 OffsetFraction;
    }

    /// <summary>
    /// Cue strike model (Alciatore, TP A-30 for a level cue; TP A-14/B-? style elevated cue for massé).
    /// The tip impulse J acts along the cue direction d at the contact point
    /// r = R(a right + b up' - sqrt(1-a^2-b^2) d), where up' is perpendicular to d in the vertical plane.
    /// Linear velocity v = J/m with its downward part absorbed by the slate; w = (r x J)/I.
    /// With a level cue d is the aim and this reduces to the classic model (natural roll at b = 0.4).
    /// With an elevated cue and a side offset, w gains a component about the aim axis: the cloth friction
    /// then bends the path (massé / swerve) without any special-case code.
    /// </summary>
    public static class CueStrikeModel
    {
        /// <summary>Ball speed after impact for a given cue speed and tip offset (a, b as fractions of R).</summary>
        public static float BallSpeed(float cueSpeed, float ballMass, in CueStrikeSettings settings, Vector2 offsetFraction)
        {
            float offsetTerm = 2.5f * offsetFraction.sqrMagnitude;
            return (1f + settings.TipRestitution) * cueSpeed / (1f + ballMass / settings.CueMass + offsetTerm);
        }

        /// <summary>Cue frame for an aim direction and elevation: d (butt to tip, pointing down for elevated cues), right, up'.</summary>
        public static void CueFrame(Vector3 aim, float elevationDegrees, out Vector3 direction, out Vector3 right, out Vector3 up)
        {
            aim.y = 0f;
            aim = aim.sqrMagnitude > 1e-8f ? aim.normalized : Vector3.forward;
            float elevation = Mathf.Clamp(elevationDegrees, 0f, 89f) * Mathf.Deg2Rad;
            float cos = Mathf.Cos(elevation);
            float sin = Mathf.Sin(elevation);
            right = Vector3.Cross(Vector3.up, aim);
            direction = aim * cos - Vector3.up * sin;
            up = aim * sin + Vector3.up * cos;
        }

        /// <summary>Tip contact point relative to the ball centre (offset in fractions of R, already scaled).</summary>
        public static Vector3 ContactPoint(Vector3 direction, Vector3 right, Vector3 up, Vector2 offset, float radius)
        {
            float depth = Mathf.Sqrt(Mathf.Max(0f, 1f - offset.sqrMagnitude));
            return radius * (offset.x * right + offset.y * up - depth * direction);
        }

        public static StrikeResult Compute(in ShotParameters shot, float cueSpeed, float ballMass, float ballRadius, in CueStrikeSettings settings)
        {
            Vector2 offset = Vector2.ClampMagnitude(shot.TipOffset, 1f) * settings.MaxTipOffsetFraction;
            float speed = BallSpeed(cueSpeed, ballMass, settings, offset);

            // Squirt: the ball leaves slightly away from the side of english.
            float squirtDegrees = -offset.x / Mathf.Max(1e-4f, settings.MaxTipOffsetFraction) * settings.SquirtAtMaxOffsetDegrees;
            Vector3 aim = Quaternion.AngleAxis(squirtDegrees, Vector3.up) * shot.AimDirection;
            CueFrame(aim, shot.CueElevation, out Vector3 direction, out Vector3 right, out Vector3 up);

            Vector3 impulse = direction * (ballMass * speed);
            Vector3 contact = ContactPoint(direction, right, up, offset, ballRadius);
            float inertia = PoolConstants.SolidSphereInertiaFactor * ballMass * ballRadius * ballRadius;

            // The slate stops the downward part of an elevated stroke and throws part of it back up: a jump.
            // Very steep strokes (massé) keep the ball pinned under the tip, so the rebound fades out there.
            Vector3 linear = impulse / ballMass;
            float down = Mathf.Max(0f, -linear.y);
            float pin = settings.PinEndDegrees > settings.PinStartDegrees
                ? Mathf.InverseLerp(settings.PinEndDegrees, settings.PinStartDegrees, shot.CueElevation)
                : 1f;
            linear.y = down * settings.SlateRebound * pin;
            return new StrikeResult
            {
                LinearVelocity = linear,
                AngularVelocity = Vector3.Cross(contact, impulse) / inertia,
                OffsetFraction = offset
            };
        }
    }
}
