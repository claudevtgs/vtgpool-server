using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VTG.Pool.Aiming;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Match;
using VTG.Pool.Save;
using VTG.Pool.UI;

namespace VTG.Pool.Tests
{
    /// <summary>On-screen touch controls in 02_Match: visibility setting, pull-to-shoot slider, fine aim, tap to aim, ball in hand.</summary>
    public sealed class TouchControlsTests
    {
        private string directory;
        private PoolHud hud;
        private ShotController shot;
        private CueBallPlacementController placement;
        private AimSystem aim;
        private ShotRecord started;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "VTGPoolTouchTests_" + System.Guid.NewGuid().ToString("N"));
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

        private IEnumerator LoadMatch(int touchSetting, bool leftHanded = false)
        {
            if (!Application.CanStreamedLevelBeLoaded("02_Match"))
            {
                Assert.Ignore("02_Match is not in the build settings (run VTG Pool/Setup).");
            }

            SaveSystem.UpdateSettings(s =>
            {
                s.touchControls = touchSetting;
                s.leftHanded = leftHanded;
            }, false);
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            yield return null;
            hud = Object.FindAnyObjectByType<PoolHud>();
            shot = Object.FindAnyObjectByType<ShotController>();
            placement = Object.FindAnyObjectByType<CueBallPlacementController>();
            aim = Object.FindAnyObjectByType<AimSystem>();
            Assert.IsNotNull(hud.TouchControls, "Touch controls are built with the HUD");
            PoolEvents.ShotStarted += record => started = record;
        }

        [UnityTest]
        public IEnumerator TouchSetting_TogglesControlsAndScale()
        {
            yield return LoadMatch(0);
            Assert.IsFalse(hud.TouchControls.Visible, "Off: no on-screen controls");
            CanvasScaler scaler = hud.GetComponentInChildren<CanvasScaler>();
            Assert.AreEqual(1920f, scaler.referenceResolution.x);

            SaveSystem.UpdateSettings(s => s.touchControls = 1, false);
            yield return null;
            Assert.IsTrue(hud.TouchControls.Visible, "On: controls appear live");
            Assert.AreEqual(1600f, scaler.referenceResolution.x, "Touch layout uses larger UI");
            RectTransform slider = (RectTransform)hud.TouchControls.PowerSlider.transform;
            Assert.AreEqual(1f, slider.anchorMin.x, "Right-handed: slider on the right");

            SaveSystem.UpdateSettings(s => s.leftHanded = true, false);
            yield return null;
            Assert.AreEqual(0f, slider.anchorMin.x, "Left-handed: slider on the left");
        }

        [UnityTest]
        public IEnumerator PowerSlider_PullAndReleaseShoots()
        {
            yield return LoadMatch(1);
            Assert.IsTrue(placement.TryConfirm());
            TouchPowerSlider slider = hud.TouchControls.PowerSlider;

            // A tiny pull cancels.
            Assert.IsTrue(slider.Press(0f));
            Assert.AreEqual(ShotPhase.PowerSelection, shot.Phase);
            slider.Release();
            Assert.AreEqual(ShotPhase.Aiming, shot.Phase);

            // Sliding off sideways cancels.
            Assert.IsTrue(slider.Press(0f));
            slider.Drag(0f, -slider.Travel * 0.4f);
            slider.Drag(2000f, -slider.Travel * 0.4f);
            Assert.AreEqual(ShotPhase.Aiming, shot.Phase);
            Assert.IsNull(started);

            // Pull to half power and let go.
            Assert.IsTrue(slider.Press(10f));
            slider.Drag(0f, 10f - slider.Travel * 0.5f);
            Assert.AreEqual(0.5f, shot.CurrentPower, 1e-3f);
            yield return null;
            slider.Release();
            float timeout = Time.realtimeSinceStartup + 3f;
            while (started == null && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.IsNotNull(started, "Releasing the slider plays the shot");
            Assert.AreEqual(0.5f, started.Power, 1e-3f);
        }

        [UnityTest]
        public IEnumerator FineAimStrip_And_TapToAim()
        {
            yield return LoadMatch(1);
            TouchAimStrip strip = hud.TouchControls.AimStrip;
            Assert.IsFalse(strip.Turn(50f), "No aiming while placing the cue ball");
            Assert.IsTrue(placement.TryConfirm());

            float yaw = aim.AimYawDegrees;
            Assert.IsTrue(strip.Turn(100f));
            Assert.AreEqual(Mathf.DeltaAngle(yaw, yaw + 100f * strip.DegreesPerUnit), Mathf.DeltaAngle(yaw, aim.AimYawDegrees), 1e-3f);

            PoolBall target = null;
            foreach (PoolBall ball in BallRegistry.Balls)
            {
                if (!ball.IsCueBall && (target == null || ball.BallId < target.BallId))
                {
                    target = ball;
                }
            }

            Object.FindAnyObjectByType<CameraSystem.CameraController>().SetMode(CameraSystem.CameraMode.Top);
            yield return null;
            yield return null;
            Vector2 screen = Camera.main.WorldToScreenPoint(target.Position);
            Assert.IsTrue(hud.TouchControls.TryAimAtScreenPoint(screen));
            Vector3 expected = target.Position - shot.CueBall.Position;
            expected.y = 0f;
            Assert.Greater(Vector3.Dot(aim.AimDirection, expected.normalized), 0.999f, "Tap aims through the tapped point");
        }

        [UnityTest]
        public IEnumerator BallInHand_PlaceButtonAppearsAndPlaces()
        {
            yield return LoadMatch(1);
            yield return null;
            Assert.IsTrue(placement.IsPlacing);
            Button place = FindActiveButton("Button_PLACE");
            Assert.IsNotNull(place, "PLACE button shown while placing the cue ball");
            place.onClick.Invoke();
            Assert.IsFalse(placement.IsPlacing);
            yield return null;
            Assert.IsNull(FindActiveButton("Button_PLACE"));
            Assert.IsNotNull(FindActiveButton("Button_MOVE CUE BALL"), "Cue ball can be picked up again before the shot");
        }

        [UnityTest]
        public IEnumerator SpinEditor_OpensFromPadAndEditsSpinAndAngle()
        {
            yield return LoadMatch(1);
            var spin = Object.FindAnyObjectByType<CueBallSpinController>();
            hud.OpenSpinEditor();
            Assert.IsNull(hud.SpinEditor, "Not while the cue ball is being placed");
            Assert.IsTrue(placement.TryConfirm());
            yield return null;
            Assert.IsNotNull(hud.SpinPad.OpenEditor, "Touch layout: the small pad opens the editor");
            hud.SpinPad.OpenEditor();
            Assert.IsNotNull(hud.SpinEditor);

            FindActiveButton("Button_Draw").onClick.Invoke();
            Assert.AreEqual(new Vector2(0f, -0.6f), spin.TipOffset);
            FindActiveButton("Button_+5°").onClick.Invoke();
            FindActiveButton("Button_+5°").onClick.Invoke();
            FindActiveButton("Button_+1°").onClick.Invoke();
            Assert.AreEqual(11f, spin.Elevation, 1e-4f);
            FindActiveButton("Button_Level").onClick.Invoke();
            Assert.AreEqual(0f, spin.Elevation);
            hud.SpinEditor.SetElevation(65f);
            FindActiveButton("Button_DONE").onClick.Invoke();
            yield return null;
            Assert.IsNull(hud.SpinEditor, "DONE closes the editor");
            Assert.AreEqual(65f, spin.Elevation, 1e-4f, "Values are kept for the shot");

            SaveSystem.UpdateSettings(s => s.touchControls = 0, false);
            yield return null;
            Assert.IsNull(hud.SpinPad.OpenEditor, "Desktop: the pad edits in place");
        }

        private static Button FindActiveButton(string name)
        {
            foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude))
            {
                if (button.name == name)
                {
                    return button;
                }
            }

            return null;
        }
    }
}
