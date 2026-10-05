using System;
using UnityEngine;

namespace VTG.Pool.Cue
{
    /// <summary>
    /// Visual cue: align -> draw back -> accelerate -> contact -> retract.
    /// The cue has no collider; at the contact moment it calls back so ShotController applies the
    /// physics impulse (MASTER_PROMPT section 8).
    /// </summary>
    public sealed class CueController : MonoBehaviour
    {
        private enum StrokePhase
        {
            Idle,
            Stroking,
            FollowThrough
        }

        [SerializeField, Tooltip("Child transform holding the cue mesh; its local +Z points from butt to tip, tip at the origin.")]
        private Transform cueVisual;

        [SerializeField] private float tipGap = 0.004f;
        [SerializeField] private float minDrawBack = 0.03f;
        [SerializeField] private float maxDrawBack = 0.28f;
        [SerializeField, Tooltip("Minimum visual cue elevation (deg) so a level cue clears the rail; the shot's own elevation is used when higher.")]
        private float visualElevation = 3f;
        [SerializeField] private float minStrokeDuration = 0.05f;
        [SerializeField] private float maxStrokeDuration = 0.35f;
        [SerializeField] private float followThroughDistance = 0.06f;
        [SerializeField] private float followThroughDuration = 0.25f;

        private StrokePhase phase;
        private float strokeTime;
        private float strokeDuration;
        private float strokeStartOffset;
        private Action contactCallback;
        private Vector3 ballCenter;
        private Vector3 aimDirection = Vector3.forward;
        private Vector2 tipOffset;
        private float elevation;
        private float ballRadius = 0.028575f;

        public bool IsStroking => phase == StrokePhase.Stroking;

        public bool Visible
        {
            get => cueVisual != null && cueVisual.gameObject.activeSelf;
            set
            {
                if (cueVisual != null && cueVisual.gameObject.activeSelf != value)
                {
                    cueVisual.gameObject.SetActive(value);
                }
            }
        }

        /// <summary>Positions the cue behind the ball. <paramref name="offset"/> is the tip offset in fractions of R; draw-back is 0..1.</summary>
        public void Align(Vector3 center, float radius, Vector3 direction, Vector2 offset, float drawBack01, float elevationDegrees = 0f)
        {
            if (phase != StrokePhase.Idle)
            {
                return;
            }

            elevation = elevationDegrees;
            SetPose(center, radius, direction, offset, Mathf.Lerp(minDrawBack, maxDrawBack, Mathf.Clamp01(drawBack01)));
        }

        /// <summary>Plays the forward stroke; <paramref name="onContact"/> fires when the tip reaches the ball.</summary>
        public void PlayStroke(float power01, Action onContact)
        {
            contactCallback = onContact;
            strokeStartOffset = Mathf.Lerp(minDrawBack, maxDrawBack, Mathf.Clamp01(power01));
            strokeDuration = Mathf.Lerp(maxStrokeDuration, minStrokeDuration, Mathf.Clamp01(power01));
            strokeTime = 0f;
            phase = StrokePhase.Stroking;
        }

        public void CancelStroke()
        {
            phase = StrokePhase.Idle;
            contactCallback = null;
        }

        private void Update()
        {
            if (phase == StrokePhase.Idle)
            {
                return;
            }

            strokeTime += Time.deltaTime;
            if (phase == StrokePhase.Stroking)
            {
                float t = Mathf.Clamp01(strokeTime / strokeDuration);
                float eased = t * t; // accelerating stroke
                SetPose(ballCenter, ballRadius, aimDirection, tipOffset, Mathf.Lerp(strokeStartOffset, 0f, eased));
                if (t >= 1f)
                {
                    phase = StrokePhase.FollowThrough;
                    strokeTime = 0f;
                    Action callback = contactCallback;
                    contactCallback = null;
                    callback?.Invoke();
                }
            }
            else
            {
                float t = Mathf.Clamp01(strokeTime / followThroughDuration);
                float offset = -followThroughDistance * Mathf.Sin(t * Mathf.PI * 0.5f);
                SetPose(ballCenter, ballRadius, aimDirection, tipOffset, offset);
                if (t >= 1f)
                {
                    phase = StrokePhase.Idle;
                }
            }
        }

        private void SetPose(Vector3 center, float radius, Vector3 direction, Vector2 offset, float drawBack)
        {
            ballCenter = center;
            ballRadius = radius;
            direction.y = 0f;
            aimDirection = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.forward;
            tipOffset = offset;

            if (cueVisual == null)
            {
                return;
            }

            // Same frame as the physics (CueStrikeModel), so the tip meets the ball where the impulse is applied.
            float shown = Mathf.Max(visualElevation, elevation);
            CueStrikeModel.CueFrame(aimDirection, shown, out Vector3 cueForward, out Vector3 right, out Vector3 up);
            Vector3 contact = center + CueStrikeModel.ContactPoint(cueForward, right, up, offset, radius);
            Quaternion rotation = Quaternion.LookRotation(cueForward, up);
            cueVisual.SetPositionAndRotation(contact - cueForward * (tipGap + drawBack), rotation);
        }
    }
}
