using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FunFlow.Presentation.Sandbox
{
    /// <summary>
    /// ТОЛЬКО для PresentationSandbox. Поддельные Core + Loot + Player по настоящим контрактам,
    /// чтобы проверить PresentationBinder без остальных модулей. Состояния попытки берутся
    /// из настоящего RunStateMachine участника №1. В Game.unity не добавлять.
    /// </summary>
    public sealed class SandboxFakeGame : MonoBehaviour, IRunSession, ILootInventory, IPlayerControl
    {
        [SerializeField] private Transform thief;
        [SerializeField] private GameConfig config;
        [Tooltip("Только для клавиши 7 (удар): события удара пока нет в контрактах. Пусто — найдётся сам.")]
        [SerializeField] private PresentationController presentation;

        [Header("Фальшивый уровень")]
        [SerializeField] private float startX = -8f;
        [SerializeField] private float endX = 8f;
        [SerializeField] private float launchStep = 2.5f;

        // ---------- IRunSession ----------
        public RunState State => machine.State;
        public bool HasAlarmTriggered => machine.HasAlarmTriggered;
        public float RemainingTime { get; private set; }
        public RunResult? Result => machine.Result;
        public event Action<RunState> StateChanged;
        public event Action<float> RemainingTimeChanged;
        public event Action<RunResult> Finished;

        // ---------- ILootInventory ----------
        public int CurrentValue { get; private set; }
        public float NormalizedWeight => Mathf.Clamp01(CurrentValue / (float)FullWeightValue);
        public event Action Changed;

        // ---------- IPlayerControl ----------
        public bool IsMoving { get; private set; }
        public event Action Launched;

        private readonly RunStateMachine machine = new RunStateMachine();
        private bool controlEnabled;
        private float targetX;

        private int FullWeightValue => config != null ? config.ValueAtFullWeight : 1000;
        private float AlarmDuration => config != null ? config.AlarmDurationSeconds : 60f;

        private void Start()
        {
            // Песочница: однократный поиск при старте допустим (в игре так не делаем).
            if (presentation == null) presentation = FindFirstObjectByType<PresentationController>();
        }

        private void Awake()
        {
            targetX = startX;
            if (thief != null) thief.position = new Vector3(startX, thief.position.y, thief.position.z);
        }

        // ---------- Команды IRunSession ----------

        public void StartRun()
        {
            if (!machine.StartRun()) return;
            SetControlEnabled(true);
            StateChanged?.Invoke(machine.State);
        }

        public void RestartRun()
        {
            // Как договорено в контракте: перезагрузка сцены. Песочница не в списке сборки,
            // поэтому в редакторе грузим её по пути.
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                SceneManager.GetActiveScene().path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
#endif
        }

        public void TriggerAlarm()
        {
            if (!machine.TriggerAlarm()) return; // повторный вызов игнорируется — как в Core
            RemainingTime = AlarmDuration;
            StateChanged?.Invoke(machine.State);
            RemainingTimeChanged?.Invoke(RemainingTime);
        }

        public void ReachExit() => Finish(true);

        // ---------- Команды ILootInventory / IPlayerControl ----------

        public void AddValue(int value)
        {
            if (value <= 0) return;
            CurrentValue += value;
            Changed?.Invoke();
        }

        public int RemoveFraction(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            int lost = Mathf.Min(CurrentValue, Mathf.CeilToInt(CurrentValue * fraction));
            if (lost <= 0) return 0;
            CurrentValue -= lost;
            Changed?.Invoke();
            return lost;
        }

        public void SetControlEnabled(bool enabled) => controlEnabled = enabled;

        // ---------- Песочница ----------

        private void Update()
        {
            MoveThief();
            bool inRun = State == RunState.Playing || State == RunState.Alarm;
            Keyboard kb = Keyboard.current;

            if (inRun && kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame) Launch();
                if (kb.digit1Key.wasPressedThisFrame) AddValue(100);
                if (kb.digit2Key.wasPressedThisFrame) AddValue(320);
                if (kb.digit3Key.wasPressedThisFrame) RemoveFraction(0.35f);
                if (kb.digit4Key.wasPressedThisFrame) TriggerAlarm();
                if (kb.digit5Key.wasPressedThisFrame) ReachExit();
                // 7 — удар о стену: в игре придёт от PlayerMotor.Collided, когда №1 добавит его в контракт
                if (kb.digit7Key.wasPressedThisFrame && presentation != null && thief != null)
                    presentation.NotifyImpact(thief.position + Vector3.right * 0.5f, UnityEngine.Random.Range(0.3f, 1f));
                // 6 — быстро проверить поражение: остаётся 3 секунды тревоги
                if (kb.digit6Key.wasPressedThisFrame && State == RunState.Alarm) RemainingTime = Mathf.Min(RemainingTime, 3f);
            }

            if (State != RunState.Alarm) return;
            RemainingTime = Mathf.Max(0f, RemainingTime - Time.deltaTime);
            if (RemainingTime <= 0f)
            {
                Finish(false);
                return;
            }
            RemainingTimeChanged?.Invoke(RemainingTime);
        }

        private void Launch()
        {
            if (!controlEnabled || IsMoving) return;
            float heavy = Mathf.Lerp(1f, config != null ? config.FullWeightSpeedMultiplier : 0.4f, NormalizedWeight);
            targetX = Mathf.Min(endX, targetX + launchStep * heavy * heavy);
            IsMoving = true;
            Launched?.Invoke();
        }

        private void MoveThief()
        {
            if (thief == null) return;
            Vector3 p = thief.position;
            p.x = Mathf.MoveTowards(p.x, targetX, 6f * Time.deltaTime);
            thief.position = p;
            if (IsMoving && Mathf.Abs(p.x - targetX) < 0.01f)
            {
                IsMoving = false;
                if (targetX >= endX) ReachExit();
            }
        }

        private void Finish(bool victory)
        {
            if (!machine.FinishRun(new RunResult(victory, CurrentValue))) return;
            SetControlEnabled(false);
            StateChanged?.Invoke(machine.State);
            if (State == RunState.Lost) RemainingTimeChanged?.Invoke(RemainingTime);
            Finished?.Invoke(machine.Result.Value);
        }
    }
}
