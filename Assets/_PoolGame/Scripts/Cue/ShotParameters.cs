using System;
using UnityEngine;

namespace VTG.Pool.Cue
{
    /// <summary>Everything needed to reproduce a cue strike. Serializable for presets, replay and AI.</summary>
    [Serializable]
    public struct ShotParameters
    {
        /// <summary>Highest cue elevation the player can choose (degrees above horizontal).</summary>
        public const float MaxCueElevation = 80f;

        [Tooltip("Horizontal aim direction (normalised).")]
        public Vector3 AimDirection;

        [Tooltip("Normalised power 0..1 (mapped through CueStrikeProfile.powerToCueVelocity).")]
        [Range(0f, 1f)] public float Power;

        [Tooltip("Cue tip offset on the ball face in the unit disc: x = right, y = up. 1 = maximum safe offset.")]
        public Vector2 TipOffset;

        [Tooltip("Cue elevation above horizontal in degrees (0 = level cue; 50-80 = massé).")]
        [Range(0f, MaxCueElevation)] public float CueElevation;

        public ShotParameters(Vector3 aimDirection, float power, Vector2 tipOffset, float cueElevation = 0f)
        {
            aimDirection.y = 0f;
            AimDirection = aimDirection.sqrMagnitude > 1e-8f ? aimDirection.normalized : Vector3.forward;
            Power = Mathf.Clamp01(power);
            TipOffset = Vector2.ClampMagnitude(tipOffset, 1f);
            CueElevation = Mathf.Clamp(cueElevation, 0f, MaxCueElevation);
        }

        public override string ToString()
        {
            return $"dir={AimDirection:F3} power={Power:F2} tip={TipOffset:F2} elev={CueElevation:F0}";
        }
    }
}
