using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Game/Core/Game Config")]
public sealed class GameConfig : ScriptableObject
{
    [SerializeField, Min(1f)] private float _alarmDurationSeconds = 60f;
    [Tooltip("Seconds per metre remaining along +X; the base duration is the minimum.")]
    [SerializeField, Min(0f)] private float _alarmSecondsPerMeter = 1.2f;
    [SerializeField, Min(1)] private int _valueAtFullWeight = 1000;
    [SerializeField, Min(1f)] private float _maxDragPixels = 200f;
    [SerializeField, Min(0.01f)] private float _maxLaunchSpeed = 7.6f;
    [SerializeField, Min(0.01f)] private float _slideDeceleration = 4f;
    [SerializeField, Range(0.01f, 1f)] private float _fullWeightSpeedMultiplier = 0.5f;
    [SerializeField, Min(0.001f)] private float _stopSpeed = 0.1f;

    public float AlarmDurationSeconds => _alarmDurationSeconds;
    public float GetAlarmDurationSeconds(float remainingDistance) =>
        Mathf.Max(_alarmDurationSeconds, Mathf.Max(0f, remainingDistance) * _alarmSecondsPerMeter);
    public int ValueAtFullWeight => _valueAtFullWeight;
    public float MaxDragPixels => _maxDragPixels;
    public float MaxLaunchSpeed => _maxLaunchSpeed;
    public float SlideDeceleration => _slideDeceleration;
    public float FullWeightSpeedMultiplier => _fullWeightSpeedMultiplier;
    public float StopSpeed => _stopSpeed;
}
