using System;

public interface IRunSession
{
    RunState State { get; }
    bool HasAlarmTriggered { get; }
    float RemainingTime { get; }

    // Null until the attempt has finished.
    RunResult? Result { get; }

    event Action<RunState> StateChanged;
    event Action<float> RemainingTimeChanged;
    event Action<RunResult> Finished;

    void StartRun();
    void RestartRun();
    void TriggerAlarm();
    void ReachExit();
}
