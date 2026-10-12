using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FunFlow.Player
{
    /// <summary>
    /// Только визуал: подсветка объектов на ВИДИМОЙ части предпросмотра пути в коридоре шириной с шарик.
    /// Добыча слегка мерцает, ловушки и сигнальные плиты светятся красным. Объекты определяются по контракту, их материалы,
    /// рендереры и логика не меняются: поверх рисуется светящаяся оболочка из их же мешей.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class PlayerPathHighlighter : MonoBehaviour
    {
        [SerializeField] private PlayerPathPreview preview;
        [Tooltip("Аддитивный прозрачный материал оболочки (цвет задаётся через _BaseColor).")]
        [SerializeField] private Material glowMaterial;
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

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Collider[] overlaps = new Collider[32];
        private readonly HashSet<MonoBehaviour> lootTargets = new HashSet<MonoBehaviour>();
        private readonly HashSet<MonoBehaviour> hazardTargets = new HashSet<MonoBehaviour>();
        private readonly List<MonoBehaviour> components = new List<MonoBehaviour>();
        private readonly List<MeshFilter> meshes = new List<MeshFilter>();
        private readonly HashSet<MeshFilter> drawnMeshes = new HashSet<MeshFilter>();
        private MaterialPropertyBlock lootProperties;
        private MaterialPropertyBlock hazardProperties;

        private void OnDisable()
        {
            lootTargets.Clear();
            hazardTargets.Clear();
            drawnMeshes.Clear();
        }

        private void LateUpdate()
        {
            lootTargets.Clear();
            hazardTargets.Clear();
            if (preview == null || !preview.IsVisible || glowMaterial == null) return;

            Collect(preview.VisibleStart, preview.VisibleEnd);
            // Опасность важнее при нескольких игровых компонентах на одном объекте.
            lootTargets.ExceptWith(hazardTargets);

            lootProperties ??= new MaterialPropertyBlock();
            hazardProperties ??= new MaterialPropertyBlock();
            float flicker = 0.55f + 0.45f * Mathf.Sin(Time.time * lootFlickerFrequency * 2f * Mathf.PI);
            lootProperties.SetColor(BaseColorId, lootColor * (lootIntensity * flicker));
            hazardProperties.SetColor(BaseColorId, hazardColor * hazardIntensity);

            Draw(hazardTargets, hazardProperties);
            Draw(lootTargets, lootProperties);
        }

        private void Collect(Vector3 start, Vector3 end)
        {
            lootTargets.Clear();
            hazardTargets.Clear();
            drawnMeshes.Clear();
            int count;
            while (true)
            {
                count = Physics.OverlapCapsuleNonAlloc(start, end, corridorRadius, overlaps,
                    Physics.AllLayers, QueryTriggerInteraction.Collide);
                if (count < overlaps.Length) break;
                System.Array.Resize(ref overlaps, overlaps.Length * 2);
            }
            for (int i = 0; i < count; i++)
            {
                if (overlaps[i].gameObject.scene != gameObject.scene) continue;
                components.Clear();
                overlaps[i].GetComponentsInParent(false, components);
                foreach (var component in components)
                {
                    if (component is not IPathHighlightTarget target || !component.isActiveAndEnabled || !target.CanHighlight) continue;
                    // A guard's vision sensor is not its body; never tint the whole guard from a cone trigger.
                    if (target.HighlightKind == PathHighlightKind.Guard && overlaps[i].isTrigger) continue;
                    (target.HighlightKind == PathHighlightKind.Loot ? lootTargets : hazardTargets).Add(component);
                    break;
                }
            }
        }

        private void Draw(HashSet<MonoBehaviour> targets, MaterialPropertyBlock properties)
        {
            var renderParams = new RenderParams(glowMaterial)
            {
                matProps = properties,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer
            };

            foreach (MonoBehaviour target in targets)
            {
                meshes.Clear();
                target.GetComponentsInChildren(false, meshes);
                foreach (MeshFilter filter in meshes)
                {
                    if (filter == null || filter.sharedMesh == null) continue;
                    if (!drawnMeshes.Add(filter)) continue;
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

    }
}
