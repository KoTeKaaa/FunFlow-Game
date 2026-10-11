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
    private const string LinkError = "RunSession: assign Player Source (IPlayerControl) and Loot Source (ILootInventory) in Inspector.";

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        _root = new GameObject("Core test");
        _root.SetActive(false);
        _probe = _root.AddComponent<CoreModuleProbe>();
        _session = _root.AddComponent<RunSession>();
        Assign("_playerSource", _probe);
        Assign("_lootSource", _probe);
    }

    private void Assign(string field, Object source)
    {
        var serialized = new SerializedObject(_session);
        serialized.FindProperty(field).objectReferenceValue = source;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Object.DestroyImmediate(_root);
        yield return new ExitPlayMode();
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
