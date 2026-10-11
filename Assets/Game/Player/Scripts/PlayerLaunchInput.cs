using UnityEngine;
using UnityEngine.InputSystem;

namespace FunFlow.Player
{
    /// <summary>
    /// Оттягивание мышью без видимой резинки: нажал рядом с шариком, потянул назад, отпустил.
    /// Направление — от курсора к шарику на полу, сила — расстояние от шарика до курсора на экране,
    /// нормированное по высоте экрана: одинаковая доля экрана даёт одинаковую силу при любом размере окна Game.
    /// Ввод — общие действия Player/PointerPosition и Player/Launch.
    /// </summary>
    public class PlayerLaunchInput : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private Camera viewCamera;
        [Tooltip("Общий конфиг: MaxDragPixels — оттягивание на столько пикселей при высоте экрана 1080 даёт полную силу.")]
        [SerializeField] private GameConfig config;
        [Tooltip("Player/PointerPosition из InputSystem_Actions.")]
        [SerializeField] private InputActionReference pointerPositionAction;
        [Tooltip("Player/Launch из InputSystem_Actions.")]
        [SerializeField] private InputActionReference launchAction;
        [Tooltip("Радиус захвата вокруг шарика на полу, м.")]
        [SerializeField, Min(0.1f)] private float grabRadius = 1.2f;
        [Tooltip("Короче — запуск отменяется, пиксели при высоте экрана 1080.")]
        [SerializeField, Min(0f)] private float minDragPixels = 15f;

        // Высота экрана, для которой заданы MaxDragPixels и minDragPixels.
        private const float ReferenceScreenHeight = 1080f;

        private bool isAiming;
        private bool wasButtonDown;
        private Vector3 pull;
        // Длина оттягивания в пикселях экрана высотой ReferenceScreenHeight.
        private float drag;

        public bool IsAiming => isAiming;
        /// <summary>Направление запуска с учётом ограничения угла.</summary>
        public Vector3 AimDirection => motor.ClampDirection(pull);
        public float AimPower => Mathf.Clamp01(drag / config.MaxDragPixels);
        /// <summary>Отпускание сейчас даст запуск: оттянуто достаточно и не назад.</summary>
        public bool HasLaunchableAim => isAiming && drag >= minDragPixels && motor.IsForward(pull);

        private void OnEnable()
        {
            if (config == null || pointerPositionAction == null || launchAction == null)
            {
                Debug.LogError($"{name}: не назначены GameConfig или действия ввода — запуск мышью отключён.", this);
                enabled = false;
                return;
            }

            // Действия проектные (Project-wide) и включаются Input System; выключать их здесь не нужно —
            // ассет общий, его состоянием управляет Core.
            pointerPositionAction.action.Enable();
            launchAction.action.Enable();
        }

        private void OnDisable() => CancelAim();

        private void Update()
        {
            InputAction launch = launchAction.action;
            if (viewCamera == null) return;

            // Отключение действия даёт «отпускание» — это отмена, а не запуск.
            if (!launch.enabled || !motor.CanLaunch)
            {
                CancelAim();
                wasButtonDown = launch.enabled && launch.IsPressed();
                return;
            }

            // Фронты считаем по состоянию кнопки: отпускание вне окна Game тоже завершит прицеливание.
            bool isButtonDown = launch.IsPressed();
            bool pressed = isButtonDown && !wasButtonDown;
            bool released = !isButtonDown && wasButtonDown;
            wasButtonDown = isButtonDown;

            Vector2 pointer = pointerPositionAction.action.ReadValue<Vector2>();
            if (!TryGetPointerOnFloor(pointer, out Vector3 point)) return;

            Vector3 toBall = transform.position - point;
            toBall.y = 0f;

            if (pressed && toBall.magnitude <= grabRadius)
            {
                isAiming = true;
                pull = Vector3.zero;
                drag = 0f;
            }

            if (!isAiming) return;

            pull = toBall;
            Vector2 ballOnScreen = viewCamera.WorldToScreenPoint(transform.position);
            drag = Vector2.Distance(ballOnScreen, pointer) * (ReferenceScreenHeight / Screen.height);

            if (released)
            {
                if (HasLaunchableAim)
                    motor.TryLaunch(pull, AimPower);
                CancelAim();
            }
        }

        private void CancelAim()
        {
            isAiming = false;
            pull = Vector3.zero;
            drag = 0f;
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
