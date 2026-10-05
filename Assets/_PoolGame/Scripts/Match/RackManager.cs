using System.Collections.Generic;
using UnityEngine;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Rules;
using VTG.Pool.Table;

namespace VTG.Pool.Match
{
    /// <summary>
    /// Places balls for a game: procedural racks (triangle / diamond) from the ball diameter, the cue
    /// ball on the head spot, and re-spotting balls on the foot spot / long string.
    /// </summary>
    public sealed class RackManager : MonoBehaviour
    {
        [SerializeField] private TableBuilder table;
        [SerializeField] private List<PoolBall> balls = new List<PoolBall>(16);
        [SerializeField, Tooltip("Gap between racked balls (m). Tiny gap avoids PhysX pre-load in the rack.")]
        private float rackGap = 0.0002f;

        [SerializeField, Tooltip("Random placement error of each racked ball (m). A perfect lattice breaks unrealistically (only the back corners fly).")]
        private float rackJitter = 0.0003f;

        [SerializeField, Tooltip("0 = random seed each rack; otherwise racks are reproducible.")]
        private int seed;

        private readonly Vector3[] slots = new Vector3[15];
        private System.Random random;

        public TableBuilder Table => table;

        public IReadOnlyList<PoolBall> Balls => balls;

        public void Configure(TableBuilder newTable, List<PoolBall> newBalls)
        {
            table = newTable;
            balls = newBalls;
        }

        public PoolBall GetBall(int number)
        {
            for (int i = 0; i < balls.Count; i++)
            {
                if (balls[i] != null && balls[i].BallId == number)
                {
                    return balls[i];
                }
            }

            return null;
        }

        public PoolBall CueBall => GetBall(0);

        /// <summary>Racks every ball used by the game, disables the others and puts the cue ball on the head spot.</summary>
        public void Rack(GameMode mode)
        {
            random ??= seed == 0 ? new System.Random() : new System.Random(seed);
            if (table.PocketManager != null)
            {
                table.PocketManager.ClearCaptured();
            }

            float radius = BallRadius();
            Vector3 apex = table.FootSpot + Vector3.up * radius;
            int[] order;
            if (mode == GameMode.NineBall)
            {
                RackLayout.GetDiamondPositions(apex, radius, rackGap, slots);
                order = RackLayout.BuildNineBallOrder(random);
                RackLayout.Jitter(slots, 9, radius, rackJitter, 0.00005f, random);
            }
            else
            {
                RackLayout.GetTrianglePositions(apex, radius, rackGap, slots);
                order = RackLayout.BuildEightBallOrder(random);
                RackLayout.Jitter(slots, 15, radius, rackJitter, 0.00005f, random);
            }

            for (int i = 0; i < balls.Count; i++)
            {
                PoolBall ball = balls[i];
                if (ball == null)
                {
                    continue;
                }

                if (ball.IsCueBall)
                {
                    ball.gameObject.SetActive(true);
                    ball.PlaceAt(table.HeadSpot + Vector3.up * radius);
                    continue;
                }

                int slot = System.Array.IndexOf(order, ball.BallId);
                ball.gameObject.SetActive(slot >= 0);
                if (slot >= 0)
                {
                    ball.PlaceAt(slots[slot]);
                }
            }
        }

        /// <summary>
        /// Spots a ball on the foot spot; if occupied, as close as possible behind it on the long string
        /// (toward the foot rail), otherwise in front of it.
        /// </summary>
        public bool Respot(int number)
        {
            PoolBall ball = GetBall(number);
            if (ball == null)
            {
                return false;
            }

            ball.gameObject.SetActive(true);
            float radius = BallRadius();
            Vector3 spot = table.FootSpot + Vector3.up * radius;
            float halfLength = table.Geometry.HalfLength - radius;
            const float step = 0.002f;
            for (int direction = 1; direction >= -1; direction -= 2)
            {
                for (float offset = 0f; offset < table.Geometry.playLength; offset += step)
                {
                    Vector3 candidate = spot + Vector3.forward * (offset * direction);
                    float along = table.transform.InverseTransformPoint(candidate).z;
                    if (Mathf.Abs(along) > halfLength)
                    {
                        break;
                    }

                    if (IsFree(candidate, radius, ball))
                    {
                        ball.PlaceAt(candidate);
                        return true;
                    }
                }
            }

            Debug.LogWarning($"[RackManager] Could not find a free spot for ball {number}.");
            return false;
        }

        private static bool IsFree(Vector3 position, float radius, PoolBall ignore)
        {
            var balls = BallRegistry.Balls;
            float minDistance = 2f * radius + 0.0005f;
            for (int i = 0; i < balls.Count; i++)
            {
                PoolBall other = balls[i];
                if (other == ignore || other.IsPocketed)
                {
                    continue;
                }

                if ((other.Position - position).sqrMagnitude < minDistance * minDistance)
                {
                    return false;
                }
            }

            return true;
        }

        private float BallRadius()
        {
            PoolBall cue = CueBall;
            return cue != null ? cue.Radius : PoolConstants.BallRadius;
        }
    }
}
