using VTG.Pool.Localization;
using System;
using System.Collections.Generic;
using UnityEngine;
using VTG.Pool.Cue;
using VTG.Pool.AI;
using VTG.Pool.Rules;
using VTG.Pool.Rules.EightBall;
using VTG.Pool.Rules.NineBall;
using VTG.Pool.Rules.Practice;

namespace VTG.Pool.Match
{
    /// <summary>
    /// Sets up a match: players, rule set for the game mode, first rack and breaker. Restarts on request.
    /// Owns no turn or rule logic itself.
    /// </summary>
    public sealed class MatchManager : MonoBehaviour
    {
        [SerializeField] private GameMode mode = GameMode.EightBall;
        [SerializeField] private string[] playerNames = { "Player 1", "Player 2" };
        [SerializeField] private EightBallRulesConfig eightBallRules;
        [SerializeField] private NineBallRulesConfig nineBallRules;
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private ShotController shotController;
        [SerializeField] private bool startOnPlay = true;
        [SerializeField, Tooltip("Alternate the breaker between consecutive matches.")]
        private bool alternateBreak = true;

        private int nextBreaker;
        private bool practice;
        private int onlineLocalIndex = -1;
        private string[] onlineNames;
        private bool onlineTeams;

        /// <summary>Online match: index of the player on this device (-1 offline).</summary>
        public int OnlineLocalIndex => onlineLocalIndex;

        public bool IsOnline => onlineLocalIndex >= 0 || onlineSpectator;

        /// <summary>Online: this device only watches (every player is remote, <see cref="OnlineLocalIndex"/> is -1).</summary>
        public bool IsSpectator => onlineSpectator;

        private bool onlineSpectator;
        [SerializeField] private bool versusAI;
        [SerializeField] private AIProfile aiProfile;
        private AIProfile runtimeAIProfile;
        private AIController aiController;
        public bool VersusAI => versusAI;
        public AIDifficulty Difficulty => runtimeAIProfile != null ? runtimeAIProfile.difficulty
            : aiProfile != null ? aiProfile.difficulty : AIDifficulty.Intermediate;

        public void StartMatch(GameMode newMode, bool playAgainstAI, AIDifficulty difficulty = AIDifficulty.Intermediate)
        {
            practice = false;
            onlineLocalIndex = -1;
            onlineSpectator = false;
            versusAI = playAgainstAI;
            EnsureAI();
            runtimeAIProfile.difficulty = difficulty;
            StartMatch(newMode);
        }

        private void EnsureAI()
        {
            if (runtimeAIProfile == null)
                runtimeAIProfile = aiProfile != null ? Instantiate(aiProfile) : ScriptableObject.CreateInstance<AIProfile>();
            if (aiController == null)
                aiController = GetComponent<AIController>() ?? gameObject.AddComponent<AIController>();
            aiController.Configure(this, shotController, runtimeAIProfile);
        }

        private void OnDestroy()
        {
            if (runtimeAIProfile != null) Destroy(runtimeAIProfile);
        }

        public GameMode Mode => mode;

        /// <summary>Free practice is running (no opponent, <see cref="PracticeRuleSet"/>).</summary>
        public bool IsPractice => practice;

        public IRuleSet Rules { get; private set; }

        public MatchState State => turnManager != null ? turnManager.State : null;

        public TurnManager TurnManager => turnManager;

        public event Action MatchStarted;

        private void Start()
        {
            // A match chosen in the main menu takes precedence over the scene defaults.
            if (Save.MatchLaunch.HasRequest && Save.MatchLaunch.Online)
            {
                int local = Save.MatchLaunch.LocalIndex;
                string[] names = Save.MatchLaunch.Names;
                bool teams = Save.MatchLaunch.Teams;
                Save.MatchLaunch.TryConsume(out GameMode onlineMode, out _, out _);
                StartOnline(onlineMode, local, names, teams);
                return;
            }

            if (Save.MatchLaunch.TryConsume(out GameMode requestedMode, out bool requestedAI, out int requestedDifficulty, out bool requestedPractice))
            {
                if (requestedPractice)
                {
                    StartPractice(requestedMode);
                }
                else
                {
                    StartMatch(requestedMode, requestedAI, (AIDifficulty)requestedDifficulty);
                }

                return;
            }

            if (startOnPlay)
            {
                StartMatch();
            }
        }

        /// <summary>Names used for human players from the next match on (player settings).</summary>
        public void SetPlayerNames(string first, string second)
        {
            playerNames = new[] { first, second };
        }

        /// <summary>Starts a new match in another game mode.</summary>
        public void StartMatch(GameMode newMode)
        {
            mode = newMode;
            StartMatch();
        }

        /// <summary>
        /// Starts an online match: player 0 is the room host (breaks first), player 1 the guest. The player on the other
        /// device is a <see cref="PlayerKind.Remote"/>; <see cref="Network.OnlineMatchController"/> handles the traffic.
        /// </summary>
        public void StartOnline(GameMode gameMode, int localIndex, string[] names, bool teams = false)
        {
            practice = false;
            versusAI = false;
            onlineNames = names != null && names.Length >= 2 ? names : new[] { "Player 1", "Player 2" };
            onlineSpectator = localIndex < 0;
            onlineLocalIndex = onlineSpectator ? -1 : Mathf.Clamp(localIndex, 0, onlineNames.Length - 1);
            onlineTeams = teams && onlineNames.Length == 4;
            nextBreaker = 0;
            mode = gameMode;
            Network.OnlineMatchController controller = GetComponent<Network.OnlineMatchController>();
            if (controller == null)
            {
                controller = gameObject.AddComponent<Network.OnlineMatchController>();
            }

            controller.Configure(this, shotController);
            StartMatch();
        }

        /// <summary>Starts free practice with the given rack shape (8-ball triangle or 9-ball diamond).</summary>
        public void StartPractice(GameMode rackMode)
        {
            practice = true;
            versusAI = false;
            mode = rackMode;
            StartMatch();
        }

        /// <summary>Restarts in the current kind of game (match or practice).</summary>
        public void StartMatch()
        {
            if (practice || IsOnline)
            {
                versusAI = false;
            }

            EnsureAI();
            shotController.AutoRespawnCueBall = false;
            Rules = practice ? new PracticeRuleSet(mode) : CreateRules(mode);
            int playerCount = IsOnline ? onlineNames.Length : Mathf.Max(2, playerNames.Length);
            var players = new List<MatchPlayer>(playerCount);
            for (int i = 0; i < playerCount; i++)
            {
                string name = i < playerNames.Length && !string.IsNullOrEmpty(playerNames[i]) ? playerNames[i] : $"Player {i + 1}";
                // Default names follow the interface language; custom names are kept as typed.
                if (name == $"Player {i + 1}")
                {
                    name = Loc.T("player.default", i + 1);
                }

                string aiName = Loc.T("player.ai", Loc.T("ai." + Difficulty.ToString().ToLowerInvariant()));
                if (IsOnline)
                {
                    string onlineName = onlineNames != null && i < onlineNames.Length && !string.IsNullOrEmpty(onlineNames[i]) ? onlineNames[i] : name;
                    players.Add(new MatchPlayer(i, onlineName, i == onlineLocalIndex ? PlayerKind.LocalHuman : PlayerKind.Remote)
                    {
                        // Doubles: seats alternate teams (A1, B1, A2, B2).
                        Team = onlineTeams ? i % 2 : i
                    });
                    continue;
                }

                players.Add(versusAI && i == 1 ? new AIPlayer(i, aiName) : new MatchPlayer(i, name));
            }

            var state = new MatchState(mode, players);
            int breaker = practice ? 0 : nextBreaker;
            if (alternateBreak && !practice)
            {
                nextBreaker = (nextBreaker + 1) % players.Count;
            }

            turnManager.StartRack(state, Rules, breaker);
            MatchStarted?.Invoke();
        }

        private IRuleSet CreateRules(GameMode gameMode)
        {
            switch (gameMode)
            {
                case GameMode.NineBall:
                    return new NineBallRuleSet(nineBallRules);
                default:
                    return new EightBallRuleSet(eightBallRules);
            }
        }
    }
}
