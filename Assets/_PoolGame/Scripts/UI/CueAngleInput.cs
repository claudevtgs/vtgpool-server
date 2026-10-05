using UnityEngine;
using UnityEngine.EventSystems;
using VTG.Pool.Cue;

namespace VTG.Pool.UI
{
    /// <summary>Spin editor cue-angle input: a tap sets the angle at that height, a drag changes it relatively (0.15° per unit).</summary>
    public sealed class CueAngleInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        internal SpinEditorOverlay Owner;
        private bool dragged;

        public void OnPointerDown(PointerEventData eventData) => dragged = false;

        public void OnDrag(PointerEventData eventData)
        {
            dragged = true;
            Canvas canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Owner.Spin.SetElevation(Owner.Spin.Elevation + eventData.delta.y / scale * 0.15f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (dragged)
            {
                Owner.SetElevation(Owner.Spin.Elevation);
                return;
            }

            var rect = (RectTransform)transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                Owner.SetElevation((local.y / rect.rect.height + 0.5f) * ShotParameters.MaxCueElevation);
            }
        }
    }
}
