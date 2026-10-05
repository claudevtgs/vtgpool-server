using VTG.Pool.Localization;
using System;
using System.Collections.Generic;
using UnityEngine;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Rules;
using VTG.Pool.Table;

namespace VTG.Pool.Match
{
    /// <summary>What made a shot notable (presentation only: replays, troll and celebration effects).</summary>
    public sealed class ShotHighlight
    {
        /// <summary>Object balls pocketed by this shot.</summary>
        public readonly List<int> Pocketed = new List<int>(8);

        public int Shooter;
        public bool Foul;
        public bool WasBreak;
        public bool GameOver;
        public int Winner = -1;
        public bool Combo;
        public bool Bank;
        public bool Kick;

        /// <summary>"Bida rùa": the ball that dropped was not the one aimed at, after rails or kisses.</summary>
        public bool Fluke;

        /// <summary>The ball that decides the rack (8 in 8-ball, 9 in 9-ball) went down on this shot, or -1.</summary>
        public int MoneyBall = -1;

        /// <summary>The foul committed, if any.</summary>
        public FoulType FoulType;

        /// <summary>Nothing went down, no foul, and the turn passed (not the break).</summary>
        public bool Missed;

        /// <summary>The shooter won the rack with this shot.</summary>
        public bool WinningShot => GameOver && Winner == Shooter;

        /// <summary>The shooter lost the rack with this shot (8 early / scratch on the 8, 9 on a foul, ...).</summary>
        public bool LosingShot => GameOver && Winner >= 0 && Winner != Shooter;

        /// <summary>A miss that left a ball it touched hanging on a pocket lip.</summary>
        public bool NearMiss;
    }

    /// <summary>
    /// Recognises notable shots for presentation (HUD toast, sound): combo, bank, kick, multi-ball pots,
    /// pocketing streaks, golden break and break-and-run. Reads facts from PoolEvents and the rule outcome;
    /// never affects rules. Allocation-free during play (fixed-size per-ball arrays).
    /// </summary>
    public sealed class ShotCallouts : MonoBehaviour
    {
        private const int MaxBallNumber = 16;

        [SerializeField] private TurnManager turnManager;
        [SerializeField, Tooltip("Pocketing shots in a row by the same player that trigger a streak callout.")]
        private int streakThreshold = 3;

        private readonly bool[] hitByCue = new bool[MaxBallNumber];
        private readonly bool[] touchedRail = new bool[MaxBallNumber];
        private readonly List<int> pocketed = new List<int>(8);
        private readonly List<string> pending = new List<string>(4);
        private bool cueRailBeforeContact;
        private bool cueContactMade;
        private bool shotActive;
        private ShotOutcome lastOutcome;
        private int streakPlayer = -1;
        private int streakCount;
        private int rackShots;
        private int rackBreaker = -1;
        private bool opponentShotThisRack;

        /// <summary>Raised once per callout with a short uppercase label (e.g. "BANK SHOT").</summary>
        public event Action<string> CalloutRaised;

        /// <summary>Raised after every evaluated shot (pots, misses, fouls, game over).</summary>
        public event Action<ShotHighlight> HighlightDetected;

        private int firstObjectHit = -1;
        private int cueRailsBeforeContact;
        private int shooter;

        public void Configure(TurnManager turns) => turnManager = turns;

        private void OnEnable()
        {
            PoolEvents.ShotStarted += HandleShotStarted;
            PoolEvents.BallHit += HandleBallHit;
            PoolEvents.CushionHit += HandleCushionHit;
            PoolEvents.BallPocketed += HandlePocketed;
            if (turnManager != null)
            {
                turnManager.StateChanged += HandleStateChanged;
            }
        }

        private void OnDisable()
        {
            PoolEvents.ShotStarted -= HandleShotStarted;
            PoolEvents.BallHit -= HandleBallHit;
            PoolEvents.CushionHit -= HandleCushionHit;
            PoolEvents.BallPocketed -= HandlePocketed;
            if (turnManager != null)
            {
                turnManager.StateChanged -= HandleStateChanged;
            }
        }

        private void HandleShotStarted(ShotRecord record)
        {
            Array.Clear(hitByCue, 0, hitByCue.Length);
            Array.Clear(touchedRail, 0, touchedRail.Length);
            pocketed.Clear();
            cueRailBeforeContact = false;
            cueContactMade = false;
            shotActive = true;
            firstObjectHit = -1;
            cueRailsBeforeContact = 0;

            MatchState state = turnManager != null ? turnManager.State : null;
            if (state == null)
            {
                return;
            }

            shooter = state.CurrentPlayerIndex;
            if (record.IsBreakShot)
            {
                rackShots = 0;
                rackBreaker = state.CurrentPlayerIndex;
                opponentShotThisRack = false;
            }

            rackShots++;
            if (state.CurrentPlayerIndex != rackBreaker)
            {
                opponentShotThisRack = true;
            }
        }

        private void HandleBallHit(BallHitInfo info)
        {
            if (!shotActive)
            {
                return;
            }

            PoolBall cue = info.A.IsCueBall ? info.A : (info.B.IsCueBall ? info.B : null);
            if (cue != null)
            {
                if (!cueContactMade)
                {
                    firstObjectHit = info.Other(cue).BallId;
                }

                cueContactMade = true;
                MarkBall(hitByCue, info.Other(cue).BallId);
            }
        }

        private void HandleCushionHit(CushionHitInfo info)
        {
            if (!shotActive || info.Surface == null || info.Surface.Kind != CushionSurfaceKind.Rail)
            {
                return;
            }

            if (info.Ball.IsCueBall)
            {
                if (!cueContactMade)
                {
                    cueRailBeforeContact = true;
                    cueRailsBeforeContact++;
                }
            }
            else
            {
                MarkBall(touchedRail, info.Ball.BallId);
            }
        }

        private void HandlePocketed(PoolBall ball, Pocket pocket)
        {
            if (shotActive && !ball.IsCueBall && pocket != null)
            {
                pocketed.Add(ball.BallId);
            }
        }

        private void HandleStateChanged()
        {
            MatchState state = turnManager.State;
            ShotOutcome outcome = state != null ? state.LastOutcome : null;
            if (outcome == null || outcome == lastOutcome)
            {
                return;
            }

            lastOutcome = outcome;
            shotActive = false;
            pending.Clear();
            Evaluate(state, outcome);
            for (int i = 0; i < pending.Count; i++)
            {
                CalloutRaised?.Invoke(pending[i]);
            }

            HighlightDetected?.Invoke(BuildHighlight(state, outcome));
        }

        /// <summary>A ball the cue (or the first object ball) touched this shot stopped right at a pocket mouth.</summary>
        private bool HangingOnLip()
        {
            if (table == null) table = FindAnyObjectByType<TableBuilder>();
            if (table == null || table.PocketManager == null)
            {
                return false;
            }

            foreach (PoolBall ball in BallRegistry.Balls)
            {
                if (ball == null || ball.IsCueBall || ball.IsPocketed || !ball.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!IsMarked(hitByCue, ball.BallId) && ball.BallId != firstObjectHit)
                {
                    continue;
                }

                foreach (Pocket candidate in table.PocketManager.Pockets)
                {
                    Vector3 offset = ball.Position - candidate.Center;
                    offset.y = 0f;
                    if (offset.magnitude < candidate.HoleRadius + ball.Radius * 1.1f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private TableBuilder table;

        private ShotHighlight BuildHighlight(MatchState state, ShotOutcome outcome)
        {
            var highlight = new ShotHighlight
            {
                Shooter = shooter,
                Foul = outcome.Foul,
                WasBreak = outcome.WasBreak,
                GameOver = outcome.GameOver,
                Winner = outcome.WinnerIndex,
                Kick = cueRailBeforeContact,
                FoulType = outcome.FoulType,
                Missed = !outcome.Foul && !outcome.GameOver && !outcome.WasBreak && !outcome.TurnContinues && pocketed.Count == 0
            };
            highlight.Pocketed.AddRange(pocketed);
            highlight.NearMiss = highlight.Missed && HangingOnLip();
            bool aimedBallDropped = false;
            bool railOrKiss = cueRailsBeforeContact >= 2;
            for (int i = 0; i < pocketed.Count; i++)
            {
                int ball = pocketed[i];
                highlight.Combo |= !IsMarked(hitByCue, ball);
                highlight.Bank |= IsMarked(touchedRail, ball);
                aimedBallDropped |= ball == firstObjectHit;
                railOrKiss |= IsMarked(touchedRail, ball) || !IsMarked(hitByCue, ball);
            }

            // Luck: not the ball the cue hit first, and it got there via a rail or another ball; or a multi-rail kick.
            highlight.Fluke = !outcome.WasBreak && pocketed.Count > 0 && firstObjectHit >= 0 && !aimedBallDropped && railOrKiss;
            if (!highlight.Fluke && !outcome.WasBreak && cueRailsBeforeContact >= 3 && pocketed.Count > 0)
            {
                highlight.Fluke = true;
            }

            int money = turnManager != null && turnManager.Rules is Rules.Practice.PracticeRuleSet ? -1
                : state.Mode == GameMode.NineBall ? 9 : 8;
            if (money > 0 && pocketed.Contains(money))
            {
                highlight.MoneyBall = money;
            }

            return highlight;
        }

        private void Evaluate(MatchState state, ShotOutcome outcome)
        {
            if (outcome.GameOver)
            {
                int winner = outcome.WinnerIndex;
                if (outcome.WasBreak && winner == rackBreaker)
                {
                    pending.Add(Loc.T("callout.golden"));
                }
                else if (winner == rackBreaker && !opponentShotThisRack && rackShots > 1)
                {
                    pending.Add(Loc.T("callout.breakrun"));
                }

                streakCount = 0;
                return;
            }

            if (outcome.Foul || pocketed.Count == 0)
            {
                streakCount = 0;
                streakPlayer = -1;
                return;
            }

            if (!outcome.WasBreak)
            {
                bool combo = false;
                bool bank = false;
                for (int i = 0; i < pocketed.Count; i++)
                {
                    int ball = pocketed[i];
                    combo |= !IsMarked(hitByCue, ball);
                    bank |= IsMarked(touchedRail, ball);
                }

                if (cueRailBeforeContact) pending.Add(Loc.T("callout.kick"));
                if (combo) pending.Add(Loc.T("callout.combo"));
                if (bank) pending.Add(Loc.T("callout.bank"));
                if (pocketed.Count >= 3) pending.Add(Loc.T("callout.triple"));
                else if (pocketed.Count == 2) pending.Add(Loc.T("callout.double"));
            }
            else if (pocketed.Count >= 2)
            {
                pending.Add(Loc.T("callout.powerbreak", pocketed.Count));
            }

            // Streak: consecutive shots by the same player that kept the turn.
            if (!outcome.TurnContinues)
            {
                streakCount = 0;
                streakPlayer = -1;
                return;
            }

            int shooter = state.CurrentPlayerIndex;
            if (shooter == streakPlayer)
            {
                streakCount++;
            }
            else
            {
                streakPlayer = shooter;
                streakCount = 1;
            }

            if (streakCount >= streakThreshold)
            {
                pending.Add(Loc.T("callout.streak", streakCount));
            }
        }

        private static void MarkBall(bool[] flags, int ball)
        {
            if (ball >= 0 && ball < flags.Length)
            {
                flags[ball] = true;
            }
        }

        private static bool IsMarked(bool[] flags, int ball) => ball >= 0 && ball < flags.Length && flags[ball];
    }
}
