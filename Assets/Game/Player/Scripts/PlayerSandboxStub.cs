using UnityEngine;

namespace FunFlow.Player
{
    /// <summary>
    /// Только для PlayerSandbox: заменяет источник тяжести (№3 Loot) и разрешение управления (№1 Core),
    /// пока их контракты не появились в Core/Contracts.
    /// </summary>
    public class PlayerSandboxStub : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField, Range(0f, 1f)] private float debugBurden;
        [SerializeField] private bool controlEnabled = true;

        private void Update()
        {
            motor.SetBurden(debugBurden);
            motor.ControlEnabled = controlEnabled;
        }
    }
}
