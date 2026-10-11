using System;

public interface IPlayerControl
{
    bool IsMoving { get; }

    // Raised once per accepted launch, not per input attempt.
    event Action Launched;

    // Blocks new aiming/launches; does not freeze an existing movement.
    void SetControlEnabled(bool enabled);
}
