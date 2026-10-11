using System;
using UnityEngine;

namespace FunFlow.Player
{
    /// <summary>
    /// Скольжение шарика по полу XZ: запуск, постоянное замедление, остановка малой скорости,
    /// запрет движения назад (vx >= 0) и прыжков (Y заморожен).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMotor : MonoBehaviour
    {
        // Временно здесь, пока №1 не заведёт общий конфиг в Core/Config.
        [Header("Баланс (временно, до Core/Config)")]
        [SerializeField, Min(0.1f)] private float baseSpeed = 14f;
        [SerializeField, Min(0.1f)] private float baseDeceleration = 7f;
        [SerializeField, Min(0f)] private float speedBurdenFactor = 0.35f;
        [SerializeField, Min(0f)] private float decelerationBurdenFactor = 1.15f;
        [SerializeField, Range(0f, 1f)] private float minLaunchPower = 0.1f;
        [SerializeField, Min(0f)] private float stopSpeed = 0.35f;
        [SerializeField, Range(0f, 90f)] private float maxLaunchAngle = 77f;

        [SerializeField] private Rigidbody body;

        private float burden;
        private bool isMoving;

        /// <summary>Запуск произошёл (один раз за запуск).</summary>
        public event Action Launched;
        /// <summary>true — начал движение, false — остановился.</summary>
        public event Action<bool> MovingChanged;
        /// <summary>Столкновение с препятствием (пол не считается).</summary>
        public event Action<Collision> Collided;

        public bool IsMoving => isMoving;
        public bool ControlEnabled { get; set; } = true;
        public bool CanLaunch => ControlEnabled && !isMoving;
        public float Burden => burden;

        public void SetBurden(float value) => burden = Mathf.Clamp01(value);

        public float LaunchSpeed(float power) =>
            Mathf.Lerp(minLaunchPower, 1f, Mathf.Clamp01(power)) * baseSpeed / (1f + speedBurdenFactor * burden);

        public float Deceleration => baseDeceleration * (1f + decelerationBurdenFactor * burden);

        /// <summary>Длина скольжения по полу без препятствий: v² / (2a).</summary>
        public float SlideDistance(float power)
        {
            float v = LaunchSpeed(power);
            return v * v / (2f * Deceleration);
        }

        /// <summary>Направление на полу, ограниченное углом от +X.</summary>
        public Vector3 ClampDirection(Vector3 direction)
        {
            float angle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
            angle = Mathf.Clamp(angle, -maxLaunchAngle, maxLaunchAngle) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        public bool TryLaunch(Vector3 direction, float power)
        {
            if (!CanLaunch) return false;

            body.linearVelocity = ClampDirection(direction) * LaunchSpeed(power);
            SetMoving(true);
            Launched?.Invoke();
            return true;
        }

        private void Reset() => body = GetComponent<Rigidbody>();

        private void OnDisable()
        {
            isMoving = false;
        }

        private void FixedUpdate()
        {
            if (!isMoving) return;

            Vector3 v = body.linearVelocity;
            v.y = 0f;
            if (v.x < 0f) v.x = 0f;

            float speed = v.magnitude - Deceleration * Time.fixedDeltaTime;
            if (speed <= stopSpeed)
            {
                body.linearVelocity = Vector3.zero;
                SetMoving(false);
                return;
            }

            body.linearVelocity = v.normalized * speed;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.contactCount > 0 && collision.GetContact(0).normal.y > 0.5f) return;
            Collided?.Invoke(collision);
        }

        private void SetMoving(bool value)
        {
            if (isMoving == value) return;
            isMoving = value;
            MovingChanged?.Invoke(value);
        }
    }
}
