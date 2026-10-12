using System;
using UnityEngine;

namespace FunFlow.Player
{
    /// <summary>
    /// Скольжение шарика по полу XZ: запуск, постоянное замедление, остановка малой скорости,
    /// запуск только вперёд, короткий отскок назад при столкновении; Y заморожен.
    /// Реализует IPlayerControl для Core; тяжесть берёт из ILootInventory (Loot).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMotor : MonoBehaviour, IPlayerControl
    {
        [Tooltip("Общий конфиг баланса: MaxLaunchSpeed, SlideDeceleration, FullWeightSpeedMultiplier, StopSpeed.")]
        [SerializeField] private GameConfig config;
        [Tooltip("Источник тяжести мешка — компонент, реализующий ILootInventory. Подключается в сцене.")]
        [SerializeField] private MonoBehaviour lootSource;

        // Параметры, которых нет в GameConfig; перенести туда после согласования с №1.
        [Header("Баланс (нет в GameConfig)")]
        [Tooltip("Рост замедления при полном мешке: a = SlideDeceleration · (1 + k · тяжесть). 0 — тяжесть влияет только на скорость.")]
        [SerializeField, Min(0f)] private float decelerationBurdenFactor;
        [Tooltip("Доля MaxLaunchSpeed при самом слабом запуске.")]
        [SerializeField, Range(0f, 1f)] private float minLaunchPower = 0.1f;
        [Tooltip("Наибольший угол запуска от +X, градусы.")]
        [SerializeField, Range(0f, 90f)] private float maxLaunchAngle = 77f;

        [Header("Столкновения со стенами")]
        [Tooltip("Доля скорости после отражения от боковой стены, ориентированной вдоль +X. Без ограничения дальности.")]
        [SerializeField, Range(0f, 1f)] private float sideWallSpeedMultiplier = 0.8f;
        [Tooltip("Доля скорости, теряемая при ударе в лоб (90° к стене).")]
        [SerializeField, Range(0f, 1f)] private float headOnSpeedLoss = 0.5f;
        [Tooltip("Доля скорости, теряемая при ударе по касательной (около 0° к стене).")]
        [SerializeField, Range(0f, 1f)] private float glancingSpeedLoss = 0.05f;
        [Tooltip("Отскок: доля скорости в стену, возвращаемая от стены. 0 — только скольжение вдоль неё.")]
        [SerializeField, Range(0f, 1f)] private float wallBounce = 0.3f;
        [Tooltip("Скорость в стену меньше этой — касание, а не удар, м/с.")]
        [SerializeField, Min(0f)] private float minImpactSpeed = 0.5f;
        [Tooltip("Доля скорости, с которой шарик соскальзывает в сторону после удара почти в лоб в торец или ребро.")]
        [SerializeField, Range(0f, 1f)] private float edgeSlideKeep = 0.5f;
        [Tooltip("Удар ближе этого к краю грани считается ударом в торец или ребро, м.")]
        [SerializeField, Min(0f)] private float edgeProbe = 0.5f;

        [SerializeField] private Rigidbody body;

        private const int MaxWallNormals = 4;

        private ILootInventory loot;
        private float burden;
        private bool controlEnabled = true;
        private bool isMoving;
        private bool collisionRecoil;
        // Скорость, с которой тело вошло в текущий шаг физики, — до того, как PhysX погасил её о стену.
        private Vector3 stepVelocity;

        // Контакты последнего шага физики; разбираются один раз в следующем FixedUpdate.
        private readonly Vector3[] wallNormals = new Vector3[MaxWallNormals];
        private int wallNormalCount;
        private Vector3 impactNormalSum;
        private Vector3 impactPointSum;
        private int impactContacts;
        private Collider impactCollider;
        private bool impactSeveralColliders;

        /// <summary>Запуск произошёл (один раз за запуск).</summary>
        public event Action Launched;
        /// <summary>true — начал движение, false — остановился.</summary>
        public event Action<bool> MovingChanged;
        /// <summary>
        /// Удар о препятствие: точка контакта и сила 0..1 (скорость в стену относительно MaxLaunchSpeed).
        /// Пол и касания без удара не считаются.
        /// </summary>
        public event Action<Vector3, float> Collided;

        public bool IsMoving => isMoving;
        public bool ControlEnabled => controlEnabled;
        public bool CanLaunch => controlEnabled && !isMoving;
        public float Burden => burden;

        /// <summary>Запрещает новое прицеливание и запуск; текущее движение не останавливает.</summary>
        public void SetControlEnabled(bool enabled) => controlEnabled = enabled;

        /// <summary>Полный мешок оставляет FullWeightSpeedMultiplier от скорости, а не ноль.</summary>
        public float LaunchSpeed(float power) =>
            Mathf.Lerp(minLaunchPower, 1f, Mathf.Clamp01(power)) * config.MaxLaunchSpeed *
            Mathf.Lerp(1f, config.FullWeightSpeedMultiplier, burden);

        public float Deceleration => config.SlideDeceleration * (1f + decelerationBurdenFactor * burden);

        /// <summary>
        /// Длина скольжения по полу без препятствий — по тем же шагам, что FixedUpdate:
        /// каждый шаг скорость падает на a·dt, затем тело проходит v·dt; при v ≤ StopSpeed — стоп.
        /// Это v²/(2a) без ошибки дискретизации.
        /// </summary>
        public float SlideDistance(float power)
        {
            float v0 = LaunchSpeed(power);
            float dv = Deceleration * Time.fixedDeltaTime;
            int steps = Mathf.Max(0, Mathf.CeilToInt((v0 - config.StopSpeed) / dv) - 1);
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
            collisionRecoil = false;
            body.linearVelocity = stepVelocity;
            ClearContacts();
            SetMoving(true);
            Launched?.Invoke();
            return true;
        }

        private void Reset() => body = GetComponent<Rigidbody>();

        private void OnValidate()
        {
            // При перетаскивании объекта в поле Unity берёт первый MonoBehaviour — ищем нужный на том же объекте.
            if (lootSource == null || lootSource is ILootInventory) return;
            lootSource = lootSource.GetComponent<ILootInventory>() as MonoBehaviour;
            if (lootSource == null) Debug.LogWarning($"{name}: источник тяжести должен реализовывать ILootInventory.", this);
        }

        private void OnEnable()
        {
            if (config == null)
            {
                Debug.LogError($"{name}: не назначен GameConfig — шарик отключён.", this);
                enabled = false;
                return;
            }

            loot = lootSource as ILootInventory;
            if (loot == null)
            {
                Debug.LogError($"{name}: не назначен источник тяжести (ILootInventory) — тяжесть считается нулевой.", this);
                return;
            }

            loot.Changed += OnLootChanged;
            OnLootChanged();
        }

        private void OnDisable()
        {
            if (loot != null) loot.Changed -= OnLootChanged;
            loot = null;
            burden = 0f;
            isMoving = false;
            collisionRecoil = false;
            stepVelocity = Vector3.zero;
            ClearContacts();
        }

        private void FixedUpdate()
        {
            if (!isMoving)
            {
                ClearContacts();
                return;
            }

            Vector3 v = impactContacts > 0 ? ResolveImpact() : body.linearVelocity;
            v.y = 0f;
            for (int i = 0; i < wallNormalCount; i++) v = Constrain(v, wallNormals[i]);
            if (!collisionRecoil && v.x < 0f) v.x = 0f;
            ClearContacts();

            float speed = v.magnitude - Deceleration * Time.fixedDeltaTime;
            if (speed <= config.StopSpeed)
            {
                stepVelocity = Vector3.zero;
                body.linearVelocity = Vector3.zero;
                SetMoving(false);
                collisionRecoil = false;
                return;
            }

            stepVelocity = v.normalized * speed;
            body.linearVelocity = stepVelocity;
        }

        private void OnCollisionEnter(Collision collision) => CollectContacts(collision);
        private void OnCollisionStay(Collision collision) => CollectContacts(collision);

        private void CollectContacts(Collision collision)
        {
            if (!isMoving) return;

            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                if (!TryWallNormal(contact.normal, out Vector3 normal)) continue;

                float approach = -Vector3.Dot(stepVelocity, normal);
                if (approach <= 0f) continue; // уже уходит от стены

                if (wallNormalCount < MaxWallNormals) wallNormals[wallNormalCount++] = normal;

                // Launching while already touching a wall produces Stay, not Enter.
                // After reflection approach <= 0, so continuing contact does not bounce twice.
                if (approach < minImpactSpeed) continue;

                impactNormalSum += normal;
                impactPointSum += contact.point;
                impactContacts++;
                if (impactCollider == null) impactCollider = collision.collider;
                else if (impactCollider != collision.collider) impactSeveralColliders = true;
            }
        }

        private static bool TryWallNormal(Vector3 contactNormal, out Vector3 normal)
        {
            normal = new Vector3(contactNormal.x, 0f, contactNormal.z);
            // Low-rim edges have an upward component; only near-horizontal support is floor.
            if (contactNormal.y > 0.9f || normal.sqrMagnitude < 1e-4f) return false;
            normal.Normalize();
            return true;
        }

        /// <summary>
        /// Один удар за шаг физики: нормали всех новых контактов (две стены угла) усредняются,
        /// потеря скорости и событие Collided — один раз.
        /// </summary>
        private Vector3 ResolveImpact()
        {
            Vector3 normal = impactNormalSum;
            // Противоположные стены гасят друг друга — остаётся только запрет движения в них.
            if (normal.sqrMagnitude < 1e-4f) return body.linearVelocity;
            normal.Normalize();

            float approach = -Vector3.Dot(stepVelocity, normal);
            if (approach <= 0f || approach < minImpactSpeed) return body.linearVelocity;

            Vector3 point = impactPointSum / impactContacts;
            bool sideWall = IsSideWall(normal);
            Vector3 result = Bounce(stepVelocity, normal, approach);
            if (!sideWall && !impactSeveralColliders && result.x >= 0f)
            {
                Vector3 slide = EdgeSlide(normal, point);
                if (slide.sqrMagnitude > result.sqrMagnitude) result = slide;
            }
            if (result.x < 0f)
            {
                collisionRecoil = true;
                if (!sideWall)
                {
                    float maxSpeed = Mathf.Sqrt(2f * Deceleration * config.MaxBackwardRecoilDistance);
                    result = Vector3.ClampMagnitude(result, maxSpeed);
                }
            }

            Collided?.Invoke(point, Mathf.Clamp01(approach / config.MaxLaunchSpeed));
            return result;
        }

        /// <summary>
        /// Отскок от стены с потерей скорости по углу удара. Короткий откат ограничивает ResolveImpact.
        /// Отклик считаем сами, поэтому новым стенам не нужен специальный PhysicsMaterial.
        /// </summary>
        private Vector3 Bounce(Vector3 velocity, Vector3 normal, float approach)
        {
            if (IsSideWall(normal)) return Vector3.Reflect(velocity, normal) * sideWallSpeedMultiplier;
            float speed = velocity.magnitude;
            // Синус угла между скоростью и стеной: 1 — в лоб, 0 — по касательной.
            float impact = approach / speed;
            // Квадрат синуса — доля энергии, ушедшей в стену: скользящие удары почти не тормозят.
            float loss = Mathf.Lerp(glancingSpeedLoss, headOnSpeedLoss, impact * impact);

            Vector3 along = velocity + normal * approach;
            Vector3 direction = along + normal * (approach * wallBounce);
            if (direction.sqrMagnitude < 1e-6f) return Vector3.zero;

            return direction.normalized * (speed * (1f - loss));
        }

        // Classify the actual contact face; newly created walls need no marker or wiring.
        private static bool IsSideWall(Vector3 normal) => Mathf.Abs(normal.z) > Mathf.Abs(normal.x);

        /// <summary>
        /// Торец или ребро: дополнительное скольжение вдоль грани, когда нет отскока назад.
        /// Направление — к ближайшему краю или по своей боковой скорости.
        /// Возвращает ноль, если край дальше edgeProbe (широкая грань).
        /// </summary>
        private Vector3 EdgeSlide(Vector3 normal, Vector3 point)
        {
            Vector3 center = body.position;
            var tangent = new Vector3(normal.z, 0f, -normal.x);
            float reach = Vector3.Dot(center - point, normal) + 0.1f;
            bool openPlus = !HitsImpactCollider(center + tangent * edgeProbe, normal, reach);
            bool openMinus = !HitsImpactCollider(center - tangent * edgeProbe, normal, reach);
            if (!openPlus && !openMinus) return Vector3.zero;

            float side;
            float sideways = Vector3.Dot(stepVelocity, tangent);
            if (openPlus != openMinus) side = openPlus ? 1f : -1f;
            // Узкий торец, оба края рядом: по боковой скорости, а строго в лоб — к ближнему краю.
            else if (Mathf.Abs(sideways) > 0.05f) side = Mathf.Sign(sideways);
            else side = Vector3.Dot(point - impactCollider.bounds.center, tangent) >= 0f ? 1f : -1f;

            return KeepForward(tangent * (side * stepVelocity.magnitude * edgeSlideKeep), normal);
        }

        private bool HitsImpactCollider(Vector3 origin, Vector3 normal, float reach) =>
            impactCollider.Raycast(new Ray(origin, -normal), out _, reach);

        /// <summary>Убирает скорость в стену, сохраняя разрешённый столкновением отскок.</summary>
        private Vector3 Constrain(Vector3 velocity, Vector3 normal)
        {
            float into = Vector3.Dot(velocity, normal);
            if (into < 0f) velocity -= normal * into;
            return collisionRecoil ? velocity : KeepForward(velocity, normal);
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

        private void OnLootChanged() => burden = Mathf.Clamp01(loot.NormalizedWeight);

        private void ClearContacts()
        {
            wallNormalCount = 0;
            impactNormalSum = Vector3.zero;
            impactPointSum = Vector3.zero;
            impactContacts = 0;
            impactCollider = null;
            impactSeveralColliders = false;
        }

        private void SetMoving(bool value)
        {
            if (isMoving == value) return;
            isMoving = value;
            MovingChanged?.Invoke(value);
        }
    }
}
