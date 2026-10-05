using NUnit.Framework;
using UnityEngine;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Table;

namespace VTG.Pool.Tests
{
    /// <summary>
    /// Physics acceptance tests on a runtime-built 9 ft table (MASTER_PROMPT sections 35 and 43).
    /// </summary>
    public sealed class BallPhysicsPlayModeTests
    {
        private PoolTestTable table;

        [SetUp]
        public void SetUp()
        {
            table = new PoolTestTable();
        }

        [TearDown]
        public void TearDown()
        {
            table.Dispose();
            PoolEvents.ClearAll();
        }

        [Test]
        public void StationaryBall_StaysAtRestWithoutDrift()
        {
            PoolBall ball = table.AddBall(1, new Vector2(0.1f, 0.2f));
            Vector3 start = ball.Position;
            table.Step(240);
            Assert.AreEqual(BallState.Stationary, ball.CurrentState);
            Assert.Less(Vector3.Distance(start, ball.Position), 1e-4f, "Resting ball must not drift or vibrate");
        }

        [Test]
        public void StruckBall_EventuallyRestsOnTable()
        {
            PoolBall cue = table.AddBall(0, new Vector2(0f, -0.6f));
            table.Strike(cue, new Vector3(0.3f, 0f, 1f), 0.5f, Vector2.zero);
            float time = table.RunUntilRest();
            Assert.Greater(time, 0f, "Ball never came to rest");
            if (cue.IsPocketed)
            {
                Assert.IsNotNull(cue.CurrentPocket, "Ball left the table instead of being pocketed");
                return;
            }

            Vector2 p = table.ToTable(cue.Position);
            Assert.Less(Mathf.Abs(p.x), table.Table.Geometry.HalfWidth);
            Assert.Less(Mathf.Abs(p.y), table.Table.Geometry.HalfLength);
            Assert.AreEqual(table.Table.SurfaceHeight + table.Radius, cue.Position.y, 0.002f, "Ball must rest on the cloth");
        }

        [Test]
        public void StunShot_TransitionsSlidingToRolling()
        {
            PoolBall cue = table.AddBall(0, new Vector2(0f, -1f));
            table.Strike(cue, Vector3.forward, 0.4f, Vector2.zero);
            float initialSpeed = cue.LinearVelocity.magnitude;
            table.Step(1);
            Assert.AreEqual(BallState.Sliding, cue.CurrentState);

            int guard = 0;
            while (cue.CurrentState == BallState.Sliding && guard++ < 600)
            {
                table.Step(1);
            }

            Assert.AreEqual(BallState.Rolling, cue.CurrentState);
            Assert.AreEqual(initialSpeed * 5f / 7f, cue.LinearVelocity.magnitude, 0.05f);
        }

        [Test]
        public void DrawShot_CueBallComesBack()
        {
            PoolBall cue = table.AddBall(0, new Vector2(0f, -0.35f));
            PoolBall target = table.AddBall(1, new Vector2(0f, 0.1f));
            float startZ = table.ToTable(cue.Position).y;
            table.Strike(cue, Vector3.forward, 0.5f, new Vector2(0f, -0.9f));
            float minZAfterContact = float.MaxValue;
            bool contact = false;
            table.RunUntilRest(perStep: () =>
            {
                contact |= target.LinearVelocity.sqrMagnitude > 0.01f;
                if (contact)
                {
                    minZAfterContact = Mathf.Min(minZAfterContact, table.ToTable(cue.Position).y);
                }
            });

            Assert.IsTrue(contact, "Cue ball must hit the object ball");
            float contactZ = 0.1f - 2f * table.Radius;
            float endZ = table.ToTable(cue.Position).y;
            Assert.Less(endZ, contactZ - 0.15f, $"Draw: cue ball should come back well behind contact (end {endZ:F3}, start {startZ:F3})");
        }

        [Test]
        public void FollowShot_CueBallContinuesForward()
        {
            PoolBall cue = table.AddBall(0, new Vector2(0f, -0.55f));
            table.AddBall(1, new Vector2(0f, 0.2f));
            table.Strike(cue, Vector3.forward, 0.4f, new Vector2(0f, 0.8f));
            float maxZ = float.MinValue;
            table.RunUntilRest(perStep: () => maxZ = Mathf.Max(maxZ, table.ToTable(cue.Position).y));
            float contactZ = 0.2f - 2f * table.Radius;
            Assert.Greater(maxZ, contactZ + 0.4f, "Follow: cue ball should roll on well past the contact point");
        }

        [Test]
        public void StopShot_CueBallStopsNearContact()
        {
            PoolBall cue = table.AddBall(0, new Vector2(0f, -0.25f));
            table.AddBall(1, new Vector2(0f, 0.2f));
            // Short distance, firm stun: almost no rolling develops before contact.
            table.Strike(cue, Vector3.forward, 0.55f, new Vector2(0f, -0.2f));
            float firstRestZ = float.NaN;
            table.RunUntilRest(perStep: () =>
            {
                // The object ball later rebounds off the foot rail and may hit the cue ball again; measure the first stop.
                if (float.IsNaN(firstRestZ) && cue.CurrentState == BallState.Stationary)
                {
                    firstRestZ = table.ToTable(cue.Position).y;
                }
            });
            float contactZ = 0.2f - 2f * table.Radius;
            Assert.AreEqual(contactZ, firstRestZ, 0.06f, "Stop shot: cue ball should stop near the contact point");
        }

        [Test]
        public void BallCollision_ObjectBallDepartsAlongLineOfCentres()
        {
            float r = table.Radius;
            PoolBall cue = table.AddBall(0, new Vector2(0f, -0.4f));
            PoolBall target = table.AddBall(2, new Vector2(r, 0.2f));
            table.Strike(cue, Vector3.forward, 0.35f, Vector2.zero);
            Vector3 departure = Vector3.zero;
            table.RunUntilRest(perStep: () =>
            {
                if (departure == Vector3.zero && target.LinearVelocity.sqrMagnitude > 0.01f)
                {
                    departure = target.LinearVelocity;
                }
            });

            Assert.AreNotEqual(Vector3.zero, departure);
            float cutAngle = Vector3.Angle(Vector3.forward, new Vector3(departure.x, 0f, departure.z));
            Assert.AreEqual(30f, cutAngle, 4f, "Half-ball hit sends the object ball ~30 degrees off the aim line");
        }

        [Test]
        public void RailBounce_RunningEnglishWidensAndReverseNarrowsRebound()
        {
            // Off the right rail the ball rebounds to the left: left english is running, right is reverse.
            float plain = BankExitAngle(Vector2.zero);
            float running = BankExitAngle(new Vector2(-0.8f, 0f));
            float reverse = BankExitAngle(new Vector2(0.8f, 0f));
            Assert.Greater(plain, 5f);
            Assert.Greater(running, plain + 2f, $"Running english should widen the rebound (plain {plain:F1}, running {running:F1})");
            Assert.Less(reverse, plain - 2f, $"Reverse english should narrow the rebound (plain {plain:F1}, reverse {reverse:F1})");
        }

        /// <summary>Shoots at the right long rail and returns the rebound angle from the rail normal (degrees).</summary>
        private float BankExitAngle(Vector2 tip)
        {
            table.Dispose();
            table = new PoolTestTable();
            PoolBall cue = table.AddBall(0, new Vector2(0.2f, -0.6f));
            Vector3 aimPoint = table.Table.BallRestPosition(new Vector2(0.635f, -0.3f), table.Radius);
            Vector3 direction = aimPoint - cue.Position;
            direction.y = 0f;
            table.Strike(cue, direction.normalized, 0.4f, tip);

            bool bounced = false;
            Vector3 exit = Vector3.zero;
            int guard = 0;
            while (guard++ < 1200 && exit == Vector3.zero)
            {
                table.Step(1);
                if (!bounced && cue.LinearVelocity.x < 0f)
                {
                    bounced = true;
                }
                else if (bounced)
                {
                    // Measure a little after the impact so cloth friction has not yet curved the path much.
                    exit = cue.LinearVelocity;
                }
            }

            Assert.IsTrue(bounced, "Ball must rebound from the rail");
            return Vector3.Angle(Vector3.left, new Vector3(exit.x, 0f, exit.z));
        }

        [Test]
        public void NewtonsCradle_ImpulsePassesThroughTouchingBalls()
        {
            float r = table.Radius;
            PoolBall cue = table.AddBall(0, new Vector2(0f, -0.4f));
            PoolBall middle = table.AddBall(1, new Vector2(0f, 0.2f));
            PoolBall last = table.AddBall(2, new Vector2(0f, 0.2f + 2f * r + 0.0001f));
            table.Strike(cue, Vector3.forward, 0.4f, Vector2.zero);
            table.Step(1);
            float speedBefore = 0f;
            int guard = 0;
            while (last.LinearVelocity.sqrMagnitude < 0.0001f && guard++ < 600)
            {
                speedBefore = cue.LinearVelocity.magnitude;
                table.Step(1);
            }

            table.Step(2);
            Assert.Greater(last.LinearVelocity.z, 0.8f * speedBefore, "Last ball takes the impulse");
            Assert.Less(middle.LinearVelocity.magnitude, 0.15f * speedBefore, "Middle ball passes the impulse on");
            Assert.Less(Mathf.Abs(cue.LinearVelocity.z), 0.15f * speedBefore, "Cue ball nearly stops (stun)");
        }

        [Test]
        public void Break_CueBallDoesNotReboundOffTheRack()
        {
            var rack = new Vector3[15];
            RackLayout.GetTrianglePositions(table.Table.FootSpot + Vector3.up * table.Radius, table.Radius, 0.0002f, rack);
            for (int i = 0; i < 15; i++)
            {
                table.AddBall(RackLayout.EightBallOrder[i], table.ToTable(rack[i]));
            }

            PoolBall cue = table.AddBall(0, new Vector2(0f, -0.635f));
            table.Step(10);
            Vector3 direction = rack[0] - cue.Position;
            direction.y = 0f;
            direction.Normalize();
            table.Strike(cue, direction, 1f, Vector2.zero);
            float speedAtContact = 0f;
            bool contact = false;
            PoolEvents.BallHit += info => contact |= info.Involves(cue);
            int guard = 0;
            while (!contact && guard++ < 600)
            {
                speedAtContact = cue.LinearVelocity.magnitude;
                table.Step(1);
            }

            table.Step(6);
            float along = Vector3.Dot(cue.LinearVelocity, direction);
            Assert.Greater(along, -0.25f * speedAtContact, $"Cue ball rebounded off the rack at {along:F2} m/s (contact speed {speedAtContact:F2})");
        }

        [Test]
        public void BallShotIntoCornerPocket_IsPocketed()
        {
            PoolBall ball = table.AddBall(0, new Vector2(0.3f, 0.9f));
            PoolBall pocketed = null;
            PoolEvents.BallPocketed += (b, p) => pocketed = b;
            Pocket corner = table.Table.PocketManager.Pockets[3];
            Vector3 direction = corner.Center - ball.Position;
            direction.y = 0f;
            table.Strike(ball, direction.normalized, 0.35f, Vector2.zero);
            table.RunUntilRest();
            Assert.AreSame(ball, pocketed);
            Assert.IsTrue(ball.IsPocketed);
            Assert.AreEqual(BallState.Pocketed, ball.CurrentState);
            Assert.Less(ball.Position.y, table.Table.SurfaceHeight, "Pocketed ball must have dropped below the slate");
        }

        [Test]
        public void BallAlongRail_PassesSidePocketMouth()
        {
            // A ball hugging the long rail passes the side pocket: its centre never crosses the hole.
            PoolBall ball = table.AddBall(0, new Vector2(0.635f - table.Radius - 0.002f, -0.4f));
            table.Strike(ball, Vector3.forward, 0.3f, Vector2.zero);
            table.RunUntilRest();
            bool inSidePocket = ball.IsPocketed && ball.CurrentPocket != null && ball.CurrentPocket.Kind == PocketKind.Side;
            Assert.IsFalse(inSidePocket, "Rail-hugging ball must not drop into the side pocket");
        }

        [Test]
        public void BreakShot_IsStableAndNoBallLeavesTheTable()
        {
            var rack = new Vector3[15];
            RackLayout.GetTrianglePositions(table.Table.FootSpot + Vector3.up * table.Radius, table.Radius, 0.0002f, rack);
            var balls = new PoolBall[16];
            balls[0] = table.AddBall(0, new Vector2(0.15f, -0.635f));
            for (int i = 0; i < 15; i++)
            {
                int number = RackLayout.EightBallOrder[i];
                balls[number] = table.AddBall(number, table.ToTable(rack[i]));
            }

            // Racked balls must be at rest before the break (no initial overlap explosion).
            table.Step(60);
            for (int n = 1; n <= 15; n++)
            {
                Assert.AreEqual(BallState.Stationary, balls[n].CurrentState, $"Ball {n} moved before the break");
            }

            Vector3 apex = rack[0];
            Vector3 direction = apex - balls[0].Position;
            direction.y = 0f;
            table.Strike(balls[0], direction.normalized, 1f, new Vector2(0f, -0.15f));
            float maxHeight = 0f;
            float time = table.RunUntilRest(90f, () =>
            {
                for (int n = 0; n < balls.Length; n++)
                {
                    if (!balls[n].IsPocketed)
                    {
                        maxHeight = Mathf.Max(maxHeight, balls[n].Position.y - table.Table.SurfaceHeight - table.Radius);
                    }
                }
            });

            Assert.Greater(time, 0f, "Balls did not come to rest after the break");
            Assert.Less(maxHeight, 0.03f, "Balls should not jump unrealistically on the break");
            for (int n = 0; n < balls.Length; n++)
            {
                PoolBall ball = balls[n];
                if (ball.IsPocketed)
                {
                    Assert.IsNotNull(ball.CurrentPocket, $"Ball {n} left the table instead of being pocketed");
                    continue;
                }

                Vector2 p = table.ToTable(ball.Position);
                Assert.Less(Mathf.Abs(p.x), table.Table.Geometry.HalfWidth, $"Ball {n} tunnelled through a cushion");
                Assert.Less(Mathf.Abs(p.y), table.Table.Geometry.HalfLength, $"Ball {n} tunnelled through a cushion");
                for (int m = n + 1; m < balls.Length; m++)
                {
                    if (!balls[m].IsPocketed)
                    {
                        Assert.GreaterOrEqual(Vector3.Distance(ball.Position, balls[m].Position), 2f * table.Radius - 0.001f, $"Balls {n} and {m} overlap after the break");
                    }
                }
            }
        }
    }
}
