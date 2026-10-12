using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

[SetUpFixture]
public sealed class CorePlayModeStartSceneScope
{
    private const string SavedSceneKey = "Game.Core.Tests.SavedPlayModeStartScene";
    private const string Unset = "<unset>";

    [OneTimeSetUp]
    public void UseTestScenes()
    {
        // SessionState survives the domain reloads caused by Enter/ExitPlayMode.
        if (SessionState.GetString(SavedSceneKey, Unset) == Unset)
            SessionState.SetString(SavedSceneKey,
                AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene = null;
    }

    [OneTimeTearDown]
    public void RestoreStartScene()
    {
        string path = SessionState.GetString(SavedSceneKey, Unset);
        if (path == Unset) return;
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        SessionState.EraseString(SavedSceneKey);
    }
}

public sealed class PlayModeStartSceneTests
{
    private const string MenuPath = "Assets/Game/Presentation/Scenes/MainMenu.unity";

    [UnityTest]
    public IEnumerator Play_OpensMenuEvenWhenGameSceneIsOpen()
    {
        EditorSceneManager.OpenScene("Assets/Game/Core/Scenes/Game.unity");
        var menu = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuPath);
        Assert.That(menu, Is.Not.Null);
        EditorSceneManager.playModeStartScene = menu;
        bool reload = !EditorSettings.enterPlayModeOptionsEnabled
            || (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0;
        yield return new EnterPlayMode(reload);
        yield return null;
        Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, Is.EqualTo(MenuPath));
        Assert.That(Object.FindAnyObjectByType<RunSession>(), Is.Null);
        Assert.That(Object.FindObjectsByType<MonoBehaviour>()
            .Count(component => component.GetType().Name == "MainMenuController"), Is.EqualTo(1));
    }

    [UnityTearDown]
    public IEnumerator ReturnToTestScenes()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.playModeStartScene = null;
    }
}
