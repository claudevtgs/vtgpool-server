namespace VTG.Pool.Aiming
{
    /// <summary>How much prediction is drawn while aiming.</summary>
    public enum AimAssistLevel
    {
        /// <summary>No guides.</summary>
        None,

        /// <summary>Cue direction line only.</summary>
        Minimal,

        /// <summary>Line, ghost ball and short object-ball line.</summary>
        Standard,

        /// <summary>Standard plus cue-ball tangent line and longer predictions.</summary>
        Training
    }
}
