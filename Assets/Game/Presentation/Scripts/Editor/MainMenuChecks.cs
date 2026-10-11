using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FunFlow.Presentation.Editor
{
    public static class MainMenuChecks
    {
        [MenuItem("Game/Check Main Menu in Play Mode")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Open MainMenu and enter Play Mode first.");
            var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
            if (menu == null) throw new InvalidOperationException("MainMenu is not loaded.");
            var data = new SerializedObject(menu);
            var music = (Slider)data.FindProperty("musicSlider").objectReferenceValue;
            var effects = (Slider)data.FindProperty("effectsSlider").objectReferenceValue;
            float oldMusic = AudioPreferences.MusicVolume, oldEffects = AudioPreferences.EffectsVolume;
            bool hadMusic = PlayerPrefs.HasKey("Audio.Music"), hadEffects = PlayerPrefs.HasKey("Audio.Effects");
            try
            {
                Click(data, "settingsButton");
                effects.value = 0.25f;
                music.value = 0.6f;
                Require(Mathf.Approximately(AudioPreferences.EffectsVolume, 0.25f) &&
                    Mathf.Approximately(AudioPreferences.MusicVolume, 0.6f), "Volume binding");
                Require(((TMPro.TMP_Text)data.FindProperty("effectsValue").objectReferenceValue).text == "25%", "Volume label");
                Click(data, "settingsBack");
                Click(data, "playButton");
                var content = (RectTransform)data.FindProperty("content").objectReferenceValue;
                var catalog = (LevelCatalog)data.FindProperty("catalog").objectReferenceValue;
                int expected = catalog.Levels.Count(entry => entry != null);
                Require(content.GetComponentsInChildren<LevelCardView>().Length == expected, "Catalog population");
                Click(data, "levelsBack");
                Click(data, "playButton");
                Require(content.GetComponentsInChildren<LevelCardView>().Length == expected, "No duplicate cards");
                Require(menu.GetComponents<AudioSource>().Length == 1, "Single menu audio source");
                Canvas.ForceUpdateCanvases();
                Require(!menu.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(text => text.isTextOverflowing), "No text overflow");
                Debug.Log("MainMenu checks passed: volume binding, labels, catalog, repeated opening, audio source, text overflow.");
            }
            finally
            {
                music.value = oldMusic;
                effects.value = oldEffects;
                Click(data, "levelsBack");
                if (!hadMusic) PlayerPrefs.DeleteKey("Audio.Music");
                if (!hadEffects) PlayerPrefs.DeleteKey("Audio.Effects");
                PlayerPrefs.Save();
            }
        }

        private static void Click(SerializedObject data, string field) =>
            ((Button)data.FindProperty(field).objectReferenceValue).onClick.Invoke();

        private static void Require(bool result, string check)
        {
            if (!result) throw new InvalidOperationException("MainMenu check failed: " + check);
        }
    }
}
