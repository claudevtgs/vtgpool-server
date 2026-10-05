namespace VTG.Pool.Balls
{
    /// <summary>Physical motion state of a ball on the table.</summary>
    public enum BallState
    {
        /// <summary>At rest: no translation and no spin.</summary>
        Stationary,

        /// <summary>Contact point slips on the cloth (kinetic friction acts).</summary>
        Sliding,

        /// <summary>Pure rolling: v = w x r at the cloth contact.</summary>
        Rolling,

        /// <summary>No translation but spinning about the vertical axis.</summary>
        Spinning,

        /// <summary>Not supported by the cloth (jumping or dropping into a pocket).</summary>
        Airborne,

        /// <summary>Captured by a pocket; no longer simulated.</summary>
        Pocketed
    }
}
