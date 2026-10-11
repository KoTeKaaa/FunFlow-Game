using UnityEngine;

namespace FunFlow.Presentation
{
    /// <summary>
    /// Простые синтезированные звуки-заглушки, чтобы игра «звучала» без аудиофайлов.
    /// Как только в PresentationAudio назначен настоящий клип, заглушка не создаётся.
    /// </summary>
    internal static class ProceduralSfx
    {
        private const int Rate = 44100;

        private enum Wave { Sine, Square, Triangle, Saw }

        public static AudioClip Launch() => Sweep("sfx_launch", 0.2f, 380f, 140f, Wave.Triangle, 0.35f, 0.7f, 11);
        public static AudioClip Pickup() => Notes("sfx_pickup", new[] { 988f, 1319f }, 0.08f, Wave.Sine, 0.6f);
        public static AudioClip Impact() => Sweep("sfx_impact", 0.16f, 150f, 55f, Wave.Sine, 0.35f, 0.9f, 67);
        public static AudioClip Cut() => Sweep("sfx_cut", 0.25f, 260f, 90f, Wave.Saw, 0.5f, 0.6f, 23);
        public static AudioClip Shatter() => Sweep("sfx_shatter", 0.35f, 2400f, 1700f, Wave.Sine, 0.8f, 0.6f, 37);
        public static AudioClip Alarm() => Notes("sfx_alarm", new[] { 620f, 820f, 620f, 820f }, 0.14f, Wave.Square, 0.3f);
        public static AudioClip Tick() => Sweep("sfx_tick", 0.05f, 1200f, 1200f, Wave.Square, 0f, 0.25f, 41);
        public static AudioClip Win() => Notes("sfx_win", new[] { 523f, 659f, 784f, 1047f }, 0.12f, Wave.Triangle, 0.6f);
        public static AudioClip Lose() => Sweep("sfx_lose", 0.7f, 330f, 110f, Wave.Square, 0.05f, 0.3f, 53);

        private static AudioClip Sweep(string name, float duration, float f0, float f1, Wave wave, float noise, float volume, int seed)
        {
            int n = Mathf.CeilToInt(duration * Rate);
            var data = new float[n];
            var rng = new System.Random(seed);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float u = t / duration;
                phase += Mathf.Lerp(f0, f1, u) / Rate;
                float osc = Sample(wave, phase);
                float nz = (float)(rng.NextDouble() * 2.0 - 1.0);
                data[i] = (osc * (1f - noise) + nz * noise) * Envelope(t, duration) * volume;
            }
            return Create(name, data);
        }

        private static AudioClip Notes(string name, float[] freqs, float noteDuration, Wave wave, float volume)
        {
            int perNote = Mathf.CeilToInt(noteDuration * Rate);
            var data = new float[perNote * freqs.Length];
            for (int k = 0; k < freqs.Length; k++)
            {
                double phase = 0;
                for (int i = 0; i < perNote; i++)
                {
                    float t = i / (float)Rate;
                    phase += freqs[k] / Rate;
                    data[k * perNote + i] = Sample(wave, phase) * Envelope(t, noteDuration) * volume;
                }
            }
            return Create(name, data);
        }

        private static float Sample(Wave wave, double phase)
        {
            float p = (float)(phase - System.Math.Floor(phase));
            switch (wave)
            {
                case Wave.Square: return p < 0.5f ? 0.6f : -0.6f;
                case Wave.Triangle: return 4f * Mathf.Abs(p - 0.5f) - 1f;
                case Wave.Saw: return (2f * p - 1f) * 0.7f;
                default: return Mathf.Sin(2f * Mathf.PI * p);
            }
        }

        private static float Envelope(float t, float duration)
        {
            float attack = Mathf.Clamp01(t / 0.004f);
            float rest = Mathf.Clamp01(1f - t / duration);
            return attack * rest * rest;
        }

        private static AudioClip Create(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
