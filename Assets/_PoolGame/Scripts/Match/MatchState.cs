using System.Collections.Generic;
using VTG.Pool.Rules;

namespace VTG.Pool.Match
{
    public enum PlayerKind
    {
        LocalHuman,
        AI,
        Remote
    }

    /// <summary>A participant in a match (local human for now; AI/remote later).</summary>
    public interface IMatchPlayer
    {
        int Index { get; }
        string DisplayName { get; }
        PlayerKind Kind { get; }
    }

    public class MatchPlayer : IMatchPlayer
    {
        public MatchPlayer(int index, string displayName, PlayerKind kind = PlayerKind.LocalHuman)
        {
            Index = index;
            DisplayName = displayName;
            Kind = kind;
        }

        public int Index { get; }
        public string DisplayName { get; }
        public PlayerKind Kind { get; set; }

        /// <summary>Online: left the room. Their turns are skipped, or played by a remaining team mate.</summary>
        public bool Left { get; set; }

        /// <summary>8-ball group (None while the table is open). Team mates share it.</summary>
        public BallGroup Group { get; set; }

        /// <summary>Side the player is on (doubles: 0 or 1; otherwise the player's own index).</summary>
        public int Team { get; set; } = -1;

        public int Fouls { get; set; }

        /// <summary>Fouls in a row (reset by a legal shot). Used by the 9-ball three-foul rule.</summary>
        public int ConsecutiveFouls { get; set; }
        public int BallsPocketed { get; set; }
    }

    /// <summary>
    /// Complete, serialisable-in-principle state of a match: players, turn, groups, balls on the table,
    /// ball in hand and result. Owned by the TurnManager; read by rules (pure) and the HUD.
    /// </summary>
    public sealed class MatchState
    {
        private readonly HashSet<int> ballsOnTable = new HashSet<int>();

        public MatchState(GameMode mode, IReadOnlyList<MatchPlayer> players)
        {
            Mode = mode;
            Players = players;
        }

        public GameMode Mode { get; }

        public IReadOnlyList<MatchPlayer> Players { get; }

        public int CurrentPlayerIndex { get; set; }

        public int BreakingPlayerIndex { get; set; }

        public bool IsBreakShot { get; set; } = true;

        public bool TableOpen { get; set; } = true;

        public BallInHandMode BallInHand { get; set; }

        public bool IsGameOver { get; set; }

        public int WinnerIndex { get; set; } = -1;

        public string Result { get; set; } = string.Empty;

        public int ShotsPlayed { get; set; }

        public int RackNumber { get; set; }

        public ShotOutcome LastOutcome { get; set; }

        public MatchPlayer CurrentPlayer => Players[CurrentPlayerIndex];

        public MatchPlayer Opponent => Players[OpponentIndex];

        /// <summary>Next shooter. Seats are ordered so that this also alternates teams in doubles (A1, B1, A2, B2).</summary>
        public int OpponentIndex
        {
            get
            {
                for (int step = 1; step <= Players.Count; step++)
                {
                    int next = (CurrentPlayerIndex + step) % Players.Count;
                    if (!IsOut(next)) return next;
                }

                return CurrentPlayerIndex;
            }
        }

        /// <summary>Who plays this player's turns: the player, a remaining team mate if they left, or -1.</summary>
        public int ControllerOf(int playerIndex)
        {
            if (!Players[playerIndex].Left) return playerIndex;
            for (int i = 0; i < Players.Count; i++)
            {
                if (i != playerIndex && !Players[i].Left && TeamOf(i) == TeamOf(playerIndex)) return i;
            }

            return -1;
        }

        /// <summary>Left with nobody of their side to stand in: skipped in the rotation.</summary>
        public bool IsOut(int playerIndex) => ControllerOf(playerIndex) < 0;

        /// <summary>Sides that still have a player at the table.</summary>
        public int SidesRemaining
        {
            get
            {
                var sides = new HashSet<int>();
                for (int i = 0; i < Players.Count; i++)
                {
                    if (!Players[i].Left) sides.Add(TeamOf(i));
                }

                return sides.Count;
            }
        }

        /// <summary>The player's side (team in doubles, otherwise their own index).</summary>
        public int TeamOf(int playerIndex) => Players[playerIndex].Team >= 0 ? Players[playerIndex].Team : playerIndex;

        /// <summary>Doubles: more players than sides.</summary>
        public bool HasTeams
        {
            get
            {
                for (int i = 0; i < Players.Count; i++)
                {
                    if (TeamOf(i) != i) return true;
                }

                return false;
            }
        }

        /// <summary>"An & Cường" for a team, the player's name otherwise.</summary>
        public string SideName(int playerIndex, System.Func<MatchPlayer, string> label = null)
        {
            int team = TeamOf(playerIndex);
            var names = new List<string>(2);
            for (int i = 0; i < Players.Count; i++)
            {
                if (TeamOf(i) == team) names.Add(label != null ? label(Players[i]) : Players[i].DisplayName);
            }

            return string.Join(" & ", names);
        }

        public IReadOnlyCollection<int> BallsOnTable => ballsOnTable;

        public bool IsOnTable(int ballNumber) => ballsOnTable.Contains(ballNumber);

        public void SetBallsOnTable(IEnumerable<int> balls)
        {
            ballsOnTable.Clear();
            foreach (int ball in balls)
            {
                ballsOnTable.Add(ball);
            }
        }

        public void RemoveBall(int ballNumber) => ballsOnTable.Remove(ballNumber);

        public void AddBall(int ballNumber) => ballsOnTable.Add(ballNumber);

        /// <summary>Balls of a group still on the table.</summary>
        public int RemainingInGroup(BallGroup group)
        {
            if (group == BallGroup.None)
            {
                return 0;
            }

            int count = 0;
            foreach (int ball in ballsOnTable)
            {
                if (BallGroups.GroupOf(ball) == group)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Lowest numbered ball on the table (9-ball target), or -1.</summary>
        public int LowestBallOnTable()
        {
            int lowest = -1;
            foreach (int ball in ballsOnTable)
            {
                if (ball > 0 && (lowest < 0 || ball < lowest))
                {
                    lowest = ball;
                }
            }

            return lowest;
        }
    }
}
