using System;
using UnityEngine;

public interface IPlayerControl
{
    bool IsMoving { get; }

    // Raised once per accepted launch, not per input attempt.
    event Action Launched;

    // Obstacle impact: world contact point and strength 0..1, once per impact.
    event Action<Vector3, float> Collided;

    // Blocks new aiming/launches; does not freeze an existing movement.
    void SetControlEnabled(bool enabled);
}
