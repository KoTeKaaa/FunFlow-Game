using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace FunFlow.Presentation
{
    /// <summary>
    /// Все звуки реакции на действия игрока. Пустой слот клипа = синтезированная заглушка.
    /// Несколько «голосов», чтобы звуки не обрывали друг друга.
    /// </summary>
    public sealed class PresentationAudio : MonoBehaviour
    {
        [Header("Клипы (пусто — синтезированный звук)")]
        [SerializeField] private AudioClip launch;
        [SerializeField] private AudioClip pickup;
        [SerializeField] private AudioClip impact;
        [SerializeField] private AudioClip cut;
        [SerializeField] private AudioClip shatter;
        [SerializeField] private AudioClip alarm;
        [SerializeField] private AudioClip tick;
        [SerializeField] private AudioClip win;
        [SerializeField] private AudioClip lose;

        [Header("Микширование")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.8f;
        [SerializeField, Range(1, 8)] private int voiceCount = 5;
        [SerializeField] private AudioMixerGroup output;

        private AudioSource[] voices;
        private int nextVoice;
        private readonly List<AudioClip> generated = new List<AudioClip>();
        private AudioClip launchClip, pickupClip, impactClip, cutClip, shatterClip, alarmClip, tickClip, winClip, loseClip;

        private void Awake()
        {
            voices = new AudioSource[voiceCount];
            for (int i = 0; i < voiceCount; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.outputAudioMixerGroup = output;
                voices[i] = s;
            }

            launchClip = launch != null ? launch : Keep(ProceduralSfx.Launch());
            pickupClip = pickup != null ? pickup : Keep(ProceduralSfx.Pickup());
            impactClip = impact != null ? impact : Keep(ProceduralSfx.Impact());
            cutClip = cut != null ? cut : Keep(ProceduralSfx.Cut());
            shatterClip = shatter != null ? shatter : Keep(ProceduralSfx.Shatter());
            alarmClip = alarm != null ? alarm : Keep(ProceduralSfx.Alarm());
            tickClip = tick != null ? tick : Keep(ProceduralSfx.Tick());
            winClip = win != null ? win : Keep(ProceduralSfx.Win());
            loseClip = lose != null ? lose : Keep(ProceduralSfx.Lose());
        }

        private void OnDisable() => StopAll();

        private void OnDestroy()
        {
            for (int i = 0; i < generated.Count; i++)
                if (generated[i] != null) Destroy(generated[i]);
            generated.Clear();
        }

        public void PlayLaunch(float power01)
        {
            power01 = Mathf.Clamp01(power01);
            Play(launchClip, Mathf.Lerp(0.45f, 1f, power01), Mathf.Lerp(0.9f, 1.15f, power01));
        }

        public void PlayPickup(int amount) =>
            Play(pickupClip, 1f, 1f + Mathf.Min(0.25f, amount / 2000f) + Random.Range(-0.03f, 0.03f));

        /// <summary>Удар о стену; strength01 — сила удара 0…1 от PlayerMotor.Collided.</summary>
        public void PlayImpact(float strength01)
        {
            strength01 = Mathf.Clamp01(strength01);
            Play(impactClip, Mathf.Lerp(0.25f, 1f, strength01), Mathf.Lerp(1.15f, 0.9f, strength01));
        }

        public void PlayCut() => Play(cutClip, 1f, Random.Range(0.95f, 1.05f));
        public void PlayShatter() => Play(shatterClip, 1f, Random.Range(0.9f, 1.1f));
        public void PlayAlarm() => Play(alarmClip, 1f, 1f);
        public void PlayTick() => Play(tickClip, 0.8f, 1f);
        public void PlayWin() => Play(winClip, 1f, 1f);
        public void PlayLose() => Play(loseClip, 1f, 1f);

        public void StopAll()
        {
            if (voices == null) return;
            for (int i = 0; i < voices.Length; i++)
                if (voices[i] != null) voices[i].Stop();
        }

        private void Play(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || voices == null || voices.Length == 0) return;

            AudioSource source = null;
            for (int i = 0; i < voices.Length; i++)
            {
                if (!voices[i].isPlaying) { source = voices[i]; break; }
            }
            if (source == null)
            {
                source = voices[nextVoice];
                nextVoice = (nextVoice + 1) % voices.Length;
            }

            source.clip = clip;
            source.pitch = pitch;
            source.volume = Mathf.Clamp01(volume) * masterVolume;
            source.Play();
        }

        private AudioClip Keep(AudioClip clip)
        {
            generated.Add(clip);
            return clip;
        }
    }
}
