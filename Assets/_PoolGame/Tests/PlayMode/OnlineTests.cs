using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Match;
using VTG.Pool.Network;
using VTG.Pool.Rules;
using VTG.Pool.Save;
using VTG.Pool.UI;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace VTG.Pool.Tests
{
    /// <summary>
    /// Online rooms end to end against the real room server (Server/server.js, started with Node for these tests;
    /// ignored when Node is not installed). The "other device" is a second <see cref="OnlineClient"/> driven by the test.
    /// </summary>
    public sealed class OnlineTests
    {
        private static Process server;
        private static string url;
        private string directory;
        private readonly List<GameObject> spawned = new List<GameObject>();

        [OneTimeSetUp]
        public void StartServer()
        {
            string script = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Server", "server.js"));
            int port = 19000 + Random.Range(0, 900);
            try
            {
                server = Process.Start(new ProcessStartInfo("node", $"\"{script}\" --port {port}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
            }
            catch (System.Exception exception)
            {
                server = null;
                Debug.LogWarning("[OnlineTests] Node not available: " + exception.Message);
                return;
            }

            server.StandardOutput.ReadLine(); // "VTG Pool room server on ..."
            url = $"ws://127.0.0.1:{port}/ws";
        }

        [OneTimeTearDown]
        public void StopServer()
        {
            if (server != null && !server.HasExited)
            {
                server.Kill();
            }

            server = null;
        }

        [SetUp]
        public void SetUp()
        {
            if (server == null)
            {
                Assert.Ignore("Node.js is not installed; online tests need Server/server.js.");
            }

            directory = Path.Combine(Path.GetTempPath(), "VTGPoolOnlineTests_" + System.Guid.NewGuid().ToString("N"));
            SaveSystem.DirectoryOverride = directory;
            SaveSystem.ClearCache();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject go in spawned)
            {
                if (go != null) Object.Destroy(go);
            }

            spawned.Clear();
            if (OnlineClient.Exists)
            {
                OnlineClient.Instance.Leave();
                Object.Destroy(OnlineClient.Instance.gameObject);
            }

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

        private OnlineClient NewClient(string name)
        {
            var go = new GameObject("TestClient_" + name);
            Object.DontDestroyOnLoad(go);
            spawned.Add(go);
            return go.AddComponent<OnlineClient>();
        }

        private static IEnumerator WaitFor(System.Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }

            Assert.IsTrue(condition(), "Timed out waiting for " + what);
        }

        /// <summary>Collects game messages received by a client.</summary>
        private static List<NetGameMessage> Record(OnlineClient client)
        {
            var list = new List<NetGameMessage>();
            client.GameMessage += payload =>
            {
                NetGameMessage message = NetGameMessage.FromJson(payload);
                if (message != null) list.Add(message);
            };
            return list;
        }

        private static IEnumerator Pair(OnlineClient host, OnlineClient guest, int mode)
        {
            string code = null;
            host.RoomCreated += c => code = c;
            host.CreateRoom(url, "Host", mode);
            yield return WaitFor(() => code != null, 10f, "room code");
            guest.JoinRoom(url, code, "Guest");
            yield return WaitFor(() => host.InMatch && guest.InMatch, 10f, "both players in the room");
        }

        [UnityTest]
        public IEnumerator Client_CreateJoinRelayAndLeave()
        {
            OnlineClient a = NewClient("A");
            OnlineClient b = NewClient("B");
            string error = null;
            b.Error += e => error = e;
            b.JoinRoom(url, "ZZZZZZ", "Nobody");
            yield return WaitFor(() => error != null, 10f, "room_not_found");
            Assert.AreEqual("room_not_found", error);

            yield return Pair(a, b, 1);
            Assert.AreEqual(OnlineRole.Host, a.Role);
            Assert.AreEqual(OnlineRole.Guest, b.Role);
            Assert.AreEqual("Host", b.HostName);
            Assert.AreEqual(1, b.Mode);

            string received = null;
            b.GameMessage += m => received = m;
            a.SendGame("{\"type\":\"aim\",\"yaw\":12.5}");
            yield return WaitFor(() => received != null, 5f, "relayed message");
            Assert.AreEqual(12.5f, NetGameMessage.FromJson(received).yaw, 1e-4f);

            bool left = false;
            a.PeerLeft += () => left = true;
            b.Leave();
            yield return WaitFor(() => left, 5f, "peer_left");
        }

        [UnityTest]
        public IEnumerator Lobby_CreateRoomAndStartMatchWhenFriendJoins()
        {
            SceneManager.LoadScene("01_MainMenu");
            yield return null;
            yield return null;
            Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).First(b => b.name == "Button_PLAY").onClick.Invoke();
            yield return null;
            Button online = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).First(b => b.name == "Button_ONLINE");
            online.onClick.Invoke();
            yield return null;
            OnlineLobbyPanel lobby = Object.FindAnyObjectByType<OnlineLobbyPanel>();
            Assert.IsNotNull(lobby, "ONLINE opens the lobby");
            lobby.ServerField.text = url;
            lobby.NameField.text = "An";
            lobby.CreateRoom();
            yield return WaitFor(() => lobby != null && lobby.ShownCode.Length == 6, 10f, "room code shown");
            Assert.AreEqual(url, SaveSystem.Settings.serverUrl, "Server address remembered");

            OnlineClient friend = NewClient("Friend");
            friend.JoinRoom(url, lobby.ShownCode.ToLowerInvariant(), "Binh");
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "02_Match", 10f, "match scene");
            yield return null;
            yield return null;
            MatchManager match = Object.FindAnyObjectByType<MatchManager>();
            Assert.IsTrue(match.IsOnline);
            Assert.AreEqual(0, match.OnlineLocalIndex, "The room creator hosts and breaks");
            Assert.AreEqual("An", match.State.Players[0].DisplayName);
            Assert.AreEqual("Binh", match.State.Players[1].DisplayName);
            Assert.AreEqual(PlayerKind.Remote, match.State.Players[1].Kind);
        }

        [UnityTest]
        public IEnumerator Host_SendsStateAndShots_AndPlaysTheGuestsShot()
        {
            OnlineClient host = OnlineClient.Instance;
            OnlineClient guest = NewClient("Guest");
            List<NetGameMessage> toGuest = Record(guest);
            yield return Pair(host, guest, 0);
            MatchLaunch.RequestOnline(GameMode.EightBall, 0, "Host", "Guest");
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            yield return null;
            var shot = Object.FindAnyObjectByType<ShotController>();
            var turns = Object.FindAnyObjectByType<TurnManager>();
            var placement = Object.FindAnyObjectByType<CueBallPlacementController>();
            Assert.IsNotNull(OnlineMatchController.Active);

            guest.SendGame(new NetGameMessage { type = NetGameMessage.Ready }.ToJson());
            yield return WaitFor(() => toGuest.Any(m => m.type == NetGameMessage.State && m.newRack), 5f, "rack state");
            NetGameMessage rack = toGuest.Last(m => m.type == NetGameMessage.State);
            Assert.AreEqual(16, rack.balls.Length);
            Assert.AreEqual(0, rack.match.current);
            Assert.IsFalse(shot.NetworkLocked, "Host breaks");

            // Host breaks; the guest receives the shot and then the authoritative result.
            yield return WaitFor(() => placement.IsPlacing, 5f, "ball in hand for the break");
            Time.timeScale = 4f;
            for (int attempt = 0; attempt < 4 && turns.State.CurrentPlayerIndex == 0 && !turns.State.IsGameOver; attempt++)
            {
                if (placement.IsPlacing)
                {
                    Assert.IsTrue(placement.TryConfirm());
                }

                int states = toGuest.Count(m => m.type == NetGameMessage.State);
                PoolBall target = BallRegistry.Balls.Where(b => !b.IsCueBall && PracticeSession.IsOnTable(b)).OrderBy(b => b.BallId).First();
                Vector3 aim = target.Position - shot.CueBall.Position;
                yield return WaitFor(() => shot.Phase == ShotPhase.Aiming, 10f, "aiming");
                Assert.IsTrue(shot.ExecuteShot(new ShotParameters(aim, attempt == 0 ? 1f : 0.25f, Vector2.zero)), "Host shot accepted");
                yield return WaitFor(() => toGuest.Count(m => m.type == NetGameMessage.State) > states, 40f, "state after the host's shot");
                Assert.IsTrue(toGuest.Any(m => m.type == NetGameMessage.Shot), "Shot forwarded to the guest");
                yield return null;
            }

            if (turns.State.IsGameOver)
            {
                Assert.Inconclusive("Game ended before the guest's turn.");
            }

            Assert.AreEqual(1, turns.State.CurrentPlayerIndex, "Turn passed to the guest");
            yield return null;
            Assert.IsTrue(shot.NetworkLocked, "Host cannot play during the guest's turn");
            Assert.IsFalse(placement.IsPlacing && !shot.InputOwnedElsewhere);

            // The guest plays: aim stream, then the shot itself.
            int shotsBefore = turns.State.ShotsPlayed;
            Vector3 cue = shot.CueBall.IsPocketed ? FindTable().HeadSpot : shot.CueBall.Position;
            cue.y = shot.CueBall.Radius + FindTable().SurfaceHeight;
            guest.SendGame(new NetGameMessage { type = NetGameMessage.Aim, yaw = 30f }.ToJson());
            yield return WaitFor(() => Mathf.Abs(Mathf.DeltaAngle(Object.FindAnyObjectByType<Aiming.AimSystem>().AimYawDegrees, 30f)) < 0.01f, 5f, "remote aim");
            int statesBefore = toGuest.Count(m => m.type == NetGameMessage.State);
            guest.SendGame(new NetGameMessage { type = NetGameMessage.Shot, yaw = 30f, power = 0.3f, px = cue.x, py = cue.y, pz = cue.z }.ToJson());
            yield return WaitFor(() => turns.State.ShotsPlayed > shotsBefore, 40f, "host evaluating the guest's shot");
            yield return WaitFor(() => toGuest.Count(m => m.type == NetGameMessage.State) > statesBefore, 10f, "state after the guest's shot");
            Assert.AreEqual(turns.State.ShotsPlayed, toGuest.Last(m => m.type == NetGameMessage.State).match.shotsPlayed);
        }

        [UnityTest]
        public IEnumerator Guest_AppliesHostStateAndSendsItsShot()
        {
            OnlineClient fakeHost = NewClient("Host");
            List<NetGameMessage> toHost = Record(fakeHost);
            OnlineClient guest = OnlineClient.Instance;
            yield return Pair(fakeHost, guest, 0);
            MatchLaunch.RequestOnline(GameMode.EightBall, 1, "Host", "Guest");
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            yield return null;
            var shot = Object.FindAnyObjectByType<ShotController>();
            var turns = Object.FindAnyObjectByType<TurnManager>();
            var rack = Object.FindAnyObjectByType<RackManager>();
            Assert.IsTrue(turns.RemoteAuthority, "The guest never evaluates rules itself");
            yield return WaitFor(() => toHost.Any(m => m.type == NetGameMessage.Ready), 5f, "guest ready");
            Assert.IsTrue(shot.NetworkLocked, "Host breaks first");

            // Host state: guest to shoot, 1-ball moved, 3-ball pocketed.
            BallSnapshot[] table = NetGameMessage.CaptureTable(rack.Balls);
            int one = System.Array.FindIndex(table, b => b.id == 1);
            int three = System.Array.FindIndex(table, b => b.id == 3);
            table[one].x += 0.2f;
            table[one].z -= 0.5f;
            table[three].onTable = false;
            NetMatchState state = NetMatchState.Capture(turns.State);
            state.current = 1;
            state.isBreak = false;
            state.ballInHand = 0;
            state.shotsPlayed = 1;
            state.ballsOnTable = state.ballsOnTable.Where(b => b != 3).ToArray();
            state.hasOutcome = true;
            state.message = "Host to shoot.";
            fakeHost.SendGame(new NetGameMessage { type = NetGameMessage.State, match = state, balls = table }.ToJson());
            yield return WaitFor(() => turns.State.CurrentPlayerIndex == 1, 5f, "host state applied");
            yield return null;
            Assert.Less(Vector3.Distance(rack.GetBall(1).Position, new Vector3(table[one].x, table[one].y, table[one].z)), 1e-4f, "Ball placed where the host has it");
            Assert.IsFalse(rack.GetBall(3).gameObject.activeSelf, "Ball pocketed on the host is gone");
            Assert.IsFalse(turns.State.IsOnTable(3));
            Assert.IsFalse(shot.NetworkLocked, "Guest's turn");

            // Guest shoots: the host gets the parameters; the guest waits for the verdict.
            Assert.IsTrue(shot.ExecuteShot(new ShotParameters(Vector3.forward, 0.35f, new Vector2(0.1f, -0.2f), 12f)));
            yield return WaitFor(() => toHost.Any(m => m.type == NetGameMessage.Shot), 5f, "guest shot sent");
            NetGameMessage sent = toHost.First(m => m.type == NetGameMessage.Shot);
            Assert.AreEqual(0.35f, sent.power, 1e-4f);
            Assert.AreEqual(12f, sent.elevation, 1e-4f);
            Assert.AreEqual(-0.2f, sent.tipY, 1e-4f);
            Assert.IsTrue(OnlineMatchController.Active.AwaitingAuthority);
            Assert.IsTrue(shot.NetworkLocked, "No second shot before the host's result");

            NetMatchState after = NetMatchState.Capture(turns.State);
            after.current = 0;
            after.shotsPlayed = 2;
            Time.timeScale = 4f;
            fakeHost.SendGame(new NetGameMessage { type = NetGameMessage.State, match = after, balls = NetGameMessage.CaptureTable(rack.Balls) }.ToJson());
            yield return WaitFor(() => turns.State.CurrentPlayerIndex == 0 && !OnlineMatchController.Active.AwaitingAuthority, 15f, "result applied");
            Assert.AreEqual(2, turns.State.ShotsPlayed);
            yield return null;
            Assert.IsTrue(shot.NetworkLocked);
            Assert.IsTrue(shot.ShowRemoteCue, "The opponent's cue is shown while they aim");
        }

        [UnityTest]
        public IEnumerator Client_DoublesLobbyFillsAndStarts()
        {
            OnlineClient[] clients = { NewClient("A1"), NewClient("B1"), NewClient("A2"), NewClient("B2") };
            string code = null;
            clients[0].RoomCreated += c => code = c;
            clients[0].CreateRoom(url, "An", 0, 4, true);
            yield return WaitFor(() => code != null, 10f, "room code");
            Assert.IsTrue(clients[0].Teams);
            Assert.AreEqual(4, clients[0].Capacity);
            string[] names = { "An", "Binh", "Cuong", "Dung" };
            for (int i = 1; i < 3; i++)
            {
                clients[i].JoinRoom(url, code, names[i]);
                int expected = i + 1;
                yield return WaitFor(() => clients[0].Names.Count(n => !string.IsNullOrEmpty(n)) == expected, 10f, "lobby update");
            }

            Assert.AreEqual(2, clients[2].Seat);
            Assert.IsFalse(clients[0].InMatch, "Doubles waits for four");
            clients[3].JoinRoom(url, code, names[3]);
            yield return WaitFor(() => clients.All(c => c.InMatch), 10f, "all four in the match");
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(i, clients[i].Seat);
                CollectionAssert.AreEqual(names, clients[i].Names);
                Assert.IsTrue(clients[i].Teams);
            }

            // Messages reach everyone else in the room.
            var got = new int[4];
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                clients[i].GameMessage += _ => got[k]++;
            }

            clients[1].SendGame(new NetGameMessage { type = NetGameMessage.Aim, yaw = 5f }.ToJson());
            yield return WaitFor(() => got[0] == 1 && got[2] == 1 && got[3] == 1, 5f, "broadcast relay");
            Assert.AreEqual(0, got[1], "Not echoed to the sender");
        }

        [UnityTest]
        public IEnumerator Lobby_FreeForAll_HostStartsWithThree()
        {
            SceneManager.LoadScene("01_MainMenu");
            yield return null;
            yield return null;
            Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).First(b => b.name == "Button_PLAY").onClick.Invoke();
            yield return null;
            Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).First(b => b.name == "Button_ONLINE").onClick.Invoke();
            yield return null;
            OnlineLobbyPanel lobby = Object.FindAnyObjectByType<OnlineLobbyPanel>();
            lobby.ServerField.text = url;
            lobby.NameField.text = "An";
            lobby.FormatIndex = 2;
            lobby.CreateRoom();
            yield return WaitFor(() => lobby != null && lobby.ShownCode.Length == 6, 10f, "room code shown");
            Assert.IsFalse(lobby.StartButton.gameObject.activeSelf, "Nobody to play with yet");
            OnlineClient b = NewClient("B");
            OnlineClient c = NewClient("C");
            b.JoinRoom(url, lobby.ShownCode, "Binh");
            yield return WaitFor(() => b.Seat == 1, 10f, "second player");
            c.JoinRoom(url, lobby.ShownCode, "Cuong");
            yield return WaitFor(() => lobby.SeatsText.Contains("Cuong"), 10f, "lobby lists the third player");
            StringAssert.Contains("Binh", lobby.SeatsText);
            Assert.IsTrue(lobby.StartButton.gameObject.activeSelf, "The host may start early");
            lobby.StartButton.onClick.Invoke();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "02_Match", 10f, "match scene");
            yield return null;
            yield return null;
            MatchManager match = Object.FindAnyObjectByType<MatchManager>();
            Assert.AreEqual(3, match.State.Players.Count);
            Assert.AreEqual(GameMode.NineBall, match.State.Mode, "Three players play 9-ball rotation");
            Assert.AreEqual("Cuong", match.State.Players[2].DisplayName);
            Assert.AreEqual(PlayerKind.LocalHuman, match.State.Players[0].Kind);
            Assert.AreEqual(PlayerKind.Remote, match.State.Players[2].Kind);
            Assert.IsFalse(match.State.HasTeams);
            Assert.AreEqual(2, c.Seat);
            int panels = Object.FindAnyObjectByType<PoolHud>().GetComponentsInChildren<Transform>(false).Count(t => t.name == "PlayerFrame");
            Assert.AreEqual(3, panels, "One HUD panel per player");
        }

        [UnityTest]
        public IEnumerator Doubles_TeamsRotateAndShareTheGroup()
        {
            MatchLaunch.RequestOnline(GameMode.EightBall, 0, new[] { "An", "Binh", "Cuong", "Dung" }, true);
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            yield return null;
            var turns = Object.FindAnyObjectByType<TurnManager>();
            MatchState state = turns.State;
            Assert.AreEqual(4, state.Players.Count);
            Assert.IsTrue(state.HasTeams);
            Assert.AreEqual(new[] { 0, 1, 0, 1 }, Enumerable.Range(0, 4).Select(state.TeamOf).ToArray(), "Seats alternate teams");
            Assert.AreEqual("An & Cuong", state.SideName(2));

            state.IsBreakShot = false;
            var order = new List<int> { state.CurrentPlayerIndex };
            turns.Apply(new ShotOutcome { AssignGroupToShooter = BallGroup.Solids, TurnContinues = false, Message = "open" }, new ShotRecord { FirstObjectBallHit = 1 });
            Assert.AreEqual(BallGroup.Solids, state.Players[0].Group);
            Assert.AreEqual(BallGroup.Solids, state.Players[2].Group, "Team mate shares the group");
            Assert.AreEqual(BallGroup.Stripes, state.Players[1].Group);
            Assert.AreEqual(BallGroup.Stripes, state.Players[3].Group);
            for (int i = 0; i < 3; i++)
            {
                order.Add(state.CurrentPlayerIndex);
                turns.Apply(new ShotOutcome { Message = "miss" }, new ShotRecord { FirstObjectBallHit = 9 });
            }

            order.Add(state.CurrentPlayerIndex);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 0 }, order, "A1 → B1 → A2 → B2");

            turns.Apply(new ShotOutcome { GameOver = true, WinnerIndex = 2, Message = "8" }, new ShotRecord { FirstObjectBallHit = 8 });
            yield return null;
            PoolHud hud = Object.FindAnyObjectByType<PoolHud>();
            int panels = hud.GetComponentsInChildren<Transform>(false).Count(t => t.name == "PlayerFrame");
            Assert.AreEqual(2, panels, "Doubles: one panel per team");
            Text[] texts = hud.GetComponentsInChildren<Text>(false);
            Assert.IsTrue(texts.Any(t => t.text.Contains("An & Cuong")), "Team names on the HUD");
        }

        private IEnumerator Room(OnlineClient host, OnlineClient[] others, int mode, int players, bool teams)
        {
            string code = null;
            host.RoomCreated += c => code = c;
            host.CreateRoom(url, "P0", mode, players, teams);
            yield return WaitFor(() => code != null, 10f, "room code");
            for (int i = 0; i < others.Length; i++)
            {
                others[i].JoinRoom(url, code, "P" + (i + 1));
                int seat = i + 1;
                yield return WaitFor(() => others[seat - 1].Seat == seat || others[seat - 1].InMatch, 10f, "join " + seat);
            }

            yield return WaitFor(() => host.InMatch && others.All(o => o.InMatch), 10f, "everyone in the match");
        }

        [UnityTest]
        public IEnumerator Leave_FreeForAllGoesOnThenForfeit()
        {
            OnlineClient host = OnlineClient.Instance;
            OnlineClient b = NewClient("B");
            OnlineClient c = NewClient("C");
            yield return Room(host, new[] { b, c }, 1, 3, false);
            MatchLaunch.RequestOnline(GameMode.NineBall, 0, host.Names, false);
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            yield return null;
            var turns = Object.FindAnyObjectByType<TurnManager>();
            OnlineMatchController online = OnlineMatchController.Active;
            string lost = null;
            string leftName = null;
            online.ConnectionLost += k => lost = k;
            online.PlayerLeft += n => leftName = n;
            List<NetGameMessage> toB = Record(b);

            c.Leave();
            yield return WaitFor(() => turns.State.Players[2].Left, 10f, "C marked as left");
            Assert.AreEqual("P2", leftName);
            Assert.IsNull(lost, "Two players remain: the match goes on");
            Assert.IsFalse(turns.State.IsGameOver);
            yield return WaitFor(() => toB.Any(m => m.type == NetGameMessage.State && m.match.left.Length == 3 && m.match.left[2]), 5f, "leave shared with B");
            turns.State.CurrentPlayerIndex = 1;
            Assert.AreEqual(0, turns.State.OpponentIndex, "C's turns are skipped");

            b.Leave();
            yield return WaitFor(() => turns.State.IsGameOver, 10f, "forfeit");
            Assert.AreEqual(0, turns.State.WinnerIndex, "The last player standing wins");
            yield return WaitFor(() => lost != null, 8f, "alone notice after the victory");
            Assert.AreEqual("online.alone", lost);
        }

        [UnityTest]
        public IEnumerator Leave_HostLeaves_NextSeatTakesOver()
        {
            OnlineClient fakeHost = NewClient("Host");
            OnlineClient local = OnlineClient.Instance;
            OnlineClient c = NewClient("C");
            yield return Room(fakeHost, new[] { local, c }, 1, 3, false);
            List<NetGameMessage> toC = Record(c);
            MatchLaunch.RequestOnline(GameMode.NineBall, local.Seat, local.Names, false);
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            yield return null;
            var turns = Object.FindAnyObjectByType<TurnManager>();
            OnlineMatchController online = OnlineMatchController.Active;
            Assert.IsFalse(online.IsHost);
            Assert.IsTrue(turns.RemoteAuthority);
            int before = toC.Count(m => m.type == NetGameMessage.State);

            fakeHost.Leave();
            yield return WaitFor(() => online.IsHost, 10f, "host handover");
            Assert.AreEqual(1, local.HostSeat);
            Assert.IsFalse(turns.RemoteAuthority, "The new host runs the rules");
            Assert.IsFalse(turns.State.IsGameOver, "Two players remain");
            Assert.IsTrue(turns.State.Players[0].Left);
            Assert.AreNotEqual(0, turns.State.CurrentPlayerIndex, "The leaver's turn moved on");
            yield return WaitFor(() => toC.Count(m => m.type == NetGameMessage.State) > before, 5f, "new host publishes the state");
            Assert.IsTrue(toC.Last(m => m.type == NetGameMessage.State).match.left[0]);
        }

        [UnityTest]
        public IEnumerator Doubles_TeamMateStandsInForALeaver()
        {
            MatchLaunch.RequestOnline(GameMode.EightBall, 0, new[] { "An", "Binh", "Cuong", "Dung" }, true);
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            yield return null;
            MatchState state = Object.FindAnyObjectByType<TurnManager>().State;
            state.Players[2].Left = true;
            Assert.AreEqual(0, state.ControllerOf(2), "Cuong's turns are played by An");
            Assert.IsFalse(state.IsOut(2));
            state.CurrentPlayerIndex = 1;
            Assert.AreEqual(2, state.OpponentIndex, "Rotation still alternates teams");
            Assert.AreEqual(2, state.SidesRemaining);
            state.Players[0].Left = true;
            Assert.IsTrue(state.IsOut(2));
            Assert.AreEqual(1, state.SidesRemaining);
            Assert.AreEqual(3, state.OpponentIndex, "Team A is gone: B1 → B2");
        }

        [UnityTest]
        public IEnumerator Spectator_WatchesTheMatchAndOrbitsTheCamera()
        {
            OnlineClient host = NewClient("Host");
            OnlineClient guest = NewClient("Guest");
            List<NetGameMessage> toHost = Record(host);
            yield return Pair(host, guest, 0);
            OnlineClient fan = OnlineClient.Instance;
            fan.Watch(url, host.RoomCode, "Fan");
            yield return WaitFor(() => fan.InMatch, 10f, "spectator start");
            Assert.IsTrue(fan.IsSpectator);
            Assert.AreEqual(-1, fan.Seat);
            Assert.AreEqual(1, host.Audience, "The players see the audience");
            MatchLaunch.RequestOnline(GameMode.EightBall, fan.Seat, fan.Names, fan.Teams);
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            yield return null;
            yield return null;
            MatchManager match = Object.FindAnyObjectByType<MatchManager>();
            var shot = Object.FindAnyObjectByType<ShotController>();
            var turns = Object.FindAnyObjectByType<TurnManager>();
            var cameras = Object.FindAnyObjectByType<CameraSystem.CameraController>();
            Assert.IsTrue(match.IsOnline && match.IsSpectator);
            Assert.IsTrue(match.State.Players.All(p => p.Kind == PlayerKind.Remote), "Nobody plays on this device");
            Assert.IsTrue(shot.NetworkLocked);
            Assert.AreEqual(CameraSystem.CameraMode.Orbit, cameras.ActiveMode, "Spectators start in the free orbit camera");
            Assert.IsTrue(cameras.OrbitWithPrimaryDrag);
            PoolHud hud = Object.FindAnyObjectByType<PoolHud>();
            Assert.IsTrue(hud.GetComponentsInChildren<Transform>(false).Any(t => t.name == "SpectatorBadge"));
            Assert.IsFalse(hud.GetComponentsInChildren<Transform>(false).Any(t => t.name == "Power"), "No cue controls");

            // The spectator asked for the table; the host answers with a spectator-only state.
            yield return WaitFor(() => toHost.Any(m => m.type == NetGameMessage.Sync), 5f, "sync request");
            NetMatchState state = NetMatchState.Capture(turns.State);
            state.current = 1;
            state.wins = new[] { 0, 2 };
            host.SendGame(new NetGameMessage { type = NetGameMessage.State, spectatorOnly = true, match = state,
                balls = NetGameMessage.CaptureTable(Object.FindAnyObjectByType<RackManager>().Balls) }.ToJson());
            yield return WaitFor(() => turns.State.CurrentPlayerIndex == 1, 10f, "state applied");
            Assert.AreEqual(2, OnlineMatchController.Active.RacksWon(1), "Room score shown to the audience");

            // Look around: the orbit turns the camera around the table.
            Vector3 before = Camera.main.transform.position;
            cameras.OrbitBy(new Vector2(90f, 0f), 0.2f);
            float end = Time.realtimeSinceStartup + 3f;
            while (Vector3.Distance(Camera.main.transform.position, before) < 0.5f && Time.realtimeSinceStartup < end) yield return null;
            Assert.Greater(Vector3.Distance(Camera.main.transform.position, before), 0.5f, "Camera moved around the table");
            Assert.Greater(Camera.main.transform.position.y, FindTable().SurfaceHeight, "Never under the table");
        }

        [UnityTest]
        public IEnumerator Host_AnswersSync_CountsRacksAndSavesHistory()
        {
            OnlineClient host = OnlineClient.Instance;
            OnlineClient guest = NewClient("Guest");
            List<NetGameMessage> toGuest = Record(guest);
            yield return Pair(host, guest, 0);
            MatchLaunch.RequestOnline(GameMode.EightBall, 0, "Host", "Guest");
            yield return SceneManager.LoadSceneAsync("02_Match", LoadSceneMode.Single);
            yield return null;
            var turns = Object.FindAnyObjectByType<TurnManager>();

            guest.SendGame(new NetGameMessage { type = NetGameMessage.Sync }.ToJson());
            yield return WaitFor(() => toGuest.Any(m => m.type == NetGameMessage.State && m.spectatorOnly), 5f, "spectator-only state");

            Assert.AreEqual(0, SaveSystem.History.matches.Count);
            turns.State.IsBreakShot = false;
            turns.Apply(new ShotOutcome { GameOver = true, WinnerIndex = 1, Message = "8 early" }, new ShotRecord { FirstObjectBallHit = 8 });
            yield return WaitFor(() => toGuest.Any(m => m.type == NetGameMessage.State && m.match.gameOver), 5f, "final state");
            NetGameMessage final = toGuest.Last(m => m.type == NetGameMessage.State);
            CollectionAssert.AreEqual(new[] { 0, 1 }, final.match.wins, "The guest won a rack");
            Assert.AreEqual(1, SaveSystem.History.matches.Count, "Saved once");
            OnlineMatchRecord record = SaveSystem.History.matches[0];
            Assert.AreEqual(host.RoomCode, record.room);
            Assert.AreEqual("Guest", record.winner);
            Assert.AreEqual(0, record.result, "The host lost this one");
            Assert.AreEqual("0 - 1", record.score);
            turns.NotifyStateChanged();
            Assert.AreEqual(1, SaveSystem.History.matches.Count, "Not saved twice");
            StringAssert.Contains(host.RoomCode, HistoryScreen.Describe());

            // Survives a reload from disk.
            SaveSystem.ClearCache();
            Assert.AreEqual(1, SaveSystem.History.matches.Count);
        }

        private static Table.TableBuilder FindTable() => Object.FindAnyObjectByType<Table.TableBuilder>();
    }
}
