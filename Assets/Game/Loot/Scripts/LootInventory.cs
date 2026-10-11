using System;
using UnityEngine;

public class LootInventory : MonoBehaviour, ILootInventory
{
    [SerializeField] private GameConfig config;

    public int CurrentValue { get; private set; }

    public float NormalizedWeight
    {
        get
        {
            if (config == null || config.ValueAtFullWeight <= 0)
                return 0f;

            return Mathf.Clamp01(
                (float)CurrentValue / config.ValueAtFullWeight
            );
        }
    }

    public event Action Changed;

    public void AddValue(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value));

        if (value > int.MaxValue - CurrentValue)
            throw new OverflowException();

        CurrentValue += value;

        Changed?.Invoke();
    }

    public int RemoveFraction(float fraction)
    {
        if (float.IsNaN(fraction) ||
            float.IsInfinity(fraction) ||
            fraction < 0f ||
            fraction > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(fraction));
        }

        int lostValue = (int)Math.Floor(
            (double)CurrentValue * fraction
        );

        if (lostValue == 0)
            return 0;

        CurrentValue -= lostValue;

        Changed?.Invoke();

        return lostValue;
    }
}