using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VTG.Pool.Aiming;

namespace VTG.Pool.UI
{
    /// <summary>
    /// Fine-aim wheel for touch screens: dragging the ridged strip up or down turns the aim by small amounts
    /// (a full strip length is a few degrees). The ridges scroll with the finger as feedback.
    /// </summary>
    public sealed class TouchAimStrip : MonoBehaviour, IDragHandler
    {
        private const int RidgeCount = 14;

        [SerializeField, Tooltip("Aim degrees per canvas unit dragged.")] private float degreesPerUnit = 0.02f;

        private AimSystem aim;
        private RectTransform area;
        private RectTransform[] ridges;
        private Canvas canvas;
        private float scroll;

        /// <summary>Aiming is accepted only while this returns true.</summary>
        public System.Func<bool> CanAim { get; set; }

        public float DegreesPerUnit => degreesPerUnit;

        public void Initialize(RectTransform stripArea, AimSystem aimSystem)
        {
            area = stripArea;
            aim = aimSystem;
            canvas = GetComponentInParent<Canvas>();
            ridges = new RectTransform[RidgeCount];
            for (int i = 0; i < RidgeCount; i++)
            {
                var go = new GameObject("Ridge", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(area, false);
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.15f, 0.5f);
                rect.anchorMax = new Vector2(0.85f, 0.5f);
                rect.sizeDelta = new Vector2(0f, 3f);
                Image image = go.GetComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0.35f);
                image.raycastTarget = false;
                ridges[i] = rect;
            }

            LayoutRidges();
        }

        public void OnDrag(PointerEventData eventData)
        {
            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Turn(eventData.delta.y / scale);
        }

        /// <summary>Turns the aim for a drag of <paramref name="units"/> canvas units (positive = up = clockwise).</summary>
        public bool Turn(float units)
        {
            if (aim == null || (CanAim != null && !CanAim()))
            {
                return false;
            }

            aim.Rotate(units * degreesPerUnit);
            scroll += units;
            LayoutRidges();
            return true;
        }

        private void LayoutRidges()
        {
            if (ridges == null || area == null)
            {
                return;
            }

            float height = Mathf.Max(1f, area.rect.height);
            float spacing = height / RidgeCount;
            for (int i = 0; i < RidgeCount; i++)
            {
                float y = Mathf.Repeat(i * spacing + scroll, height) - height * 0.5f;
                ridges[i].anchoredPosition = new Vector2(0f, y);
                // Fake a cylinder: ridges fade toward the ends.
                float t = 1f - Mathf.Abs(y) / (height * 0.5f);
                ridges[i].GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f + 0.4f * t);
            }
        }
    }
}
