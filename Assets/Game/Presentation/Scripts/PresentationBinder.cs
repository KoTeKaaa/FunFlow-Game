using UnityEngine;

namespace FunFlow.Presentation
{
    /// <summary>
    /// Подключает HUD к контрактам Core/Contracts: IRunSession, ILootInventory, IPlayerControl.
    /// Только читает данные и подписывается на события; своих денег и таймера не ведёт.
    /// Ссылки на источники назначает №1 в Game.unity (на экземпляре HUD).
    /// </summary>
    public sealed class PresentationBinder : MonoBehaviour
    {
        [SerializeField] private PresentationController presentation;

        [Header("Источники данных (назначает №1 в Game.unity)")]
        [Tooltip("Компонент Core, реализующий IRunSession")]
        [SerializeField] private MonoBehaviour runSessionSource;
        [Tooltip("Компонент Loot, реализующий ILootInventory")]
        [SerializeField] private MonoBehaviour lootSource;
        [Tooltip("Компонент Player, реализующий IPlayerControl")]
        [SerializeField] private MonoBehaviour playerSource;

        [Header("Необязательно")]
        [Tooltip("Над чем всплывают «+N/−N». Пусто — объект игрока (playerSource)")]
        [SerializeField] private Transform playerTransform;
        [Tooltip("Rigidbody игрока — для громкости звука запуска")]
        [SerializeField] private Rigidbody playerBody;
        [Tooltip("Общий конфиг: «% дальности» и максимальная скорость запуска")]
        [SerializeField] private GameConfig config;
        [Tooltip("Начало и выход уровня (от №4). Пусто — блок «До выхода» скрыт")]
        [SerializeField] private Transform routeStart;
        [SerializeField] private Transform routeExit;

        private IRunSession session;
        private ILootInventory loot;
        private IPlayerControl player;
        private bool started;
        private int lastValue;
        private int launches, items, cuts;

        private Transform PlayerTransform =>
            playerTransform != null ? playerTransform : (playerSource != null ? playerSource.transform : null);

        private Vector3 PopupPoint
        {
            get
            {
                Transform t = PlayerTransform;
                return t != null ? t.position + Vector3.up * 0.8f : Vector3.zero;
            }
        }

        private void OnEnable()
        {
            session = Resolve<IRunSession>(runSessionSource, nameof(runSessionSource));
            loot = Resolve<ILootInventory>(lootSource, nameof(lootSource));
            player = Resolve<IPlayerControl>(playerSource, nameof(playerSource));

            if (session != null)
            {
                session.StateChanged += HandleStateChanged;
                session.RemainingTimeChanged += HandleTimeChanged;
                session.Finished += HandleFinished;
            }
            if (loot != null) loot.Changed += HandleLootChanged;
            if (player != null) player.Launched += HandleLaunched;
            if (presentation != null)
            {
                presentation.StartClicked += HandleStartClicked;
                presentation.RestartClicked += HandleRestartClicked;
            }

            if (started) SyncAll(); // повторное включение: перечитываем текущее состояние
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.StateChanged -= HandleStateChanged;
                session.RemainingTimeChanged -= HandleTimeChanged;
                session.Finished -= HandleFinished;
            }
            if (loot != null) loot.Changed -= HandleLootChanged;
            if (player != null) player.Launched -= HandleLaunched;
            if (presentation != null)
            {
                presentation.StartClicked -= HandleStartClicked;
                presentation.RestartClicked -= HandleRestartClicked;
            }
            session = null;
            loot = null;
            player = null;
        }

        // Start — после Awake/OnEnable всех объектов сцены, поэтому источники уже инициализированы.
        private void Start()
        {
            started = true;
            if (presentation == null)
            {
                Debug.LogError("PresentationBinder: не назначен PresentationController.", this);
                enabled = false;
                return;
            }
            if (session == null)
                Debug.LogWarning("PresentationBinder: IRunSession не подключён — HUD работает без данных игры.", this);
            SyncAll();
        }

        private void Update()
        {
            if (presentation == null) return;
            Transform p = PlayerTransform;
            if (routeStart == null || routeExit == null || p == null) return;

            float total = routeExit.position.x - routeStart.position.x;
            if (total <= 0.01f) return;
            float done = p.position.x - routeStart.position.x;
            presentation.SetRoute(Mathf.Max(0f, total - done), done / total);
        }

        // ---------- Синхронизация ----------

        private void SyncAll()
        {
            presentation.SetRouteVisible(routeStart != null && routeExit != null);
            lastValue = loot != null ? loot.CurrentValue : 0;

            if (session == null)
            {
                PushLoot();
                return;
            }

            switch (session.State)
            {
                case RunState.Waiting:
                    presentation.ShowStartScreen();
                    break;
                case RunState.Playing:
                    presentation.BeginRun();
                    break;
                case RunState.Alarm:
                    presentation.BeginRun();
                    presentation.NotifyAlarmStarted(session.RemainingTime);
                    break;
                case RunState.Won:
                case RunState.Lost:
                    if (session.Result.HasValue) ShowResult(session.Result.Value);
                    break;
            }
            PushLoot();
        }

        private void PushLoot()
        {
            int value = loot != null ? loot.CurrentValue : 0;
            float weight = loot != null ? loot.NormalizedWeight : 0f;
            presentation.SetLoot(value, weight, RangeFor(weight));
        }

        /// <summary>
        /// Доля дальности по общему конфигу: скорость падает до FullWeightSpeedMultiplier,
        /// путь при постоянном замедлении ∝ v². Без конфига — −1 (показываем только тяжесть).
        /// </summary>
        private float RangeFor(float weight01)
        {
            if (config == null) return -1f;
            float speedMul = Mathf.Lerp(1f, config.FullWeightSpeedMultiplier, Mathf.Clamp01(weight01));
            return speedMul * speedMul;
        }

        private void ShowResult(RunResult result)
        {
            float alarmLeft = session != null && session.HasAlarmTriggered ? session.RemainingTime : -1f;
            presentation.ShowResult(new ResultInfo(result.IsVictory, result.FinalValue, alarmLeft, launches, items, cuts));
        }

        // ---------- События контрактов ----------

        private void HandleStateChanged(RunState state)
        {
            switch (state)
            {
                case RunState.Waiting:
                    presentation.ShowStartScreen();
                    PushLoot();
                    break;
                case RunState.Playing:
                    launches = items = cuts = 0;
                    lastValue = loot != null ? loot.CurrentValue : 0;
                    presentation.BeginRun();
                    PushLoot();
                    break;
                case RunState.Alarm:
                    presentation.NotifyAlarmStarted(session.RemainingTime);
                    break;
                // Won/Lost показываем по Finished: к нему итог уже зафиксирован.
            }
        }

        private void HandleTimeChanged(float remaining)
        {
            if (session != null && session.HasAlarmTriggered) presentation.SetAlarmTime(remaining);
        }

        private void HandleFinished(RunResult result) => ShowResult(result);

        private void HandleLootChanged()
        {
            int value = loot.CurrentValue;
            int delta = value - lastValue;
            lastValue = value;

            if (delta > 0)
            {
                items++;
                presentation.NotifyPickup(PopupPoint, delta);
            }
            else if (delta < 0)
            {
                cuts++;
                presentation.NotifyCut(PopupPoint, -delta);
            }
            PushLoot();
        }

        private void HandleLaunched()
        {
            launches++;
            float power = 0.8f;
            if (playerBody != null && config != null && config.MaxLaunchSpeed > 0f)
            {
                float speed = playerBody.linearVelocity.magnitude;
                if (speed > 0.01f) power = Mathf.Clamp01(speed / config.MaxLaunchSpeed);
            }
            presentation.NotifyLaunch(power);
        }

        private void HandleStartClicked()
        {
            if (session == null)
            {
                Debug.LogWarning("PresentationBinder: «Начать» нажато, но IRunSession не подключён — некому начать попытку.", this);
                return;
            }
            session.StartRun();
            if (session.State == RunState.Waiting)
                Debug.LogWarning($"PresentationBinder: StartRun() вызван, но {runSessionSource.GetType().Name} остался в Waiting.", this);
        }

        private void HandleRestartClicked()
        {
            if (session == null)
            {
                Debug.LogWarning("PresentationBinder: «Ещё раз» нажато, но IRunSession не подключён.", this);
                return;
            }
            session.RestartRun();
        }

        private T Resolve<T>(MonoBehaviour source, string field) where T : class
        {
            if (source == null) return null;
            var result = source as T;
            if (result == null)
                Debug.LogError($"PresentationBinder: {field} = {source.GetType().Name} не реализует {typeof(T).Name}.", this);
            return result;
        }
    }
}
