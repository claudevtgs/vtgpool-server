using System.Collections;
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
using VTG.Pool.Inputs;
using VTG.Pool.Match;
using VTG.Pool.Save;
using VTG.Pool.UI;

namespace VTG.Pool.Tests
{
    /// <summary>Scripted input for the shot controller (mouse stroke tests).</summary>
    public sealed class ScriptedShotInput : IShotInput
    {
        public float AimDeltaDegrees { get; set; }
        public Vector2 LookDeltaDegrees { get; set; }
        public float ZoomDelta { get; set; }
        public Vector2 SpinDelta { get; set; }
        public float ElevationDelta { get; set; }
        public bool ResetSpinPressed { get; set; }
        public bool PrecisionHeld { get; set; }
        public bool PowerPressed { get; set; }
        public bool PowerHeld { get; set; }
        public bool PowerReleased { get; set; }
        public float StrokeDelta { get; set; }
        public bool StrokeAvailable { get; set; }
        public float AnalogPower { get; set; } = -1f;
        public bool ShootPressed { get; set; }
        public bool CancelPressed { get; set; }
        public bool PausePressed { get; set; }
        public bool CameraCuePressed { get; set; }
        public bool CameraTacticalPressed { get; set; }
        public bool CameraTopPressed { get; set; }
        public Vector2 PointerPosition { get; set; }
        public bool PointerMoved { get; set; }
        public bool PlacePressed { get; set; }
        public bool BallInHandPressed { get; set; }
        public Vector2 MoveAxis { get; set; }
        public bool PointerOverUI { get; set; }
        public bool PointerPressed { get; set; }
        public bool PointerHeld { get; set; }
        public bool PointerReleased { get; set; }
    }

    public sealed class StrokeCinematicIntroTests
    {
        private string directory;
        private ShotRecord started;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "VTGPoolStrokeTests_" + System.Guid.NewGuid().ToString("N"));
            SaveSystem.DirectoryOverride = directory;
            SaveSystem.ClearCache();
            started = null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            IntroDirector.PlayOnNextMenu = false;
            ShotCinematics.Enabled = true;
            PoolEvents.ClearAll();
            yield return SceneTestUtility.UnloadAllScenes();
            SaveSystem.DirectoryOverride = null;
            SaveSystem.ClearCache();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        private IEnumerator LoadPracticeTable()
        {
            MatchLaunch.RequestPractice(Rules.GameMode.EightBall);
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            yield return null;
            Object.FindAnyObjectByType<CueBallPlacementController>().TryConfirm();
            PoolEvents.ShotStarted += record => started = record;
            yield return null;
        }

        [UnityTest]
        public IEnumerator MouseStroke_PullBackThenPushStrikesWithHandSpeed()
        {
            ShotCinematics.Enabled = false;
            yield return LoadPracticeTable();
            var shot = Object.FindAnyObjectByType<ShotController>();
            var input = new ScriptedShotInput();
            shot.SetInput(input);
            shot.MouseStrokeEnabled = true;

            // Key down: stroke starts; pulling back draws the cue back (no shot).
            input.PowerPressed = input.PowerHeld = input.StrokeAvailable = true;
            yield return null;
            input.PowerPressed = false;
            Assert.IsTrue(shot.Stroking);
            for (int i = 0; i < 6; i++)
            {
                input.StrokeDelta = -0.05f;
                yield return null;
            }

            input.StrokeDelta = 0f;
            Assert.Greater(shot.StrokeDrawBack, 0.05f, "Pulling the mouse back draws the cue back");
            Assert.IsNull(started);

            // Releasing the key before pushing cancels.
            input.PowerHeld = false;
            input.PowerReleased = true;
            yield return null;
            input.PowerReleased = false;
            Assert.AreEqual(ShotPhase.Aiming, shot.Phase);
            Assert.IsNull(started, "No shot without a forward stroke");

            // Again: pull back, then a fast push through the ball.
            input.PowerPressed = input.PowerHeld = true;
            yield return null;
            input.PowerPressed = false;
            for (int i = 0; i < 6; i++)
            {
                input.StrokeDelta = -0.05f;
                yield return null;
            }

            float wait = Time.realtimeSinceStartup + 3f;
            while (started == null && Time.realtimeSinceStartup < wait)
            {
                input.StrokeDelta = 2.4f * Time.unscaledDeltaTime; // ~full-power hand speed
                yield return null;
            }

            Assert.IsNotNull(started, "Pushing the cue through the ball plays the shot");
            Assert.Greater(started.Power, 0.6f, "A fast stroke is a strong shot");
            shot.SetInput(Object.FindAnyObjectByType<PoolInputReader>());
        }

        [UnityTest]
        public IEnumerator Cinematics_TakeOverDuringTheShotAndReturn()
        {
            ShotCinematics.Enabled = true;
            yield return LoadPracticeTable();
            var shot = Object.FindAnyObjectByType<ShotController>();
            var cameras = Object.FindAnyObjectByType<CameraController>();
            var cinematics = cameras.GetComponent<ShotCinematics>();
            Assert.IsNotNull(cinematics);
            CameraMode before = cameras.ActiveMode;
            PoolBall apex = BallRegistry.Balls.Where(b => !b.IsCueBall).OrderBy(b => Vector3.Distance(b.Position, Object.FindAnyObjectByType<Table.TableBuilder>().FootSpot)).First();
            Time.timeScale = 2f;
            Assert.IsTrue(shot.ExecuteShot(new ShotParameters(apex.Position - shot.CueBall.Position, 1f, Vector2.zero)));
            yield return null;
            yield return null;
            Assert.IsTrue(cinematics.Active);
            Assert.AreEqual(CameraMode.Cinematic, cameras.ActiveMode);
            float bars = Time.realtimeSinceStartup + 2f;
            while (cinematics.Letterbox < 0.99f && Time.realtimeSinceStartup < bars) yield return null;
            Assert.Greater(cinematics.Letterbox, 0.99f, "Letterbox bars slide in");
            Vector3 eye = Camera.main.transform.position;
            Assert.Greater(eye.y, Object.FindAnyObjectByType<Table.TableBuilder>().SurfaceHeight + 0.05f, "Camera stays above the cloth");

            float wait = Time.realtimeSinceStartup + 40f;
            while (shot.Phase != ShotPhase.Aiming && Time.realtimeSinceStartup < wait)
            {
                yield return null;
            }

            yield return null;
            Assert.IsFalse(cinematics.Active, "Cinematic ends when the balls stop");
            float clear = Time.realtimeSinceStartup + 2f;
            while (cinematics.Letterbox > 0f && Time.realtimeSinceStartup < clear) yield return null;
            Assert.AreEqual(0f, cinematics.Letterbox, "Bars slide away");
            Assert.AreEqual(before, cameras.ActiveMode, "Player camera restored");
            Assert.AreEqual(2f, Time.timeScale, 1e-4f, "Slow motion restores the previous time scale");
        }

        [UnityTest]
        public IEnumerator JumpOffTheTable_IsAFoulWithBallInHand()
        {
            ShotCinematics.Enabled = false;
            MatchLaunch.Request(Rules.GameMode.EightBall, false, 1);
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            yield return null;
            var shot = Object.FindAnyObjectByType<ShotController>();
            var turns = Object.FindAnyObjectByType<TurnManager>();
            var placement = Object.FindAnyObjectByType<CueBallPlacementController>();
            float wait = Time.realtimeSinceStartup + 5f;
            while (!placement.IsPlacing && Time.realtimeSinceStartup < wait) yield return null;
            Assert.IsTrue(placement.TryConfirm());
            int shooter = turns.State.CurrentPlayerIndex;
            turns.State.IsBreakShot = false; // an open-table shot, not the break
            ShotRecord finished = null;
            PoolEvents.BallsStopped += r => finished = r;

            // Full-power jump straight at the side rail.
            Time.timeScale = 3f;
            Assert.IsTrue(shot.ExecuteShot(new ShotParameters(Vector3.right, 1f, Vector2.zero, 30f)));
            wait = Time.realtimeSinceStartup + 30f;
            while (finished == null && Time.realtimeSinceStartup < wait) yield return null;
            Assert.IsNotNull(finished);
            yield return null;
            yield return null;
            Assert.IsTrue(finished.CueBallOffTable, "The cue ball left the table");
            Assert.IsTrue(turns.State.LastOutcome.Foul);
            Assert.AreNotEqual(shooter, turns.State.CurrentPlayerIndex, "Turn passes after the foul");
            Assert.AreEqual(Rules.BallInHandMode.Anywhere, turns.State.BallInHand, "Opponent gets ball in hand");
            wait = Time.realtimeSinceStartup + 3f;
            while (!placement.IsPlacing && Time.realtimeSinceStartup < wait) yield return null;
            Assert.IsFalse(shot.CueBall.IsPocketed, "Cue ball is back on the table for placement");
        }

        [UnityTest]
        public IEnumerator Intro_PlaysABreakAndHandsOverToTheMenu()
        {
            IntroDirector.PlayOnNextMenu = true;
            yield return SceneManager.LoadSceneAsync("01_MainMenu", LoadSceneMode.Single);
            yield return null;
            IntroDirector intro = Object.FindAnyObjectByType<IntroDirector>();
            Assert.IsNotNull(intro, "Intro runs when the game starts");
            CanvasGroup menu = Object.FindAnyObjectByType<MainMenuController>().GetComponentInChildren<CanvasGroup>();
            Assert.IsFalse(menu.blocksRaycasts, "Menu waits for the intro");

            PoolBall cue = BallRegistry.FindCueBall();
            Vector3 start = cue.Position;
            float wait = Time.realtimeSinceStartup + 8f;
            while (Vector3.Distance(cue.Position, start) < 0.2f && Time.realtimeSinceStartup < wait)
            {
                yield return null;
            }

            Assert.Greater(Vector3.Distance(cue.Position, start), 0.2f, "The intro breaks the rack with real physics");
            intro.Finish();
            yield return null;
            Assert.IsNull(Object.FindAnyObjectByType<IntroDirector>());
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsTrue(menu.blocksRaycasts, "Menu usable after the intro");
        }
    }
}
