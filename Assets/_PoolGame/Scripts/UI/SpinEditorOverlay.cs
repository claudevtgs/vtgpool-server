using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VTG.Pool.Cue;
using VTG.Pool.Localization;

namespace VTG.Pool.UI
{
    /// <summary>
    /// Large spin and cue-angle editor for touch screens (opened by tapping the HUD spin pad). The cue-ball face is
    /// big enough for a finger: a tap places the tip, a drag moves it relatively at reduced speed so the marker is
    /// never hidden under the finger. The cue angle has its own tall slider, ±1° / ±5° steps and a side-view sketch.
    /// UI only: edits <see cref="CueBallSpinController"/>.
    /// </summary>
    public sealed class SpinEditorOverlay : MonoBehaviour
    {
        private const float FaceSize = 440f;
        private const float AngleHeight = 400f;

        private CueBallSpinController spin;
        private RectTransform marker;
        private RectTransform face;

        internal CueBallSpinController Spin => spin;

        internal RectTransform Face => face;

        internal const float FaceRadius = FaceSize * 0.5f;
        private Image angleFill;
        private Text angleText;
        private Text offsetText;
        private RectTransform sketchCue;
        private Action onClose;

        public static SpinEditorOverlay Open(RectTransform parent, CueBallSpinController spinController, Action closed)
        {
            RectTransform dim = UiKit.Fill(parent, "SpinEditor", new Color(0f, 0f, 0f, 0.7f));
            SpinEditorOverlay overlay = dim.gameObject.AddComponent<SpinEditorOverlay>();
            overlay.Build(dim, spinController, closed);
            return overlay;
        }

        public void Close()
        {
            Destroy(gameObject);
            onClose?.Invoke();
        }

        private void Build(RectTransform dim, CueBallSpinController spinController, Action closed)
        {
            spin = spinController;
            onClose = closed;
            RectTransform box = UiKit.Rect(dim, "Box", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 760f), UiKit.Panel);
            UiKit.Label(box, "spin.title", 34, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1000f, 46f), UiKit.Accent, FontStyle.Bold);
            UiKit.Label(box, "spin.hint", 18, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(1000f, 30f), UiKit.Muted);

            // Cue-ball face.
            face = UiKit.Rect(box, "Face", new Vector2(0f, 1f), new Vector2(60f, -110f), new Vector2(FaceSize, FaceSize), new Color(0.96f, 0.95f, 0.9f));
            face.GetComponent<Image>().sprite = BallIconFactory.Circle;
            foreach (bool vertical in new[] { true, false })
            {
                RectTransform line = UiKit.Rect(face, "Cross", new Vector2(0.5f, 0.5f), Vector2.zero,
                    vertical ? new Vector2(2f, FaceSize * 0.9f) : new Vector2(FaceSize * 0.9f, 2f), new Color(0f, 0f, 0f, 0.15f));
                line.GetComponent<Image>().raycastTarget = false;
            }

            marker = UiKit.Rect(face, "Marker", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 46f), new Color(0.85f, 0.1f, 0.1f));
            Image markerImage = marker.GetComponent<Image>();
            markerImage.sprite = BallIconFactory.Circle;
            markerImage.raycastTarget = false;
            face.gameObject.AddComponent<SpinFaceInput>().Owner = this;

            float presetY = -110f - FaceSize - 16f;
            AddPreset(box, "spin.center", new Vector2(60f, presetY), Vector2.zero);
            AddPreset(box, "spin.top", new Vector2(60f + 90f, presetY), new Vector2(0f, 0.6f));
            AddPreset(box, "spin.bottom", new Vector2(60f + 180f, presetY), new Vector2(0f, -0.6f));
            AddPreset(box, "spin.left", new Vector2(60f + 270f, presetY), new Vector2(-0.6f, 0f));
            AddPreset(box, "spin.right", new Vector2(60f + 360f, presetY), new Vector2(0.6f, 0f));
            offsetText = UiKit.Label(box, string.Empty, 18, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(60f, presetY - 64f), new Vector2(FaceSize, 28f), UiKit.Muted);

            // Cue angle: tall slider, steps, side-view sketch.
            RectTransform track = UiKit.Rect(box, "AngleTrack", new Vector2(0f, 1f), new Vector2(560f, -150f), new Vector2(110f, AngleHeight), new Color(1f, 1f, 1f, 0.1f));
            RectTransform fillRect = UiKit.Rect(track, "Fill", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, AngleHeight), UiKit.Accent);
            angleFill = fillRect.GetComponent<Image>();
            angleFill.sprite = UiKit.WhiteSprite;
            angleFill.type = Image.Type.Filled;
            angleFill.fillMethod = Image.FillMethod.Vertical;
            angleFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            angleFill.raycastTarget = false;
            track.gameObject.AddComponent<CueAngleInput>().Owner = this;
            angleText = UiKit.Label(box, string.Empty, 26, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(560f, -110f), new Vector2(480f, 36f), UiKit.Text, FontStyle.Bold);

            float stepX = 700f;
            AddStep(box, "+5°", new Vector2(stepX, -150f), 5f);
            AddStep(box, "+1°", new Vector2(stepX, -220f), 1f);
            AddStep(box, "−1°", new Vector2(stepX, -290f), -1f);
            AddStep(box, "−5°", new Vector2(stepX, -360f), -5f);
            Button level = UiKit.Button(box, "spin.level", new Vector2(0f, 1f), new Vector2(stepX, -430f), new Vector2(150f, 56f), () => SetElevation(0f), 20);
            level.name = "Button_Level";

            // Side view: ball and a cue rotated by the elevation.
            RectTransform sketch = UiKit.Rect(box, "Sketch", new Vector2(0f, 1f), new Vector2(870f, -150f), new Vector2(190f, 190f), new Color(0f, 0f, 0f, 0.25f));
            RectTransform ball = UiKit.Rect(sketch, "Ball", new Vector2(0.5f, 0f), new Vector2(40f, 12f), new Vector2(44f, 44f), new Color(0.96f, 0.95f, 0.9f));
            ball.GetComponent<Image>().sprite = BallIconFactory.Circle;
            RectTransform cloth = UiKit.Rect(sketch, "Cloth", new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(180f, 4f), new Color(0.3f, 0.5f, 1f));
            cloth.GetComponent<Image>().raycastTarget = false;
            sketchCue = UiKit.Rect(sketch, "Cue", new Vector2(0.5f, 0f), new Vector2(18f, 34f), new Vector2(150f, 8f), new Color(0.75f, 0.6f, 0.4f));
            sketchCue.pivot = new Vector2(1f, 0.5f);
            sketchCue.GetComponent<Image>().raycastTarget = false;

            UiKit.Button(box, "spin.done", new Vector2(1f, 0f), new Vector2(-40f, 30f), new Vector2(260f, 72f), Close, 28);
            Refresh();
        }

        private void AddPreset(RectTransform box, string key, Vector2 position, Vector2 offset)
        {
            UiKit.Button(box, key, new Vector2(0f, 1f), position, new Vector2(84f, 52f), () => SetOffset(offset), 17);
        }

        private void AddStep(RectTransform box, string label, Vector2 position, float degrees)
        {
            UiKit.Button(box, label, new Vector2(0f, 1f), position, new Vector2(150f, 60f), () => SetElevation(spin.Elevation + degrees), 24);
        }

        /// <summary>Sets the tip offset (unit disc) — also used by tests.</summary>
        public void SetOffset(Vector2 offset)
        {
            spin.SetTipOffset(offset);
            Refresh();
        }

        public void SetElevation(float degrees)
        {
            spin.SetElevation(Mathf.Round(degrees));
            Refresh();
        }

        private void Refresh()
        {
            Vector2 offset = spin.TipOffset;
            marker.anchoredPosition = offset * (FaceSize * 0.5f);
            offsetText.text = Loc.T("spin.offset", $"({offset.x:+0.00;-0.00;0.00}, {offset.y:+0.00;-0.00;0.00})");
            float elevation = spin.Elevation;
            angleFill.fillAmount = elevation / ShotParameters.MaxCueElevation;
            string kind = ElevationSlider.Describe(elevation);
            angleFill.color = elevation >= ElevationSlider.MasseThreshold ? new Color(1f, 0.55f, 0.2f)
                : elevation >= ElevationSlider.JumpThreshold ? new Color(0.45f, 0.8f, 1f) : UiKit.Accent;
            angleText.text = Loc.T("spin.angle", elevation.ToString("0")) + (kind.Length > 0 ? "  ·  " + kind : string.Empty);
            sketchCue.localEulerAngles = new Vector3(0f, 0f, -elevation);
        }

        private void Update()
        {
            // Keys / other inputs may still change the values while open.
            Refresh();
        }
    }
}
