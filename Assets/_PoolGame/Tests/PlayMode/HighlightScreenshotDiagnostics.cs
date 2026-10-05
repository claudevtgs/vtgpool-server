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
using VTG.Pool.Match;
using VTG.Pool.Replay;
using VTG.Pool.Rules;
using VTG.Pool.Save;
using VTG.Pool.Table;
using VTG.Pool.UI;

namespace VTG.Pool.Tests
{
    /// <summary>Captures replay / troll / combo / emblem frames to Temp/VTGPoolBridge/hl_*.png for review.</summary>
    public sealed class HighlightScreenshotDiagnostics
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "VTGPoolHlShots_" + System.Guid.NewGuid().ToString("N"));
            SaveSystem.DirectoryOverride = directory;
            SaveSystem.ClearCache();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ShotReplay.ForceDisabled = true;
            ShotCinematics.Enabled = true;
            Time.timeScale = 1f;
            PoolEvents.ClearAll();
            yield return SceneTestUtility.UnloadAllScenes();
            SaveSystem.DirectoryOverride = null;
            SaveSystem.ClearCache();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [UnityTest]
        public IEnumerator Capture_Highlights()
        {
            ShotReplay.ForceDisabled = false;
            ShotCinematics.Enabled = false;
            MatchLaunch.RequestPractice(GameMode.EightBall);
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            yield return null;
            var session = Object.FindAnyObjectByType<PracticeSession>();
            var shot = Object.FindAnyObjectByType<ShotController>();
            var table = Object.FindAnyObjectByType<TableBuilder>();
            Object.FindAnyObjectByType<CueBallPlacementController>().TryConfirm();
            foreach (PoolBall ball in BallRegistry.Balls.Where(b => !b.IsCueBall && b.BallId != 1 && b.BallId != 8).ToList()) session.RemoveBall(ball);
            Pocket pocket = table.PocketManager.Pockets.First(p => p.Kind == PocketKind.Corner);
            Vector3 toCenter = table.SurfaceCenter - pocket.Center;
            toCenter.y = 0f;
            toCenter.Normalize();
            float restY = table.SurfaceHeight + shot.CueBall.Radius;
            Vector3 one = new Vector3(pocket.Center.x, restY, pocket.Center.z) + toCenter * 0.4f;
            BallRegistry.Balls.First(b => b.BallId == 1).PlaceAt(one);
            shot.CueBall.PlaceAt(one + toCenter * 0.5f);
            session.LayoutChanged();
            yield return null;
            shot.ExecuteShot(new ShotParameters(-toCenter, 0.35f, new Vector2(0f, -0.4f)));
            float wait = Time.realtimeSinceStartup + 20f;
            while (!ShotReplay.IsPlaying && Time.realtimeSinceStartup < wait) yield return null;
            float replayStart = Time.realtimeSinceStartup;
            foreach (float at in new[] { 0.4f, 2.0f, 3.4f })
            {
                while (Time.realtimeSinceStartup - replayStart < at) yield return null;
                yield return UiScreenshotDiagnostics.CaptureFrame($"hl_replay_{at:0.0}s.png", 1920, 1080);
            }

            if (ShotReplay.Instance != null) ShotReplay.Instance.Stop();
            var hud = Object.FindAnyObjectByType<PoolHud>();
            hud.Highlights.ShowTroll();
            float troll = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - troll < 1.2f) yield return null;
            yield return UiScreenshotDiagnostics.CaptureFrame("hl_troll.png", 1920, 1080);
            while (hud.Highlights.TrollShowing) yield return null;
            hud.Highlights.ShowCombo(3);
            float combo = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - combo < 0.35f) yield return null;
            yield return UiScreenshotDiagnostics.CaptureFrame("hl_combo.png", 1920, 1080);

            // Deciding 8 in a match: emblem on the victory title.
            MatchLaunch.Request(GameMode.EightBall, false, 1);
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            yield return null;
            var turns = Object.FindAnyObjectByType<TurnManager>();
            turns.State.IsBreakShot = false;
            PoolBall cue = BallRegistry.FindCueBall();
            PoolBall eight = BallRegistry.Balls.First(b => b.BallId == 8);
            var record = new ShotRecord { FirstObjectBallHit = 8 };
            PoolEvents.RaiseShotStarted(record);
            PoolEvents.RaiseBallHit(new BallHitInfo(cue, eight, 1f, cue.Position));
            PoolEvents.RaiseBallPocketed(eight, Object.FindAnyObjectByType<TableBuilder>().PocketManager.Pockets[0]);
            record.BallsPocketed.Add(8);
            turns.Apply(new ShotOutcome { GameOver = true, WinnerIndex = turns.State.CurrentPlayerIndex, Message = "8" }, record);
            float won = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - won < 0.7f) yield return null;
            yield return UiScreenshotDiagnostics.CaptureFrame("hl_emblem8.png", 1920, 1080);
        }

        [UnityTest]
        public IEnumerator Capture_FoulMissAndTeams()
        {
            MatchLaunch.Request(GameMode.EightBall, false, 1);
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            yield return null;
            var hud = Object.FindAnyObjectByType<PoolHud>();
            hud.Highlights.ShowFoul(FoulType.Scratch);
            float t = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t < 0.3f) yield return null;
            yield return UiScreenshotDiagnostics.CaptureFrame("fx_foul.png", 1920, 1080);
            while (hud.Highlights.StampShowing) yield return null;
            hud.Highlights.ShowMiss();
            t = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t < 0.5f) yield return null;
            yield return UiScreenshotDiagnostics.CaptureFrame("fx_miss.png", 1920, 1080);
            foreach (HighlightPresenter.Gag gag in new[] { HighlightPresenter.Gag.Ghost, HighlightPresenter.Gag.Tumbleweed, HighlightPresenter.Gag.Sweat, HighlightPresenter.Gag.RainCloud, HighlightPresenter.Gag.Toang })
            {
                hud.Highlights.ShowGag(gag, 3);
                t = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t < 1.1f) yield return null;
                yield return UiScreenshotDiagnostics.CaptureFrame("gag_" + gag + ".png", 1920, 1080);
                while (hud.Highlights.GagShowing) yield return null;
            }

            MatchLaunch.RequestOnline(GameMode.EightBall, 0, new[] { "An", "Binh", "Cuong", "Dung" }, true);
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return UiScreenshotDiagnostics.CaptureFrame("online_2v2.png", 1920, 1080);

            MatchLaunch.RequestOnline(GameMode.NineBall, 2, new[] { "An", "Binh", "Cuong", "Dung" }, false);
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return UiScreenshotDiagnostics.CaptureFrame("online_ffa4.png", 1920, 1080);

            // Spectator view (no connection needed to look at the HUD).
            MatchLaunch.RequestOnline(GameMode.EightBall, -1, new[] { "An", "Binh" }, false);
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            for (int i = 0; i < 20; i++) yield return null;
            yield return UiScreenshotDiagnostics.CaptureFrame("online_spectator.png", 1920, 1080);

            // Online history.
            SaveSystem.AddOnlineResult(new OnlineMatchRecord { time = "2026-10-05 20:15", room = "K7P2QX", mode = 0, players = new[] { "An", "Binh" }, winner = "An", score = "2 - 1", result = 1 });
            SaveSystem.AddOnlineResult(new OnlineMatchRecord { time = "2026-10-05 20:41", room = "Q9WM3T", mode = 1, teams = true, players = new[] { "An", "Binh", "Cuong", "Dung" }, winner = "Binh & Dung", score = "0 - 1", result = 0 });
            SaveSystem.AddOnlineResult(new OnlineMatchRecord { time = "2026-10-05 21:02", room = "Z2KD8P", mode = 1, players = new[] { "Hai", "Lan", "Minh" }, winner = "Lan", score = "Hai 0 · Lan 1 · Minh 0", result = -1, forfeit = true });
            SceneManager.LoadScene("01_MainMenu");
            yield return null;
            yield return null;
            IntroDirector intro = Object.FindAnyObjectByType<IntroDirector>();
            if (intro != null) intro.Finish();
            // The real path: STATISTICS → ONLINE HISTORY.
            Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude).First(b => b.name == "Button_STATISTICS").onClick.Invoke();
            yield return null;
            Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude).First(b => b.name == "Button_ONLINE HISTORY").onClick.Invoke();
            yield return null;
            yield return UiScreenshotDiagnostics.CaptureFrame("online_history.png", 1920, 1080);
            Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude).First(b => b.name == "Button_BACK" && b.transform.parent.parent.name == "HistoryOverlay").onClick.Invoke();
            yield return null;
            Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude).First(b => b.name == "Button_BACK" && b.transform.parent.parent.name == "StatsOverlay").onClick.Invoke();
            yield return null;
            Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude).First(b => b.name == "Button_PLAY").onClick.Invoke();
            yield return null;
            Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude).First(b => b.name == "Button_ONLINE").onClick.Invoke();
            yield return null;
            yield return UiScreenshotDiagnostics.CaptureFrame("online_lobby.png", 1920, 1080);
        }
    }
}
