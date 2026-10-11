using UnityEngine;

namespace FunFlow.Presentation
{
    /// <summary>
    /// Разлёт частиц в точке мира: подбор, разрез мешка, разбитая витрина.
    /// Если своя система частиц не назначена, создаётся простая (квадратики, без текстур).
    /// </summary>
    public sealed class WorldVfx : MonoBehaviour
    {
        [Tooltip("Необязательно. Своя система частиц: Emission выключен, Simulation Space = World.")]
        [SerializeField] private ParticleSystem customSystem;
        [SerializeField, Min(0.01f)] private float particleSize = 0.16f;

        private ParticleSystem runtimeSystem;
        private Material runtimeMaterial;

        public void Burst(Vector3 position, Color color, int count)
        {
            ParticleSystem ps = customSystem != null ? customSystem : EnsureRuntimeSystem();
            if (ps == null || count <= 0) return;

            var emit = new ParticleSystem.EmitParams
            {
                position = position,
                applyShapeToPosition = true,
                startColor = color
            };
            ps.Emit(emit, count);
        }

        private ParticleSystem EnsureRuntimeSystem()
        {
            if (runtimeSystem != null) return runtimeSystem;

            // Не дочерний объект Canvas: частицы живут в мире, а не в UI.
            var go = new GameObject("PresentationParticles");
            runtimeSystem = go.AddComponent<ParticleSystem>();
            runtimeSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = runtimeSystem.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.6f, particleSize * 1.3f);
            main.gravityModifier = 1.2f;
            main.maxParticles = 600;

            var emission = runtimeSystem.emission;
            emission.enabled = false;

            var shape = runtimeSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var size = runtimeSystem.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                runtimeMaterial = new Material(shader) { name = "PresentationParticles (runtime)" };
                renderer.sharedMaterial = runtimeMaterial;
            }
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            runtimeSystem.Play();
            return runtimeSystem;
        }

        private void OnDestroy()
        {
            if (runtimeSystem != null) Destroy(runtimeSystem.gameObject);
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }
    }
}
