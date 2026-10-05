using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VTG.Pool.Balls;
using VTG.Pool.CameraSystem;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Match;
using VTG.Pool.Replay;
using VTG.Pool.Rules;
using VTG.Pool.Save;
using VTG.Pool.Table;
using VTG.Pool.UI;

namespace VTG.Pool.Tests
{
    /// <summary>Pot replays, lucky-shot detection, multi-ball and deciding-ball presentation.</summary>
    public sealed class HighlightReplayTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "VTGPoolHighlightTests_" + System.Guid.NewGuid().ToString("N"));
            SaveSystem.DirectoryOverride = directory;
            SaveSystem.ClearCache();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ShotReplay.ForceDisabled = true;
            ShotReplay.Setting = ShotReplay.Mode.EveryPot;
            ShotCinematics.Enabled = true;
            Time.timeScale = 1f;
            PoolEvents.ClearAll();
            yield return SceneTestUtility.UnloadAllScenes();
            SaveSystem.DirectoryOverride = null;
            SaveSystem.ClearCache();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private static IEnumerator LoadMatch(bool practice)
        {
            if (practice) MatchLaunch.RequestPractice(GameMode.EightBall);
            else MatchLaunch.Request(GameMode.EightBall, false, 1);
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Pot_PlaysASlowMotionReplayThenHandsBack()
        {
            ShotReplay.ForceDisabled = false;
            ShotReplay.Setting = ShotReplay.Mode.EveryPot;
            ShotCinematics.Enabled = false;
            yield return LoadMatch(true);
            var session = Object.FindAnyObjectByType<PracticeSession>();
            var shot = Object.FindAnyObjectByType<ShotController>();
            var cameras = Object.FindAnyObjectByType<CameraController>();
            var table = Object.FindAnyObjectByType<TableBuilder>();
            Object.FindAnyObjectByType<CueBallPlacementController>().TryConfirm();
            yield return null;

            // Only the 1-ball, lined up 25 cm in front of a corner pocket with the cue ball behind it.
            foreach (PoolBall ball in BallRegistry.Balls.Where(b => !b.IsCueBall && b.BallId != 1).ToList())
            {
                session.RemoveBall(ball);
            }

            Pocket pocket = table.PocketManager.Pockets.First(p => p.Kind == PocketKind.Corner);
            Vector3 toCenter = table.SurfaceCenter - pocket.Center;
            toCenter.y = 0f;
            toCenter.Normalize();
            float restY = table.SurfaceHeight + shot.CueBall.Radius;
            Vector3 one = new Vector3(pocket.Center.x, restY, pocket.Center.z) + toCenter * 0.3f;
            BallRegistry.Balls.First(b => b.BallId == 1).PlaceAt(one);
            shot.CueBall.PlaceAt(one + toCenter * 0.45f);
            session.LayoutChanged();
            yield return null;
            CameraMode before = cameras.ActiveMode;

            Assert.IsTrue(shot.ExecuteShot(new ShotParameters(-toCenter, 0.3f, Vector2.zero)));
            float wait = Time.realtimeSinceStartup + 20f;
            while (!ShotReplay.IsPlaying && Time.realtimeSinceStartup < wait) yield return null;
            Assert.IsTrue(ShotReplay.IsPlaying, "The pot is replayed");
            Assert.AreEqual(CameraMode.Cinematic, cameras.ActiveMode, "Replay camera");
            Assert.IsTrue(shot.ShotInputBlocked, "No shooting during the replay");
            Assert.IsTrue(Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).Any(t => t.name.StartsWith("Replay_")), "Ball stand-ins are shown");
            Renderer realCue = shot.CueBall.GetComponentInChildren<MeshRenderer>(true);
            Assert.IsFalse(realCue.enabled, "Real balls are hidden behind the stand-ins");

            wait = Time.realtimeSinceStartup + 15f;
            while (ShotReplay.IsPlaying && Time.realtimeSinceStartup < wait) yield return null;
            Assert.IsFalse(ShotReplay.IsPlaying, "Replay ends by itself");
            yield return null;
            Assert.IsTrue(realCue.enabled, "Real balls visible again");
            Assert.IsFalse(Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).Any(t => t.name.StartsWith("Replay_")));
            Assert.AreEqual(before, cameras.ActiveMode, "Player camera restored");
            Assert.IsFalse(shot.PresentationLocked, "The replay releases the table (ball in hand after a scratch may still apply)");
        }

        [UnityTest]
        public IEnumerator Highlights_FlukeMultiAndDecidingBall()
        {
            yield return LoadMatch(false);
            var turns = Object.FindAnyObjectByType<TurnManager>();
            var callouts = Object.FindAnyObjectByType<ShotCallouts>();
            var table = Object.FindAnyObjectByType<TableBuilder>();
            var hud = Object.FindAnyObjectByType<PoolHud>();
            var highlights = new List<ShotHighlight>();
            callouts.HighlightDetected += h => highlights.Add(h);
            PoolBall cue = BallRegistry.FindCueBall();
            PoolBall Ball(int n) => BallRegistry.Balls.First(b => b.BallId == n);
            Pocket pocket = table.PocketManager.Pockets[0];
            turns.State.IsBreakShot = false;

            // Fluke: the cue hits the 1, the 3 drops (it was never touched by the cue).
            var record = new ShotRecord { FirstObjectBallHit = 1, RailContactsAfterFirstHit = 1 };
            PoolEvents.RaiseShotStarted(record);
            PoolEvents.RaiseBallHit(new BallHitInfo(cue, Ball(1), 1f, cue.Position));
            PoolEvents.RaiseBallHit(new BallHitInfo(Ball(1), Ball(3), 1f, cue.Position));
            PoolEvents.RaiseBallPocketed(Ball(3), pocket);
            record.BallsPocketed.Add(3);
            turns.Apply(new ShotOutcome { TurnContinues = true, Message = "fluke" }, record);
            Assert.AreEqual(1, highlights.Count);
            Assert.IsTrue(highlights[0].Fluke, "Aimed at the 1, the 3 went in off a kiss");

            // Clean multi-ball pot: both balls hit by the cue directly → not a fluke, x2.
            record = new ShotRecord { FirstObjectBallHit = 2 };
            PoolEvents.RaiseShotStarted(record);
            PoolEvents.RaiseBallHit(new BallHitInfo(cue, Ball(2), 1f, cue.Position));
            PoolEvents.RaiseBallHit(new BallHitInfo(cue, Ball(4), 1f, cue.Position));
            PoolEvents.RaiseBallPocketed(Ball(2), pocket);
            PoolEvents.RaiseBallPocketed(Ball(4), table.PocketManager.Pockets[1]);
            record.BallsPocketed.Add(2);
            record.BallsPocketed.Add(4);
            turns.Apply(new ShotOutcome { TurnContinues = true, Message = "two" }, record);
            Assert.AreEqual(2, highlights.Count);
            Assert.IsFalse(highlights[1].Fluke);
            Assert.AreEqual(2, highlights[1].Pocketed.Count);
            yield return null;
            yield return null;
            Assert.IsTrue(hud.Highlights.ComboShowing, "x2 shown for a double");

            // The 8 wins the rack: deciding-ball highlight and the emblem on the victory title.
            int shooter = turns.State.CurrentPlayerIndex;
            record = new ShotRecord { FirstObjectBallHit = 8 };
            PoolEvents.RaiseShotStarted(record);
            PoolEvents.RaiseBallHit(new BallHitInfo(cue, Ball(8), 1f, cue.Position));
            PoolEvents.RaiseBallPocketed(Ball(8), pocket);
            record.BallsPocketed.Add(8);
            turns.Apply(new ShotOutcome { GameOver = true, WinnerIndex = shooter, Message = "8 down" }, record);
            Assert.AreEqual(3, highlights.Count);
            Assert.AreEqual(8, highlights[2].MoneyBall);
            Assert.IsTrue(highlights[2].WinningShot);
            yield return null;
            yield return null;
            Transform emblem = hud.transform.GetComponentsInChildren<Transform>(true).First(t => t.name == "Emblem");
            Assert.IsTrue(emblem.gameObject.activeInHierarchy, "The 8 emblem crowns the victory");

            hud.Highlights.ShowTroll();
            Assert.IsTrue(hud.Highlights.TrollShowing);
        }

        [UnityTest]
        public IEnumerator Gags_ScratchWhiffStreakAndLoss()
        {
            yield return LoadMatch(false);
            var turns = Object.FindAnyObjectByType<TurnManager>();
            var hud = Object.FindAnyObjectByType<PoolHud>();
            PoolBall cue = BallRegistry.FindCueBall();
            PoolBall one = BallRegistry.Balls.First(b => b.BallId == 1);
            turns.State.IsBreakShot = false;

            ShotRecord Shot(bool hit)
            {
                var record = new ShotRecord { FirstObjectBallHit = hit ? 1 : -1 };
                PoolEvents.RaiseShotStarted(record);
                if (hit) PoolEvents.RaiseBallHit(new BallHitInfo(cue, one, 1f, cue.Position));
                return record;
            }

            turns.Apply(new ShotOutcome { Foul = true, FoulType = FoulType.Scratch, BallInHand = BallInHandMode.Anywhere }, Shot(true));
            yield return null;
            Assert.AreEqual(HighlightPresenter.Gag.Ghost, hud.Highlights.CurrentGag, "Scratch: the cue ball's ghost");

            turns.Apply(new ShotOutcome { Foul = true, FoulType = FoulType.NoContact, BallInHand = BallInHandMode.Anywhere }, Shot(false));
            yield return null;
            Assert.AreEqual(HighlightPresenter.Gag.Tumbleweed, hud.Highlights.CurrentGag, "Hit nothing: tumbleweed and crickets");

            // Same shooter misses three times in a row (turn passes back each time via the 2-player rotation).
            int shooter = turns.State.CurrentPlayerIndex;
            for (int i = 0; i < 6; i++)
            {
                turns.Apply(new ShotOutcome { Message = "miss" }, Shot(true));
            }

            yield return null;
            Assert.AreEqual(HighlightPresenter.Gag.RainCloud, hud.Highlights.CurrentGag, "Three misses in a row: rain cloud");

            int loser = turns.State.CurrentPlayerIndex;
            turns.Apply(new ShotOutcome { GameOver = true, WinnerIndex = 1 - loser, Message = "8 early" }, Shot(true));
            yield return null;
            Assert.AreEqual(HighlightPresenter.Gag.Toang, hud.Highlights.CurrentGag, "Losing the rack: TOANG");
        }

        [UnityTest]
        public IEnumerator MissAndFoul_ShowStamps()
        {
            yield return LoadMatch(false);
            var turns = Object.FindAnyObjectByType<TurnManager>();
            var callouts = Object.FindAnyObjectByType<ShotCallouts>();
            var hud = Object.FindAnyObjectByType<PoolHud>();
            var highlights = new List<ShotHighlight>();
            callouts.HighlightDetected += h => highlights.Add(h);
            PoolBall cue = BallRegistry.FindCueBall();
            PoolBall one = BallRegistry.Balls.First(b => b.BallId == 1);
            turns.State.IsBreakShot = false;

            // Legal hit, nothing down, turn passes: a miss.
            var record = new ShotRecord { FirstObjectBallHit = 1, RailContactsAfterFirstHit = 1 };
            PoolEvents.RaiseShotStarted(record);
            PoolEvents.RaiseBallHit(new BallHitInfo(cue, one, 1f, cue.Position));
            turns.Apply(new ShotOutcome { Message = "miss" }, record);
            Assert.AreEqual(1, highlights.Count);
            Assert.IsTrue(highlights[0].Missed);
            yield return null;
            yield return null;
            Assert.IsTrue(hud.Highlights.StampShowing, "MISS stamp");
            Assert.IsFalse(hud.Highlights.LastStampWasFoul);

            // Scratch: a foul, not a miss.
            record = new ShotRecord { FirstObjectBallHit = 1 };
            PoolEvents.RaiseShotStarted(record);
            PoolEvents.RaiseBallHit(new BallHitInfo(cue, one, 1f, cue.Position));
            turns.Apply(new ShotOutcome { Foul = true, FoulType = FoulType.Scratch, BallInHand = BallInHandMode.Anywhere, Message = "scratch" }, record);
            Assert.AreEqual(2, highlights.Count);
            Assert.IsFalse(highlights[1].Missed);
            Assert.AreEqual(FoulType.Scratch, highlights[1].FoulType);
            yield return null;
            yield return null;
            Assert.IsTrue(hud.Highlights.StampShowing && hud.Highlights.LastStampWasFoul, "FOUL stamp");
            float end = Time.realtimeSinceStartup + 4f;
            while (hud.Highlights.StampShowing && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsFalse(hud.Highlights.StampShowing, "The stamp clears by itself");
        }
    }
}
