using UnityEngine;

namespace VTG.Pool.Table
{
    /// <summary>
    /// Table dimensions and pocket geometry (SI units). Defaults: 9 ft WPA table.
    /// The playing area is measured between cushion noses; the table is centred on its transform
    /// with the long axis along Z (head rail at -Z, foot rail at +Z).
    /// </summary>
    [CreateAssetMenu(menuName = "VTG Pool/Table Geometry", fileName = "TableGeometry_9ft")]
    public sealed class TableGeometry : ScriptableObject
    {
        [Header("Playing area")]
        public float playLength = 2.54f;
        public float playWidth = 1.27f;

        [Tooltip("Height of the slate/cloth surface above the floor.")]
        public float surfaceHeight = 0.76f;

        public float bedThickness = 0.04f;

        [Header("Cushions")]
        [Tooltip("Depth of the cushion rubber from nose to rail (m).")]
        public float cushionDepth = 0.05f;

        [Tooltip("Collider height above the cloth. The collision face is vertical; the nose height below drives the model.")]
        public float cushionColliderHeight = 0.048f;

        [Tooltip("Nose contact height above the cloth (WPA ~63.5% of ball diameter).")]
        public float cushionNoseHeight = 0.0365f;

        [Header("Rails (wood)")]
        public float railWidth = 0.13f;
        public float railHeight = 0.056f;

        [Header("Corner pockets")]
        [Tooltip("Mouth width between the jaw points.")]
        public float cornerMouth = 0.116f;

        [Tooltip("Interior angle between the cushion nose and the jaw face (deg).")]
        public float cornerJawAngle = 142f;

        public float cornerHoleRadius = 0.068f;

        [Tooltip("Hole centre offset outward from the playing-area corner along each axis (shelf).")]
        public float cornerHoleOffset = 0.02f;

        [Header("Side pockets")]
        public float sideMouth = 0.13f;
        public float sideJawAngle = 104f;
        public float sideHoleRadius = 0.065f;

        [Tooltip("Hole centre offset outward from the long cushion nose line.")]
        public float sideHoleOffset = 0.045f;

        [Header("Pocket liner")]
        public float linerDepth = 0.2f;
        public float linerThickness = 0.012f;

        [Tooltip("Drop of the liner top below the cloth (forms the hole lip).")]
        public float linerTopDrop = 0.015f;

        [Header("Surface response")]
        [Range(0.1f, 1.2f)] public float jawRestitutionScale = 0.8f;
        [Range(0f, 3f)] public float jawFrictionScale = 1.2f;
        [Range(0.1f, 1.2f)] public float pocketBackRestitutionScale = 0.2f;
        [Range(0f, 3f)] public float pocketBackFrictionScale = 1.5f;

        [Header("Physics materials")]
        public PhysicsMaterial clothPhysicsMaterial;
        public PhysicsMaterial cushionPhysicsMaterial;

        [Header("Visual materials")]
        public Material clothMaterial;
        public Material cushionMaterial;
        public Material railMaterial;
        public Material pocketMaterial;

        public float HalfLength => playLength * 0.5f;

        public float HalfWidth => playWidth * 0.5f;

        /// <summary>Distance from the playing-area corner to each corner jaw point along the rail.</summary>
        public float CornerJawSetback => cornerMouth / Mathf.Sqrt(2f);

        private void OnValidate()
        {
            playLength = Mathf.Max(1f, playLength);
            playWidth = Mathf.Clamp(playWidth, 0.5f, playLength);
            cushionDepth = Mathf.Max(0.02f, cushionDepth);
            cushionColliderHeight = Mathf.Max(0.035f, cushionColliderHeight);
            cornerJawAngle = Mathf.Clamp(cornerJawAngle, 120f, 160f);
            sideJawAngle = Mathf.Clamp(sideJawAngle, 90f, 130f);
            cornerMouth = Mathf.Clamp(cornerMouth, 0.09f, 0.16f);
            sideMouth = Mathf.Clamp(sideMouth, 0.1f, 0.18f);
        }
    }
}
