using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VTG.Pool.AI;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Match;
using VTG.Pool.Rules;
using VTG.Pool.UI;

namespace VTG.Pool.Tests
{
    public sealed class AIMatchTests
    {
        private MatchManager match;
        private ShotController shot;
        private CueBallPlacementController placement;
        private ShotRecord started;
        private ShotRecord finished;

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            PoolEvents.ClearAll();
            yield return SceneTestUtility.UnloadAllScenes();
        }

        private IEnumerator Load(GameMode mode)
        {
            started = null;
            finished = null;
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            yield return null;
            match = Object.FindAnyObjectByType<MatchManager>();
            shot = Object.FindAnyObjectByType<ShotController>();
            placement = Object.FindAnyObjectByType<CueBallPlacementController>();
            PoolEvents.ShotStarted += record => started = record;
            PoolEvents.BallsStopped += record => finished = record;
            match.StartMatch(mode, true, AIDifficulty.Expert); // Second match alternates to AI breaker.
            yield return null;
            Assert.AreEqual(PlayerKind.AI, match.State.CurrentPlayer.Kind);
            Assert.IsTrue(shot.ComputerOwnsInput);
            Time.timeScale = 4f;
        }

        private IEnumerator WaitForShot()
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (started == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsNotNull(started, "AI must execute a physical shot");
        }

        [UnityTest] public IEnumerator NineBall_AIPlacesBreaksAndPauseBlocksExecution()
        {
            yield return Load(GameMode.NineBall);
            var hud = Object.FindAnyObjectByType<PoolHud>();
            hud.SetPaused(true);
            shot.BeginShot(1f);
            Assert.IsFalse(shot.ExecuteShot(new ShotParameters(Vector3.forward, 1f, Vector2.zero)));
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsNull(started);
            Assert.IsTrue(placement.IsPlacing);
            hud.SetPaused(false);
            Time.timeScale = 4f;
            yield return WaitForShot();
            Assert.AreEqual(1, started.PlayerId);
            Assert.IsTrue(started.IsBreakShot);
            Assert.IsFalse(shot.CueBall.IsHeld);
            float deadline = Time.realtimeSinceStartup + 20f;
            while (finished == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsNotNull(finished);
            Assert.AreEqual(1, finished.FirstObjectBallHit);
            Assert.Greater(match.State.ShotsPlayed, 0);
            match.StartMatch(GameMode.NineBall, false);
            yield return null;
            Assert.IsFalse(shot.ComputerOwnsInput);
            Assert.AreEqual(PlayerKind.LocalHuman, match.State.Players[1].Kind);
        }

        [UnityTest] public IEnumerator EightBall_AIBreakAndGameOverCancelFurtherShots()
        {
            yield return Load(GameMode.EightBall);
            shot.BeginShot(1f);
            Assert.IsNull(started, "Human BeginShot cannot hijack AI placement");
            yield return WaitForShot();
            Assert.IsTrue(started.IsBreakShot);
            match.TurnManager.Apply(new ShotOutcome { GameOver = true, WinnerIndex = 0, Message = "Test" }, new ShotRecord());
            started = null;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsNull(started);
            Assert.AreEqual(ShotPhase.GameOver, shot.Phase);
            Assert.IsFalse(shot.ComputerOwnsInput);
        }

        [UnityTest] public IEnumerator NineBall_AIPlansAndPotsAnIsolatedStraightBall()
        {
            yield return Load(GameMode.NineBall);
            Time.timeScale = 0f;
            match.State.IsBreakShot = false;
            match.State.SetBallsOnTable(new[] { 1, 9 });
            var all = BallRegistry.Balls;
            for (int i = all.Count - 1; i >= 0; i--)
                if (!all[i].IsCueBall && all[i].BallId != 1 && all[i].BallId != 9) all[i].gameObject.SetActive(false);
            float y = shot.Table.SurfaceHeight + shot.CueBall.Radius;
            BallRegistry.FindById(1).PlaceAt(new Vector3(0.3f, y, 0f));
            BallRegistry.FindById(9).PlaceAt(new Vector3(-0.4f, y, 0.7f));
            Assert.IsTrue(placement.TryMoveTo(new Vector3(-0.1f, y, -0.8f))); // kitchen rights still active
            placement.Clear();
            shot.CueBall.PlaceAt(new Vector3(-0.1f, y, 0f));
            Assert.IsTrue(match.GetComponent<AIController>().TryPlan(out var planned));
            Assert.Greater(planned.AimDirection.x, 0.99f);
            Time.timeScale = 4f;
            yield return WaitForShot();
            float deadline = Time.realtimeSinceStartup + 20f;
            while (finished == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsNotNull(finished);
            Assert.AreEqual(1, finished.FirstObjectBallHit);
            CollectionAssert.Contains(finished.BallsPocketed, 1);
        }

        [UnityTest] public IEnumerator Foul_AIUsesBallInHandAndReturnsInputToHuman()
        {
            yield return Load(GameMode.NineBall);
            match.StartMatch(GameMode.NineBall, true, AIDifficulty.Expert); // Human breaker.
            yield return null;
            Assert.AreEqual(0, match.State.CurrentPlayerIndex);
            match.State.IsBreakShot = false;
            match.TurnManager.Apply(new ShotOutcome { Foul = true, FoulType = FoulType.Scratch,
                BallInHand = BallInHandMode.Anywhere, Message = "Test scratch" }, new ShotRecord { CueBallPocketed = true });
            yield return null;
            Assert.AreEqual(1, match.State.CurrentPlayerIndex);
            Assert.IsTrue(shot.ComputerOwnsInput);
            yield return WaitForShot();
            Assert.AreEqual(1, started.PlayerId);
            Assert.IsFalse(shot.CueBall.IsHeld);
            match.StartMatch(GameMode.EightBall, false);
            yield return null;
            Assert.IsFalse(shot.ComputerOwnsInput);
            Assert.IsTrue(placement.TryConfirm());
            Assert.IsFalse(shot.ShotInputBlocked);
        }

        [UnityTest] public IEnumerator SnookeredNineBall_PlannedKickHitsLegalBallFirst()
        {
            yield return Load(GameMode.NineBall);
            Time.timeScale = 0f;
            match.State.IsBreakShot = false;
            match.State.SetBallsOnTable(new[] { 1, 9 });
            var all = BallRegistry.Balls;
            for (int i = all.Count - 1; i >= 0; i--)
                if (!all[i].IsCueBall && all[i].BallId != 1 && all[i].BallId != 9) all[i].gameObject.SetActive(false);
            placement.Clear();
            float y = shot.Table.SurfaceHeight + shot.CueBall.Radius;
            // Offset layout: the one-rail kick contacts the long rail ~0.16 m from the side pocket. With all three balls
            // on the centre line the only one-rail path meets the rail inside the side-pocket mouth (no cushion there).
            shot.CueBall.PlaceAt(new Vector3(0.2f, y, -0.5f));
            BallRegistry.FindById(1).PlaceAt(new Vector3(-0.2f, y, 0.5f));
            BallRegistry.FindById(9).PlaceAt(new Vector3(0f, y, 0f));
            var ai = match.GetComponent<AIController>();
            Assert.IsTrue(ai.TryPlan(out var planned));
            Assert.IsTrue(ai.LastPlanWasKick, "Must route around the blocking 9-ball");
            TestContext.WriteLine($"kick trials={ai.LastKickTrials}, planning={ai.LastPlanningMilliseconds:F2} ms, shot={planned}");
            ai.enabled = false; // Execute exact plan to isolate prediction from difficulty error.
            Time.timeScale = 4f;
            Assert.IsTrue(shot.ExecuteShot(planned));
            float deadline = Time.realtimeSinceStartup + 20f;
            while (finished == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsNotNull(finished);
            Assert.AreEqual(1, finished.FirstObjectBallHit);
            Assert.Greater(finished.CushionHits, 0);
        }
    }
}
