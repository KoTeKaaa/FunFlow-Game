using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class IndependentLevelUiTests
{
    [TestCase("Assets/Game/Core/Scenes/Game.unity")]
    [TestCase("Assets/Game/Core/Scenes/Game_Level2.unity")]
    [TestCase("Assets/Game/Core/Scenes/Game_Level3.unity")]
    [TestCase("Assets/Game/Presentation/Scenes/MainMenu.unity")]
    public void SavedUi_UsesBakedRussianFont(string path)
    {
        var font = AssetDatabase.LoadAssetAtPath<Object>("Assets/Game/Presentation/UI/RussianUi SDF.asset");
        Assert.That(font, Is.Not.Null);
        Assert.That(font.GetType().GetProperty("atlasPopulationMode").GetValue(font).ToString(), Is.EqualTo("Static"));
        var characters = (System.Collections.IDictionary)font.GetType().GetProperty("characterLookupTable").GetValue(font);
        foreach (char character in "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя•−«»№")
            Assert.That(characters.Contains((uint)character), Is.True, $"Missing baked glyph {character}.");
        var scene = EditorSceneManager.OpenPreviewScene(path);
        try
        {
            var texts = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component => component != null && component.GetType().Name == "TextMeshProUGUI").ToArray();
            Assert.That(texts, Is.Not.Empty);
            foreach (var text in texts)
                Assert.That(new SerializedObject(text).FindProperty("m_fontAsset").objectReferenceValue, Is.SameAs(font), text.name);
            Assert.That(texts.Any(text => ((string)text.GetType().GetProperty("text").GetValue(text)).Contains("→")
                || ((string)text.GetType().GetProperty("text").GetValue(text)).Contains("←")), Is.False);
            var arrows = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                .Where(image => image.name == "NavigationIcon").ToArray();
            Assert.That(arrows, Is.Empty, "Navigation buttons no longer use arrows.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    private static Object Reference(Object owner, string field) =>
        new SerializedObject(owner).FindProperty(field).objectReferenceValue;
    private static string Text(Object text) => (string)text.GetType().GetProperty("text").GetValue(text);

    [Test]
    public void CatalogAndSequence_ListTheSameRealLevels()
    {
        var config = AssetDatabase.LoadAssetAtPath<LevelSequenceConfig>("Assets/Game/Core/Config/LevelSequence.asset");
        var paths = Enumerable.Range(0, config.Count).Select(config.GetScenePath).ToArray();
        var catalog = new SerializedObject(AssetDatabase.LoadAssetAtPath<Object>("Assets/Game/Presentation/UI/LevelCatalog.asset"))
            .FindProperty("levels");
        var menuPaths = Enumerable.Range(0, catalog.arraySize)
            .Select(i => catalog.GetArrayElementAtIndex(i).FindPropertyRelative("ScenePath").stringValue).ToArray();
        Assert.That(menuPaths, Is.EqualTo(paths));
        Assert.That(paths.Distinct().Count(), Is.EqualTo(config.Count));
        var builtLevels = new System.Collections.Generic.List<string>();
        foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            var scene = EditorSceneManager.OpenPreviewScene(entry.path);
            try
            {
                if (LevelServices.Components(scene).OfType<RunSession>().Any()) builtLevels.Add(entry.path);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        Assert.That(paths, Is.EquivalentTo(builtLevels));
        foreach (string path in paths)
        {
            Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == path), Is.True);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var sequence = LevelServices.Components(scene).OfType<LevelSequence>().Single();
                Assert.That(Reference(sequence, "_config"), Is.SameAs(config));
                Assert.That(LevelServices.Components(scene).OfType<RunSession>().Count(), Is.EqualTo(1));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }

    [UnityTest] public IEnumerator FirstLevel_ShowsOnlyItsAttempt() => CheckResult("Game", 1);
    [UnityTest] public IEnumerator SecondLevel_ShowsOnlyItsAttempt() => CheckResult("Game_Level2", 2);
    [UnityTest] public IEnumerator ThirdLevel_ShowsOnlyItsAttemptAndUsesConfiguredCount() => CheckResult("Game_Level3", 3);

    [UnityTest]
    public IEnumerator MainMenu_MouseTransfersFocusAndButtonsHaveNoArrows()
    {
        EditorSceneManager.OpenScene("Assets/Game/Presentation/Scenes/MainMenu.unity");
        yield return new EnterPlayMode();
        yield return null;
        var menu = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
            .Single(c => c != null && c.GetType().Name == "MainMenuController");
        var settings = (UnityEngine.UI.Button)Reference(menu, "settingsButton");
        settings.onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        var back = (UnityEngine.UI.Button)Reference(menu, "settingsBack");
        Assert.That(back.transform.Find("Label/NavigationIcon"), Is.Null);
        back.onClick.Invoke();
        ((UnityEngine.UI.Button)Reference(menu, "playButton")).onClick.Invoke();
        var content = (RectTransform)Reference(menu, "content");
        var buttons = content.GetComponentsInChildren<UnityEngine.UI.Button>();
        Assert.That(buttons.Length, Is.GreaterThanOrEqualTo(2));
        Assert.That(content.GetComponentsInChildren<UnityEngine.UI.Image>(true)
            .Any(image => image.name == "NavigationIcon" && image.gameObject.activeSelf), Is.False);
        foreach (var text in content.GetComponentsInChildren<MonoBehaviour>())
            if (text != null && text.GetType().Name == "TextMeshProUGUI")
                Assert.That((bool)text.GetType().GetProperty("isTextOverflowing").GetValue(text), Is.False, text.name);
        var events = UnityEngine.EventSystems.EventSystem.current;
        Assert.That(events.currentSelectedGameObject, Is.EqualTo(buttons[0].gameObject));
        UnityEngine.EventSystems.ExecuteEvents.Execute(buttons[1].gameObject,
            new UnityEngine.EventSystems.PointerEventData(events), UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
        Assert.That(events.currentSelectedGameObject, Is.EqualTo(buttons[1].gameObject));
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.That(buttons[0].transform.localScale.x, Is.EqualTo(1).Within(0.001f));
        Assert.That(buttons[1].transform.localScale.x, Is.GreaterThan(1.02f));
    }

    private static IEnumerator CheckResult(string sceneName, int number)
    {
        EditorSceneManager.OpenScene($"Assets/Game/Core/Scenes/{sceneName}.unity");
        bool reload = !EditorSettings.enterPlayModeOptionsEnabled
            || (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0;
        yield return new EnterPlayMode(reload);
        yield return null;
        var session = Object.FindAnyObjectByType<RunSession>();
        var startScreens = LevelServices.Components(session.gameObject.scene).Single(c => c.GetType().Name == "HudScreens");
        startScreens.GetType().GetMethod("ShowStart").Invoke(startScreens, null);
        var tutorial = (GameObject)Reference(startScreens, "tutorial");
        Assert.That(tutorial.activeSelf, Is.True);
        var tutorialIcons = tutorial.GetComponentsInChildren<UnityEngine.UI.Image>();
        Assert.That(tutorialIcons.Length, Is.EqualTo(4));
        foreach (var icon in tutorialIcons)
        {
            Assert.That(icon.sprite, Is.Not.Null);
            Assert.That(icon.raycastTarget, Is.False);
        }
        session.StartRun();
        LevelServices.Resolve<ILootInventory>(session).AddValue(493);
        session.ReachExit();
        yield return null;
        var scripts = LevelServices.Components(session.gameObject.scene);
        var flow = scripts.Single(c => c.GetType().Name == "HudLevelFlow");
        var screens = scripts.Single(c => c.GetType().Name == "HudScreens");
        var sequence = Object.FindAnyObjectByType<LevelSequence>();
        var menuButton = (UnityEngine.UI.Button)Reference(flow, "menuButton");
        Assert.That(menuButton.gameObject.activeSelf, Is.EqualTo(sequence.HasNextLevel));
        var hud = scripts.Single(c => c.GetType().Name == "HudView");
        var mouse = (UnityEngine.UI.Image)Reference(hud, "statusIcon");
        Assert.That(AssetDatabase.GetAssetPath(mouse.sprite), Does.EndWith("HudIcons/Mouse.png"));
        Assert.That(tutorial.activeSelf, Is.False, "Tutorial must not appear on the result card.");
        Assert.That(((Component)Reference(screens, "bodyText")).gameObject.activeSelf, Is.True);
        Assert.That(Text(Reference(flow, "tagText")), Is.EqualTo($"УРОВЕНЬ {number} ИЗ {sequence.LevelCount} ПРОЙДЕН"));
        Assert.That(Text(Reference(screens, "titleText")), Does.Contain("493"));
        Assert.That(Text(Reference(screens, "titleText")), Does.Not.Contain("Итого"));
        Assert.That(Text(Reference(screens, "bodyText")), Does.Not.Contain("Уровень 1 —"));
        Assert.That(Text(Reference(flow, "levelButtonLabel")), Is.EqualTo(sequence.HasNextLevel
            ? "Следующий уровень" : "Выход в главное меню"));

        if (number == 3)
        {
            // A fourth entry changes the displayed count without changing HUD code or the shared asset.
            var original = (LevelSequenceConfig)Reference(sequence, "_config");
            var temporary = Object.Instantiate(original);
            var configData = new SerializedObject(temporary);
            var paths = configData.FindProperty("_scenePaths");
            paths.arraySize = 4;
            paths.GetArrayElementAtIndex(3).stringValue = "TemporaryFourthLevelForUiTest";
            configData.ApplyModifiedPropertiesWithoutUndo();
            sequence.enabled = false;
            var sequenceData = new SerializedObject(sequence);
            sequenceData.FindProperty("_config").objectReferenceValue = temporary;
            sequenceData.ApplyModifiedPropertiesWithoutUndo();
            sequence.enabled = true;
            yield return null;
            Assert.That(Text(Reference(flow, "tagText")), Is.EqualTo("УРОВЕНЬ 3 ИЗ 4 ПРОЙДЕН"));
            sequence.enabled = false;
            sequenceData.FindProperty("_config").objectReferenceValue = original;
            sequenceData.ApplyModifiedPropertiesWithoutUndo();
            sequence.enabled = true;
            Object.Destroy(temporary);
        }
        if (sequence.HasNextLevel) menuButton.onClick.Invoke();
        else flow.GetType().GetMethod("Submit").Invoke(flow, null);
        for (int i = 0; i < 120 && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenu"; i++)
            yield return null;
        Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }
}
