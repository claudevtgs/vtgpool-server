namespace VTG.Pool.Core
{
    /// <summary>Explicit shot/match flow states (MASTER_PROMPT section 18).</summary>
    public enum ShotPhase
    {
        Preparing,
        Aiming,
        PowerSelection,
        Shooting,
        BallsMoving,
        ResolvingShot,
        TurnTransition,
        GameOver
    }
}
