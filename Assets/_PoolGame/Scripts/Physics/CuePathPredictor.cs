using System.Collections.Generic;
using UnityEngine;

namespace VTG.Pool.Simulation
{
    /// <summary>Flight settings for <see cref="CuePathPredictor"/> (jump shots).</summary>
    public struct FlightParameters
    {
        /// <summary>Ball-centre height when resting on the cloth (world Y).</summary>
        public float RestHeight;
        public float Gravity;
        public float LandingRestitution;
        public float LandingThreshold;

        public bool Enabled => Gravity > 0f;
    }

    /// <summary>
    /// Predicts the cue ball's path on open cloth by stepping <see cref="ClothFrictionModel"/> from the strike's
    /// initial velocities. Used for massé / swerve / jump guides, where a straight aim line would be wrong.
    /// With <see cref="FlightParameters"/> a jumping ball flies ballistically (no cloth friction), passes over balls
    /// it clears, and bounces on landing. Stops at the first ball contact, at the cushion line or when the ball rests.
    /// Pure (no PhysX).
    /// </summary>
    public static class CuePathPredictor
    {
        public enum End
        {
            Rest,
            Ball,
            Cushion,
            TimeLimit
        }

        /// <param name="halfExtents">Half width (x) and half length (y) of the playing area minus one ball radius, in table space.</param>
        /// <param name="toTable">World to table-space transform (x/z on the cloth).</param>
        /// <param name="obstacles">Centres of other balls on the table.</param>
        /// <param name="points">Receives the path (world space), starting at <paramref name="start"/>.</param>
        public static End Predict(Vector3 start, Vector3 linear, Vector3 angular, in ClothParameters cloth, Vector2 halfExtents,
            Matrix4x4 toTable, IReadOnlyList<Vector3> obstacles, List<Vector3> points, out int hitIndex, float maxTime = 4f, float dt = 1f / 120f)
        {
            return Predict(start, linear, angular, cloth, default, halfExtents, toTable, obstacles, points, out hitIndex, maxTime, dt);
        }

        public static End Predict(Vector3 start, Vector3 linear, Vector3 angular, in ClothParameters cloth, in FlightParameters flight, Vector2 halfExtents,
            Matrix4x4 toTable, IReadOnlyList<Vector3> obstacles, List<Vector3> points, out int hitIndex, float maxTime = 4f, float dt = 1f / 120f)
        {
            points.Clear();
            points.Add(start);
            hitIndex = -1;
            Vector3 position = start;
            float contact = 2f * cloth.Radius;
            int steps = Mathf.CeilToInt(maxTime / dt);
            bool flying = flight.Enabled && linear.y > 0f;
            if (!flight.Enabled)
            {
                linear.y = 0f;
            }

            for (int step = 1; step <= steps; step++)
            {
                ClothMotionPhase phase = ClothMotionPhase.Sliding;
                Vector3 next;
                if (flying)
                {
                    linear.y -= flight.Gravity * dt;
                    next = position + linear * dt;
                    if (next.y <= flight.RestHeight && linear.y < 0f)
                    {
                        next.y = flight.RestHeight;
                        float landing = -linear.y;
                        linear.y = landing > flight.LandingThreshold ? landing * flight.LandingRestitution : 0f;
                        flying = linear.y > 0f;
                    }
                }
                else
                {
                    phase = ClothFrictionModel.Step(ref linear, ref angular, cloth, dt);
                    linear.y = 0f;
                    next = position + new Vector3(linear.x, 0f, linear.z) * dt;
                }

                for (int i = 0; i < obstacles.Count; i++)
                {
                    // 3D distance: a flying ball passes over balls it clears.
                    Vector3 delta = obstacles[i] - next;
                    if (!flight.Enabled)
                    {
                        delta.y = 0f;
                    }

                    if (delta.sqrMagnitude < contact * contact)
                    {
                        hitIndex = i;
                        points.Add(next);
                        return End.Ball;
                    }
                }

                Vector3 local = toTable.MultiplyPoint3x4(next);
                if (Mathf.Abs(local.x) > halfExtents.x || Mathf.Abs(local.z) > halfExtents.y)
                {
                    points.Add(next);
                    return End.Cushion;
                }

                position = next;
                if (step % 3 == 0 || flying)
                {
                    points.Add(position);
                }

                if (!flying && (phase == ClothMotionPhase.Stationary || phase == ClothMotionPhase.Spinning))
                {
                    points.Add(position);
                    return End.Rest;
                }
            }

            points.Add(position);
            return End.TimeLimit;
        }
    }
}
