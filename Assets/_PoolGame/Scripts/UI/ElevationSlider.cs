using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VTG.Pool.Cue;

namespace VTG.Pool.UI
{
    /// <summary>
    /// Cue elevation control next to the spin pad: drag up to raise the cue butt (0..80 degrees). High elevation
    /// with a side offset plays a massé. UI only: forwards the value to <see cref="CueBallSpinController"/>.
    /// </summary>
    public sealed class ElevationSlider : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        /// <summary>Elevation from which the cue pins the ball (massé label).</summary>
        public const float MasseThreshold = 55f;

        /// <summary>Elevation from which the ball leaves the cloth noticeably (jump label).</summary>
        public const float JumpThreshold = 12f;

        /// <summary>Label for a cue elevation: "", jump or massé.</summary>
        public static string Describe(float elevation)
        {
            if (elevation >= MasseThreshold) return Localization.Loc.T("hud.masse");
            if (elevation >= JumpThreshold) return Localization.Loc.T("hud.jump");
            return string.Empty;
        }

        private RectTransform area;
        private Image fill;
        private Text label;
        private CueBallSpinController spin;
        private float shown = -1f;

        public System.Func<bool> CanEdit { get; set; }

        /// <summary>When set (touch layout), a press opens the large spin editor instead of editing in place.</summary>
        public System.Action OpenEditor { get; set; }

        public void Initialize(RectTransform sliderArea, Image fillImage, Text valueLabel, CueBallSpinController spinController)
        {
            area = sliderArea;
            fill = fillImage;
            label = valueLabel;
            spin = spinController;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (OpenEditor != null)
            {
                OpenEditor();
                return;
            }

            SetFromPointer(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (OpenEditor == null)
            {
                SetFromPointer(eventData);
            }
        }

        /// <summary>Sets the elevation from a 0..1 position along the slider (also used by tests).</summary>
        public void SetNormalized(float value)
        {
            if (spin == null || (CanEdit != null && !CanEdit()))
            {
                return;
            }

            spin.SetElevation(Mathf.Clamp01(value) * ShotParameters.MaxCueElevation);
        }

        private void SetFromPointer(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                SetNormalized(local.y / Mathf.Max(1f, area.rect.height) + 0.5f);
            }
        }

        private void LateUpdate()
        {
            if (spin == null)
            {
                return;
            }

            float elevation = spin.Elevation;
            if (Mathf.Abs(elevation - shown) < 0.25f)
            {
                return;
            }

            shown = elevation;
            fill.fillAmount = elevation / ShotParameters.MaxCueElevation;
            string kind = Describe(elevation);
            fill.color = elevation >= MasseThreshold ? new Color(1f, 0.55f, 0.2f) : elevation >= JumpThreshold ? new Color(0.45f, 0.8f, 1f) : new Color(1f, 0.78f, 0.25f);
            label.text = kind.Length > 0 ? $"{elevation:0}°\n{kind}" : $"{elevation:0}°";
        }
    }
}
