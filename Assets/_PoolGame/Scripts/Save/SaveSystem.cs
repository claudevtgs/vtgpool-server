using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine;

namespace VTG.Pool.Save
{
    /// <summary>Player-editable preferences (MASTER_PROMPT section 37). Plain data, JSON serialisable.</summary>
    [Serializable]
    public sealed class GameSettings
    {
        public float masterVolume = 1f;
        public float effectsVolume = 0.9f;
        public float musicVolume = 0.18f;
        public float ambienceVolume = 0.25f;

        /// <summary>0 Low, 1 Medium, 2 High, 3 Ultra; -1 = choose by platform.</summary>
        public int graphicsTier = -1;

        /// <summary>CameraMode index used when a match starts (0 Cue, 1 Tactical, 2 Top).</summary>
        public int defaultCamera;

        /// <summary>AimAssistLevel index (0 None .. 3 Training).</summary>
        public int aimAssist = 3;

        /// <summary>Multiplier on mouse/touch aim and look sensitivity.</summary>
        public float aimSensitivity = 1f;

        /// <summary>AIDifficulty index preselected in menus.</summary>
        public int aiDifficulty = 1;

        public string playerName = "Player 1";
        public string secondPlayerName = "Player 2";

        /// <summary>On-screen touch controls: -1 = auto (touch devices), 0 = off, 1 = on.</summary>
        public int touchControls = -1;

        /// <summary>Puts the touch power slider and fine-aim strip on the left side of the screen.</summary>
        public bool leftHanded;

        /// <summary>Interface language: -1 = device language, 0 = English, 1 = Vietnamese.</summary>
        public int language = -1;

        /// <summary>Cue-ball placement speed relative to the pointer (ball in hand).</summary>
        public float placementSensitivity = 0.7f;

        /// <summary>Mouse shots: true = stroke the cue with the mouse while SPACE is held; false = hold SPACE to charge.</summary>
        public bool mouseStroke = true;

        /// <summary>Cinematic camera during shots (chase, pocket and break cams, short slow motion).</summary>
        public bool cinematicCamera = true;

        /// <summary>Slow-motion replay after pots: 0 every pot, 1 highlights only, 2 off.</summary>
        public int replayMode;

        /// <summary>Online room server (host name or ws/wss URL). Empty = the web page's own server (browser build).</summary>
        public string serverUrl = string.Empty;

        public GameSettings Clone() => (GameSettings)MemberwiseClone();

        public void Sanitize()
        {
            masterVolume = Mathf.Clamp01(masterVolume);
            effectsVolume = Mathf.Clamp01(effectsVolume);
            musicVolume = Mathf.Clamp01(musicVolume);
            ambienceVolume = Mathf.Clamp01(ambienceVolume);
            graphicsTier = Mathf.Clamp(graphicsTier, -1, 3);
            defaultCamera = Mathf.Clamp(defaultCamera, 0, 2);
            aimAssist = Mathf.Clamp(aimAssist, 0, 3);
            aimSensitivity = Mathf.Clamp(aimSensitivity, 0.1f, 3f);
            placementSensitivity = Mathf.Clamp(placementSensitivity, 0.1f, 2f);
            aiDifficulty = Mathf.Clamp(aiDifficulty, 0, 3);
            touchControls = Mathf.Clamp(touchControls, -1, 1);
            language = Mathf.Clamp(language, -1, 1);
            replayMode = Mathf.Clamp(replayMode, 0, 2);
            if (string.IsNullOrWhiteSpace(playerName)) playerName = "Player 1";
            if (string.IsNullOrWhiteSpace(secondPlayerName)) secondPlayerName = "Player 2";
        }
    }

    /// <summary>Lifetime statistics of the local (profile) player = match player index 0.</summary>
    [Serializable]
    public sealed class PlayerStats
    {
        public int matchesPlayed;
        public int wins;
        public int ballsPocketed;
        public int breaks;
        public int successfulBreaks;
        public int shotsInWonMatches;
        public int fouls;
        public int currentWinStreak;
        public int bestWinStreak;
        public int eightBallWins;
        public int nineBallWins;

        public float WinRate => matchesPlayed > 0 ? (float)wins / matchesPlayed : 0f;

        public float BreakSuccessRate => breaks > 0 ? (float)successfulBreaks / breaks : 0f;

        public float AverageShotsPerWin => wins > 0 ? (float)shotsInWonMatches / wins : 0f;
    }

    /// <summary>One finished online rack, as seen on this device.</summary>
    [Serializable]
    public sealed class OnlineMatchRecord
    {
        /// <summary>Local time, "yyyy-MM-dd HH:mm".</summary>
        public string time;
        public string room;
        public int mode;
        public bool teams;
        public string[] players = new string[0];
        public string winner;

        /// <summary>Racks won per side in this room after this rack, e.g. "2 - 1".</summary>
        public string score;

        /// <summary>1 won, 0 lost, -1 watched as a spectator.</summary>
        public int result;

        /// <summary>The rack ended because everyone else left.</summary>
        public bool forfeit;
    }

    [Serializable]
    public sealed class OnlineHistory
    {
        public const int Limit = 100;
        public List<OnlineMatchRecord> matches = new List<OnlineMatchRecord>();
    }

    /// <summary>
    /// Loads/saves <see cref="GameSettings"/> and <see cref="PlayerStats"/> as JSON in
    /// <c>Application.persistentDataPath</c>. Static and lazily loaded so any scene (including tests and the
    /// physics test scene) can read settings without a bootstrap object.
    /// </summary>
    public static class SaveSystem
    {
        private const string SettingsFile = "settings.json";
        private const string StatsFile = "stats.json";

        private static GameSettings settings;
        private static PlayerStats stats;

        /// <summary>Directory override for tests (null = persistentDataPath).</summary>
        public static string DirectoryOverride { get; set; }

        /// <summary>Raised after settings are changed through <see cref="UpdateSettings"/>.</summary>
        public static event Action<GameSettings> SettingsChanged;

        public static event Action<PlayerStats> StatsChanged;

        public static GameSettings Settings => settings ??= Load<GameSettings>(SettingsFile, s => s.Sanitize());

        public static PlayerStats Stats => stats ??= Load<PlayerStats>(StatsFile, null);

        /// <summary>Applies a modification, sanitises, saves and notifies listeners.</summary>
        /// <param name="persist">False for live previews (e.g. while dragging a slider); call <see cref="SaveSettings"/> later.</param>
        public static void UpdateSettings(Action<GameSettings> change, bool persist = true)
        {
            GameSettings current = Settings;
            change?.Invoke(current);
            current.Sanitize();
            if (persist)
            {
                Write(SettingsFile, current);
            }

            SettingsChanged?.Invoke(current);
        }

        public static void SaveSettings() => Write(SettingsFile, Settings);

        public static void UpdateStats(Action<PlayerStats> change)
        {
            PlayerStats current = Stats;
            change?.Invoke(current);
            Write(StatsFile, current);
            StatsChanged?.Invoke(current);
        }

        private const string HistoryFile = "online_history.json";
        private static OnlineHistory history;

        /// <summary>Finished online racks on this device, newest last.</summary>
        public static OnlineHistory History => history ??= Load<OnlineHistory>(HistoryFile, null);

        public static event Action<OnlineMatchRecord> HistoryAdded;

        public static void AddOnlineResult(OnlineMatchRecord record)
        {
            OnlineHistory current = History;
            current.matches.Add(record);
            if (current.matches.Count > OnlineHistory.Limit)
            {
                current.matches.RemoveRange(0, current.matches.Count - OnlineHistory.Limit);
            }

            Write(HistoryFile, current);
            HistoryAdded?.Invoke(record);
        }

        public static void ClearHistory()
        {
            History.matches.Clear();
            Write(HistoryFile, History);
        }

        public static void ResetStats() => UpdateStats(s => JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new PlayerStats()), s));

        /// <summary>Forgets cached data (tests, or after changing <see cref="DirectoryOverride"/>).</summary>
        public static void ClearCache()
        {
            settings = null;
            stats = null;
            history = null;
        }

        private static string PathFor(string file) => Path.Combine(DirectoryOverride ?? Application.persistentDataPath, file);

        private static T Load<T>(string file, Action<T> validate) where T : class, new()
        {
            var value = new T();
            try
            {
                string path = PathFor(file);
                if (File.Exists(path))
                {
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(path), value);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveSystem] Could not read {file}: {exception.Message}. Using defaults.");
                value = new T();
            }

            validate?.Invoke(value);
            return value;
        }

        private static void Write<T>(string file, T value)
        {
            try
            {
                string path = PathFor(file);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(value, true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveSystem] Could not write {file}: {exception.Message}");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SettingsChanged = null;
            StatsChanged = null;
            HistoryAdded = null;
        }
    }

    /// <summary>Hand-off of the match chosen in the main menu to the match scene.</summary>
    public static class MatchLaunch
    {
        public static bool HasRequest { get; private set; }
        public static Rules.GameMode Mode { get; private set; }
        public static bool VersusAI { get; private set; }
        public static int Difficulty { get; private set; }

        /// <summary>Free practice (no opponent, no rules); <see cref="Mode"/> is the rack shape.</summary>
        public static bool Practice { get; private set; }

        /// <summary>Online match through <see cref="Network.OnlineClient"/>; this device plays <see cref="LocalIndex"/>.</summary>
        public static bool Online { get; private set; }

        public static int LocalIndex { get; private set; }

        public static string[] Names { get; private set; }

        /// <summary>Online doubles: seats 0 &amp; 2 vs 1 &amp; 3.</summary>
        public static bool Teams { get; private set; }

        public static void RequestOnline(Rules.GameMode mode, int localIndex, string[] names, bool teams)
        {
            RequestOnline(mode, localIndex, names.Length > 0 ? names[0] : string.Empty, names.Length > 1 ? names[1] : string.Empty);
            Names = names;
            Teams = teams;
        }

        public static void RequestOnline(Rules.GameMode mode, int localIndex, string hostName, string guestName)
        {
            Teams = false;
            Mode = mode;
            VersusAI = false;
            Difficulty = 0;
            Practice = false;
            Online = true;
            LocalIndex = localIndex;
            Names = new[] { hostName, guestName };
            HasRequest = true;
        }

        public static void Request(Rules.GameMode mode, bool versusAI, int difficulty)
        {
            Mode = mode;
            VersusAI = versusAI;
            Difficulty = difficulty;
            Practice = false;
            Online = false;
            HasRequest = true;
        }

        public static void RequestPractice(Rules.GameMode rackMode)
        {
            Mode = rackMode;
            VersusAI = false;
            Difficulty = 0;
            Practice = true;
            Online = false;
            HasRequest = true;
        }

        /// <summary>Returns and clears the pending request.</summary>
        public static bool TryConsume(out Rules.GameMode mode, out bool versusAI, out int difficulty)
        {
            return TryConsume(out mode, out versusAI, out difficulty, out _);
        }

        public static bool TryConsume(out Rules.GameMode mode, out bool versusAI, out int difficulty, out bool practice)
        {
            mode = Mode;
            versusAI = VersusAI;
            difficulty = Difficulty;
            practice = Practice;
            bool had = HasRequest;
            HasRequest = false;
            return had;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => HasRequest = false;
    }
}
