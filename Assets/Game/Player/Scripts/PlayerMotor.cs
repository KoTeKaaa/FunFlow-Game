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

        [Header("Столкновения со стенами")]
        [Tooltip("Доля скорости, теряемая при ударе в лоб (90° к стене).")]
        [SerializeField, Range(0f, 1f)] private float headOnSpeedLoss = 0.6f;
        [Tooltip("Доля скорости, теряемая при ударе по касательной (около 0° к стене).")]
        [SerializeField, Range(0f, 1f)] private float glancingSpeedLoss = 0.05f;
        [Tooltip("Отскок: доля скорости в стену, возвращаемая от стены. 0 — только скольжение вдоль неё.")]
        [SerializeField, Range(0f, 1f)] private float wallBounce = 0.3f;
        [Tooltip("Скорость в стену меньше этой — касание, а не удар, м/с.")]
        [SerializeField, Min(0f)] private float minImpactSpeed = 0.5f;

        [SerializeField] private Rigidbody body;

        private float burden;
        private bool isMoving;
        // Скорость, с которой тело вошло в текущий шаг физики, — до того, как PhysX погасил её о стену.
        private Vector3 stepVelocity;

        /// <summary>Запуск произошёл (один раз за запуск).</summary>
        public event Action Launched;
        /// <summary>true — начал движение, false — остановился.</summary>
        public event Action<bool> MovingChanged;
        /// <summary>
        /// Удар о препятствие: точка контакта и сила 0..1 (скорость в стену относительно baseSpeed).
        /// Пол и касания без удара не считаются.
        /// </summary>
        public event Action<Vector3, float> Collided;

        public bool IsMoving => isMoving;
        public bool ControlEnabled { get; set; } = true;
        public bool CanLaunch => ControlEnabled && !isMoving;
        public float Burden => burden;

        public void SetBurden(float value) => burden = Mathf.Clamp01(value);

        public float LaunchSpeed(float power) =>
            Mathf.Lerp(minLaunchPower, 1f, Mathf.Clamp01(power)) * baseSpeed / (1f + speedBurdenFactor * burden);

        public float Deceleration => baseDeceleration * (1f + decelerationBurdenFactor * burden);

        /// <summary>
        /// Длина скольжения по полу без препятствий — по тем же шагам, что FixedUpdate:
        /// каждый шаг скорость падает на a·dt, затем тело проходит v·dt; при v ≤ stopSpeed — стоп.
        /// Это v²/(2a) без ошибки дискретизации.
        /// </summary>
        public float SlideDistance(float power)
        {
            float v0 = LaunchSpeed(power);
            float dv = Deceleration * Time.fixedDeltaTime;
            int steps = Mathf.Max(0, Mathf.CeilToInt((v0 - stopSpeed) / dv) - 1);
            return Time.fixedDeltaTime * (steps * v0 - dv * steps * (steps + 1) * 0.5f);
        }

        /// <summary>Запуск назад или строго вбок отменяется, а не поворачивается.</summary>
        public bool IsForward(Vector3 direction) => direction.x > 0f;

        /// <summary>Направление на полу, ограниченное углом от +X.</summary>
        public Vector3 ClampDirection(Vector3 direction)
        {
            float angle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
            angle = Mathf.Clamp(angle, -maxLaunchAngle, maxLaunchAngle) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        public bool TryLaunch(Vector3 direction, float power)
        {
            if (!CanLaunch || !IsForward(direction)) return false;

            stepVelocity = ClampDirection(direction) * LaunchSpeed(power);
            body.linearVelocity = stepVelocity;
            SetMoving(true);
            Launched?.Invoke();
            return true;
        }

        private void Reset() => body = GetComponent<Rigidbody>();

        private void OnDisable()
        {
            isMoving = false;
            stepVelocity = Vector3.zero;
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
                stepVelocity = Vector3.zero;
                body.linearVelocity = Vector3.zero;
                SetMoving(false);
                return;
            }

            stepVelocity = v.normalized * speed;
            body.linearVelocity = stepVelocity;
        }

        // Stay тоже нужен: запуск в стену, которой шарик уже касается, не даёт нового Enter.
        private void OnCollisionEnter(Collision collision) => HandleContacts(collision);
        private void OnCollisionStay(Collision collision) => HandleContacts(collision);

        private void HandleContacts(Collision collision)
        {
            if (!isMoving) return;

            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                Vector3 normal = contact.normal;
                if (normal.y > 0.5f) continue; // пол

                normal.y = 0f;
                if (normal.sqrMagnitude < 1e-4f) continue;
                normal.Normalize();

                // Контакты обрабатываются по очереди от уже изменённой скорости — так в углу
                // вторая стена получает отскок от первой, а не исходный удар.
                float approach = -Vector3.Dot(stepVelocity, normal);
                if (approach <= 0f || approach < minImpactSpeed) continue;

                stepVelocity = Bounce(stepVelocity, normal, approach);
                body.linearVelocity = stepVelocity;
                Collided?.Invoke(contact.point, Mathf.Clamp01(approach / baseSpeed));
            }
        }

        /// <summary>
        /// Отскок от стены с потерей скорости по углу удара, затем запрет движения назад.
        /// Отклик считаем сами, а не через PhysicsMaterial: так потеря и vx ≥ 0 настраиваются в одном месте.
        /// </summary>
        private Vector3 Bounce(Vector3 velocity, Vector3 normal, float approach)
        {
            float speed = velocity.magnitude;
            // Синус угла между скоростью и стеной: 1 — в лоб, 0 — по касательной.
            float impact = approach / speed;
            // Квадрат синуса — доля энергии, ушедшей в стену: скользящие удары почти не тормозят.
            float loss = Mathf.Lerp(glancingSpeedLoss, headOnSpeedLoss, impact * impact);

            Vector3 along = velocity + normal * approach;
            Vector3 direction = along + normal * (approach * wallBounce);
            if (direction.sqrMagnitude < 1e-6f) return Vector3.zero;

            return KeepForward(direction.normalized * (speed * (1f - loss)), normal);
        }

        /// <summary>vx ≥ 0 и не в стену: иначе шарик прилипает к ней или застревает в углу.</summary>
        private static Vector3 KeepForward(Vector3 velocity, Vector3 normal)
        {
            if (velocity.x >= 0f) return velocity;

            velocity.x = 0f;
            if (Vector3.Dot(velocity, normal) >= 0f) return velocity;

            // Без x-составляющей скорость снова смотрит в стену — остаётся только скольжение вдоль неё вперёд.
            var tangent = new Vector3(normal.z, 0f, -normal.x);
            if (tangent.x < 0f) tangent = -tangent;
            return tangent * Mathf.Max(0f, Vector3.Dot(velocity, tangent));
        }

        private void SetMoving(bool value)
        {
            if (isMoving == value) return;
            isMoving = value;
            MovingChanged?.Invoke(value);
        }
    }
}
