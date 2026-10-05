using VTG.Pool.Localization;
using System.Collections.Generic;

namespace VTG.Pool.Rules
{
    public enum GameMode
    {
        EightBall,
        NineBall
    }

    /// <summary>8-ball group owned by a player.</summary>
    public enum BallGroup
    {
        None,
        Solids,
        Stripes
    }

    public enum FoulType
    {
        None,
        Scratch,
        CueBallOffTable,
        NoContact,
        WrongBallFirst,
        NoRailAfterContact,
        IllegalBreak,
        ObjectBallOffTable
    }

    /// <summary>Where the incoming player may place the cue ball.</summary>
    public enum BallInHandMode
    {
        None,

        /// <summary>Anywhere on the table.</summary>
        Anywhere,

        /// <summary>Behind the head string (the "kitchen").</summary>
        BehindHeadString
    }

    /// <summary>Result of evaluating one shot. Produced by an <see cref="IRuleSet"/>, applied by the TurnManager.</summary>
    public sealed class ShotOutcome
    {
        public bool Foul;
        public FoulType FoulType;
        public bool TurnContinues;
        public BallInHandMode BallInHand;
        public bool GameOver;
        public int WinnerIndex = -1;

        /// <summary>Group assigned to the shooter by this shot (opponent gets the other group).</summary>
        public BallGroup AssignGroupToShooter;

        /// <summary>Balls to put back on the table (e.g. 8 on the break, balls driven off the table).</summary>
        public readonly List<int> RespotBalls = new List<int>(2);

        /// <summary>Re-rack and have the opponent break (illegal break).</summary>
        public bool Rerack;

        public bool WasBreak;
        public string Message = string.Empty;

        public override string ToString()
        {
            return $"foul={Foul}({FoulType}) continue={TurnContinues} bih={BallInHand} gameOver={GameOver} winner={WinnerIndex} assign={AssignGroupToShooter} rerack={Rerack} msg='{Message}'";
        }
    }

    /// <summary>Static helpers for 8-ball groups.</summary>
    public static class BallGroups
    {
        public static BallGroup GroupOf(int ballNumber)
        {
            if (ballNumber >= 1 && ballNumber <= 7) return BallGroup.Solids;
            if (ballNumber >= 9 && ballNumber <= 15) return BallGroup.Stripes;
            return BallGroup.None;
        }

        public static BallGroup Opposite(BallGroup group)
        {
            switch (group)
            {
                case BallGroup.Solids: return BallGroup.Stripes;
                case BallGroup.Stripes: return BallGroup.Solids;
                default: return BallGroup.None;
            }
        }

        public static string Describe(BallGroup group)
        {
            switch (group)
            {
                case BallGroup.Solids: return Loc.T("group.solids");
                case BallGroup.Stripes: return Loc.T("group.stripes");
                default: return Loc.T("group.open");
            }
        }
    }
}
