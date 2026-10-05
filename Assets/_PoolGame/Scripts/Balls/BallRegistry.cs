using System.Collections.Generic;
using UnityEngine;

namespace VTG.Pool.Balls
{
    /// <summary>
    /// Allocation-free registry of enabled balls. Balls register themselves in OnEnable,
    /// so systems never need FindObjectsByType.
    /// </summary>
    public static class BallRegistry
    {
        private static readonly List<PoolBall> balls = new List<PoolBall>(16);

        public static IReadOnlyList<PoolBall> Balls => balls;

        public static int Count => balls.Count;

        public static void Register(PoolBall ball)
        {
            if (ball != null && !balls.Contains(ball))
            {
                balls.Add(ball);
            }
        }

        public static void Unregister(PoolBall ball)
        {
            balls.Remove(ball);
        }

        /// <summary>Returns the first registered cue ball, or null.</summary>
        public static PoolBall FindCueBall()
        {
            for (int i = 0; i < balls.Count; i++)
            {
                if (balls[i].IsCueBall)
                {
                    return balls[i];
                }
            }

            return null;
        }

        public static PoolBall FindById(int id)
        {
            for (int i = 0; i < balls.Count; i++)
            {
                if (balls[i].BallId == id)
                {
                    return balls[i];
                }
            }

            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            balls.Clear();
        }
    }
}
