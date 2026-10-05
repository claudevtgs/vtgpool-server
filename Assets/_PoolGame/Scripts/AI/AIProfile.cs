using UnityEngine;

namespace VTG.Pool.AI
{
    public enum AIDifficulty { Beginner, Intermediate, Advanced, Expert }

    /// <summary>Planning preferences and bounded execution error; never changes ball physics.</summary>
    [CreateAssetMenu(menuName = "VTG Pool/AI Profile", fileName = "AIProfile")]
    public sealed class AIProfile : ScriptableObject
    {
        public AIDifficulty difficulty = AIDifficulty.Intermediate;
        [Range(1f, 85f)] public float maximumCutAngle = 70f;
        [Min(0f)] public float clearance = 0.002f;
        [Min(0f)] public float distanceWeight = 1f;
        [Min(0f)] public float cutWeight = 2f;
        [Min(0f)] public float thinkSeconds = 1.2f;
        [Range(0.1f, 1f)] public float breakPower = 0.95f;
        [Range(0.1f, 1f)] public float fallbackPower = 0.4f;
        [Min(0.05f)] public float pocketArrivalSpeed = 0.4f;
        [Min(0f)] public float scratchWeight = 8f;
        [Min(0f)] public float positionWeight = 0.3f;
        [Range(0.1f, 1f)] public float kickPower = 0.55f;
        [Range(0f, 20f)] public float kickSearchDegrees = 12f;

        public float AimErrorDegrees => difficulty switch
        {
            AIDifficulty.Beginner => 2f,
            AIDifficulty.Intermediate => 0.8f,
            AIDifficulty.Advanced => 0.3f,
            _ => 0.1f
        };

        public float RelativePowerError => difficulty switch
        {
            AIDifficulty.Beginner => 0.2f,
            AIDifficulty.Intermediate => 0.1f,
            AIDifficulty.Advanced => 0.05f,
            _ => 0.02f
        };

        private void OnValidate()
        {
            maximumCutAngle = Mathf.Clamp(maximumCutAngle, 1f, 85f);
            clearance = Mathf.Max(0f, clearance);
            distanceWeight = Mathf.Max(0f, distanceWeight);
            cutWeight = Mathf.Max(0f, cutWeight);
            thinkSeconds = Mathf.Max(0f, thinkSeconds);
            pocketArrivalSpeed = Mathf.Max(0.05f, pocketArrivalSpeed);
            scratchWeight = Mathf.Max(0f, scratchWeight);
            positionWeight = Mathf.Max(0f, positionWeight);
            kickPower = Mathf.Clamp(kickPower, 0.1f, 1f);
            kickSearchDegrees = Mathf.Clamp(kickSearchDegrees, 0f, 20f);
        }
    }
}
