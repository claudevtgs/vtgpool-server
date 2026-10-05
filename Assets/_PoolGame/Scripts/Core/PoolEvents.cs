using System;
using UnityEngine;
using VTG.Pool.Balls;
using VTG.Pool.Match;
using VTG.Pool.Rules;
using VTG.Pool.Table;

namespace VTG.Pool.Core
{
    /// <summary>Payload for a ball-ball contact.</summary>
    public readonly struct BallHitInfo
    {
        public readonly PoolBall A;
        public readonly PoolBall B;
        public readonly float Impulse;
        public readonly Vector3 Point;

        public BallHitInfo(PoolBall a, PoolBall b, float impulse, Vector3 point)
        {
            A = a;
            B = b;
            Impulse = impulse;
            Point = point;
        }

        public bool Involves(PoolBall ball) => A == ball || B == ball;

        public PoolBall Other(PoolBall ball) => A == ball ? B : A;
    }

    /// <summary>Payload for a ball-cushion (rail, jaw, pocket back) contact.</summary>
    public readonly struct CushionHitInfo
    {
        public readonly PoolBall Ball;
        public readonly CushionSurface Surface;
        public readonly float NormalSpeed;
        public readonly Vector3 Point;

        public CushionHitInfo(PoolBall ball, CushionSurface surface, float normalSpeed, Vector3 point)
        {
            Ball = ball;
            Surface = surface;
            NormalSpeed = normalSpeed;
            Point = point;
        }
    }

    /// <summary>
    /// Global gameplay event hub. Systems publish facts here; rules, audio, UI, replay and AI subscribe.
    /// Keeps systems decoupled without a monolithic manager.
    /// </summary>
    public static class PoolEvents
    {
        public static event Action<ShotRecord> ShotStarted;
        public static event Action<BallHitInfo> BallHit;
        public static event Action<CushionHitInfo> CushionHit;
        public static event Action<PoolBall, Pocket> BallPocketed;
        public static event Action<PoolBall> CueBallPocketed;
        public static event Action<ShotRecord> BallsStopped;
        public static event Action<int, FoulType, string> FoulCommitted;
        public static event Action<int> TurnChanged;
        public static event Action<int, string> GameWon;

        public static void RaiseShotStarted(ShotRecord record) => ShotStarted?.Invoke(record);

        public static void RaiseBallHit(in BallHitInfo info) => BallHit?.Invoke(info);

        public static void RaiseCushionHit(in CushionHitInfo info) => CushionHit?.Invoke(info);

        public static void RaiseBallPocketed(PoolBall ball, Pocket pocket)
        {
            BallPocketed?.Invoke(ball, pocket);
            if (ball != null && ball.IsCueBall)
            {
                CueBallPocketed?.Invoke(ball);
            }
        }

        public static void RaiseBallsStopped(ShotRecord record) => BallsStopped?.Invoke(record);

        public static void RaiseFoulCommitted(int playerIndex, FoulType foul, string message) => FoulCommitted?.Invoke(playerIndex, foul, message);

        public static void RaiseTurnChanged(int playerIndex) => TurnChanged?.Invoke(playerIndex);

        public static void RaiseGameWon(int winnerIndex, string message) => GameWon?.Invoke(winnerIndex, message);

        /// <summary>Clears all subscribers (supports Enter Play Mode without domain reload).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ClearAll()
        {
            ShotStarted = null;
            BallHit = null;
            CushionHit = null;
            BallPocketed = null;
            CueBallPocketed = null;
            BallsStopped = null;
            FoulCommitted = null;
            TurnChanged = null;
            GameWon = null;
        }
    }
}
