using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RunSession : MonoBehaviour
{
    [SerializeField] private MonoBehaviour _playerSource;
    [SerializeField] private MonoBehaviour _lootSource;
    private readonly RunStateMachine _run = new RunStateMachine();
    private IPlayerControl _player;
    private ILootInventory _loot;
    private bool _restarting;

    public RunState State => _run.State;
    public RunResult? Result => _run.Result;
    public event Action<RunState> StateChanged;
    public event Action<RunResult> Finished;

    private void OnEnable()
    {
        if (ValidateSources())
            _player.SetControlEnabled(!_restarting && (State == RunState.Playing || State == RunState.Alarm));
    }

    private void OnDisable()
    {
        if (_playerSource != null && _playerSource is IPlayerControl player)
            player.SetControlEnabled(false);
    }

    private void OnDestroy()
    {
        StateChanged = null;
        Finished = null;
        _player = null;
        _loot = null;
    }

    [ContextMenu("Test Run/Restart")]
    public void RestartRun()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || _restarting)
            return;

        var scene = gameObject.scene;
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)
            || !Application.CanStreamedLevelBeLoaded(scene.path))
        {
            Debug.LogError("RunSession: Restart requires a saved scene enabled in Build Settings.", this);
            return;
        }

        _restarting = true;
        if (_playerSource != null && _playerSource is IPlayerControl player)
            player.SetControlEnabled(false);
        try
        {
            SceneManager.LoadSceneAsync(scene.path, LoadSceneMode.Single);
        }
        catch (Exception exception)
        {
            _restarting = false;
            if (_playerSource != null && _playerSource is IPlayerControl currentPlayer)
                currentPlayer.SetControlEnabled(State == RunState.Playing || State == RunState.Alarm);
            Debug.LogError($"RunSession: scene reload failed: {exception.Message}", this);
        }
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
        if (!isActiveAndEnabled || _restarting || !ValidateSources() || !_run.StartRun())
            return;

        _player.SetControlEnabled(true);
        StateChanged?.Invoke(State);
    }

    [ContextMenu("Test Run/Reach Exit")]
    public void ReachExit()
    {
        if (!isActiveAndEnabled || _restarting || (State != RunState.Playing && State != RunState.Alarm)
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
