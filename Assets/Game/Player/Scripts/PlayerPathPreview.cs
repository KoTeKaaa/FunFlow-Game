using UnityEngine;

namespace FunFlow.Player
{
    /// <summary>
    /// Предпросмотр пути по полу во время оттягивания: пунктир из «бегущих» точек на длину скольжения с учётом силы
    /// и тяжести, обрезанный до первого препятствия. Видна только начальная доля пути. Не воздушная траектория.
    /// </summary>
    public class PlayerPathPreview : MonoBehaviour
    {
        [SerializeField] private PlayerLaunchInput launchInput;
        [SerializeField] private PlayerMotor motor;
        [Tooltip("Линия с материалом точки: Texture Mode = Tile, выравнивание по полу.")]
        [SerializeField] private LineRenderer line;
        [SerializeField] private Transform stopMarker;
        [Tooltip("Показывать маркер точки остановки (только если остановка попадает в видимую часть пути).")]
        [SerializeField] private bool showStopMarker;
        [Tooltip("Чуть меньше радиуса шарика, чтобы каст не цеплял пол.")]
        [SerializeField, Min(0.01f)] private float castRadius = 0.48f;
        [SerializeField] private LayerMask obstacleMask = ~(1 << 2);
        [Tooltip("Радиус шарика: от центра до пола, м.")]
        [SerializeField, Min(0.01f)] private float ballRadius = 0.5f;
        [Tooltip("Высота линии и маркера над полом, м.")]
        [SerializeField, Min(0f)] private float floorOffset = 0.02f;

        [Header("Пунктир")]
        [Tooltip("Какая доля полного пути видна: остальное игрок угадывает сам. Стена ближе — линия кончается на ней.")]
        [SerializeField, Range(0.2f, 1f)] private float visibleFraction = 0.6f;
        [Tooltip("Расстояние между точками пунктира, м.")]
        [SerializeField, Min(0.05f)] private float dotSpacing = 0.35f;
        [Tooltip("Скорость «бега» точек вперёд по пути, м/с.")]
        [SerializeField, Min(0f)] private float dotRunSpeed = 1.2f;
        [Tooltip("Ширина пунктира у шарика, м.")]
        [SerializeField, Min(0.001f)] private float startWidth = 0.26f;
        [Tooltip("Ширина пунктира в конце видимой части, м.")]
        [SerializeField, Min(0.001f)] private float endWidth = 0.08f;
        [Tooltip("Непрозрачность в конце видимой части (у шарика — полная).")]
        [SerializeField, Range(0f, 1f)] private float endAlpha = 0f;

        [Header("Цвет от силы")]
        [Tooltip("Цвет при самом слабом запуске — бледный.")]
        [SerializeField] private Color weakColor = new Color(1f, 0.96f, 0.82f);
        [Tooltip("Цвет при полной силе — яркий.")]
        [SerializeField] private Color fullColor = new Color(1f, 0.6f, 0.05f);

        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        private MaterialPropertyBlock lineProperties;

        /// <summary>Точка остановки по предпросмотру (центр шарика); валидна, пока предпросмотр виден.</summary>
        public Vector3 PredictedStop { get; private set; }
        /// <summary>Начало видимой части пути (центр шарика).</summary>
        public Vector3 VisibleStart { get; private set; }
        /// <summary>Конец видимой части пути (центр шарика); валиден, пока предпросмотр виден.</summary>
        public Vector3 VisibleEnd { get; private set; }
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
            // Слабый запуск: хотя бы одна точка за краем шарика, иначе весь пунктир спрятан под ним.
            float visibleDistance = Mathf.Max(distance * visibleFraction, ballRadius + dotSpacing);

            if (Physics.SphereCast(origin, castRadius, direction, out RaycastHit hit, distance,
                    obstacleMask, QueryTriggerInteraction.Ignore))
                distance = hit.distance;

            visibleDistance = Mathf.Min(visibleDistance, distance);
            PredictedStop = origin + direction * distance;
            VisibleStart = origin;
            VisibleEnd = origin + direction * visibleDistance;

            Vector3 floorShift = Vector3.down * (ballRadius - floorOffset);
            line.SetPosition(0, origin + floorShift);
            line.SetPosition(1, VisibleEnd + floorShift);
            ApplyLook(launchInput.AimPower);

            bool markerVisible = showStopMarker && visibleDistance >= distance;
            stopMarker.SetPositionAndRotation(PredictedStop + floorShift, Quaternion.identity);
            SetVisible(true);
            SetMarkerVisible(markerVisible);
        }

        /// <summary>Только вид линии: цвет и ширина через вершины, сдвиг точек через MaterialPropertyBlock.</summary>
        private void ApplyLook(float power)
        {
            Color color = Color.Lerp(weakColor, fullColor, power);
            line.startColor = new Color(color.r, color.g, color.b, 1f);
            line.endColor = new Color(color.r, color.g, color.b, endAlpha);
            line.widthMultiplier = 1f;
            line.startWidth = startWidth;
            line.endWidth = endWidth;

            // Tile: одна точка текстуры на 1 / textureScale метров. Сдвиг назад по текстуре = точки бегут вперёд.
            float tiling = 1f / dotSpacing;
            line.textureScale = new Vector2(tiling, 1f);
            float offset = -Mathf.Repeat(Time.time * dotRunSpeed * tiling, 1f);
            lineProperties ??= new MaterialPropertyBlock();
            line.GetPropertyBlock(lineProperties);
            lineProperties.SetVector(BaseMapStId, new Vector4(1f, 1f, offset, 0f));
            line.SetPropertyBlock(lineProperties);
        }

        private void SetVisible(bool visible)
        {
            if (line.enabled != visible) line.enabled = visible;
            if (!visible) SetMarkerVisible(false);
        }

        private void SetMarkerVisible(bool visible)
        {
            if (stopMarker.gameObject.activeSelf != visible) stopMarker.gameObject.SetActive(visible);
        }
    }
}
