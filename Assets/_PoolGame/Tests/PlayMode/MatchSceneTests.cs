using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Match;
using VTG.Pool.Rules;
using VTG.Pool.Table;
using VTG.Pool.UI;

namespace VTG.Pool.Tests
{
    /// <summary>End-to-end 8-ball flow in 02_Match: rack, ball in hand, break, foul, turn switching.</summary>
    public sealed class MatchSceneTests
    {
        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            PoolEvents.ClearAll();
            yield return SceneTestUtility.UnloadAllScenes();
        }

        private const string SceneName = "02_Match";

        private MatchManager match;
        private TurnManager turns;
        private ShotController shot;
        private CueBallPlacementController placement;
        private ShotRecord finished;
        private float previousTimeScale;
        private readonly StringBuilder report = new StringBuilder();

        private IEnumerator LoadScene()
        {
            if (!Application.CanStreamedLevelBeLoaded(SceneName))
            {
                Assert.Ignore("02_Match is not in the build settings (run VTG Pool/Setup).");
            }

            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            match = Object.FindAnyObjectByType<MatchManager>();
            turns = Object.FindAnyObjectByType<TurnManager>();
            shot = Object.FindAnyObjectByType<ShotController>();
            placement = Object.FindAnyObjectByType<CueBallPlacementController>();
            Assert.IsNotNull(match);
            Assert.IsNotNull(turns);
            Assert.IsNotNull(shot);
            Assert.IsNotNull(placement);
            PoolEvents.BallsStopped += record => finished = record;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 4f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            Directory.CreateDirectory("Temp/VTGPoolBridge");
            File.WriteAllText("Temp/VTGPoolBridge/match-flow.txt", report.ToString());
        }

        private IEnumerator Play(ShotParameters parameters)
        {
            finished = null;
            Assert.IsTrue(shot.ExecuteShot(parameters), "Shot was not accepted");
            float timeout = Time.realtimeSinceStartup + 40f;
            while (finished == null && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.IsNotNull(finished, "Balls never came to rest");
            yield return null;
            report.AppendLine($"{finished} -> {turns.State.LastOutcome}");
        }

        [UnityTest]
        public IEnumerator EightBallFlow_RackBreakAndScratch()
        {
            yield return LoadScene();
            MatchState state = turns.State;

            // Rack ---------------------------------------------------------------
            Assert.AreEqual(15, state.BallsOnTable.Count);
            Assert.IsTrue(state.IsBreakShot);
            Assert.AreEqual(BallInHandMode.BehindHeadString, placement.Mode);
            Assert.IsTrue(placement.IsPlacing, "Breaker starts with ball in hand behind the head string");
            Assert.IsNotNull(Object.FindAnyObjectByType<PoolHud>(), "HUD missing");
            var balls = BallRegistry.Balls;
            for (int i = 0; i < balls.Count; i++)
            {
                for (int j = i + 1; j < balls.Count; j++)
                {
                    Assert.GreaterOrEqual(Vector3.Distance(balls[i].Position, balls[j].Position), 2f * balls[i].Radius - 1e-4f, "Rack overlaps");
                }
            }

            // Ball in hand: kitchen limit is enforced, then place on the head spot.
            TableBuilder table = Object.FindAnyObjectByType<TableBuilder>();
            float radius = shot.CueBall.Radius;
            Assert.IsFalse(placement.IsValid(table.SurfaceCenter + Vector3.up * radius), "Cue ball may not be placed past the head string on the break");
            Assert.IsTrue(placement.TryConfirm());
            Assert.IsFalse(shot.ShotInputBlocked);

            // Break --------------------------------------------------------------
            int breaker = state.CurrentPlayerIndex;
            PoolBall apexBall = FindApexBall(table);
            Vector3 aim = apexBall.Position - shot.CueBall.Position;
            aim.y = 0f;
            yield return Play(new ShotParameters(aim.normalized, 1f, new Vector2(0f, -0.15f)));
            Assert.AreEqual(1, state.ShotsPlayed);
            ShotOutcome breakOutcome = state.LastOutcome;
            Assert.IsTrue(breakOutcome.WasBreak);
            if (breakOutcome.Rerack)
            {
                Assert.Inconclusive("Break was illegal and re-racked; flow covered by rule tests.");
            }

            Assert.IsFalse(state.IsBreakShot);
            int expectedPlayer = breakOutcome.TurnContinues ? breaker : 1 - breaker;
            Assert.AreEqual(expectedPlayer, state.CurrentPlayerIndex);
            Assert.AreEqual(15 - finished.BallsPocketed.Count + breakOutcome.RespotBalls.Count, state.BallsOnTable.Count);

            // Scratch ------------------------------------------------------------
            yield return WaitForAiming();
            if (placement.IsPlacing)
            {
                Assert.IsTrue(placement.TryConfirm());
            }

            int shooter = state.CurrentPlayerIndex;
            Vector3 cue = shot.CueBall.Position;
            Pocket target = FindOpenPocket(table, cue, radius);
            if (target == null)
            {
                Assert.Inconclusive("No clear path to a pocket for the scratch test.");
            }

            Vector3 toPocket = target.Center - cue;
            toPocket.y = 0f;
            yield return Play(new ShotParameters(toPocket.normalized, 0.45f, Vector2.zero));
            ShotOutcome scratch = state.LastOutcome;
            Assert.IsTrue(finished.CueBallPocketed, "Cue ball should have been pocketed");
            Assert.IsTrue(scratch.Foul);
            Assert.AreEqual(1 - shooter, state.CurrentPlayerIndex, "Turn passes after a foul");
            Assert.AreEqual(1, state.Players[shooter].Fouls);
            yield return WaitForAiming();
            Assert.AreEqual(BallInHandMode.Anywhere, placement.Mode);
            Assert.IsTrue(placement.IsPlacing);
            Assert.IsFalse(shot.CueBall.IsPocketed, "Cue ball is back on the table for placement");
            Assert.IsTrue(placement.TryConfirm());
        }

        [UnityTest]
        public IEnumerator GameOver_StopsPlayShowsResultAndRestartReracks()
        {
            yield return LoadScene();
            MatchState state = turns.State;
            int winner = 1;
            var outcome = new ShotOutcome { GameOver = true, WinnerIndex = winner, Message = "Test win" };
            turns.Apply(outcome, new ShotRecord());
            yield return null;

            Assert.IsTrue(state.IsGameOver);
            Assert.AreEqual(ShotPhase.GameOver, shot.Phase);
            Assert.IsFalse(shot.ExecuteShot(new ShotParameters(Vector3.forward, 0.5f, Vector2.zero)), "No shots after game over");
            float wait = Time.realtimeSinceStartup + 6f;
            while (GameObject.Find("GameOverPanel") == null && Time.realtimeSinceStartup < wait)
            {
                yield return null;
            }

            GameObject panel = GameObject.Find("GameOverPanel");
            Assert.IsNotNull(panel, "Game-over panel should be visible after the victory moment");
            Assert.IsTrue(panel.activeInHierarchy);

            match.StartMatch();
            yield return null;
            MatchState fresh = turns.State;
            Assert.AreNotSame(state, fresh);
            Assert.IsFalse(fresh.IsGameOver);
            Assert.AreEqual(15, fresh.BallsOnTable.Count);
            Assert.AreEqual(ShotPhase.Aiming, shot.Phase);
            Assert.AreEqual(1, fresh.BreakingPlayerIndex, "Break alternates between matches");
            Assert.IsFalse(GameObject.Find("GameOverPanel") != null && GameObject.Find("GameOverPanel").activeInHierarchy);
        }

        [UnityTest]
        public IEnumerator NineBall_DiamondRackAndBreakFlow()
        {
            yield return LoadScene();
            match.StartMatch(GameMode.NineBall);
            yield return null;
            MatchState state = turns.State;
            TableBuilder table = Object.FindAnyObjectByType<TableBuilder>();

            Assert.AreEqual(GameMode.NineBall, state.Mode);
            Assert.AreEqual(9, state.BallsOnTable.Count);
            Assert.AreEqual(10, BallRegistry.Count, "Cue ball + 9 object balls active; 10-15 disabled");
            PoolBall one = BallRegistry.FindById(1);
            PoolBall nine = BallRegistry.FindById(9);
            Assert.Less(Vector3.Distance(one.Position, table.FootSpot + Vector3.up * one.Radius), 0.001f, "1-ball on the foot spot (apex)");
            Vector3 local = table.transform.InverseTransformPoint(nine.Position);
            Assert.AreEqual(0f, local.x, 0.001f, "9-ball on the long string");
            Assert.Greater(local.z, table.transform.InverseTransformPoint(one.Position).z + 0.05f, "9-ball in the middle of the diamond");
            Assert.AreEqual("Break (1-ball first)", turns.Rules.DescribeTarget(state));

            Assert.IsTrue(placement.TryConfirm());
            Vector3 aim = one.Position - shot.CueBall.Position;
            aim.y = 0f;
            yield return Play(new ShotParameters(aim.normalized, 1f, Vector2.zero));
            ShotOutcome outcome = state.LastOutcome;
            Assert.IsTrue(outcome.WasBreak);
            Assert.AreEqual(1, finished.FirstObjectBallHit, "Break contacts the 1-ball first");
            if (!outcome.GameOver)
            {
                Assert.IsFalse(state.IsBreakShot);
                int expectedBalls = 9 - finished.BallsPocketed.Count - finished.BallsOffTable.Count + outcome.RespotBalls.Count;
                Assert.AreEqual(expectedBalls, state.BallsOnTable.Count);
                if (outcome.Foul)
                {
                    yield return WaitForAiming();
                    Assert.AreEqual(BallInHandMode.Anywhere, placement.Mode, "Any 9-ball foul gives ball in hand anywhere");
                }
            }
        }

        private IEnumerator WaitForAiming()
        {
            float timeout = Time.realtimeSinceStartup + 5f;
            while (shot.Phase != ShotPhase.Aiming && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            yield return null;
        }

        private static PoolBall FindApexBall(TableBuilder table)
        {
            PoolBall best = null;
            float bestDistance = float.MaxValue;
            var balls = BallRegistry.Balls;
            for (int i = 0; i < balls.Count; i++)
            {
                if (balls[i].IsCueBall)
                {
                    continue;
                }

                float distance = Vector3.Distance(balls[i].Position, table.FootSpot);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = balls[i];
                }
            }

            return best;
        }

        private static Pocket FindOpenPocket(TableBuilder table, Vector3 cue, float radius)
        {
            int mask = 1 << PoolLayers.Ball;
            Pocket best = null;
            float bestDistance = float.MaxValue;
            foreach (Pocket pocket in table.PocketManager.Pockets)
            {
                Vector3 target = new Vector3(pocket.Center.x, cue.y, pocket.Center.z);
                Vector3 delta = target - cue;
                float distance = delta.magnitude;
                Vector3 axis = table.transform.InverseTransformPoint(pocket.Center);
                axis = pocket.Kind == PocketKind.Side ? new Vector3(Mathf.Sign(axis.x), 0f, 0f) : new Vector3(Mathf.Sign(axis.x), 0f, Mathf.Sign(axis.z)).normalized;
                bool straightIn = Vector3.Dot(delta / distance, axis) > Mathf.Cos(30f * Mathf.Deg2Rad);
                if (straightIn && !Physics.SphereCast(cue, radius * 1.05f, delta / distance, out _, distance, mask, QueryTriggerInteraction.Ignore) && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = pocket;
                }
            }

            return best;
        }
    }
}
