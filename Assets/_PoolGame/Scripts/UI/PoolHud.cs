using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using VTG.Pool.CameraSystem;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Inputs;
using VTG.Pool.Localization;
using VTG.Pool.Match;
using VTG.Pool.Rules;

namespace VTG.Pool.UI
{
    /// <summary>
    /// Match HUD (MASTER_PROMPT section 28), built at runtime with uGUI: player panels with group ball icons
    /// and turn highlight, mode / target line, message banner, power bar, interactive spin pad,
    /// camera buttons, pause menu and game-over panel. Reads state only; never decides rules.
    /// </summary>
    public sealed class PoolHud : MonoBehaviour
    {
        private const float BannerDuration = 3.5f;

        private static readonly Color PanelColor = new Color(0.05f, 0.06f, 0.07f, 0.78f);
        private static readonly Color ActiveColor = new Color(1f, 0.78f, 0.25f, 1f);
        private static readonly Color InactiveColor = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color TextColor = new Color(0.95f, 0.95f, 0.93f);
        private static readonly Color MutedText = new Color(0.75f, 0.77f, 0.8f);

        [SerializeField] private MatchManager matchManager;
        [SerializeField] private ShotController shotController;
        [SerializeField] private CueBallSpinController spinController;
        [SerializeField] private CueBallPlacementController placement;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private MonoBehaviour inputSource;
        [SerializeField] private ShotCallouts callouts;
        [SerializeField] private VTG.Pool.Presentation.GraphicsQualityManager graphicsQuality;
        [SerializeField] private VTG.Pool.Audio.AudioManager audioManager;
        [SerializeField] private VTG.Pool.Aiming.AimSystem aimSystem;
        [SerializeField] private PracticeSession practiceSession;
        [SerializeField] private PracticeLayoutEditor practiceEditor;

        private TurnManager turnManager;
        private IShotInput input;
        private PoolInputReader inputReader;
        private Font font;

        private PlayerPanel[] players;
        private Text modeText;
        private Text statusText;
        private Image[] tableIcons;
        private RectTransform tableIconRow;
        private Text bannerText;
        private CanvasGroup bannerGroup;
        private Image powerFill;
        private Text powerLabel;
        private GameObject pausePanel;
        private GameObject gameOverPanel;
        private Text gameOverTitle;
        private Text gameOverReason;
        private float bannerTime;
        private bool paused;
        private VTG.Pool.AI.AIDifficulty selectedDifficulty = VTG.Pool.AI.AIDifficulty.Intermediate;
        private string lastStatus;
        private int lastPowerPercent = -1;
        private ShotOutcome shownOutcome;
        private Text calloutText;
        private CanvasGroup calloutGroup;
        private float calloutTime;
        private RectTransform hudRoot;
        private GameObject settingsOverlay;
        private ResponsiveCanvas responsive;
        private RectTransform topPanel;
        private Text spinLabel;
        private TouchControls touchControls;
        private SpinPad spinPad;
        private SpinEditorOverlay spinEditor;

        public SpinPad SpinPad => spinPad;

        public SpinEditorOverlay SpinEditor => spinEditor;
        private PracticePanel practicePanel;
        private bool touchLayout;

        public TouchControls TouchControls => touchControls;

        private bool onlineApplied;
        private float gameOverShowAt = -1f;
        private CanvasGroup victoryGroup;
        private Text victoryTitle;
        private Text victorySub;
        private float victoryTime = -1f;
        private HighlightPresenter highlights;
        private RectTransform emblem;
        private Image emblemBall;
        private Image emblemRays;
        private Text emblemNumber;
        private CanvasGroup hudGroup;
        private int moneyBallWon = -1;
        private bool victoryScheduled;

        public HighlightPresenter Highlights => highlights;
        private GameObject connectionPanel;
        private Text connectionReason;

        private bool Online => matchManager != null && matchManager.IsOnline;

        public PracticePanel PracticePanel => practicePanel;

        public bool IsPaused => paused;

        private sealed class PlayerPanel
        {
            public Image Frame;
            public Text Name;
            public Text Detail;
            public Image[] Icons;
        }

        private void Awake()
        {
            input = inputSource as IShotInput;
            inputReader = inputSource as PoolInputReader;
            turnManager = matchManager != null ? matchManager.TurnManager : null;
            font = UiKit.Font;
            EnsureEventSystem();
            Build();
        }

        private void OnEnable()
        {
            PoolEvents.FoulCommitted += HandleFoul;
            PoolEvents.GameWon += HandleGameWon;
            if (turnManager != null)
            {
                turnManager.StateChanged += Refresh;
            }

            if (callouts != null)
            {
                callouts.CalloutRaised += HandleCallout;
            }

            if (responsive != null)
            {
                responsive.LayoutChanged += ApplyLayout;
            }

            Loc.Changed += HandleLanguageChanged;
        }

        private void OnDisable()
        {
            PoolEvents.FoulCommitted -= HandleFoul;
            PoolEvents.GameWon -= HandleGameWon;
            if (turnManager != null)
            {
                turnManager.StateChanged -= Refresh;
            }

            if (callouts != null)
            {
                callouts.CalloutRaised -= HandleCallout;
            }

            if (responsive != null)
            {
                responsive.LayoutChanged -= ApplyLayout;
            }

            Loc.Changed -= HandleLanguageChanged;

            if (paused)
            {
                Time.timeScale = 1f;
            }
        }

        private void Update()
        {
            if (input != null && input.PausePressed)
            {
                SetPaused(!paused);
            }
            else if (paused && input != null && input.CancelPressed)
            {
                SetPaused(false);
            }
            else if (!paused && touchLayout && input != null && input.CancelPressed && shotController != null && shotController.Phase != ShotPhase.PowerSelection)
            {
                // Android back button.
                SetPaused(true);
            }

            UpdatePower();
            UpdateStatus();
            UpdateVictory();

            // The HUD steps aside during replays.
            if (hudGroup != null)
            {
                float target = Replay.ShotReplay.IsPlaying ? 0f : 1f;
                hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, target, Time.unscaledDeltaTime * 5f);
            }
            if (gameOverShowAt > 0f && Time.unscaledTime >= gameOverShowAt && turnManager != null && turnManager.State != null && turnManager.State.IsGameOver)
            {
                gameOverShowAt = -1f;
                ShowGameOver(turnManager.State);
            }
            if (bannerTime > 0f)
            {
                bannerTime -= Time.unscaledDeltaTime;
                bannerGroup.alpha = Mathf.Clamp01(bannerTime / 0.5f);
            }

            if (calloutTime > 0f)
            {
                calloutTime -= Time.unscaledDeltaTime;
                calloutGroup.alpha = Mathf.Clamp01(calloutTime / 0.4f);
                float pop = 1f + 0.12f * Mathf.Clamp01((calloutTime - 2f) / 0.2f);
                calloutText.rectTransform.localScale = new Vector3(pop, pop, 1f);
            }
        }

        private void HandleLanguageChanged()
        {
            lastStatus = null;
            lastPowerPercent = -1;
            Refresh();
            ApplyLayout();
        }

        private static string DifficultyName(VTG.Pool.AI.AIDifficulty difficulty) => Loc.T("ai." + difficulty.ToString().ToLowerInvariant());

        private void HandleCallout(string label)
        {
            // Several callouts from one shot are joined into one line.
            calloutText.text = calloutTime > 2f && !string.IsNullOrEmpty(calloutText.text) ? calloutText.text + "  ·  " + label : label;
            calloutGroup.alpha = 1f;
            calloutTime = 2.2f;
            if (audioManager != null)
            {
                audioManager.PlayCallout();
            }
        }

        // ------------------------------------------------------------------ state

        private void Refresh()
        {
            MatchState state = turnManager != null ? turnManager.State : null;
            if (state == null)
            {
                return;
            }

            ApplyOnlineMode();
            modeText.text = turnManager.Rules != null ? turnManager.Rules.DisplayName : "POOL";
            bool practice = matchManager != null && matchManager.IsPractice;
            // One panel per side: a player, or a team in doubles (team mates share fouls and the group).
            bool teams = state.HasTeams;
            int sides = teams ? 2 : state.Players.Count;
            for (int i = 0; i < players.Length; i++)
            {
                PlayerPanel panel = players[i];
                bool shown = i < sides && (!practice || i == 0);
                panel.Frame.gameObject.SetActive(shown);
                if (!shown)
                {
                    continue;
                }

                int lead = teams ? FirstOfTeam(state, i) : i;
                MatchPlayer player = state.Players[lead];
                bool mine = state.TeamOf(state.CurrentPlayerIndex) == state.TeamOf(lead);
                bool active = !state.IsGameOver && mine;
                bool won = state.WinnerIndex >= 0 && state.TeamOf(state.WinnerIndex) == state.TeamOf(lead);
                panel.Frame.color = active ? ActiveColor : InactiveColor;
                panel.Name.text = (teams ? state.SideName(lead, LabelOf) : LabelOf(player)) + (won ? "  ★" : string.Empty);
                if (teams)
                {
                    int fouls = 0;
                    for (int p = 0; p < state.Players.Count; p++)
                    {
                        if (state.TeamOf(p) == state.TeamOf(lead)) fouls += state.Players[p].Fouls;
                    }

                    string turn = active ? "   ·   ▶ " + state.CurrentPlayer.DisplayName : string.Empty;
                    panel.Detail.text = RacksPrefix(lead) + (state.Mode == GameMode.EightBall ? BallGroups.Describe(player.Group) + "   ·   " : string.Empty) + Loc.T("hud.fouls", fouls) + turn;
                    FillIcons(panel, state, state.Mode == GameMode.EightBall ? player.Group : BallGroup.None);
                    continue;
                }
                if (practice)
                {
                    panel.Detail.text = Loc.T("hud.ballsontable", state.BallsOnTable.Count);
                    FillIcons(panel, state, BallGroup.None);
                }
                else if (state.Mode == GameMode.NineBall)
                {
                    string streak = player.ConsecutiveFouls > 0 ? "   ·   " + Loc.T("hud.inarow", player.ConsecutiveFouls) : string.Empty;
                    panel.Detail.text = RacksPrefix(i) + Loc.T("hud.fouls", player.Fouls) + streak;
                    FillIcons(panel, state, BallGroup.None);
                }
                else
                {
                    panel.Detail.text = $"{RacksPrefix(i)}{BallGroups.Describe(player.Group)}   ·   {Loc.T("hud.fouls", player.Fouls)}";
                    FillIcons(panel, state, player.Group);
                }
            }

            FillTableIcons(state);
            if (state.IsGameOver)
            {
                // Let the winning moment play (victory title, fireworks, camera) before the result panel.
                if (!victoryScheduled && gameOverShowAt < 0f && !gameOverPanel.activeSelf)
                {
                    victoryScheduled = true;
                    Replay.ShotReplay.RunAfterReplay(this, () =>
                    {
                        if (turnManager == null || turnManager.State == null || !turnManager.State.IsGameOver)
                        {
                            return;
                        }

                        gameOverShowAt = Time.unscaledTime + (CameraSystem.ShotCinematics.Enabled ? 3.2f : 1.6f);
                        ShowVictory(turnManager.State);
                    });
                }
            }
            else
            {
                gameOverShowAt = -1f;
                victoryScheduled = false;
                moneyBallWon = -1;
                gameOverPanel.SetActive(false);
                victoryTime = -1f;
                if (emblem != null) emblem.gameObject.SetActive(false);
                if (victoryGroup != null) victoryGroup.alpha = 0f;
            }

            ShotOutcome outcome = state.LastOutcome;
            if (outcome != null && outcome != shownOutcome)
            {
                shownOutcome = outcome;
                if (!outcome.Foul && !outcome.GameOver && !string.IsNullOrEmpty(outcome.Message))
                {
                    ShowBanner(outcome.Message, TextColor);
                }
            }

            lastStatus = null;
        }

        /// <summary>Online: racks won in this room ("Racks 2   ·   ").</summary>
        private string RacksPrefix(int playerIndex)
        {
            Network.OnlineMatchController online = Online ? Network.OnlineMatchController.Active : null;
            return online != null ? Loc.T("hud.racks", online.RacksWon(playerIndex)) + "   ·   " : string.Empty;
        }

        private static string LabelOf(MatchPlayer player) => player.Left ? player.DisplayName + " " + Loc.T("online.lefttag") : player.DisplayName;

        private static int FirstOfTeam(MatchState state, int team)
        {
            for (int i = 0; i < state.Players.Count; i++)
            {
                if (state.TeamOf(i) == team) return i;
            }

            return 0;
        }

        private void FillIcons(PlayerPanel panel, MatchState state, BallGroup group)
        {
            int first = group == BallGroup.Stripes ? 9 : 1;
            bool show = group != BallGroup.None;
            bool onEight = show && state.RemainingInGroup(group) == 0;
            for (int i = 0; i < panel.Icons.Length; i++)
            {
                Image icon = panel.Icons[i];
                if (!show)
                {
                    icon.enabled = false;
                    continue;
                }

                int number = onEight ? (i == 0 ? 8 : -1) : first + i;
                icon.enabled = number > 0;
                if (number > 0)
                {
                    icon.sprite = BallIconFactory.ForBall(number);
                    icon.color = state.IsOnTable(number) ? Color.white : new Color(1f, 1f, 1f, 0.18f);
                }
            }
        }

        /// <summary>9-ball: balls still on the table, the lowest (legal target) enlarged.</summary>
        private void FillTableIcons(MatchState state)
        {
            bool show = state.Mode == GameMode.NineBall;
            tableIconRow.gameObject.SetActive(show);
            if (!show)
            {
                return;
            }

            int lowest = state.LowestBallOnTable();
            for (int number = 1; number <= tableIcons.Length; number++)
            {
                Image icon = tableIcons[number - 1];
                bool onTable = state.IsOnTable(number);
                icon.sprite = BallIconFactory.ForBall(number);
                icon.color = onTable ? Color.white : new Color(1f, 1f, 1f, 0.15f);
                float size = number == lowest ? 40f : 28f;
                icon.rectTransform.sizeDelta = new Vector2(size, size);
            }
        }

        private void UpdateStatus()
        {
            MatchState state = turnManager != null ? turnManager.State : null;
            if (state == null || statusText == null)
            {
                return;
            }

            string status;
            if (state.IsGameOver)
            {
                status = Loc.T("hud.gameover");
            }
            else if (shotController != null && (shotController.Phase == ShotPhase.BallsMoving || shotController.Phase == ShotPhase.Shooting))
            {
                status = Loc.T("hud.inprogress", state.CurrentPlayer.DisplayName);
            }
            else if (Online && Network.OnlineMatchController.Active != null && Network.OnlineMatchController.Active.AwaitingAuthority)
            {
                status = Loc.T("online.waiting");
            }
            else if (practiceEditor != null && practiceEditor.IsEditing)
            {
                status = Loc.T(touchLayout ? "hud.arrange.touch" : "hud.arrange.mouse");
            }
            else if (placement != null && placement.IsPlacing)
            {
                string area = placement.Mode == BallInHandMode.BehindHeadString ? Loc.T("hud.behindhead") : string.Empty;
                string how = Loc.T(touchLayout ? "hud.place.touch" : "hud.place.mouse");
                status = state.CurrentPlayer.Kind != PlayerKind.LocalHuman
                    ? Loc.T("hud.aiplacing", state.CurrentPlayer.DisplayName, area)
                    : Loc.T("hud.ballinhand", state.CurrentPlayer.DisplayName, area, how);
            }
            else
            {
                string target = turnManager.Rules != null ? turnManager.Rules.DescribeTarget(state) : string.Empty;
                string bih = placement != null && placement.Mode != BallInHandMode.None && !touchLayout ? Loc.T("hud.bkey") : string.Empty;
                status = state.CurrentPlayer.Kind == PlayerKind.AI
                    ? Loc.T("hud.aithinking", state.CurrentPlayer.DisplayName, target)
                    : state.CurrentPlayer.Kind == PlayerKind.Remote
                        ? Loc.T("online.remoteaiming", state.CurrentPlayer.DisplayName, target)
                        : Loc.T("hud.toshoot", state.CurrentPlayer.DisplayName, target, bih);
            }

            if (status != lastStatus)
            {
                statusText.text = status;
                lastStatus = status;
            }
        }

        private void UpdatePower()
        {
            if (shotController == null)
            {
                return;
            }

            float power = shotController.Phase == ShotPhase.PowerSelection ? shotController.CurrentPower : shotController.LastPower;
            powerFill.fillAmount = power;
            powerFill.color = Color.Lerp(new Color(0.35f, 0.85f, 0.4f), new Color(0.95f, 0.3f, 0.2f), power);
            int percent = Mathf.RoundToInt(power * 100f);
            // Mouse stroke: show the back swing while stroking, and how to stroke while aiming (desktop).
            int stroke = shotController.Stroking ? 2 : (!touchLayout && shotController.MouseStrokeEnabled && shotController.Phase == ShotPhase.Aiming ? 1 : 0);
            int key = percent * 4 + stroke;
            if (key != lastPowerPercent)
            {
                powerLabel.text = stroke == 2
                    ? Loc.T("hud.stroke.draw", Mathf.RoundToInt(shotController.StrokeDrawBack * 100f))
                    : Loc.T("hud.power", percent) + (stroke == 1 ? "   ·   " + Loc.T("hud.stroke.hint") : string.Empty);
                lastPowerPercent = key;
            }
        }

        private void HandleFoul(int playerIndex, FoulType foul, string message) => ShowBanner(message, new Color(1f, 0.45f, 0.4f));

        private void HandleGameWon(int winnerIndex, string message) => ShowBanner(message, ActiveColor);

        private void ShowBanner(string message, Color color)
        {
            bannerText.text = message;
            bannerText.color = color;
            bannerGroup.alpha = 1f;
            bannerTime = BannerDuration;
        }

        private void ShowVictory(MatchState state)
        {
            if (victoryGroup == null || state.WinnerIndex < 0)
            {
                return;
            }

            victoryTitle.text = Loc.T("victory.title");
            victorySub.text = Loc.T("victory.wins", state.SideName(state.WinnerIndex));
            victoryTime = 0f;

            // The deciding 8 / 9 went down: its ball as a spinning emblem with rays, in the ball's colours.
            emblem.gameObject.SetActive(moneyBallWon > 0);
            if (moneyBallWon > 0)
            {
                emblemBall.sprite = BallIconFactory.ForBall(moneyBallWon);
                emblemNumber.text = moneyBallWon.ToString();
                emblemRays.color = moneyBallWon == 8 ? new Color(1f, 0.78f, 0.25f, 0.85f) : new Color(1f, 0.95f, 0.35f, 0.9f);
                if (audioManager != null) audioManager.PlayFanfare(moneyBallWon == 8 ? 0.95f : 1.15f);
            }
        }

        private void UpdateVictory()
        {
            if (victoryGroup == null || victoryTime < 0f)
            {
                return;
            }

            victoryTime += Time.unscaledDeltaTime;
            float t = victoryTime;
            float pop = t < 0.25f ? Mathf.Lerp(2.2f, 0.92f, t / 0.25f) : Mathf.Lerp(0.92f, 1f, Mathf.Clamp01((t - 0.25f) / 0.15f));
            victoryTitle.rectTransform.localScale = new Vector3(pop, pop, 1f);
            victoryGroup.alpha = Mathf.Clamp01(t / 0.15f) * (1f - Mathf.Clamp01((t - 2.8f) / 0.5f));
            float shimmer = 0.75f + 0.25f * Mathf.Sin(t * 9f);
            if (emblem != null && emblem.gameObject.activeSelf)
            {
                float grow = t < 0.35f ? Mathf.Lerp(0f, 1.15f, t / 0.35f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((t - 0.35f) / 0.2f));
                emblem.localScale = new Vector3(grow, grow, 1f);
                emblemBall.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -360f * Mathf.Clamp01(t / 0.6f));
                emblemRays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, t * 40f);
            }
            victoryTitle.color = new Color(1f, 0.82f * shimmer + 0.1f, 0.3f);
            if (t > 3.4f)
            {
                victoryTime = -1f;
                victoryGroup.alpha = 0f;
            }
        }

        /// <summary>Shows the result panel now (skips the victory moment).</summary>
        public void ShowResultNow()
        {
            if (turnManager != null && turnManager.State != null && turnManager.State.IsGameOver)
            {
                gameOverShowAt = -1f;
                ShowGameOver(turnManager.State);
            }
        }

        private void ShowGameOver(MatchState state)
        {
            gameOverPanel.SetActive(true);
            gameOverTitle.text = state.WinnerIndex >= 0 ? Loc.T("hud.wins", state.SideName(state.WinnerIndex)) : Loc.T("hud.gameover");
            gameOverReason.text = state.Result;
        }

        /// <summary>Opens the large spin / cue-angle editor (touch layout, or tests).</summary>
        public void OpenSpinEditor()
        {
            bool canEdit = shotController == null || (shotController.CanShoot && !shotController.ShotInputBlocked);
            if (spinEditor != null || spinController == null || !canEdit || paused)
            {
                return;
            }

            spinEditor = SpinEditorOverlay.Open(hudRoot, spinController, () => spinEditor = null);
            if (audioManager != null)
            {
                audioManager.PlayUiClick();
                foreach (Button button in spinEditor.GetComponentsInChildren<Button>(true))
                {
                    button.onClick.AddListener(audioManager.PlayUiClick);
                }
            }
        }

        public void SetPaused(bool value)
        {
            if (value && spinEditor != null)
            {
                spinEditor.Close();
            }

            if (value && practiceEditor != null)
            {
                practiceEditor.SetEditing(false);
            }

            if (!value && settingsOverlay != null)
            {
                Save.SaveSystem.SaveSettings();
                Destroy(settingsOverlay);
                settingsOverlay = null;
            }

            paused = value;
            pausePanel.SetActive(value);
            Time.timeScale = value && !Online ? 0f : 1f;
            if (inputReader != null)
            {
                inputReader.Suppressed = value;
            }
        }

        private void NewGame(GameMode mode)
        {
            SetPaused(false);
            gameOverPanel.SetActive(false);
            matchManager.StartMatch(mode, false);
        }

        private void NewAIGame(GameMode mode)
        {
            SetPaused(false);
            gameOverPanel.SetActive(false);
            matchManager.StartMatch(mode, true, selectedDifficulty);
        }

        private void OpenSettings()
        {
            pausePanel.SetActive(false);
            settingsOverlay = SettingsScreen.Build(hudRoot, () =>
            {
                settingsOverlay = null;
                if (paused)
                {
                    pausePanel.SetActive(true);
                }
            });
            if (audioManager != null)
            {
                foreach (Button button in settingsOverlay.GetComponentsInChildren<Button>(true))
                {
                    button.onClick.AddListener(audioManager.PlayUiClick);
                }
            }
        }

        private void GoToMainMenu()
        {
            if (Online && Network.OnlineClient.Exists)
            {
                Network.OnlineClient.Instance.Leave();
            }

            Time.timeScale = 1f;
            if (inputReader != null)
            {
                inputReader.Suppressed = false;
            }

            SceneManager.LoadScene("01_MainMenu");
        }

        private void Restart()
        {
            SetPaused(false);
            gameOverPanel.SetActive(false);
            if (Online && Network.OnlineMatchController.Active != null)
            {
                // The host re-racks for both; the guest asks the host.
                Network.OnlineMatchController.Active.RequestRematch();
                return;
            }

            matchManager.StartMatch();
        }

        /// <summary>Online: hides offline-only pause options and listens for the connection.</summary>
        private void ApplyOnlineMode()
        {
            if (onlineApplied || !Online || Network.OnlineMatchController.Active == null)
            {
                return;
            }

            onlineApplied = true;
            Transform box = pausePanel.transform.Find("Box");
            foreach (string key in new[] { "hud.restart", "hud.new8local", "hud.new9local", "hud.new8ai", "hud.new9ai" })
            {
                Transform button = box.Find(Loc.InEnglish(key));
                if (button != null) button.gameObject.SetActive(false);
            }

            foreach (string name in new[] { "AIDifficulty" })
            {
                Transform button = box.Find(name);
                if (button != null) button.gameObject.SetActive(false);
            }

            Network.OnlineMatchController.Active.ConnectionLost += ShowConnectionLost;
            if (Network.OnlineMatchController.Active.IsSpectator)
            {
                ApplySpectatorMode();
            }

            Network.OnlineMatchController.Active.PlayerLeft += name => ShowBanner(Loc.T("online.playerleft", name), new Color(1f, 0.6f, 0.3f));
        }

        private bool spectating;
        private Text spectatorText;

        /// <summary>Watching: no cue controls, a "watching" badge with the audience, free orbit camera by default.</summary>
        private void ApplySpectatorMode()
        {
            spectating = true;
            foreach (string name in new[] { "Power", "SpinPanel" })
            {
                Transform part = hudRoot.Find(name);
                if (part != null) part.gameObject.SetActive(false);
            }

            Transform again = gameOverPanel.transform.Find("Box/" + Loc.InEnglish("hud.playagain"));
            if (again != null) again.gameObject.SetActive(false);
            RectTransform badge = Panel(hudRoot, "SpectatorBadge", new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(780f, 64f), PanelColor);
            spectatorText = Label(badge, "Text", string.Empty, 21, FontStyle.Bold, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(760f, 60f), TextColor);
            spectatorText.alignment = TextAnchor.MiddleCenter;
            Network.OnlineClient client = Network.OnlineClient.Instance;
            client.AudienceChanged += UpdateSpectatorBadge;
            UpdateSpectatorBadge();
            if (cameraController != null)
            {
                cameraController.OrbitWithPrimaryDrag = true;
                cameraController.SetMode(CameraMode.Orbit);
            }

            ApplyLayout();
        }

        private void UpdateSpectatorBadge()
        {
            if (spectatorText == null)
            {
                return;
            }

            Network.OnlineClient client = Network.OnlineClient.Exists ? Network.OnlineClient.Instance : null;
            spectatorText.text = Loc.T("spectator.badge", client != null ? client.RoomCode : string.Empty, client != null ? client.Audience : 0);
        }

        private void OnDestroy()
        {
            if (Network.OnlineClient.Exists)
            {
                Network.OnlineClient.Instance.AudienceChanged -= UpdateSpectatorBadge;
            }
        }

        private void ShowConnectionLost(string reasonKey)
        {
            SetPaused(false);
            gameOverPanel.SetActive(false);
            connectionReason.text = Loc.T(reasonKey);
            connectionPanel.SetActive(true);
            if (inputReader != null)
            {
                inputReader.Suppressed = true;
            }
        }

        // ------------------------------------------------------------------ building

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        private void Build()
        {
            var canvasObject = new GameObject("HUD Canvas");
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
            responsive = canvasObject.AddComponent<ResponsiveCanvas>();
            RectTransform root = responsive.SafeRoot;
            hudRoot = root;

            players = new[]
            {
                BuildPlayerPanel(root, new Vector2(0f, 1f), new Vector2(24f, -24f)),
                BuildPlayerPanel(root, new Vector2(1f, 1f), new Vector2(-24f, -24f)),
                // Third and fourth players (online free-for-all) stack under the first two.
                BuildPlayerPanel(root, new Vector2(0f, 1f), new Vector2(24f, -158f)),
                BuildPlayerPanel(root, new Vector2(1f, 1f), new Vector2(-24f, -158f))
            };

            RectTransform top = Panel(root, "TopCenter", new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(860f, 92f), PanelColor);
            topPanel = top;
            modeText = Label(top, "Mode", "8-BALL", 30, FontStyle.Bold, TextAnchor.UpperCenter, new Vector2(0f, -8f), new Vector2(840f, 38f), TextColor);
            statusText = Label(top, "Status", string.Empty, 19, FontStyle.Normal, TextAnchor.UpperCenter, new Vector2(0f, -50f), new Vector2(840f, 30f), MutedText);
            statusText.horizontalOverflow = HorizontalWrapMode.Overflow;
            Button pause = MakeButton(root, "PauseButton", "II", new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(56f, 44f));
            tableIconRow = Panel(root, "TableBalls", new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(400f, 48f), PanelColor);
            tableIconRow.anchoredPosition = new Vector2(-262f, -124f);
            tableIcons = new Image[9];
            for (int i = 0; i < tableIcons.Length; i++)
            {
                var iconObject = new GameObject($"Ball{i + 1}", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(tableIconRow, false);
                var rect = iconObject.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(26f + i * 42f, 0f);
                rect.sizeDelta = new Vector2(28f, 28f);
                Image image = iconObject.GetComponent<Image>();
                image.raycastTarget = false;
                tableIcons[i] = image;
            }

            tableIconRow.gameObject.SetActive(false);
            pause.onClick.AddListener(() => SetPaused(!paused));

            RectTransform banner = Panel(root, "Banner", new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1100f, 64f), new Color(0f, 0f, 0f, 0.6f));
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerGroup.blocksRaycasts = false;
            bannerText = Label(banner, "Text", string.Empty, 28, FontStyle.Bold, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(1080f, 60f), TextColor, true);

            BuildPowerBar(root);
            BuildSpinPad(root);
            BuildCameraButtons(root);
            if (aimSystem == null)
            {
                aimSystem = FindAnyObjectByType<VTG.Pool.Aiming.AimSystem>();
            }

            hudGroup = root.gameObject.AddComponent<CanvasGroup>();
            touchControls = TouchControls.Create(root, shotController, aimSystem, placement, inputReader);
            highlights = HighlightPresenter.Create(root, callouts, audioManager, root);
            if (callouts != null)
            {
                callouts.HighlightDetected += h =>
                {
                    if (h.WinningShot && h.MoneyBall > 0) moneyBallWon = h.MoneyBall;
                };
            }
            if (practiceSession != null && practiceEditor != null)
            {
                practicePanel = PracticePanel.Create(root, practiceSession, practiceEditor);
            }
            pausePanel = BuildModal(root, "PausePanel", "hud.paused", out _, out _, ("hud.resume", () => SetPaused(false)), ("hud.restart", Restart), ("hud.settings", OpenSettings), ("hud.mainmenu", GoToMainMenu),
                ("hud.new8local", () => NewGame(GameMode.EightBall)), ("hud.new9local", () => NewGame(GameMode.NineBall)),
                ("hud.new8ai", () => NewAIGame(GameMode.EightBall)), ("hud.new9ai", () => NewAIGame(GameMode.NineBall)));
            Button difficultyButton = MakeButton((RectTransform)pausePanel.transform.Find("Box"), "AIDifficulty",
                string.Empty, new Vector2(0.5f, 1f), new Vector2(-155f, -100f), new Vector2(290f, 44f));
            Text difficultyText = LocalizedText.Bind(difficultyButton.GetComponentInChildren<Text>(), () => Loc.T("hud.newai", DifficultyName(selectedDifficulty)));
            difficultyButton.onClick.AddListener(() =>
            {
                selectedDifficulty = (VTG.Pool.AI.AIDifficulty)(((int)selectedDifficulty + 1) % 4);
                difficultyText.text = Loc.T("hud.newai", DifficultyName(selectedDifficulty));
            });
            if (graphicsQuality != null)
            {
                Button qualityButton = MakeButton((RectTransform)pausePanel.transform.Find("Box"), "GraphicsQuality",
                    string.Empty, new Vector2(0.5f, 1f), new Vector2(155f, -100f), new Vector2(290f, 44f));
                Text qualityText = LocalizedText.Bind(qualityButton.GetComponentInChildren<Text>(),
                    () => Loc.T("hud.graphics", Loc.T("quality." + graphicsQuality.Tier.ToString().ToLowerInvariant())));
                qualityButton.onClick.AddListener(() =>
                {
                    graphicsQuality.Cycle();
                    qualityText.text = Loc.T("hud.graphics", Loc.T("quality." + graphicsQuality.Tier.ToString().ToLowerInvariant()));
                });
            }

            gameOverPanel = BuildModal(root, "GameOverPanel", "hud.gameover", out gameOverTitle, out gameOverReason, ("hud.playagain", Restart));
            connectionPanel = BuildModal(root, "ConnectionPanel", "online.title", out _, out connectionReason, ("hud.mainmenu", GoToMainMenu));
            connectionPanel.SetActive(false);
            pausePanel.SetActive(false);
            gameOverPanel.SetActive(false);

            RectTransform victory = Panel(root, "Victory", new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1400f, 240f), new Color(0f, 0f, 0f, 0f));
            victory.GetComponent<Image>().raycastTarget = false;
            victoryGroup = victory.gameObject.AddComponent<CanvasGroup>();
            victoryGroup.alpha = 0f;
            victoryGroup.blocksRaycasts = false;
            victoryTitle = Label(victory, "Title", string.Empty, 120, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0f, 30f), new Vector2(1400f, 160f), ActiveColor, true);
            Outline victoryGlow = victoryTitle.gameObject.AddComponent<Outline>();
            victoryGlow.effectColor = new Color(0.6f, 0.25f, 0f, 0.8f);
            victoryGlow.effectDistance = new Vector2(4f, -4f);
            victorySub = Label(victory, "Sub", string.Empty, 36, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0f, -70f), new Vector2(1400f, 60f), TextColor, true);
            victorySub.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
            emblem = Panel(victory, "Emblem", new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(300f, 300f), new Color(0f, 0f, 0f, 0f));
            emblem.GetComponent<Image>().raycastTarget = false;
            emblemRays = Panel(emblem, "Rays", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 520f), Color.white).GetComponent<Image>();
            emblemRays.sprite = FunSprites.Rays;
            emblemRays.raycastTarget = false;
            emblemBall = Panel(emblem, "Ball", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 220f), Color.white).GetComponent<Image>();
            emblemBall.raycastTarget = false;
            emblemNumber = Label(emblemBall.rectTransform, "Number", string.Empty, 96, FontStyle.Bold, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(220f, 220f), new Color(0.08f, 0.08f, 0.1f), true);
            emblem.gameObject.SetActive(false);

            RectTransform callout = Panel(root, "Callout", new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(900f, 70f), new Color(0f, 0f, 0f, 0f));
            calloutGroup = callout.gameObject.AddComponent<CanvasGroup>();
            calloutGroup.alpha = 0f;
            calloutGroup.blocksRaycasts = false;
            calloutText = Label(callout, "Text", string.Empty, 40, FontStyle.Bold, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(900f, 70f), ActiveColor, true);
            Shadow calloutShadow = calloutText.gameObject.AddComponent<Shadow>();
            calloutShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            calloutShadow.effectDistance = new Vector2(2f, -2f);

            if (audioManager != null)
            {
                foreach (Button button in canvasObject.GetComponentsInChildren<Button>(true))
                {
                    button.onClick.AddListener(audioManager.PlayUiClick);
                }
            }

            ApplyLayout();
        }

        /// <summary>Desktop or touch arrangement (scaled header, on-screen controls, handedness).</summary>
        private void ApplyLayout()
        {
            if (responsive == null || topPanel == null)
            {
                return;
            }

            touchLayout = responsive.TouchLayout;
            bool leftHanded = Save.SaveSystem.Settings.leftHanded;
            float headerScale = touchLayout ? 0.82f : 1f;
            topPanel.localScale = new Vector3(headerScale, headerScale, 1f);
            for (int i = 0; i < players.Length; i++)
            {
                RectTransform frame = players[i].Frame.rectTransform;
                frame.localScale = new Vector3(headerScale, headerScale, 1f);
                if (i >= 2)
                {
                    frame.anchoredPosition = new Vector2(frame.anchoredPosition.x, -24f - 134f * headerScale);
                }
            }

            if (spinLabel != null)
            {
                spinLabel.text = Loc.T(touchLayout ? "hud.spin.touch" : "hud.spin.mouse");
            }

            touchControls.SetLayout(touchLayout && !spectating, leftHanded);
            // Touch: the small pad and angle slider open the large editor (finger-sized, fine control).
            spinPad.OpenEditor = touchLayout ? OpenSpinEditor : (System.Action)null;
            if (ElevationSlider != null)
            {
                ElevationSlider.OpenEditor = touchLayout ? OpenSpinEditor : (System.Action)null;
            }
            if (practicePanel != null)
            {
                practicePanel.SetSide(touchLayout && leftHanded);
            }
        }

        private PlayerPanel BuildPlayerPanel(RectTransform root, Vector2 corner, Vector2 offset)
        {
            var panel = new PlayerPanel();
            RectTransform frame = Panel(root, "PlayerFrame", corner, offset, new Vector2(470f, 124f), InactiveColor);
            panel.Frame = frame.GetComponent<Image>();
            RectTransform inner = Panel(frame, "Inner", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(462f, 116f), new Color(0.05f, 0.06f, 0.07f, 0.95f));
            TextAnchor align = corner.x < 0.5f ? TextAnchor.UpperLeft : TextAnchor.UpperRight;
            panel.Name = Label(inner, "Name", "Player", 28, FontStyle.Bold, align, new Vector2(0f, -8f), new Vector2(430f, 36f), TextColor);
            panel.Detail = Label(inner, "Detail", "Open", 18, FontStyle.Normal, align, new Vector2(0f, -44f), new Vector2(430f, 26f), MutedText);
            panel.Icons = new Image[7];
            for (int i = 0; i < 7; i++)
            {
                float x = corner.x < 0.5f ? -200f + i * 40f : 200f - (6 - i) * 40f;
                var icon = new GameObject($"Ball{i}", typeof(RectTransform), typeof(Image));
                icon.transform.SetParent(inner, false);
                var rect = icon.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(34f, 34f);
                rect.anchoredPosition = new Vector2(x, 26f);
                Image image = icon.GetComponent<Image>();
                image.raycastTarget = false;
                image.enabled = false;
                panel.Icons[i] = image;
            }

            return panel;
        }

        private void BuildPowerBar(RectTransform root)
        {
            RectTransform back = Panel(root, "Power", new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(420f, 64f), PanelColor);
            powerLabel = Label(back, "Label", "POWER", 18, FontStyle.Bold, TextAnchor.UpperLeft, new Vector2(0f, -6f), new Vector2(400f, 24f), MutedText);
            RectTransform track = Panel(back, "Track", new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(396f, 18f), new Color(1f, 1f, 1f, 0.1f));
            RectTransform fill = Panel(track, "Fill", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(396f, 18f), Color.green);
            powerFill = fill.GetComponent<Image>();
            powerFill.type = Image.Type.Filled;
            powerFill.fillMethod = Image.FillMethod.Horizontal;
            powerFill.sprite = UiKit.WhiteSprite;
        }

        private void BuildSpinPad(RectTransform root)
        {
            RectTransform back = Panel(root, "SpinPanel", new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(262f, 214f), PanelColor);
            spinLabel = Label(back, "Label", "hud.spin.mouse", 13, FontStyle.Normal, TextAnchor.UpperCenter, new Vector2(0f, -4f), new Vector2(258f, 20f), MutedText);
            RectTransform ball = Panel(back, "CueFace", new Vector2(1f, 0f), new Vector2(-14f, 14f), new Vector2(160f, 160f), new Color(0.96f, 0.95f, 0.9f));
            BuildElevationSlider(back);
            Image face = ball.GetComponent<Image>();
            face.sprite = BallIconFactory.Circle;
            RectTransform marker = Panel(ball, "Marker", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f), new Color(0.85f, 0.1f, 0.1f));
            Image markerImage = marker.GetComponent<Image>();
            markerImage.sprite = BallIconFactory.Circle;
            markerImage.raycastTarget = false;
            SpinPad pad = ball.gameObject.AddComponent<SpinPad>();
            spinPad = pad;
            pad.Initialize(ball, marker, spinController);
            pad.CanEdit = () => shotController == null || (shotController.CanShoot && !shotController.ShotInputBlocked);
        }

        /// <summary>Cue elevation (massé) slider at the left of the spin panel; keys R / F also raise and lower the cue.</summary>
        private void BuildElevationSlider(RectTransform panel)
        {
            RectTransform track = Panel(panel, "Elevation", new Vector2(0f, 0f), new Vector2(18f, 14f), new Vector2(46f, 132f), new Color(1f, 1f, 1f, 0.1f));
            RectTransform fillRect = Panel(track, "Fill", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 132f), UiKit.Accent);
            Image fill = fillRect.GetComponent<Image>();
            fill.sprite = UiKit.WhiteSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;
            Text value = Label(panel, "ElevationValue", "0°", 14, FontStyle.Bold, TextAnchor.LowerCenter, new Vector2(-90f, -26f), new Vector2(70f, 40f), TextColor);
            value.rectTransform.anchorMin = value.rectTransform.anchorMax = value.rectTransform.pivot = new Vector2(0f, 1f);
            value.rectTransform.anchoredPosition = new Vector2(6f, -24f);
            ElevationSlider slider = track.gameObject.AddComponent<ElevationSlider>();
            slider.Initialize(track, fill, value, spinController);
            slider.CanEdit = () => shotController == null || (shotController.CanShoot && !shotController.ShotInputBlocked);
            ElevationSlider = slider;
        }

        public ElevationSlider ElevationSlider { get; private set; }

        private void BuildCameraButtons(RectTransform root)
        {
            if (cameraController == null)
            {
                return;
            }

            string[] labels = { "hud.cam.cue", "hud.cam.tactical", "hud.cam.top", "hud.cam.orbit" };
            CameraMode[] modes = { CameraMode.Cue, CameraMode.Tactical, CameraMode.Top, CameraMode.Orbit };
            for (int i = 0; i < labels.Length; i++)
            {
                CameraMode mode = modes[i];
                Button button = MakeButton(root, $"Camera{mode}", labels[i], new Vector2(1f, 0f), new Vector2(-302f - (labels.Length - 1 - i) * 132f, 24f), new Vector2(124f, 48f));
                button.onClick.AddListener(() => cameraController.SetMode(mode));
            }
        }

        private GameObject BuildModal(RectTransform root, string name, string title, out Text titleText, out Text bodyText, params (string label, UnityEngine.Events.UnityAction action)[] buttons)
        {
            RectTransform dim = Panel(root, name, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.55f));
            dim.anchorMin = Vector2.zero;
            dim.anchorMax = Vector2.one;
            dim.offsetMin = new Vector2(-600f, -600f);
            dim.offsetMax = new Vector2(600f, 600f);
            float height = 200f + buttons.Length * 70f;
            RectTransform box = Panel(dim, "Box", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, height), new Color(0.07f, 0.08f, 0.1f, 0.95f));
            titleText = Label(box, "Title", title, 40, FontStyle.Bold, TextAnchor.UpperCenter, new Vector2(0f, -24f), new Vector2(580f, 56f), TextColor);
            bodyText = Label(box, "Body", string.Empty, 20, FontStyle.Normal, TextAnchor.UpperCenter, new Vector2(0f, -84f), new Vector2(560f, 70f), MutedText);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = MakeButton(box, Loc.InEnglish(buttons[i].label), buttons[i].label, new Vector2(0.5f, 0f), new Vector2(0f, 28f + (buttons.Length - 1 - i) * 70f), new Vector2(320f, 56f));
                button.onClick.AddListener(buttons[i].action);
            }

            return dim.gameObject;
        }

        private RectTransform Panel(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            panel.GetComponent<Image>().color = color;
            return rect;
        }

        private Text Label(RectTransform parent, string name, string text, int size, FontStyle style, TextAnchor alignment, Vector2 position, Vector2 boxSize, Color color, bool center = false)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);
            var rect = labelObject.GetComponent<RectTransform>();
            Vector2 anchor = center ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 1f);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = boxSize;
            Text label = labelObject.GetComponent<Text>();
            label.font = font;
            if (Loc.Has(text))
            {
                LocalizedText.Bind(label, text);
            }
            else
            {
                label.text = text;
            }

            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private Button MakeButton(RectTransform parent, string name, string text, Vector2 anchor, Vector2 position, Vector2 size)
        {
            RectTransform rect = Panel(parent, name, anchor, position, size, new Color(0.18f, 0.2f, 0.24f, 0.95f));
            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = colors;
            Label(rect, "Text", text, 20, FontStyle.Bold, TextAnchor.MiddleCenter, Vector2.zero, size, TextColor, true);
            return button;
        }
    }
}
