using System.Collections;
using System.IO;
using System.Text;
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

namespace VTG.Pool.Tests
{
    /// <summary>Temporary diagnostics: traces the AI kick scenario to Temp/VTGPoolBridge/trace-kick.txt. Always passes.</summary>
    [Category("Diagnostics")]
    public sealed class KickDiagnostics
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            PoolEvents.ClearAll();
            yield return SceneTestUtility.UnloadAllScenes();
        }

        [UnityTest]
        public IEnumerator TraceKick()
        {
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            yield return null;
            var match = Object.FindAnyObjectByType<MatchManager>();
            var shot = Object.FindAnyObjectByType<ShotController>();
            var placement = Object.FindAnyObjectByType<CueBallPlacementController>();
            var log = new StringBuilder();
            ShotRecord finished = null;
            PoolEvents.BallsStopped += r => finished = r;
            PoolEvents.CushionHit += i => log.AppendLine($"  CUSHION ball {i.Ball.BallId} {i.Surface.name} ({i.Surface.Kind}) vn={i.NormalSpeed:F2} at {i.Point:F3}");
            PoolEvents.BallHit += i => log.AppendLine($"  HIT {i.A.BallId}-{i.B.BallId} at {i.Point:F3}");
            match.StartMatch(GameMode.NineBall, true, AIDifficulty.Expert);
            yield return null;
            Time.timeScale = 0f;
            match.State.IsBreakShot = false;
            match.State.SetBallsOnTable(new[] { 1, 9 });
            var all = BallRegistry.Balls;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                if (!all[i].IsCueBall && all[i].BallId != 1 && all[i].BallId != 9) all[i].gameObject.SetActive(false);
            }

            placement.Clear();
            float y = shot.Table.SurfaceHeight + shot.CueBall.Radius;
            shot.CueBall.PlaceAt(new Vector3(0.2f, y, -0.5f));
            BallRegistry.FindById(1).PlaceAt(new Vector3(-0.2f, y, 0.5f));
            BallRegistry.FindById(9).PlaceAt(new Vector3(0f, y, 0f));
            var ai = match.GetComponent<AIController>();
            bool planned = ai.TryPlan(out var plan);
            log.AppendLine($"planned={planned} kick={ai.LastPlanWasKick} shot={plan} timeScale={Time.timeScale} phase={shot.Phase} canShoot={shot.CanShoot} blocked={shot.ShotInputBlocked}");
            ai.enabled = false;
            Time.timeScale = 4f;
            bool executed = shot.ExecuteShot(plan);
            log.AppendLine($"executed={executed} v={shot.CueBall.LinearVelocity:F3} w={shot.CueBall.AngularVelocity:F1}");
            float deadline = Time.realtimeSinceStartup + 20f;
            int frame = 0;
            while (finished == null && Time.realtimeSinceStartup < deadline)
            {
                if (frame++ % 10 == 0)
                {
                    log.AppendLine($"t={Time.time:F2} cue={shot.CueBall.Position:F3} v={shot.CueBall.LinearVelocity:F2} state={shot.CueBall.CurrentState}");
                }

                yield return null;
            }

            log.AppendLine($"finished: {finished}");
            Directory.CreateDirectory("Temp/VTGPoolBridge");
            File.WriteAllText("Temp/VTGPoolBridge/trace-kick.txt", log.ToString());
        }
    }
}
