using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using VTG.Pool.Balls;
using VTG.Pool.Cue;
using VTG.Pool.Match;
using VTG.Pool.Simulation;

namespace VTG.Pool.Diagnostics
{
    /// <summary>
    /// Development-only overlay: FPS, physics Hz, shot phase, power, spin, cue-ball state and
    /// velocities, first contact and contact counts. Includes a minimal power bar and spin indicator
    /// for Milestone 1 (the real HUD is Milestone 2/5). Toggle with F1. Disabled in release builds.
    /// </summary>
    public sealed class PoolDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool visible = true;
        [SerializeField] private ShotController shotController;
        [SerializeField] private CueBallSpinController spinController;
        [SerializeField] private PoolPhysicsSystem physicsSystem;
        [SerializeField] private ShotTracker shotTracker;

        private readonly StringBuilder builder = new StringBuilder(1024);
        private float smoothedDeltaTime = 1f / 60f;
        private GUIStyle labelStyle;
        private Texture2D whiteTexture;

        private void Awake()
        {
            if (!Debug.isDebugBuild)
            {
                enabled = false;
            }
        }

        private void Update()
        {
            smoothedDeltaTime = Mathf.Lerp(smoothedDeltaTime, Time.unscaledDeltaTime, 0.05f);
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            {
                visible = !visible;
            }
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
                whiteTexture = Texture2D.whiteTexture;
            }

            BuildText();
            float width = 360f;
            Rect panel = new Rect(Screen.width - width - 10f, 10f, width, 330f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(panel, whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x + 8f, panel.y + 6f, panel.width - 16f, panel.height - 12f), builder.ToString(), labelStyle);

            DrawPowerBar();
            DrawSpinIndicator();
        }

        private void BuildText()
        {
            builder.Clear();
            builder.Append("<b>VTG Pool 3D — Physics Prototype</b>\n");
            builder.Append("FPS: ").Append((1f / Mathf.Max(1e-4f, smoothedDeltaTime)).ToString("F0"));
            builder.Append("   Physics: ").Append((1f / Time.fixedDeltaTime).ToString("F0")).Append(" Hz\n");

            if (shotController != null)
            {
                builder.Append("Phase: ").Append(shotController.Phase);
                builder.Append("   Power: ").Append((shotController.Phase == Core.ShotPhase.PowerSelection
                    ? shotController.CurrentPower
                    : shotController.LastPower).ToString("P0")).Append('\n');
            }

            if (spinController != null)
            {
                builder.Append("Tip offset: ").Append(spinController.TipOffset.ToString("F2")).Append('\n');
            }

            if (physicsSystem != null)
            {
                builder.Append("Moving balls: ").Append(physicsSystem.MovingBallCount).Append('\n');
            }

            PoolBall cue = shotController != null ? shotController.CueBall : null;
            if (cue != null)
            {
                Vector3 v = cue.LinearVelocity;
                Vector3 w = cue.AngularVelocity;
                builder.Append("Cue ball: ").Append(cue.CurrentState).Append('\n');
                builder.Append("  |v| ").Append(new Vector2(v.x, v.z).magnitude.ToString("F3")).Append(" m/s  v=").Append(v.ToString("F2")).Append('\n');
                builder.Append("  |w| ").Append(w.magnitude.ToString("F1")).Append(" rad/s  w=").Append(w.ToString("F1")).Append('\n');
                Vector3 slip = ClothFrictionModel.ContactSlipVelocity(v, w, cue.Radius);
                builder.Append("  slip ").Append(slip.magnitude.ToString("F3")).Append(" m/s\n");
            }

            ShotRecord shot = shotTracker != null ? (shotTracker.CurrentShot ?? shotTracker.LastCompletedShot) : null;
            if (shot != null)
            {
                builder.Append("First contact: ").Append(shot.FirstObjectBallHit < 0 ? "-" : shot.FirstObjectBallHit.ToString());
                builder.Append("   Ball contacts: ").Append(shotTracker.BallContactsThisShot).Append('\n');
                builder.Append("Cushions: ").Append(shot.CushionHits).Append("   Pocketed: ").Append(shot.BallsPocketed.Count);
                if (shot.CueBallPocketed)
                {
                    builder.Append("  <color=#ff6060>SCRATCH</color>");
                }

                builder.Append('\n');
            }

            builder.Append("\n<size=11>LMB-drag/A-D aim · Shift fine · Space hold=power · Enter=repeat power\n");
            builder.Append("Arrows spin · C centre · RMB orbit · wheel zoom · 1/2/3 cameras\n");
            builder.Append("F1 overlay · F2 vectors · F3..F12 / panel presets · R reset · P pause</size>");
        }

        private void DrawPowerBar()
        {
            if (shotController == null)
            {
                return;
            }

            float power = shotController.Phase == Core.ShotPhase.PowerSelection ? shotController.CurrentPower : shotController.LastPower;
            Rect back = new Rect(20f, Screen.height - 50f, 300f, 22f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(back, whiteTexture);
            GUI.color = Color.Lerp(new Color(0.3f, 0.9f, 0.3f), new Color(1f, 0.25f, 0.2f), power);
            GUI.DrawTexture(new Rect(back.x + 2f, back.y + 2f, (back.width - 4f) * power, back.height - 4f), whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(back.x, back.y - 20f, 200f, 20f), "Power", labelStyle);
        }

        private void DrawSpinIndicator()
        {
            if (spinController == null)
            {
                return;
            }

            const float size = 90f;
            Rect area = new Rect(Screen.width - size - 20f, Screen.height - size - 20f, size, size);
            GUI.color = new Color(0.95f, 0.95f, 0.9f, 0.9f);
            GUI.DrawTexture(area, whiteTexture);
            Vector2 offset = spinController.TipOffset;
            Vector2 center = area.center + new Vector2(offset.x, -offset.y) * (size * 0.5f);
            GUI.color = new Color(0.85f, 0.1f, 0.1f);
            GUI.DrawTexture(new Rect(center.x - 5f, center.y - 5f, 10f, 10f), whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(area.x, area.y - 20f, 120f, 20f), "Spin", labelStyle);
        }
    }
}
