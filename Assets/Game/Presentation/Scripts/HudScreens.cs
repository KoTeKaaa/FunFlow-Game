using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FunFlow.Presentation
{
    /// <summary>
    /// Карточка поверх игры: стартовый экран и экран результата с кнопкой повтора.
    /// Сама попытку не запускает — только сообщает о нажатии через события.
    /// </summary>
    public sealed class HudScreens : MonoBehaviour
    {
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private TMP_Text tagText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private UnityEngine.UI.Button primaryButton;
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField, Min(0.01f)] private float fadeTime = 0.2f;
        [SerializeField] private bool showStartOnAwake = true;

        /// <summary>Нажата кнопка на стартовом экране.</summary>
        public event Action StartRequested;
        /// <summary>Нажата кнопка «Ещё раз» на экране результата (или R).</summary>
        public event Action RestartRequested;

        private enum Mode { Hidden, Start, Result }

        private Mode mode = Mode.Hidden;
        private bool latched;

        public bool IsVisible => mode != Mode.Hidden;
        public bool IsResultShown => mode == Mode.Result;

        private void Awake()
        {
            if (showStartOnAwake) ShowStart();
            else
            {
                SetMode(Mode.Hidden);
                if (overlay != null) overlay.alpha = 0f;
            }
        }

        private void OnEnable()
        {
            if (primaryButton != null) primaryButton.onClick.AddListener(Submit);
        }

        private void OnDisable()
        {
            if (primaryButton != null) primaryButton.onClick.RemoveListener(Submit);
        }

        public void ShowStart()
        {
            Fill(
                "Один музей. Один шанс вынести всё.",
                "Разгонись. Разбогатей.\nВыберись.",
                "<b>1.</b> Зажми вора мышью, оттяни назад и отпусти. Чем дальше оттянул — тем сильнее запуск.\n" +
                "<b>2.</b> Врезайся в монеты и витрины. Мешок растёт — каждый запуск становится короче.\n" +
                "<b>3.</b> Лезвия режут мешок. Красные плиты включают тревогу: у тебя будет минута.\n" +
                "Доберись до выхода справа. Назад вернуться нельзя.",
                "Начать ограбление →",
                "Мышь — прицел и запуск · R — заново после результата");
            SetMode(Mode.Start);
        }

        public void ShowResult(ResultInfo r)
        {
            string coins = $"{HudStyle.Money(r.Value)} {HudStyle.Coins(r.Value)}";
            if (r.Won)
            {
                string alarm = r.AlarmTriggered
                    ? $"Ты выбрался — на таймере оставалось {Mathf.CeilToInt(r.AlarmSecondsLeft)} сек."
                    : "Ты выбрался, а сигнализация так и не сработала.";
                Fill("Ограбление завершено",
                    $"Удалось унести\n<color={HudStyle.GoldHex}>{coins}.</color>",
                    alarm + StatsLine(r),
                    "Ещё одно ограбление →",
                    "R — заново");
            }
            else
            {
                Fill("Попытка завершена",
                    "Время вышло.\nОхрана уже здесь.",
                    $"В мешке было {coins}. Попробуй другой маршрут или возьми меньше добычи." + StatsLine(r),
                    "Попробовать снова →",
                    "R — заново");
            }
            SetMode(Mode.Result);
        }

        public void Hide() => SetMode(Mode.Hidden);

        /// <summary>То же, что нажатие главной кнопки. Срабатывает один раз на показ экрана.</summary>
        public void Submit()
        {
            if (latched) return;
            Mode current = mode;
            if (current == Mode.Hidden) return;
            latched = true;
            if (overlay != null) overlay.interactable = false;

            if (current == Mode.Start) StartRequested?.Invoke();
            else RestartRequested?.Invoke();
        }

        private void Update()
        {
            if (overlay == null) return;
            float target = mode == Mode.Hidden ? 0f : 1f;
            overlay.alpha = Mathf.MoveTowards(overlay.alpha, target, Time.unscaledDeltaTime / fadeTime);
        }

        private void SetMode(Mode next)
        {
            mode = next;
            latched = false;
            bool on = next != Mode.Hidden;
            if (overlay != null)
            {
                overlay.blocksRaycasts = on;   // пока карточка видна, клики не уходят в игру
                overlay.interactable = on;
            }

            if (on && primaryButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(primaryButton.gameObject);
        }

        private void Fill(string tag, string title, string body, string button, string hint)
        {
            if (tagText != null) tagText.text = tag.ToUpperInvariant();
            if (titleText != null) titleText.text = title;
            if (bodyText != null) bodyText.text = body;
            if (primaryLabel != null) primaryLabel.text = button;
            if (hintText != null) hintText.text = hint;
        }

        private static string StatsLine(ResultInfo r)
        {
            if (!r.HasStats) return string.Empty;
            return $"\n<color={HudStyle.MutedHex}>{r.Launches} {HudStyle.Plural(r.Launches, "запуск", "запуска", "запусков")} · " +
                   $"{r.Items} {HudStyle.Plural(r.Items, "предмет", "предмета", "предметов")} · " +
                   $"{r.Cuts} {HudStyle.Plural(r.Cuts, "разрез", "разреза", "разрезов")} мешка</color>";
        }
    }
}
