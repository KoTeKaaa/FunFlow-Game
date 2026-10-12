using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public enum GuardTurnMode
{
    Static,
    Sweep,
    Rotate
}

/// <summary>
/// Охранник: поворачивается по выбранному режиму, видит вора в секторе на полу (стены закрывают обзор)
/// и включает тревогу тем же вызовом, что сигнальная плита AlarmTrap. Конус рисуется и в редакторе без Play.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class Guard : MonoBehaviour
{
    // Запас радиуса поиска: шарик мог за шаг физики выйти за дальность, но пересечь конус.
    private const float SearchMargin = 5f;
    private const float SampleStep = 0.1f;
    private const int MaxSamples = 64;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [Header("Тревога")]
    [Tooltip("Объект Core с RunSession (GameRoot в сцене уровня) — тот же, что у сигнальных плит AlarmTrap. Назначается в сцене.")]
    [SerializeField] private MonoBehaviour coreSessionSource;

    [Header("Зрение")]
    [Tooltip("Дальность взгляда по полу, метров.")]
    [SerializeField, Range(1f, 20f)] private float viewRange = 6f;
    [Tooltip("Полный угол обзора от края до края конуса, градусов.")]
    [SerializeField, Range(10f, 180f)] private float viewAngle = 60f;
    [Tooltip("Высота лучей взгляда над основанием охранника. 0.5 — центр шарика: препятствия ниже не закрывают обзор.")]
    [SerializeField, Range(0.1f, 2f)] private float eyeHeight = 0.5f;
    [Tooltip("Слои, которые закрывают обзор: стены и препятствия (Default, Obstacle).")]
    [SerializeField] private LayerMask obstacleMask = (1 << 0) | (1 << 10);
    [Tooltip("Слой вора (Player).")]
    [SerializeField] private LayerMask thiefMask = 1 << 8;
    [Tooltip("Число лучей в веере конуса. Больше — точнее обрезка стенами, дороже расчёт.")]
    [SerializeField, Range(8, 128)] private int rayCount = 48;

    [Header("Поворот")]
    [Tooltip("Static — стоит; Sweep — качается между углами A и B с паузой на краях; Rotate — поворачивается на шаг и ждёт.")]
    [SerializeField] private GuardTurnMode turnMode = GuardTurnMode.Sweep;
    [Tooltip("Скорость поворота, градусов в секунду.")]
    [SerializeField, Range(5f, 360f)] private float turnSpeed = 40f;
    [Tooltip("Пауза перед первым поворотом после загрузки уровня, секунд. Разные значения рассинхронизируют охранников.")]
    [SerializeField, Min(0f)] private float startDelay;
    [Tooltip("Sweep: первый крайний угол относительно направления, в котором охранник поставлен. Минус — влево (вид сверху). С него охранник начинает.")]
    [SerializeField, Range(-180f, 180f)] private float sweepAngleA = -45f;
    [Tooltip("Sweep: второй крайний угол относительно направления установки. Плюс — вправо (вид сверху).")]
    [SerializeField, Range(-180f, 180f)] private float sweepAngleB = 45f;
    [Tooltip("Sweep: пауза на каждом краю, секунд.")]
    [SerializeField, Min(0f)] private float sweepPause = 1f;
    [Tooltip("Rotate: шаг поворота, градусов. 90 — смотрит по очереди в четыре стороны.")]
    [SerializeField, Range(10f, 180f)] private float rotateStep = 90f;
    [Tooltip("Rotate: пауза после каждого шага, секунд.")]
    [SerializeField, Min(0f)] private float rotatePause = 1.5f;
    [Tooltip("Rotate: поворачиваться по часовой стрелке (вид сверху); выключено — против.")]
    [SerializeField] private bool rotateClockwise = true;

    [Header("Вид")]
    [Tooltip("Поворачиваемая часть (тело с «лицом»). Корень не вращается, поэтому расстановка не меняется.")]
    [SerializeField] private Transform head;
    [Tooltip("Полупрозрачный материал конуса (URP Unlit, Transparent).")]
    [SerializeField] private Material coneMaterial;
    [Tooltip("Цвет конуса, пока вор не замечен.")]
    [SerializeField] private Color coneColor = new Color(1f, 0.08f, 0.05f, 0.22f);
    [Tooltip("Цвет конуса после того, как охранник заметил вора (мигает между обычным и этим).")]
    [SerializeField] private Color alertColor = new Color(1f, 0.15f, 0.05f, 0.65f);
    [Tooltip("Частота мигания после тревоги, раз в секунду.")]
    [SerializeField, Range(0.5f, 10f)] private float alertBlinkRate = 3f;
    [Tooltip("Подъём конуса над полом, метров — чтобы он не мерцал вместе с полом.")]
    [SerializeField, Range(0.005f, 0.2f)] private float coneHeight = 0.03f;

    private enum Phase
    {
        Delay,
        Turning,
        Holding
    }

    // Номер входа в Play: при отключённой перезагрузке сцены поля объекта переживают вход,
    // поэтому состояние попытки сбрасывается по смене этого номера.
    private static int s_playSession;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => s_playSession++;

    private readonly Collider[] _thiefHits = new Collider[8];
    private int _session = -1;
    private IRunSession _runSession;
    private Phase _phase;
    private float _yaw;
    private float _targetYaw;
    private float _timer;
    private bool _nextSweepToB;
    private bool _spotted;
    private float _spottedTime;

    private Mesh _mesh;
    private Vector3[] _vertices;
    private int[] _triangles;
    private int _meshFrame = -1;
    private MaterialPropertyBlock _props;

    /// <summary>Охранник уже заметил вора в этой попытке.</summary>
    public bool HasSpottedThief => _spotted;

    /// <summary>Текущий угол поворота относительно направления установки, градусов.</summary>
    public float CurrentYaw => ViewYaw;

    /// <summary>Куда сейчас смотрит охранник (на полу, единичный вектор).</summary>
    public Vector3 Facing => GuardVision.Direction(PlacementYaw + ViewYaw);

    private Vector3 Eye => transform.position + Vector3.up * eyeHeight;
    private float PlacementYaw => GuardVision.Yaw(transform.forward);
    private float InitialYaw => turnMode == GuardTurnMode.Sweep ? sweepAngleA : 0f;

    // В редакторе без Play охранник всегда показан в начальной позе, даже если Update ещё не вызывался.
    private float ViewYaw => Application.isPlaying ? _yaw : InitialYaw;

    /// <summary>Видит ли охранник точку сейчас: она в секторе и луч к ней не перекрыт стеной.</summary>
    public bool CanSee(Vector3 worldPoint)
    {
        Vector3 eye = Eye;
        if (!GuardVision.IsPointInSector(eye, Facing, viewAngle, viewRange, worldPoint))
            return false;

        Vector3 toPoint = worldPoint - eye;
        toPoint.y = 0f;
        float distance = toPoint.magnitude;
        if (distance < 1e-4f)
            return true;

        return !PhysicsWorld().Raycast(eye, toPoint / distance, out _, distance, obstacleMask, QueryTriggerInteraction.Ignore);
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        if (!Application.isPlaying)
            ShowEditorPose();
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    private void OnDestroy()
    {
        if (_mesh == null)
            return;
        if (Application.isPlaying)
            Destroy(_mesh);
        else
            DestroyImmediate(_mesh);
    }

    private void OnValidate()
    {
        _meshFrame = -1;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // Поворот Transform из OnValidate запрещён — переносим на следующий тик редактора.
            EditorApplication.delayCall -= ShowEditorPoseDelayed;
            EditorApplication.delayCall += ShowEditorPoseDelayed;
        }
#endif
    }

#if UNITY_EDITOR
    private void ShowEditorPoseDelayed()
    {
        if (this != null && !Application.isPlaying)
            ShowEditorPose();
    }
#endif

    private void Update()
    {
        if (!Application.isPlaying)
        {
            ShowEditorPose();
            return;
        }

        EnsureRunState();
        Turn(Time.deltaTime);
        ApplyHead();
    }

    private void FixedUpdate()
    {
        if (!Application.isPlaying)
            return;

        EnsureRunState();
        if (!CanWatch())
            return;

        if (SeesThief())
            Spot();
    }

    private void EnsureRunState()
    {
        if (_session == s_playSession)
            return;

        _session = s_playSession;
        _runSession = coreSessionSource as IRunSession;
        if (coreSessionSource == null)
            Debug.LogWarning($"{name}: не назначен Core Session Source — охранник видит вора, но тревогу не включит.", this);
        else if (_runSession == null)
            Debug.LogError($"{name}: Core Session Source не реализует IRunSession.", this);

        _yaw = InitialYaw;
        _phase = Phase.Delay;
        _timer = startDelay;
        _nextSweepToB = true;
        _spotted = false;
        _meshFrame = -1;
        ApplyHead();
    }

    private void Turn(float deltaTime)
    {
        if (turnMode == GuardTurnMode.Static || deltaTime <= 0f)
            return;

        if (_phase != Phase.Turning)
        {
            _timer -= deltaTime;
            if (_timer > 0f)
                return;
            BeginTurn();
        }

        _yaw = Mathf.MoveTowards(_yaw, _targetYaw, turnSpeed * deltaTime);
        if (_yaw != _targetYaw)
            return;

        if (turnMode == GuardTurnMode.Rotate)
            _yaw = Mathf.Repeat(_yaw, 360f);
        _phase = Phase.Holding;
        _timer = turnMode == GuardTurnMode.Sweep ? sweepPause : rotatePause;
    }

    private void BeginTurn()
    {
        _phase = Phase.Turning;
        if (turnMode == GuardTurnMode.Sweep)
        {
            _targetYaw = _nextSweepToB ? sweepAngleB : sweepAngleA;
            _nextSweepToB = !_nextSweepToB;
        }
        else
        {
            _targetYaw = _yaw + (rotateClockwise ? rotateStep : -rotateStep);
        }
    }

    private bool CanWatch()
    {
        // Без назначенной сессии (Sandbox) охранник всё равно смотрит, чтобы проверить зону.
        if (coreSessionSource == null || _runSession == null)
            return true;
        RunState state = _runSession.State;
        return state == RunState.Playing || state == RunState.Alarm;
    }

    private bool SeesThief()
    {
        int count = PhysicsWorld().OverlapSphere(Eye, viewRange + SearchMargin, _thiefHits, thiefMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider thief = _thiefHits[i];
            Rigidbody body = thief.attachedRigidbody;
            Vector3 now = body != null ? body.position : thief.bounds.center;
            // Проверяем весь отрезок последнего шага физики, чтобы быстрый шарик не проскочил узкий конус.
            Vector3 before = body != null ? now - body.linearVelocity * Time.fixedDeltaTime : now;
            if (SeesAlong(before, now))
                return true;
        }
        return false;
    }

    private bool SeesAlong(Vector3 from, Vector3 to)
    {
        int samples = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(from, to) / SampleStep), 1, MaxSamples);
        for (int i = 0; i <= samples; i++)
        {
            if (CanSee(Vector3.Lerp(from, to, (float)i / samples)))
                return true;
        }
        return false;
    }

    private void Spot()
    {
        if (!_spotted)
        {
            _spotted = true;
            _spottedTime = Time.time;
        }

        // Тот же вызов, что у AlarmTrap: первый запускает отсчёт, повторно Core не сбрасывает и не продлевает.
        if (coreSessionSource != null && _runSession != null && _runSession.State == RunState.Playing)
            _runSession.TriggerAlarm();
    }

    private void ShowEditorPose()
    {
        _yaw = InitialYaw;
        _spotted = false;
        ApplyHead();
    }

    private void ApplyHead()
    {
        if (head == null)
            return;
        Quaternion rotation = Quaternion.Euler(0f, _yaw, 0f);
        if (Quaternion.Angle(head.localRotation, rotation) > 0.01f)
            head.localRotation = rotation;
    }

    private PhysicsScene PhysicsWorld() => gameObject.scene.GetPhysicsScene();

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (coneMaterial == null || camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection)
            return;
#if UNITY_EDITOR
        // В режиме префаба не рисовать чужие конусы: камера сцены и камера префаба видят только свою сцену.
        if (camera.scene.IsValid() ? camera.scene != gameObject.scene : EditorSceneManager.IsPreviewScene(gameObject.scene))
            return;
#endif
        RebuildMesh();
        _props ??= new MaterialPropertyBlock();
        _props.SetColor(BaseColorId, CurrentConeColor());

        var renderParams = new RenderParams(coneMaterial)
        {
            camera = camera,
            layer = gameObject.layer,
            matProps = _props,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            worldBounds = _mesh.bounds
        };
        Graphics.RenderMesh(renderParams, _mesh, 0, Matrix4x4.identity);
    }

    private Color CurrentConeColor()
    {
        if (!_spotted || !Application.isPlaying)
            return coneColor;
        float blink = 0.5f + 0.5f * Mathf.Cos((Time.time - _spottedTime) * alertBlinkRate * 2f * Mathf.PI);
        return Color.Lerp(coneColor, alertColor, 0.35f + 0.65f * blink);
    }

    // Веер лучей из той же точки и по той же маске, что проверка вора: конус обрезается первой стеной.
    private void RebuildMesh()
    {
        if (Application.isPlaying && _meshFrame == Time.frameCount && _mesh != null)
            return;
        _meshFrame = Time.frameCount;

        if (!Application.isPlaying)
            Physics.SyncTransforms();

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "GuardVisionCone", hideFlags = HideFlags.HideAndDontSave };
            _mesh.MarkDynamic();
        }

        int rays = Mathf.Max(rayCount, 2);
        if (_vertices == null || _vertices.Length != rays + 2)
        {
            _vertices = new Vector3[rays + 2];
            _triangles = new int[rays * 3];
            for (int i = 0; i < rays; i++)
            {
                _triangles[i * 3] = 0;
                _triangles[i * 3 + 1] = i + 1;
                _triangles[i * 3 + 2] = i + 2;
            }
            _mesh.Clear();
        }

        PhysicsScene physics = PhysicsWorld();
        Vector3 eye = Eye;
        Vector3 floor = transform.position + Vector3.up * coneHeight;
        float startYaw = PlacementYaw + ViewYaw - viewAngle * 0.5f;
        _vertices[0] = floor;
        for (int i = 0; i <= rays; i++)
        {
            Vector3 direction = GuardVision.Direction(startYaw + viewAngle * i / rays);
            float distance = physics.Raycast(eye, direction, out RaycastHit hit, viewRange, obstacleMask, QueryTriggerInteraction.Ignore)
                ? hit.distance
                : viewRange;
            _vertices[i + 1] = floor + direction * distance;
        }

        _mesh.vertices = _vertices;
        _mesh.triangles = _triangles;
        _mesh.RecalculateBounds();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position + Vector3.up * coneHeight;
        float placement = PlacementYaw;
        float half = viewAngle * 0.5f;

        switch (turnMode)
        {
            case GuardTurnMode.Static:
                Handles.color = new Color(1f, 0.6f, 0.1f, 0.9f);
                DrawRay(center, placement - half, "");
                DrawRay(center, placement + half, "");
                break;

            case GuardTurnMode.Sweep:
            {
                float min = Mathf.Min(sweepAngleA, sweepAngleB);
                float max = Mathf.Max(sweepAngleA, sweepAngleB);
                Handles.color = new Color(1f, 0.6f, 0.1f, 0.35f);
                Handles.DrawWireArc(center, Vector3.up, GuardVision.Direction(placement + min - half), max - min + viewAngle, viewRange);
                DrawRay(center, placement + min - half, "");
                DrawRay(center, placement + max + half, "");
                Handles.color = new Color(1f, 0.6f, 0.1f, 0.95f);
                DrawRay(center, placement + sweepAngleA, "A");
                DrawRay(center, placement + sweepAngleB, "B");
                break;
            }

            case GuardTurnMode.Rotate:
            {
                Handles.color = new Color(1f, 0.6f, 0.1f, 0.35f);
                Handles.DrawWireDisc(center, Vector3.up, viewRange);
                Handles.color = new Color(1f, 0.6f, 0.1f, 0.95f);
                int stops = Mathf.Min(Mathf.CeilToInt(360f / rotateStep - 0.001f), 36);
                float direction = rotateClockwise ? 1f : -1f;
                for (int i = 0; i < stops; i++)
                    DrawRay(center, placement + direction * rotateStep * i, i.ToString());
                break;
            }
        }
    }

    private void DrawRay(Vector3 center, float yaw, string label)
    {
        Vector3 end = center + GuardVision.Direction(yaw) * viewRange;
        Handles.DrawLine(center, end, 2f);
        if (label.Length > 0)
            Handles.Label(end, label);
    }
#endif
}
