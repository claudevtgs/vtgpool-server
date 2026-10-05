using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VTG.Pool.Localization;
using VTG.Pool.Network;
using VTG.Pool.Rules;
using VTG.Pool.Save;

namespace VTG.Pool.UI
{
    /// <summary>
    /// Main-menu ONLINE panel: player name, server address, game choice; CREATE ROOM shows a 6-character code to
    /// send to friends, JOIN enters a friend's code. Formats: 1 v 1, 3 or 4 players (9-ball rotation) and 2 v 2
    /// doubles. The lobby lists who is in; a full room starts by itself, and the host may START a free-for-all early.
    /// </summary>
    public sealed class OnlineLobbyPanel : MonoBehaviour
    {
        private static readonly string[] GameOptions = { "opt.8ball", "opt.9ball" };
        private static readonly string[] FormatOptions = { "online.fmt.1v1", "online.fmt.ffa3", "online.fmt.ffa4", "online.fmt.2v2" };

        private OnlineClient client;
        private InputField nameField;
        private InputField serverField;
        private InputField codeField;
        private Text status;
        private Text codeLabel;
        private Button createButton;
        private Button joinButton;
        private Button watchButton;
        private Button cancelButton;
        private Action onBack;
        private int gameIndex;
        private int formatIndex;
        private bool busy;
        private Text seatsText;
        private Button startButton;

        /// <summary>Lobby seat list as shown (one line per seat).</summary>
        public string SeatsText => seatsText != null ? seatsText.text : string.Empty;

        /// <summary>Host START button (free-for-all rooms with 2+ players).</summary>
        public Button StartButton => startButton;

        /// <summary>0 = 1v1, 1 = 3 players, 2 = 4 players, 3 = 2v2.</summary>
        public int FormatIndex
        {
            get => formatIndex;
            set => formatIndex = Mathf.Clamp(value, 0, FormatOptions.Length - 1);
        }

        /// <summary>Seats for a format.</summary>
        public static int PlayersFor(int format) => format == 1 ? 3 : format >= 2 ? 4 : 2;

        public InputField NameField => nameField;

        public InputField ServerField => serverField;

        public InputField CodeField => codeField;

        public Text Status => status;

        /// <summary>Room code shown after CREATE ROOM (empty otherwise).</summary>
        public string ShownCode { get; private set; } = string.Empty;

        /// <summary>Browser build: link that opens the game and joins this room (empty elsewhere).</summary>
        public string InviteLink { get; private set; } = string.Empty;

        /// <summary>Room code from an invite link (?room=K7P2QX), or null.</summary>
        public static string RoomFromUrl(string pageUrl)
        {
            if (string.IsNullOrEmpty(pageUrl) || !Uri.TryCreate(pageUrl, UriKind.Absolute, out Uri uri))
            {
                return null;
            }

            foreach (string part in uri.Query.TrimStart('?').Split('&'))
            {
                string[] pair = part.Split('=');
                if (pair.Length == 2 && pair[0] == "room" && pair[1].Length == 6)
                {
                    return Uri.UnescapeDataString(pair[1]).ToUpperInvariant();
                }
            }

            return null;
        }

        /// <summary>Invite link for a room on the current page (http/https pages only).</summary>
        public static string InviteLinkFor(string pageUrl, string code)
        {
            if (string.IsNullOrEmpty(pageUrl) || !Uri.TryCreate(pageUrl, UriKind.Absolute, out Uri uri) || (uri.Scheme != "http" && uri.Scheme != "https")
                || OnlineClient.IsPortalPage(pageUrl))
            {
                // Portal frames (itch.io) cannot pass ?room= to the game: share the code instead.
                return string.Empty;
            }

            return uri.GetLeftPart(UriPartial.Path) + "?room=" + code;
        }

        /// <summary>Fills in a code and joins (invite links).</summary>
        public void JoinWithCode(string code)
        {
            codeField.text = code;
            JoinRoom();
        }

        public static OnlineLobbyPanel Build(RectTransform canvas, Action back, OnlineClient onlineClient = null)
        {
            RectTransform panel = UiKit.Rect(canvas, "OnlinePanel", new Vector2(0f, 0.5f), new Vector2(110f, -140f), new Vector2(760f, 780f), UiKit.Panel);
            OnlineLobbyPanel lobby = panel.gameObject.AddComponent<OnlineLobbyPanel>();
            lobby.Construct(panel, back, onlineClient != null ? onlineClient : OnlineClient.Instance);
            return lobby;
        }

        private void Construct(RectTransform panel, Action back, OnlineClient onlineClient)
        {
            client = onlineClient;
            onBack = back;
            GameSettings settings = SaveSystem.Settings;
            const float labelWidth = 220f;
            const float fieldWidth = 470f;

            UiKit.Label(panel, "online.title", 36, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(30f, -24f), new Vector2(700f, 50f), UiKit.Accent, FontStyle.Bold);

            UiKit.Label(panel, "online.name", 22, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(30f, -86f), new Vector2(labelWidth, 50f));
            nameField = UiKit.Input(panel, "Name", new Vector2(0f, 1f), new Vector2(30f + labelWidth, -86f), new Vector2(fieldWidth, 50f), settings.playerName, "online.name", 20);

            string server = !string.IsNullOrEmpty(settings.serverUrl) ? settings.serverUrl : OnlineClient.SuggestedUrl(Application.absoluteURL);
            UiKit.Label(panel, "online.server", 22, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(30f, -146f), new Vector2(labelWidth, 50f));
            serverField = UiKit.Input(panel, "Server", new Vector2(0f, 1f), new Vector2(30f + labelWidth, -146f), new Vector2(fieldWidth, 50f), server, "pool.example.com", 120, 20);

            UiKit.Selector(panel, "menu.game", new Vector2(0f, -206f), 700f, GameOptions, gameIndex, i => gameIndex = i);
            UiKit.Selector(panel, "online.format", new Vector2(0f, -262f), 700f, FormatOptions, formatIndex, i => formatIndex = i);
            createButton = UiKit.Button(panel, "online.create", new Vector2(0f, 1f), new Vector2(30f, -326f), new Vector2(700f, 64f), CreateRoom, 26);

            UiKit.Rect(panel, "Rule", new Vector2(0f, 1f), new Vector2(30f, -402f), new Vector2(700f, 2f), new Color(1f, 1f, 1f, 0.12f));
            UiKit.Label(panel, "online.code", 22, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(30f, -416f), new Vector2(labelWidth, 60f));
            codeField = UiKit.Input(panel, "Code", new Vector2(0f, 1f), new Vector2(30f + labelWidth, -416f), new Vector2(200f, 60f), string.Empty, "K7P2QX", 6, 30);
            codeField.characterValidation = InputField.CharacterValidation.Alphanumeric;
            codeField.onValueChanged.AddListener(v =>
            {
                string upper = v.ToUpperInvariant();
                if (upper != v) codeField.text = upper;
            });
            joinButton = UiKit.Button(panel, "online.join", new Vector2(0f, 1f), new Vector2(30f + labelWidth + 210f, -416f), new Vector2(130f, 60f), JoinRoom, 20);
            watchButton = UiKit.Button(panel, "online.watch", new Vector2(0f, 1f), new Vector2(30f + labelWidth + 350f, -416f), new Vector2(130f, 60f), WatchRoom, 20);

            codeLabel = UiKit.Label(panel, string.Empty, 44, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -484f), new Vector2(700f, 54f), UiKit.Accent, FontStyle.Bold);
            status = UiKit.Label(panel, string.Empty, 18, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -536f), new Vector2(700f, 50f), UiKit.Muted);
            seatsText = UiKit.Label(panel, string.Empty, 20, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(40f, -588f), new Vector2(440f, 96f), Color.white);
            startButton = UiKit.Button(panel, "online.start", new Vector2(1f, 1f), new Vector2(-30f, -600f), new Vector2(230f, 64f), RequestStart, 24);
            startButton.gameObject.SetActive(false);

            cancelButton = UiKit.Button(panel, "online.cancel", new Vector2(0.5f, 0f), new Vector2(-180f, 24f), new Vector2(320f, 64f), Cancel, 24);
            UiKit.Button(panel, "common.back", new Vector2(0.5f, 0f), new Vector2(180f, 24f), new Vector2(320f, 64f), Back, 24);
            cancelButton.gameObject.SetActive(false);

            client.RoomCreated += HandleCreated;
            client.MatchStarted += HandleStarted;
            client.Error += HandleError;
            client.LobbyChanged += HandleLobby;
            client.AudienceChanged += HandleLobby;
            client.PeerLeft += HandleRoomClosed;
        }

        private void HandleRoomClosed()
        {
            if (client.InMatch || SceneManager.GetActiveScene().name == MainMenuController.MatchScene)
            {
                return;
            }

            SetBusy(false);
            ShownCode = string.Empty;
            codeLabel.text = string.Empty;
            seatsText.text = string.Empty;
            startButton.gameObject.SetActive(false);
            ShowStatus("online.err.closed");
        }

        /// <summary>Host: start a free-for-all with the players already in.</summary>
        public void RequestStart()
        {
            if (client != null && client.Role == OnlineRole.Host)
            {
                client.RequestStart();
            }
        }

        private void HandleLobby()
        {
            string[] names = client.Names ?? new string[0];
            var lines = new System.Text.StringBuilder();
            int present = 0;
            for (int i = 0; i < client.Capacity; i++)
            {
                string name = i < names.Length ? names[i] : string.Empty;
                if (!string.IsNullOrEmpty(name)) present++;
                string who = string.IsNullOrEmpty(name) ? Loc.T("online.seat.empty") : name + (i == 0 ? "  " + Loc.T("online.seat.host") : string.Empty) + (i == client.Seat ? "  " + Loc.T("online.seat.you") : string.Empty);
                string side = client.Teams ? Loc.T(i % 2 == 0 ? "online.team.a" : "online.team.b") + " · " : string.Empty;
                lines.Append(side).Append(i + 1).Append(". ").Append(who).Append('\n');
            }

            if (client.Audience > 0)
            {
                lines.Append(Loc.T("online.audience", client.Audience)).Append('\n');
            }

            seatsText.text = lines.ToString().TrimEnd('\n');
            status.text = client.IsSpectator
                ? Loc.T("online.watching", client.RoomCode)
                : Loc.T("online.lobby.count", present, client.Capacity) + (InviteLink.Length > 0 ? "\n" + InviteLink : string.Empty);
            startButton.gameObject.SetActive(client.Role == OnlineRole.Host && !client.Teams && client.Capacity > 2 && present >= 2);
        }

        private void OnDestroy()
        {
            if (client != null)
            {
                client.RoomCreated -= HandleCreated;
                client.MatchStarted -= HandleStarted;
                client.Error -= HandleError;
                client.LobbyChanged -= HandleLobby;
                client.AudienceChanged -= HandleLobby;
                client.PeerLeft -= HandleRoomClosed;
            }
        }

        private bool Prepare(out string url, out string playerName)
        {
            url = serverField.text.Trim();
            playerName = string.IsNullOrWhiteSpace(nameField.text) ? SaveSystem.Settings.playerName : nameField.text.Trim();
            if (url.Length == 0)
            {
                ShowStatus("online.err.noserver");
                return false;
            }

            string savedName = playerName;
            string savedUrl = url;
            SaveSystem.UpdateSettings(s =>
            {
                s.playerName = savedName;
                s.serverUrl = savedUrl;
            });
            return true;
        }

        public void CreateRoom()
        {
            if (busy || !Prepare(out string url, out string playerName))
            {
                return;
            }

            SetBusy(true);
            ShowStatus("online.connecting");
            int seats = PlayersFor(formatIndex);
            // More than two players without teams: 9-ball rotation (the server enforces this too).
            int mode = seats > 2 && formatIndex != 3 ? 1 : gameIndex;
            client.CreateRoom(url, playerName, mode, seats, formatIndex == 3);
        }

        public void JoinRoom()
        {
            string code = codeField.text.Trim().ToUpperInvariant();
            if (busy)
            {
                return;
            }

            if (code.Length != 6)
            {
                ShowStatus("online.err.nocode");
                return;
            }

            if (!Prepare(out string url, out string playerName))
            {
                return;
            }

            SetBusy(true);
            status.text = Loc.T("online.joining", code);
            client.JoinRoom(url, code, playerName);
        }

        /// <summary>Watch a room as a spectator (its lobby, or the match already running).</summary>
        public void WatchRoom()
        {
            string code = codeField.text.Trim().ToUpperInvariant();
            if (busy)
            {
                return;
            }

            if (code.Length != 6)
            {
                ShowStatus("online.err.nocode");
                return;
            }

            if (!Prepare(out string url, out string playerName))
            {
                return;
            }

            SetBusy(true);
            status.text = Loc.T("online.joining", code);
            client.Watch(url, code, playerName);
        }

        private void Cancel()
        {
            client.Leave();
            SetBusy(false);
            ShownCode = string.Empty;
            codeLabel.text = string.Empty;
            status.text = string.Empty;
            seatsText.text = string.Empty;
            startButton.gameObject.SetActive(false);
        }

        private void Back()
        {
            if (busy)
            {
                Cancel();
            }

            Destroy(gameObject);
            onBack?.Invoke();
        }

        private void HandleCreated(string code)
        {
            ShownCode = code;
            codeLabel.text = code;
            InviteLink = InviteLinkFor(Application.absoluteURL, code);
            status.text = Loc.T("online.created", code) + (InviteLink.Length > 0 ? "\n" + InviteLink : string.Empty);
            GUIUtility.systemCopyBuffer = InviteLink.Length > 0 ? InviteLink : code;
        }

        private void HandleStarted()
        {
            status.text = Loc.T("online.starting.n", string.Join(", ", client.Names));
            MatchLaunch.RequestOnline(client.Mode == 1 ? GameMode.NineBall : GameMode.EightBall, client.Seat, client.Names, client.Teams);
            SceneManager.LoadScene(MainMenuController.MatchScene);
        }

        private void HandleError(string code)
        {
            SetBusy(false);
            ShownCode = string.Empty;
            codeLabel.text = string.Empty;
            seatsText.text = string.Empty;
            startButton.gameObject.SetActive(false);
            ShowStatus("online.err." + code);
        }

        private void ShowStatus(string key)
        {
            status.text = Loc.Has(key) ? Loc.T(key) : key;
        }

        private void SetBusy(bool value)
        {
            busy = value;
            createButton.interactable = !value;
            joinButton.interactable = !value;
            watchButton.interactable = !value;
            cancelButton.gameObject.SetActive(value);
        }
    }
}
