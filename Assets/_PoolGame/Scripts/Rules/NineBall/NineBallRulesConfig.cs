using UnityEngine;

namespace VTG.Pool.Rules.NineBall
{
    /// <summary>Configurable 9-ball rule options (defaults follow WPA world-standardised rules, without push-out).</summary>
    [CreateAssetMenu(menuName = "VTG Pool/Rules/9-Ball Rules", fileName = "NineBallRules")]
    public sealed class NineBallRulesConfig : ScriptableObject
    {
        [Tooltip("A legal break must contact the 1-ball first and pocket a ball or drive at least this many object balls to a cushion.")]
        [Range(0, 9)] public int minBallsToRailOnBreak = 4;

        [Tooltip("Pocketing the 9 on a legal break wins the rack.")]
        public bool nineOnBreakWins = true;

        [Tooltip("House rule: pocketing the 9 on a foul loses the rack (WPA: the 9 is re-spotted and play goes on).")]
        public bool nineOnFoulLoses = true;

        [Tooltip("Three consecutive fouls by the same player lose the rack.")]
        public bool threeFoulRule = true;

        [Tooltip("After contact a ball must be pocketed or any ball must reach a cushion.")]
        public bool requireRailAfterContact = true;

        public static NineBallRulesConfig CreateDefault()
        {
            var config = CreateInstance<NineBallRulesConfig>();
            config.name = "NineBallRules (default)";
            return config;
        }
    }
}
