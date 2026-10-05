using UnityEngine;

namespace VTG.Pool.Aiming
{
    /// <summary>
    /// Pure aiming geometry on the table plane. The ghost ball is where the cue ball's centre is at
    /// first contact: the cue-ball path (a ray) meets a sphere of radius 2R around the object ball.
    /// </summary>
    public static class GhostBallMath
    {
        /// <summary>Ray/sphere intersection (3D). Returns the nearest non-negative hit distance.</summary>
        public static bool RaySphere(Vector3 origin, Vector3 direction, Vector3 center, float radius, out float distance)
        {
            Vector3 toCenter = origin - center;
            float b = Vector3.Dot(toCenter, direction);
            float c = toCenter.sqrMagnitude - radius * radius;
            if (c > 0f && b > 0f)
            {
                distance = 0f;
                return false;
            }

            float discriminant = b * b - c;
            if (discriminant < 0f)
            {
                distance = 0f;
                return false;
            }

            distance = Mathf.Max(0f, -b - Mathf.Sqrt(discriminant));
            return true;
        }

        /// <summary>
        /// Distance the cue ball travels along <paramref name="aimDirection"/> before touching the
        /// object ball. Computed on the horizontal plane.
        /// </summary>
        public static bool TryGetContactDistance(Vector3 cueBall, Vector3 aimDirection, Vector3 objectBall, float ballRadius, out float distance)
        {
            Vector3 origin = Flatten(cueBall);
            Vector3 direction = Flatten(aimDirection);
            if (direction.sqrMagnitude < 1e-10f)
            {
                distance = 0f;
                return false;
            }

            return RaySphere(origin, direction.normalized, Flatten(objectBall), 2f * ballRadius, out distance);
        }

        /// <summary>Ghost ball centre for a given aim, or false if the aim misses the object ball.</summary>
        public static bool TryGetGhostBall(Vector3 cueBall, Vector3 aimDirection, Vector3 objectBall, float ballRadius, out Vector3 ghostBall)
        {
            if (TryGetContactDistance(cueBall, aimDirection, objectBall, ballRadius, out float distance))
            {
                Vector3 direction = Flatten(aimDirection).normalized;
                ghostBall = cueBall + direction * distance;
                return true;
            }

            ghostBall = cueBall;
            return false;
        }

        /// <summary>Ghost ball position needed to send the object ball toward a target point.</summary>
        public static Vector3 GhostBallForTarget(Vector3 objectBall, Vector3 target, float ballRadius)
        {
            Vector3 direction = Flatten(target - objectBall).normalized;
            return objectBall - direction * (2f * ballRadius);
        }

        /// <summary>Object-ball departure direction (along the line of centres at contact).</summary>
        public static Vector3 ObjectBallDirection(Vector3 ghostBall, Vector3 objectBall)
        {
            return Flatten(objectBall - ghostBall).normalized;
        }

        /// <summary>
        /// Stun-shot cue-ball direction after contact (90-degree rule): the aim component tangent to
        /// the line of centres. Returns zero for a full-ball hit.
        /// </summary>
        public static Vector3 CueBallTangentDirection(Vector3 aimDirection, Vector3 objectBallDirection)
        {
            Vector3 aim = Flatten(aimDirection).normalized;
            Vector3 tangent = aim - Vector3.Dot(aim, objectBallDirection) * objectBallDirection;
            return tangent.sqrMagnitude > 1e-8f ? tangent.normalized : Vector3.zero;
        }

        /// <summary>Cut angle in degrees between the aim line and the object-ball direction.</summary>
        public static float CutAngleDegrees(Vector3 aimDirection, Vector3 objectBallDirection)
        {
            return Vector3.Angle(Flatten(aimDirection), Flatten(objectBallDirection));
        }

        public static Vector3 Flatten(Vector3 value)
        {
            return new Vector3(value.x, 0f, value.z);
        }
    }
}
