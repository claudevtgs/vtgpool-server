using UnityEngine;
using UnityEngine.UI;
using VTG.Pool.Aiming;
using VTG.Pool.Cue;
using VTG.Pool.Inputs;
using VTG.Pool.Table;

namespace VTG.Pool.UI
{
    /// <summary>
    /// On-screen controls for phones and tablets, built under the HUD: pull-back power slider, fine-aim strip,
    /// PLACE / MOVE CUE BALL buttons for ball in hand, and tap-on-table to aim at a point. Drag on the table
    /// still turns the aim and two fingers zoom/orbit (handled by <see cref="PoolInputReader"/>).
    /// Mirrored for left-handed players.
    /// </summary>
    public sealed class TouchControls : MonoBehaviour
    {
        private const float SliderWidth = 116f;
        private const float SliderHeight = 430f;
        private const float StripWidth = 84f;
        private const float StripHeight = 360f;

        private ShotController shot;
        private AimSystem aim;
        private CueBallPlacementController placement;
        private PoolInputReader input;
        private RectTransform root;
        private RectTransform slider;
        private RectTransform strip;
        private Button placeButton;
        private Button moveCueButton;

        public TouchPowerSlider PowerSlider { get; private set; }

        public TouchAimStrip AimStrip { get; private set; }

        public bool Visible => root != null && root.gameObject.activeSelf;

        public static TouchControls Create(RectTransform parent, ShotController shotController, AimSystem aimSystem,
            CueBallPlacementController ballInHand, PoolInputReader inputReader)
        {
            RectTransform rootRect = UiKit.Rect(parent, "TouchControls", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            TouchControls controls = rootRect.gameObject.AddComponent<TouchControls>();
            controls.Build(rootRect, shotController, aimSystem, ballInHand, inputReader);
            return controls;
        }

        private void Build(RectTransform rootRect, ShotController shotController, AimSystem aimSystem, CueBallPlacementController ballInHand, PoolInputReader inputReader)
        {
            root = rootRect;
            shot = shotController;
            aim = aimSystem;
            placement = ballInHand;
            input = inputReader;

            // Power slider: dark track, fill from the top, a cue-tip handle that follows the pull.
            slider = UiKit.Rect(root, "PowerSlider", new Vector2(1f, 0.5f), Vector2.zero, new Vector2(SliderWidth, SliderHeight), new Color(0.05f, 0.06f, 0.08f, 0.72f));
            RectTransform fillRect = UiKit.Rect(slider, "Fill", new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(SliderWidth - 36f, SliderHeight * 0.8f), Color.green);
            Image fill = fillRect.GetComponent<Image>();
            fill.sprite = UiKit.WhiteSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Top;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;
            RectTransform handle = UiKit.Rect(slider, "Handle", new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(SliderWidth - 12f, 40f), new Color(0.93f, 0.9f, 0.82f, 0.95f));
            handle.GetComponent<Image>().raycastTarget = false;
            UiKit.Label(handle, "touch.pull", 18, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SliderWidth, 40f), new Color(0.1f, 0.1f, 0.12f), FontStyle.Bold);
            UiKit.Label(slider, "touch.release", 15, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(SliderWidth, 40f), UiKit.Muted);
            PowerSlider = slider.gameObject.AddComponent<TouchPowerSlider>();
            PowerSlider.Initialize(slider, handle, fill, shot);

            // Fine-aim strip.
            strip = UiKit.Rect(root, "AimStrip", new Vector2(1f, 0.5f), Vector2.zero, new Vector2(StripWidth, StripHeight), new Color(0.05f, 0.06f, 0.08f, 0.72f));
            UiKit.Label(strip, "touch.fineaim", 14, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, 26f), new Vector2(StripWidth + 20f, 40f), UiKit.Muted, FontStyle.Bold);
            AimStrip = strip.gameObject.AddComponent<TouchAimStrip>();
            AimStrip.Initialize(strip, aim);
            AimStrip.CanAim = () => shot != null && shot.Phase == Core.ShotPhase.Aiming && !shot.ShotInputBlocked;

            // Ball in hand.
            placeButton = UiKit.Button(root, "touch.place", new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(260f, 76f), () => placement.TryConfirm(), 30);
            moveCueButton = UiKit.Button(root, "touch.movecue", new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(300f, 64f), () => placement.BeginPlacement(), 22);
            placeButton.gameObject.SetActive(false);
            moveCueButton.gameObject.SetActive(false);
        }

        /// <summary>Shows or hides the controls and puts the slider/strip on the player's preferred side.</summary>
        public void SetLayout(bool visible, bool leftHanded)
        {
            root.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            float side = leftHanded ? 0f : 1f;
            float sign = leftHanded ? 1f : -1f;
            Place(slider, side, new Vector2(sign * 24f, 40f));
            Place(strip, side, new Vector2(sign * (24f + SliderWidth + 18f), 40f));
        }

        private static void Place(RectTransform rect, float side, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(side, 0.5f);
            rect.anchoredPosition = position;
        }

        private void Update()
        {
            if (placement != null && shot != null)
            {
                bool humanTurn = !shot.InputOwnedElsewhere && shot.Phase == Core.ShotPhase.Aiming && !shot.LayoutEditing;
                SetActive(placeButton, humanTurn && placement.IsPlacing);
                SetActive(moveCueButton, humanTurn && !placement.IsPlacing && placement.Mode != Rules.BallInHandMode.None);
            }

            if (input != null && input.TouchTapped)
            {
                TryAimAtScreenPoint(input.PointerPosition);
            }
        }

        /// <summary>Tap on the table: aim the cue ball's path through that point (then refine with the strip).</summary>
        public bool TryAimAtScreenPoint(Vector2 screenPoint)
        {
            if (aim == null || shot == null || shot.Phase != Core.ShotPhase.Aiming || shot.ShotInputBlocked || aim.CueBall == null)
            {
                return false;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            Vector3 cue = aim.CueBall.Position;
            Ray ray = camera.ScreenPointToRay(screenPoint);
            if (!new Plane(Vector3.up, cue).Raycast(ray, out float distance))
            {
                return false;
            }

            Vector3 direction = ray.GetPoint(distance) - cue;
            direction.y = 0f;
            if (direction.magnitude < aim.CueBall.Radius * 2f)
            {
                return false;
            }

            aim.SetAimDirection(direction);
            return true;
        }

        private static void SetActive(Button button, bool active)
        {
            if (button.gameObject.activeSelf != active)
            {
                button.gameObject.SetActive(active);
            }
        }
    }
}
