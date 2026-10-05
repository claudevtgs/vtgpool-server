using VTG.Pool.Localization;
using System.Collections.Generic;
using VTG.Pool.Match;

namespace VTG.Pool.Rules.NineBall
{
    /// <summary>
    /// 9-ball rules (WPA, no push-out): lowest-numbered ball must be contacted first, combinations allowed,
    /// legal break, 9 on the break, ball in hand anywhere after every foul, 9 re-spotted when pocketed on a
    /// foul, optional three-consecutive-foul loss. The game is won by legally pocketing the 9.
    /// </summary>
    public sealed class NineBallRuleSet : IRuleSet
    {
        private static readonly int[] Numbers = { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        private readonly NineBallRulesConfig config;

        public NineBallRuleSet(NineBallRulesConfig config)
        {
            this.config = config != null ? config : NineBallRulesConfig.CreateDefault();
        }

        public GameMode Mode => GameMode.NineBall;

        public string DisplayName => Loc.T("mode.9ball");

        public IReadOnlyList<int> BallNumbers => Numbers;

        public BallInHandMode OpeningBallInHand => BallInHandMode.BehindHeadString;

        public NineBallRulesConfig Config => config;

        public void BeginRack(MatchState state)
        {
            state.SetBallsOnTable(Numbers);
            state.TableOpen = true;
            state.IsBreakShot = true;
            for (int i = 0; i < state.Players.Count; i++)
            {
                state.Players[i].Group = BallGroup.None;
                state.Players[i].ConsecutiveFouls = 0;
            }
        }

        public bool IsLegalFirstContact(MatchState state, int ballNumber)
        {
            return ballNumber > 0 && ballNumber == state.LowestBallOnTable();
        }

        public string DescribeTarget(MatchState state)
        {
            int lowest = state.LowestBallOnTable();
            string target = lowest > 0 ? Loc.T("rule9.target", lowest) : "-";
            return state.IsBreakShot ? Loc.T("rule9.target.break", target) : target;
        }

        public ShotOutcome Evaluate(MatchState state, ShotRecord shot)
        {
            var outcome = new ShotOutcome { WasBreak = state.IsBreakShot };
            string shooterName = state.CurrentPlayer.DisplayName;
            string opponentName = state.Opponent.DisplayName;
            bool ninePocketed = shot.BallsPocketed.Contains(9);
            bool nineOffTable = shot.BallsOffTable.Contains(9);

            FoulType foul = DetectFoul(state, shot);
            if (foul == FoulType.None && outcome.WasBreak)
            {
                bool legalBreak = shot.BallsPocketed.Count > 0 || shot.ObjectBallsHitRail.Count >= config.minBallsToRailOnBreak;
                if (!legalBreak)
                {
                    foul = FoulType.IllegalBreak;
                }
            }

            if (foul == FoulType.None && ninePocketed)
            {
                if (outcome.WasBreak && !config.nineOnBreakWins)
                {
                    outcome.RespotBalls.Add(9);
                }
                else
                {
                    string how = outcome.WasBreak ? Loc.T("rule9.onbreak") : (shot.FirstObjectBallHit != 9 ? Loc.T("rule9.oncombo") : string.Empty);
                    return Win(outcome, state.CurrentPlayerIndex, Loc.T("rule9.win", shooterName, how));
                }
            }

            // House rule: a foul that drops the 9 loses the rack.
            if (ninePocketed && foul != FoulType.None && config.nineOnFoulLoses)
            {
                outcome.Foul = true;
                outcome.FoulType = foul;
                return Win(outcome, state.OpponentIndex, Loc.T("rule9.nineonfoul", shooterName, Describe(foul), opponentName));
            }

            // WPA: the 9 never stays down on a foul (or off the table); other pocketed balls stay down.
            if (ninePocketed && foul != FoulType.None)
            {
                outcome.RespotBalls.Add(9);
            }

            for (int i = 0; i < shot.BallsOffTable.Count; i++)
            {
                if (!outcome.RespotBalls.Contains(shot.BallsOffTable[i]))
                {
                    outcome.RespotBalls.Add(shot.BallsOffTable[i]);
                }
            }

            if (nineOffTable && !outcome.RespotBalls.Contains(9))
            {
                outcome.RespotBalls.Add(9);
            }

            if (foul != FoulType.None)
            {
                outcome.Foul = true;
                outcome.FoulType = foul;
                outcome.TurnContinues = false;
                if (config.threeFoulRule && state.CurrentPlayer.ConsecutiveFouls >= 2)
                {
                    return Win(outcome, state.OpponentIndex, Loc.T("rule9.threefouls", shooterName, opponentName));
                }

                outcome.BallInHand = BallInHandMode.Anywhere;
                string warning = config.threeFoulRule && state.CurrentPlayer.ConsecutiveFouls == 1 ? Loc.T("rule9.twofouls", shooterName) : string.Empty;
                outcome.Message = Loc.T("rule9.foul", Describe(foul), opponentName, warning);
                return outcome;
            }

            int pocketedObjects = 0;
            for (int i = 0; i < shot.BallsPocketed.Count; i++)
            {
                if (shot.BallsPocketed[i] != 9 || outcome.RespotBalls.Contains(9))
                {
                    pocketedObjects++;
                }
            }

            outcome.TurnContinues = pocketedObjects > 0;
            outcome.Message = outcome.TurnContinues
                ? Loc.T(outcome.WasBreak ? "rule.continuesbreak" : "rule.continues", shooterName)
                : Loc.T("rule.toshoot", opponentName);
            return outcome;
        }

        private FoulType DetectFoul(MatchState state, ShotRecord shot)
        {
            if (shot.CueBallOffTable)
            {
                return FoulType.CueBallOffTable;
            }

            if (shot.CueBallPocketed)
            {
                return FoulType.Scratch;
            }

            if (shot.FirstObjectBallHit < 0)
            {
                return FoulType.NoContact;
            }

            if (!IsLegalFirstContact(state, shot.FirstObjectBallHit))
            {
                return FoulType.WrongBallFirst;
            }

            if (!state.IsBreakShot && config.requireRailAfterContact && shot.BallsPocketed.Count == 0 && shot.RailContactsAfterFirstHit == 0)
            {
                return FoulType.NoRailAfterContact;
            }

            if (shot.BallsOffTable.Count > 0)
            {
                return FoulType.ObjectBallOffTable;
            }

            return FoulType.None;
        }

        private static ShotOutcome Win(ShotOutcome outcome, int winner, string message)
        {
            outcome.GameOver = true;
            outcome.WinnerIndex = winner;
            outcome.TurnContinues = false;
            outcome.Message = message;
            return outcome;
        }

        private static string Describe(FoulType foul) => EightBall.EightBallRuleSet.Describe(foul);
    }
}
