using UnityEngine;
using VTG.Pool.Core;
using VTG.Pool.Rules;
using VTG.Pool.Save;

namespace VTG.Pool.Match
{
    /// <summary>
    /// Records lifetime statistics for the profile player (match player 0) from shot results and game over.
    /// Reads facts only; never affects play.
    /// </summary>
    public sealed class StatsTracker : MonoBehaviour
    {
        [SerializeField] private TurnManager turnManager;

        private ShotRecord pendingRecord;
        private ShotOutcome lastOutcome;
        private int profileShotsThisMatch;
        private bool matchCounted;

        public void Configure(TurnManager turns) => turnManager = turns;

        private void OnEnable()
        {
            PoolEvents.ShotStarted += HandleShotStarted;
            if (turnManager != null)
            {
                turnManager.StateChanged += HandleStateChanged;
            }
        }

        private void OnDisable()
        {
            PoolEvents.ShotStarted -= HandleShotStarted;
            if (turnManager != null)
            {
                turnManager.StateChanged -= HandleStateChanged;
            }
        }

        // The record object is filled during the shot; capturing it at the start avoids depending on the
        // order of BallsStopped subscribers (TurnManager evaluates on BallsStopped).
        private void HandleShotStarted(ShotRecord record) => pendingRecord = record;

        private void HandleStateChanged()
        {
            MatchState state = turnManager.State;
            if (state == null || turnManager.Rules is Rules.Practice.PracticeRuleSet)
            {
                // Practice does not count toward lifetime statistics.
                pendingRecord = null;
                return;
            }

            // A fresh rack/match resets the per-match counters.
            if (state.ShotsPlayed == 0 && state.LastOutcome == null)
            {
                profileShotsThisMatch = 0;
                matchCounted = false;
                lastOutcome = null;
                return;
            }

            ShotOutcome outcome = state.LastOutcome;
            if (outcome == null || outcome == lastOutcome)
            {
                return;
            }

            lastOutcome = outcome;
            ShotRecord record = pendingRecord;
            pendingRecord = null;
            // Offline the profile player is player 0; online it is whoever plays on this device.
            MatchManager match = FindAnyObjectByType<MatchManager>();
            if (match != null && match.IsSpectator)
            {
                return; // watching is not playing
            }

            int profile = match != null && match.IsOnline ? match.OnlineLocalIndex : 0;
            bool profileShot = record != null && record.PlayerId == profile;
            if (profileShot)
            {
                profileShotsThisMatch++;
            }

            SaveSystem.UpdateStats(stats =>
            {
                if (profileShot)
                {
                    if (!outcome.Foul)
                    {
                        stats.ballsPocketed += record.BallsPocketed.Count;
                    }
                    else
                    {
                        stats.fouls++;
                    }

                    if (outcome.WasBreak)
                    {
                        stats.breaks++;
                        if (!outcome.Foul && record.BallsPocketed.Count > 0)
                        {
                            stats.successfulBreaks++;
                        }
                    }
                }

                if (outcome.GameOver && !matchCounted)
                {
                    matchCounted = true;
                    stats.matchesPlayed++;
                    if (outcome.WinnerIndex >= 0 && outcome.WinnerIndex < state.Players.Count && state.TeamOf(outcome.WinnerIndex) == state.TeamOf(profile))
                    {
                        stats.wins++;
                        stats.shotsInWonMatches += profileShotsThisMatch;
                        stats.currentWinStreak++;
                        stats.bestWinStreak = Mathf.Max(stats.bestWinStreak, stats.currentWinStreak);
                        if (state.Mode == GameMode.EightBall) stats.eightBallWins++;
                        else stats.nineBallWins++;
                    }
                    else
                    {
                        stats.currentWinStreak = 0;
                    }
                }
            });
        }
    }
}
