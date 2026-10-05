using UnityEngine;

namespace VTG.Pool.Inputs
{
    /// <summary>
    /// Device-independent gameplay input, sampled once per frame. Gameplay code depends on this
    /// interface only (mouse, touch, gamepad, AI or network can implement it).
    /// </summary>
    public interface IShotInput
    {
        /// <summary>Aim rotation requested this frame, in degrees (positive = clockwise from above).</summary>
        float AimDeltaDegrees { get; }

        /// <summary>Camera orbit requested this frame (x = yaw degrees, y = pitch degrees).</summary>
        Vector2 LookDeltaDegrees { get; }

        /// <summary>Zoom requested this frame (positive = zoom in).</summary>
        float ZoomDelta { get; }

        /// <summary>Tip-offset change requested this frame, in unit-disc units.</summary>
        Vector2 SpinDelta { get; }

        bool ResetSpinPressed { get; }

        /// <summary>Cue elevation change requested this frame, in degrees (positive = raise the butt).</summary>
        float ElevationDelta { get; }

        bool PrecisionHeld { get; }

        bool PowerPressed { get; }

        bool PowerHeld { get; }

        bool PowerReleased { get; }

        /// <summary>
        /// Mouse cue stroke while the power key is held: pointer movement this frame in screen heights
        /// (positive = pushed forward / up, negative = pulled back). Zero for devices without a mouse.
        /// </summary>
        float StrokeDelta { get; }

        /// <summary>The power key is held on a keyboard + mouse (a mouse stroke is possible).</summary>
        bool StrokeAvailable { get; }

        /// <summary>Analog power (0..1) if the device provides one (e.g. trigger); negative if not.</summary>
        float AnalogPower { get; }

        bool ShootPressed { get; }

        bool CancelPressed { get; }

        bool PausePressed { get; }

        bool CameraCuePressed { get; }

        bool CameraTacticalPressed { get; }

        bool CameraTopPressed { get; }

        /// <summary>Pointer position in screen pixels (mouse / primary touch).</summary>
        Vector2 PointerPosition { get; }

        /// <summary>True if the pointer moved this frame (and is not over UI).</summary>
        bool PointerMoved { get; }

        /// <summary>Confirm cue-ball placement (ball in hand).</summary>
        bool PlacePressed { get; }

        /// <summary>Pick the cue ball up again while ball in hand is available.</summary>
        bool BallInHandPressed { get; }

        /// <summary>Directional move axis (-1..1, per second) for placement with keys / stick.</summary>
        Vector2 MoveAxis { get; }

        /// <summary>True while the pointer is over interactive UI (gameplay pointer input is suppressed).</summary>
        bool PointerOverUI { get; }

        /// <summary>Primary pointer (mouse left button / first finger) went down this frame outside UI.</summary>
        bool PointerPressed { get; }

        /// <summary>Primary pointer is held, and the press started outside UI.</summary>
        bool PointerHeld { get; }

        /// <summary>Primary pointer was released this frame (the press started outside UI).</summary>
        bool PointerReleased { get; }
    }
}
