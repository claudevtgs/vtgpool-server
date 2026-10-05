using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Match;
using VTG.Pool.Save;
using VTG.Pool.UI;

namespace VTG.Pool.Tests
{
    /// <summary>
    /// Renders the HUD (desktop and phone-shaped touch layouts) to Temp/VTGPoolBridge/ui_*.png for visual review.
    /// Overlay canvases are temporarily switched to camera space so they appear in an off-screen render.
    /// </summary>
    public sealed class UiScreenshotDiagnostics
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "VTGPoolShots_" + System.Guid.NewGuid().ToString("N"));
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

        [UnityTest]
        public IEnumerator Capture_PracticeAndTouchLayouts()
        {
            if (!Application.CanStreamedLevelBeLoaded("02_Match"))
            {
                Assert.Ignore("02_Match is not in the build settings.");
            }

            SaveSystem.UpdateSettings(s => s.touchControls = 0, false);
            MatchLaunch.RequestPractice(Rules.GameMode.EightBall);
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            for (int i = 0; i < 30; i++) yield return null;
            var placement = Object.FindAnyObjectByType<CueBallPlacementController>();
            placement.TryConfirm();
            for (int i = 0; i < 20; i++) yield return null;
            yield return Capture("ui_practice_desktop.png", 1920, 1080);

            SaveSystem.UpdateSettings(s => s.touchControls = 1, false);
            Object.FindAnyObjectByType<PracticeLayoutEditor>().SetEditing(true);
            Object.FindAnyObjectByType<PracticeSession>().RemoveBall(FindBall(5));
            for (int i = 0; i < 40; i++) yield return null;
            yield return Capture("ui_practice_touch_arrange.png", 2400, 1080);
            Object.FindAnyObjectByType<PracticeLayoutEditor>().SetEditing(false);

            var shot = Object.FindAnyObjectByType<ShotController>();
            var hud = Object.FindAnyObjectByType<PoolHud>();
            for (int i = 0; i < 40; i++) yield return null;
            Assert.IsTrue(hud.TouchControls.PowerSlider.Press(0f));
            hud.TouchControls.PowerSlider.Drag(0f, -hud.TouchControls.PowerSlider.Travel * 0.6f);
            for (int i = 0; i < 5; i++) yield return null;
            yield return Capture("ui_touch_power.png", 2400, 1080);
            hud.TouchControls.PowerSlider.Cancel();
            Assert.AreEqual(ShotPhase.Aiming, shot.Phase);

            hud.SetPaused(true);
            yield return null;
            foreach (UnityEngine.UI.Button button in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude))
            {
                if (button.name == "Settings")
                {
                    button.onClick.Invoke();
                    break;
                }
            }

            yield return null;
            yield return Capture("ui_settings_touch.png", 2400, 1080);
            hud.SetPaused(false);

            // Vietnamese HUD with a right-english massé: the guide shows the predicted curved path.
            Localization.Loc.Override = Localization.Language.Vietnamese;
            Localization.Loc.Refresh();
            SaveSystem.UpdateSettings(s => s.touchControls = 0, false);
            var spin = Object.FindAnyObjectByType<CueBallSpinController>();
            spin.SetTipOffset(new Vector2(0.9f, 0f));
            spin.SetElevation(70f);
            Object.FindAnyObjectByType<CameraSystem.CameraController>().SetMode(CameraSystem.CameraMode.Top);
            var aim = Object.FindAnyObjectByType<Aiming.AimSystem>();
            aim.SetAimDirection(new Vector3(-0.35f, 0f, 1f));
            float settle = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < settle) yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<Aiming.AimSystem>().HasCurvedPath, "Massé shows a curved guide");
            yield return Capture("ui_masse_vi.png", 1920, 1080);
            hud.OpenSpinEditor();
            yield return null;
            yield return Capture("ui_spin_editor_vi.png", 2400, 1080);
            hud.SpinEditor.Close();

            yield return SceneManager.LoadSceneAsync("01_MainMenu", LoadSceneMode.Single);
            for (int i = 0; i < 60; i++) yield return null;
            yield return Capture("ui_menu_vi.png", 1920, 1080);
            Localization.Loc.Override = Localization.Language.English;
            Localization.Loc.Refresh();
        }

        private static Balls.PoolBall FindBall(int number)
        {
            foreach (Balls.PoolBall ball in Balls.BallRegistry.Balls)
            {
                if (ball.BallId == number) return ball;
            }

            return null;
        }

        private static IEnumerator Capture(string file, int width, int height) => CaptureFrame(file, width, height);

        public static IEnumerator CaptureFrame(string file, int width, int height)
        {
            yield return new WaitForEndOfFrame();
            Camera camera = Camera.main;
            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var canvases = new List<(Canvas canvas, RenderMode mode)>();
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvases.Add((canvas, canvas.renderMode));
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = camera.nearClipPlane + 0.01f;
                }
            }

            RenderTexture previousTarget = camera.targetTexture;
            camera.targetTexture = texture;
            foreach ((Canvas canvas, RenderMode _) in canvases)
            {
                ResponsiveCanvas responsive = canvas.GetComponent<ResponsiveCanvas>();
                if (responsive != null) responsive.Apply();
            }

            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            camera.targetTexture = previousTarget;
            foreach ((Canvas canvas, RenderMode mode) in canvases)
            {
                canvas.renderMode = mode;
                ResponsiveCanvas responsive = canvas.GetComponent<ResponsiveCanvas>();
                if (responsive != null) responsive.Apply();
            }

            Directory.CreateDirectory("Temp/VTGPoolBridge");
            File.WriteAllBytes(Path.Combine("Temp/VTGPoolBridge", file), image.EncodeToPNG());
            Object.Destroy(image);
            texture.Release();
            Object.Destroy(texture);
        }
    }
}
