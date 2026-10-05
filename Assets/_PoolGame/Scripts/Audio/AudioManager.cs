using System.Collections.Generic;
using UnityEngine;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Match;
using VTG.Pool.Rules;
using VTG.Pool.Simulation;
using VTG.Pool.Table;

namespace VTG.Pool.Audio
{
    /// <summary>
    /// Event-driven game audio: ball/cushion/pocket/cue sounds scaled by impact strength with pitch variation,
    /// a rolling loop driven by ball speeds, foul/win/callout stings, ambience and music. Uses a fixed voice
    /// pool (no per-hit allocation) and limits voices per frame so the break does not saturate the mix.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const int VoiceCount = 12;
        private const int MaxImpactsPerFrame = 4;

        [SerializeField] private AudioLibrary library;
        [SerializeField] private PoolPhysicsSystem physicsSystem;

        private readonly List<AudioSource> voices = new List<AudioSource>(VoiceCount);
        private AudioSource rollSource;
        private AudioSource ambienceSource;
        private AudioSource musicSource;
        private int nextVoice;
        private int impactsThisFrame;
        private bool breakSoundPlayed;
        private bool shotIsBreak;
        private float masterVolume = 1f;
        private float effectsVolume = 0.9f;
        private float musicVolume = 0.18f;
        private float ambienceVolume = 0.25f;

        public AudioLibrary Library => library;

        private void Awake()
        {
            if (library == null)
            {
                enabled = false;
                return;
            }

            masterVolume = library.masterVolume;
            effectsVolume = library.effectsVolume;
            musicVolume = library.musicVolume;
            ambienceVolume = library.ambienceVolume;

            for (int i = 0; i < VoiceCount; i++)
            {
                voices.Add(CreateSource($"Voice_{i}", false));
            }

            rollSource = CreateSource("Roll", true);
            ambienceSource = CreateSource("Ambience", true);
            musicSource = CreateSource("Music", true);
        }

        private void Start()
        {
            PlayLoop(ambienceSource, library.ambience, ambienceVolume);
            PlayLoop(musicSource, library.music, musicVolume);
            PlayLoop(rollSource, library.rollLoop, 0f);
        }

        private void OnEnable()
        {
            PoolEvents.BallHit += HandleBallHit;
            PoolEvents.CushionHit += HandleCushionHit;
            PoolEvents.BallPocketed += HandlePocketed;
            PoolEvents.ShotStarted += HandleShotStarted;
            PoolEvents.FoulCommitted += HandleFoul;
            PoolEvents.GameWon += HandleGameWon;
        }

        private void OnDisable()
        {
            PoolEvents.BallHit -= HandleBallHit;
            PoolEvents.CushionHit -= HandleCushionHit;
            PoolEvents.BallPocketed -= HandlePocketed;
            PoolEvents.ShotStarted -= HandleShotStarted;
            PoolEvents.FoulCommitted -= HandleFoul;
            PoolEvents.GameWon -= HandleGameWon;
        }

        private void LateUpdate()
        {
            impactsThisFrame = 0;
            UpdateRolling();
        }

        // ------------------------------------------------------------------ public API

        /// <summary>Sets the mix (0..1 each) from player settings; loops update immediately.</summary>
        public void SetVolumes(float master, float effects, float music, float ambience)
        {
            masterVolume = Mathf.Clamp01(master);
            effectsVolume = Mathf.Clamp01(effects);
            musicVolume = Mathf.Clamp01(music);
            ambienceVolume = Mathf.Clamp01(ambience);
            if (musicSource != null) musicSource.volume = musicVolume * masterVolume;
            if (ambienceSource != null) ambienceSource.volume = ambienceVolume * masterVolume;
        }

        public void PlayUiClick() => PlayOneShot(library.uiClick, 0.6f, 0f);

        public void PlayCallout() => PlayOneShot(library.callout, 0.55f, 0f);

        /// <summary>Comic low "wah" for lucky shots (the callout sound pitched down).</summary>
        public void PlayTroll() => PlayOneShot(library.callout, 0.7f, 0f, 0.55f);

        /// <summary>Low buzzer for fouls.</summary>
        public void PlayFoul() => PlayOneShot(library.callout, 0.8f, 0f, 0.42f);

        /// <summary>Soft "aww" for a missed shot.</summary>
        public void PlayMiss() => PlayOneShot(library.callout, 0.35f, 0f, 0.72f);

        /// <summary>Bright fanfare hit for multi-ball pots and the deciding ball (the callout sound pitched up).</summary>
        public void PlayFanfare(float pitch = 1.25f) => PlayOneShot(library.callout, 0.7f, 0f, pitch);

        /// <summary>Plays any clip as a UI / comic effect (no panning).</summary>
        public void PlayClip(AudioClip clip, float volume = 0.7f, float pitch = 1f) => PlayOneShot(clip, volume, 0f, pitch);

        public void PlayChalk() => PlayOneShot(library.chalk, 0.6f, 0f);

        // ------------------------------------------------------------------ events

        private void HandleShotStarted(ShotRecord record)
        {
            shotIsBreak = record.IsBreakShot;
            breakSoundPlayed = false;
            float speed = record.InitialCueVelocity.magnitude;
            float strength = Mathf.Clamp01(speed / 8f);
            AudioClip clip = strength > 0.55f && library.cueStrikeHard != null ? library.cueStrikeHard : library.cueStrikeSoft;
            PlayOneShot(clip, Mathf.Lerp(0.35f, 1f, strength), Pan(record.CueBallPosition));
        }

        private void HandleBallHit(BallHitInfo info)
        {
            if (!AllowImpact())
            {
                return;
            }

            BallPhysicsProfile profile = physicsSystem != null ? physicsSystem.Profile : null;
            float mass = profile != null ? profile.ballMass : PoolConstants.BallMass;
            float restitution = profile != null ? profile.ballRestitution : 0.93f;
            float relativeSpeed = info.Impulse / (mass * 0.5f * (1f + restitution));
            float strength = Mathf.Clamp01(relativeSpeed / library.fullVolumeImpactSpeed);

            if (shotIsBreak && !breakSoundPlayed && (info.A.IsCueBall || info.B.IsCueBall) && library.breakClatter != null && strength > 0.6f)
            {
                breakSoundPlayed = true;
                PlayOneShot(library.breakClatter, 0.9f, Pan(info.Point));
                return;
            }

            AudioClip clip = Pick(library.ballClicks, strength);
            PlayOneShot(clip, Mathf.Pow(strength, 0.6f) * 0.9f, Pan(info.Point));
        }

        private void HandleCushionHit(CushionHitInfo info)
        {
            if (!AllowImpact())
            {
                return;
            }

            float strength = Mathf.Clamp01(info.NormalSpeed / 3f);
            AudioClip clip = Pick(library.cushionHits, strength);
            PlayOneShot(clip, Mathf.Pow(strength, 0.7f) * 0.8f, Pan(info.Point));
        }

        private void HandlePocketed(PoolBall ball, Pocket pocket)
        {
            if (pocket == null)
            {
                return;
            }

            AudioClip clip = Pick(library.pocketDrops, Random.value);
            PlayOneShot(clip, 0.75f, Pan(pocket.Center));
        }

        private void HandleFoul(int playerIndex, FoulType foul, string message) => PlayOneShot(library.foul, 0.5f, 0f);

        private void HandleGameWon(int winnerIndex, string message) => PlayOneShot(library.win, 0.8f, 0f);

        // ------------------------------------------------------------------ helpers

        private void UpdateRolling()
        {
            if (rollSource == null || rollSource.clip == null)
            {
                return;
            }

            float total = 0f;
            IReadOnlyList<PoolBall> balls = BallRegistry.Balls;
            for (int i = 0; i < balls.Count; i++)
            {
                PoolBall ball = balls[i];
                if (ball.CurrentState == BallState.Rolling || ball.CurrentState == BallState.Sliding)
                {
                    Vector3 v = ball.LinearVelocity;
                    total += Mathf.Sqrt(v.x * v.x + v.z * v.z);
                }
            }

            float target = Mathf.Clamp01(total / 3f) * 0.35f * effectsVolume * masterVolume;
            rollSource.volume = Mathf.MoveTowards(rollSource.volume, target, Time.deltaTime * 1.5f);
            rollSource.pitch = Mathf.Lerp(0.85f, 1.15f, Mathf.Clamp01(total / 4f));
        }

        private bool AllowImpact()
        {
            if (impactsThisFrame >= MaxImpactsPerFrame)
            {
                return false;
            }

            impactsThisFrame++;
            return true;
        }

        private static AudioClip Pick(AudioClip[] clips, float strength)
        {
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            int index = Mathf.Clamp(Mathf.FloorToInt(strength * clips.Length), 0, clips.Length - 1);
            return clips[index] != null ? clips[index] : clips[0];
        }

        /// <summary>Stereo position from world X across the table (-1 left .. 1 right), kept subtle.</summary>
        private static float Pan(Vector3 position) => Mathf.Clamp(position.x / 1.5f, -1f, 1f) * 0.5f;

        private void PlayOneShot(AudioClip clip, float volume, float pan, float pitch = -1f)
        {
            if (clip == null || volume <= 0.01f)
            {
                return;
            }

            AudioSource voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Count;
            voice.Stop();
            voice.clip = clip;
            voice.volume = Mathf.Clamp01(volume) * effectsVolume * masterVolume;
            voice.pitch = pitch > 0f ? pitch : 1f + Random.Range(-library.pitchVariation, library.pitchVariation);
            voice.panStereo = pan;
            voice.Play();
        }

        private void PlayLoop(AudioSource source, AudioClip clip, float volume)
        {
            if (source == null || clip == null)
            {
                return;
            }

            source.clip = clip;
            source.volume = volume * masterVolume;
            source.Play();
        }

        private AudioSource CreateSource(string sourceName, bool loop)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            return source;
        }
    }
}
