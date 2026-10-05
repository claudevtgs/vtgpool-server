using System.IO;
using NUnit.Framework;
using UnityEngine;
using VTG.Pool.Rules;
using VTG.Pool.Save;

namespace VTG.Pool.Tests
{
    public sealed class SaveSystemTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "VTGPoolSaveTests_" + System.Guid.NewGuid().ToString("N"));
            SaveSystem.DirectoryOverride = directory;
            SaveSystem.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.DirectoryOverride = null;
            SaveSystem.ClearCache();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void Defaults_AreUsedWhenNoFileExists()
        {
            GameSettings s = SaveSystem.Settings;
            Assert.AreEqual(1f, s.masterVolume);
            Assert.AreEqual(-1, s.graphicsTier, "Auto graphics by default");
            Assert.AreEqual(3, s.aimAssist, "Training assist by default");
        }

        [Test]
        public void Settings_RoundTripThroughDisk()
        {
            SaveSystem.UpdateSettings(s =>
            {
                s.musicVolume = 0.42f;
                s.graphicsTier = 1;
                s.defaultCamera = 2;
                s.playerName = "Phu";
            });
            SaveSystem.ClearCache();
            GameSettings loaded = SaveSystem.Settings;
            Assert.AreEqual(0.42f, loaded.musicVolume, 1e-5f);
            Assert.AreEqual(1, loaded.graphicsTier);
            Assert.AreEqual(2, loaded.defaultCamera);
            Assert.AreEqual("Phu", loaded.playerName);
        }

        [Test]
        public void PreviewUpdates_DoNotWriteUntilSaved()
        {
            SaveSystem.UpdateSettings(s => s.effectsVolume = 0.3f, false);
            Assert.IsFalse(File.Exists(Path.Combine(directory, "settings.json")));
            SaveSystem.SaveSettings();
            Assert.IsTrue(File.Exists(Path.Combine(directory, "settings.json")));
        }

        [Test]
        public void Settings_AreSanitised()
        {
            SaveSystem.UpdateSettings(s =>
            {
                s.masterVolume = 3f;
                s.graphicsTier = 9;
                s.aimSensitivity = 0f;
                s.playerName = "  ";
            });
            GameSettings s2 = SaveSystem.Settings;
            Assert.AreEqual(1f, s2.masterVolume);
            Assert.AreEqual(3, s2.graphicsTier);
            Assert.AreEqual(0.1f, s2.aimSensitivity, 1e-5f);
            Assert.AreEqual("Player 1", s2.playerName);
        }

        [Test]
        public void CorruptFile_FallsBackToDefaults()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "settings.json"), "{ not json");
            LogAssertIgnore();
            Assert.AreEqual(1f, SaveSystem.Settings.masterVolume);
        }

        [Test]
        public void SettingsChanged_IsRaised()
        {
            int calls = 0;
            void Handler(GameSettings s) => calls++;
            SaveSystem.SettingsChanged += Handler;
            SaveSystem.UpdateSettings(s => s.aimAssist = 1);
            SaveSystem.SettingsChanged -= Handler;
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void Stats_RatesAndReset()
        {
            SaveSystem.UpdateStats(s =>
            {
                s.matchesPlayed = 4;
                s.wins = 3;
                s.breaks = 5;
                s.successfulBreaks = 2;
                s.shotsInWonMatches = 30;
            });
            PlayerStats stats = SaveSystem.Stats;
            Assert.AreEqual(0.75f, stats.WinRate, 1e-5f);
            Assert.AreEqual(0.4f, stats.BreakSuccessRate, 1e-5f);
            Assert.AreEqual(10f, stats.AverageShotsPerWin, 1e-5f);
            SaveSystem.ResetStats();
            SaveSystem.ClearCache();
            Assert.AreEqual(0, SaveSystem.Stats.matchesPlayed);
        }

        [Test]
        public void MatchLaunch_IsConsumedOnce()
        {
            MatchLaunch.Request(GameMode.NineBall, true, 3);
            Assert.IsTrue(MatchLaunch.TryConsume(out GameMode mode, out bool vsAI, out int difficulty));
            Assert.AreEqual(GameMode.NineBall, mode);
            Assert.IsTrue(vsAI);
            Assert.AreEqual(3, difficulty);
            Assert.IsFalse(MatchLaunch.TryConsume(out _, out _, out _));
        }

        private static void LogAssertIgnore() => UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
    }
}
