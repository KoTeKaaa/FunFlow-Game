using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FunFlow.Player
{
    /// <summary>
    /// Только визуал: подсветка объектов на ВИДИМОЙ части предпросмотра пути в коридоре шириной с шарик.
    /// Добыча слегка мерцает, ловушки и сигнальные плиты светятся красным. Объекты ищутся по слоям, их материалы,
    /// рендереры и логика не меняются: поверх рисуется светящаяся оболочка из их же мешей.
    /// </summary>
    public class PlayerPathHighlighter : MonoBehaviour
    {
        [SerializeField] private PlayerPathPreview preview;
        [Tooltip("Аддитивный прозрачный материал оболочки (цвет задаётся через _BaseColor).")]
        [SerializeField] private Material glowMaterial;
        [Tooltip("Слои добычи: монеты и витрины.")]
        [SerializeField] private LayerMask lootMask = 1 << 11;
        [Tooltip("Слои опасностей: сигнальные плиты и резаки.")]
        [SerializeField] private LayerMask hazardMask = (1 << 13) | (1 << 14);
        [Tooltip("Радиус коридора — радиус шарика, м.")]
        [SerializeField, Min(0.01f)] private float corridorRadius = 0.5f;

        [Header("Вид")]
        [Tooltip("Цвет мерцания добычи.")]
        [SerializeField] private Color lootColor = new Color(1f, 0.85f, 0.35f);
        [Tooltip("Яркость подсветки добычи на пике мерцания.")]
        [SerializeField, Range(0f, 2f)] private float lootIntensity = 0.9f;
        [Tooltip("Частота мерцания добычи, Гц.")]
        [SerializeField, Min(0f)] private float lootFlickerFrequency = 2.5f;
        [Tooltip("Цвет подсветки ловушек и плит.")]
        [SerializeField] private Color hazardColor = new Color(1f, 0.1f, 0.06f);
        [Tooltip("Яркость красной подсветки ловушек и плит.")]
        [SerializeField, Range(0f, 2f)] private float hazardIntensity = 0.6f;
        [Tooltip("Насколько оболочка больше объекта: 1.06 — на 6%.")]
        [SerializeField, Range(1f, 1.3f)] private float shellScale = 1.06f;

        private const int MaxHits = 32;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly RaycastHit[] hits = new RaycastHit[MaxHits];
        private readonly Collider[] overlaps = new Collider[MaxHits];
        private readonly HashSet<Collider> lootTargets = new HashSet<Collider>();
        private readonly HashSet<Collider> hazardTargets = new HashSet<Collider>();
        // Меши под коллайдером ищем один раз: GetComponentsInChildren каждый кадр — лишний мусор.
        private readonly Dictionary<Collider, MeshFilter[]> meshesByCollider = new Dictionary<Collider, MeshFilter[]>();
        private MaterialPropertyBlock lootProperties;
        private MaterialPropertyBlock hazardProperties;

        private void OnDisable()
        {
            lootTargets.Clear();
            hazardTargets.Clear();
            meshesByCollider.Clear();
        }

        private void LateUpdate()
        {
            lootTargets.Clear();
            hazardTargets.Clear();
            if (!preview.IsVisible || glowMaterial == null) return;

            Collect(lootMask, lootTargets);
            Collect(hazardMask, hazardTargets);
            // Ловушка важнее: объект на обоих слоях светится красным.
            lootTargets.ExceptWith(hazardTargets);

            lootProperties ??= new MaterialPropertyBlock();
            hazardProperties ??= new MaterialPropertyBlock();
            float flicker = 0.55f + 0.45f * Mathf.Sin(Time.time * lootFlickerFrequency * 2f * Mathf.PI);
            lootProperties.SetColor(BaseColorId, lootColor * (lootIntensity * flicker));
            hazardProperties.SetColor(BaseColorId, hazardColor * hazardIntensity);

            Draw(lootTargets, lootProperties);
            Draw(hazardTargets, hazardProperties);
        }

        private void Collect(LayerMask mask, HashSet<Collider> targets)
        {
            Vector3 start = preview.VisibleStart;
            Vector3 path = preview.VisibleEnd - start;
            float length = path.magnitude;

            // Каст не видит то, что уже пересекает сферу в начале, поэтому отдельно проверяем точку старта.
            int overlapCount = Physics.OverlapSphereNonAlloc(start, corridorRadius, overlaps, mask,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < overlapCount; i++) targets.Add(overlaps[i]);

            if (length < 0.001f) return;
            int hitCount = Physics.SphereCastNonAlloc(start, corridorRadius, path / length, hits, length, mask,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < hitCount; i++) targets.Add(hits[i].collider);
        }

        private void Draw(HashSet<Collider> targets, MaterialPropertyBlock properties)
        {
            var renderParams = new RenderParams(glowMaterial)
            {
                matProps = properties,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer
            };

            foreach (Collider target in targets)
            {
                foreach (MeshFilter filter in MeshesOf(target))
                {
                    if (filter == null || filter.sharedMesh == null) continue;
                    var meshRenderer = filter.GetComponent<MeshRenderer>();
                    if (meshRenderer == null || !meshRenderer.enabled || !filter.gameObject.activeInHierarchy) continue;

                    // Масштаб вокруг центра объекта, а не вокруг пивота меша — оболочка ложится ровно поверх.
                    Vector3 center = meshRenderer.bounds.center;
                    Matrix4x4 shell = Matrix4x4.Translate(center) * Matrix4x4.Scale(Vector3.one * shellScale)
                        * Matrix4x4.Translate(-center) * filter.transform.localToWorldMatrix;
                    Mesh mesh = filter.sharedMesh;
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                        Graphics.RenderMesh(renderParams, mesh, sub, shell);
                }
            }
        }

        private MeshFilter[] MeshesOf(Collider target)
        {
            if (!meshesByCollider.TryGetValue(target, out MeshFilter[] filters))
            {
                filters = target.GetComponentsInChildren<MeshFilter>(true);
                meshesByCollider[target] = filters;
            }
            return filters;
        }
    }
}
