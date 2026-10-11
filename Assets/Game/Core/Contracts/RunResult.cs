using System;

public readonly struct RunResult
{
    public bool IsVictory { get; }
    public int FinalValue { get; }

    public RunResult(bool isVictory, int finalValue)
    {
        if (finalValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(finalValue));
        }

        IsVictory = isVictory;
        FinalValue = finalValue;
    }
}
