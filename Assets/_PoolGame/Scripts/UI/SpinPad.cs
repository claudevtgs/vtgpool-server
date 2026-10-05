using UnityEngine;
using UnityEngine.EventSystems;
using VTG.Pool.Cue;

namespace VTG.Pool.UI
{
    /// <summary>
    /// Cue-ball face widget: shows the tip offset and lets mouse/touch set it by clicking or dragging.
    /// UI only: forwards the value to <see cref="CueBallSpinController"/>.
    /// </summary>
    public sealed class SpinPad : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        private RectTransform area;
        private RectTransform marker;
        private CueBallSpinController spin;

        /// <summary>Spin input is accepted only while this returns true (e.g. while aiming).</summary>
        public System.Func<bool> CanEdit { get; set; }

        /// <summary>When set (touch layout), a press opens the large spin editor instead of editing in place.</summary>
        public System.Action OpenEditor { get; set; }

        public void Initialize(RectTransform padArea, RectTransform padMarker, CueBallSpinController spinController)
        {
            area = padArea;
            marker = padMarker;
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

        private void SetFromPointer(PointerEventData eventData)
        {
            if (spin == null || (CanEdit != null && !CanEdit()))
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                Vector2 halfSize = area.rect.size * 0.5f;
                spin.SetTipOffset(new Vector2(local.x / halfSize.x, local.y / halfSize.y));
            }
        }

        private void LateUpdate()
        {
            if (spin == null || marker == null)
            {
                return;
            }

            Vector2 halfSize = area.rect.size * 0.5f;
            Vector2 offset = spin.TipOffset;
            marker.anchoredPosition = new Vector2(offset.x * halfSize.x, offset.y * halfSize.y);
        }
    }
}
