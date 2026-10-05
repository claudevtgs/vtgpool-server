using UnityEngine;
using VTG.Pool.Simulation;

namespace VTG.Pool.Table
{
    /// <summary>What part of the table a cushion collider represents.</summary>
    public enum CushionSurfaceKind
    {
        Rail,
        Jaw,
        PocketBack
    }

    /// <summary>
    /// Marks a collider as a cushion-like surface whose impacts are resolved by
    /// <see cref="CushionResponseModel"/> instead of PhysX restitution/friction.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CushionSurface : MonoBehaviour
    {
        [SerializeField] private CushionSurfaceKind kind = CushionSurfaceKind.Rail;

        [Tooltip("Multiplier on the profile's cushion restitution (jaws and pocket backs absorb more).")]
        [SerializeField, Range(0.1f, 1.2f)] private float restitutionScale = 1f;

        [Tooltip("Multiplier on the profile's cushion friction.")]
        [SerializeField, Range(0f, 3f)] private float frictionScale = 1f;

        [Tooltip("Height of the cushion nose contact above the cloth (m). WPA: ~63.5% of ball diameter.")]
        [SerializeField] private float contactHeightAboveCloth = 0.0365f;

        public CushionSurfaceKind Kind => kind;

        public void Configure(CushionSurfaceKind newKind, float newRestitutionScale, float newFrictionScale, float newContactHeight)
        {
            kind = newKind;
            restitutionScale = newRestitutionScale;
            frictionScale = newFrictionScale;
            contactHeightAboveCloth = newContactHeight;
        }

        public CushionParameters BuildParameters(BallPhysicsProfile profile)
        {
            float heightAboveCentre = contactHeightAboveCloth - profile.ballRadius;
            return profile.ToCushionParameters(heightAboveCentre, restitutionScale, frictionScale);
        }

        private void OnValidate()
        {
            contactHeightAboveCloth = Mathf.Clamp(contactHeightAboveCloth, 0.01f, 0.06f);
        }
    }
}
