using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class MusicIntegrationTests
{
    private float _previousVolume;
    private bool _previousListenerPause;
    private static object Field(MonoBehaviour music, string name) => music.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(music);
    private static System.Type Preferences => System.AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(a => a.GetTypes()).Single(t => t.FullName == "FunFlow.Presentation.AudioPreferences");
    private static void SetMusic(float value) => Preferences.GetMethod("SetMusic").Invoke(null, new object[] { value });

    [SetUp]
    public void PreserveAudio()
    {
        _previousVolume = (float)Preferences.GetProperty("MusicVolume").GetValue(null);
        _previousListenerPause = AudioListener.pause;
    }

    [TestCase("Assets/Game/Presentation/Scenes/MainMenu.unity", false)]
    [TestCase("Assets/Game/Core/Scenes/Game.unity", true)]
    [TestCase("Assets/Game/Core/Scenes/Game_Level2.unity", true)]
    [TestCase("Assets/Game/Core/Scenes/Game_Level3.unity", true)]
    public void SavedScene_HasOneReadyMusicPrefabAndOneListener(string path, bool game)
    {
        var scene = EditorSceneManager.OpenPreviewScene(path);
        try
        {
            var scripts = LevelServices.Components(scene);
            var music = scripts.Single(c => c.GetType().Name == "MusicController");
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(music.gameObject),
                Is.EqualTo("Assets/Game/Presentation/Prefabs/Music.prefab"));
            var data = new SerializedObject(music);
            foreach (string clip in new[] { "lobbyMusic", "levelMusic", "alarmMusic" })
                Assert.That(data.FindProperty(clip).objectReferenceValue, Is.InstanceOf<AudioClip>());
            var sources = music.GetComponents<AudioSource>();
            Assert.That(sources.Length, Is.EqualTo(2));
            Assert.That(sources.All(s => !s.playOnAwake && s.loop && s.spatialBlend == 0f), Is.True);
            Assert.That(data.FindProperty("sourceA").objectReferenceValue,
                Is.Not.SameAs(data.FindProperty("sourceB").objectReferenceValue));
            Assert.That(scripts.Count(c => c is IRunSession), Is.EqualTo(game ? 1 : 0));
            Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AudioListener>(true))
                .Count(l => l.isActiveAndEnabled), Is.EqualTo(1));
            Assert.That(scripts.Any(c => c.GetType().Name == "TEMP_FakeRunSession"), Is.False);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [UnityTest]
    public IEnumerator Music_InterruptedFadeAndVolumeWorkWhilePausedAndDisableStopsBothVoices()
    {
        SetMusic(0f);
        EditorSceneManager.OpenScene("Assets/Game/Core/Scenes/Game.unity");
        yield return new EnterPlayMode();
        yield return null;
        var session = Object.FindAnyObjectByType<RunSession>();
        var music = LevelServices.Components(session.gameObject.scene).Single(c => c.GetType().Name == "MusicController");
        Assert.That(Field(music, "_runSession"), Is.SameAs(session));
        var data = new SerializedObject(music);
        data.FindProperty("fadeDuration").floatValue = 0.02f;
        data.ApplyModifiedPropertiesWithoutUndo();
        session.StartRun();
        session.TriggerAlarm(); // Interrupt the unfinished level fade.
        session.SetPaused(true);
        AudioListener.pause = true;
        yield return FinishFade(music);
        Assert.That(((AudioSource)Field(music, "_active")).clip,
            Is.SameAs(data.FindProperty("alarmMusic").objectReferenceValue));
        SetMusic(0.35f);
        yield return null;
        Assert.That(((AudioSource)Field(music, "_active")).volume, Is.EqualTo(0.35f).Within(0.001f));
        SetMusic(0f);
        music.enabled = false;
        Assert.That(music.GetComponents<AudioSource>().All(s => !s.isPlaying && s.volume == 0f), Is.True);
        music.enabled = true;
        yield return FinishFade(music);
        session.SetPaused(false);
        AudioListener.pause = false;
        session.ReachExit();
        yield return FinishFade(music);
        Assert.That(music.GetComponents<AudioSource>().All(s => !s.isPlaying && s.volume == 0f), Is.True);
    }

    private static IEnumerator FinishFade(MonoBehaviour music)
    {
        float deadline = Time.realtimeSinceStartup + 2f;
        while (Field(music, "_fadeRoutine") != null && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Field(music, "_fadeRoutine"), Is.Null);
    }

    [UnityTearDown]
    public IEnumerator RestoreAudio()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        SetMusic(_previousVolume);
        AudioListener.pause = _previousListenerPause;
    }
}
