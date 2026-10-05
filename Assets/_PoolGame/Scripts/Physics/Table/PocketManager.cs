using System.Collections.Generic;
using UnityEngine;
using VTG.Pool.Balls;
using VTG.Pool.Core;

namespace VTG.Pool.Table
{
    /// <summary>
    /// Runs the pocket drop/capture logic for all balls each physics step:
    /// mouth (jaw colliders) -> drop region (slate support removed) -> liner -> capture volume.
    /// </summary>
    public sealed class PocketManager : MonoBehaviour
    {
        [SerializeField] private List<Pocket> pockets = new List<Pocket>(6);
        [SerializeField] private Collider bedCollider;
        [SerializeField] private float surfaceHeight = 0.76f;

        [Tooltip("A dropping ball is captured when its centre is this far below the slate (m).")]
        [SerializeField] private float captureDepth = 0.09f;

        [Tooltip("If a ball leaves the hole having dropped less than this (m), slate support is restored.")]
        [SerializeField] private float rimRecoveryDrop = 0.006f;

        [Tooltip("A ball this far below the slate outside any pocket left the table.")]
        [SerializeField] private float offTableDepth = 0.5f;

        public IReadOnlyList<Pocket> Pockets => pockets;

        public float SurfaceHeight => surfaceHeight;

        public void Configure(List<Pocket> newPockets, Collider bed, float surface)
        {
            pockets = newPockets;
            bedCollider = bed;
            surfaceHeight = surface;
        }

        /// <summary>Called by PoolPhysicsSystem before each PhysX step.</summary>
        public void Step()
        {
            IReadOnlyList<PoolBall> balls = BallRegistry.Balls;
            for (int i = 0; i < balls.Count; i++)
            {
                PoolBall ball = balls[i];
                if (ball.IsPocketed || ball.IsHeld)
                {
                    continue;
                }

                Vector3 position = ball.Body.position;
                Pocket hole = FindPocketUnder(position);
                if (hole != null)
                {
                    if (ball.CurrentPocket != hole)
                    {
                        ball.EnterPocketHole(hole, bedCollider);
                    }
                }
                else if (ball.CurrentPocket != null && position.y > surfaceHeight + ball.Radius - rimRecoveryDrop)
                {
                    ball.ExitPocketHole();
                }

                if (ball.CurrentPocket != null && position.y < surfaceHeight - captureDepth)
                {
                    Capture(ball, ball.CurrentPocket);
                }
                else if (position.y < surfaceHeight - offTableDepth)
                {
                    Debug.LogWarning($"[PocketManager] Ball {ball.BallId} left the table.");
                    Capture(ball, null);
                }
            }
        }

        private static void Capture(PoolBall ball, Pocket pocket)
        {
            ball.MarkPocketed(pocket);
            if (pocket != null)
            {
                pocket.RegisterCapture(ball);
            }

            PoolEvents.RaiseBallPocketed(ball, pocket);
        }

        public void ClearCaptured()
        {
            for (int i = 0; i < pockets.Count; i++)
            {
                if (pockets[i] != null)
                {
                    pockets[i].ClearCaptured();
                }
            }
        }

        private Pocket FindPocketUnder(Vector3 position)
        {
            for (int i = 0; i < pockets.Count; i++)
            {
                Pocket pocket = pockets[i];
                if (pocket != null && pocket.IsOverHole(position))
                {
                    return pocket;
                }
            }

            return null;
        }
    }
}
