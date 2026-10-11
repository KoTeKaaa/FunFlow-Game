using System;
using UnityEngine;

public sealed class RunSession : MonoBehaviour
{
    [SerializeField] private MonoBehaviour _playerSource;
    [SerializeField] private MonoBehaviour _lootSource;
    private readonly RunStateMachine _run = new RunStateMachine();
    private IPlayerControl _player;
    private ILootInventory _loot;

    public RunState State => _run.State;
    public RunResult? Result => _run.Result;
    public event Action<RunState> StateChanged;
    public event Action<RunResult> Finished;

    private void OnEnable()
    {
        if (ValidateSources())
            _player.SetControlEnabled(State == RunState.Playing || State == RunState.Alarm);
    }

    private void OnDisable()
    {
        if (_playerSource != null && _playerSource is IPlayerControl player)
            player.SetControlEnabled(false);
    }

    private bool ValidateSources()
    {
        _player = _playerSource != null ? _playerSource as IPlayerControl : null;
        _loot = _lootSource != null ? _lootSource as ILootInventory : null;
        if (_player != null && _loot != null)
            return true;

        _player?.SetControlEnabled(false);
        Debug.LogError("RunSession: assign Player Source (IPlayerControl) and Loot Source (ILootInventory) in Inspector.", this);
        enabled = false;
        return false;
    }

    [ContextMenu("Test Run/Start")]
    public void StartRun()
    {
        if (!isActiveAndEnabled || !ValidateSources() || !_run.StartRun())
            return;

        _player.SetControlEnabled(true);
        StateChanged?.Invoke(State);
    }

    [ContextMenu("Test Run/Reach Exit")]
    public void ReachExit()
    {
        if (!isActiveAndEnabled || (State != RunState.Playing && State != RunState.Alarm)
            || !ValidateSources())
            return;

        var result = new RunResult(true, _loot.CurrentValue);
        if (!_run.FinishRun(result))
            return;

        _player.SetControlEnabled(false);
        StateChanged?.Invoke(State);
        Finished?.Invoke(result);
    }
}
