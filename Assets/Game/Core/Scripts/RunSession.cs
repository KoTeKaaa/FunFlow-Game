using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RunSession : MonoBehaviour, IRunSession
{
    [SerializeField] private MonoBehaviour _playerSource;
    [SerializeField] private MonoBehaviour _lootSource;
    [SerializeField] private GameConfig _config;
    private readonly RunStateMachine _run = new RunStateMachine();
    private IPlayerControl _player;
    private ILootInventory _loot;
    private bool _restarting;
    private double _alarmDeadline;
    private float _remainingTime;

    public RunState State => _run.State;
    public bool HasAlarmTriggered => _run.HasAlarmTriggered;
    public float RemainingTime => _remainingTime;
    public RunResult? Result => _run.Result;
    public event Action<float> RemainingTimeChanged;
    public event Action<RunState> StateChanged;
    public event Action<RunResult> Finished;

    [ContextMenu("Test Run/Alarm")]
    public void TriggerAlarm()
    {
        if (!isActiveAndEnabled || _restarting || State != RunState.Playing || !ValidateSources())
            return;

        if (_config == null || !float.IsFinite(_config.AlarmDurationSeconds) || _config.AlarmDurationSeconds <= 0f)
        {
            Debug.LogError("RunSession: assign GameConfig with a positive finite alarm duration.", this);
            return;
        }

        _run.TriggerAlarm();
        _remainingTime = _config.AlarmDurationSeconds;
        _alarmDeadline = Time.timeAsDouble + _remainingTime;
        StateChanged?.Invoke(State);
        RemainingTimeChanged?.Invoke(RemainingTime);
    }

    private void Update()
    {
        if (!_restarting && State == RunState.Alarm && ValidateSources())
            RefreshAlarm(Time.timeAsDouble, false);
    }

    private void RefreshAlarm(double now, bool reachingExit)
    {
        bool timeChanged = false;
        if (State == RunState.Alarm)
        {
            float remaining = (float)Math.Max(0d, _alarmDeadline - now);
            timeChanged = remaining != _remainingTime;
            _remainingTime = remaining;
        }

        // Confirmed rule: an exit exactly at the deadline wins; only later time expires.
        bool expired = State == RunState.Alarm && now > _alarmDeadline;
        if (expired || reachingExit)
            FinishAttempt(!expired, timeChanged);
        else if (timeChanged)
            RemainingTimeChanged?.Invoke(RemainingTime);
    }

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
        RemainingTimeChanged = null;
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

        RefreshAlarm(Time.timeAsDouble, true);
    }

    private void FinishAttempt(bool victory, bool timeChanged)
    {
        var result = new RunResult(victory, _loot.CurrentValue);
        if (!_run.FinishRun(result))
            return;

        _player.SetControlEnabled(false);
        StateChanged?.Invoke(State);
        if (timeChanged)
            RemainingTimeChanged?.Invoke(RemainingTime);
        Finished?.Invoke(result);
    }
}
