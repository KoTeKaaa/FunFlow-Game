using UnityEngine;

namespace FunFlow.Player
{
    /// <summary>
    /// «Ощущение» шарика — только визуал, физику и предпросмотр не трогает:
    /// сжатие при оттягивании, растяжение при запуске, сплющивание при ударе (масштаб Squash)
    /// и качение по скорости (поворот BallMesh). Иерархия: Player → Squash → BallMesh.
    /// Squash поворачивается к оси деформации, BallMesh только катится — сплющивание не крутится вместе с шариком.
    /// </summary>
    public class PlayerBallFeel : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerLaunchInput launchInput;
        [SerializeField] private Rigidbody body;
        [Tooltip("Масштаб и поворот к оси деформации; без коллайдеров.")]
        [SerializeField] private Transform squash;
        [Tooltip("Меш шарика внутри Squash; только вращение качения.")]
        [SerializeField] private Transform ballMesh;
        [Tooltip("Радиус шарика, м: для качения и чтобы низ деформированного шарика оставался на полу.")]
        [SerializeField, Min(0.01f)] private float ballRadius = 0.5f;

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

        private Quaternion roll = Quaternion.identity;

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
            UpdateRoll(dt);
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

        /// <summary>Качение без проскальзывания: угол = путь / радиус, ось — поперёк скорости.</summary>
        private void UpdateRoll(float dt)
        {
            if (motor.IsMoving)
            {
                Vector3 velocity = body.linearVelocity;
                velocity.y = 0f;
                float speed = velocity.magnitude;
                if (speed > 1e-4f)
                {
                    Vector3 rollAxis = Vector3.Cross(Vector3.up, velocity / speed);
                    roll = Quaternion.AngleAxis(speed * dt / ballRadius * Mathf.Rad2Deg, rollAxis) * roll;
                }
            }
            // Мировой поворот: поворот Squash к оси деформации не должен крутить шарик.
            ballMesh.rotation = roll;
        }

        private void ApplySquash()
        {
            float along = 1f + amount;
            float across = 1f / Mathf.Sqrt(along);
            squash.rotation = Quaternion.LookRotation(axis, Vector3.up);
            squash.localScale = new Vector3(across, across, along);
            // Центр сдвигается так, чтобы низ шарика оставался на полу.
            squash.localPosition = Vector3.up * (ballRadius * (across - 1f));
        }

        private void ResetVisual()
        {
            axis = Vector3.right;
            amount = 0f;
            pulseActive = false;
            roll = Quaternion.identity;
            if (squash != null)
            {
                squash.localPosition = Vector3.zero;
                squash.localRotation = Quaternion.identity;
                squash.localScale = Vector3.one;
            }
            if (ballMesh != null) ballMesh.localRotation = Quaternion.identity;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
