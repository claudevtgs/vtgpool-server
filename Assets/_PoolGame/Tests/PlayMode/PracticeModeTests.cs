using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VTG.Pool.Balls;
using VTG.Pool.CameraSystem;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Match;
using VTG.Pool.Rules;
using VTG.Pool.Rules.Practice;
using VTG.Pool.Save;
using VTG.Pool.Table;
using VTG.Pool.UI;

namespace VTG.Pool.Tests
{
    /// <summary>Free practice in 02_Match: launch from the menu, no turn change, undo, arranging balls, racks.</summary>
    public sealed class PracticeModeTests
    {
        private string directory;
        private MatchManager match;
        private TurnManager turns;
        private ShotController shot;
        private CueBallPlacementController placement;
        private PracticeSession session;
        private PracticeLayoutEditor editor;
        private TableBuilder table;
        private ShotRecord finished;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "VTGPoolPracticeTests_" + System.Guid.NewGuid().ToString("N"));
            SaveSystem.DirectoryOverride = directory;
            SaveSystem.ClearCache();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            PoolEvents.ClearAll();
            yield return SceneTestUtility.UnloadAllScenes();
            SaveSystem.DirectoryOverride = null;
            SaveSystem.ClearCache();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        private static IEnumerator Load(string scene)
        {
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Assert.Ignore($"{scene} is not in the build settings (run VTG Pool/Setup).");
            }

            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            yield return null;
        }

        private void FindSystems()
        {
            match = Object.FindAnyObjectByType<MatchManager>();
            turns = Object.FindAnyObjectByType<TurnManager>();
            shot = Object.FindAnyObjectByType<ShotController>();
            placement = Object.FindAnyObjectByType<CueBallPlacementController>();
            session = Object.FindAnyObjectByType<PracticeSession>();
            editor = Object.FindAnyObjectByType<PracticeLayoutEditor>();
            table = Object.FindAnyObjectByType<TableBuilder>();
            Assert.IsNotNull(session, "PracticeSession missing in 02_Match (run VTG Pool/Setup)");
            Assert.IsNotNull(editor, "PracticeLayoutEditor missing in 02_Match");
            PoolEvents.BallsStopped += record => finished = record;
        }

        private IEnumerator LoadPractice(GameMode rack = GameMode.EightBall)
        {
            MatchLaunch.RequestPractice(rack);
            yield return Load("02_Match");
            FindSystems();
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

        [UnityTest]
        public IEnumerator MainMenu_PracticeOpensFreePractice()
        {
            yield return Load("01_MainMenu");
            Button practice = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude)
                .FirstOrDefault(b => b.GetComponentInChildren<Text>() != null && b.GetComponentInChildren<Text>().text == "PRACTICE");
            Assert.IsNotNull(practice);
            practice.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.AreEqual("02_Match", SceneManager.GetActiveScene().name);
            FindSystems();
            yield return null;

            Assert.IsTrue(match.IsPractice);
            Assert.IsInstanceOf<PracticeRuleSet>(turns.Rules);
            Assert.IsFalse(match.VersusAI);
            Assert.AreEqual(15, turns.State.BallsOnTable.Count);
            Assert.AreEqual(BallInHandMode.Anywhere, placement.Mode, "Practice starts with ball in hand anywhere");
            var hud = Object.FindAnyObjectByType<PoolHud>();
            Assert.IsNotNull(hud.PracticePanel, "Practice panel");
            Assert.IsTrue(hud.PracticePanel.GetComponent<Image>().enabled, "Practice panel is shown in practice");
            int frames = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude).Count(r => r.name == "PlayerFrame");
            Assert.AreEqual(1, frames, "Only the practising player is shown");
        }

        [UnityTest]
        public IEnumerator Practice_ShotKeepsTable_UndoRestoresLayout_NoStats()
        {
            yield return LoadPractice();
            Assert.IsTrue(placement.TryConfirm());
            var before = new Dictionary<PoolBall, Vector3>();
            foreach (PoolBall ball in BallRegistry.Balls)
            {
                before[ball] = ball.Position;
            }

            // Hard break into the rack.
            PoolBall apex = BallRegistry.Balls.Where(b => !b.IsCueBall).OrderBy(b => Vector3.Distance(b.Position, table.FootSpot)).First();
            Vector3 aim = apex.Position - shot.CueBall.Position;
            aim.y = 0f;
            Time.timeScale = 4f;
            finished = null;
            Assert.IsTrue(shot.ExecuteShot(new ShotParameters(aim.normalized, 1f, Vector2.zero)));
            float timeout = Time.realtimeSinceStartup + 40f;
            while (finished == null && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.IsNotNull(finished, "Balls never came to rest");
            yield return WaitForAiming();
            Time.timeScale = 1f;

            Assert.AreEqual(0, turns.State.CurrentPlayerIndex, "Practice never passes the turn");
            Assert.IsFalse(turns.State.LastOutcome.Foul);
            Assert.AreEqual(1, session.Shots);
            Assert.AreEqual(finished.BallsPocketed.Count, session.BallsPocketed);
            Assert.IsTrue(session.CanUndo);

            Assert.IsTrue(session.Undo());
            yield return null;
            foreach (KeyValuePair<PoolBall, Vector3> entry in before)
            {
                Assert.IsFalse(entry.Key.IsPocketed, $"Ball {entry.Key.BallId} restored");
                Assert.Less(Vector3.Distance(entry.Key.Position, entry.Value), 1e-3f, $"Ball {entry.Key.BallId} back in place");
            }

            Assert.AreEqual(15, turns.State.BallsOnTable.Count);
            Assert.IsFalse(session.CanUndo, "Only one level of undo");
            Assert.IsFalse(placement.IsPlacing);
            Assert.IsFalse(shot.ShotInputBlocked, "Ready to shoot again");

            PlayerStats stats = SaveSystem.Stats;
            Assert.AreEqual(0, stats.matchesPlayed + stats.breaks + stats.ballsPocketed + stats.fouls, "Practice is not counted in statistics");
        }

        [UnityTest]
        public IEnumerator Practice_ArrangeRemoveAddAndSwitchRacks()
        {
            yield return LoadPractice();
            var cameras = Object.FindAnyObjectByType<CameraController>();
            CameraMode startCamera = cameras.ActiveMode;

            editor.SetEditing(true);
            Assert.IsTrue(editor.IsEditing);
            Assert.IsTrue(shot.ShotInputBlocked, "No shooting while arranging");
            Assert.IsFalse(placement.IsPlacing, "Ball in hand is settled when arranging starts");
            Assert.AreEqual(CameraMode.Top, cameras.ActiveMode);

            // Move the 1-ball to an empty spot.
            PoolBall one = BallRegistry.Balls.First(b => b.BallId == 1);
            float radius = one.Radius;
            Vector3 free = table.SurfaceCenter + Vector3.right * 0.35f + Vector3.forward * -0.3f + Vector3.up * radius;
            Assert.IsTrue(editor.TryPick(one.Position));
            Assert.AreEqual(one, editor.HeldBall);
            editor.DragTo(free);
            editor.Drop();
            Assert.Less(Vector3.Distance(one.Position, free), 1e-3f, "Ball dropped where it was dragged");
            Assert.IsFalse(one.IsHeld);

            // Drop the 2-ball into a pocket: it leaves the table.
            PoolBall two = BallRegistry.Balls.First(b => b.BallId == 2);
            Pocket pocket = table.PocketManager.Pockets[0];
            Assert.IsTrue(editor.TryPick(two.Position));
            editor.DragTo(new Vector3(pocket.Center.x, free.y, pocket.Center.z));
            editor.Drop();
            Assert.IsFalse(two.gameObject.activeSelf, "Ball dropped in a pocket is removed");
            Assert.AreEqual(14, turns.State.BallsOnTable.Count);
            CollectionAssert.Contains(session.BallsOffTable(), 2);

            // The cue ball cannot be removed.
            Assert.IsTrue(editor.TryPick(shot.CueBall.Position));
            editor.DragTo(new Vector3(pocket.Center.x, free.y, pocket.Center.z));
            editor.Drop();
            Assert.IsTrue(shot.CueBall.gameObject.activeSelf);
            Assert.IsFalse(shot.CueBall.IsPocketed);

            // Put the 2-ball back from the tray.
            Assert.IsTrue(session.AddBall(2));
            Assert.IsTrue(PracticeSession.IsOnTable(two));
            Assert.AreEqual(15, turns.State.BallsOnTable.Count);

            editor.SetEditing(false);
            Assert.IsFalse(shot.ShotInputBlocked);
            Assert.AreEqual(startCamera, cameras.ActiveMode, "Camera restored after arranging");

            // Racks.
            session.SetRack(PracticeRack.NineBall);
            yield return null;
            Assert.AreEqual(PracticeRack.NineBall, session.Rack);
            Assert.AreEqual(9, turns.State.BallsOnTable.Count);
            session.SetRack(PracticeRack.Empty);
            yield return null;
            Assert.AreEqual(PracticeRack.Empty, session.Rack);
            Assert.AreEqual(0, turns.State.BallsOnTable.Count);
            Assert.AreEqual(1, BallRegistry.Balls.Count(b => PracticeSession.IsOnTable(b)), "Only the cue ball remains");
            Assert.AreEqual("table clear", turns.Rules.DescribeTarget(turns.State));

            // Restart keeps practice (pause menu "Restart"); a real game leaves it.
            match.StartMatch();
            Assert.IsTrue(match.IsPractice);
            Assert.AreEqual(0, turns.State.BallsOnTable.Count, "Restart keeps the empty table");
            match.StartMatch(GameMode.EightBall, false);
            Assert.IsFalse(match.IsPractice);
            Assert.IsNotInstanceOf<PracticeRuleSet>(turns.Rules);
            Assert.AreEqual(15, turns.State.BallsOnTable.Count);
        }
    }
}
