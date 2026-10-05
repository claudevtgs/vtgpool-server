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
using VTG.Pool.Save;
using VTG.Pool.UI;

namespace VTG.Pool.Tests
{
    /// <summary>Captures intro frames and shot-cinematic frames to Temp/VTGPoolBridge/intro_*.png / cine_*.png for review.</summary>
    public sealed class IntroScreenshotDiagnostics
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "VTGPoolIntroShots_" + System.Guid.NewGuid().ToString("N"));
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
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [UnityTest]
        public IEnumerator Capture_IntroAndShotCinematics()
        {
            IntroDirector.PlayOnNextMenu = true;
            yield return SceneManager.LoadSceneAsync("01_MainMenu", LoadSceneMode.Single);
            float start = Time.realtimeSinceStartup;
            foreach (float at in new[] { 0.7f, 2.2f, 3.3f, 4.0f, 5.6f, 6.5f, 7.6f })
            {
                while (Time.realtimeSinceStartup - start < at) yield return null;
                yield return UiScreenshotDiagnostics.CaptureFrame($"intro_{at:0.0}s.png", 1920, 1080);
            }

            IntroDirector intro = Object.FindAnyObjectByType<IntroDirector>();
            if (intro != null) intro.Finish();

            MatchLaunch.RequestPractice(Rules.GameMode.EightBall);
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            yield return null;
            Object.FindAnyObjectByType<CueBallPlacementController>().TryConfirm();
            var shot = Object.FindAnyObjectByType<ShotController>();
            PoolBall apex = BallRegistry.Balls.Where(b => !b.IsCueBall).OrderBy(b => Vector3.Distance(b.Position, Object.FindAnyObjectByType<Table.TableBuilder>().FootSpot)).First();
            yield return null;
            shot.ExecuteShot(new ShotParameters(apex.Position - shot.CueBall.Position, 1f, Vector2.zero));
            float shotStart = Time.realtimeSinceStartup;
            foreach (float at in new[] { 0.15f, 0.5f, 1.2f, 2.5f, 3.6f })
            {
                while (Time.realtimeSinceStartup - shotStart < at) yield return null;
                yield return UiScreenshotDiagnostics.CaptureFrame($"cine_break_{at:0.00}s.png", 1920, 1080);
            }

            // Victory moment: a won rack in a normal match.
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            yield return null;
            var turns = Object.FindAnyObjectByType<TurnManager>();
            turns.Apply(new Rules.ShotOutcome { GameOver = true, WinnerIndex = 0, Message = "Player 1 pocketed the 8. Player 1 wins!" }, new ShotRecord());
            float won = Time.realtimeSinceStartup;
            foreach (float at in new[] { 0.3f, 1.2f, 2.4f, 3.6f })
            {
                while (Time.realtimeSinceStartup - won < at) yield return null;
                yield return UiScreenshotDiagnostics.CaptureFrame($"victory_{at:0.0}s.png", 1920, 1080);
            }
        }
    }
}
