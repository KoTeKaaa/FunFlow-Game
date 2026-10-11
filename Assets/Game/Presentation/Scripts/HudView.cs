using TMPro;
using UnityEngine;

namespace FunFlow.Presentation
{
    /// <summary>
    /// Верхняя панель (добыча, тяжесть, путь, сигнализация) и строка статуса.
    /// Ничего не считает сама: показывает значения, которые ей передали, и плавно анимирует их.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [Header("Мешок")]
        [SerializeField] private TMP_Text lootValue;
        [SerializeField] private TMP_Text weightValue;
        [SerializeField] private RectTransform weightFill;

        [Header("Путь до выхода")]
        [SerializeField] private GameObject routeBlock;
        [SerializeField] private TMP_Text routeValue;
        [SerializeField] private RectTransform routeFill;

        [Header("Сигнализация")]
        [SerializeField] private TMP_Text alarmValue;
        [SerializeField] private CanvasGroup alarmBorder;

        [Header("Статус")]
        [SerializeField] private TMP_Text statusText;

        [Header("Анимация")]
        [SerializeField, Min(0.1f)] private float counterSpeed = 10f;
        [SerializeField, Min(0.1f)] private float meterSpeed = 8f;

        private int targetLoot;
        private float shownLoot;
        private int lastShownLoot = -1;
        private float weightTarget, weightShown;
        private float routeTarget, routeShown;
        private bool alarmActive;
        private float alarmSeconds;
        private int lastAlarmSecond = -1;
        private int lastLoad = -1, lastRange = -2, lastRouteMeters = -1;
        private string defaultStatus = string.Empty;
        private bool statusTemporary;
        private float statusUntil;
        private float punch;
        private Vector3 lootBaseScale = Vector3.one;

        private void Awake()
        {
            if (lootValue != null) lootBaseScale = lootValue.rectTransform.localScale;
        }

        /// <summary>Сброс к началу попытки.</summary>
        public void ResetView(string status)
        {
            targetLoot = 0;
            shownLoot = 0f;
            lastShownLoot = -1;
            lastLoad = -1;
            lastRange = -2;
            lastRouteMeters = -1;
            weightTarget = weightShown = 0f;
            routeTarget = routeShown = 0f;
            SetLoot(0, 0f, 1f);
            punch = 0f;
            SetAlarmIdle();
            statusTemporary = false;
            SetDefaultStatus(status);
            RefreshLoot();
            ApplyMeters();
        }

        public void SetLoot(int value, float burden01, float range01)
        {
            value = Mathf.Max(0, value);
            if (value > targetLoot) punch = 1f;
            targetLoot = value;
            weightTarget = Mathf.Clamp01(burden01);

            if (weightValue == null) return;
            int load = Mathf.RoundToInt(weightTarget * 100f);
            // range01 < 0 — дальность неизвестна, показываем только тяжесть.
            int range = range01 < 0f ? -1 : Mathf.RoundToInt(Mathf.Clamp01(range01) * 100f);
            if (load == lastLoad && range == lastRange) return;
            lastLoad = load;
            lastRange = range;
            weightValue.text = range < 0
                ? $"{load}%"
                : $"{load}%<size=50%><color={HudStyle.MutedHex}> · дальность {range}%</color></size>";
        }

        public void SetRouteVisible(bool visible)
        {
            if (routeBlock != null) routeBlock.SetActive(visible);
        }

        public void SetRoute(float metersLeft, float progress01)
        {
            routeTarget = Mathf.Clamp01(progress01);
            int m = Mathf.Max(0, Mathf.CeilToInt(metersLeft));
            if (routeValue == null || m == lastRouteMeters) return; // можно вызывать каждый кадр
            lastRouteMeters = m;
            routeValue.text = $"{m}<size=65%> м</size>";
        }

        public void SetAlarmIdle()
        {
            alarmActive = false;
            lastAlarmSecond = -1;
            if (alarmValue != null)
            {
                alarmValue.text = "Тихо";
                alarmValue.color = HudStyle.Mint;
            }
            if (alarmBorder != null) alarmBorder.alpha = 0f;
        }

        public void SetAlarmTime(float secondsLeft)
        {
            alarmActive = true;
            alarmSeconds = Mathf.Max(0f, secondsLeft);
            int sec = Mathf.CeilToInt(alarmSeconds);
            if (sec == lastAlarmSecond || alarmValue == null) return;
            lastAlarmSecond = sec;
            alarmValue.text = $"{sec / 60:00}:{sec % 60:00}";
            alarmValue.color = HudStyle.Red;
        }

        /// <summary>Временное сообщение; через <paramref name="seconds"/> вернётся постоянная подсказка.</summary>
        public void ShowStatus(string message, float seconds)
        {
            if (statusText == null) return;
            statusText.text = message;
            statusTemporary = true;
            statusUntil = Time.unscaledTime + Mathf.Max(0.1f, seconds);
        }

        public void SetDefaultStatus(string message)
        {
            defaultStatus = message ?? string.Empty;
            if (!statusTemporary && statusText != null) statusText.text = defaultStatus;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            shownLoot = Mathf.Lerp(shownLoot, targetLoot, 1f - Mathf.Exp(-counterSpeed * dt));
            if (Mathf.Abs(shownLoot - targetLoot) < 0.5f) shownLoot = targetLoot;
            RefreshLoot();

            float k = 1f - Mathf.Exp(-meterSpeed * dt);
            weightShown = Mathf.Lerp(weightShown, weightTarget, k);
            routeShown = Mathf.Lerp(routeShown, routeTarget, k);
            ApplyMeters();

            if (lootValue != null)
            {
                punch = Mathf.Max(0f, punch - dt * 4f);
                lootValue.rectTransform.localScale = lootBaseScale * (1f + 0.18f * punch);
            }

            if (statusTemporary && Time.unscaledTime >= statusUntil)
            {
                statusTemporary = false;
                if (statusText != null) statusText.text = defaultStatus;
            }

            if (alarmBorder != null)
            {
                float speed = alarmSeconds <= 10f ? 10f : 4f;
                alarmBorder.alpha = alarmActive ? 0.5f + 0.35f * Mathf.Sin(Time.unscaledTime * speed) : 0f;
            }
        }

        private void RefreshLoot()
        {
            if (lootValue == null) return;
            int v = Mathf.RoundToInt(shownLoot);
            if (v == lastShownLoot) return;
            lastShownLoot = v;
            lootValue.text = $"{HudStyle.Money(v)}<size=55%><color={HudStyle.MutedHex}> {HudStyle.Coins(v)}</color></size>";
        }

        private void ApplyMeters()
        {
            SetFill(weightFill, weightShown);
            SetFill(routeFill, routeShown);
        }

        private static void SetFill(RectTransform fill, float value)
        {
            if (fill == null) return;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        }
    }
}
