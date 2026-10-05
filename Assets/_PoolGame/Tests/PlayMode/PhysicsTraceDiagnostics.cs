using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Cue;

namespace VTG.Pool.Tests
{
    /// <summary>Writes per-step traces to Temp/VTGPoolBridge for tuning. Always passes.</summary>
    [Category("Diagnostics")]
    public sealed class PhysicsTraceDiagnostics
    {
        [TearDown]
        public void TearDown() => PoolEvents.ClearAll();

        [Test]
        public void TraceRailBounce()
        {
            var table = new PoolTestTable();
            var log = new StringBuilder();
            PoolEvents.CushionHit += info => log.AppendLine($"  CUSHION {info.Surface.name} vn={info.NormalSpeed:F3} v'={info.Ball.LinearVelocity:F3} w'={info.Ball.AngularVelocity:F1}");
            PoolBall cue = table.AddBall(0, new Vector2(0.2f, -0.6f));
            Vector3 aim = table.Table.BallRestPosition(new Vector2(0.635f, -0.3f), table.Radius) - cue.Position;
            aim.y = 0f;
            table.Strike(cue, aim.normalized, 0.4f, Vector2.zero);
            for (int i = 0; i < 90; i++)
            {
                table.Step(1);
                log.AppendLine($"{i} {cue.CurrentState} p={table.ToTable(cue.Position):F3} y={cue.Position.y:F4} v={cue.LinearVelocity:F3} w={cue.AngularVelocity:F1}");
            }

            File.WriteAllText("Temp/VTGPoolBridge/trace-rail.txt", log.ToString());
            table.Dispose();
        }

        [Test]
        public void TracePocketShot()
        {
            var table = new PoolTestTable();
            var log = new StringBuilder();
            PoolEvents.CushionHit += info => log.AppendLine($"  CUSHION {info.Surface.name} vn={info.NormalSpeed:F3} point={info.Point:F3}");
            foreach (Collider c in table.Root.GetComponentsInChildren<Collider>())
            {
                if (c.name.StartsWith("Cushion_Short") || c.name.StartsWith("Jaw_Corner_9") || c.name.StartsWith("Jaw_Corner_10") || c.name == "Bed")
                {
                    log.AppendLine($"  COLLIDER {c.name} bounds min={c.bounds.min:F3} max={c.bounds.max:F3} layer={LayerMask.LayerToName(c.gameObject.layer)}");
                }
            }

            PoolEvents.BallPocketed += (b, p) => log.AppendLine($"  POCKETED {b.BallId} in {(p != null ? p.name : "null")}");
            VTG.Pool.Table.Pocket pocket = table.Table.PocketManager.Pockets[5];
            PoolBall ball = table.AddBall(0, new Vector2(0.23f, 0f));
            Vector3 direction = pocket.Center - ball.Position;
            direction.y = 0f;
            log.AppendLine($"pocket {pocket.name} centre={pocket.Center:F3} r={pocket.HoleRadius} ball={ball.Position:F3} layer={LayerMask.LayerToName(ball.gameObject.layer)}");
            table.Strike(ball, direction.normalized, 0.2f, Vector2.zero);
            for (int i = 0; i < 400; i++)
            {
                table.Step(1);
                if (i < 120)
                {
                    log.AppendLine($"{i} {ball.CurrentState} p={ball.Position:F3} v={ball.LinearVelocity:F2} over={(ball.CurrentPocket != null ? ball.CurrentPocket.name : "-")} pocketed={ball.IsPocketed}");
                }
            }

            File.WriteAllText("Temp/VTGPoolBridge/trace-pocket.txt", log.ToString());
            table.Dispose();
        }

        [Test]
        public void TraceBreakCueBall()
        {
            var log = new StringBuilder();
            float[] offsets = { 0f, 0.15f, -0.15f };
            int scratches = 0;
            int runs = 0;
            foreach (float lateral in offsets)
            {
                foreach (float tipY in new[] { 0f, -0.15f })
                {
                    var table = new PoolTestTable();
                    var rack = new Vector3[15];
                    RackLayout.GetTrianglePositions(table.Table.FootSpot + Vector3.up * table.Radius, table.Radius, 0.0002f, rack);
                    for (int i = 0; i < 15; i++)
                    {
                        table.AddBall(RackLayout.EightBallOrder[i], table.ToTable(rack[i]));
                    }

                    PoolBall cue = table.AddBall(0, new Vector2(lateral, -0.635f));
                    table.Step(30);
                    Vector3 dir = rack[0] - cue.Position;
                    dir.y = 0f;
                    bool hit = false;
                    int stepsAfterHit = 0;
                    Vector3 vBefore = Vector3.zero;
                    Vector3 vAfter = Vector3.zero;
                    PoolEvents.BallHit += info => { if (info.Involves(cue)) hit = true; };
                    table.Strike(cue, dir.normalized, 1f, new Vector2(0f, tipY));
                    for (int i = 0; i < 1200 && stepsAfterHit < 8; i++)
                    {
                        Vector3 before = cue.LinearVelocity;
                        table.Step(1);
                        if (hit)
                        {
                            if (stepsAfterHit == 0) vBefore = before;
                            stepsAfterHit++;
                            vAfter = cue.LinearVelocity;
                        }
                    }

                    table.RunUntilRest(60f);
                    runs++;
                    if (cue.IsPocketed) scratches++;
                    log.AppendLine($"lateral={lateral:F2} tipY={tipY:F2}: v at contact={vBefore.magnitude:F2} along={Vector3.Dot(vBefore, dir.normalized):F2} -> v 8 steps later={vAfter:F2} (along {Vector3.Dot(vAfter, dir.normalized):F2}) scratch={cue.IsPocketed} rest={table.ToTable(cue.Position):F2}");
                    PoolEvents.ClearAll();
                    table.Dispose();
                }
            }

            log.AppendLine($"scratches {scratches}/{runs}");
            File.WriteAllText("Temp/VTGPoolBridge/trace-break.txt", log.ToString());
        }

        [Test]
        public void TraceBreakSpread()
        {
            var log = new StringBuilder();
            foreach (float cueSpeed in new[] { 8f })
            {
                foreach (float jitter in new[] { 0f, 0.0003f })
                {
                    for (int seed = 1; seed <= 6; seed++)
                    {
                        RunBreak(log, $"cue {cueSpeed} jitter {jitter * 1000f:F1}mm seed {seed}", 0.93f, 0.0002f, jitter, seed, cueSpeed);
                    }
                }
            }
            File.WriteAllText("Temp/VTGPoolBridge/trace-break-spread.txt", log.ToString());
        }

        private static void RunBreak(StringBuilder log, string label, float restitution, float gap, float jitter, int seed = 7, float cueSpeed = 7f)
        {
            var table = new PoolTestTable();
            table.Profile.ballRestitution = restitution;
            table.StrikeProfile.maxCueSpeed = cueSpeed;
            var rack = new Vector3[15];
            RackLayout.GetTrianglePositions(table.Table.FootSpot + Vector3.up * table.Radius, table.Radius, gap, rack);
            var random = new System.Random(seed);
            var balls = new PoolBall[15];
            RackLayout.Jitter(rack, 15, table.Radius, jitter, 0.00005f, random);
            for (int i = 0; i < 15; i++)
            {
                balls[i] = table.AddBall(RackLayout.EightBallOrder[i], table.ToTable(rack[i]));
            }

            PoolBall cue = table.AddBall(0, new Vector2(0f, -0.635f));
            table.Step(30);
            Vector3 dir = rack[0] - cue.Position;
            dir.y = 0f;
            int cushionHits = 0;
            PoolEvents.CushionHit += info => { if (!info.Ball.IsCueBall) cushionHits++; };
            table.Strike(cue, dir.normalized, 1f, Vector2.zero);
            float startSpeed = cue.LinearVelocity.magnitude;
            var collisions = new StringBuilder();
            int budget0 = table.Physics.CollisionBudgetExhausted;
            float energyAfter = 0f;
            for (int step = 0; step < 120; step++)
            {
                table.Step(1);
                if (table.Physics.LastStepBallCollisions > 0 && collisions.Length < 300)
                {
                    collisions.Append(table.Physics.LastStepBallCollisions).Append(' ');
                }

                if (step == 24)
                {
                    foreach (PoolBall b in balls) energyAfter += b.LinearVelocity.sqrMagnitude;
                    energyAfter += cue.LinearVelocity.sqrMagnitude;
                }
            }

            table.RunUntilRest(60f);
            float displacement = 0f;
            int far = 0;
            int pocketed = 0;
            for (int i = 0; i < 15; i++)
            {
                float moved = Vector3.Distance(balls[i].Position, rack[i]);
                displacement += moved;
                if (moved > 0.3f) far++;
                if (balls[i].IsPocketed) pocketed++;
            }

            log.AppendLine($"[{label}] start {startSpeed:F2} m/s, sum v^2 at 0.2s = {energyAfter:F1} of {startSpeed * startSpeed:F1}, avg moved {displacement / 15f:F2} m, moved>30cm {far}/15, pocketed {pocketed}, cushion hits {cushionHits}, budget exhausted {table.Physics.CollisionBudgetExhausted - budget0}");

            PoolEvents.ClearAll();
            table.Dispose();
        }

        [Test]
        public void JumpShot_FliesOverABallAndLands()
        {
            var table = new PoolTestTable();
            const float power = 0.85f;
            const float elevation = 35f;
            var shot = new ShotParameters(Vector3.forward, power, Vector2.zero, elevation);
            StrikeResult launch = CueStrikeModel.Compute(shot, table.StrikeProfile.EvaluateCueSpeed(power), table.Profile.ballMass, table.Profile.ballRadius, table.StrikeProfile.ToSettings());
            float apex = launch.LinearVelocity.y / 9.81f * launch.LinearVelocity.z;
            PoolBall cue = table.AddBall(0, new Vector2(0f, -1.0f));
            // A blocker right where the cue ball is at the top of its arc.
            PoolBall blocker = table.AddBall(1, new Vector2(0f, -1.0f + apex));
            table.Step(10);
            bool touched = false;
            bool landed = false;
            PoolEvents.BallHit += info => { if (info.Involves(blocker) && !landed) touched = true; };
            float rest = cue.Position.y;
            table.Strike(cue, Vector3.forward, power, Vector2.zero, elevation);
            float peak = 0f;
            int bounces = 0;
            float previousVy = 0f;
            for (int i = 0; i < 360; i++)
            {
                table.Step(1);
                peak = Mathf.Max(peak, cue.Position.y - rest);
                float vy = cue.LinearVelocity.y;
                if (previousVy < -0.2f && vy > 0.1f)
                {
                    bounces++;
                    landed = true; // later contacts (after a rail) are not part of the jump
                }
                previousVy = vy;
            }

            File.WriteAllText("Temp/VTGPoolBridge/trace-jump.txt", $"launch={launch.LinearVelocity:F2} apex at {apex:F2} m peak={peak:F3} m bounces={bounces} touched={touched} end={table.ToTable(cue.Position):F2} y={cue.Position.y - rest:F4} pocketed={cue.IsPocketed}");
            Assert.Greater(peak, 2f * table.Radius, "The cue ball jumps higher than a ball");
            Assert.Less(peak, 0.4f, "...but not absurdly high");
            Assert.IsFalse(touched, "It clears the blocking ball in flight");
            Assert.GreaterOrEqual(bounces, 1, "It bounces on landing");
            Assert.AreEqual(0f, cue.Position.y - rest, 0.003f, "It ends up back on the cloth");
            PoolEvents.ClearAll();
            table.Dispose();
        }

        [Test]
        public void HardJumpIntoTheRail_SendsTheBallOffTheTable_LevelShotDoesNot()
        {
            var log = new StringBuilder();
            foreach (float elevation in new[] { 0f, 30f })
            {
                var table = new PoolTestTable();
                PoolBall cue = table.AddBall(0, new Vector2(0.45f, -0.3f));
                table.Step(10);
                bool offTable = false;
                PoolEvents.BallPocketed += (ball, pocket) => { if (ball == cue && pocket == null) offTable = true; };
                table.Strike(cue, Vector3.right, 1f, Vector2.zero, elevation);
                float peak = 0f;
                float rest = cue.Position.y;
                for (int i = 0; i < 600 && !offTable; i++)
                {
                    table.Step(1);
                    peak = Mathf.Max(peak, cue.Position.y - rest);
                }

                log.AppendLine($"elevation {elevation}: peak {peak:F3} m, off table {offTable}, pocketed {cue.IsPocketed}, end {cue.Position:F2}");
                if (elevation == 0f)
                {
                    Assert.IsFalse(offTable, "A level full-power shot into the rail stays on the table");
                }
                else
                {
                    Assert.IsTrue(offTable, "A full-power jump into the near rail flies off the table");
                }

                PoolEvents.ClearAll();
                table.Dispose();
            }

            File.WriteAllText("Temp/VTGPoolBridge/trace-offtable.txt", log.ToString());
        }

        [Test]
        public void TraceStopShot()
        {
            var table = new PoolTestTable();
            var log = new StringBuilder();
            PoolEvents.BallHit += info => log.AppendLine($"  BALLHIT impulse={info.Impulse:F3}");
            PoolBall cue = table.AddBall(0, new Vector2(0f, -0.25f));
            PoolBall target = table.AddBall(1, new Vector2(0f, 0.2f));
            table.Strike(cue, Vector3.forward, 0.55f, new Vector2(0f, -0.2f));
            log.AppendLine($"start v={cue.LinearVelocity:F3} w={cue.AngularVelocity:F1}");
            for (int i = 0; i < 120; i++)
            {
                table.Step(1);
                log.AppendLine($"{i} {cue.CurrentState} z={table.ToTable(cue.Position).y:F3} v={cue.LinearVelocity:F3} w={cue.AngularVelocity:F1} | obj v={target.LinearVelocity:F3}");
            }

            File.WriteAllText("Temp/VTGPoolBridge/trace-stop.txt", log.ToString());
            table.Dispose();
        }
    }
}
