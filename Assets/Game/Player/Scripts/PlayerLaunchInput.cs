using UnityEngine;
using UnityEngine.InputSystem;

namespace FunFlow.Player
{
    /// <summary>
    /// Оттягивание мышью без видимой резинки: нажал рядом с шариком, потянул назад, отпустил.
    /// Направление — от курсора к шарику, сила — длина оттягивания.
    /// </summary>
    public class PlayerLaunchInput : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private Camera viewCamera;
        [Tooltip("Радиус захвата вокруг шарика на полу, м.")]
        [SerializeField, Min(0.1f)] private float grabRadius = 1.2f;
        [Tooltip("Оттягивание на эту длину даёт полную силу, м.")]
        [SerializeField, Min(0.1f)] private float maxPullDistance = 4f;
        [Tooltip("Короче — запуск отменяется, м.")]
        [SerializeField, Min(0f)] private float minPullDistance = 0.3f;

        private bool isAiming;
        private bool wasButtonDown;
        private Vector3 pull;

        public bool IsAiming => isAiming;
        /// <summary>Направление запуска с учётом ограничения угла.</summary>
        public Vector3 AimDirection => motor.ClampDirection(pull);
        public float AimPower => Mathf.Clamp01(pull.magnitude / maxPullDistance);
        /// <summary>Отпускание сейчас даст запуск: оттянуто достаточно и не назад.</summary>
        public bool HasLaunchableAim => isAiming && pull.magnitude >= minPullDistance && motor.IsForward(pull);

        private void OnDisable()
        {
            isAiming = false;
            wasButtonDown = false;
            pull = Vector3.zero;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || viewCamera == null) return;

            // Фронты считаем сами: отпускание вне окна Game тоже завершит прицеливание.
            bool isButtonDown = mouse.leftButton.isPressed;
            bool pressed = isButtonDown && !wasButtonDown;
            bool released = !isButtonDown && wasButtonDown;
            wasButtonDown = isButtonDown;

            if (!motor.CanLaunch)
            {
                isAiming = false;
                return;
            }

            if (!TryGetPointerOnFloor(mouse.position.ReadValue(), out Vector3 point)) return;

            Vector3 toBall = transform.position - point;
            toBall.y = 0f;

            if (pressed && toBall.magnitude <= grabRadius)
            {
                isAiming = true;
                pull = Vector3.zero;
            }

            if (!isAiming) return;

            pull = Vector3.ClampMagnitude(toBall, maxPullDistance);

            if (released)
            {
                if (HasLaunchableAim)
                    motor.TryLaunch(pull, AimPower);
                isAiming = false;
            }
        }

        private bool TryGetPointerOnFloor(Vector2 screenPosition, out Vector3 point)
        {
            Ray ray = viewCamera.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.up, transform.position);
            if (plane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }
            point = default;
            return false;
        }
    }
}
