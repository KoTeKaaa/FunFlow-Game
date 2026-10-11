using System;

public interface ILootInventory
{
    int CurrentValue { get; }
    float NormalizedWeight { get; }

    // Raised after both value and weight have been updated.
    event Action Changed;

    void AddValue(int value);

    // Returns the actual loss; fraction is in the range 0..1.
    int RemoveFraction(float fraction);
}
