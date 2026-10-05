using System;
using System.Collections.Generic;
using UnityEngine;
using VTG.Pool.Cue;

namespace VTG.Pool.Match
{
    /// <summary>
    /// Facts about one shot (MASTER_PROMPT section 31). Filled by ShotController/ShotTracker;
    /// consumed by rules, replay, statistics, AI and multiplayer. Contains no rule decisions itself
    /// except the <see cref="Foul"/>/<see cref="Result"/> fields written by a rule set.
    /// </summary>
    [Serializable]
    public sealed class ShotRecord
    {
        public int ShotIndex;
        public int PlayerId;
        public Vector3 CueBallPosition;
        public Vector3 AimDirection;
        public float Power;
        public Vector2 CueOffset;
        public float CueElevation;
        public Vector3 InitialCueVelocity;
        public Vector3 InitialAngularVelocity;
        public List<int> BallsPocketed = new List<int>(4);
        public int FirstObjectBallHit = -1;
        public int CushionHits;
        public int CueBallCushionHits;
        public bool CueBallPocketed;

        /// <summary>Cue ball left the playing surface (not via a pocket).</summary>
        public bool CueBallOffTable;

        /// <summary>Object balls that left the table (not via a pocket).</summary>
        public List<int> BallsOffTable = new List<int>(2);

        /// <summary>Cushion contacts by any ball after the cue ball's first contact with an object ball.</summary>
        public int RailContactsAfterFirstHit;

        /// <summary>Distinct object balls that touched a cushion during the shot (legal-break check).</summary>
        public List<int> ObjectBallsHitRail = new List<int>(16);

        /// <summary>Set by the match flow when this shot is a break.</summary>
        public bool IsBreakShot;
        public bool Foul;
        public string Result = string.Empty;
        public float StartTime;
        public float Duration;

        public ShotParameters Parameters => new ShotParameters(AimDirection, Power, CueOffset, CueElevation);

        public override string ToString()
        {
            return $"Shot #{ShotIndex}: power={Power:F2} tip={CueOffset:F2} first={FirstObjectBallHit} " +
                   $"cushions={CushionHits} pocketed=[{string.Join(",", BallsPocketed)}] scratch={CueBallPocketed} t={Duration:F2}s";
        }
    }
}
