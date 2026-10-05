using System;
using UnityEngine;

namespace VTG.Pool.Network
{
    public enum OnlineRole
    {
        None,
        Host,
        Guest,

        /// <summary>Watches a room without a seat.</summary>
        Spectator
    }

    /// <summary>Room protocol message (see Server/server.js). Unused fields are ignored by both sides.</summary>
    [Serializable]
    public sealed class RoomMessage
    {
        public string t;
        public string code;
        public string name;
        public int mode;
        public string d;
        public string role;
        public string host;
        public string guest;
        public string m;
        public int seat;
        public int from;
        public int players;
        public int capacity;
        public bool teams;
        public string[] names;
        public int hostSeat;
        public bool closed;
        public bool started;
        public int count;
        public int spectators;
    }

    /// <summary>
    /// Connection to the room server (survives scene loads): create / join a room by code, relay game messages to
    /// the other player, report the peer leaving or the connection dropping. All events fire on the main thread.
    /// </summary>
    public sealed class OnlineClient : MonoBehaviour
    {
        private const float PingInterval = 20f;

        private static OnlineClient instance;
        private IWebSocketTransport transport;
        private Action pendingOnOpen;
        private float pingTimer;

        public static OnlineClient Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("OnlineClient");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<OnlineClient>();
                }

                return instance;
            }
        }

        public static bool Exists => instance != null;

        public bool Connected => transport != null && transport.State == TransportState.Open;

        public OnlineRole Role { get; private set; }

        public string RoomCode { get; private set; }

        public string HostName { get; private set; }

        public string GuestName { get; private set; }

        /// <summary>Game mode chosen by the host (0 = 8-ball, 1 = 9-ball).</summary>
        public int Mode { get; private set; }

        /// <summary>A match is in progress (everyone in the room).</summary>
        public bool InMatch { get; private set; }

        /// <summary>This client's seat (0 = host). Seats alternate teams in doubles: 0 &amp; 2 vs 1 &amp; 3.</summary>
        public int Seat { get; private set; }

        /// <summary>Player names by seat (lobby: empty strings for free seats).</summary>
        public string[] Names { get; private set; } = new string[0];

        /// <summary>Doubles (2 v 2).</summary>
        public bool Teams { get; private set; }

        /// <summary>Seats in the room (2-4).</summary>
        public int Capacity { get; private set; } = 2;

        /// <summary>Lobby seats changed (someone joined or left before the start).</summary>
        public event Action LobbyChanged;

        public event Action<string> RoomCreated;

        public event Action MatchStarted;

        public event Action<string> GameMessage;

        public event Action PeerLeft;

        /// <summary>A player left a started match (seat, name). The match goes on while two or more remain.</summary>
        public event Action<int, string> PlayerLeft;

        /// <summary>Seat that hosts (rule authority); moves to the lowest remaining seat when the host leaves.</summary>
        public int HostSeat { get; private set; }

        /// <summary>This device watches the room (no seat).</summary>
        public bool IsSpectator => Role == OnlineRole.Spectator;

        /// <summary>Spectators in the room.</summary>
        public int Audience { get; private set; }

        /// <summary>The audience count changed.</summary>
        public event Action AudienceChanged;

        /// <summary>Watching a room (its lobby, or straight into a running match).</summary>
        public void Watch(string url, string code, string playerName)
        {
            ConnectThen(url, () => Send(new RoomMessage { t = "watch", code = code.Trim().ToUpperInvariant(), name = playerName }));
        }

        /// <summary>Spectator: the match scene is ready, ask the host for the table.</summary>
        public void RequestSync() => Send(new RoomMessage { t = "sync" });

        /// <summary>Server error code (room_not_found, room_full, ...) or connection failure text.</summary>
        public event Action<string> Error;

        public event Action Disconnected;

        /// <summary>Builds the WebSocket URL from a user entry: "pool.example.com" → wss://pool.example.com/ws.</summary>
        public static string NormalizeUrl(string entry)
        {
            string url = (entry ?? string.Empty).Trim();
            if (url.Length == 0)
            {
                return url;
            }

            if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) url = "wss://" + url.Substring(8);
            else if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) url = "ws://" + url.Substring(7);
            else if (!url.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("wss://", StringComparison.OrdinalIgnoreCase)) url = "wss://" + url;

            int pathStart = url.IndexOf('/', url.IndexOf("//", StringComparison.Ordinal) + 2);
            if (pathStart < 0)
            {
                url += "/ws";
            }

            return url;
        }

        /// <summary>Default server for a browser build: the page's own host at /ws.</summary>
        /// <summary>Public room server (Render free tier) used by native builds and game portals.</summary>
        public const string PublicServerUrl = "wss://vtgpool-server.onrender.com/ws";

        /// <summary>The page runs inside a game portal's frame (itch.io) that does not host the room server.</summary>
        public static bool IsPortalPage(string pageUrl)
        {
            if (string.IsNullOrEmpty(pageUrl) || !Uri.TryCreate(pageUrl, UriKind.Absolute, out Uri uri))
            {
                return false;
            }

            string host = uri.Host.ToLowerInvariant();
            return host.EndsWith("itch.io") || host.EndsWith("itch.zone") || host.EndsWith("hwcdn.net");
        }

        /// <summary>Server address to suggest: the page's own server when self-hosted, otherwise <see cref="PublicServerUrl"/>.</summary>
        public static string SuggestedUrl(string pageUrl)
        {
            string own = IsPortalPage(pageUrl) ? string.Empty : DefaultUrlForPage(pageUrl);
            return own.Length > 0 ? own : PublicServerUrl;
        }

        public static string DefaultUrlForPage(string pageUrl)
        {
            if (string.IsNullOrEmpty(pageUrl) || !Uri.TryCreate(pageUrl, UriKind.Absolute, out Uri uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                return string.Empty;
            }

            return (uri.Scheme == "https" ? "wss://" : "ws://") + uri.Authority + "/ws";
        }

        public void CreateRoom(string url, string playerName, int mode) => CreateRoom(url, playerName, mode, 2, false);

        /// <summary>Creates a room for 2-4 players; <paramref name="teams"/> = doubles (needs 4).</summary>
        public void CreateRoom(string url, string playerName, int mode, int players, bool teams)
        {
            ConnectThen(url, () => Send(new RoomMessage { t = "create", name = playerName, mode = mode, players = players, teams = teams }));
        }

        /// <summary>Host: start with the players present (free-for-all with 2+ players; a full room starts by itself).</summary>
        public void RequestStart() => Send(new RoomMessage { t = "start" });

        public void JoinRoom(string url, string code, string playerName)
        {
            ConnectThen(url, () => Send(new RoomMessage { t = "join", code = (code ?? string.Empty).Trim().ToUpperInvariant(), name = playerName }));
        }

        /// <summary>Sends a game message to the other player.</summary>
        public void SendGame(string payload)
        {
            Send(new RoomMessage { t = "relay", d = payload });
        }

        /// <summary>Leaves the room and closes the connection.</summary>
        public void Leave()
        {
            if (Connected)
            {
                Send(new RoomMessage { t = "leave" });
            }

            ResetRoom();
            pendingOnOpen = null;
            transport?.Close();
        }

        private void ConnectThen(string url, Action action)
        {
            ResetRoom();
            if (Connected)
            {
                action();
                return;
            }

            transport?.Dispose();
            transport = WebSocketTransportFactory.Create();
            transport.Opened += HandleOpened;
            transport.MessageReceived += HandleMessage;
            transport.Closed += HandleClosed;
            pendingOnOpen = action;
            transport.Connect(NormalizeUrl(url));
        }

        private void ResetRoom()
        {
            Role = OnlineRole.None;
            RoomCode = null;
            InMatch = false;
        }

        private void Send(RoomMessage message)
        {
            transport?.Send(JsonUtility.ToJson(message));
        }

        private void Update()
        {
            transport?.Poll();
            if (Connected)
            {
                pingTimer += Time.unscaledDeltaTime;
                if (pingTimer > PingInterval)
                {
                    pingTimer = 0f;
                    Send(new RoomMessage { t = "ping" });
                }
            }
        }

        private void HandleOpened()
        {
            Action action = pendingOnOpen;
            pendingOnOpen = null;
            action?.Invoke();
        }

        private void HandleMessage(string text)
        {
            RoomMessage message;
            try
            {
                message = JsonUtility.FromJson<RoomMessage>(text);
            }
            catch (Exception)
            {
                return;
            }

            if (message == null)
            {
                return;
            }

            switch (message.t)
            {
                case "created":
                    Role = OnlineRole.Host;
                    RoomCode = message.code;
                    Seat = 0;
                    Capacity = Mathf.Max(2, message.capacity);
                    Teams = message.teams;
                    Mode = message.mode;
                    RoomCreated?.Invoke(message.code);
                    break;
                case "joined":
                    Role = OnlineRole.Guest;
                    RoomCode = message.code;
                    Seat = message.seat;
                    Capacity = Mathf.Max(2, message.capacity);
                    Teams = message.teams;
                    Mode = message.mode;
                    break;
                case "watching":
                    Role = OnlineRole.Spectator;
                    RoomCode = message.code;
                    Seat = -1;
                    Names = message.names ?? new string[0];
                    Capacity = Mathf.Max(2, message.capacity);
                    Teams = message.teams;
                    Mode = message.mode;
                    LobbyChanged?.Invoke();
                    break;
                case "audience":
                    Audience = message.count;
                    AudienceChanged?.Invoke();
                    break;
                case "lobby":
                    Audience = message.spectators;
                    Names = message.names ?? new string[0];
                    Capacity = Mathf.Max(2, message.capacity);
                    Teams = message.teams;
                    Mode = message.mode;
                    LobbyChanged?.Invoke();
                    break;
                case "start":
                    Role = message.role == "host" ? OnlineRole.Host : message.role == "spectator" ? OnlineRole.Spectator : OnlineRole.Guest;
                    RoomCode = message.code;
                    Seat = message.seat;
                    Names = message.names != null && message.names.Length > 0 ? message.names : new[] { message.host, message.guest };
                    HostName = Names[0];
                    GuestName = Names.Length > 1 ? Names[1] : string.Empty;
                    Teams = message.teams;
                    Capacity = Names.Length;
                    Mode = message.mode;
                    HostSeat = Mathf.Max(0, message.hostSeat);
                    InMatch = true;
                    MatchStarted?.Invoke();
                    break;
                case "relay":
                    GameMessage?.Invoke(message.d);
                    break;
                case "peer_left":
                    if (message.hostSeat >= 0)
                    {
                        HostSeat = message.hostSeat;
                        if (!IsSpectator) Role = Seat == HostSeat ? OnlineRole.Host : OnlineRole.Guest;
                    }

                    if (InMatch)
                    {
                        PlayerLeft?.Invoke(message.seat, message.name);
                    }

                    if (message.closed)
                    {
                        // Room is gone (everyone else left, or the lobby's host left).
                        InMatch = false;
                        PeerLeft?.Invoke();
                    }

                    break;
                case "error":
                    Error?.Invoke(message.m);
                    break;
            }
        }

        private void HandleClosed(string reason)
        {
            bool wasActive = Role != OnlineRole.None;
            ResetRoom();
            if (pendingOnOpen != null)
            {
                pendingOnOpen = null;
                Error?.Invoke("connect_failed");
                return;
            }

            if (wasActive)
            {
                Disconnected?.Invoke();
            }
        }

        private void OnDestroy()
        {
            transport?.Dispose();
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
