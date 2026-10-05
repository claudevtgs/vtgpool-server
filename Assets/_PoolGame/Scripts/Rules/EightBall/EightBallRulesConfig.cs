using UnityEngine;

namespace VTG.Pool.Rules.EightBall
{
    public enum EightOnBreakPolicy
    {
        /// <summary>Spot the 8 on the foot spot and continue (WPA option).</summary>
        Respot,

        /// <summary>Pocketing the 8 on a legal break wins the rack (common bar rule).</summary>
        Win
    }

    /// <summary>Configurable 8-ball rule options (defaults follow WPA world-standardised rules, uncalled shots).</summary>
    [CreateAssetMenu(menuName = "VTG Pool/Rules/8-Ball Rules", fileName = "EightBallRules")]
    public sealed class EightBallRulesConfig : ScriptableObject
    {
        [Tooltip("A legal break pockets a ball or drives at least this many object balls to a cushion.")]
        [Range(0, 15)] public int minBallsToRailOnBreak = 4;

        [Tooltip("Illegal break: re-rack and the opponent breaks. Otherwise it is a normal foul (ball in hand).")]
        public bool rerackOnIllegalBreak = true;

        public EightOnBreakPolicy eightOnBreak = EightOnBreakPolicy.Respot;

        [Tooltip("Scratch on the break gives ball in hand behind the head string (WPA) instead of anywhere.")]
        public bool breakScratchBehindHeadString = true;

        [Tooltip("Groups can be assigned by balls pocketed on the break (bar rule). WPA keeps the table open.")]
        public bool assignGroupsOnBreak;

        [Tooltip("On an open table, hitting the 8 first is a foul.")]
        public bool openTableEightFirstIsFoul = true;

        [Tooltip("After contact a ball must be pocketed or any ball must reach a cushion.")]
        public bool requireRailAfterContact = true;

        [Tooltip("Pocketing the 8 together with a foul loses the game (WPA). Otherwise only scratch loses.")]
        public bool anyFoulOnEightLoses = true;

        public static EightBallRulesConfig CreateDefault()
        {
            var config = CreateInstance<EightBallRulesConfig>();
            config.name = "EightBallRules (default)";
            return config;
        }
    }
}
