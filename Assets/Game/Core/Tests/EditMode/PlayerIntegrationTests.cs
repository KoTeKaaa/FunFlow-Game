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
        var savedHud = Object.FindObjectsByType<MonoBehaviour>()
            .Single(component => component.GetType().Name == "PresentationBinder");
        Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(savedHud.gameObject),
            Is.EqualTo("Assets/Game/Presentation/Prefabs/HUD.prefab"));
        var savedMotor = Object.FindObjectsByType<MonoBehaviour>()
            .Single(component => component.GetType().FullName == "FunFlow.Player.PlayerMotor");
        Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(savedMotor.gameObject),
            Is.EqualTo("Assets/Game/Player/Prefabs/Player.prefab"));
        var savedLoot = (MonoBehaviour)Source(Object.FindAnyObjectByType<RunSession>(), "_lootSource");
        Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(savedLoot.gameObject),
            Is.EqualTo("Assets/Game/Core/Prefabs/LootSet.prefab"));
        bool reload = !EditorSettings.enterPlayModeOptionsEnabled
            || (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0;
        yield return new EnterPlayMode(reload);
        BindScene();
    }

    private void BindScene()
    {
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

    private MonoBehaviour LootSource => (MonoBehaviour)Source(_session, "_lootSource");
    private ILootInventory Loot => (ILootInventory)LootSource;
    private MonoBehaviour BagVisual => Object.FindObjectsByType<MonoBehaviour>()
        .Single(component => component.GetType().Name == "LootBagVisual");
    private MonoBehaviour[] Items => Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,
        FindObjectsSortMode.None).Where(component => component.GetType().Name == "LootItem").ToArray();
    private static int PrefabItemCount => AssetDatabase.LoadAssetAtPath<GameObject>(
        "Assets/Game/Level/Prefabs/MuseumLevel 1.prefab").GetComponentsInChildren<MonoBehaviour>(true)
        .Count(component => component != null && component.GetType().Name == "LootItem");

    private MonoBehaviour[] Module(string name) => Object.FindObjectsByType<MonoBehaviour>(
        FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.GetType().Name == name).ToArray();

    private static object Field(Object component, string name) => component.GetType().GetField(name,
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(component);

    [UnityTest]
    public IEnumerator Feedback_AlarmAndLossPlayOnceAndTimeComesFromCore()
    {
        yield return null;
        _session.StartRun();
        _session.TriggerAlarm();
        _session.TriggerAlarm();
        var audio = Module("PresentationAudio").Single();
        var voices = audio.GetComponents<AudioSource>();
        Assert.That(voices.Count(v => v.clip != null && v.clip.name == "sfx_alarm"), Is.EqualTo(1));
        typeof(RunSession).GetField("_alarmDeadline", System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic).SetValue(_session, Time.timeAsDouble + 7d);
        _session.SendMessage("Update");
        var text = Source(Module("HudView").Single(), "alarmValue");
        Assert.That((string)text.GetType().GetProperty("text").GetValue(text),
            Is.EqualTo("00:07"));
        typeof(RunSession).GetField("_alarmDeadline", System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic).SetValue(_session, Time.timeAsDouble - 1d);
        _session.SendMessage("Update");
        Assert.That(_session.State, Is.EqualTo(RunState.Lost));
        var resultTitle = Source(Module("HudScreens").Single(), "titleText");
        Assert.That((string)resultTitle.GetType().GetProperty("text").GetValue(resultTitle), Does.Contain("Время вышло"));
        var lossVoice = voices.Single(v => v.clip != null && v.clip.name == "sfx_lose");
        yield return new WaitForSeconds(0.1f);
        float playback = lossVoice.time;
        _session.TriggerAlarm();
        _session.ReachExit();
        _session.SendMessage("Update");
        Assert.That(lossVoice.time, Is.GreaterThanOrEqualTo(playback));
        Assert.That(voices.Count(v => v.isPlaying), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator FullLevel_PhysicalTrapsAndShowcaseUseSharedContractsOnce()
    {
        yield return null;
        var binder = Module("PresentationBinder").Single();
        var exit = (Transform)Source(binder, "routeExit");
        var alarm = Module("AlarmTrap").Single();
        Assert.That(alarm.transform.position.x, Is.GreaterThan(exit.position.x / 2f));
        Assert.That(Source(alarm, "coreSessionSource"), Is.SameAs(_session));
        _session.StartRun();
        SetValue(1000);
        var cutter = Module("CutterTrap").OrderBy(c => c.transform.position.x).First();
        Assert.That(Source(cutter, "lootSource"), Is.SameAs(LootSource));
        Assert.That(BagVisual.GetComponent<Collider>().attachedRigidbody.isKinematic, Is.True);
        _body.position = new Vector3(cutter.transform.position.x - 1f, _body.position.y, cutter.transform.position.z);
        Physics.SyncTransforms();
        Assert.That(Launch(Vector3.right, 1f), Is.True);
        float stopDeadline = Time.realtimeSinceStartup + 4f;
        while (_player.IsMoving && Time.realtimeSinceStartup < stopDeadline) yield return null;
        Assert.That(Loot.CurrentValue, Is.EqualTo(651)); // floor(1000 * float(0.35)) = 349
        Assert.That(Loot.NormalizedWeight, Is.LessThan(1f));
        cutter.SendMessage("OnTriggerEnter", BagVisual.GetComponent<Collider>());
        Assert.That(Loot.CurrentValue, Is.EqualTo(651));
        SetValue(0);
        var emptyCutter = Module("CutterTrap").OrderBy(c => c.transform.position.x).Last();
        emptyCutter.SendMessage("OnTriggerEnter", BagVisual.GetComponent<Collider>());
        Assert.That(Loot.CurrentValue, Is.Zero);
        _body.position = new Vector3(alarm.transform.position.x - 2f, _body.position.y, alarm.transform.position.z);
        Physics.SyncTransforms();
        Assert.That(Launch(Vector3.right, 0.35f), Is.True);
        stopDeadline = Time.realtimeSinceStartup + 4f;
        while (_player.IsMoving && Time.realtimeSinceStartup < stopDeadline) yield return null;
        Assert.That(_session.State, Is.EqualTo(RunState.Alarm));
        double deadline = (double)Field(_session, "_alarmDeadline");
        alarm.SendMessage("OnTriggerEnter", _body.GetComponent<Collider>());
        _session.TriggerAlarm();
        Assert.That(Field(_session, "_alarmDeadline"), Is.EqualTo(deadline));
        var showcase = Module("ValuableShowcase").OrderBy(c => c.transform.position.x).First();
        _body.position = new Vector3(showcase.transform.position.x - 2f, _body.position.y, showcase.transform.position.z);
        Physics.SyncTransforms();
        Assert.That(Launch(Vector3.right, 0.35f), Is.True);
        stopDeadline = Time.realtimeSinceStartup + 4f;
        while (_player.IsMoving && Time.realtimeSinceStartup < stopDeadline) yield return null;
        int reward = (int)showcase.GetType().GetField("rewardValue").GetValue(showcase);
        Assert.That(Loot.CurrentValue, Is.EqualTo(reward));
        Assert.That(((GameObject)showcase.GetType().GetField("glassModel").GetValue(showcase)).activeSelf, Is.False);
        showcase.SendMessage("OnTriggerEnter", _body.GetComponent<Collider>());
        Assert.That(Loot.CurrentValue, Is.EqualTo(reward));
        Assert.That((bool)Field(Module("PresentationController").Single(), "alarmAnnounced"), Is.True);
        var alarmText = Source(Module("HudView").Single(), "alarmValue");
        Assert.That((string)alarmText.GetType().GetProperty("text").GetValue(alarmText), Does.Match(@"^\d{2,}:[0-5]\d$"));
    }

    [UnityTest]
    public IEnumerator FullLevel_MaximumWeightCanReachExitWithForwardLaunches()
    {
        yield return null;
        _session.StartRun();
        SetValue(1000);
        var config = (GameConfig)Source(LootSource, "config");
        // PR #33 widens the columns and adds obstacles at X=36, 67 and 79.
        // Keep the full bag clear of cutters; cross the alarm on its left side.
        var route = new[] { new Vector3(24f, 0f, 5.5f), new Vector3(48f, 0f, 5.5f),
            new Vector3(52f, 0f, -4.5f), new Vector3(61f, 0f, -4.5f),
            new Vector3(64f, 0f, -5.5f), new Vector3(70f, 0f, -5.5f),
            new Vector3(75f, 0f, 6f), new Vector3(96f, 0f, 6f), new Vector3(102.5f, 0f, 0f) };
        int waypoint = 0;
        for (int launch = 0; launch < 120 && _session.Result == null; launch++)
        {
            Vector3 position = new Vector3(_body.position.x, 0f, _body.position.z);
            while (waypoint < route.Length - 1 && Vector3.Distance(position, route[waypoint]) < 0.5f) waypoint++;
            Vector3 direction = route[waypoint] - _body.position;
            direction.y = 0f;
            Assert.That(direction.x, Is.GreaterThan(0f), "Route requires a backward launch.");
            float speed = config.MaxLaunchSpeed * Mathf.Lerp(1f, config.FullWeightSpeedMultiplier, Loot.NormalizedWeight);
            float power = Mathf.Clamp01(Mathf.Sqrt(2f * config.SlideDeceleration * direction.magnitude) / speed);
            Assert.That(Launch(direction.normalized, power), Is.True);
            float stopDeadline = Time.realtimeSinceStartup + 8f;
            while (_player.IsMoving && _session.Result == null && Time.realtimeSinceStartup < stopDeadline) yield return null;
            Assert.That(_player.IsMoving && _session.Result == null, Is.False, "Player failed to stop.");
        }
        Assert.That(_session.State, Is.EqualTo(RunState.Won), $"Route stopped at {_body.position} (waypoint {waypoint}).");
        Assert.That(_session.HasAlarmTriggered, Is.True);
        Assert.That(_session.Result.Value.FinalValue, Is.GreaterThanOrEqualTo(1000), "Route must not require cutting loot.");
    }

    private void SetValue(int value)
    {
        Loot.RemoveFraction(1f);
        if (value > 0) LootSource.GetType().GetMethod("AddValue").Invoke(LootSource, new object[] { value });
    }

    [UnityTest, Explicit("Three timed full-bag attempts; opt in to this long balance benchmark.")]
    public IEnumerator Balance_ThreeFullBagsReachExitAfterEarliestAlarmWithoutCuts()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (attempt > 0)
            {
                var previous = _session;
                _session.RestartRun();
                float reloadDeadline = Time.realtimeSinceStartup + 5f;
                while (Object.FindAnyObjectByType<RunSession>() == previous && Time.realtimeSinceStartup < reloadDeadline)
                    yield return null;
                BindScene();
                Assert.That(_session, Is.Not.Null.And.Not.SameAs(previous));
            }
            yield return null;
            var config = (GameConfig)Source(LootSource, "config");
            var alarm = Module("AlarmTrap").OrderBy(c => c.transform.position.x).First();
            var exit = Object.FindAnyObjectByType<RunExitTrigger>();
            _session.StartRun();
            SetValue(config.ValueAtFullWeight);
            Assert.That((float)_motor.GetType().GetProperty("Burden").GetValue(_motor), Is.EqualTo(1f));
            float radius = _body.GetComponent<Collider>().bounds.extents.x;
            _body.position = new Vector3(alarm.GetComponent<Collider>().bounds.min.x - radius + 0.02f,
                _body.position.y, -4.5f);
            Physics.SyncTransforms();
            double alarmStart = -1d;
            _session.StateChanged += state =>
            {
                if (state == RunState.Alarm) alarmStart = Time.timeAsDouble;
            };
            int launches = 0;
            // Current level: trigger the alarm, then pass below the two wide columns.
            var route = new[] { new Vector3(62f, 0f, -6.5f), new Vector3(96.5f, 0f, -6.5f),
                new Vector3(exit.transform.position.x, 0f, exit.transform.position.z) };
            int waypoint = 0;
            double safetyDeadline = Time.timeAsDouble + 70d;
            while (_session.Result == null && Time.timeAsDouble < safetyDeadline && launches < 40)
            {
                var position = _body.position;
                position.y = 0f;
                while (waypoint < route.Length - 1 && Vector3.Distance(position, route[waypoint]) < 0.75f) waypoint++;
                var direction = route[waypoint] - _body.position;
                direction.y = 0f;
                Assert.That(direction.x, Is.GreaterThan(0f));
                float speed = config.MaxLaunchSpeed * config.FullWeightSpeedMultiplier;
                float minPower = new SerializedObject(_motor).FindProperty("minLaunchPower").floatValue;
                float targetSpeed = Mathf.Sqrt(2f * config.SlideDeceleration * direction.magnitude)
                    + config.SlideDeceleration * Time.fixedDeltaTime * 0.5f;
                float power = Mathf.Clamp01((targetSpeed / speed - minPower) / (1f - minPower));
                Assert.That(Launch(direction.normalized, power), Is.True);
                launches++;
                double stopDeadline = Time.timeAsDouble + 4d;
                while (_player.IsMoving && _session.Result == null && Time.timeAsDouble < stopDeadline)
                    yield return null;
                Assert.That(_player.IsMoving && _session.Result == null, Is.False);
                Assert.That(Loot.CurrentValue, Is.GreaterThanOrEqualTo(config.ValueAtFullWeight), "Route cut the bag.");
                // EditMode Runner does not honour WaitForSeconds after EnterPlayMode.
                double aimUntil = Time.timeAsDouble + 1d;
                while (_session.Result == null && Time.timeAsDouble < aimUntil) yield return null;
            }
            Assert.That(alarmStart, Is.GreaterThanOrEqualTo(0d), "Physical alarm contact did not occur.");
            Assert.That(_session.State, Is.EqualTo(RunState.Won), $"Stopped at {_body.position}; exit {exit.transform.position}.");
            double elapsed = Time.timeAsDouble - alarmStart;
            Debug.Log($"BALANCE full-bag run {attempt + 1}: {elapsed:F2}s, {launches} launches, value {Loot.CurrentValue}, aim 1s, fixed dt {Time.fixedDeltaTime}.");
            Assert.That(_session.RemainingTime, Is.GreaterThanOrEqualTo(5f),
                "Full bag must leave at least five seconds within this level's alarm budget.");
        }
    }

    [UnityTest, Explicit("Measured ordinary aiming pace; opt in to this long balance benchmark.")]
    public IEnumerator Balance_OrdinaryLootRouteCompletesWithinBudget()
    {
        yield return null;
        _session.StartRun();
        double started = Time.timeAsDouble;
        var config = (GameConfig)Source(LootSource, "config");
        var route = new[] { new Vector3(10f, 0f, -2f), new Vector3(12f, 0f, -1f),
            new Vector3(14f, 0f, 0f), new Vector3(16f, 0f, 1f), new Vector3(18f, 0f, 2f),
            new Vector3(28f, 0f, 3.47f), new Vector3(39f, 0f, 3f), new Vector3(43f, 0f, -3f),
            new Vector3(64f, 0f, -3f), new Vector3(101f, 0f, -3f) };
        int waypoint = 0, launches = 0;
        while (_session.Result == null && Time.timeAsDouble - started < 300d && launches < 80)
        {
            while (waypoint < route.Length - 1 && _body.position.x >= route[waypoint].x - 0.5f) waypoint++;
            Vector3 direction = route[waypoint] - _body.position;
            direction.y = 0f;
            Assert.That(direction.x, Is.GreaterThan(0f));
            float speed = config.MaxLaunchSpeed * Mathf.Lerp(1f, config.FullWeightSpeedMultiplier, Loot.NormalizedWeight);
            float power = Mathf.Clamp01(Mathf.Sqrt(2f * config.SlideDeceleration * direction.magnitude) / speed);
            Assert.That(Launch(direction.normalized, power), Is.True);
            launches++;
            double stopDeadline = Time.timeAsDouble + 5d;
            while (_player.IsMoving && _session.Result == null && Time.timeAsDouble < stopDeadline) yield return null;
            Assert.That(_player.IsMoving && _session.Result == null, Is.False);
            double aimUntil = Time.timeAsDouble + (_session.HasAlarmTriggered ? 1.2d : 4.5d);
            while (_session.Result == null && Time.timeAsDouble < aimUntil) yield return null;
        }
        double elapsed = Time.timeAsDouble - started;
        Debug.Log($"BALANCE ordinary route: {elapsed:F2}s, {launches} launches, value {Loot.CurrentValue}, aim 4.5s / alarm 1.2s.");
        Assert.That(_session.State, Is.EqualTo(RunState.Won));
        Assert.That(_session.HasAlarmTriggered, Is.True);
        Assert.That(Loot.CurrentValue, Is.GreaterThan(0));
        Assert.That(elapsed, Is.LessThan(300d));
    }

    [Test]
    public void SavedScene_UsesRealPrefabAndSharedCameraAndLootSource()
    {
        Assert.That(Source(_session, "_playerSource"), Is.SameAs(_motor));
        Assert.That(Source(_session, "_alarmExit"), Is.SameAs(Object.FindAnyObjectByType<RunExitTrigger>().transform));
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
    public IEnumerator ThreeRestarts_ReloadSceneAndRestoreAttemptAndModuleLinks()
    {
        yield return null;
        var startPosition = _body.position;
        var cameraPosition = _camera.transform.position;
        var bag = LootSource;
        var bagVisual = BagVisual.transform;
        var bagPosition = bagVisual.position;
        var bagScale = bagVisual.localScale;
        int finished = 0;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            _session.Finished += result => finished++;
            if (attempt > 0) _session.StartRun();
            SetValue(750);
            Items[0].SendMessage("OnTriggerEnter", _body.GetComponent<Collider>());
            if (attempt > 0)
            {
                Assert.That(Launch(Vector3.right), Is.True);
                yield return new WaitForFixedUpdate();
            }
            if (attempt > 0)
            {
                _session.TriggerAlarm();
                Assert.That(_session.HasAlarmTriggered, Is.True);
                Assert.That(_session.RemainingTime, Is.GreaterThan(0f));
            }
            if (attempt == 2)
            {
                typeof(RunSession).GetField("_alarmDeadline",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(_session, Time.timeAsDouble - 1d);
                _session.SendMessage("Update");
                Assert.That(_session.State, Is.EqualTo(RunState.Lost));
            }
            var hiddenObject = GameObject.Find("GameRoot/MuseumLevel/loot (1)");
            hiddenObject.SetActive(false);
            var alarm = GameObject.Find("GameRoot/MuseumLevel").transform
                .Find("Generated_Track/AlarmZone").gameObject;
            var alarmComponent = alarm.GetComponents<MonoBehaviour>().Single(c => c.GetType().Name == "AlarmTrap");
            alarmComponent.GetType().GetMethod("OnTriggerEnter",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(alarmComponent, new object[] { _body.GetComponent<Collider>() });

            var oldSession = _session;
            var oldBody = _body;
            var oldBag = bag;
            oldSession.RestartRun();
            oldSession.RestartRun();
            oldSession.StartRun();
            oldSession.ReachExit();
            Assert.That(Launch(Vector3.right), Is.False);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (oldSession != null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(oldSession == null, Is.True, "Restart did not unload the old scene.");
            yield return null;
            Assert.That(oldBody == null && oldBag == null, Is.True);

            _session = Object.FindAnyObjectByType<RunSession>();
            _motor = Object.FindObjectsByType<MonoBehaviour>()
                .Single(component => component.GetType().FullName == "FunFlow.Player.PlayerMotor");
            _player = (IPlayerControl)_motor;
            _body = _motor.GetComponent<Rigidbody>();
            _camera = Object.FindAnyObjectByType<CorridorCamera>();
            bag = LootSource;
            bagVisual = BagVisual.transform;
            Assert.That(Object.FindObjectsByType<RunSession>().Length, Is.EqualTo(1));
            Assert.That(_session.State, Is.EqualTo(RunState.Waiting));
            Assert.That(_session.HasAlarmTriggered, Is.False);
            Assert.That(_session.RemainingTime, Is.Zero);
            Assert.That(_session.Result, Is.Null);
            Assert.That(_player.IsMoving, Is.False);
            Assert.That(Launch(Vector3.right), Is.False);
            Assert.That(Loot.CurrentValue, Is.Zero);
            Assert.That(Loot.NormalizedWeight, Is.Zero);
            Assert.That(Items.Length, Is.EqualTo(PrefabItemCount));
            Assert.That(Items.All(item => item.gameObject.activeSelf), Is.True);
            Assert.That(_body.position, Is.EqualTo(startPosition));
            Assert.That(_camera.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(bagVisual.position, Is.EqualTo(bagPosition));
            Assert.That(bagVisual.localScale, Is.EqualTo(bagScale));
            Assert.That(Source(_session, "_playerSource"), Is.SameAs(_motor));
            Assert.That(Source(_session, "_lootSource"), Is.SameAs(bag));
            Assert.That(Source(_motor, "lootSource"), Is.SameAs(bag));
            Assert.That(GameObject.Find("GameRoot/MuseumLevel/loot (1)"), Is.Not.Null);
            var resetAlarm = GameObject.Find("GameRoot/MuseumLevel").transform
                .Find("Generated_Track/AlarmZone").gameObject
                .GetComponents<MonoBehaviour>().Single(component => component.GetType().Name == "AlarmTrap");
            Assert.That(resetAlarm.GetType().GetField("_isUsed",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(resetAlarm), Is.EqualTo(false));
        }
        Assert.That(finished, Is.EqualTo(1));
        _session.Finished += result => finished++;
        _session.StartRun();
        _session.ReachExit();
        Assert.That(finished, Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator ThreeLaunches_RejectMovingAndBackwardInputAndFollowPlayer()
    {
        int launches = 0;
        _player.Launched += () => launches++;
        Assert.That(Launch(Vector3.right), Is.False);
        _session.StartRun();
        float height = _body.position.y;
        Vector3 cameraOffset = new SerializedObject(_camera).FindProperty("_offset").vector3Value;
        Quaternion cameraRotation = _camera.transform.rotation;
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
            Assert.That(_camera.transform.position.x, Is.EqualTo(_body.position.x + cameraOffset.x).Within(0.01f));
            Assert.That(_camera.transform.position.y, Is.EqualTo(cameraOffset.y).Within(0.01f));
            Assert.That(_camera.transform.position.z, Is.EqualTo(cameraOffset.z).Within(0.01f));
            Assert.That(Quaternion.Angle(_camera.transform.rotation, cameraRotation), Is.LessThan(0.01f));
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
    public IEnumerator RealLoot_WeightShortensActualSlideButStillAllowsMovement()
    {
        // Measure full slides without finishing at the short route's exit.
        // Exit contacts and scene reloads have their own integration tests.
        Object.FindAnyObjectByType<RunExitTrigger>().gameObject.SetActive(false);
        var loot = Loot;
        var config = (GameConfig)Source(_motor, "config");
        Assert.That(Source(LootSource, "config"), Is.SameAs(config));
        foreach (var item in Items) item.gameObject.SetActive(false);
        _session.StartRun();
        var origin = new Vector3(-100f, 0.5f, -100f);
        _body.useGravity = false; // Open lane without level obstacles or pickups affecting the measurement.
        float emptyDistance = 0f;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            SetValue(attempt == 0 ? 0 : config.ValueAtFullWeight);
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
            if (attempt == 0)
            {
                emptyDistance = distance;
                Assert.That(distance, Is.InRange(15f, 16f), "Empty maximum launch should cross the 15 m corridor.");
            }
            else
            {
                Assert.That(distance, Is.LessThan(emptyDistance));
                Assert.That(distance, Is.InRange(3.5f, 4.2f), "Full bag must still make useful progress.");
            }
        }
        SetValue(0);
        _body.position = origin;
        float angle = new SerializedObject(_motor).FindProperty("maxLaunchAngle").floatValue * Mathf.Deg2Rad;
        Assert.That(Launch(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)), 1f), Is.True);
        float transverseDeadline = Time.realtimeSinceStartup + 8f;
        while (_player.IsMoving && Time.realtimeSinceStartup < transverseDeadline) yield return null;
        Assert.That(_player.IsMoving, Is.False);
        Assert.That(_body.position.z - origin.z, Is.InRange(14.5f, 15.5f), "Maximum angled launch must cross the corridor.");
        SetValue(config.ValueAtFullWeight * 2);
        Assert.That(loot.NormalizedWeight, Is.EqualTo(1f));
        Assert.That(Launch(Vector3.right, 1f), Is.True);
        Assert.That(_body.linearVelocity.x, Is.GreaterThan(0f));
    }

    [TestCase(0.5f, 0.866025f, true)]
    [TestCase(0.6f, 0.8f, true)]
    [TestCase(1f, 0f, false)]
    [TestCase(0.99f, 0.14f, false)]
    public void LowRimContact_ReflectsEdgeButIgnoresFloor(float up, float horizontal, bool wall)
    {
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var args = new object[] { new Vector3(0f, up, horizontal).normalized, Vector3.zero };
        bool accepted = (bool)_motor.GetType().GetMethod("TryWallNormal", flags).Invoke(null, args);
        Assert.That(accepted, Is.EqualTo(wall));
        if (wall) Assert.That(Vector3.Distance((Vector3)args[1], Vector3.forward), Is.LessThan(0.001f));
    }

    [TestCase(0f, -1f)]
    [TestCase(-0.4f, -0.916515f)]
    public void SideWallReflection_RetainsEightyPercentWithoutDistanceCap(float normalX, float normalZ)
    {
        SetValue(0);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var velocity = new Vector3(1f, 0f, 12f);
        var normal = new Vector3(normalX, 0f, normalZ).normalized;
        _motor.GetType().GetField("stepVelocity", flags).SetValue(_motor, velocity);
        _motor.GetType().GetField("impactNormalSum", flags).SetValue(_motor, normal);
        _motor.GetType().GetField("impactContacts", flags).SetValue(_motor, 1);
        var result = (Vector3)_motor.GetType().GetMethod("ResolveImpact", flags).Invoke(_motor, null);
        Assert.That(Vector3.Distance(result, Vector3.Reflect(velocity, normal) * 0.8f), Is.LessThan(0.001f));
        var config = (GameConfig)Source(_motor, "config");
        Assert.That(result.magnitude, Is.GreaterThan(Mathf.Sqrt(2f * config.SlideDeceleration * config.MaxBackwardRecoilDistance)));
    }

    [UnityTest]
    public IEnumerator NewSolidWall_AllowsShortRecoilButNotBackwardInput()
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            wall.transform.position = new Vector3(-97f, 0.5f, -100f);
            wall.transform.localScale = new Vector3(0.3f, 2f, 10f);
            _body.useGravity = false;
            _body.position = new Vector3(-100f, 0.5f, -100f);
            SetValue(0);
            _session.StartRun();
            Physics.SyncTransforms();
            Assert.That(Launch(Vector3.left, 1f), Is.False);
            Assert.That(Launch(Vector3.right, 1f), Is.True);
            bool movedBackward = false;
            float furthestX = _body.position.x;
            float deadline = Time.realtimeSinceStartup + 8f;
            while (_player.IsMoving && Time.realtimeSinceStartup < deadline)
            {
                furthestX = Mathf.Max(furthestX, _body.position.x);
                movedBackward |= _body.linearVelocity.x < -0.01f;
                yield return null;
            }
            Assert.That(_player.IsMoving, Is.False);
            Assert.That(movedBackward, Is.True, "Fresh solid walls must rebound without special wiring or material.");
            var config = (GameConfig)Source(_motor, "config");
            Assert.That(furthestX - _body.position.x, Is.InRange(0.05f, config.MaxBackwardRecoilDistance + 0.1f));
            Assert.That(Launch(Vector3.left), Is.False);
            Assert.That(Launch(Vector3.right), Is.True, "Forward re-aiming must work after recoil stops.");
        }
        finally { Object.Destroy(wall); }
    }

    [UnityTest]
    public IEnumerator RealLoot_BagFollowsAndGrowsAndResultKeepsSnapshot()
    {
        var loot = Loot;
        var config = (GameConfig)Source(LootSource, "config");
        var bag = BagVisual.transform;
        Assert.That(Source(BagVisual, "target"), Is.SameAs(_motor.transform));
        Assert.That(Source(BagVisual, "inventorySource"), Is.SameAs(LootSource));
        Assert.That(bag.GetComponent<Collider>().isTrigger, Is.True);
        SetValue(0);
        var emptyScale = bag.localScale;
        int changes = 0;
        System.Action onChange = () =>
        {
            changes++;
            Assert.That(loot.CurrentValue, Is.EqualTo(config.ValueAtFullWeight));
            Assert.That(loot.NormalizedWeight, Is.EqualTo(1f));
        };
        loot.Changed += onChange;
        SetValue(config.ValueAtFullWeight);
        loot.Changed -= onChange;
        Assert.That(changes, Is.EqualTo(1));
        Assert.That(bag.localScale.x, Is.GreaterThan(emptyScale.x));
        _session.StartRun();
        Assert.That(Launch(Vector3.right), Is.True);
        yield return new WaitForFixedUpdate();
        yield return null;
        // The bag hangs on a rope with its own inertia: it stays behind the thief, within the rope length.
        var rope = bag.GetComponents<MonoBehaviour>().Single(component => component.GetType().Name == "BagRope");
        float ropeLength = new SerializedObject(rope).FindProperty("length").floatValue;
        Assert.That(bag.position.x, Is.LessThan(_motor.transform.position.x));
        Assert.That(Vector3.Distance(bag.position, _motor.transform.position), Is.LessThanOrEqualTo(ropeLength + 0.01f));
        _session.ReachExit();
        SetValue(0);
        _session.ReachExit();
        Assert.That(_session.Result.Value.FinalValue, Is.EqualTo(config.ValueAtFullWeight));
        Assert.That(bag.localScale, Is.EqualTo(emptyScale));
    }

    [UnityTest]
    public IEnumerator FullLevel_CollectsEachItemOnceAndFinishesWithRealValue()
    {
        yield return null;
        Assert.That(LootSource.GetType().Name, Is.EqualTo("LootInventory"));
        Assert.That(Object.FindAnyObjectByType<CoreModuleProbe>(), Is.Null);
        var items = Items;
        Assert.That(items.Length, Is.EqualTo(PrefabItemCount));
        int totalValue = items.Sum(item => new SerializedObject(item).FindProperty("value").intValue);
        Assert.That(items.All(item => Source(item, "inventory") == LootSource), Is.True);
        int changes = 0, finishes = 0;
        Loot.Changed += () => changes++;
        _session.Finished += result => finishes++;
        _session.StartRun();
        foreach (var item in items)
        {
            _body.position = item.transform.position;
            _body.WakeUp();
            Physics.SyncTransforms();
            float contactDeadline = Time.realtimeSinceStartup + 2f;
            while (item.gameObject.activeSelf && Time.realtimeSinceStartup < contactDeadline)
                yield return new WaitForFixedUpdate();
            Assert.That(item.gameObject.activeSelf, Is.False, $"No pickup contact with {item.name}.");
        }
        var exit = Object.FindAnyObjectByType<RunExitTrigger>();
        _body.position = exit.transform.position;
        _body.WakeUp();
        Physics.SyncTransforms();
        float exitDeadline = Time.realtimeSinceStartup + 2f;
        while (_session.State == RunState.Playing && Time.realtimeSinceStartup < exitDeadline)
            yield return new WaitForFixedUpdate();
        Assert.That(_session.State, Is.EqualTo(RunState.Won));
        Assert.That(Loot.CurrentValue, Is.EqualTo(totalValue));
        Assert.That(_session.Result.Value.FinalValue, Is.EqualTo(totalValue));
        Assert.That(items.All(item => !item.gameObject.activeSelf), Is.True);
        foreach (var item in items)
            item.GetType().GetMethod("OnTriggerEnter", System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance).Invoke(item, new object[] { _body.GetComponent<Collider>() });
        _session.ReachExit();
        Assert.That(Loot.CurrentValue, Is.EqualTo(totalValue));
        Assert.That(changes, Is.EqualTo(items.Length));
        Assert.That(finishes, Is.EqualTo(1));
        Assert.That(Launch(Vector3.right), Is.False);
    }

    [UnityTest]
    public IEnumerator HudButtons_ThreeRestartsKeepSourcesScoreAndResultInSync()
    {
        string Text(Object component) => (string)component.GetType().GetProperty("text").GetValue(component);
        MonoBehaviour Component(string name) => Object.FindObjectsByType<MonoBehaviour>()
            .Single(c => c.GetType().Name == name);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            yield return null;
            var binder = Component("PresentationBinder");
            Assert.That(Source(binder, "runSessionSource"), Is.SameAs(_session));
            Assert.That(Source(binder, "lootSource"), Is.SameAs(LootSource));
            Assert.That(Source(binder, "playerSource"), Is.SameAs(_motor));
            Assert.That(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Length, Is.EqualTo(1));
            var handlers = (System.Delegate)typeof(RunSession).GetField("Finished",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(_session);
            Assert.That(handlers.GetInvocationList().Length, Is.EqualTo(1));
            var screens = Component("HudScreens");
            var button = (UnityEngine.UI.Button)Source(screens, "primaryButton");
            button.onClick.Invoke();
            button.onClick.Invoke();
            Assert.That(_session.State, Is.EqualTo(RunState.Playing));
            _session.TriggerAlarm();
            _session.TriggerAlarm();
            Assert.That((bool)Field(Component("PresentationController"), "alarmAnnounced"), Is.True);
            Assert.That(Text(Source(Component("HudView"), "alarmValue")), Does.Match(@"^\d{2,}:[0-5]\d$"));
            SetValue(750);
            float counterDeadline = Time.realtimeSinceStartup + 2f;
            while (!Text(Source(Component("HudView"), "lootValue")).StartsWith("750")
                && Time.realtimeSinceStartup < counterDeadline) yield return null;
            Assert.That(Text(Source(Component("HudView"), "lootValue")), Does.Contain("750"));
            _session.ReachExit();
            Assert.That(Text(Source(screens, "titleText")), Does.Contain("750"));
            SetValue(125); // Late Loot notifications must not rewrite the result snapshot.
            Assert.That(_session.Result.Value.FinalValue, Is.EqualTo(750));
            Assert.That(Text(Source(screens, "titleText")), Does.Contain("750"));
            var previous = _session;
            button.onClick.Invoke();
            button.onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (previous != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(previous == null, Is.True);
            yield return null;
            _session = Object.FindAnyObjectByType<RunSession>();
            _motor = (MonoBehaviour)Source(_session, "_playerSource");
            _player = (IPlayerControl)_motor;
            _body = _motor.GetComponent<Rigidbody>();
            _camera = Object.FindAnyObjectByType<CorridorCamera>();
            Assert.That(_session.State, Is.EqualTo(RunState.Waiting));
            Assert.That(_session.Result, Is.Null);
            Assert.That(Loot.CurrentValue, Is.Zero);
            Assert.That(Text(Source(Component("HudView"), "lootValue")), Does.StartWith("0"));
            Assert.That((bool)Field(Component("PresentationController"), "alarmAnnounced"), Is.False);
            Assert.That(Text(Source(Component("HudView"), "alarmValue")), Is.EqualTo("Тихо"));
            Assert.That(Object.FindObjectsByType<AudioSource>().All(s => !s.isPlaying), Is.True);
            Assert.That(GameObject.Find("PresentationParticles"), Is.Null);
        }
    }

    [UnityTest]
    public IEnumerator HudMouseClick_StartsThroughInputSystemUiModule()
    {
        yield return null;
        var button = Object.FindAnyObjectByType<UnityEngine.UI.Button>();
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)button.transform;
        Vector2 point = rect.TransformPoint(rect.rect.center);
        var mouse = InputSystem.AddDevice<Mouse>();
        try
        {
            foreach (bool down in new[] { false, true, false })
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = (ushort)(down ? 1 : 0) });
                InputSystem.Update();
                yield return null;
            }
            Assert.That(_session.State, Is.EqualTo(RunState.Playing));
            Assert.That(_player.IsMoving, Is.False);
            Assert.That(_body.linearVelocity.sqrMagnitude, Is.Zero);
        }
        finally { InputSystem.RemoveDevice(mouse); }
    }

    [UnityTest]
    public IEnumerator HudPointerGesture_CannotAimOrLaunchAfterLeavingUi()
    {
        yield return null;
        var guard = Object.FindAnyObjectByType<UiLaunchGuard>();
        var input = (MonoBehaviour)Source(guard, "_launchInput");
        var screens = Object.FindObjectsByType<MonoBehaviour>().Single(c => c.GetType().Name == "HudScreens");
        var button = (UnityEngine.UI.Button)Source(screens, "primaryButton");
        Canvas.ForceUpdateCanvases();
        Vector2 ui = ((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center);
        var mouse = InputSystem.AddDevice<Mouse>();
        IEnumerator Pointer(Vector2 position, bool down)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = (ushort)(down ? 1 : 0) });
            InputSystem.Update();
            yield return null; // Let the UI guard and input run in their actual frame order.
        }
        try
        {
            yield return Pointer(ui, true);
            Assert.That(input.enabled, Is.False);
            button.onClick.Invoke(); // Start while the mouse is still held on UI.
            Vector2 ball = _camera.GetComponent<Camera>().WorldToScreenPoint(_motor.transform.position);
            Vector2 pull = _camera.GetComponent<Camera>().WorldToScreenPoint(_motor.transform.position - Vector3.right * 3f);
            yield return Pointer(ball, true);
            Assert.That(input.enabled, Is.False);
            yield return Pointer(pull, false);
            Assert.That(_player.IsMoving, Is.False);
            yield return Pointer(ball, false);
            Assert.That(input.enabled, Is.True);
            yield return Pointer(ball, true);
            Assert.That((bool)input.GetType().GetProperty("IsAiming").GetValue(input), Is.True);
            yield return Pointer(pull, true);
            Assert.That(input.enabled, Is.True, "The gameplay drag must remain outside UI.");
            Assert.That((bool)input.GetType().GetProperty("HasLaunchableAim").GetValue(input), Is.True);
            yield return Pointer(pull, false);
            Assert.That(_player.IsMoving, Is.True, "Normal gameplay must still launch.");
        }
        finally { InputSystem.RemoveDevice(mouse); }
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
            Object.FindAnyObjectByType<UiLaunchGuard>().SendMessage("Update");
            if (input.enabled) input.SendMessage("Update");
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
