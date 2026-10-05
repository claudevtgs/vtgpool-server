using System.Collections.Generic;
using VTG.Pool.Match;

namespace VTG.Pool.Rules
{
    /// <summary>
    /// A game's rules. Pure decision logic over <see cref="MatchState"/> and <see cref="ShotRecord"/> facts:
    /// no physics, no UI, no scene access, so rule sets are fully unit-testable.
    /// </summary>
    public interface IRuleSet
    {
        GameMode Mode { get; }

        string DisplayName { get; }

        /// <summary>Ball numbers used by this game (excluding the cue ball).</summary>
        IReadOnlyList<int> BallNumbers { get; }

        /// <summary>Cue-ball placement allowed for the opening shot of a rack (break).</summary>
        BallInHandMode OpeningBallInHand { get; }

        /// <summary>Prepares a fresh rack (groups, flags) in the state.</summary>
        void BeginRack(MatchState state);

        /// <summary>True if the current player may legally contact this ball first.</summary>
        bool IsLegalFirstContact(MatchState state, int ballNumber);

        /// <summary>Short description of the legal target for the HUD (e.g. "Solids", "8-ball", "Any").</summary>
        string DescribeTarget(MatchState state);

        /// <summary>Evaluates a completed shot against the state as it was before the shot.</summary>
        ShotOutcome Evaluate(MatchState state, ShotRecord shot);
    }
}
