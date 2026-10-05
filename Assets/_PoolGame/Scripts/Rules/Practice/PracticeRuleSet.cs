using VTG.Pool.Localization;
using System.Collections.Generic;
using VTG.Pool.Match;

namespace VTG.Pool.Rules.Practice
{
    /// <summary>
    /// Free practice: one player keeps the table, any ball may be hit first, nothing is a foul and the game
    /// never ends. A scratch (or cue ball off the table) gives ball in hand anywhere. <see cref="Mode"/> only
    /// selects the rack shape (and the HUD ball display).
    /// </summary>
    public sealed class PracticeRuleSet : IRuleSet
    {
        private static readonly int[] EightBallNumbers = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
        private static readonly int[] NineBallNumbers = { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        public PracticeRuleSet(GameMode rackMode)
        {
            Mode = rackMode;
        }

        public GameMode Mode { get; }

        public string DisplayName => Loc.T("mode.practice");

        public IReadOnlyList<int> BallNumbers => Mode == GameMode.NineBall ? NineBallNumbers : EightBallNumbers;

        public BallInHandMode OpeningBallInHand => BallInHandMode.Anywhere;

        public void BeginRack(MatchState state)
        {
            state.SetBallsOnTable(BallNumbers);
            state.TableOpen = true;
            state.IsBreakShot = true;
            for (int i = 0; i < state.Players.Count; i++)
            {
                state.Players[i].Group = BallGroup.None;
                state.Players[i].ConsecutiveFouls = 0;
            }
        }

        public bool IsLegalFirstContact(MatchState state, int ballNumber) => ballNumber > 0;

        public string DescribeTarget(MatchState state) => Loc.T(state.BallsOnTable.Count > 0 ? "rulep.target.any" : "rulep.target.clear");

        public ShotOutcome Evaluate(MatchState state, ShotRecord shot)
        {
            var outcome = new ShotOutcome
            {
                WasBreak = state.IsBreakShot,
                TurnContinues = true
            };

            int pocketed = shot.BallsPocketed.Count;
            int remaining = state.BallsOnTable.Count - pocketed - shot.BallsOffTable.Count;
            if (shot.CueBallPocketed || shot.CueBallOffTable)
            {
                outcome.BallInHand = BallInHandMode.Anywhere;
                outcome.Message = Loc.T(shot.CueBallPocketed ? "rulep.scratch" : "rulep.cueofftable");
            }
            else if (pocketed > 0 && remaining <= 0)
            {
                outcome.Message = Loc.T("rulep.cleared");
            }
            else if (pocketed > 0)
            {
                outcome.Message = pocketed == 1 ? Loc.T("rulep.pocketedone", shot.BallsPocketed[0]) : Loc.T("rulep.pocketedmany", pocketed);
            }
            else if (shot.FirstObjectBallHit < 0)
            {
                outcome.Message = Loc.T("rulep.nohit");
            }

            return outcome;
        }
    }
}
