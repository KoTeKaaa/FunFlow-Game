using System;
using UnityEngine;

// Temporary contract probe for task 7. Replace with module prefabs in tasks 9/10.
[AddComponentMenu("Game/Core/Testing/Core Module Probe")]
public sealed class CoreModuleProbe : MonoBehaviour, IPlayerControl, ILootInventory
{
    [SerializeField, Min(0)] private int _currentValue;
    public int CurrentValue => _currentValue;
    public float NormalizedWeight => 0f;
    public bool IsMoving => false;
    public bool ControlEnabled { get; private set; }
    public event Action Launched { add { } remove { } }
    public event Action Changed;

    public void SetControlEnabled(bool enabled) => ControlEnabled = enabled;
    public void AddValue(int value)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        _currentValue = checked(_currentValue + value);
        Changed?.Invoke();
    }
    public int RemoveFraction(float fraction) => throw new NotSupportedException("Core probe has no loot gameplay.");
}
