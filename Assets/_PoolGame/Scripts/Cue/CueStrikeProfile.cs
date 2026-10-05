using UnityEngine;

namespace VTG.Pool.Cue
{
    /// <summary>Cue and strike tuning (MASTER_PROMPT sections 14 and 19).</summary>
    [CreateAssetMenu(menuName = "VTG Pool/Cue Strike Profile", fileName = "CueStrikeProfile")]
    public sealed class CueStrikeProfile : ScriptableObject
    {
        [Tooltip("Maps normalised input power (0..1) to normalised cue speed (0..1). Ease-in gives fine control of soft shots.")]
        public AnimationCurve powerToCueVelocity = DefaultPowerCurve();

        [Tooltip("Cue speed at full power (m/s). 8 m/s gives a ~10.6 m/s break (strong amateur / pro range is 9-12 m/s).")]
        public float maxCueSpeed = 8f;

        [Tooltip("Lowest cue speed for a non-zero shot (m/s).")]
        public float minCueSpeed = 0.15f;

        [Tooltip("Cue mass (kg).")]
        public float cueMass = 0.54f;

        [Tooltip("Tip-ball coefficient of restitution.")]
        [Range(0.3f, 1f)] public float tipRestitution = 0.75f;

        [Tooltip("Largest tip offset as a fraction of ball radius (beyond ~0.5R miscues).")]
        [Range(0.1f, 0.7f)] public float maxTipOffsetFraction = 0.5f;

        [Tooltip("Cue-ball deflection (squirt) at maximum side offset, degrees. Pushes the ball away from the side of english.")]
        [Range(0f, 5f)] public float squirtAtMaxOffsetDegrees = 1.2f;

        [Header("Jump shots")]
        [Tooltip("Vertical rebound off the slate for an elevated stroke (fraction of the downward speed).")]
        [Range(0f, 0.8f)] public float slateRebound = 0.35f;

        [Tooltip("Cue elevation where the tip starts to pin the ball (massé): the jump fades out from here...")]
        [Range(20f, 89f)] public float pinStartDegrees = 50f;

        [Tooltip("...and is gone from this elevation.")]
        [Range(20f, 89f)] public float pinEndDegrees = 65f;

        public float EvaluateCueSpeed(float power01)
        {
            float normalized = Mathf.Clamp01(powerToCueVelocity.Evaluate(Mathf.Clamp01(power01)));
            if (power01 <= 0f)
            {
                return 0f;
            }

            return Mathf.Max(minCueSpeed, normalized * maxCueSpeed);
        }

        /// <summary>Inverse of <see cref="EvaluateCueSpeed"/>: the input power that gives a cue speed (m/s).</summary>
        public float PowerForCueSpeed(float cueSpeed)
        {
            if (cueSpeed <= minCueSpeed)
            {
                return cueSpeed <= 0f ? 0f : 0.001f;
            }

            if (cueSpeed >= EvaluateCueSpeed(1f))
            {
                return 1f;
            }

            float low = 0f;
            float high = 1f;
            for (int i = 0; i < 24; i++)
            {
                float mid = 0.5f * (low + high);
                if (EvaluateCueSpeed(mid) < cueSpeed) low = mid;
                else high = mid;
            }

            return 0.5f * (low + high);
        }

        public CueStrikeSettings ToSettings()
        {
            return new CueStrikeSettings
            {
                CueMass = cueMass,
                TipRestitution = tipRestitution,
                MaxTipOffsetFraction = maxTipOffsetFraction,
                SquirtAtMaxOffsetDegrees = squirtAtMaxOffsetDegrees,
                SlateRebound = slateRebound,
                PinStartDegrees = pinStartDegrees,
                PinEndDegrees = pinEndDegrees
            };
        }

        public static AnimationCurve DefaultPowerCurve()
        {
            return new AnimationCurve(new Keyframe(0f, 0f, 0f, 0.35f), new Keyframe(1f, 1f, 1.7f, 0f));
        }

        private void OnValidate()
        {
            maxCueSpeed = Mathf.Clamp(maxCueSpeed, 1f, 15f);
            minCueSpeed = Mathf.Clamp(minCueSpeed, 0.01f, maxCueSpeed);
            cueMass = Mathf.Clamp(cueMass, 0.3f, 0.8f);
            if (powerToCueVelocity == null || powerToCueVelocity.length < 2)
            {
                powerToCueVelocity = DefaultPowerCurve();
            }
        }
    }
}
