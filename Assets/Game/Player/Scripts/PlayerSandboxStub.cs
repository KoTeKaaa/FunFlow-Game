using System;
using UnityEngine;

namespace FunFlow.Player
{
    /// <summary>
    /// Только для PlayerSandbox: тестовая реализация ILootInventory (тяжесть — ползунок) и замена Core,
    /// который разрешает/запрещает управление через IPlayerControl. В Player.prefab и Game.unity не добавлять.
    /// </summary>
    public class PlayerSandboxStub : MonoBehaviour, ILootInventory
    {
        [Tooltip("Компонент, реализующий IPlayerControl (PlayerMotor).")]
        [SerializeField] private MonoBehaviour playerControlSource;
        [Tooltip("Общий конфиг: ValueAtFullWeight переводит стоимость в тяжесть.")]
        [SerializeField] private GameConfig config;
        [SerializeField, Range(0f, 1f)] private float debugWeight;
        [SerializeField] private bool controlEnabled = true;

        [Header("Только чтение (Play)")]
        [SerializeField] private int launchCount;
        [SerializeField] private bool isMoving;

        private IPlayerControl playerControl;
        private float notifiedWeight;
        private bool appliedControl;

        public int CurrentValue => Mathf.RoundToInt(NormalizedWeight * config.ValueAtFullWeight);
        public float NormalizedWeight => Mathf.Clamp01(debugWeight);

        public event Action Changed;

        public void AddValue(int value)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            SetWeight((CurrentValue + (float)value) / config.ValueAtFullWeight);
        }

        public int RemoveFraction(float fraction)
        {
            if (float.IsNaN(fraction) || fraction < 0f || fraction > 1f)
                throw new ArgumentOutOfRangeException(nameof(fraction));

            int current = CurrentValue;
            int loss = Mathf.FloorToInt(current * fraction);
            SetWeight((float)(current - loss) / config.ValueAtFullWeight);
            return loss;
        }

        private void OnEnable()
        {
            launchCount = 0;
            notifiedWeight = NormalizedWeight;

            playerControl = playerControlSource as IPlayerControl;
            if (playerControl == null)
            {
                Debug.LogError($"{name}: не назначен IPlayerControl.", this);
                return;
            }

            playerControl.Launched += OnLaunched;
            ApplyControl();
        }

        private void OnDisable()
        {
            if (playerControl != null) playerControl.Launched -= OnLaunched;
            playerControl = null;
        }

        private void Update()
        {
            // Ползунок в Inspector меняет тяжесть без вызова методов — сообщаем об этом как настоящий Loot.
            if (!Mathf.Approximately(notifiedWeight, NormalizedWeight)) NotifyChanged();
            if (playerControl == null) return;
            if (appliedControl != controlEnabled) ApplyControl();
            isMoving = playerControl.IsMoving;
        }

        private void SetWeight(float weight)
        {
            debugWeight = Mathf.Clamp01(weight);
            if (!Mathf.Approximately(notifiedWeight, NormalizedWeight)) NotifyChanged();
        }

        private void NotifyChanged()
        {
            notifiedWeight = NormalizedWeight;
            Changed?.Invoke();
        }

        private void ApplyControl()
        {
            appliedControl = controlEnabled;
            playerControl.SetControlEnabled(controlEnabled);
        }

        private void OnLaunched() => launchCount++;
    }
}
