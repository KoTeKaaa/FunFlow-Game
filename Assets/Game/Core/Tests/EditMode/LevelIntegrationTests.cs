using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class LevelIntegrationTests
{
    [Test]
    public void Resolver_UsesOwnSceneAndRejectsAmbiguousSources()
    {
        var first = EditorSceneManager.NewPreviewScene();
        var second = EditorSceneManager.NewPreviewScene();
        try
        {
            var owner = new GameObject("Owner");
            SceneManager.MoveGameObjectToScene(owner, first);
            var type = System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
                .Single(t => t.Name == "LootInventory");
            var local = owner.AddComponent(type) as MonoBehaviour;
            var foreignObject = new GameObject("Foreign inventory");
            SceneManager.MoveGameObjectToScene(foreignObject, second);
            var foreign = foreignObject.AddComponent(type) as MonoBehaviour;
            Assert.That(LevelServices.Resolve<ILootInventory>(owner.transform, foreign), Is.SameAs(local));
            var duplicate = new GameObject("Duplicate inventory");
            SceneManager.MoveGameObjectToScene(duplicate, first);
            duplicate.AddComponent(type);
            LogAssert.Expect(LogType.Error, "Owner: expected one active ILootInventory in this scene; found 2.");
            Assert.That(LevelServices.Resolve<ILootInventory>(owner.transform), Is.Null);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(first);
            EditorSceneManager.ClosePreviewScene(second);
        }
    }

    [Test]
    public void Validator_RejectsDuplicatePickupAndSolidCollider()
    {
        var root = new GameObject("Invalid coin");
        try
        {
            var type = System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
                .Single(t => t.Name == "LootItem");
            root.AddComponent(type);
            root.AddComponent(type);
            root.AddComponent<SphereCollider>();
            var issues = LevelValidator.Validate(new[] { root }, false);
            Assert.That(issues.Any(i => i.Message.Contains("Duplicate LootItem")), Is.True);
            Assert.That(issues.Any(i => i.Message.Contains("trigger collider")), Is.True);
            Assert.That(issues.Any(i => i.Message.Contains("solid collider")), Is.True);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase("Assets/Game/Core/Scenes/Game.unity")]
    [TestCase("Assets/Game/Core/Scenes/Game_Level2.unity")]
    [TestCase("Assets/Game/Core/Scenes/Game_Level3.unity")]
    public void SavedLevel_PassesValidation(string path)
    {
        var scene = EditorSceneManager.OpenPreviewScene(path);
        try
        {
            Assert.That(LevelValidator.Validate(scene.GetRootGameObjects(), true).Select(i => i.Message), Is.Empty);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [TestCase("Assets/Game/Level/Prefabs/MuseumLevel 1.prefab")]
    [TestCase("Assets/Game/Level/Prefabs/MuseumLevel 2.prefab")]
    [TestCase("Assets/Game/Level/Prefabs/MuseumLevel 3.prefab")]
    public void LevelPrefab_PassesStructuralValidation(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { Assert.That(LevelValidator.Validate(new[] { root }, false).Select(i => i.Message), Is.Empty); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [UnityTest] public IEnumerator FreshElements_WorkInGame() => CheckFreshElements("Game");
    [UnityTest] public IEnumerator FreshElements_WorkInLevel2() => CheckFreshElements("Game_Level2");
    [UnityTest] public IEnumerator FreshElements_WorkInLevel3() => CheckFreshElements("Game_Level3");

    [Test]
    public void ExitPrefab_ContainsOneAutomaticTriggerInItsDoorway()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Game/Level/Prefabs/Elements/exit.prefab");
        try
        {
            var exit = root.GetComponentsInChildren<MonoBehaviour>(true).Single(c => c is ILevelExit);
            Assert.That(root.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(exit.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Exit")));
            Assert.That(exit.GetComponent<BoxCollider>().isTrigger, Is.True);
            Assert.That(new SerializedObject(exit).FindProperty("coreSessionSource").objectReferenceValue, Is.Null);
            Assert.That(LevelValidator.Validate(new[] { root }, false).Select(i => i.Message), Is.Empty);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [UnityTest]
    public IEnumerator FreshExit_PhysicalContactFinishesWithoutSceneWiring()
    {
        EditorSceneManager.OpenScene("Assets/Game/Core/Scenes/Game.unity");
        yield return new EnterPlayMode();
        var session = Object.FindAnyObjectByType<RunSession>();
        var hud = LevelServices.Components(session.gameObject.scene).Single(c => c.GetType().Name == "PresentationBinder");
        Assert.That(new SerializedObject(hud).FindProperty("routeExit").objectReferenceValue,
            Is.SameAs(LevelServices.FindExit(session.gameObject.scene)), "HUD must find the prefab exit automatically.");
        var gate = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Game/Level/Prefabs/Elements/exit.prefab"), new Vector3(-200, 0, 0), Quaternion.Euler(0, 30, 0));
        yield return null;
        var exit = gate.GetComponentsInChildren<MonoBehaviour>().Single(c => c is ILevelExit);
        Assert.That(new SerializedObject(exit).FindProperty("coreSessionSource").objectReferenceValue, Is.Null);
        session.StartRun();
        var body = (LevelServices.Resolve<IPlayerControl>(session) as MonoBehaviour).GetComponent<Rigidbody>();
        body.position = exit.transform.position;
        body.linearVelocity = Vector3.zero;
        body.WakeUp();
        Physics.SyncTransforms();
        float deadline = Time.realtimeSinceStartup + 2f;
        while (session.State == RunState.Playing && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(session.State, Is.EqualTo(RunState.Won), "Moved and rotated fresh gate must finish the run.");
    }

    [UnityTest]
    public IEnumerator FreshGuard_DetectsPlayerAndStartsRealCoreAlarm()
    {
        EditorSceneManager.OpenScene("Assets/Game/Core/Scenes/Game.unity");
        bool reload = !EditorSettings.enterPlayModeOptionsEnabled
            || (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0;
        yield return new EnterPlayMode(reload);
        var session = Object.FindAnyObjectByType<RunSession>();
        session.StartRun();
        var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Game/Level/Prefabs/Guard.prefab"), new Vector3(-100, 0, 0), Quaternion.identity);
        var guard = instance.GetComponents<MonoBehaviour>().Single(c => c.GetType().Name == "Guard");
        var data = new SerializedObject(guard);
        data.FindProperty("turnMode").enumValueIndex = 0;
        data.ApplyModifiedPropertiesWithoutUndo();
        var player = LevelServices.Resolve<IPlayerControl>(session) as MonoBehaviour;
        var body = player.GetComponent<Rigidbody>();
        body.useGravity = false;
        body.linearVelocity = Vector3.zero;
        body.position = instance.transform.position + Vector3.forward + Vector3.up * 0.5f;
        Physics.SyncTransforms();
        float deadline = Time.realtimeSinceStartup + 2f;
        while (!session.HasAlarmTriggered && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(new SerializedObject(guard).FindProperty("coreSessionSource").objectReferenceValue, Is.SameAs(session));
        Assert.That(session.State, Is.EqualTo(RunState.Alarm), "Fresh guard must detect the real Player without scene wiring.");
        Assert.That(session.RemainingTime, Is.GreaterThan(0));
    }

    [Test]
    public void OriginalBalance_IsStoredInLevelPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Game/Level/Prefabs/MuseumLevel 1.prefab");
        try
        {
            var scripts = root.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c.isActiveAndEnabled).ToArray();
            var prices = scripts.Where(c => c.GetType().Name == "LootItem")
                .Select(c => new SerializedObject(c).FindProperty("value").intValue).ToArray();
            Assert.That(prices.Count(v => v == 50), Is.GreaterThanOrEqualTo(5));
            Assert.That(prices.Count(v => v == 75), Is.GreaterThanOrEqualTo(3));
            Assert.That(prices.Count(v => v == 100), Is.GreaterThanOrEqualTo(3));
            Assert.That(scripts.Where(c => c.GetType().Name == "ValuableShowcase")
                .Select(c => new SerializedObject(c).FindProperty("rewardValue").intValue), Is.EquivalentTo(new[] { 200, 300 }));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static IEnumerator CheckFreshElements(string name)
    {
        EditorSceneManager.OpenScene($"Assets/Game/Core/Scenes/{name}.unity");
        bool reload = !EditorSettings.enterPlayModeOptionsEnabled
            || (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0;
        yield return new EnterPlayMode(reload);
        var session = Object.FindAnyObjectByType<RunSession>();
        int levelNumber = name == "Game" ? 1 : name == "Game_Level2" ? 2 : 3;
        var oldLevel = LevelServices.Components(session.gameObject.scene)
            .OfType<LevelAlarmSettings>().Single().gameObject;
        Object.Destroy(oldLevel);
        yield return null;
        Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/Game/Level/Prefabs/MuseumLevel {levelNumber}.prefab"));
        yield return null;
        var pickups = LevelServices.Components(session.gameObject.scene).Where(c => c.GetType().Name == "LootItem");
        Assert.That(pickups.All(c => new SerializedObject(c).FindProperty("inventory").objectReferenceValue != null),
            Is.True, "Replacing the entire level must bind its pickups without scene overrides.");
        var player = LevelServices.Resolve<IPlayerControl>(session) as MonoBehaviour;
        var body = player.GetComponent<Rigidbody>();
        var loot = LevelServices.Resolve<ILootInventory>(session);
        session.StartRun();
        GameObject Spawn(string prefab) => Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/Game/Level/Prefabs/Elements/{prefab}.prefab"), new Vector3(-50, 0.5f, 0), Quaternion.identity);
        var coin = Spawn("loot");
        var pickup = coin.GetComponents<MonoBehaviour>().Single(c => c.GetType().Name == "LootItem");
        int value = new SerializedObject(pickup).FindProperty("value").intValue;
        body.position = coin.transform.position;
        body.WakeUp();
        Physics.SyncTransforms();
        float deadline = Time.realtimeSinceStartup + 2f;
        while (coin.activeSelf && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(coin.activeSelf, Is.False, "Fresh prefab must collect without Inspector wiring.");
        Assert.That(loot.CurrentValue, Is.EqualTo(value));

        var showcase = Spawn("Showcase_BigLoot");
        yield return null;
        var reward = showcase.GetComponents<MonoBehaviour>().Single(c => c.GetType().Name == "ValuableShowcase");
        int rewardValue = new SerializedObject(reward).FindProperty("rewardValue").intValue;
        reward.SendMessage("OnTriggerEnter", body.GetComponent<Collider>());
        reward.SendMessage("OnTriggerEnter", body.GetComponent<Collider>());
        Assert.That(loot.CurrentValue, Is.EqualTo(value + rewardValue));

        var cutter = Spawn("CutterTrap");
        yield return null;
        cutter.SendMessage("OnTriggerEnter", body.GetComponent<Collider>());
        Assert.That(loot.CurrentValue, Is.EqualTo(value + rewardValue), "Player contact must not cut the bag.");
        var bag = LevelServices.Components(session.gameObject.scene).SelectMany(c => c.GetComponents<Collider>())
            .First(c => c.gameObject.layer == LayerMask.NameToLayer("Bag"));
        Assert.That(bag.attachedRigidbody, Is.Not.Null);
        cutter.SendMessage("OnTriggerEnter", bag);
        int afterCut = loot.CurrentValue;
        Assert.That(afterCut, Is.LessThan(value + rewardValue));
        cutter.SendMessage("OnTriggerEnter", bag);
        Assert.That(loot.CurrentValue, Is.EqualTo(afterCut));

        var alarm = Spawn("AlarmZone");
        yield return null;
        alarm.SendMessage("OnTriggerEnter", body.GetComponent<Collider>());
        Assert.That(session.State, Is.EqualTo(RunState.Alarm));
        Assert.That(new SerializedObject(session).FindProperty("_alarmExit").objectReferenceValue, Is.Not.Null);
        var exit = LevelServices.Components(session.gameObject.scene).Single(c => c.GetType().Name == "ExitTrigger");
        exit.SendMessage("OnTriggerEnter", body.GetComponent<Collider>());
        Assert.That(session.State, Is.EqualTo(RunState.Won));
    }

    [UnityTearDown]
    public IEnumerator ExitPlayMode()
    {
        if (EditorApplication.isPlaying) yield return new UnityEngine.TestTools.ExitPlayMode();
    }
}
