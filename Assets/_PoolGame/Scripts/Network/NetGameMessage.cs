using System;
using System.Collections.Generic;
using UnityEngine;
using VTG.Pool.Balls;
using VTG.Pool.Match;
using VTG.Pool.Rules;

namespace VTG.Pool.Network
{
    /// <summary>One ball in a table snapshot (world position on the cloth).</summary>
    [Serializable]
    public struct BallSnapshot
    {
        public int id;
        public bool onTable;
        public float x;
        public float y;
        public float z;
    }

    /// <summary>Authoritative match state sent by the host after every rack and shot.</summary>
    [Serializable]
    public sealed class NetMatchState
    {
        public int current;
        public int breaker;
        public bool isBreak;
        public bool tableOpen;
        public int ballInHand;
        public bool gameOver;
        public int winner = -1;
        public string result = string.Empty;
        public int shotsPlayed;
        public int rackNumber;
        public int[] groups = new int[0];
        public int[] fouls = new int[0];
        public int[] consecutiveFouls = new int[0];
        public int[] pocketed = new int[0];
        public bool[] left = new bool[0];

        /// <summary>Racks won in this room so far, per player (team mates share their side's wins).</summary>
        public int[] wins = new int[0];
        public int[] ballsOnTable = new int[0];

        public bool hasOutcome;
        public bool foul;
        public int foulType;
        public bool wasBreak;
        public string message = string.Empty;

        public static NetMatchState Capture(MatchState state)
        {
            var net = new NetMatchState
            {
                current = state.CurrentPlayerIndex,
                breaker = state.BreakingPlayerIndex,
                isBreak = state.IsBreakShot,
                tableOpen = state.TableOpen,
                ballInHand = (int)state.BallInHand,
                gameOver = state.IsGameOver,
                winner = state.WinnerIndex,
                result = state.Result ?? string.Empty,
                shotsPlayed = state.ShotsPlayed,
                rackNumber = state.RackNumber,
                ballsOnTable = new List<int>(state.BallsOnTable).ToArray()
            };

            int count = state.Players.Count;
            net.groups = new int[count];
            net.fouls = new int[count];
            net.consecutiveFouls = new int[count];
            net.pocketed = new int[count];
            net.left = new bool[count];
            for (int i = 0; i < count; i++)
            {
                MatchPlayer player = state.Players[i];
                net.groups[i] = (int)player.Group;
                net.left[i] = player.Left;
                net.fouls[i] = player.Fouls;
                net.consecutiveFouls[i] = player.ConsecutiveFouls;
                net.pocketed[i] = player.BallsPocketed;
            }

            ShotOutcome outcome = state.LastOutcome;
            if (outcome != null)
            {
                net.hasOutcome = true;
                net.foul = outcome.Foul;
                net.foulType = (int)outcome.FoulType;
                net.wasBreak = outcome.WasBreak;
                net.message = outcome.Message ?? string.Empty;
            }

            return net;
        }

        public void CopyTo(MatchState state)
        {
            state.CurrentPlayerIndex = current;
            state.BreakingPlayerIndex = breaker;
            state.IsBreakShot = isBreak;
            state.TableOpen = tableOpen;
            state.BallInHand = (BallInHandMode)ballInHand;
            state.IsGameOver = gameOver;
            state.WinnerIndex = winner;
            state.Result = result;
            state.ShotsPlayed = shotsPlayed;
            state.RackNumber = rackNumber;
            state.SetBallsOnTable(ballsOnTable);
            for (int i = 0; i < state.Players.Count && i < groups.Length; i++)
            {
                MatchPlayer player = state.Players[i];
                player.Group = (BallGroup)groups[i];
                if (left != null && i < left.Length && left[i]) player.Left = true;
                player.Fouls = fouls[i];
                player.ConsecutiveFouls = consecutiveFouls[i];
                player.BallsPocketed = pocketed[i];
            }
        }

        /// <summary>Outcome for HUD / stats on the guest (null when the state is a fresh rack).</summary>
        public ShotOutcome ToOutcome()
        {
            if (!hasOutcome)
            {
                return null;
            }

            return new ShotOutcome
            {
                Foul = foul,
                FoulType = (FoulType)foulType,
                GameOver = gameOver,
                WinnerIndex = winner,
                WasBreak = wasBreak,
                BallInHand = (BallInHandMode)ballInHand,
                TurnContinues = true,
                Message = message
            };
        }
    }

    /// <summary>Game message relayed between the two players (JSON through the room server).</summary>
    [Serializable]
    public sealed class NetGameMessage
    {
        public const string Aim = "aim";
        public const string Place = "place";
        public const string Shot = "shot";
        public const string State = "state";
        public const string Rematch = "rematch";

        /// <summary>Guest scene is ready: the host answers with its current state.</summary>
        public const string Ready = "ready";

        /// <summary>A spectator's scene is ready (forwarded by the server): the host answers with a spectator-only state.</summary>
        public const string Sync = "sync";

        public string type;

        // aim / shot
        public float yaw;
        public float tipX;
        public float tipY;
        public float elevation;
        public float power;

        // place / shot: cue-ball position
        public float px;
        public float py;
        public float pz;

        public int shotIndex;

        // state
        public bool newRack;

        /// <summary>Answer to a spectator's sync: players ignore it.</summary>
        public bool spectatorOnly;
        public NetMatchState match;
        public BallSnapshot[] balls;

        public string ToJson() => JsonUtility.ToJson(this);

        public static NetGameMessage FromJson(string json)
        {
            try
            {
                return JsonUtility.FromJson<NetGameMessage>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static BallSnapshot[] CaptureTable(IReadOnlyList<PoolBall> balls)
        {
            var result = new List<BallSnapshot>(balls.Count);
            for (int i = 0; i < balls.Count; i++)
            {
                PoolBall ball = balls[i];
                if (ball == null)
                {
                    continue;
                }

                Vector3 p = ball.Position;
                result.Add(new BallSnapshot
                {
                    id = ball.BallId,
                    onTable = ball.gameObject.activeInHierarchy && !ball.IsPocketed,
                    x = p.x,
                    y = p.y,
                    z = p.z
                });
            }

            return result.ToArray();
        }
    }
}
