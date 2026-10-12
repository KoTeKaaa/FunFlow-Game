using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FunFlow.Presentation
{
    /// <summary>
    /// Дополняет карточку HudScreens последовательностью уровней: номер уровня, кнопка
    /// «Следующий уровень» после победы и выход в главное меню после последнего.
    /// Главная кнопка карточки по-прежнему перезапускает текущий уровень через IRunSession.
    /// </summary>
    public sealed class HudLevelFlow : MonoBehaviour
    {
        [Header("Источники (назначает №1 в сцене уровня)")]
        [Tooltip("Компонент Core, реализующий ILevelSequence")]
        [SerializeField] private MonoBehaviour levelSequenceSource;
        [Tooltip("Компонент Core, реализующий IRunSession")]
        [SerializeField] private MonoBehaviour runSessionSource;

        [Header("Карточка HUD")]
        [SerializeField] private HudScreens screens;
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private Button levelButton;
        [SerializeField] private TMP_Text levelButtonLabel;
        [SerializeField] private TMP_Text tagText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private TMP_Text primaryLabel;

        [Tooltip("N — та же кнопка уровня на экране результата")]
        [SerializeField] private bool keyboardShortcut = true;

        private ILevelSequence sequence;
        private IRunSession session;
        private bool requested;

        private void OnEnable()
        {
            if (screens == null || levelButton == null)
            {
                Debug.LogError("HudLevelFlow: назначьте HudScreens и кнопку уровня.", this);
                enabled = false;
                return;
            }

            sequence = levelSequenceSource as ILevelSequence;
            session = runSessionSource as IRunSession;
            if (sequence == null || session == null)
            {
                // Сцена из одного уровня (например, PresentationSandbox): карточка работает как раньше.
                levelButton.gameObject.SetActive(false);
                enabled = false;
                return;
            }
            levelButton.onClick.AddListener(Submit);
        }

        private void OnDisable()
        {
            if (levelButton != null) levelButton.onClick.RemoveListener(Submit);
            sequence = null;
            session = null;
        }

        private void Update()
        {
            if (keyboardShortcut && levelButton.gameObject.activeInHierarchy
                && Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame)
                Submit();
        }

        // После HudScreens: карточку заполняют обработчики событий Core в этом же кадре.
        private void LateUpdate()
        {
            bool won = screens.IsResultShown && session.State == RunState.Won;
            SetActive(levelButton.gameObject, won && !requested);
            if (!screens.IsVisible || sequence.LevelCount < 1) return;

            string level = $"Уровень {sequence.LevelNumber} из {sequence.LevelCount}";
            if (!screens.IsResultShown)
            {
                SetText(tagText, level.ToUpperInvariant());
                return;
            }

            if (!won)
            {
                SetText(tagText, level.ToUpperInvariant());
                return;
            }

            // HudScreens keeps the result of this attempt; earlier levels are independent.
            SetText(tagText, $"{level} пройден".ToUpperInvariant());
            SetText(primaryLabel, "Переиграть уровень");
            SetText(levelButtonLabel, sequence.HasNextLevel ? "Следующий уровень →" : "Выход в главное меню");
            SetText(hintText, sequence.HasNextLevel
                ? "N — следующий уровень · R — переиграть"
                : "N — главное меню · R — переиграть уровень");
        }

        /// <summary>То же, что нажатие кнопки уровня. Срабатывает один раз.</summary>
        public void Submit()
        {
            // Карточка уже отправила Restart (HudScreens отключает её) или переход уже начат.
            if (requested || (overlay != null && !overlay.interactable)) return;
            if (!screens.IsResultShown || session.State != RunState.Won) return;

            requested = true;
            if (overlay != null) overlay.interactable = false;
            screens.Hide(); // R/Enter больше не отправят Restart поверх загрузки уровня
            if (sequence.HasNextLevel) sequence.LoadNextLevel();
            else sequence.ReturnToMainMenu();
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target.activeSelf != active) target.SetActive(active);
        }

        private static void SetText(TMP_Text target, string value)
        {
            ButtonLabels.Set(target, value);
        }
    }
}
