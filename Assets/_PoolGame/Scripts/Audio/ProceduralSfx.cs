using UnityEngine;

namespace VTG.Pool.Audio
{
    /// <summary>Comic sound effects synthesised at runtime (no imported audio needed). Clips are built once and cached.</summary>
    public static class ProceduralSfx
    {
        private const int Rate = 22050;

        private static AudioClip crickets;
        private static AudioClip sadTrombone;
        private static AudioClip ghost;
        private static AudioClip boing;

        /// <summary>Two bursts of cricket chirps ("... cri cri ...").</summary>
        public static AudioClip Crickets => crickets != null ? crickets : (crickets = Build("Crickets", 1.6f, CricketSample));

        /// <summary>"Wah wah wah waaah" — four falling, wobbling brass notes.</summary>
        public static AudioClip SadTrombone => sadTrombone != null ? sadTrombone : (sadTrombone = Build("SadTrombone", 2.3f, TromboneSample));

        /// <summary>Rising, wavering "woooo" for the cue ball's ghost.</summary>
        public static AudioClip Ghost => ghost != null ? ghost : (ghost = Build("Ghost", 1.4f, GhostSample));

        /// <summary>Springy "boing" for a ball hanging on the pocket lip.</summary>
        public static AudioClip Boing => boing != null ? boing : (boing = Build("Boing", 0.6f, BoingSample));

        private static AudioClip Build(string name, float seconds, System.Func<float, float> sample)
        {
            int count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                data[i] = Mathf.Clamp(sample(i / (float)Rate), -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(name, count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float CricketSample(float t)
        {
            // Each chirp: a 4.6 kHz tone gated by a fast 30 Hz pulse train; three chirps per burst, two bursts.
            float burst = t % 0.8f;
            if (burst > 0.42f)
            {
                return 0f;
            }

            float chirp = burst % 0.14f;
            if (chirp > 0.09f)
            {
                return 0f;
            }

            float gate = Mathf.Clamp01(Mathf.Sin(chirp * Mathf.PI * 2f * 30f) * 3f);
            float envelope = Mathf.Sin(chirp / 0.09f * Mathf.PI);
            return Mathf.Sin(t * Mathf.PI * 2f * 4600f) * gate * envelope * 0.35f;
        }

        private static float TromboneSample(float t)
        {
            // Notes: D4, C#4, C4, then a long, wobbling B3.
            float[] notes = { 293.66f, 277.18f, 261.63f, 246.94f };
            float[] starts = { 0f, 0.42f, 0.84f, 1.26f };
            int index = t < starts[1] ? 0 : t < starts[2] ? 1 : t < starts[3] ? 2 : 3;
            float local = t - starts[index];
            float length = index == 3 ? 1.0f : 0.4f;
            if (local > length)
            {
                return 0f;
            }

            float vibrato = index == 3 ? Mathf.Sin(local * Mathf.PI * 2f * 5.5f) * 0.03f * Mathf.Clamp01(local / 0.2f) : 0f;
            float bend = index == 3 ? -0.04f * Mathf.Clamp01(local / length) : 0f;
            float frequency = notes[index] * (1f + vibrato + bend);
            float phase = t * frequency;
            // Brassy: a few harmonics with falling weights.
            float tone = Mathf.Sin(phase * Mathf.PI * 2f) + 0.5f * Mathf.Sin(phase * Mathf.PI * 4f) + 0.3f * Mathf.Sin(phase * Mathf.PI * 6f) + 0.15f * Mathf.Sin(phase * Mathf.PI * 8f);
            float attack = Mathf.Clamp01(local / 0.04f);
            float release = Mathf.Clamp01((length - local) / 0.08f);
            float wah = 0.6f + 0.4f * Mathf.Sin(Mathf.Clamp01(local / length) * Mathf.PI);
            return tone * attack * release * wah * 0.22f;
        }

        private static float GhostSample(float t)
        {
            // Linear glide 380 -> 720 Hz (phase in closed form) with a slow waver.
            float phase = 380f * t + (720f - 380f) * t * t / (2f * 1.4f) + 2f * Mathf.Sin(t * Mathf.PI * 2f * 6f);
            float envelope = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((1.4f - t) / 0.4f);
            float tone = Mathf.Sin(phase * Mathf.PI * 2f) * 0.8f + 0.2f * Mathf.Sin(phase * Mathf.PI * 4f);
            return tone * envelope * 0.28f;
        }

        private static float BoingSample(float t)
        {
            // Spring: pitch wobbling around a rising centre, quick decay.
            float centre = Mathf.Lerp(180f, 420f, t / 0.6f);
            float phase = centre * t + 18f * Mathf.Sin(t * Mathf.PI * 2f * 14f) / 14f;
            float envelope = Mathf.Exp(-t * 5f) * Mathf.Clamp01(t / 0.01f);
            return Mathf.Sin(phase * Mathf.PI * 2f) * envelope * 0.4f;
        }
    }
}
