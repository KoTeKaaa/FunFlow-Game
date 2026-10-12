using UnityEngine;

// Keep on the level prefab root; Core discovers it without scene references.
[DisallowMultipleComponent]
[AddComponentMenu("Game/Core/Level Alarm Settings")]
public sealed class LevelAlarmSettings : MonoBehaviour
{
    [Tooltip("Время до поражения с момента первой тревоги, секунд. Не зависит от места срабатывания.")]
    [SerializeField, Min(1f)] private float _alarmDurationSeconds = 45f;

    public float AlarmDurationSeconds => _alarmDurationSeconds;
}
