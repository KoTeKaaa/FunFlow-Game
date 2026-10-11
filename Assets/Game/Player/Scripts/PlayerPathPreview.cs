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

        [Header("Вид от силы")]
        [Tooltip("Цвет линии при самом слабом запуске — бледный.")]
        [SerializeField] private Color weakColor = new Color(1f, 0.96f, 0.82f);
        [Tooltip("Цвет линии при полной силе — яркий.")]
        [SerializeField] private Color fullColor = new Color(1f, 0.6f, 0.05f);
        [Tooltip("Толщина линии при самом слабом запуске, м.")]
        [SerializeField, Min(0.001f)] private float weakWidth = 0.05f;
        [Tooltip("Толщина линии при полной силе, м.")]
        [SerializeField, Min(0.001f)] private float fullWidth = 0.18f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock lineProperties;

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
            ApplyPowerLook(launchInput.AimPower);
            stopMarker.SetPositionAndRotation(PredictedStop + floorShift, Quaternion.identity);
            SetVisible(true);
        }

        /// <summary>Только вид линии: цвет через MaterialPropertyBlock, общий материал не меняется.</summary>
        private void ApplyPowerLook(float power)
        {
            line.widthMultiplier = Mathf.Lerp(weakWidth, fullWidth, power);
            lineProperties ??= new MaterialPropertyBlock();
            line.GetPropertyBlock(lineProperties);
            lineProperties.SetColor(BaseColorId, Color.Lerp(weakColor, fullColor, power));
            line.SetPropertyBlock(lineProperties);
        }

        private void SetVisible(bool visible)
        {
            if (line.enabled != visible) line.enabled = visible;
            if (stopMarker.gameObject.activeSelf != visible) stopMarker.gameObject.SetActive(visible);
        }
    }
}
