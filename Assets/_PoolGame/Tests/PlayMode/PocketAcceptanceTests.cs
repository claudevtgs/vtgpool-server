using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Table;

namespace VTG.Pool.Tests
{
    /// <summary>
    /// Pocket acceptance matrix: shots at the pocket centre from several approach angles, distances and
    /// powers. Shots within 20 degrees of the pocket axis must always drop (steeper, faster corner shots may rattle out); the full matrix is written to
    /// Temp/VTGPoolBridge/pocket-matrix.txt for tuning jaw geometry.
    /// </summary>
    public sealed class PocketAcceptanceTests
    {
        private static readonly float[] Powers = { 0.2f, 0.45f, 0.8f };
        private static readonly float[] Distances = { 0.35f, 0.8f };

        [TearDown]
        public void TearDown() => PoolEvents.ClearAll();

        [Test]
        public void ShotsWithinTwentyDegreesOfPocketAxis_AlwaysDrop()
        {
            var report = new StringBuilder();
            int failures = 0;
            failures += RunMatrix(report, PocketKind.Corner, new[] { 0f, 15f, -15f, 30f, -30f }, true);
            failures += RunMatrix(report, PocketKind.Side, new[] { 0f, 20f, -20f, 35f, -35f }, true);
            Directory.CreateDirectory("Temp/VTGPoolBridge");
            File.WriteAllText("Temp/VTGPoolBridge/pocket-matrix.txt", report.ToString());
            Assert.AreEqual(0, failures, "Straight-on pocket shots rejected:\n" + report);
        }

        /// <summary>Returns the number of rejected shots within 20 degrees of the pocket axis.</summary>
        private static int RunMatrix(StringBuilder report, PocketKind kind, float[] angles, bool requireStraight)
        {
            int failures = 0;
            foreach (float angle in angles)
            {
                foreach (float distance in Distances)
                {
                    foreach (float power in Powers)
                    {
                        bool dropped = Shoot(kind, angle, distance, power, out string detail);
                        report.AppendLine($"{kind,-6} angle {angle,5:F0} dist {distance:F2} power {power:F2}: {(dropped ? "IN " : "OUT")} {detail}");
                        if (!dropped && requireStraight && Mathf.Abs(angle) <= 20f)
                        {
                            failures++;
                        }
                    }
                }
            }

            return failures;
        }

        private static bool Shoot(PocketKind kind, float angle, float distance, float power, out string detail)
        {
            var table = new PoolTestTable();
            try
            {
                Pocket pocket = null;
                foreach (Pocket p in table.Table.PocketManager.Pockets)
                {
                    Vector3 local = table.Root.transform.InverseTransformPoint(p.Center);
                    if (p.Kind == kind && local.x > 0f && local.z >= 0f)
                    {
                        pocket = p;
                    }
                }

                Vector3 localCenter = table.Root.transform.InverseTransformPoint(pocket.Center);
                Vector3 axis = kind == PocketKind.Side ? Vector3.right : new Vector3(1f, 0f, 1f).normalized;

                // Approach direction rotated away from the pocket axis toward the table interior.
                Vector3 approach = Quaternion.AngleAxis(angle, Vector3.up) * axis;
                Vector2 mouth = new Vector2(localCenter.x, localCenter.z) - new Vector2(axis.x, axis.z) * 0.1f;
                Vector2 start = mouth - new Vector2(approach.x, approach.z) * distance;
                float r = table.Radius;
                start.x = Mathf.Clamp(start.x, -table.Table.Geometry.HalfWidth + r * 1.5f, table.Table.Geometry.HalfWidth - r * 1.5f);
                start.y = Mathf.Clamp(start.y, -table.Table.Geometry.HalfLength + r * 1.5f, table.Table.Geometry.HalfLength - r * 1.5f);

                PoolBall ball = table.AddBall(1, start);
                Vector3 direction = pocket.Center - ball.Position;
                direction.y = 0f;
                int cushions = 0;
                PoolEvents.CushionHit += info => cushions++;
                table.Strike(ball, direction.normalized, power, Vector2.zero);
                table.RunUntilRest(30f);
                PoolEvents.ClearAll();
                detail = $"start=({start.x:F2},{start.y:F2}) cushions={cushions}";
                return ball.IsPocketed && ball.CurrentPocket == pocket;
            }
            finally
            {
                table.Dispose();
            }
        }
    }
}
