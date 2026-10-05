using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Match;
using VTG.Pool.Simulation.Testing;

namespace VTG.Pool.Tests
{
    /// <summary>
    /// Loads 03_PhysicsTest and runs every preset through the real scene wiring
    /// (PhysicsTestController -> ShotController -> PoolPhysicsSystem). Writes a report to
    /// Temp/VTGPoolBridge/scene-presets.txt.
    /// </summary>
    public sealed class PhysicsTestSceneTests
    {
        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            PoolEvents.ClearAll();
            yield return SceneTestUtility.UnloadAllScenes();
        }

        private const string SceneName = "03_PhysicsTest";

        [UnityTest]
        public IEnumerator AllPresets_CompleteAndReturnToAiming()
        {
            if (!Application.CanStreamedLevelBeLoaded(SceneName))
            {
                Assert.Ignore("03_PhysicsTest is not in the build settings (run VTG Pool/Setup).");
            }

            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;

            var controller = Object.FindAnyObjectByType<PhysicsTestController>();
            var shot = Object.FindAnyObjectByType<ShotController>();
            Assert.IsNotNull(controller, "PhysicsTestController missing");
            Assert.IsNotNull(shot, "ShotController missing");
            Assert.IsNotNull(Camera.main, "Main camera missing");

            ShotRecord finished = null;
            PoolEvents.BallsStopped += record => finished = record;
            float previousScale = Time.timeScale;
            Time.timeScale = 4f;
            var report = new StringBuilder();

            try
            {
                for (int i = 0; i < controller.Presets.Count; i++)
                {
                    finished = null;
                    controller.Run(i, true);
                    float timeout = Time.realtimeSinceStartup + 30f;
                    while (finished == null && Time.realtimeSinceStartup < timeout)
                    {
                        yield return null;
                    }

                    string name = controller.Presets[i].name;
                    Assert.IsNotNull(finished, $"Preset '{name}' did not finish (balls never came to rest)");
                    yield return null;
                    Assert.AreEqual(ShotPhase.Aiming, shot.Phase, $"Preset '{name}' did not return to Aiming");
                    report.AppendLine($"{name}: {finished} cueRest={shot.CueBall.Position:F3}");
                }
            }
            finally
            {
                Time.timeScale = previousScale;
                PoolEvents.ClearAll();
                Directory.CreateDirectory("Temp/VTGPoolBridge");
                File.WriteAllText("Temp/VTGPoolBridge/scene-presets.txt", report.ToString());
            }
        }
    }
}
