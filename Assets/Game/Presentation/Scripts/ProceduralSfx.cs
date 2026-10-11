using System;
using UnityEngine;

namespace FunFlow.Presentation
{
    /// <summary>
    /// Синтезированные звуки без аудиофайлов. Если в PresentationAudio назначен настоящий клип,
    /// синтез для этого слота не используется.
    /// </summary>
    internal static class ProceduralSfx
    {
        internal const int Rate = 44100;

        private enum Wave { Sine, Triangle, Square, Saw }

        public static AudioClip Launch() => Create("sfx_launch", LaunchData());
        public static AudioClip Pickup() => Create("sfx_pickup", PickupData());
        public static AudioClip Impact() => Create("sfx_impact", ImpactData());
        public static AudioClip Cut() => Create("sfx_cut", CutData());
        public static AudioClip Shatter() => Create("sfx_shatter", ShatterData());
        public static AudioClip Alarm() => Create("sfx_alarm", AlarmData());
        public static AudioClip Tick() => Create("sfx_tick", TickData());
        public static AudioClip Win() => Create("sfx_win", WinData());
        public static AudioClip Lose() => Create("sfx_lose", LoseData());

        // ---------- Рецепты ----------

        /// <summary>«Вжух»: шум, у которого фильтр открывается и закрывается, плюс лёгкий тон вниз.</summary>
        internal static float[] LaunchData()
        {
            float[] b = Buffer(0.3f);
            var rng = new System.Random(11);
            float lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float u = i / (float)b.Length;
                float bell = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 1.5f);
                float cutoff = 0.02f + 0.15f * bell;
                lp += cutoff * (Noise(rng) - lp);
                b[i] = lp * bell * 2.5f;
            }
            Tone(b, 0f, 0.22f, 320f, 170f, 0.12f, 9f, Wave.Triangle);
            return Normalize(b, 0.6f);
        }

        /// <summary>«Дзынь-дзынь» монеты: две ноты с металлическими обертонами.</summary>
        internal static float[] PickupData()
        {
            float[] b = Buffer(0.4f);
            Bell(b, 0f, 1760f, 1f, 14f);
            Bell(b, 0.07f, 2349f, 0.9f, 10f);
            return Normalize(b, 0.7f);
        }

        /// <summary>Глухой «бум»: низкий тон с падением высоты и щелчок шума.</summary>
        internal static float[] ImpactData()
        {
            float[] b = Buffer(0.22f);
            Tone(b, 0f, 0.22f, 120f, 45f, 1f, 16f, Wave.Sine);
            NoiseBurst(b, 0f, 0.05f, 0.25f, 50f, 0.04f, false, 31);
            return Normalize(b, 0.9f);
        }

        /// <summary>Металлическое «вжик»: шипение и звенящие обертоны лезвия.</summary>
        internal static float[] CutData()
        {
            float[] b = Buffer(0.35f);
            NoiseBurst(b, 0f, 0.15f, 0.4f, 22f, 0.35f, true, 23);
            Tone(b, 0f, 0.35f, 2900f, 2850f, 0.35f, 11f, Wave.Sine);
            Tone(b, 0f, 0.3f, 4350f, 4300f, 0.2f, 14f, Wave.Sine);
            Tone(b, 0f, 0.12f, 1300f, 400f, 0.15f, 25f, Wave.Saw);
            return Normalize(b, 0.75f);
        }

        /// <summary>Разбитое стекло: удар шума и россыпь коротких высоких «звяков».</summary>
        internal static float[] ShatterData()
        {
            float[] b = Buffer(0.55f);
            NoiseBurst(b, 0f, 0.12f, 0.7f, 30f, 0.45f, true, 37);
            var rng = new System.Random(5);
            for (int k = 0; k < 16; k++)
            {
                float start = (float)rng.NextDouble() * 0.3f;
                float f = 2500f + (float)rng.NextDouble() * 3500f;
                float amp = 0.15f + (float)rng.NextDouble() * 0.2f;
                float decay = 25f + (float)rng.NextDouble() * 20f;
                Tone(b, start, 0.2f, f, f, amp, decay, Wave.Sine);
            }
            return Normalize(b, 0.75f);
        }

        /// <summary>Сирена: тон плавно «воет» вверх-вниз.</summary>
        internal static float[] AlarmData()
        {
            float[] b = Buffer(0.9f);
            double phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float f = 700f + 250f * Mathf.Sin(2f * Mathf.PI * 2.2f * t);
                phase += f / Rate;
                float s = 0.75f * Osc(Wave.Sine, phase) + 0.25f * Osc(Wave.Square, phase);
                b[i] = s * Fade(t, 0.9f, 0.02f, 0.03f);
            }
            return Normalize(b, 0.4f);
        }

        /// <summary>Щелчок секундомера.</summary>
        internal static float[] TickData()
        {
            float[] b = Buffer(0.06f);
            Tone(b, 0f, 0.06f, 1900f, 1900f, 0.8f, 70f, Wave.Sine);
            NoiseBurst(b, 0f, 0.02f, 0.3f, 150f, 0.5f, false, 41);
            return Normalize(b, 0.45f);
        }

        /// <summary>Победа: восходящее арпеджио, последняя нота звучит дольше.</summary>
        internal static float[] WinData()
        {
            float[] b = Buffer(0.8f);
            float[] notes = { 1047f, 1319f, 1568f, 2093f };
            for (int k = 0; k < notes.Length; k++)
            {
                bool last = k == notes.Length - 1;
                float start = k * 0.09f;
                float dur = last ? 0.8f - start : 0.25f;
                float decay = last ? 3.5f : 8f;
                Tone(b, start, dur, notes[k], notes[k], 0.8f, decay, Wave.Triangle);
                Tone(b, start, dur, notes[k] * 2f, notes[k] * 2f, 0.2f, decay * 1.5f, Wave.Sine);
            }
            return Normalize(b, 0.65f);
        }

        /// <summary>Проигрыш: три ноты вниз, последняя «сползает».</summary>
        internal static float[] LoseData()
        {
            float[] b = Buffer(0.85f);
            Tone(b, 0f, 0.22f, 392f, 392f, 0.7f, 4f, Wave.Triangle);
            Tone(b, 0.2f, 0.22f, 370f, 370f, 0.7f, 4f, Wave.Triangle);
            Tone(b, 0.4f, 0.45f, 349f, 290f, 0.8f, 3f, Wave.Triangle);
            return Normalize(b, 0.6f);
        }

        // ---------- Инструменты ----------

        private static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        /// <summary>Тон с линейным изменением частоты и экспоненциальным затуханием.</summary>
        private static void Tone(float[] b, float start, float dur, float f0, float f1, float amp, float decay, Wave wave)
        {
            int from = Mathf.FloorToInt(start * Rate);
            int count = Mathf.Min(Mathf.CeilToInt(dur * Rate), b.Length - from);
            double phase = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Rate;
                phase += Mathf.Lerp(f0, f1, t / dur) / Rate;
                b[from + i] += Osc(wave, phase) * amp * Mathf.Exp(-decay * t) * Fade(t, dur, 0.003f, 0.005f);
            }
        }

        /// <summary>Колокольчик: основной тон и негармоничные металлические обертоны.</summary>
        private static void Bell(float[] b, float start, float f, float amp, float decay)
        {
            Tone(b, start, 0.35f, f, f, amp, decay, Wave.Sine);
            Tone(b, start, 0.25f, f * 2.76f, f * 2.76f, amp * 0.35f, decay * 1.6f, Wave.Sine);
            Tone(b, start, 0.15f, f * 5.4f, f * 5.4f, amp * 0.15f, decay * 2.5f, Wave.Sine);
        }

        /// <summary>Всплеск шума через однополюсный фильтр: lowpass (тёмный) или highpass (шипящий).</summary>
        private static void NoiseBurst(float[] b, float start, float dur, float amp, float decay, float filter, bool highpass, int seed)
        {
            var rng = new System.Random(seed);
            int from = Mathf.FloorToInt(start * Rate);
            int count = Mathf.Min(Mathf.CeilToInt(dur * Rate), b.Length - from);
            float lp = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Rate;
                float x = Noise(rng);
                lp += filter * (x - lp);
                float y = highpass ? x - lp : lp * 2f;
                b[from + i] += y * amp * Mathf.Exp(-decay * t) * Fade(t, dur, 0.001f, 0.005f);
            }
        }

        private static float Osc(Wave wave, double phase)
        {
            float p = (float)(phase - Math.Floor(phase));
            switch (wave)
            {
                case Wave.Triangle: return 4f * Mathf.Abs(p - 0.5f) - 1f;
                case Wave.Square: return p < 0.5f ? 1f : -1f;
                case Wave.Saw: return 2f * p - 1f;
                default: return Mathf.Sin(2f * Mathf.PI * p);
            }
        }

        private static float Noise(System.Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);

        /// <summary>Короткие плавные вход и выход, чтобы не было щелчков на краях.</summary>
        private static float Fade(float t, float dur, float attack, float release) =>
            Mathf.Clamp01(t / attack) * Mathf.Clamp01((dur - t) / release);

        private static float[] Normalize(float[] b, float peak)
        {
            float max = 0f;
            for (int i = 0; i < b.Length; i++) max = Mathf.Max(max, Mathf.Abs(b[i]));
            if (max < 1e-6f) return b;
            float k = peak / max;
            for (int i = 0; i < b.Length; i++) b[i] *= k;
            return b;
        }

        private static AudioClip Create(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
