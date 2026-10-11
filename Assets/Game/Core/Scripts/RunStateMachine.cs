// Pure attempt logic. Scene wiring and notifications belong to the Core component.
public sealed class RunStateMachine
{
    public RunState State { get; private set; } = RunState.Waiting;
    public bool HasAlarmTriggered { get; private set; }
    public RunResult? Result { get; private set; }

    public bool StartRun()
    {
        if (State != RunState.Waiting)
        {
            return false;
        }

        State = RunState.Playing;
        return true;
    }

    public bool TriggerAlarm()
    {
        if (State != RunState.Playing)
        {
            return false;
        }

        HasAlarmTriggered = true;
        State = RunState.Alarm;
        return true;
    }

    // The caller supplies a snapshot; current loot and the timer live elsewhere.
    public bool FinishRun(RunResult result)
    {
        if (State != RunState.Playing && State != RunState.Alarm)
        {
            return false;
        }

        if (!result.IsVictory && State != RunState.Alarm)
        {
            return false;
        }

        Result = result;
        State = result.IsVictory ? RunState.Won : RunState.Lost;
        return true;
    }
}
