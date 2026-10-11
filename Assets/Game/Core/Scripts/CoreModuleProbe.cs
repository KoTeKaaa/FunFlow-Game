using System;
using UnityEngine;

// Temporary contract probe. Replace Loot/Bag with participant 3's prefabs in task 10.
[AddComponentMenu("Game/Core/Testing/Core Module Probe")]
public sealed class CoreModuleProbe : MonoBehaviour, IPlayerControl, ILootInventory
{
    [SerializeField, Min(0)] private int _currentValue;
    [SerializeField] private GameConfig _config;
    [SerializeField] private Transform _playerTarget;
    [SerializeField] private Transform _bagVisual;
    private int _lastPublishedValue;
    // ASSUMPTION: test bag follows in world space; size is 0.4..1.2 metres.
    private static readonly Vector3 BagOffset = new Vector3(-0.8f, -0.1f, 0f);
    public int CurrentValue => _currentValue;
    public float NormalizedWeight => _config == null ? 0f
        : Mathf.Clamp01((float)_currentValue / _config.ValueAtFullWeight);
    public bool IsMoving => false;
    public bool ControlEnabled { get; private set; }
    public event Action Launched { add { } remove { } }
    public event Action<Vector3, float> Collided { add { } remove { } }
    public event Action Changed;

    private void OnEnable()
    {
        _lastPublishedValue = _currentValue;
        UpdateBag();
    }
    private void OnValidate() => _currentValue = Mathf.Max(0, _currentValue);
    private void LateUpdate()
    {
        UpdateBag();
        if (_lastPublishedValue == _currentValue) return;
        _lastPublishedValue = _currentValue;
        Changed?.Invoke();
    }

    private void UpdateBag()
    {
        if (_bagVisual == null || _playerTarget == null) return;
        _bagVisual.position = _playerTarget.position + BagOffset;
        _bagVisual.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.2f, NormalizedWeight);
    }

    // Assign synthetic data, without implementing collection, contacts or loot losses.
    public void SetTestValue(int value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        if (_currentValue == value) return;
        _currentValue = value;
        _lastPublishedValue = value;
        UpdateBag();
        Changed?.Invoke();
    }

    [ContextMenu("Test Loot/Empty Bag")]
    private void EmptyBag() => SetTestValue(0);

    [ContextMenu("Test Loot/Full Bag")]
    private void FullBag()
    {
        if (_config == null)
        {
            Debug.LogError("CoreModuleProbe: assign GameConfig before testing full weight.", this);
            return;
        }
        SetTestValue(_config.ValueAtFullWeight);
    }

    public void SetControlEnabled(bool enabled) => ControlEnabled = enabled;
    public void AddValue(int value)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        _currentValue = checked(_currentValue + value);
        _lastPublishedValue = _currentValue;
        UpdateBag();
        Changed?.Invoke();
    }
    public int RemoveFraction(float fraction) => throw new NotSupportedException("Core probe has no loot gameplay.");
}
