using VTG.Pool.Localization;
using System.Collections.Generic;
using VTG.Pool.Match;

namespace VTG.Pool.Rules.EightBall
{
    /// <summary>
    /// 8-ball (15-ball rack) rules, uncalled-shot variant of the WPA rules with configurable options:
    /// legal break, open table, group assignment, first legal contact, rail after contact, scratch,
    /// ball in hand, 8 on the break, early 8 loss and legal 8 win.
    /// </summary>
    public sealed class EightBallRuleSet : IRuleSet
    {
        private static readonly int[] Numbers = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

        private readonly EightBallRulesConfig config;

        public EightBallRuleSet(EightBallRulesConfig config)
        {
            this.config = config != null ? config : EightBallRulesConfig.CreateDefault();
        }

        public GameMode Mode => GameMode.EightBall;

        public string DisplayName => Loc.T("mode.8ball");

        public IReadOnlyList<int> BallNumbers => Numbers;

        public BallInHandMode OpeningBallInHand => BallInHandMode.BehindHeadString;

        public EightBallRulesConfig Config => config;

        public void BeginRack(MatchState state)
        {
            state.SetBallsOnTable(Numbers);
            state.TableOpen = true;
            state.IsBreakShot = true;
            for (int i = 0; i < state.Players.Count; i++)
            {
                state.Players[i].Group = BallGroup.None;
            }
        }

        /// <summary>True when the current player has cleared their group and must play the 8.</summary>
        public static bool IsShootingEight(MatchState state)
        {
            BallGroup group = state.CurrentPlayer.Group;
            return !state.TableOpen && group != BallGroup.None && state.RemainingInGroup(group) == 0;
        }

        public bool IsLegalFirstContact(MatchState state, int ballNumber)
        {
            if (state.IsBreakShot)
            {
                return ballNumber > 0;
            }

            bool shootingEight = IsShootingEight(state);
            if (ballNumber == 8)
            {
                return shootingEight || (state.TableOpen && !config.openTableEightFirstIsFoul);
            }

            if (state.TableOpen)
            {
                return ballNumber > 0;
            }

            return !shootingEight && BallGroups.GroupOf(ballNumber) == state.CurrentPlayer.Group;
        }

        public string DescribeTarget(MatchState state)
        {
            if (state.IsBreakShot)
            {
                return Loc.T("rule8.target.break");
            }

            if (state.TableOpen)
            {
                return Loc.T("rule8.target.open");
            }

            return IsShootingEight(state) ? Loc.T("rule8.target.eight") : BallGroups.Describe(state.CurrentPlayer.Group);
        }

        public ShotOutcome Evaluate(MatchState state, ShotRecord shot)
        {
            var outcome = new ShotOutcome { WasBreak = state.IsBreakShot };
            string shooterName = state.CurrentPlayer.DisplayName;
            string opponentName = state.Opponent.DisplayName;
            bool shootingEight = IsShootingEight(state);
            bool eightPocketed = shot.BallsPocketed.Contains(8);
            bool eightOffTable = shot.BallsOffTable.Contains(8);
            bool scratch = shot.CueBallPocketed || shot.CueBallOffTable;

            FoulType foul = DetectFoul(state, shot);

            if (outcome.WasBreak)
            {
                bool legalBreak = shot.BallsPocketed.Count > 0 || shot.ObjectBallsHitRail.Count >= config.minBallsToRailOnBreak;
                if (!legalBreak)
                {
                    outcome.Foul = true;
                    outcome.FoulType = FoulType.IllegalBreak;
                    outcome.TurnContinues = false;
                    if (config.rerackOnIllegalBreak)
                    {
                        outcome.Rerack = true;
                        outcome.Message = Loc.T("rule8.illegalbreak.rerack", shooterName, opponentName);
                    }
                    else
                    {
                        outcome.BallInHand = BallInHandMode.Anywhere;
                        outcome.Message = Loc.T("rule8.illegalbreak.bih", shooterName, opponentName);
                    }

                    return outcome;
                }

                if (eightPocketed && config.eightOnBreak == EightOnBreakPolicy.Win && !scratch)
                {
                    return Win(outcome, state.CurrentPlayerIndex, Loc.T("rule8.eightonbreak", shooterName));
                }

                if (eightPocketed || eightOffTable)
                {
                    outcome.RespotBalls.Add(8);
                }
            }
            else if (eightPocketed || eightOffTable)
            {
                if (eightOffTable)
                {
                    return Win(outcome, state.OpponentIndex, Loc.T("rule8.eightofftable", shooterName, opponentName));
                }

                if (!shootingEight)
                {
                    return Win(outcome, state.OpponentIndex, Loc.T("rule8.eightearly", shooterName, opponentName));
                }

                if (foul != FoulType.None && (config.anyFoulOnEightLoses || scratch))
                {
                    return Win(outcome, state.OpponentIndex, Loc.T("rule8.eightfoul", shooterName, opponentName, Describe(foul)));
                }

                return Win(outcome, state.CurrentPlayerIndex, Loc.T("rule8.eightwin", shooterName));
            }

            for (int i = 0; i < shot.BallsOffTable.Count; i++)
            {
                int ball = shot.BallsOffTable[i];
                if (ball != 8)
                {
                    outcome.RespotBalls.Add(ball);
                }
            }

            if (foul != FoulType.None)
            {
                outcome.Foul = true;
                outcome.FoulType = foul;
                outcome.TurnContinues = false;
                outcome.BallInHand = outcome.WasBreak && scratch && config.breakScratchBehindHeadString
                    ? BallInHandMode.BehindHeadString
                    : BallInHandMode.Anywhere;
                string where = Loc.T(outcome.BallInHand == BallInHandMode.BehindHeadString ? "rule.where.kitchen" : "rule.where.anywhere");
                outcome.Message = Loc.T("rule8.foul", Describe(foul), opponentName, where);
                return outcome;
            }

            int firstPocketedObject = -1;
            int ownPocketed = 0;
            BallGroup group = state.CurrentPlayer.Group;
            for (int i = 0; i < shot.BallsPocketed.Count; i++)
            {
                int ball = shot.BallsPocketed[i];
                if (ball == 8)
                {
                    continue;
                }

                if (firstPocketedObject < 0)
                {
                    firstPocketedObject = ball;
                }
            }

            if (state.TableOpen && firstPocketedObject > 0 && (!outcome.WasBreak || config.assignGroupsOnBreak))
            {
                outcome.AssignGroupToShooter = BallGroups.GroupOf(firstPocketedObject);
                group = outcome.AssignGroupToShooter;
            }

            for (int i = 0; i < shot.BallsPocketed.Count; i++)
            {
                int ball = shot.BallsPocketed[i];
                if (ball == 8)
                {
                    continue;
                }

                if (group == BallGroup.None || BallGroups.GroupOf(ball) == group)
                {
                    ownPocketed++;
                }
            }

            outcome.TurnContinues = ownPocketed > 0;
            if (outcome.AssignGroupToShooter != BallGroup.None)
            {
                outcome.Message = Loc.T("rule8.takesgroup", shooterName, BallGroups.Describe(outcome.AssignGroupToShooter));
            }
            else if (outcome.TurnContinues)
            {
                outcome.Message = Loc.T(outcome.WasBreak ? "rule.continuesbreak" : "rule.continues", shooterName);
            }
            else
            {
                outcome.Message = Loc.T("rule.toshoot", opponentName);
            }

            if (outcome.RespotBalls.Contains(8) && outcome.WasBreak)
            {
                outcome.Message = Loc.T("rule8.eightspotted") + outcome.Message;
            }

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

        public static string Describe(FoulType foul)
        {
            switch (foul)
            {
                case FoulType.Scratch: return Loc.T("foul.scratch");
                case FoulType.CueBallOffTable: return Loc.T("foul.cueofftable");
                case FoulType.NoContact: return Loc.T("foul.nocontact");
                case FoulType.WrongBallFirst: return Loc.T("foul.wrongfirst");
                case FoulType.NoRailAfterContact: return Loc.T("foul.norail");
                case FoulType.IllegalBreak: return Loc.T("foul.illegalbreak");
                case FoulType.ObjectBallOffTable: return Loc.T("foul.objectofftable");
                default: return Loc.T("foul.none");
            }
        }
    }
}
