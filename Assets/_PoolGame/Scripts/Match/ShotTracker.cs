using UnityEngine;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Table;

namespace VTG.Pool.Match
{
    /// <summary>
    /// Records the facts of the shot in progress (first contact, cushions, pocketed balls) from
    /// PoolEvents. Pure bookkeeping: rule evaluation belongs to a future IRuleSet.
    /// </summary>
    public sealed class ShotTracker : MonoBehaviour
    {
        public ShotRecord CurrentShot { get; private set; }

        public ShotRecord LastCompletedShot { get; private set; }

        public int BallContactsThisShot { get; private set; }

        private void OnEnable()
        {
            PoolEvents.ShotStarted += HandleShotStarted;
            PoolEvents.BallHit += HandleBallHit;
            PoolEvents.CushionHit += HandleCushionHit;
            PoolEvents.BallPocketed += HandleBallPocketed;
            PoolEvents.BallsStopped += HandleBallsStopped;
        }

        private void OnDisable()
        {
            PoolEvents.ShotStarted -= HandleShotStarted;
            PoolEvents.BallHit -= HandleBallHit;
            PoolEvents.CushionHit -= HandleCushionHit;
            PoolEvents.BallPocketed -= HandleBallPocketed;
            PoolEvents.BallsStopped -= HandleBallsStopped;
        }

        private void HandleShotStarted(ShotRecord record)
        {
            CurrentShot = record;
            BallContactsThisShot = 0;
        }

        private void HandleBallHit(BallHitInfo info)
        {
            if (CurrentShot == null)
            {
                return;
            }

            BallContactsThisShot++;
            if (CurrentShot.FirstObjectBallHit < 0)
            {
                PoolBall cue = info.A.IsCueBall ? info.A : (info.B.IsCueBall ? info.B : null);
                if (cue != null)
                {
                    CurrentShot.FirstObjectBallHit = info.Other(cue).BallId;
                }
            }
        }

        private void HandleCushionHit(CushionHitInfo info)
        {
            if (CurrentShot == null)
            {
                return;
            }

            CurrentShot.CushionHits++;
            if (CurrentShot.FirstObjectBallHit >= 0)
            {
                CurrentShot.RailContactsAfterFirstHit++;
            }

            if (info.Ball.IsCueBall)
            {
                CurrentShot.CueBallCushionHits++;
            }
            else if (!CurrentShot.ObjectBallsHitRail.Contains(info.Ball.BallId))
            {
                CurrentShot.ObjectBallsHitRail.Add(info.Ball.BallId);
            }
        }

        private void HandleBallPocketed(PoolBall ball, Pocket pocket)
        {
            if (CurrentShot == null)
            {
                return;
            }

            bool offTable = pocket == null;
            if (ball.IsCueBall)
            {
                if (offTable)
                {
                    CurrentShot.CueBallOffTable = true;
                }
                else
                {
                    CurrentShot.CueBallPocketed = true;
                }
            }
            else if (offTable)
            {
                CurrentShot.BallsOffTable.Add(ball.BallId);
            }
            else
            {
                CurrentShot.BallsPocketed.Add(ball.BallId);
            }
        }

        private void HandleBallsStopped(ShotRecord record)
        {
            LastCompletedShot = record;
            CurrentShot = null;
        }
    }
}
