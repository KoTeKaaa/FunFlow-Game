using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class PauseMenuTests
{
    private Keyboard _keyboard;

    private static MonoBehaviour Menu() => Object.FindObjectsByType<MonoBehaviour>()
        .Single(c => c.GetType().Name == "PauseMenuController");
    private static Object Reference(Object source, string field) =>
        new SerializedObject(source).FindProperty(field).objectReferenceValue;

    [TestCase("Game")]
    [TestCase("Game_Level2")]
    [TestCase("Game_Level3")]
    public void RestartButton_IsInheritedAndFitsPauseCard(string sceneName)
    {
        var scene = EditorSceneManager.OpenPreviewScene($"Assets/Game/Core/Scenes/{sceneName}.unity");
        try
        {
            var menu = LevelServices.Components(scene).Single(c => c.GetType().Name == "PauseMenuController");
            var button = (Button)Reference(menu, "restartButton");
            Assert.That(button, Is.Not.Null);
            var label = button.GetComponentsInChildren<MonoBehaviour>().Single(c => c.GetType().Name == "TextMeshProUGUI");
            Assert.That(label.GetType().GetProperty("text").GetValue(label), Is.EqualTo("Начать уровень заново"));
            var rect = (RectTransform)button.transform;
            var card = (RectTransform)rect.parent;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Assert.That(corners.All(c => card.rect.Contains(card.InverseTransformPoint(c))), Is.True);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [UnityTest]
    public IEnumerator EscapeRestart_ThreeTimesReloadsCurrentLevelAndClearsPauseAndLoot()
    {
        const string path = "Assets/Game/Core/Scenes/Game_Level2.unity";
        EditorSceneManager.OpenScene(path);
        bool reload = !EditorSettings.enterPlayModeOptionsEnabled
            || (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0;
        yield return new EnterPlayMode(reload);
        _keyboard = InputSystem.AddDevice<Keyboard>();
        for (int attempt = 0; attempt < 3; attempt++)
        {
            yield return null;
            var session = Object.FindAnyObjectByType<RunSession>();
            var menu = Menu();
            Assert.That(menu.isActiveAndEnabled, Is.True);
            var loot = LevelServices.Resolve<ILootInventory>(session);
            session.StartRun();
            loot.AddValue(750);
            session.TriggerAlarm();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Escape));
            InputSystem.Update();
            // Read the key in this input tick, before the runner's next input update clears it.
            menu.SendMessage("Update");
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
            Assert.That(session.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(((GameObject)Reference(menu, "pausePanel")).activeSelf, Is.True);
            var button = (Button)Reference(menu, "restartButton");
            Assert.That(button.gameObject.activeInHierarchy, Is.True);
            button.onClick.Invoke();
            button.onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (session != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(session == null, Is.True, "Restart did not reload the scene.");
            yield return null;
            session = Object.FindAnyObjectByType<RunSession>();
            Assert.That(session.gameObject.scene.path, Is.EqualTo(path));
            Assert.That(session.State, Is.EqualTo(RunState.Waiting));
            Assert.That(session.IsPaused, Is.False);
            Assert.That(session.HasAlarmTriggered, Is.False);
            Assert.That(session.Result, Is.Null);
            Assert.That(LevelServices.Resolve<ILootInventory>(session).CurrentValue, Is.Zero);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(AudioListener.pause, Is.False);
            Assert.That(((GameObject)Reference(Menu(), "pausePanel")).activeSelf, Is.False);
        }
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
        _keyboard = null;
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }
}
