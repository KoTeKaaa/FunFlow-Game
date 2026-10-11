using UnityEngine;

/// <summary>
/// Только визуал: пульсация Emission одного рендерера через MaterialPropertyBlock (общий материал не меняется).
/// Материал должен быть с включённой Emission.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class EmissionPulse : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [SerializeField, ColorUsage(false, true)] private Color emission = Color.red;
    [SerializeField, Min(0f)] private float minIntensity = 0.3f;
    [SerializeField, Min(0f)] private float maxIntensity = 1f;
    [Tooltip("Частота пульсации, Гц.")]
    [SerializeField] private float frequency = 1f;

    private Renderer target;
    private MaterialPropertyBlock block;

    private void OnEnable()
    {
        target = GetComponent<Renderer>();
        block ??= new MaterialPropertyBlock();
    }

    private void OnDisable()
    {
        if (target != null)
            target.SetPropertyBlock(null);
    }

    private void Update()
    {
        float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * frequency * 2f * Mathf.PI);
        target.GetPropertyBlock(block);
        block.SetColor(EmissionColorId, emission * Mathf.Lerp(minIntensity, maxIntensity, wave));
        target.SetPropertyBlock(block);
    }
}
