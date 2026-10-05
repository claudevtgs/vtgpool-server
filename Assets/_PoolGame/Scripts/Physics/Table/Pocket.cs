using System.Collections.Generic;
using UnityEngine;
using VTG.Pool.Balls;

namespace VTG.Pool.Table
{
    public enum PocketKind
    {
        Corner,
        Side
    }

    /// <summary>
    /// One pocket: a hole in the slate (drop region) behind the jaw colliders, a liner below the slate
    /// and a capture volume. A ball whose centre passes over the hole loses slate support and falls
    /// physically; it is captured only when it has dropped below the capture depth.
    /// </summary>
    public sealed class Pocket : MonoBehaviour
    {
        [SerializeField] private PocketKind kind = PocketKind.Corner;
        [SerializeField] private int pocketIndex;
        [SerializeField, Tooltip("Radius of the hole in the slate (m).")]
        private float holeRadius = 0.068f;

        private readonly List<PoolBall> captured = new List<PoolBall>(8);

        public PocketKind Kind => kind;

        public int PocketIndex => pocketIndex;

        public float HoleRadius => holeRadius;

        /// <summary>Hole centre on the slate surface.</summary>
        public Vector3 Center => transform.position;

        public IReadOnlyList<PoolBall> CapturedBalls => captured;

        public void Configure(PocketKind newKind, int index, float radius)
        {
            kind = newKind;
            pocketIndex = index;
            holeRadius = radius;
        }

        /// <summary>True if a point lies over the hole (horizontal test).</summary>
        public bool IsOverHole(Vector3 point)
        {
            Vector3 center = transform.position;
            float dx = point.x - center.x;
            float dz = point.z - center.z;
            return dx * dx + dz * dz < holeRadius * holeRadius;
        }

        internal void RegisterCapture(PoolBall ball)
        {
            if (!captured.Contains(ball))
            {
                captured.Add(ball);
            }
        }

        public void ClearCaptured()
        {
            captured.Clear();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.8f);
            const int segments = 24;
            Vector3 center = transform.position;
            Vector3 previous = center + new Vector3(holeRadius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * holeRadius, 0f, Mathf.Sin(angle) * holeRadius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
