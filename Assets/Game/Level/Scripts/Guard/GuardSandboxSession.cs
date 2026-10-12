using System;
using UnityEngine;

/// <summary>
/// Только для GuardSandbox: заглушка Core, которая сразу находится в Playing и показывает вызовы TriggerAlarm.
/// В уровни и Game.unity не добавлять — там охраннику назначается настоящий RunSession.
/// </summary>
public class GuardSandboxSession : MonoBehaviour, IRunSession
{
    [Header("Только чтение (Play)")]
    [Tooltip("Текущее состояние заглушки попытки.")]
    [SerializeField] private RunState state = RunState.Playing;
    [Tooltip("Сколько раз охранники вызвали TriggerAlarm (повторные вызовы не перезапускают тревогу).")]
    [SerializeField] private int alarmCalls;

    public RunState State => state;
    public bool HasAlarmTriggered { get; private set; }
    public float RemainingTime => 0f;
    public RunResult? Result => null;

    // Заглушке события не нужны: подписка допустима, но уведомлений не будет.
    public event Action<RunState> StateChanged { add { } remove { } }
    public event Action<float> RemainingTimeChanged { add { } remove { } }
    public event Action<RunResult> Finished { add { } remove { } }

    private void OnEnable() => ResetAlarm();

    public void StartRun()
    {
        if (state == RunState.Waiting)
            state = RunState.Playing;
    }

    public void RestartRun() => ResetAlarm();

    public void TriggerAlarm()
    {
        alarmCalls++;
        if (state != RunState.Playing)
            return;

        state = RunState.Alarm;
        HasAlarmTriggered = true;
        Debug.Log("[GuardSandbox] TriggerAlarm: охранник заметил вора, тревога включена.", this);
    }

    public void ReachExit()
    {
    }

    [ContextMenu("Сбросить тревогу")]
    private void ResetAlarm()
    {
        state = RunState.Playing;
        HasAlarmTriggered = false;
        alarmCalls = 0;
    }
}
