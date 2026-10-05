using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VTG.Pool.Audio;
using VTG.Pool.Rules;
using VTG.Pool.Save;

namespace VTG.Pool.UI
{
    /// <summary>
    /// Main menu (MASTER_PROMPT section 40): PLAY (8-ball / 9-ball, vs AI / local two players, online later),
    /// PRACTICE (free practice in the match scene), SETTINGS, STATISTICS, EXIT. The chosen match is handed to the match scene via <see cref="MatchLaunch"/>.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string MatchScene = "02_Match";
        public const string PhysicsLabScene = "03_PhysicsTest";

        private static readonly string[] GameOptions = { "opt.8ball", "opt.9ball" };
        private static readonly string[] OpponentOptions = { "opt.vsai", "opt.local" };
        private static readonly string[] DifficultyOptions = { "ai.beginner", "ai.intermediate", "ai.advanced", "ai.expert" };

        [SerializeField] private AudioManager audioManager;
        [SerializeField] private string versionLabel = "v0.5 · Milestone 5";

        private RectTransform canvas;
        private GameObject mainPanel;
        private GameObject playPanel;
        private int gameIndex;
        private int opponentIndex;
        private int difficultyIndex;
        private CanvasGroup fade;
        private float fadeTime;
        private bool holdForIntro;

        private void Awake()
        {
            Time.timeScale = 1f;
            difficultyIndex = SaveSystem.Settings.aiDifficulty;
            canvas = UiKit.CreateCanvas(transform, "Menu Canvas", 20);
            BuildTitle();
            mainPanel = BuildMain();
            playPanel = BuildPlay();
            playPanel.SetActive(false);
            fade = canvas.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 0f;
            holdForIntro = IntroDirector.TryStart(this);
            fade.blocksRaycasts = !holdForIntro;
        }

        private void Start()
        {
            // Invite link: https://host/?room=K7P2QX opens the lobby and joins that room.
            string room = OnlineLobbyPanel.RoomFromUrl(Application.absoluteURL);
            if (room != null && !Network.OnlineClient.Exists && !holdForIntro)
            {
                OnlineLobbyPanel lobby = ShowOnlineLobby();
                lobby.JoinWithCode(room);
            }
        }

        /// <summary>Called by the intro when it ends: the menu fades in.</summary>
        public void ShowAfterIntro()
        {
            holdForIntro = false;
            fadeTime = 0f;
            fade.blocksRaycasts = true;
        }

        private void Update()
        {
            if (!holdForIntro && fadeTime < 1f)
            {
                fadeTime += Time.unscaledDeltaTime * 1.5f;
                fade.alpha = Mathf.SmoothStep(0f, 1f, fadeTime);
            }
        }

        private void BuildTitle()
        {
            RectTransform titleArea = UiKit.Rect(canvas, "Title", new Vector2(0f, 1f), new Vector2(110f, -110f), new Vector2(900f, 200f));
            UiKit.Label(titleArea, "VTG POOL 3D", 92, TextAnchor.UpperLeft, new Vector2(0f, 1f), Vector2.zero, new Vector2(900f, 110f), UiKit.Text, FontStyle.Bold)
                .gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3f, -3f);
            UiKit.Label(titleArea, "menu.subtitle", 30, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(4f, -112f), new Vector2(900f, 44f), UiKit.Accent);
            UiKit.Label(canvas, versionLabel, 18, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-24f, 18f), new Vector2(500f, 30f), UiKit.Muted);
        }

        private GameObject BuildMain()
        {
            RectTransform panel = UiKit.Rect(canvas, "MainButtons", new Vector2(0f, 0.5f), new Vector2(110f, -80f), new Vector2(420f, 470f));
            float y = 0f;
            AddMenuButton(panel, "menu.play", ref y, ShowPlay);
            AddMenuButton(panel, "menu.practice", ref y, StartPractice);
            AddMenuButton(panel, "menu.settings", ref y, () => Overlay(SettingsScreen.Build(canvas, ShowMain)));
            AddMenuButton(panel, "menu.stats", ref y, () => Overlay(StatsScreen.Build(canvas, ShowMain)));
            AddMenuButton(panel, "menu.exit", ref y, Quit);
            return panel.gameObject;
        }

        private GameObject BuildPlay()
        {
            RectTransform panel = UiKit.Rect(canvas, "PlayPanel", new Vector2(0f, 0.5f), new Vector2(110f, -80f), new Vector2(620f, 520f), UiKit.Panel);
            UiKit.Label(panel, "menu.play", 36, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(30f, -24f), new Vector2(560f, 50f), UiKit.Accent, FontStyle.Bold);
            const float width = 560f;
            Button difficulty = null;
            UiKit.Selector(panel, "menu.game", new Vector2(0f, -100f), width, GameOptions, gameIndex, i => gameIndex = i);
            UiKit.Selector(panel, "menu.opponent", new Vector2(0f, -168f), width, OpponentOptions, opponentIndex, i =>
            {
                opponentIndex = i;
                if (difficulty != null) difficulty.interactable = opponentIndex == 0;
            });
            difficulty = UiKit.Selector(panel, "menu.ailevel", new Vector2(0f, -236f), width, DifficultyOptions, difficultyIndex, i => difficultyIndex = i);

            UiKit.Button(panel, "menu.online.play", new Vector2(0.5f, 1f), new Vector2(0f, -316f), new Vector2(width, 50f), ShowOnline, 22);

            UiKit.Button(panel, "common.start", new Vector2(0.5f, 0f), new Vector2(-145f, 30f), new Vector2(270f, 70f), StartMatch, 30);
            UiKit.Button(panel, "common.back", new Vector2(0.5f, 0f), new Vector2(145f, 30f), new Vector2(270f, 70f), ShowMain, 26);
            return panel.gameObject;
        }

        private void AddMenuButton(RectTransform panel, string text, ref float y, System.Action action)
        {
            Button button = UiKit.Button(panel, text, new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(420f, 76f), () =>
            {
                Click();
                action();
            }, 30);
            button.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
            y -= 94f;
        }

        private void ShowPlay()
        {
            mainPanel.SetActive(false);
            playPanel.SetActive(true);
        }

        private void ShowOnline()
        {
            Click();
            ShowOnlineLobby();
        }

        private OnlineLobbyPanel ShowOnlineLobby()
        {
            playPanel.SetActive(false);
            mainPanel.SetActive(false);
            OnlineLobbyPanel lobby = OnlineLobbyPanel.Build(canvas, ShowMain);
            if (audioManager != null)
            {
                foreach (Button button in lobby.GetComponentsInChildren<Button>(true))
                {
                    button.onClick.AddListener(audioManager.PlayUiClick);
                }
            }

            return lobby;
        }

        private void ShowMain()
        {
            Click();
            playPanel.SetActive(false);
            mainPanel.SetActive(true);
        }

        private void Overlay(GameObject overlay)
        {
            mainPanel.SetActive(false);
            if (audioManager != null)
            {
                foreach (Button button in overlay.GetComponentsInChildren<Button>(true))
                {
                    button.onClick.AddListener(audioManager.PlayUiClick);
                }
            }
        }

        private void StartMatch()
        {
            Click();
            if (SaveSystem.Settings.aiDifficulty != difficultyIndex)
            {
                SaveSystem.UpdateSettings(s => s.aiDifficulty = difficultyIndex);
            }

            MatchLaunch.Request(gameIndex == 0 ? GameMode.EightBall : GameMode.NineBall, opponentIndex == 0, difficultyIndex);
            Load(MatchScene);
        }

        private void StartPractice()
        {
            MatchLaunch.RequestPractice(GameMode.EightBall);
            Load(MatchScene);
        }

        private static void Load(string scene) => SceneManager.LoadScene(scene);

        private void Click()
        {
            if (audioManager != null)
            {
                audioManager.PlayUiClick();
            }
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
