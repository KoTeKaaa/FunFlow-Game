using UnityEngine;

namespace FunFlow.Presentation
{
    public static class AudioPreferences
    {
        public static float EffectsVolume => Mathf.Clamp01(PlayerPrefs.GetFloat("Audio.Effects", 1f));
        public static float MusicVolume => Mathf.Clamp01(PlayerPrefs.GetFloat("Audio.Music", 1f));

        public static void SetEffects(float value) => PlayerPrefs.SetFloat("Audio.Effects", Mathf.Clamp01(value));
        public static void SetMusic(float value) => PlayerPrefs.SetFloat("Audio.Music", Mathf.Clamp01(value));
        public static void Save() => PlayerPrefs.Save();
    }
}
