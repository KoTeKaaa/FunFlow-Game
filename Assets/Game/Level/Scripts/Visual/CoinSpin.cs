using UnityEngine;

/// <summary>
/// Только визуал монеты: медленное вращение вокруг вертикали и покачивание.
/// Висит на дочернем объекте без коллайдера; корень с LootItem и триггером не двигается.
/// Высота задаётся от пола, а не от корня: у монет в уровнях корни стоят на разной высоте.
/// </summary>
public class CoinSpin : MonoBehaviour
{
    [Tooltip("Высота центра монеты над полом, м.")]
    [SerializeField] private float hoverHeight = 0.5f;
    [Tooltip("Скорость вращения, градусов в секунду.")]
    [SerializeField] private float spinSpeed = 90f;
    [Tooltip("Амплитуда покачивания вверх-вниз, м.")]
    [SerializeField] private float bobHeight = 0.06f;
    [Tooltip("Частота покачивания, Гц.")]
    [SerializeField] private float bobFrequency = 0.8f;

    private float phase;

    private void OnEnable()
    {
        // Разная фаза у соседних монет — чтобы ряд не качался в унисон.
        Vector3 p = transform.position;
        phase = p.x * 0.37f + p.z * 0.61f;
    }

    private void Update()
    {
        float t = Time.time + phase;
        Vector3 p = transform.position;
        p.y = hoverHeight + Mathf.Sin(t * bobFrequency * 2f * Mathf.PI) * bobHeight;
        transform.SetPositionAndRotation(p, Quaternion.Euler(0f, t * spinSpeed, 0f));
    }
}
