using System;
using UnityEngine;

public class TEMP_FakeRunSession : MonoBehaviour, IRunSession, IRunPause
{
    public RunState State { get; private set; } = RunState.Waiting;
    public bool HasAlarmTriggered { get; private set; }
    public float RemainingTime { get; private set; }
    public RunResult? Result { get; private set; }
    public bool IsPaused { get; private set; }

    public event Action<RunState> StateChanged;
    public event Action<float> RemainingTimeChanged;
    public event Action<RunResult> Finished;
    public event Action<bool> PauseChanged;

    [ContextMenu("Test/Waiting")]
    public void SetWaiting() => SetState(RunState.Waiting);

    [ContextMenu("Test/Playing")]
    public void SetPlaying() => SetState(RunState.Playing);

    [ContextMenu("Test/Alarm")]
    public void SetAlarm() => SetState(RunState.Alarm);

    [ContextMenu("Test/Won")]
    public void SetWon() => SetState(RunState.Won);

    [ContextMenu("Test/Lost")]
    public void SetLost() => SetState(RunState.Lost);

    [ContextMenu("Test/Toggle Pause")]
    public void TogglePause() => SetPaused(!IsPaused);

    public void StartRun() => SetPlaying();
    public void RestartRun() => SetWaiting();
    public void TriggerAlarm() => SetAlarm();
    public void ReachExit() { }

    public void SetPaused(bool paused)
    {
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        PauseChanged?.Invoke(paused);
    }

    private void SetState(RunState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }
}