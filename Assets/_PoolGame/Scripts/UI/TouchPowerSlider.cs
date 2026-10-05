using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Inputs;

namespace VTG.Pool.UI
{
    /// <summary>
    /// Touch power control: put a finger on the slider, pull down to draw the cue back (power), let go to shoot.
    /// Sliding back to the top or off to the side cancels. UI only: drives <see cref="ShotController"/>'s
    /// external charge API, the same way <see cref="SpinPad"/> drives the spin controller.
    /// </summary>
    public sealed class TouchPowerSlider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private RectTransform track;
        private RectTransform handle;
        private Image fill;
        private ShotController shot;
        private float startY;
        private float handleTop;

        public bool IsCharging { get; private set; }

        /// <summary>Pull distance (canvas units) for full power.</summary>
        public float Travel => track != null ? track.rect.height * 0.8f : 1f;

        public void Initialize(RectTransform trackRect, RectTransform handleRect, Image fillImage, ShotController shotController)
        {
            track = trackRect;
            handle = handleRect;
            fill = fillImage;
            shot = shotController;
            handleTop = handle.anchoredPosition.y;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (TryLocal(eventData, out Vector2 local))
            {
                Press(local.y);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (TryLocal(eventData, out Vector2 local))
            {
                Drag(local.x, local.y);
            }
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        /// <summary>Finger down at a local y (track space). Starts the charge if a shot is allowed now.</summary>
        public bool Press(float localY)
        {
            if (IsCharging || shot == null || !shot.BeginExternalCharge())
            {
                return false;
            }

            IsCharging = true;
            startY = localY;
            return true;
        }

        /// <summary>Finger moved (track space). Too far sideways cancels the shot.</summary>
        public void Drag(float localX, float localY)
        {
            if (!IsCharging)
            {
                return;
            }

            if (Mathf.Abs(localX) > track.rect.width * 1.5f)
            {
                Cancel();
                return;
            }

            shot.SetExternalCharge(TouchGestures.PullPower(startY, localY, Travel));
        }

        /// <summary>Finger lifted: shoots with the pulled power (a tiny pull cancels).</summary>
        public void Release()
        {
            if (!IsCharging)
            {
                return;
            }

            IsCharging = false;
            shot.ReleaseExternalCharge(true);
        }

        public void Cancel()
        {
            if (!IsCharging)
            {
                return;
            }

            IsCharging = false;
            shot.ReleaseExternalCharge(false);
        }

        private void LateUpdate()
        {
            if (shot == null)
            {
                return;
            }

            // Pause, ball in hand or a reset may end the charge from outside.
            if (IsCharging && !shot.ExternalCharge)
            {
                IsCharging = false;
            }

            float power = IsCharging && shot.Phase == ShotPhase.PowerSelection ? shot.CurrentPower : 0f;
            handle.anchoredPosition = new Vector2(handle.anchoredPosition.x, handleTop - power * Travel);
            fill.fillAmount = power;
            fill.color = Color.Lerp(new Color(0.35f, 0.85f, 0.4f), new Color(0.95f, 0.3f, 0.2f), power);
        }

        private void OnDisable() => Cancel();

        private bool TryLocal(PointerEventData eventData, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(track, eventData.position, eventData.pressEventCamera, out local);
        }
    }
}
