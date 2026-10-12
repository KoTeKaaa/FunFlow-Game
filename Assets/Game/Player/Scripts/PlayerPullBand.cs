using UnityEngine;

namespace FunFlow.Player
{
    /// <summary>
    /// Только визуал: тонкая «резинка» от шарика в сторону оттягивания, длина от силы.
    /// Видна, пока отпускание даст запуск; исчезает при отпускании или отмене.
    /// </summary>
    public class PlayerPullBand : MonoBehaviour
    {
        [SerializeField] private PlayerLaunchInput launchInput;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private LineRenderer band;
        [Tooltip("Длина резинки при полной силе, м.")]
        [SerializeField, Min(0.05f)] private float maxLength = 1.6f;
        [Tooltip("Длина резинки при самом слабом запуске, м.")]
        [SerializeField, Min(0f)] private float minLength = 0.25f;
        [Tooltip("Толщина резинки у шарика, м.")]
        [SerializeField, Min(0.001f)] private float startWidth = 0.05f;
        [Tooltip("Толщина резинки на конце, м.")]
        [SerializeField, Min(0.001f)] private float endWidth = 0.02f;
        [Tooltip("Высота резинки над полом, м.")]
        [SerializeField, Min(0f)] private float floorOffset = 0.03f;
        [Tooltip("Радиус шарика: от центра до пола, м.")]
        [SerializeField, Min(0.01f)] private float ballRadius = 0.5f;
        [Tooltip("Цвет у шарика; к концу резинка становится прозрачной.")]
        [SerializeField] private Color color = new Color(1f, 0.92f, 0.7f, 0.85f);

        private void OnDisable() => SetVisible(false);

        private void LateUpdate()
        {
            if (!launchInput.HasLaunchableAim)
            {
                SetVisible(false);
                return;
            }

            float power = launchInput.AimPower;
            Vector3 origin = motor.transform.position + Vector3.down * (ballRadius - floorOffset);
            Vector3 pullDirection = -launchInput.AimDirection;
            band.SetPosition(0, origin + pullDirection * ballRadius);
            band.SetPosition(1, origin + pullDirection * (ballRadius + Mathf.Lerp(minLength, maxLength, power)));
            band.startWidth = startWidth;
            band.endWidth = endWidth;
            band.startColor = color;
            band.endColor = new Color(color.r, color.g, color.b, 0f);
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            if (band.enabled != visible) band.enabled = visible;
        }
    }
}
