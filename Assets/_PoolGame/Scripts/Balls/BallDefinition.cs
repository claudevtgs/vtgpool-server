using UnityEngine;

namespace VTG.Pool.Balls
{
    /// <summary>
    /// Identity and visual definition of a ball (number, category, colours).
    /// Physics tuning lives in BallPhysicsProfile, never here.
    /// </summary>
    [CreateAssetMenu(menuName = "VTG Pool/Ball Definition", fileName = "Ball_00")]
    public sealed class BallDefinition : ScriptableObject
    {
        [SerializeField, Range(0, 15)] private int ballNumber;
        [SerializeField] private BallType ballType = BallType.Solid;
        [SerializeField] private Color baseColor = Color.white;
        [SerializeField] private Material material;

        /// <summary>Ball number (0 = cue ball).</summary>
        public int BallNumber => ballNumber;

        public BallType BallType => ballType;

        public Color BaseColor => baseColor;

        public Material Material => material;

        public bool IsCueBall => ballType == BallType.Cue;

        public void Initialize(int number, Color color, Material mat)
        {
            ballNumber = number;
            ballType = StandardType(number);
            baseColor = color;
            material = mat;
        }

        private void OnValidate()
        {
            ballType = StandardType(ballNumber);
        }

        /// <summary>Standard colour for a numbered ball (1-7 solid, 9-15 stripe of the same colour).</summary>
        public static Color StandardColor(int number)
        {
            switch (number)
            {
                case 0: return new Color(0.96f, 0.95f, 0.90f);
                case 1: case 9: return new Color(0.98f, 0.78f, 0.10f);
                case 2: case 10: return new Color(0.07f, 0.22f, 0.70f);
                case 3: case 11: return new Color(0.82f, 0.10f, 0.08f);
                case 4: case 12: return new Color(0.36f, 0.12f, 0.55f);
                case 5: case 13: return new Color(0.95f, 0.42f, 0.06f);
                case 6: case 14: return new Color(0.05f, 0.45f, 0.20f);
                case 7: case 15: return new Color(0.48f, 0.12f, 0.08f);
                case 8: return new Color(0.04f, 0.04f, 0.04f);
                default: return Color.white;
            }
        }

        public static BallType StandardType(int number)
        {
            if (number == 0) return BallType.Cue;
            if (number == 8) return BallType.Eight;
            return number < 8 ? BallType.Solid : BallType.Stripe;
        }
    }
}
