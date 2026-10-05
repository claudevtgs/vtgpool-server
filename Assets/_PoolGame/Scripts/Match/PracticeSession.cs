using System;
using System.Collections.Generic;
using UnityEngine;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Rules;

namespace VTG.Pool.Match
{
    public enum PracticeRack
    {
        EightBall,
        NineBall,
        Empty
    }

    /// <summary>
    /// Free practice on top of the match flow (<see cref="Rules.Practice.PracticeRuleSet"/>): rack choice
    /// (8-ball, 9-ball, empty table), undo of the last shot, adding and removing balls, and session counters.
    /// Owns no physics; it only places balls at rest and re-syncs the match state.
    /// </summary>
    public sealed class PracticeSession : MonoBehaviour
    {
        [SerializeField] private MatchManager matchManager;
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private RackManager rackManager;
        [SerializeField] private ShotController shotController;

        private readonly List<(PoolBall ball, Vector3 position)> undoLayout = new List<(PoolBall, Vector3)>(16);
        private bool hasUndo;
        private bool emptyRequested;

        public bool IsActive => matchManager != null && matchManager.IsPractice;

        public PracticeRack Rack { get; private set; }

        public int Shots { get; private set; }

        public int BallsPocketed { get; private set; }

        public int Scratches { get; private set; }

        public RackManager RackManager => rackManager;

        public TurnManager TurnManager => turnManager;

        /// <summary>The last shot can be taken back (balls at rest, ready to aim).</summary>
        public bool CanUndo => IsActive && hasUndo && shotController != null && shotController.Phase == ShotPhase.Aiming && !shotController.LayoutEditing;

        /// <summary>Raised when counters, rack or layout change (UI refresh).</summary>
        public event Action Changed;

        public void Configure(MatchManager match, TurnManager turns, RackManager rack, ShotController shot)
        {
            matchManager = match;
            turnManager = turns;
            rackManager = rack;
            shotController = shot;
        }

        private void OnEnable()
        {
            PoolEvents.ShotStarted += HandleShotStarted;
            PoolEvents.BallsStopped += HandleBallsStopped;
            if (matchManager != null)
            {
                matchManager.MatchStarted += HandleMatchStarted;
            }
        }

        private void OnDisable()
        {
            PoolEvents.ShotStarted -= HandleShotStarted;
            PoolEvents.BallsStopped -= HandleBallsStopped;
            if (matchManager != null)
            {
                matchManager.MatchStarted -= HandleMatchStarted;
            }
        }

        /// <summary>Starts (or restarts) practice with the chosen rack.</summary>
        public void SetRack(PracticeRack rack)
        {
            emptyRequested = rack == PracticeRack.Empty;
            matchManager.StartPractice(rack == PracticeRack.NineBall ? GameMode.NineBall : GameMode.EightBall);
        }

        /// <summary>Puts every ball back where it was before the last shot.</summary>
        public bool Undo()
        {
            if (!CanUndo)
            {
                return false;
            }

            // Drop any held cue ball first so placement does not overwrite the restored layout.
            turnManager.Placement.Clear();
            for (int i = 0; i < undoLayout.Count; i++)
            {
                PoolBall ball = undoLayout[i].ball;
                if (ball != null)
                {
                    ball.gameObject.SetActive(true);
                    ball.PlaceAt(undoLayout[i].position);
                }
            }

            hasUndo = false;
            turnManager.SyncWithTable(BallInHandMode.None);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Puts a ball that is not on the table back (foot spot or the nearest free point on the long string).</summary>
        public bool AddBall(int number)
        {
            PoolBall ball = rackManager.GetBall(number);
            if (!IsActive || ball == null || ball.IsCueBall || IsOnTable(ball))
            {
                return false;
            }

            if (!rackManager.Respot(number))
            {
                ball.gameObject.SetActive(false);
                return false;
            }

            LayoutChanged();
            return true;
        }

        /// <summary>Takes an object ball off the table.</summary>
        public bool RemoveBall(PoolBall ball)
        {
            if (!IsActive || ball == null || ball.IsCueBall)
            {
                return false;
            }

            ball.gameObject.SetActive(false);
            LayoutChanged();
            return true;
        }

        /// <summary>
        /// Call after balls were moved by hand: the undo snapshot is dropped and the state re-synced. Ball in hand
        /// ends too, since the cue ball itself can be arranged.
        /// </summary>
        public void LayoutChanged()
        {
            hasUndo = false;
            turnManager.SyncWithTable(BallInHandMode.None);
            Changed?.Invoke();
        }

        public static bool IsOnTable(PoolBall ball) => ball != null && ball.gameObject.activeInHierarchy && !ball.IsPocketed;

        /// <summary>Object balls of the current rack set that are not on the table (candidates for <see cref="AddBall"/>).</summary>
        public List<int> BallsOffTable()
        {
            var result = new List<int>(15);
            for (int number = 1; number <= 15; number++)
            {
                PoolBall ball = rackManager.GetBall(number);
                if (ball != null && !IsOnTable(ball))
                {
                    result.Add(number);
                }
            }

            return result;
        }

        private void HandleMatchStarted()
        {
            Shots = 0;
            BallsPocketed = 0;
            Scratches = 0;
            hasUndo = false;
            if (!IsActive)
            {
                emptyRequested = false;
                Changed?.Invoke();
                return;
            }

            if (emptyRequested)
            {
                Rack = PracticeRack.Empty;
                for (int i = 0; i < rackManager.Balls.Count; i++)
                {
                    PoolBall ball = rackManager.Balls[i];
                    if (ball != null && !ball.IsCueBall)
                    {
                        ball.gameObject.SetActive(false);
                    }
                }

                turnManager.SyncWithTable(turnManager.State.BallInHand);
            }
            else
            {
                Rack = matchManager.Mode == GameMode.NineBall ? PracticeRack.NineBall : PracticeRack.EightBall;
            }

            Changed?.Invoke();
        }

        private void HandleShotStarted(ShotRecord record)
        {
            if (!IsActive)
            {
                return;
            }

            // The strike has been applied but nothing has moved yet: positions are the pre-shot layout.
            undoLayout.Clear();
            for (int i = 0; i < rackManager.Balls.Count; i++)
            {
                PoolBall ball = rackManager.Balls[i];
                if (IsOnTable(ball))
                {
                    undoLayout.Add((ball, ball.IsCueBall ? record.CueBallPosition : ball.Position));
                }
            }

            hasUndo = false;
        }

        private void HandleBallsStopped(ShotRecord record)
        {
            if (!IsActive)
            {
                return;
            }

            Shots++;
            BallsPocketed += record.BallsPocketed.Count;
            if (record.CueBallPocketed || record.CueBallOffTable)
            {
                Scratches++;
            }

            hasUndo = undoLayout.Count > 0;
            Changed?.Invoke();
        }
    }
}
