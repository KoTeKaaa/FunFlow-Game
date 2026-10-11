using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

public sealed class PlayerIntegrationTests
{
    private const string ScenePath = "Assets/Game/Core/Scenes/Game.unity";
    private RunSession _session;
    private MonoBehaviour _motor;
    private IPlayerControl _player;
    private Rigidbody _body;
    private CorridorCamera _camera;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var savedMotor = Object.FindObjectsByType<MonoBehaviour>()
            .Single(component => component.GetType().FullName == "FunFlow.Player.PlayerMotor");
        Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(savedMotor.gameObject),
            Is.EqualTo("Assets/Game/Player/Prefabs/Player.prefab"));
        bool reload = !EditorSettings.enterPlayModeOptionsEnabled
            || (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0;
        yield return new EnterPlayMode(reload);
        _session = Object.FindAnyObjectByType<RunSession>();
        _motor = Object.FindObjectsByType<MonoBehaviour>()
            .Single(component => component.GetType().FullName == "FunFlow.Player.PlayerMotor");
        _player = (IPlayerControl)_motor;
        _body = _motor.GetComponent<Rigidbody>();
        _camera = Object.FindAnyObjectByType<CorridorCamera>();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        yield return new ExitPlayMode();
    }

    private static Object Source(Object target, string field) =>
        new SerializedObject(target).FindProperty(field).objectReferenceValue;

    // Player is in Assembly-CSharp; Core tests deliberately have no compile-time dependency on it.
    private bool Launch(Vector3 direction, float power = 0.2f) =>
        (bool)_motor.GetType().GetMethod("TryLaunch").Invoke(_motor, new object[] { direction, power });

    [Test]
    public void SavedScene_UsesRealPrefabAndSharedCameraAndLootSource()
    {
        Assert.That(Source(_session, "_playerSource"), Is.SameAs(_motor));
        Assert.That(Source(_motor, "lootSource"), Is.SameAs(Source(_session, "_lootSource")));
        Assert.That(Source(_camera, "_target"), Is.SameAs(_motor.transform));
        var input = _motor.GetComponents<MonoBehaviour>()
            .Single(component => component.GetType().Name == "PlayerLaunchInput");
        Assert.That(Source(input, "viewCamera"), Is.SameAs(_camera.GetComponent<Camera>()));
        Assert.That(_session.State, Is.EqualTo(RunState.Waiting));
        Assert.That(Launch(Vector3.right), Is.False);
        Assert.That(_player.IsMoving, Is.False);
    }

    [UnityTest]
    public IEnumerator ThreeLaunches_RejectMovingAndBackwardInputAndFollowPlayer()
    {
        int launches = 0;
        _player.Launched += () => launches++;
        Assert.That(Launch(Vector3.right), Is.False);
        _session.StartRun();
        float height = _body.position.y;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Assert.That(Launch(Vector3.left), Is.False);
            Assert.That(Launch(Vector3.forward), Is.False);
            Assert.That(Launch(new Vector3(1f, 0f, 0.25f)), Is.True);
            Assert.That(Launch(Vector3.right), Is.False);
            float previousX = _body.position.x;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (_player.IsMoving && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                Assert.That(_body.position.x, Is.GreaterThanOrEqualTo(previousX - 0.001f));
                Assert.That(_body.position.y, Is.EqualTo(height).Within(0.001f));
                previousX = _body.position.x;
            }
            Assert.That(_player.IsMoving, Is.False, "Player did not stop within 3 seconds.");
            Assert.That(_body.linearVelocity.sqrMagnitude, Is.LessThan(0.001f));
            yield return null;
            Assert.That(_camera.transform.position.x, Is.EqualTo(_body.position.x + 6f).Within(0.01f));
        }
        Assert.That(launches, Is.EqualTo(3));
        _session.ReachExit();
        Assert.That(_session.State, Is.EqualTo(RunState.Won));
        Assert.That(Launch(Vector3.right), Is.False);
        Assert.That(launches, Is.EqualTo(3));
    }

    [UnityTest]
    public IEnumerator FinishDuringMotion_BlocksNewLaunchWithoutStoppingSlide()
    {
        _session.StartRun();
        Assert.That(Launch(Vector3.right), Is.True);
        _session.ReachExit();
        Assert.That(_session.Result.Value.IsVictory, Is.True);
        Assert.That(_player.IsMoving, Is.True);
        Assert.That(_body.linearVelocity.x, Is.GreaterThan(0f));
        float deadline = Time.realtimeSinceStartup + 3f;
        while (_player.IsMoving && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(_player.IsMoving, Is.False);
        Assert.That(Launch(Vector3.right), Is.False);
    }

    [UnityTest]
    public IEnumerator TestLoot_WeightShortensActualSlideButStillAllowsMovement()
    {
        var loot = (CoreModuleProbe)Source(_session, "_lootSource");
        var config = (GameConfig)Source(_motor, "config");
        Assert.That(Source(loot, "_config"), Is.SameAs(config));
        _session.StartRun();
        var origin = _body.position;
        float emptyDistance = 0f;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            loot.SetTestValue(attempt == 0 ? 0 : config.ValueAtFullWeight);
            Assert.That(loot.NormalizedWeight, Is.EqualTo(attempt));
            Assert.That((float)_motor.GetType().GetProperty("Burden").GetValue(_motor), Is.EqualTo(attempt));
            _body.position = origin;
            Assert.That(Launch(Vector3.right, 1f), Is.True);
            float speed = _body.linearVelocity.x;
            Assert.That(speed, Is.EqualTo(config.MaxLaunchSpeed *
                (attempt == 0 ? 1f : config.FullWeightSpeedMultiplier)).Within(0.001f));
            // EditMode runner iterations are not physics ticks, even after EnterPlayMode.
            float deadline = Time.realtimeSinceStartup + 8f;
            while (_player.IsMoving && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(_player.IsMoving, Is.False);
            float distance = _body.position.x - origin.x;
            Assert.That(distance, Is.GreaterThan(0.1f));
            if (attempt == 0) emptyDistance = distance;
            else Assert.That(distance, Is.LessThan(emptyDistance));
        }
        loot.SetTestValue(config.ValueAtFullWeight * 2);
        Assert.That(loot.NormalizedWeight, Is.EqualTo(1f));
        Assert.That(Launch(Vector3.right, 1f), Is.True);
        Assert.That(_body.linearVelocity.x, Is.GreaterThan(0f));
    }

    [UnityTest]
    public IEnumerator TestLoot_BagFollowsAndGrowsAndResultKeepsSnapshot()
    {
        var loot = (CoreModuleProbe)Source(_session, "_lootSource");
        var config = (GameConfig)Source(loot, "_config");
        var bag = (Transform)Source(loot, "_bagVisual");
        Assert.That(Source(loot, "_playerTarget"), Is.SameAs(_motor.transform));
        Assert.That(bag.GetComponent<Collider>(), Is.Null, "Visual stub must not block Player or simulate traps.");
        loot.SetTestValue(0);
        var emptyScale = bag.localScale;
        int changes = 0;
        System.Action onChange = () =>
        {
            changes++;
            Assert.That(loot.CurrentValue, Is.EqualTo(config.ValueAtFullWeight));
            Assert.That(loot.NormalizedWeight, Is.EqualTo(1f));
        };
        loot.Changed += onChange;
        loot.SetTestValue(config.ValueAtFullWeight);
        loot.SetTestValue(config.ValueAtFullWeight);
        loot.Changed -= onChange;
        Assert.That(changes, Is.EqualTo(1));
        Assert.That(bag.localScale.x, Is.GreaterThan(emptyScale.x));
        _session.StartRun();
        Assert.That(Launch(Vector3.right), Is.True);
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(Vector3.Distance(bag.position, _motor.transform.position + new Vector3(-0.8f, -0.1f, 0f)), Is.LessThan(0.01f));
        _session.ReachExit();
        loot.SetTestValue(0);
        _session.ReachExit();
        Assert.That(_session.Result.Value.FinalValue, Is.EqualTo(config.ValueAtFullWeight));
        Assert.That(bag.localScale, Is.EqualTo(emptyScale));
    }

    [Test]
    public void MouseAim_IsBlockedBeforeStartAndCancelledOnResult()
    {
        var input = _motor.GetComponents<MonoBehaviour>()
            .Single(component => component.GetType().Name == "PlayerLaunchInput");
        var mouse = InputSystem.AddDevice<Mouse>();
        var preview = _motor.GetComponentsInChildren<MonoBehaviour>()
            .Single(component => component.GetType().Name == "PlayerPathPreview");
        bool Aiming() => (bool)input.GetType().GetProperty("IsAiming").GetValue(input);
        void Pointer(Vector3 world, bool pressed)
        {
            Vector2 screen = _camera.GetComponent<Camera>().WorldToScreenPoint(world);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen, buttons = (ushort)(pressed ? 1 : 0) });
            InputSystem.Update();
            input.SendMessage("Update");
        }
        try
        {
            Pointer(_motor.transform.position, true);
            Assert.That(Aiming(), Is.False);
            Pointer(_motor.transform.position, false);
            _session.StartRun();
            Pointer(_motor.transform.position, true);
            Assert.That(Aiming(), Is.True);
            Pointer(_motor.transform.position - Vector3.right * 3f, true);
            Assert.That((bool)input.GetType().GetProperty("HasLaunchableAim").GetValue(input), Is.True);
            preview.SendMessage("LateUpdate");
            var line = preview.GetComponent<LineRenderer>();
            Assert.That(line.enabled, Is.True);
            Assert.That(line.GetPosition(1).x, Is.GreaterThan(line.GetPosition(0).x));
            Assert.That(line.GetPosition(0).y, Is.EqualTo(0.02f).Within(0.001f));
            Assert.That(line.GetPosition(1).y, Is.EqualTo(line.GetPosition(0).y).Within(0.001f));
            _session.ReachExit();
            Pointer(_motor.transform.position - Vector3.right * 3f, false);
            preview.SendMessage("LateUpdate");
            Assert.That(line.enabled, Is.False);
            Assert.That(Aiming(), Is.False);
            Assert.That(_player.IsMoving, Is.False);
            Assert.That(_body.linearVelocity.sqrMagnitude, Is.Zero);
            Pointer(_motor.transform.position, true);
            Assert.That(Aiming(), Is.False);
        }
        finally
        {
            InputSystem.RemoveDevice(mouse);
        }
    }
}
