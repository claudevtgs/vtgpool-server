using UnityEngine;

namespace VTG.Pool.Inputs
{
    /// <summary>Pure helpers for touch gestures and touch layout (unit tested).</summary>
    public static class TouchGestures
    {
        /// <summary>
        /// Two-finger gesture between two frames: <paramref name="spread"/> is the change of finger distance in
        /// pixels (positive = fingers apart = zoom in), <paramref name="pan"/> the movement of their midpoint.
        /// </summary>
        public static void TwoFinger(Vector2 previousA, Vector2 previousB, Vector2 currentA, Vector2 currentB, out float spread, out Vector2 pan)
        {
            spread = Vector2.Distance(currentA, currentB) - Vector2.Distance(previousA, previousB);
            pan = (currentA + currentB) * 0.5f - (previousA + previousB) * 0.5f;
        }

        /// <summary>
        /// Power of a pull-back slider: distance dragged down from where the finger went down, as a fraction
        /// of the track length (0..1). Dragging up gives 0 (cancels).
        /// </summary>
        public static float PullPower(float startY, float currentY, float trackLength)
        {
            if (trackLength <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01((startY - currentY) / trackLength);
        }

        /// <summary>Normalised anchors (0..1) of the safe area inside the full screen.</summary>
        public static void SafeAreaAnchors(Rect safeArea, Vector2 screenSize, out Vector2 anchorMin, out Vector2 anchorMax)
        {
            if (screenSize.x <= 0f || screenSize.y <= 0f)
            {
                anchorMin = Vector2.zero;
                anchorMax = Vector2.one;
                return;
            }

            anchorMin = new Vector2(Mathf.Clamp01(safeArea.xMin / screenSize.x), Mathf.Clamp01(safeArea.yMin / screenSize.y));
            anchorMax = new Vector2(Mathf.Clamp01(safeArea.xMax / screenSize.x), Mathf.Clamp01(safeArea.yMax / screenSize.y));
        }
    }
}
