using UnityEngine;

namespace VTG.Pool.Audio
{
    /// <summary>All game sound clips and mix levels in one asset (MASTER_PROMPT section 26).</summary>
    [CreateAssetMenu(menuName = "VTG Pool/Audio Library", fileName = "AudioLibrary")]
    public sealed class AudioLibrary : ScriptableObject
    {
        [Header("Balls")]
        [Tooltip("Ordered soft -> hard; chosen by impact strength.")]
        public AudioClip[] ballClicks;
        public AudioClip breakClatter;
        public AudioClip rollLoop;

        [Header("Table")]
        public AudioClip[] cushionHits;
        public AudioClip[] pocketDrops;

        [Header("Cue")]
        public AudioClip cueStrikeSoft;
        public AudioClip cueStrikeHard;
        public AudioClip chalk;

        [Header("UI / match")]
        public AudioClip uiClick;
        public AudioClip foul;
        public AudioClip win;
        public AudioClip callout;

        [Header("Ambience")]
        public AudioClip ambience;
        public AudioClip music;

        [Header("Mix (0..1)")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float effectsVolume = 0.9f;
        [Range(0f, 1f)] public float ambienceVolume = 0.25f;
        [Range(0f, 1f)] public float musicVolume = 0.18f;

        [Header("Response")]
        [Tooltip("Relative impact speed (m/s) that plays at full volume.")]
        public float fullVolumeImpactSpeed = 4f;

        [Tooltip("Random pitch variation (+/-) to avoid repetition.")]
        [Range(0f, 0.2f)] public float pitchVariation = 0.06f;
    }
}
