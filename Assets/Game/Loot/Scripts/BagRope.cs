using UnityEngine;

// Мешок на верёвке: своя инерция через пружину с затуханием к точке покоя позади вора.
// Двигает весь объект мешка вместе с областью попадания для резаков; вора не тянет.
// LootBagVisual по-прежнему отвечает за размер; его жёсткое следование перекрывается здесь
// (порядок выполнения после него).
[DefaultExecutionOrder(100), DisallowMultipleComponent]
public sealed class BagRope : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Rigidbody thief;
    [SerializeField] private LineRenderer rope;
    [Tooltip("Наклоняемая модель мешка (дочерний объект); коллайдер остаётся без поворота")]
    [SerializeField] private Transform model;
    [Tooltip("Точка крепления верёвки на модели мешка (завязка)")]
    [SerializeField] private Transform knot;

    [Header("Верёвка")]
    [Tooltip("Точка покоя относительно центра вора")]
    [SerializeField] private Vector3 restOffset = new Vector3(-0.8f, -0.1f, 0f);
    [SerializeField, Min(0.1f)] private float length = 1.6f;
    [Tooltip("Жёсткость пружины к точке покоя, 1/с²")]
    [SerializeField, Min(0f)] private float stiffness = 45f;
    [Tooltip("Затухание (сопротивление пола), 1/с")]
    [SerializeField, Min(0f)] private float damping = 3.5f;
    [Tooltip("Минимальное расстояние по X от центра вора до края мешка")]
    [SerializeField, Min(0f)] private float minBehind = 0.35f;
    [Tooltip("Слои, сквозь которые мешок не проходит (стены, препятствия)")]
    [SerializeField] private LayerMask obstacles = 1 << 10;

    [Header("Вид")]
    [SerializeField, Range(0f, 45f)] private float maxLean = 25f;
    [SerializeField, Min(0f)] private float ropeSag = 0.35f;

    private readonly Collider[] _hits = new Collider[8];
    private BoxCollider _area;
    private Vector3 _position, _previous, _velocity;

    public float Length => length;

    private void OnEnable()
    {
        _area = GetComponent<BoxCollider>();
        if (thief == null)
        {
            Debug.LogError("BagRope: назначьте Rigidbody вора в Inspector.", this);
            enabled = false;
            return;
        }
        Snap();
    }

    // Мешок появляется в покое: повтор попытки начинается с той же позиции.
    private void Snap()
    {
        _position = thief.position + restOffset;
        _velocity = Vector3.zero;
        Constrain(thief.position, Vector3.zero);
        _previous = _position;
        Apply(_position, Vector3.zero);
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector3 thiefVelocity = thief.linearVelocity;
        // FixedUpdate идёт до шага физики: берём положение вора после этого шага,
        // иначе верёвка на рывке растягивается на v·dt.
        Vector3 center = thief.position + thiefVelocity * dt;
        _previous = _position;

        // Пружина к точке покоя и сопротивление скольжению: при рывке мешок отстаёт,
        // при остановке вора доезжает по инерции и раскачивается.
        Vector3 rest = center + restOffset;
        Vector3 acceleration = stiffness * (rest - _position) - damping * _velocity;
        _velocity += acceleration * dt;
        _velocity.y = 0f;
        _position += _velocity * dt;
        _position.y = rest.y;

        PushOutOfObstacles();
        Constrain(center, thiefVelocity);
        Apply(_position, thiefVelocity);
    }

    private void Constrain(Vector3 center, Vector3 thiefVelocity)
    {
        // Верёвка натянута: не дальше длины, внешняя составляющая скорости гасится.
        Vector3 fromThief = _position - center;
        fromThief.y = 0f;
        float planar = Mathf.Sqrt(Mathf.Max(0.0001f, length * length - restOffset.y * restOffset.y));
        if (fromThief.magnitude > planar)
        {
            Vector3 direction = fromThief.normalized;
            _position = center + direction * planar + Vector3.up * restOffset.y;
            float outward = Vector3.Dot(_velocity - thiefVelocity, direction);
            if (outward > 0f) _velocity -= direction * outward;
        }

        // Мешок всегда позади: по X не обгоняет вора (зазор растёт вместе с размером мешка).
        float halfSize = _area != null ? 0.5f * _area.size.x * transform.lossyScale.x : 0f;
        float maxX = center.x - minBehind - halfSize;
        if (_position.x > maxX)
        {
            _position.x = maxX;
            if (_velocity.x > thiefVelocity.x) _velocity.x = thiefVelocity.x;
        }
    }

    private void PushOutOfObstacles()
    {
        if (_area == null) return;
        int count = Physics.OverlapBoxNonAlloc(_position + Vector3.Scale(_area.center, transform.lossyScale),
            0.5f * Vector3.Scale(_area.size, transform.lossyScale), _hits, Quaternion.identity, obstacles,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var other = _hits[i];
            if (!Physics.ComputePenetration(_area, _position, Quaternion.identity, other,
                    other.transform.position, other.transform.rotation, out Vector3 direction, out float distance))
                continue;
            direction.y = 0f;
            // Касание вплотную (мешок у стены в покое) не выталкиваем: иначе он дрожит на месте.
            if (distance < 0.01f || direction.sqrMagnitude < 0.0001f) continue;
            direction.Normalize();
            _position += direction * distance;
            float into = Vector3.Dot(_velocity, direction);
            if (into < 0f) _velocity -= direction * into * 1.4f; // небольшой отскок от стены
        }
    }

    private void Apply(Vector3 position, Vector3 thiefVelocity)
    {
        transform.SetPositionAndRotation(position, Quaternion.identity);
        if (model == null) return;

        // Модель наклоняется по относительной скорости: тянется за вором, раскачивается при остановке.
        Vector3 relative = _velocity - thiefVelocity;
        Vector3 axis = Vector3.Cross(Vector3.up, relative);
        float angle = Mathf.Clamp(relative.magnitude * 6f, 0f, maxLean);
        model.localRotation = axis.sqrMagnitude > 0.0001f
            ? Quaternion.AngleAxis(-angle, axis.normalized)
            : Quaternion.identity;
    }

    // После LootBagVisual: возвращаем симулированное положение, сглаженное между шагами физики,
    // как у интерполируемого Rigidbody вора.
    private void LateUpdate()
    {
        float alpha = Time.fixedDeltaTime > 0f
            ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime) : 1f;
        transform.position = Vector3.Lerp(_previous, _position, alpha);
        DrawRope();
    }

    private void DrawRope()
    {
        if (rope == null) return;
        Vector3 start = thief.transform.position;
        Vector3 end = knot != null ? knot.position : transform.position;
        Vector3 toBag = end - start;
        toBag.y = 0f;
        if (toBag.sqrMagnitude > 0.0001f)
            start += toBag.normalized * 0.45f; // от поверхности шарика, а не из центра

        float slack = Mathf.Clamp01(1f - Vector3.Distance(start, end) / length);
        int count = rope.positionCount;
        for (int i = 0; i < count; i++)
        {
            float t = count > 1 ? i / (float)(count - 1) : 0f;
            Vector3 point = Vector3.Lerp(start, end, t);
            point.y -= ropeSag * slack * 4f * t * (1f - t); // провисает, пока не натянута
            point.y = Mathf.Max(point.y, 0.03f);
            rope.SetPosition(i, point);
        }
    }
}
