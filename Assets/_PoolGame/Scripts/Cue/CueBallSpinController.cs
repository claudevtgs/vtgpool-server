using UnityEngine;
using VTG.Pool.Inputs;

namespace VTG.Pool.Cue
{
    /// <summary>
    /// Holds the cue tip offset on the cue-ball face (unit disc: x = right, y = up).
    /// Presets: centre, top (follow), bottom (draw), left/right english and diagonals. Also holds the cue
    /// elevation used for massé shots.
    /// </summary>
    public sealed class CueBallSpinController : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSource;

        private IShotInput input;

        public Vector2 TipOffset { get; private set; }

        /// <summary>Cue elevation above horizontal (degrees). High elevation with side offset = massé.</summary>
        public float Elevation { get; private set; }

        /// <summary>Allows spin input (set by ShotController).</summary>
        public bool InputEnabled { get; set; } = true;

        public void SetInput(IShotInput shotInput)
        {
            input = shotInput;
        }

        public void SetTipOffset(Vector2 offset)
        {
            TipOffset = Vector2.ClampMagnitude(offset, 1f);
        }

        public void SetElevation(float degrees)
        {
            Elevation = Mathf.Clamp(degrees, 0f, ShotParameters.MaxCueElevation);
        }

        /// <summary>Centre tip, level cue.</summary>
        public void ResetSpin()
        {
            TipOffset = Vector2.zero;
            Elevation = 0f;
        }

        private void Awake()
        {
            if (input == null && inputSource is IShotInput shotInput)
            {
                input = shotInput;
            }
        }

        private void Update()
        {
            if (!InputEnabled || input == null)
            {
                return;
            }

            if (input.ResetSpinPressed)
            {
                ResetSpin();
            }
            else if (input.SpinDelta != Vector2.zero)
            {
                SetTipOffset(TipOffset + input.SpinDelta);
            }

            if (input.ElevationDelta != 0f)
            {
                SetElevation(Elevation + input.ElevationDelta);
            }
        }
    }
}
