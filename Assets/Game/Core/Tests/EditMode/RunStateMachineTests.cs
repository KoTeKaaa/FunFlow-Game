using NUnit.Framework;

public sealed class RunStateMachineTests
{
    [Test]
    public void StartRun_ChangesWaitingToPlayingOnlyOnce()
    {
        var run = new RunStateMachine();

        Assert.That(run.State, Is.EqualTo(RunState.Waiting));
        Assert.That(run.StartRun(), Is.True);
        Assert.That(run.State, Is.EqualTo(RunState.Playing));
        Assert.That(run.StartRun(), Is.False);
        Assert.That(run.State, Is.EqualTo(RunState.Playing));
        Assert.That(run.HasAlarmTriggered, Is.False);
        Assert.That(run.Result, Is.Null);
    }

    [Test]
    public void Waiting_IgnoresAlarmAndBothOutcomes()
    {
        var run = new RunStateMachine();

        Assert.That(run.TriggerAlarm(), Is.False);
        Assert.That(run.FinishRun(new RunResult(true, 10)), Is.False);
        Assert.That(run.FinishRun(new RunResult(false, 10)), Is.False);
        Assert.That(run.State, Is.EqualTo(RunState.Waiting));
        Assert.That(run.HasAlarmTriggered, Is.False);
        Assert.That(run.Result, Is.Null);
    }

    [Test]
    public void Alarm_IsAcceptedOnlyOnceAndRejectsStart()
    {
        var run = new RunStateMachine();
        run.StartRun();

        Assert.That(run.TriggerAlarm(), Is.True);
        Assert.That(run.State, Is.EqualTo(RunState.Alarm));
        Assert.That(run.HasAlarmTriggered, Is.True);
        Assert.That(run.TriggerAlarm(), Is.False);
        Assert.That(run.StartRun(), Is.False);
        Assert.That(run.State, Is.EqualTo(RunState.Alarm));
        Assert.That(run.Result, Is.Null);
    }

    [Test]
    public void Playing_RejectsDefeatBeforeAlarm()
    {
        var run = new RunStateMachine();
        run.StartRun();

        Assert.That(run.FinishRun(new RunResult(false, 10)), Is.False);
        Assert.That(run.State, Is.EqualTo(RunState.Playing));
        Assert.That(run.Result, Is.Null);
        Assert.That(run.TriggerAlarm(), Is.True);
    }

    [TestCase(false, true, RunState.Won)]
    [TestCase(true, true, RunState.Won)]
    [TestCase(true, false, RunState.Lost)]
    public void Finished_IgnoresAllCommandsAndKeepsFirstResult(
        bool alarm, bool victory, RunState expectedState)
    {
        var run = new RunStateMachine();
        run.StartRun();
        if (alarm)
        {
            run.TriggerAlarm();
        }

        Assert.That(run.FinishRun(new RunResult(victory, 42)), Is.True);
        Assert.That(run.State, Is.EqualTo(expectedState));
        Assert.That(run.Result.Value.IsVictory, Is.EqualTo(victory));
        Assert.That(run.Result.Value.FinalValue, Is.EqualTo(42));
        Assert.That(run.StartRun(), Is.False);
        Assert.That(run.TriggerAlarm(), Is.False);
        Assert.That(run.FinishRun(new RunResult(!victory, 99)), Is.False);
        Assert.That(run.FinishRun(new RunResult(victory, 0)), Is.False);
        Assert.That(run.State, Is.EqualTo(expectedState));
        Assert.That(run.HasAlarmTriggered, Is.EqualTo(alarm));
        Assert.That(run.Result.Value.IsVictory, Is.EqualTo(victory));
        Assert.That(run.Result.Value.FinalValue, Is.EqualTo(42));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void NewAttempt_HasCleanStateAfterFinishedAttempt(bool victory)
    {
        var previous = new RunStateMachine();
        previous.StartRun();
        previous.TriggerAlarm();
        previous.FinishRun(new RunResult(victory, 42));

        var next = new RunStateMachine();
        Assert.That(next.State, Is.EqualTo(RunState.Waiting));
        Assert.That(next.HasAlarmTriggered, Is.False);
        Assert.That(next.Result, Is.Null);
        Assert.That(next.StartRun(), Is.True);
        Assert.That(previous.Result.Value.FinalValue, Is.EqualTo(42));
    }
}
