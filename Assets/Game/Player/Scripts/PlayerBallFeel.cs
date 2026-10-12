using UnityEngine;

namespace FunFlow.Player
{
    /// <summary>
    /// «Ощущение» шарика — только визуал, физику и предпросмотр не трогает:
    /// сжатие при оттягивании, растяжение при запуске, сплющивание при ударе (масштаб Squash)
    /// и поворот «мордой» по рысканью (Facing). Иерархия: Player → Facing → Squash → BallMesh.
    /// Facing поворачивается только вокруг Y: при прицеливании — в сторону запуска, в движении — по скорости.
    /// Squash поворачивается к оси деформации, BallMesh всегда держит мировой поворот Facing.
    /// </summary>
    public class PlayerBallFeel : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerLaunchInput launchInput;
        [SerializeField] private Rigidbody body;
        [Tooltip("Поворот «мордой» только вокруг Y; без коллайдеров.")]
        [SerializeField] private Transform facing;
        [Tooltip("Масштаб и поворот к оси деформации; без коллайдеров.")]
        [SerializeField] private Transform squash;
        [Tooltip("Меш шарика внутри Squash; держит мировой поворот Facing (со смещением модели).")]
        [SerializeField] private Transform ballMesh;
        [Tooltip("Радиус шарика, м: чтобы низ деформированного шарика оставался на полу.")]
        [SerializeField, Min(0.01f)] private float ballRadius = 0.5f;

        [Header("Поворот мордой")]
        [Tooltip("Время, за которое морда в основном догоняет нужное направление, с. Меньше — резче.")]
        [SerializeField, Min(0.01f)] private float turnSmoothTime = 0.1f;
        [Tooltip("Наибольшая скорость поворота, °/с: разворот после удара о стену не быстрее этого.")]
        [SerializeField, Min(1f)] private float maxTurnSpeed = 900f;
        [Tooltip("Скорость по полу меньше этой не меняет направление морды, м/с: на остановке шарик не дёргается.")]
        [SerializeField, Min(0f)] private float minTurnSpeed = 0.3f;
        [Tooltip("Поворот модели вокруг Y относительно Facing, градусы: выравнивает морду, если в модели она не вдоль +Z.")]
        [SerializeField, Range(-180f, 180f)] private float modelYawOffset;

        [Header("Оттягивание")]
        [Tooltip("Сжатие вдоль натяжения при полной силе: 0.2 — на 20%.")]
        [SerializeField, Range(0f, 0.5f)] private float aimSquash = 0.18f;
        [Tooltip("Время следования за силой натяжения и возврата при отмене, с.")]
        [SerializeField, Min(0.001f)] private float aimSmoothTime = 0.05f;

        [Header("Запуск")]
        [Tooltip("Растяжение вдоль движения при самом сильном запуске.")]
        [SerializeField, Range(0f, 0.5f)] private float launchStretch = 0.25f;
        [SerializeField, Range(0.05f, 0.3f)] private float launchDuration = 0.16f;

        [Header("Удар о стену")]
        [Tooltip("Сплющивание по нормали при ударе силой 1.")]
        [SerializeField, Range(0f, 0.6f)] private float impactSquash = 0.35f;
        [Tooltip("Удар слабее даёт хотя бы эту долю сплющивания, чтобы касание было заметно.")]
        [SerializeField, Range(0f, 1f)] private float minImpactStrength = 0.3f;
        [SerializeField, Range(0.05f, 0.3f)] private float impactDuration = 0.14f;

        [Header("Форма импульса")]
        [Tooltip("Доля длительности на нарастание до пика; остальное — затухание.")]
        [SerializeField, Range(0.05f, 0.9f)] private float pulseAttack = 0.3f;

        // Деформация: amount > 0 — растяжение вдоль axis, < 0 — сжатие. Объём примерно сохраняется.
        private Vector3 axis = Vector3.right;
        private float amount;

        private bool pulseActive;
        private float pulseTime;
        private float pulseDuration;
        private float pulseStart;
        private float pulsePeak;

        // Рысканье Facing, градусы (0 — морда вдоль +Z, 90 — вдоль +X, по ходу коридора).
        private const float StartYaw = 90f;
        private float yaw = StartYaw;
        private float targetYaw = StartYaw;
        private float yawVelocity;

        private void OnEnable()
        {
            ResetVisual();
            motor.Launched += OnLaunched;
            motor.Collided += OnCollided;
        }

        private void OnDisable()
        {
            motor.Launched -= OnLaunched;
            motor.Collided -= OnCollided;
            ResetVisual();
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            UpdateDeformation(dt);
            UpdateFacing(dt);
            ApplySquash();
        }

        private void OnLaunched()
        {
            Vector3 velocity = body.linearVelocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude < 1e-6f) return;

            float strength = Mathf.Clamp01(velocity.magnitude / motor.LaunchSpeed(1f));
            StartPulse(velocity.normalized, launchStretch * strength, launchDuration);
        }

        private void OnCollided(Vector3 point, float strength)
        {
            Vector3 normal = body.position - point;
            normal.y = 0f;
            if (normal.sqrMagnitude < 1e-6f) return;

            float squashAmount = impactSquash * Mathf.Lerp(minImpactStrength, 1f, strength);
            StartPulse(normal.normalized, -squashAmount, impactDuration);
        }

        /// <summary>Импульс начинается с текущей деформации, проецированной на новую ось, — без скачка.</summary>
        private void StartPulse(Vector3 newAxis, float peak, float duration)
        {
            float alignment = Vector3.Dot(axis, newAxis);
            pulseStart = amount * alignment * alignment;
            pulsePeak = peak;
            pulseDuration = duration;
            pulseTime = 0f;
            pulseActive = true;
            axis = newAxis;
            amount = pulseStart;
        }

        private void UpdateDeformation(float dt)
        {
            if (pulseActive)
            {
                pulseTime += dt;
                float t = Mathf.Clamp01(pulseTime / pulseDuration);
                amount = t < pulseAttack
                    ? Mathf.Lerp(pulseStart, pulsePeak, Smooth(t / pulseAttack))
                    : Mathf.Lerp(pulsePeak, 0f, Smooth((t - pulseAttack) / (1f - pulseAttack)));
                if (t >= 1f) pulseActive = false;
                return;
            }

            float target = 0f;
            if (launchInput.HasLaunchableAim)
            {
                axis = launchInput.AimDirection;
                target = -aimSquash * launchInput.AimPower;
            }
            amount = Mathf.Lerp(amount, target, 1f - Mathf.Exp(-dt / aimSmoothTime));
        }

        /// <summary>
        /// Цель — направление будущего запуска при прицеливании или скорость в движении;
        /// иначе (покой, малая скорость) морда держит последнее направление.
        /// </summary>
        private void UpdateFacing(float dt)
        {
            if (launchInput.HasLaunchableAim)
            {
                targetYaw = YawOf(launchInput.AimDirection);
            }
            else if (motor.IsMoving)
            {
                Vector3 velocity = body.linearVelocity;
                velocity.y = 0f;
                if (velocity.sqrMagnitude > minTurnSpeed * minTurnSpeed) targetYaw = YawOf(velocity);
            }

            if (dt > 0f) yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, turnSmoothTime, maxTurnSpeed, dt);
            facing.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private void ApplySquash()
        {
            float along = 1f + amount;
            float across = 1f / Mathf.Sqrt(along);
            squash.rotation = Quaternion.LookRotation(axis, Vector3.up);
            squash.localScale = new Vector3(across, across, along);
            // Центр сдвигается так, чтобы низ шарика оставался на полу.
            squash.localPosition = Vector3.up * (ballRadius * (across - 1f));
            // Мировой поворот: поворот Squash к оси деформации не должен крутить морду.
            ballMesh.rotation = facing.rotation * Quaternion.Euler(0f, modelYawOffset, 0f);
        }

        private void ResetVisual()
        {
            axis = Vector3.right;
            amount = 0f;
            pulseActive = false;
            yaw = targetYaw = StartYaw;
            yawVelocity = 0f;
            if (facing != null) facing.rotation = Quaternion.Euler(0f, StartYaw, 0f);
            if (squash != null)
            {
                squash.localPosition = Vector3.zero;
                // Ось деформации +X совпадает с мордой при старте — поворот Squash относительно Facing нулевой.
                squash.localRotation = Quaternion.identity;
                squash.localScale = Vector3.one;
            }
            if (ballMesh != null) ballMesh.localRotation = Quaternion.Euler(0f, modelYawOffset, 0f);
        }

        private static float YawOf(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
