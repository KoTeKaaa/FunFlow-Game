using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace FunFlow.Presentation
{
    /// <summary>
    /// Единственная точка входа в модуль Presentation.
    /// Core (или Sandbox-драйвер) вызывает эти методы, а контроллер раздаёт работу
    /// HUD, экранам, звуку и эффектам. Своих денег, таймера и состояния попытки здесь нет.
    /// </summary>
    public sealed class PresentationController : MonoBehaviour
    {
        [SerializeField] private HudView hud;
        [SerializeField] private HudScreens screens;
        [SerializeField] private HudFx fx;
        [SerializeField] private PresentationAudio sfx;
        [SerializeField] private WorldVfx vfx;

        [Tooltip("Enter — главная кнопка карточки; R — «Ещё раз» на экране результата")]
        [SerializeField] private bool keyboardShortcuts = true;
        [Tooltip("Писать в Console о нажатиях кнопок HUD (для отладки)")]
        [SerializeField] private bool logUiEvents = true;
        [SerializeField, TextArea]
        private string playingHint = "Зажми вора, оттяни и отпусти · врезайся в добычу · выход справа";

        /// <summary>Игрок нажал «Начать» на стартовом экране.</summary>
        public event Action StartClicked;
        /// <summary>Игрок нажал «Ещё раз» (или R) на экране результата.</summary>
        public event Action RestartClicked;

        [Tooltip("Удары чаще этого интервала не озвучиваются (защита от дребезга о стену), с")]
        [SerializeField, Min(0f)] private float impactCooldown = 0.12f;

        private bool alarmAnnounced;
        private int lastTickSecond = -1;
        private float lastImpactTime = -10f;

        private void Awake() => EnsureUiInput();

        /// <summary>
        /// Если у модуля ввода UI потерялись действия (Point/Click), кнопки не нажимаются.
        /// Восстанавливаем стандартные действия Input System при запуске.
        /// </summary>
        private void EnsureUiInput()
        {
            var module = GetComponentInChildren<InputSystemUIInputModule>(true);
            if (module == null) return;
            bool broken = module.actionsAsset == null
                          || module.point == null || module.point.action == null
                          || module.leftClick == null || module.leftClick.action == null;
            if (!broken) return;
            module.AssignDefaultActions();
            Debug.Log("FunFlow HUD: действия UI (Point/Click) восстановлены по умолчанию.", module);
        }

        private void OnEnable()
        {
            if (screens == null) return;
            screens.StartRequested += HandleStart;
            screens.RestartRequested += HandleRestart;
        }

        private void OnDisable()
        {
            if (screens == null) return;
            screens.StartRequested -= HandleStart;
            screens.RestartRequested -= HandleRestart;
        }

        private void Update()
        {
            if (!keyboardShortcuts || screens == null || !screens.IsVisible) return;
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
            bool restart = screens.IsResultShown && kb.rKey.wasPressedThisFrame;
            if (enter || restart) screens.Submit();
        }

        // ---------- Состояние попытки (вызывает Core) ----------

        public void ShowStartScreen()
        {
            ResetFeedback();
            if (hud != null) hud.ResetView(playingHint);
            if (screens != null) screens.ShowStart();
        }

        public void BeginRun()
        {
            ResetFeedback();
            if (hud != null) hud.ResetView(playingHint);
            if (screens != null) screens.Hide();
        }

        public void ShowResult(ResultInfo result)
        {
            if (sfx != null)
            {
                sfx.StopAll();
                if (result.Won) sfx.PlayWin(); else sfx.PlayLose();
            }
            if (fx != null && !result.Won) fx.Flash(HudStyle.Red, 0.4f);
            if (screens != null) screens.ShowResult(result);
        }

        public void SetDefaultStatus(string message)
        {
            if (hud != null) hud.SetDefaultStatus(message);
        }

        // ---------- Данные для HUD ----------

        /// <summary>Текущая стоимость, тяжесть 0…1 и доля дальности 0…1 (1 = пустой мешок).</summary>
        public void SetLoot(int value, float burden01, float range01)
        {
            if (hud != null) hud.SetLoot(value, burden01, range01);
        }

        public void SetRoute(float metersLeft, float progress01)
        {
            if (hud != null) hud.SetRoute(metersLeft, progress01);
        }

        public void SetRouteVisible(bool visible)
        {
            if (hud != null) hud.SetRouteVisible(visible);
        }

        /// <summary>Вызывать каждый кадр, пока идёт тревога. Время считает Core.</summary>
        public void SetAlarmTime(float secondsLeft)
        {
            if (hud != null) hud.SetAlarmTime(secondsLeft);

            int sec = Mathf.CeilToInt(Mathf.Max(0f, secondsLeft));
            if (sec == lastTickSecond) return;
            lastTickSecond = sec;
            if (sec > 0 && sec <= 10 && sfx != null) sfx.PlayTick();
        }

        // ---------- Реакции на события ----------

        public void NotifyLaunch(float power01)
        {
            if (sfx != null) sfx.PlayLaunch(power01);
        }

        /// <summary>
        /// Удар о препятствие. Подходит напрямую к событию PlayerMotor.Collided(point, strength) роли №2.
        /// </summary>
        public void NotifyImpact(Vector3 worldPoint, float strength01)
        {
            strength01 = Mathf.Clamp01(strength01);
            if (Time.time - lastImpactTime < impactCooldown) return;
            lastImpactTime = Time.time;
            if (sfx != null) sfx.PlayImpact(strength01);
            if (vfx != null && strength01 > 0.25f)
                vfx.Burst(worldPoint, HudStyle.Muted, 4 + Mathf.RoundToInt(10f * strength01));
            if (fx != null && strength01 > 0.6f) fx.Flash(Color.white, 0.12f * strength01);
        }

        public void NotifyPickup(Vector3 worldPosition, int amount)
        {
            if (fx != null) fx.ShowPopup(worldPosition, "+" + HudStyle.Money(amount), HudStyle.Gold);
            if (vfx != null) vfx.Burst(worldPosition, HudStyle.Gold, 14);
            if (sfx != null) sfx.PlayPickup(amount);
        }

        public void NotifyShowcaseBroken(Vector3 worldPosition)
        {
            if (vfx != null) vfx.Burst(worldPosition, HudStyle.Mint, 28);
            if (sfx != null) sfx.PlayShatter();
        }

        public void NotifyCut(Vector3 worldPosition, int lost)
        {
            if (lost <= 0) return;
            if (fx != null)
            {
                fx.ShowPopup(worldPosition, "−" + HudStyle.Money(lost), HudStyle.Orange);
                fx.Flash(HudStyle.Orange, 0.25f);
            }
            if (vfx != null) vfx.Burst(worldPosition, HudStyle.Orange, 18);
            if (sfx != null) sfx.PlayCut();
            if (hud != null) hud.ShowStatus($"Лезвия! Потеряно {HudStyle.Money(lost)} {HudStyle.Coins(lost)} — зато мешок стал легче.", 2.5f);
        }

        /// <summary>Первое срабатывание тревоги. Повторные вызовы ничего не делают.</summary>
        public void NotifyAlarmStarted(float secondsLeft)
        {
            if (alarmAnnounced) return;
            alarmAnnounced = true;
            SetAlarmTime(secondsLeft);
            if (fx != null) fx.Flash(HudStyle.Red, 0.35f);
            if (sfx != null) sfx.PlayAlarm();
            if (hud != null) hud.ShowStatus($"Сигнализация! До выхода — {Mathf.CeilToInt(secondsLeft)} секунд.", 4f);
        }

        private void ResetFeedback()
        {
            alarmAnnounced = false;
            lastTickSecond = -1;
            if (fx != null) fx.ClearAll();
            if (sfx != null) sfx.StopAll();
        }

        private void HandleStart()
        {
            if (logUiEvents)
                Debug.Log($"FunFlow HUD: нажато «Начать», подписчиков: {StartClicked?.GetInvocationList().Length ?? 0}", this);
            StartClicked?.Invoke();
        }

        private void HandleRestart()
        {
            if (logUiEvents)
                Debug.Log($"FunFlow HUD: нажато «Ещё раз», подписчиков: {RestartClicked?.GetInvocationList().Length ?? 0}", this);
            RestartClicked?.Invoke();
        }
    }
}
