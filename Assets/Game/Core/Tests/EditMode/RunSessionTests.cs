using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class RunSessionTests
{
    private GameObject _root;
    private RunSession _session;
    private CoreModuleProbe _probe;
    private GameConfig _config;
    private const string LinkError = "RunSession: assign Player Source (IPlayerControl) and Loot Source (ILootInventory) in Inspector.";

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
            UnityEditor.SceneManagement.NewSceneMode.Single);
        yield return new EnterPlayMode();
        _root = new GameObject("Core test");
        _root.SetActive(false);
        _probe = _root.AddComponent<CoreModuleProbe>();
        _session = _root.AddComponent<RunSession>();
        Assign("_playerSource", _probe);
        Assign("_lootSource", _probe);
        _config = ScriptableObject.CreateInstance<GameConfig>();
        Assign("_config", _config);
    }

    private void Assign(string field, Object source)
    {
        var serialized = new SerializedObject(_session);
        serialized.FindProperty(field).objectReferenceValue = source;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    [TestCase(-10f, 60f)]
    [TestCase(45f, 60f)]
    [TestCase(100f, 120f)]
    [TestCase(200f, 240f)]
    public void AlarmBudget_ScalesForLongerLevels(float distance, float expected)
    {
        Assert.That(_config.GetAlarmDurationSeconds(distance), Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void AlarmBudget_UsesRemainingDistanceOnce()
    {
        var exit = new GameObject("Test exit");
        try
        {
            exit.transform.position = new Vector3(200f, 0f, 0f);
            Assign("_alarmExit", exit.transform);
            _root.transform.position = new Vector3(100f, 0f, 0f);
            _root.SetActive(true);
            _session.StartRun();
            _session.TriggerAlarm();
            Assert.That(_session.RemainingTime, Is.EqualTo(120f).Within(0.001f));
            double deadline = Deadline;
            exit.transform.position = new Vector3(400f, 0f, 0f);
            _session.TriggerAlarm();
            Assert.That(Deadline, Is.EqualTo(deadline));
        }
        finally { Object.DestroyImmediate(exit); }
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Object.DestroyImmediate(_root);
        Object.DestroyImmediate(_config);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator Pause_FreezesAlarmAndBlocksFinishUntilResume()
    {
        _root.SetActive(true);
        _session.StartRun();
        _session.TriggerAlarm();
        float originalScale = Time.timeScale;
        float remaining = _session.RemainingTime;
        _session.SetPaused(true);
        Assert.That(_session.IsPaused, Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(_probe.ControlEnabled, Is.False);
        yield return new WaitForSecondsRealtime(0.1f);
        _session.SendMessage("Update");
        _session.ReachExit();
        Assert.That(_session.State, Is.EqualTo(RunState.Alarm));
        Assert.That(_session.RemainingTime, Is.EqualTo(remaining));
        Assert.That(_session.Result, Is.Null);
        _session.SetPaused(false);
        Assert.That(Time.timeScale, Is.EqualTo(originalScale));
        Assert.That(_probe.ControlEnabled, Is.True);
        _session.ReachExit();
        Assert.That(_session.State, Is.EqualTo(RunState.Won));
    }

    [Test]
    public void Pause_DisablingSessionRestoresTimeScale()
    {
        _root.SetActive(true);
        float originalScale = Time.timeScale;
        _session.SetPaused(true);
        Assert.That(_session.IsPaused, Is.False, "A waiting run cannot be paused.");
        _session.StartRun();
        _session.SetPaused(true);
        _root.SetActive(false);
        Assert.That(_session.IsPaused, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(originalScale));
        Assert.That(_probe.ControlEnabled, Is.False);
    }

    [Test]
    public void Pause_ResumeRestoresPreviousTimeScale()
    {
        _root.SetActive(true);
        _session.StartRun();
        float originalScale = Time.timeScale;
        try
        {
            Time.timeScale = 0.5f;
            _session.SetPaused(true);
            _session.SetPaused(true);
            _session.SetPaused(false);
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
            Assert.That(_probe.ControlEnabled, Is.True);
        }
        finally { _session.SetPaused(false); Time.timeScale = originalScale; }
    }

    [Test]
    public void Alarm_StartsOnceAndPublishesConsistentProperties()
    {
        _root.SetActive(true);
        _session.TriggerAlarm();
        _session.SendMessage("Update");
        Assert.That(_session.RemainingTime, Is.Zero);
        Assert.That(_session.HasAlarmTriggered, Is.False);
        _session.StartRun();
        _session.SendMessage("Update");
        Assert.That(_session.RemainingTime, Is.Zero);
        int changes = 0;
        _session.StateChanged += state =>
        {
            if (state != RunState.Alarm) return;
            Assert.That(_session.HasAlarmTriggered, Is.True);
            Assert.That(_session.RemainingTime, Is.EqualTo(_config.AlarmDurationSeconds));
            Assert.That(_probe.ControlEnabled, Is.True);
        };
        _session.RemainingTimeChanged += time =>
        {
            changes++;
            Assert.That(time, Is.EqualTo(_session.RemainingTime));
            Assert.That(_session.State, Is.EqualTo(RunState.Alarm));
        };
        _session.TriggerAlarm();
        double deadline = Deadline;
        Tick(deadline - 10d, false);
        _session.TriggerAlarm();
        Assert.That(Deadline, Is.EqualTo(deadline));
        Assert.That(_session.RemainingTime, Is.EqualTo(10f));
        Assert.That(changes, Is.EqualTo(2));
    }

    private double Deadline => (double)typeof(RunSession).GetField("_alarmDeadline",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(_session);

    private void Tick(double time, bool exit)
    {
        typeof(RunSession).GetMethod("RefreshAlarm",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(_session, new object[] { time, exit });
    }

    [TestCase(-0.001d, false)]
    [TestCase(-0.001d, true)]
    [TestCase(0d, false)]
    [TestCase(0d, true)]
    [TestCase(0.001d, false)]
    [TestCase(0.001d, true)]
    public void Alarm_ExitAndTimerUseSameDeadlineInEitherOrder(double offset, bool timerFirst)
    {
        _root.SetActive(true);
        _session.StartRun();
        _probe.SetTestValue(75);
        _session.TriggerAlarm();
        int finishes = 0;
        _session.StateChanged += state =>
        {
            Assert.That(_session.Result.HasValue, Is.True);
            Assert.That(_probe.ControlEnabled, Is.False);
            if (state == RunState.Lost) Assert.That(_session.RemainingTime, Is.Zero);
        };
        _session.Finished += result => finishes++;
        double now = Deadline + offset;
        Tick(now, !timerFirst);
        Tick(now, timerFirst);
        Assert.That(_session.State, Is.EqualTo(offset <= 0d ? RunState.Won : RunState.Lost));
        Assert.That(_session.Result.Value.FinalValue, Is.EqualTo(75));
        float remaining = _session.RemainingTime;
        _probe.SetTestValue(100);
        Tick(now + 20d, false);
        _session.TriggerAlarm();
        _session.ReachExit();
        Assert.That(_session.HasAlarmTriggered, Is.True);
        Assert.That(_session.RemainingTime, Is.EqualTo(remaining));
        Assert.That(_session.Result.Value.FinalValue, Is.EqualTo(75));
        Assert.That(finishes, Is.EqualTo(1));
    }

    [Test]
    public void Alarm_PublicExitChecksDeadlineBeforeUpdate()
    {
        _root.SetActive(true);
        _session.StartRun();
        _session.TriggerAlarm();
        typeof(RunSession).GetField("_alarmDeadline",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(_session, Time.timeAsDouble - 1d);
        _session.ReachExit();
        Assert.That(_session.State, Is.EqualTo(RunState.Lost));
        Assert.That(_session.RemainingTime, Is.Zero);
    }

    [UnityTest]
    public IEnumerator Alarm_UpdateCountsDownAndFreezesAfterVictory()
    {
        _root.SetActive(true);
        _session.StartRun();
        _session.TriggerAlarm();
        yield return null;
        yield return null;
        Assert.That(_session.RemainingTime, Is.LessThan(_config.AlarmDurationSeconds).And.GreaterThan(0f));
        _session.ReachExit();
        float remaining = _session.RemainingTime;
        yield return null;
        Assert.That(_session.State, Is.EqualTo(RunState.Won));
        Assert.That(_session.RemainingTime, Is.EqualTo(remaining));
    }

    [Test]
    public void Alarm_UpdateExpiresOnceWithConsistentNotificationOrder()
    {
        _root.SetActive(true);
        _session.StartRun();
        _session.TriggerAlarm();
        string events = "";
        _session.StateChanged += state => events += "state ";
        _session.RemainingTimeChanged += time =>
        {
            Assert.That(_session.State, Is.EqualTo(RunState.Lost));
            Assert.That(_session.Result.Value.IsVictory, Is.False);
            Assert.That(_probe.ControlEnabled, Is.False);
            Assert.That(time, Is.Zero);
            events += "time ";
        };
        _session.Finished += result => events += "finish";
        typeof(RunSession).GetField("_alarmDeadline",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(_session, Time.timeAsDouble - 1d);
        _session.SendMessage("Update");
        _session.SendMessage("Update");
        Assert.That(events, Is.EqualTo("state time finish"));
    }

    [Test]
    public void Alarm_MissingConfigReportsErrorWithoutChangingAttempt()
    {
        Assign("_config", null);
        _root.SetActive(true);
        _session.StartRun();
        LogAssert.Expect(LogType.Error, "RunSession: assign GameConfig with a positive finite alarm duration.");
        _session.TriggerAlarm();
        Assert.That(_session.State, Is.EqualTo(RunState.Playing));
        Assert.That(_session.HasAlarmTriggered, Is.False);
    }

    [Test]
    public void StartAndExit_GateControlAndPublishOneConsistentSnapshot()
    {
        _root.SetActive(true);
        Assert.That(_probe.ControlEnabled, Is.False);
        _session.ReachExit();
        Assert.That(_session.Result, Is.Null);
        int stateEvents = 0, finishEvents = 0;
        _session.StateChanged += state =>
        {
            stateEvents++;
            Assert.That(_probe.ControlEnabled, Is.EqualTo(state == RunState.Playing));
            if (state == RunState.Won) Assert.That(_session.Result.Value.FinalValue, Is.EqualTo(75));
        };
        _session.Finished += result => { finishEvents++; Assert.That(result.FinalValue, Is.EqualTo(75)); };
        _session.StartRun();
        _session.StartRun();
        _probe.AddValue(75);
        _session.ReachExit();
        _probe.AddValue(25);
        _session.ReachExit();
        _session.StartRun();
        Assert.That(_session.Result.Value.FinalValue, Is.EqualTo(75));
        Assert.That(_session.Result.Value.IsVictory, Is.True);
        Assert.That(_probe.ControlEnabled, Is.False);
        Assert.That(stateEvents, Is.EqualTo(2));
        Assert.That(finishEvents, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator ExitTrigger_PhysicalContactFinishesOnceAndIgnoresOtherBodies()
    {
        _root.SetActive(true);
        _session.StartRun();
        _probe.SetTestValue(75);
        int finishes = 0;
        _session.Finished += result => finishes++;

        var player = new GameObject("Exit test player");
        player.transform.SetParent(_root.transform);
        player.layer = 8;
        var body = player.AddComponent<Rigidbody>();
        body.useGravity = false;
        player.AddComponent<SphereCollider>();
        var exit = new GameObject("Exit test trigger");
        exit.transform.SetParent(_root.transform);
        exit.SetActive(false);
        exit.layer = 15;
        exit.transform.position = new Vector3(3, 0, 0);
        exit.AddComponent<BoxCollider>().isTrigger = true;
        var trigger = exit.AddComponent<RunExitTrigger>();
        var links = new SerializedObject(trigger);
        links.FindProperty("_session").objectReferenceValue = _session;
        links.FindProperty("_player").objectReferenceValue = body;
        links.ApplyModifiedPropertiesWithoutUndo();
        exit.SetActive(true);

        var stranger = new GameObject("Other body on Player layer");
        stranger.transform.SetParent(_root.transform);
        stranger.layer = 8;
        stranger.transform.position = exit.transform.position;
        stranger.AddComponent<Rigidbody>().useGravity = false;
        stranger.AddComponent<SphereCollider>();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.That(_session.State, Is.EqualTo(RunState.Playing));
        Object.DestroyImmediate(stranger);

        for (int contact = 0; contact < 2; contact++)
        {
            body.position = exit.transform.position;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            body.position = Vector3.zero;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            _probe.SetTestValue(100);
        }
        Assert.That(_session.State, Is.EqualTo(RunState.Won));
        Assert.That(_session.Result.Value.FinalValue, Is.EqualTo(75));
        Assert.That(finishes, Is.EqualTo(1));
        Assert.That(_probe.ControlEnabled, Is.False);
    }

    [Test]
    public void RestartWithoutSavedScene_ReportsErrorAndKeepsAttempt()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.CreateScene("Unsaved run");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_root, scene);
        _root.SetActive(true);
        _session.StartRun();
        LogAssert.Expect(LogType.Error,
            "RunSession: Restart requires a saved scene enabled in Build Settings.");
        _session.RestartRun();
        Assert.That(_session.State, Is.EqualTo(RunState.Playing));
        Assert.That(_probe.ControlEnabled, Is.True);
    }

    [TestCase("_playerSource", false)]
    [TestCase("_lootSource", false)]
    [TestCase("_playerSource", true)]
    [TestCase("_lootSource", true)]
    public void InvalidSource_ReportsErrorAndRejectsStart(string field, bool wrongType)
    {
        Assign(field, wrongType ? _session : null);
        LogAssert.Expect(LogType.Error, LinkError);
        _root.SetActive(true);
        _session.StartRun();
        Assert.That(_session.enabled, Is.False);
        Assert.That(_session.State, Is.EqualTo(RunState.Waiting));
        Assert.That(_probe.ControlEnabled, Is.False);
    }

    [Test]
    public void DisableAndEnable_PreservesAttemptAndZeroValueVictory()
    {
        _root.SetActive(true);
        _session.StartRun();
        _session.enabled = false;
        Assert.That(_probe.ControlEnabled, Is.False);
        _session.enabled = true;
        Assert.That(_probe.ControlEnabled, Is.True);
        _session.ReachExit();
        Assert.That(_session.Result.Value.FinalValue, Is.Zero);
        Assert.That(_session.Result.Value.IsVictory, Is.True);
    }

    [Test]
    public void DestroyedLoot_StopsNewCommandsWithoutNullReference()
    {
        var loot = _root.AddComponent<CoreModuleProbe>();
        Assign("_lootSource", loot);
        _root.SetActive(true);
        _session.StartRun();
        Object.DestroyImmediate(loot);
        LogAssert.Expect(LogType.Error, LinkError);
        _session.ReachExit();
        Assert.That(_probe.ControlEnabled, Is.False);
        Assert.That(_session.Result, Is.Null);
    }
}
