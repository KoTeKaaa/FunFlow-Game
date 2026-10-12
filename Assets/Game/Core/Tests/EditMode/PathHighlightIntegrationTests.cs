using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PathHighlightIntegrationTests
{
    private GameObject _root;
    private MonoBehaviour _highlighter;
    private static System.Type Type(string name) => System.AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(a => a.GetTypes()).Single(t => t.Name == name && typeof(UnityEngine.Object).IsAssignableFrom(t));
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Highlight tests");
        _highlighter = (MonoBehaviour)_root.AddComponent(Type("PlayerPathHighlighter"));
    }
    [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

    private GameObject Spawn(string name, Vector3 position)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Game/Level/Prefabs/Elements/{name}.prefab");
        var instance = Object.Instantiate(prefab, _root.transform);
        instance.transform.position = position;
        return instance;
    }
    private void Collect(Vector3 start, Vector3 end)
    {
        Physics.SyncTransforms();
        _highlighter.GetType().GetMethod("Collect", Private).Invoke(_highlighter, new object[] { start, end });
    }
    private MonoBehaviour[] Targets(string field) => ((System.Collections.IEnumerable)
        _highlighter.GetType().GetField(field, Private).GetValue(_highlighter)).Cast<MonoBehaviour>().ToArray();

    private GameObject SpawnGuard(Vector3 position)
    {
        var guard = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Game/Level/Prefabs/Guard.prefab"), _root.transform);
        guard.transform.position = position;
        return guard;
    }

    [Test]
    public void Guard_BlocksAimAndHighlightsBodyWithoutVisionSensor()
    {
        Vector3 start = new Vector3(200, 0.5f, 30);
        var guard = SpawnGuard(start + Vector3.right * 3 - Vector3.up * 0.5f);
        Spawn("loot", start + Vector3.right * 5);
        var sensor = new GameObject("Future vision sensor");
        sensor.transform.SetParent(guard.transform, false);
        sensor.AddComponent<SphereCollider>().radius = 6;
        sensor.GetComponent<Collider>().isTrigger = true;
        Physics.SyncTransforms();
        Assert.That(Physics.SphereCast(start, 0.48f, Vector3.right, out var hit, 6,
            1 << LayerMask.NameToLayer("Obstacle"), QueryTriggerInteraction.Ignore), Is.True);
        Assert.That(hit.collider.gameObject, Is.SameAs(guard));
        Collect(start, start + Vector3.right * hit.distance);
        Assert.That(Targets("hazardTargets").Single().gameObject, Is.SameAs(guard));
        Assert.That(Targets("lootTargets"), Is.Empty);
        Collect(start, start + Vector3.right);
        Assert.That(Targets("hazardTargets"), Is.Empty, "Vision sensor is not a physical aim target.");
    }

    [Test]
    public void Guard_ValidatorAcceptsPrefabAndRejectsBrokenSetup()
    {
        var guard = SpawnGuard(new Vector3(200, 0, 30));
        Assert.That(LevelValidator.Validate(new[] { guard }, false).Select(i => i.Message), Is.Empty);
        var data = new SerializedObject(guard.GetComponent(Type("Guard")));
        data.FindProperty("head").objectReferenceValue = null;
        data.FindProperty("thiefMask").intValue = 0;
        data.FindProperty("obstacleMask").intValue = 1 << LayerMask.NameToLayer("Player");
        data.FindProperty("viewRange").floatValue = -1;
        data.ApplyModifiedPropertiesWithoutUndo();
        guard.GetComponent<Collider>().isTrigger = true;
        var messages = LevelValidator.Validate(new[] { guard }, false).Select(i => i.Message).ToArray();
        foreach (string expected in new[] { "solid collider", "head", "thiefMask", "obstacleMask", "viewRange" })
            Assert.That(messages.Any(m => m.Contains(expected)), Is.True, expected);
    }

    [Test]
    public void Guard_AutomaticallyBindsOwnSceneAndTriggersAlarmOnce()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_root, scene);
            var session = (IRunSession)_root.AddComponent(Type("GuardSandboxSession"));
            var guard = SpawnGuard(new Vector3(200, 0, 30)).GetComponent(Type("Guard"));
            guard.GetType().GetMethod("EnsureRunState", Private).Invoke(guard, null);
            Assert.That(guard.GetType().GetField("coreSessionSource", Private).GetValue(guard), Is.SameAs(session));
            guard.GetType().GetMethod("Spot", Private).Invoke(guard, null);
            guard.GetType().GetMethod("Spot", Private).Invoke(guard, null);
            Assert.That(session.HasAlarmTriggered, Is.True);
            Assert.That(session.GetType().GetField("alarmCalls", Private).GetValue(session), Is.EqualTo(1));
            Assert.That(((IPathHighlightTarget)guard).CanHighlight, Is.True, "The guard still blocks movement after alarm.");
        }
        finally
        {
            Object.DestroyImmediate(_root);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void NewPrefabs_FollowMovedPathWithoutNamesLayersOrReferences()
    {
        Vector3 start = new Vector3(200, 0.5f, 30);
        var coin = Spawn("loot", start + Vector3.right);
        var showcase = Spawn("Showcase_BigLoot", start + Vector3.right * 2);
        Spawn("AlarmZone", start + Vector3.right * 3);
        Spawn("CutterTrap", start + Vector3.right * 4);
        showcase.name = "Arbitrary showcase name";
        showcase.layer = 0; // Highlighting does not require a special collider layer.
        Collect(start, start + Vector3.right * 6);
        Assert.That(Targets("lootTargets").Length, Is.EqualTo(2));
        Assert.That(Targets("hazardTargets").Length, Is.EqualTo(2));
        Vector3 shift = new Vector3(50, 0, 40);
        _root.transform.position += shift;
        Collect(start + shift, start + shift + Vector3.right * 6);
        Assert.That(Targets("lootTargets").Length, Is.EqualTo(2));
        Collect(start, start + Vector3.right * 6);
        Assert.That(Targets("lootTargets"), Is.Empty);
        var later = Spawn("loot", start + Vector3.right);
        Collect(start, start + Vector3.right * 6);
        Assert.That(Targets("lootTargets").Single().gameObject, Is.SameAs(later));
    }

    [Test]
    public void DenseTrack_DoesNotDropTargetsAfterThirtyTwoHits()
    {
        Vector3 start = new Vector3(200, 0.5f, 30);
        for (int i = 0; i < 48; i++) Spawn("loot", start + Vector3.right * (1 + i * 0.05f));
        Collect(start, start + Vector3.right * 6);
        Assert.That(Targets("lootTargets").Length, Is.EqualTo(48));
        var used = Targets("lootTargets")[0];
        used.GetType().GetField("collected", Private).SetValue(used, true);
        Collect(start, start + Vector3.right * 6);
        Assert.That(Targets("lootTargets").Length, Is.EqualTo(47));
    }

    [Test]
    public void NewlyAddedMeshes_AreDrawnWithoutChangingOriginalMaterials()
    {
        Vector3 start = new Vector3(200, 0.5f, 30);
        var coin = Spawn("loot", start + Vector3.right);
        Collect(start, start + Vector3.right * 3);
        var extra = GameObject.CreatePrimitive(PrimitiveType.Cube);
        extra.transform.SetParent(coin.transform, false);
        Object.DestroyImmediate(extra.GetComponent<Collider>());
        var renderers = coin.GetComponentsInChildren<MeshRenderer>();
        var materials = renderers.Select(r => r.sharedMaterials).ToArray();
        var enabled = renderers.Select(r => r.enabled).ToArray();
        var data = new SerializedObject(_highlighter);
        data.FindProperty("glowMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Game/Player/Materials/PathHighlightGlow.mat");
        data.ApplyModifiedPropertiesWithoutUndo();
        var targets = _highlighter.GetType().GetField("lootTargets", Private).GetValue(_highlighter);
        _highlighter.GetType().GetMethod("Draw", Private).Invoke(_highlighter,
            new[] { targets, new MaterialPropertyBlock() });
        var drawn = ((System.Collections.IEnumerable)_highlighter.GetType().GetField("drawnMeshes", Private)
            .GetValue(_highlighter)).Cast<MeshFilter>().ToArray();
        Assert.That(drawn, Does.Contain(extra.GetComponent<MeshFilter>()));
        for (int i = 0; i < renderers.Length; i++)
        {
            Assert.That(renderers[i].enabled, Is.EqualTo(enabled[i]));
            Assert.That(renderers[i].sharedMaterials, Is.EqualTo(materials[i]));
        }
        Assert.That(coin.GetComponent<Collider>().isTrigger, Is.True);
    }

    [Test]
    public void Builder_UsesReadyPrefabsAndTheirAuthoredHeight()
    {
        var builder = ScriptableObject.CreateInstance(Type("DifficultyLevelBuilder"));
        try
        {
            foreach (string field in new[] { "obstaclePrefab", "lootPrefab", "cutterPrefab", "alarmPrefab", "showcasePrefab" })
                Assert.That(builder.GetType().GetField(field, Private).GetValue(builder), Is.Not.Null);
            var prefab = (GameObject)builder.GetType().GetField("lootPrefab", Private).GetValue(builder);
            builder.GetType().GetMethod("Spawn", Private).Invoke(builder,
                new object[] { prefab, _root.transform, 3f, 2f });
            var coin = _root.GetComponentsInChildren<MonoBehaviour>().Single(c => c is IPathHighlightTarget);
            Assert.That(coin.transform.localPosition.y, Is.EqualTo(prefab.transform.localPosition.y));
            Assert.That(LevelValidator.Validate(new[] { coin.gameObject }, false), Is.Empty);
        }
        finally { Object.DestroyImmediate(builder); }
    }
}
