using UnityEngine;
using UnityEngine.EventSystems;
using VTG.Pool.Cue;

namespace VTG.Pool.UI
{
    /// <summary>Spin editor face input: a tap places the tip, a drag moves it relatively at 60 % speed (marker stays visible).</summary>
    public sealed class SpinFaceInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        internal SpinEditorOverlay Owner;
        private bool dragged;

        public void OnPointerDown(PointerEventData eventData) => dragged = false;

        public void OnDrag(PointerEventData eventData)
        {
            dragged = true;
            Canvas canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Owner.SetOffset(Owner.Spin.TipOffset + eventData.delta / scale / SpinEditorOverlay.FaceRadius * 0.6f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (dragged)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(Owner.Face, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                Owner.SetOffset(local / SpinEditorOverlay.FaceRadius);
            }
        }
    }
}
