using System;
using UnityEngine;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Rules;

namespace VTG.Pool.Match
{
    /// <summary>
    /// Applies rule outcomes to the match: removes/re-spots balls, assigns groups, switches turns,
    /// grants ball in hand, handles re-racks and game over. Rules decide; this class only executes.
    /// </summary>
    public sealed class TurnManager : MonoBehaviour
    {
        [SerializeField] private ShotController shotController;
        [SerializeField] private CueBallPlacementController placement;
        [SerializeField] private RackManager rackManager;

        public MatchState State { get; private set; }
        public CueBallPlacementController Placement => placement;

        public IRuleSet Rules { get; private set; }

        /// <summary>Raised after any change the HUD should reflect.</summary>
        public event Action StateChanged;

        /// <summary>Raised after a rack starts or a shot outcome was applied (online host sends its state then).</summary>
        public event Action OutcomeApplied;

        /// <summary>Online guest: shots are not evaluated locally; the host's results arrive via <see cref="ApplyAuthoritativeState"/>.</summary>
        public bool RemoteAuthority { get; set; }

        public void Configure(ShotController shot, CueBallPlacementController ballInHand, RackManager rack)
        {
            shotController = shot;
            placement = ballInHand;
            rackManager = rack;
        }

        private void OnEnable()
        {
            PoolEvents.ShotStarted += HandleShotStarted;
            PoolEvents.BallsStopped += HandleShotFinished;
        }

        private void OnDisable()
        {
            PoolEvents.ShotStarted -= HandleShotStarted;
            PoolEvents.BallsStopped -= HandleShotFinished;
        }

        /// <summary>Racks the balls and starts a rack with the given breaker (ball in hand behind the head string).</summary>
        public void StartRack(MatchState state, IRuleSet rules, int breakerIndex)
        {
            State = state;
            Rules = rules;
            rackManager.Rack(rules.Mode);
            rules.BeginRack(state);
            state.IsGameOver = false;
            state.WinnerIndex = -1;
            state.Result = string.Empty;
            state.RackNumber++;
            state.CurrentPlayerIndex = breakerIndex;
            state.BreakingPlayerIndex = breakerIndex;
            state.LastOutcome = null;

            shotController.ResetToAiming();
            GrantBallInHand(rules.OpeningBallInHand);
            PoolEvents.RaiseTurnChanged(breakerIndex);
            StateChanged?.Invoke();
            OutcomeApplied?.Invoke();
        }

        private void HandleShotStarted(ShotRecord record)
        {
            if (State == null)
            {
                return;
            }

            record.IsBreakShot = State.IsBreakShot;
            record.PlayerId = State.CurrentPlayerIndex;
            State.BallInHand = BallInHandMode.None;
            StateChanged?.Invoke();
        }

        private void HandleShotFinished(ShotRecord record)
        {
            if (State == null || State.IsGameOver || Rules == null || RemoteAuthority)
            {
                return;
            }

            ShotOutcome outcome = Rules.Evaluate(State, record);
            record.Foul = outcome.Foul;
            record.Result = outcome.Message;
            Apply(outcome, record);
        }

        /// <summary>Applies an outcome (public for tests and future replay/network authority).</summary>
        public void Apply(ShotOutcome outcome, ShotRecord record)
        {
            MatchState state = State;
            MatchPlayer shooter = state.CurrentPlayer;
            int shooterIndex = state.CurrentPlayerIndex;
            state.ShotsPlayed++;
            state.LastOutcome = outcome;

            for (int i = 0; i < record.BallsPocketed.Count; i++)
            {
                state.RemoveBall(record.BallsPocketed[i]);
                if (record.BallsPocketed[i] != 8)
                {
                    shooter.BallsPocketed++;
                }
            }

            for (int i = 0; i < record.BallsOffTable.Count; i++)
            {
                state.RemoveBall(record.BallsOffTable[i]);
            }

            if (outcome.Foul)
            {
                shooter.Fouls++;
                shooter.ConsecutiveFouls++;
                PoolEvents.RaiseFoulCommitted(shooterIndex, outcome.FoulType, outcome.Message);
            }
            else
            {
                shooter.ConsecutiveFouls = 0;
            }

            if (outcome.GameOver)
            {
                state.IsGameOver = true;
                state.WinnerIndex = outcome.WinnerIndex;
                state.Result = outcome.Message;
                state.BallInHand = BallInHandMode.None;
                placement.Clear();
                shotController.EnterGameOver();
                PoolEvents.RaiseGameWon(outcome.WinnerIndex, outcome.Message);
                StateChanged?.Invoke();
                OutcomeApplied?.Invoke();
                return;
            }

            if (outcome.Rerack)
            {
                StartRack(state, Rules, state.OpponentIndex);
                return;
            }

            for (int i = 0; i < outcome.RespotBalls.Count; i++)
            {
                if (rackManager.Respot(outcome.RespotBalls[i]))
                {
                    state.AddBall(outcome.RespotBalls[i]);
                }
            }

            if (outcome.AssignGroupToShooter != BallGroup.None)
            {
                // The shooter's side takes the group (team mates too); everyone else gets the other one.
                int shooterTeam = state.TeamOf(shooterIndex);
                for (int i = 0; i < state.Players.Count; i++)
                {
                    state.Players[i].Group = state.TeamOf(i) == shooterTeam ? outcome.AssignGroupToShooter : BallGroups.Opposite(outcome.AssignGroupToShooter);
                }

                state.TableOpen = false;
            }

            state.IsBreakShot = false;
            if (!outcome.TurnContinues)
            {
                state.CurrentPlayerIndex = state.OpponentIndex;
                PoolEvents.RaiseTurnChanged(state.CurrentPlayerIndex);
            }

            GrantBallInHand(outcome.BallInHand);
            StateChanged?.Invoke();
            OutcomeApplied?.Invoke();
        }

        /// <summary>Raises <see cref="StateChanged"/> after an outside change (online: a player left).</summary>
        public void NotifyStateChanged() => StateChanged?.Invoke();

        /// <summary>Online: everyone else left — the remaining side wins without a shot.</summary>
        public void Forfeit(int winnerIndex, string message)
        {
            MatchState state = State;
            if (state == null || state.IsGameOver)
            {
                return;
            }

            state.IsGameOver = true;
            state.WinnerIndex = winnerIndex;
            state.Result = message;
            state.LastOutcome = new ShotOutcome { GameOver = true, WinnerIndex = winnerIndex, Message = message };
            state.BallInHand = BallInHandMode.None;
            placement.Clear();
            shotController.EnterGameOver();
            PoolEvents.RaiseGameWon(winnerIndex, message);
            StateChanged?.Invoke();
            OutcomeApplied?.Invoke();
        }

        /// <summary>Online: the shooter left between shots — the turn moves on (ball-in-hand rights carry over).</summary>
        public void SkipTurn()
        {
            MatchState state = State;
            if (state == null || state.IsGameOver)
            {
                return;
            }

            state.CurrentPlayerIndex = state.OpponentIndex;
            PoolEvents.RaiseTurnChanged(state.CurrentPlayerIndex);
            GrantBallInHand(state.BallInHand);
            StateChanged?.Invoke();
            OutcomeApplied?.Invoke();
        }

        /// <summary>
        /// Online guest: takes over the host's match state after a shot or rack (the table layout is placed by the
        /// caller first). Raises the same events as a local outcome so HUD, audio and statistics react normally.
        /// </summary>
        public void ApplyAuthoritativeState(Action<MatchState> copyState, ShotOutcome outcome, bool newRack)
        {
            MatchState state = State;
            int previousPlayer = state.CurrentPlayerIndex;
            copyState(state);
            if (outcome != null)
            {
                state.LastOutcome = outcome;
                if (outcome.Foul)
                {
                    PoolEvents.RaiseFoulCommitted(previousPlayer, outcome.FoulType, outcome.Message);
                }
            }
            else if (newRack)
            {
                state.LastOutcome = null;
            }

            if (state.IsGameOver)
            {
                placement.Clear();
                shotController.EnterGameOver();
                PoolEvents.RaiseGameWon(state.WinnerIndex, state.Result);
                StateChanged?.Invoke();
                return;
            }

            if (newRack || shotController.Phase == Core.ShotPhase.GameOver)
            {
                shotController.ResetToAiming();
            }

            if (state.CurrentPlayerIndex != previousPlayer || newRack)
            {
                PoolEvents.RaiseTurnChanged(state.CurrentPlayerIndex);
            }

            GrantBallInHand(state.BallInHand);
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Re-reads which balls are on the table after an external change of the layout (practice editing,
        /// undo) and grants the given ball-in-hand rights. Turn and score are unchanged.
        /// </summary>
        public void SyncWithTable(BallInHandMode ballInHand)
        {
            if (State == null)
            {
                return;
            }

            var onTable = new System.Collections.Generic.List<int>(16);
            var balls = rackManager.Balls;
            for (int i = 0; i < balls.Count; i++)
            {
                if (balls[i] != null && !balls[i].IsCueBall && balls[i].gameObject.activeInHierarchy && !balls[i].IsPocketed)
                {
                    onTable.Add(balls[i].BallId);
                }
            }

            State.SetBallsOnTable(onTable);
            GrantBallInHand(ballInHand);
            StateChanged?.Invoke();
        }

        private void GrantBallInHand(BallInHandMode mode)
        {
            State.BallInHand = mode;
            if (mode == BallInHandMode.None)
            {
                placement.Clear();
            }
            else
            {
                placement.Grant(mode);
            }
        }
    }
}
