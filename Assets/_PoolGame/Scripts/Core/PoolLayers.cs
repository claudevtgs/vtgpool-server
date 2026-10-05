using UnityEngine;

namespace VTG.Pool.Core
{
    /// <summary>
    /// Physics layer names and cached indices. Layers are created by the editor setup
    /// (VTG Pool/Setup/Apply Project Settings). Missing layers fall back to Default.
    /// </summary>
    public static class PoolLayers
    {
        public const string BallName = "Ball";
        public const string CueBallName = "CueBall";
        public const string CushionName = "Cushion";
        public const string TableName = "Table";
        public const string PocketName = "Pocket";
        public const string CueName = "Cue";
        public const string AimHelperName = "AimHelper";
        public const string EnvironmentName = "Environment";
        public const string UI3DName = "UI3D";

        /// <summary>All custom layers in the order they are allocated (starting at user layer 6).</summary>
        public static readonly string[] All =
        {
            BallName, CueBallName, CushionName, TableName, PocketName, CueName, AimHelperName, EnvironmentName, UI3DName
        };

        /// <summary>First user layer index used for <see cref="All"/>.</summary>
        public const int FirstLayerIndex = 6;

        public static int Ball => Get(BallName);
        public static int CueBall => Get(CueBallName);
        public static int Cushion => Get(CushionName);
        public static int Table => Get(TableName);
        public static int Pocket => Get(PocketName);
        public static int Cue => Get(CueName);
        public static int AimHelper => Get(AimHelperName);
        public static int Environment => Get(EnvironmentName);

        /// <summary>Mask used by aim prediction: object balls and cushions.</summary>
        public static int AimPredictionMask => (1 << Ball) | (1 << Cushion);

        public static int Get(string layerName)
        {
            int index = LayerMask.NameToLayer(layerName);
            return index < 0 ? 0 : index;
        }

        public static bool AreConfigured()
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (LayerMask.NameToLayer(All[i]) < 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
