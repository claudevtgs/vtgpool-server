using System;
using System.Collections.Generic;
using UnityEngine;
using VTG.Pool.Aiming;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Match;

namespace VTG.Pool.Network
{
    /// <summary>
    /// Online match glue (shot-based, host-authoritative; MASTER_PROMPT section 38).
    /// - The player whose turn it is streams aim (yaw, tip, cue angle, charging power) and cue-ball placement.
    /// - A shot is sent as its <see cref="ShotParameters"/> plus the cue-ball position; both devices simulate it.
    /// - The host evaluates the rules and, after every rack and shot, sends the table snapshot and match state;
    ///   the guest never evaluates (<see cref="TurnManager.RemoteAuthority"/>) and snaps to the host's result.
    /// Input is locked whenever the other device owns the table.
    /// Players leaving: the match goes on while two or more remain. A leaver's turns are skipped, or played by a
    /// remaining team mate (doubles); when the host leaves the lowest remaining seat takes over the rules (a shot in
    /// flight is evaluated by the new host when it settles); when only one side is left it wins by forfeit.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public sealed class OnlineMatchController : MonoBehaviour
    {
        private const float StreamInterval = 0.1f;
        private const float StateWaitLimit = 6f;

        private MatchManager match;
        private ShotController shot;
        private TurnManager turns;
        private CueBallPlacementController placement;
        private AimSystem aim;
        private CueBallSpinController spin;
        private RackManager rack;
        private OnlineClient client;

        private float streamTimer;
        private NetGameMessage lastAim;
        private Vector3 lastPlace = new Vector3(float.NaN, 0f, 0f);
        private bool applyingRemoteShot;
        private bool awaitingAuthority;
        private NetGameMessage pendingState;
        private float pendingStateAge;
        private readonly HashSet<int> leftSeats = new HashSet<int>();
        private float aloneAt = -1f;
        private int[] racksWon = new int[0];
        private bool rackResultDone;

        public static OnlineMatchController Active { get; private set; }

        public bool IsHost { get; private set; }

        public int LocalIndex => match != null ? match.OnlineLocalIndex : -1;

        /// <summary>This device only watches.</summary>
        public bool IsSpectator => match != null && match.IsSpectator;

        /// <summary>Racks won in this room so far by the player (team mates share their side's wins).</summary>
        public int RacksWon(int playerIndex) => playerIndex >= 0 && playerIndex < racksWon.Length ? racksWon[playerIndex] : 0;

        /// <summary>The room score changed (a rack ended).</summary>
        public event Action ScoreChanged;

        /// <summary>Waiting for the host's result of the last shot (guest only).</summary>
        public bool AwaitingAuthority => awaitingAuthority;

        /// <summary>The match cannot go on: everyone else left or the connection dropped (argument: Loc key).</summary>
        public event Action<string> ConnectionLost;

        /// <summary>A player left; the match goes on (argument: their name).</summary>
        public event Action<string> PlayerLeft;

        /// <summary>Allows tests and tools to plug in another client than <see cref="OnlineClient.Instance"/>.</summary>
        public void Configure(MatchManager matchManager, ShotController shotController, OnlineClient onlineClient = null)
        {
            Unsubscribe();
            match = matchManager;
            shot = shotController;
            turns = match.TurnManager;
            placement = turns.Placement;
            rack = FindAnyObjectByType<RackManager>();
            aim = FindAnyObjectByType<AimSystem>();
            spin = FindAnyObjectByType<CueBallSpinController>();
            client = onlineClient != null ? onlineClient : OnlineClient.Instance;
            IsHost = match.OnlineLocalIndex == 0;
            turns.RemoteAuthority = !IsHost;
            Active = this;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }

            if (IsSpectator)
            {
                // Watching: ask the host (through the server) for the table as it is now.
                client.RequestSync();
            }
            else if (!IsHost)
            {
                // The host may have racked (or even broken) before this scene finished loading.
                Send(new NetGameMessage { type = NetGameMessage.Ready });
            }
        }

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private bool subscribed;

        private void Subscribe()
        {
            if (subscribed || client == null || turns == null)
            {
                return;
            }

            subscribed = true;
            client.GameMessage += HandleMessage;
            client.PeerLeft += HandlePeerLeft;
            client.PlayerLeft += HandlePlayerLeft;
            client.Disconnected += HandleDisconnected;
            turns.OutcomeApplied += HandleOutcomeApplied;
            PoolEvents.ShotStarted += HandleShotStarted;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            subscribed = false;
            if (client != null)
            {
                client.GameMessage -= HandleMessage;
                client.PeerLeft -= HandlePeerLeft;
                client.PlayerLeft -= HandlePlayerLeft;
                client.Disconnected -= HandleDisconnected;
            }

            if (turns != null)
            {
                turns.OutcomeApplied -= HandleOutcomeApplied;
            }

            PoolEvents.ShotStarted -= HandleShotStarted;
        }

        private void OnDestroy()
        {
            if (Active == this)
            {
                Active = null;
            }

            if (turns != null)
            {
                turns.RemoteAuthority = false;
            }

            if (shot != null)
            {
                shot.NetworkLocked = false;
                shot.ShowRemoteCue = false;
            }
        }

        /// <summary>True when this device may act (its turn, not waiting for the host).</summary>
        public bool LocalTurn
        {
            get
            {
                MatchState state = turns != null ? turns.State : null;
                // A team mate who left is stood in for by the remaining partner.
                return state != null && !IsSpectator && !state.IsGameOver && state.ControllerOf(state.CurrentPlayerIndex) == LocalIndex && !awaitingAuthority && pendingState == null;
            }
        }

        private void Update()
        {
            if (match == null || turns.State == null)
            {
                return;
            }

            if (aloneAt > 0f && Time.unscaledTime >= aloneAt)
            {
                // Everyone else left: let the forfeit victory play, then offer the way out.
                aloneAt = -1f;
                ConnectionLost?.Invoke("online.alone");
            }

            bool local = LocalTurn;
            shot.NetworkLocked = !local;
            shot.ShowRemoteCue = !local && !awaitingAuthority && !turns.State.IsGameOver;

            if (pendingState != null)
            {
                pendingStateAge += Time.unscaledDeltaTime;
                bool settled = shot.Phase == ShotPhase.Aiming || shot.Phase == ShotPhase.GameOver || shot.Phase == ShotPhase.Preparing;
                if (settled || pendingStateAge > StateWaitLimit)
                {
                    NetGameMessage state = pendingState;
                    pendingState = null;
                    ApplyState(state);
                }
            }

            if (local)
            {
                Stream();
            }
        }

        // ------------------------------------------------------------ sending

        private void Stream()
        {
            streamTimer += Time.unscaledDeltaTime;
            if (streamTimer < StreamInterval)
            {
                return;
            }

            streamTimer = 0f;
            if (placement != null && placement.IsPlacing)
            {
                Vector3 position = shot.CueBall.Position;
                if ((position - lastPlace).sqrMagnitude > 1e-8f || float.IsNaN(lastPlace.x))
                {
                    lastPlace = position;
                    Send(new NetGameMessage { type = NetGameMessage.Place, px = position.x, py = position.y, pz = position.z });
                }

                return;
            }

            if (shot.Phase != ShotPhase.Aiming && shot.Phase != ShotPhase.PowerSelection)
            {
                return;
            }

            var message = new NetGameMessage
            {
                type = NetGameMessage.Aim,
                yaw = aim != null ? aim.AimYawDegrees : 0f,
                tipX = spin != null ? spin.TipOffset.x : 0f,
                tipY = spin != null ? spin.TipOffset.y : 0f,
                elevation = spin != null ? spin.Elevation : 0f,
                power = shot.Phase == ShotPhase.PowerSelection ? shot.CurrentPower : 0f
            };

            if (lastAim == null || Mathf.Abs(Mathf.DeltaAngle(lastAim.yaw, message.yaw)) > 0.01f || Mathf.Abs(lastAim.tipX - message.tipX) > 0.005f
                || Mathf.Abs(lastAim.tipY - message.tipY) > 0.005f || Mathf.Abs(lastAim.elevation - message.elevation) > 0.2f || Mathf.Abs(lastAim.power - message.power) > 0.01f)
            {
                lastAim = message;
                Send(message);
            }
        }

        private void HandleShotStarted(ShotRecord record)
        {
            if (applyingRemoteShot || match == null || IsSpectator)
            {
                return;
            }

            // A local shot: send it; the guest then waits for the host's verdict.
            Send(new NetGameMessage
            {
                type = NetGameMessage.Shot,
                yaw = Mathf.Atan2(record.AimDirection.x, record.AimDirection.z) * Mathf.Rad2Deg,
                tipX = record.CueOffset.x,
                tipY = record.CueOffset.y,
                elevation = record.CueElevation,
                power = record.Power,
                px = record.CueBallPosition.x,
                py = record.CueBallPosition.y,
                pz = record.CueBallPosition.z,
                shotIndex = record.ShotIndex
            });

            if (!IsHost)
            {
                awaitingAuthority = true;
            }
        }

        private void HandleOutcomeApplied()
        {
            if (!IsHost || turns.State == null)
            {
                return;
            }

            // A new host that took over mid-shot has now evaluated it itself.
            awaitingAuthority = false;
            if (ReapplyLeft())
            {
                return; // the turn moved on; SkipTurn raised OutcomeApplied again
            }

            CountRack();
            Send(StateMessage(false));
        }

        private NetGameMessage StateMessage(bool spectatorOnly)
        {
            NetMatchState captured = NetMatchState.Capture(turns.State);
            captured.wins = (int[])racksWon.Clone();
            return new NetGameMessage
            {
                type = NetGameMessage.State,
                newRack = turns.State.LastOutcome == null,
                spectatorOnly = spectatorOnly,
                match = captured,
                balls = NetGameMessage.CaptureTable(rack.Balls)
            };
        }

        /// <summary>Host: a rack just ended — add it to the room score (once) and save it to the history.</summary>
        private void CountRack()
        {
            MatchState state = turns.State;
            if (!state.IsGameOver)
            {
                rackResultDone = false;
                return;
            }

            if (rackResultDone || state.WinnerIndex < 0)
            {
                return;
            }

            rackResultDone = true;
            EnsureScoreSize(state.Players.Count);
            int winnerTeam = state.TeamOf(state.WinnerIndex);
            for (int i = 0; i < state.Players.Count; i++)
            {
                if (state.TeamOf(i) == winnerTeam) racksWon[i]++;
            }

            SaveResult(state);
        }

        /// <summary>Guests and spectators: take the host's score; a new finished rack goes into the history.</summary>
        private void TakeScore(NetMatchState net)
        {
            MatchState state = turns.State;
            if (net != null && net.wins != null && net.wins.Length > 0)
            {
                racksWon = (int[])net.wins.Clone();
                ScoreChanged?.Invoke();
            }

            if (!state.IsGameOver)
            {
                rackResultDone = false;
                return;
            }

            if (!rackResultDone && state.WinnerIndex >= 0)
            {
                rackResultDone = true;
                SaveResult(state);
            }
        }

        private void EnsureScoreSize(int count)
        {
            if (racksWon.Length < count)
            {
                Array.Resize(ref racksWon, count);
            }
        }

        /// <summary>"2 - 1" (two sides) or "An 2 · Binh 1 · Cuong 0" (free-for-all).</summary>
        public string ScoreText()
        {
            MatchState state = turns != null ? turns.State : null;
            if (state == null)
            {
                return string.Empty;
            }

            var sides = new List<int>();
            var parts = new List<string>();
            for (int i = 0; i < state.Players.Count; i++)
            {
                int side = state.TeamOf(i);
                if (sides.Contains(side)) continue;
                sides.Add(side);
                parts.Add(state.HasTeams || state.Players.Count == 2 ? RacksWon(i).ToString() : state.Players[i].DisplayName + " " + RacksWon(i));
            }

            return string.Join(state.HasTeams || state.Players.Count == 2 ? " - " : " · ", parts);
        }

        private void SaveResult(MatchState state)
        {
            ScoreChanged?.Invoke();
            var names = new string[state.Players.Count];
            for (int i = 0; i < names.Length; i++) names[i] = state.Players[i].DisplayName;
            bool forfeit = leftSeats.Count > 0 && state.SidesRemaining <= 1;
            Save.SaveSystem.AddOnlineResult(new Save.OnlineMatchRecord
            {
                time = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                room = client != null ? client.RoomCode ?? string.Empty : string.Empty,
                mode = (int)state.Mode,
                teams = state.HasTeams,
                players = names,
                winner = state.SideName(state.WinnerIndex),
                score = ScoreText(),
                result = IsSpectator ? -1 : state.TeamOf(state.WinnerIndex) == state.TeamOf(LocalIndex) ? 1 : 0,
                forfeit = forfeit
            });
        }

        /// <summary>Guest: asks the host for a new rack after a game (the host restarts and broadcasts it).</summary>
        public void RequestRematch()
        {
            if (IsSpectator)
            {
                return;
            }

            if (IsHost)
            {
                match.StartMatch();
            }
            else
            {
                Send(new NetGameMessage { type = NetGameMessage.Rematch });
            }
        }

        private void Send(NetGameMessage message)
        {
            if (client != null && !IsSpectator)
            {
                client.SendGame(message.ToJson());
            }
        }

        // ------------------------------------------------------------ receiving

        private void HandleMessage(string payload)
        {
            NetGameMessage message = NetGameMessage.FromJson(payload);
            if (message == null || turns.State == null)
            {
                return;
            }

            switch (message.type)
            {
                case NetGameMessage.Aim:
                    if (!LocalTurn)
                    {
                        ApplyAim(message);
                    }

                    break;
                case NetGameMessage.Place:
                    if (!LocalTurn && placement != null)
                    {
                        if (!placement.IsPlacing)
                        {
                            placement.BeginPlacement();
                        }

                        placement.TryMoveTo(new Vector3(message.px, message.py, message.pz));
                    }

                    break;
                case NetGameMessage.Shot:
                    PlayRemoteShot(message);
                    break;
                case NetGameMessage.State:
                    // Answers to a spectator's sync are for spectators only.
                    if (!IsHost && (!message.spectatorOnly || IsSpectator))
                    {
                        pendingState = message;
                        pendingStateAge = 0f;
                    }

                    break;
                case NetGameMessage.Ready:
                    if (IsHost)
                    {
                        HandleOutcomeApplied();
                    }

                    break;
                case NetGameMessage.Sync:
                    if (IsHost)
                    {
                        Send(StateMessage(true));
                    }

                    break;
                case NetGameMessage.Rematch:
                    if (IsHost && turns.State.IsGameOver)
                    {
                        match.StartMatch();
                    }

                    break;
            }
        }

        private void ApplyAim(NetGameMessage message)
        {
            if (aim != null)
            {
                aim.SetAimDirection(Quaternion.Euler(0f, message.yaw, 0f) * Vector3.forward);
            }

            if (spin != null)
            {
                spin.SetTipOffset(new Vector2(message.tipX, message.tipY));
                spin.SetElevation(message.elevation);
            }
        }

        private void PlayRemoteShot(NetGameMessage message)
        {
            if (shot.CueBall == null)
            {
                return;
            }

            // A replay of the previous shot must not hide this one.
            if (Replay.ShotReplay.Instance != null)
            {
                Replay.ShotReplay.Instance.Stop();
            }

            // Whatever this device shows, the shot starts from the shooter's exact cue-ball position.
            placement.Clear();
            if (shot.Phase != ShotPhase.Aiming && shot.Phase != ShotPhase.PowerSelection)
            {
                shot.ResetToAiming();
            }

            shot.CueBall.gameObject.SetActive(true);
            shot.CueBall.PlaceAt(new Vector3(message.px, message.py, message.pz));
            var parameters = new ShotParameters(Quaternion.Euler(0f, message.yaw, 0f) * Vector3.forward, message.power,
                new Vector2(message.tipX, message.tipY), message.elevation);
            applyingRemoteShot = true;
            try
            {
                if (!shot.ExecuteShot(parameters))
                {
                    Debug.LogWarning("[Online] Remote shot could not be played locally; waiting for the host state.");
                }
            }
            finally
            {
                applyingRemoteShot = false;
            }

            if (!IsHost)
            {
                awaitingAuthority = true;
            }
        }

        /// <summary>Guest: snaps the table and match state to the host's result.</summary>
        private void ApplyState(NetGameMessage message)
        {
            if (shot.Phase == ShotPhase.BallsMoving || shot.Phase == ShotPhase.Shooting)
            {
                shot.ResetToAiming();
            }

            placement.Clear();
            ApplyTable(message.balls);
            NetMatchState state = message.match;
            turns.ApplyAuthoritativeState(state.CopyTo, message.newRack ? null : state.ToOutcome(), message.newRack);
            TakeScore(state);
            awaitingAuthority = false;
            lastAim = null;
            lastPlace = new Vector3(float.NaN, 0f, 0f);
        }

        private void ApplyTable(BallSnapshot[] snapshot)
        {
            if (snapshot == null || rack == null)
            {
                return;
            }

            for (int i = 0; i < snapshot.Length; i++)
            {
                BallSnapshot entry = snapshot[i];
                PoolBall ball = rack.GetBall(entry.id);
                if (ball == null)
                {
                    continue;
                }

                if (entry.onTable)
                {
                    ball.gameObject.SetActive(true);
                    ball.PlaceAt(new Vector3(entry.x, entry.y, entry.z));
                }
                else if (!ball.IsCueBall)
                {
                    ball.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>Marks leavers again after a new rack (new player objects) and moves the turn off a leaver.</summary>
        private bool ReapplyLeft()
        {
            MatchState state = turns.State;
            if (leftSeats.Count == 0 || state == null)
            {
                return false;
            }

            foreach (int seat in leftSeats)
            {
                if (seat >= 0 && seat < state.Players.Count) MarkLeft(state, seat);
            }

            if (!state.IsGameOver && state.SidesRemaining <= 1)
            {
                Forfeit(state);
                return true;
            }

            bool settled = shot.Phase != ShotPhase.BallsMoving && shot.Phase != ShotPhase.Shooting;
            if (!state.IsGameOver && settled && state.IsOut(state.CurrentPlayerIndex))
            {
                turns.SkipTurn();
                return true;
            }

            return false;
        }

        private void MarkLeft(MatchState state, int seat)
        {
            state.Players[seat].Left = true;
            int stand = state.ControllerOf(seat);
            if (stand >= 0)
            {
                state.Players[seat].Kind = stand == LocalIndex ? PlayerKind.LocalHuman : PlayerKind.Remote;
            }
        }

        private void Forfeit(MatchState state)
        {
            int winner = -1;
            for (int i = 0; i < state.Players.Count && winner < 0; i++)
            {
                if (!state.Players[i].Left) winner = i;
            }

            if (winner >= 0)
            {
                turns.Forfeit(winner, Localization.Loc.T("online.forfeit", state.SideName(winner)));
            }
        }

        private void HandlePlayerLeft(int seat, string playerName)
        {
            MatchState state = turns != null ? turns.State : null;
            if (state == null || seat < 0 || seat >= state.Players.Count || seat == LocalIndex)
            {
                return;
            }

            leftSeats.Add(seat);
            MarkLeft(state, seat);
            bool wasHost = IsHost;
            IsHost = client.HostSeat == LocalIndex;
            turns.RemoteAuthority = !IsHost;
            if (IsHost && !wasHost)
            {
                Debug.Log("[Online] The host left; this device now runs the rules.");
                // Waiting for a verdict that will never come: the shot has already settled here, publish this table.
                bool settled = shot.Phase != ShotPhase.BallsMoving && shot.Phase != ShotPhase.Shooting;
                if (awaitingAuthority && settled)
                {
                    awaitingAuthority = false;
                }

                pendingState = null;
            }

            PlayerLeft?.Invoke(string.IsNullOrEmpty(playerName) ? state.Players[seat].DisplayName : playerName);
            if (state.SidesRemaining <= 1 && !state.IsGameOver)
            {
                // Only this side is left (possibly only this device): it wins. With no one to tell, no state goes out.
                if (IsHost) Forfeit(state);
            }
            else if (IsHost)
            {
                if (!ReapplyLeft())
                {
                    HandleOutcomeApplied(); // share the leave and the new host's view
                }
            }

            if (!IsSpectator && (!client.InMatch || state.SidesRemaining <= 1))
            {
                bool aloneHere = true;
                for (int i = 0; i < state.Players.Count; i++)
                {
                    if (i != LocalIndex && !state.Players[i].Left && state.TeamOf(i) != state.TeamOf(LocalIndex)) aloneHere = false;
                }

                if (aloneHere) aloneAt = Time.unscaledTime + 4.5f;
            }

            StateChangedForHud();
        }

        private void StateChangedForHud()
        {
            // Names on the HUD show who left.
            turns.NotifyStateChanged();
        }

        /// <summary>The room is gone. After a forfeit win that is expected; otherwise the match is over.</summary>
        private void HandlePeerLeft()
        {
            MatchState state = turns != null ? turns.State : null;
            if (state != null && state.IsGameOver && aloneAt > 0f)
            {
                return; // the victory plays first, then "online.alone"
            }

            if (aloneAt < 0f)
            {
                ConnectionLost?.Invoke(IsSpectator ? "online.roomclosed" : "online.peerleft");
            }
        }

        private void HandleDisconnected() => ConnectionLost?.Invoke("online.disconnected");
    }
}
