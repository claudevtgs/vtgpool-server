using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VTG.Pool.Balls;
using VTG.Pool.Core;

namespace VTG.Pool.Diagnostics
{
    /// <summary>
    /// Development-only rendering of linear velocity (yellow) and angular velocity axis (cyan)
    /// for every ball, visible in the Game view. Toggle with F2. Disabled in release builds.
    /// LineRenderers are pooled and reused.
    /// </summary>
    public sealed class BallDebugDrawer : MonoBehaviour
    {
        [SerializeField] private bool visible = true;
        [SerializeField] private Material lineMaterial;
        [SerializeField, Tooltip("Metres drawn per m/s.")] private float velocityScale = 0.15f;
        [SerializeField, Tooltip("Metres drawn per rad/s.")] private float angularScale = 0.004f;
        [SerializeField] private float lineWidth = 0.003f;
        [SerializeField] private Color velocityColor = new Color(1f, 0.9f, 0.1f);
        [SerializeField] private Color angularColor = new Color(0.1f, 0.9f, 1f);
        [SerializeField] private Color slipColor = new Color(1f, 0.25f, 0.2f);

        private readonly List<LineRenderer> velocityLines = new List<LineRenderer>(16);
        private readonly List<LineRenderer> angularLines = new List<LineRenderer>(16);
        private readonly List<LineRenderer> slipLines = new List<LineRenderer>(16);

        public bool Visible
        {
            get => visible;
            set => visible = value;
        }

        private void Awake()
        {
            if (!Debug.isDebugBuild)
            {
                enabled = false;
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f2Key.wasPressedThisFrame)
            {
                visible = !visible;
            }
        }

        private void LateUpdate()
        {
            IReadOnlyList<PoolBall> balls = BallRegistry.Balls;
            EnsureCapacity(balls.Count);
            for (int i = 0; i < velocityLines.Count; i++)
            {
                bool active = visible && i < balls.Count && !balls[i].IsPocketed;
                velocityLines[i].enabled = active;
                angularLines[i].enabled = active;
                slipLines[i].enabled = active;
                if (!active)
                {
                    continue;
                }

                PoolBall ball = balls[i];
                Vector3 center = ball.transform.position;
                Vector3 linear = ball.LinearVelocity;
                Vector3 angular = ball.AngularVelocity;
                Vector3 lift = Vector3.up * (ball.Radius * 1.05f);
                Draw(velocityLines[i], center + lift, center + lift + linear * velocityScale);
                Draw(angularLines[i], center, center + angular * angularScale);

                // Slip of the cloth contact point (non-zero while sliding).
                Vector3 contact = center - Vector3.up * ball.Radius;
                Vector3 slip = linear + Vector3.Cross(angular, -Vector3.up * ball.Radius);
                slip.y = 0f;
                Draw(slipLines[i], contact, contact + slip * velocityScale);
            }
        }

        private static void Draw(LineRenderer line, Vector3 from, Vector3 to)
        {
            line.SetPosition(0, from);
            line.SetPosition(1, to);
        }

        private void EnsureCapacity(int count)
        {
            while (velocityLines.Count < count)
            {
                int index = velocityLines.Count;
                velocityLines.Add(CreateLine($"Velocity_{index}", velocityColor));
                angularLines.Add(CreateLine($"Angular_{index}", angularColor));
                slipLines.Add(CreateLine($"Slip_{index}", slipColor));
            }
        }

        private LineRenderer CreateLine(string lineName, Color color)
        {
            var lineObject = new GameObject(lineName);
            lineObject.layer = PoolLayers.AimHelper;
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth * 0.5f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = lineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.enabled = false;
            return line;
        }
    }
}
