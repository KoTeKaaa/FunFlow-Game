using System;

public interface IRunPause
{
    bool IsPaused { get; }
    event Action<bool> PauseChanged;
    void SetPaused(bool paused);
}
