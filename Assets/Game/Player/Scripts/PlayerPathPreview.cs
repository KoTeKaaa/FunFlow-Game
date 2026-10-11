using UnityEngine;

namespace FunFlow.Player
{
    /// <summary>
    /// Предпросмотр пути по полу во время оттягивания: прямая на длину скольжения с учётом силы и тяжести,
    /// обрезанная до первого препятствия, и маркер точки остановки. Не воздушная траектория.
    /// </summary>
    public class PlayerPathPreview : MonoBehaviour
    {
        [SerializeField] private PlayerLaunchInput launchInput;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private LineRenderer line;
        [SerializeField] private Transform stopMarker;
        [Tooltip("Чуть меньше радиуса шарика, чтобы каст не цеплял пол.")]
        [SerializeField, Min(0.01f)] private float castRadius = 0.48f;
        [SerializeField] private LayerMask obstacleMask = ~(1 << 2);
        [Tooltip("Радиус шарика: от центра до пола, м.")]
        [SerializeField, Min(0.01f)] private float ballRadius = 0.5f;
        [Tooltip("Высота линии и маркера над полом, м.")]
        [SerializeField, Min(0f)] private float floorOffset = 0.02f;

        /// <summary>Точка остановки по предпросмотру (центр шарика); валидна, пока предпросмотр виден.</summary>
        public Vector3 PredictedStop { get; private set; }
        public bool IsVisible => line.enabled;

        private void OnDisable() => SetVisible(false);

        private void LateUpdate()
        {
            if (!launchInput.HasLaunchableAim)
            {
                SetVisible(false);
                return;
            }

            Vector3 origin = motor.transform.position;
            Vector3 direction = launchInput.AimDirection;
            float distance = motor.SlideDistance(launchInput.AimPower);

            if (Physics.SphereCast(origin, castRadius, direction, out RaycastHit hit, distance,
                    obstacleMask, QueryTriggerInteraction.Ignore))
                distance = hit.distance;

            PredictedStop = origin + direction * distance;

            Vector3 floorShift = Vector3.down * (ballRadius - floorOffset);
            line.SetPosition(0, origin + floorShift);
            line.SetPosition(1, PredictedStop + floorShift);
            stopMarker.SetPositionAndRotation(PredictedStop + floorShift, Quaternion.identity);
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            if (line.enabled != visible) line.enabled = visible;
            if (stopMarker.gameObject.activeSelf != visible) stopMarker.gameObject.SetActive(visible);
        }
    }
}
