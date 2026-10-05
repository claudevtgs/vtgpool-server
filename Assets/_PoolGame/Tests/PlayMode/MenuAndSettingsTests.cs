using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VTG.Pool.Aiming;
using VTG.Pool.Core;
using VTG.Pool.Inputs;
using VTG.Pool.Match;
using VTG.Pool.Presentation;
using VTG.Pool.Rules;
using VTG.Pool.Save;

namespace VTG.Pool.Tests
{
    /// <summary>Main menu flow, live settings, statistics and pause-menu navigation. Uses a temporary save folder.</summary>
    public sealed class MenuAndSettingsTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "VTGPoolMenuTests_" + System.Guid.NewGuid().ToString("N"));
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

        private static Button FindButton(string text)
        {
            return Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude)
                .FirstOrDefault(b => b.GetComponentInChildren<Text>() != null && b.GetComponentInChildren<Text>().text == text);
        }

        private static void Click(string text)
        {
            Button button = FindButton(text);
            Assert.IsNotNull(button, $"Button '{text}' not found");
            Assert.IsTrue(button.interactable, $"Button '{text}' is disabled");
            button.onClick.Invoke();
        }

        [UnityTest]
        public IEnumerator MainMenu_PlayNineBallVsAI_StartsRequestedMatch()
        {
            yield return Load("01_MainMenu");
            Assert.IsNotNull(Object.FindAnyObjectByType<UI.MainMenuController>());
            foreach (string label in new[] { "PLAY", "PRACTICE", "SETTINGS", "STATISTICS", "EXIT" })
            {
                Assert.IsNotNull(FindButton(label), $"Main menu button {label}");
            }

            Click("PLAY");
            yield return null;
            Assert.IsTrue(FindButton("ONLINE").interactable, "Online rooms are available");
            Click("8-Ball");          // cycles the game selector to 9-Ball
            Click("Intermediate");    // cycles AI level to Advanced
            Click("START");
            yield return null;
            yield return null;

            Assert.AreEqual("02_Match", SceneManager.GetActiveScene().name);
            var match = Object.FindAnyObjectByType<MatchManager>();
            Assert.AreEqual(GameMode.NineBall, match.State.Mode);
            Assert.IsTrue(match.VersusAI);
            Assert.AreEqual(PlayerKind.AI, match.State.Players[1].Kind);
            Assert.AreEqual(2, SaveSystem.Settings.aiDifficulty, "Chosen AI level is remembered");
        }

        [UnityTest]
        public IEnumerator Settings_ApplyLiveInMatch()
        {
            yield return Load("02_Match");
            var quality = Object.FindAnyObjectByType<GraphicsQualityManager>();
            var aim = Object.FindAnyObjectByType<AimSystem>();
            var input = Object.FindAnyObjectByType<PoolInputReader>();
            Assert.IsNotNull(quality);

            SaveSystem.UpdateSettings(s =>
            {
                s.graphicsTier = 0;
                s.aimAssist = 0;
                s.aimSensitivity = 2f;
                s.placementSensitivity = 0.4f;
            }, false);
            yield return null;
            Assert.AreEqual(0.4f, Object.FindAnyObjectByType<Cue.CueBallPlacementController>().PointerSensitivity, 1e-5f);

            Assert.AreEqual(GraphicsTier.Low, quality.Tier);
            Assert.AreEqual(AimAssistLevel.None, aim.AssistLevel);
            Assert.AreEqual(2f, input.SensitivityScale, 1e-5f);

            SaveSystem.UpdateSettings(s => s.graphicsTier = 3, false);
            Assert.AreEqual(GraphicsTier.Ultra, quality.Tier);
        }

        [UnityTest]
        public IEnumerator Stats_CountWonMatch()
        {
            yield return Load("02_Match");
            var turns = Object.FindAnyObjectByType<TurnManager>();
            Assert.IsNotNull(Object.FindAnyObjectByType<StatsTracker>(), "StatsTracker missing in 02_Match");
            turns.Apply(new ShotOutcome { GameOver = true, WinnerIndex = 0, Message = "test" }, new ShotRecord());
            yield return null;
            Assert.AreEqual(1, SaveSystem.Stats.matchesPlayed);
            Assert.AreEqual(1, SaveSystem.Stats.wins);
            Assert.AreEqual(1, SaveSystem.Stats.currentWinStreak);
        }

        [UnityTest]
        public IEnumerator PauseMenu_OpensSettingsAndReturnsToMainMenu()
        {
            yield return Load("02_Match");
            var hud = Object.FindAnyObjectByType<UI.PoolHud>();
            hud.SetPaused(true);
            yield return null;
            Click("Settings");
            yield return null;
            Assert.IsNotNull(GameObject.Find("SettingsOverlay"), "Settings overlay should open");
            Click("BACK");
            yield return null;
            Assert.IsNull(GameObject.Find("SettingsOverlay"));
            Assert.IsTrue(hud.IsPaused, "Back returns to the pause menu");
            Click("Main menu");
            yield return null;
            yield return null;
            Assert.AreEqual("01_MainMenu", SceneManager.GetActiveScene().name);
            Assert.AreEqual(1f, Time.timeScale, "Leaving the match unpauses time");
        }
    }
}
