#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LevelValidator
{
    public sealed class Issue
    {
        public string Message;
        public Object Context;
    }

    // Prefabs validate their own structure; scene services belong to the Game template.
    public static List<Issue> Validate(GameObject[] roots, bool gameplayScene)
    {
        var issues = new List<Issue>();
        void Error(Object context, string message) => issues.Add(new Issue { Context = context, Message = message });
        var objects = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject).ToArray();
        var scripts = objects.SelectMany(obj => obj.GetComponents<MonoBehaviour>()).Where(c => c != null).ToArray();
        var active = scripts.Where(c => c.isActiveAndEnabled).ToArray();
        foreach (var obj in objects)
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(obj) > 0)
                Error(obj, "Missing script.");
            if (obj.GetComponents<MonoBehaviour>().Count(c => c != null && c.GetType().Name == "LootItem") > 1)
                Error(obj, "Duplicate LootItem: pickup would award money twice. Remove the old scene component.");
        }

        foreach (string layer in new[] { "Player", "Bag", "Loot", "Alarm", "Cutter", "Exit", "Obstacle" })
            if (LayerMask.NameToLayer(layer) < 0) Error(roots.FirstOrDefault(), $"Missing layer {layer}.");

        foreach (var script in scripts)
        {
            string name = script.GetType().Name;
            string layer = name switch
            {
                "LootItem" => "Loot", "AlarmTrap" => "Alarm", "CutterTrap" => "Cutter",
                "ExitTrigger" or "RunExitTrigger" => "Exit", "Guard" => "Obstacle", _ => null
            };
            if (layer == null && name != "ValuableShowcase") continue;
            if (name is "LootItem" or "AlarmTrap" or "CutterTrap" or "ValuableShowcase" or "Guard")
            {
                if (script is not IPathHighlightTarget)
                    Error(script, "Gameplay element must implement IPathHighlightTarget for automatic path highlighting.");
                if (!script.GetComponentsInChildren<MeshFilter>(true).Any(mesh => mesh.sharedMesh != null
                    && mesh.GetComponent<MeshRenderer>() != null && mesh.GetComponent<MeshRenderer>().enabled))
                    Error(script, "Highlight target requires a mesh and an enabled MeshRenderer in its own hierarchy.");
            }
            var colliders = script.GetComponents<Collider>();
            if (!colliders.Any(c => c.enabled && c.isTrigger == (name != "Guard")))
                Error(script, name == "Guard" ? "Guard requires an enabled solid collider for movement and aim blocking."
                    : "Requires an enabled trigger collider on the same object.");
            if (layer != null && script.gameObject.layer != LayerMask.NameToLayer(layer))
                Error(script, $"Expected layer {layer}.");
            var serialized = new SerializedObject(script);
            if (name == "Guard")
            {
                var head = serialized.FindProperty("head").objectReferenceValue as Transform;
                if (head == null || !head.IsChildOf(script.transform)) Error(script, "Guard head must belong to its prefab hierarchy.");
                if (serialized.FindProperty("coneMaterial").objectReferenceValue == null) Error(script, "Guard requires a vision-cone material.");
                foreach (string field in new[] { "viewRange", "viewAngle", "eyeHeight", "turnSpeed" })
                {
                    float value = serialized.FindProperty(field).floatValue;
                    if (!float.IsFinite(value) || value <= 0) Error(script, $"Guard {field} must be positive and finite.");
                }
                if (serialized.FindProperty("viewAngle").floatValue > 180) Error(script, "Guard viewAngle must not exceed 180 degrees.");
                foreach (string field in new[] { "startDelay", "sweepPause", "rotatePause", "coneHeight" })
                {
                    float value = serialized.FindProperty(field).floatValue;
                    if (!float.IsFinite(value) || value < 0) Error(script, $"Guard {field} must be non-negative and finite.");
                }
                int playerLayer = LayerMask.NameToLayer("Player"), obstacleLayer = LayerMask.NameToLayer("Obstacle");
                if (playerLayer >= 0 && (serialized.FindProperty("thiefMask").intValue & (1 << playerLayer)) == 0)
                    Error(script, "Guard thiefMask must include Player.");
                int mask = serialized.FindProperty("obstacleMask").intValue;
                if (obstacleLayer >= 0 && (mask & (1 << obstacleLayer)) == 0) Error(script, "Guard obstacleMask must include Obstacle.");
                if (playerLayer >= 0 && (mask & (1 << playerLayer)) != 0) Error(script, "Guard obstacleMask must exclude Player to avoid self-occluding detection.");
                var source = serialized.FindProperty("coreSessionSource").objectReferenceValue as MonoBehaviour;
                if (source != null && (source is not IRunSession || source.gameObject.scene != script.gameObject.scene))
                    Error(script, "Guard Core source must implement IRunSession in this scene, or be empty for automatic binding.");
            }
            string amount = name == "LootItem" ? "value" : name == "ValuableShowcase" ? "rewardValue" : null;
            if (amount != null && serialized.FindProperty(amount).intValue <= 0)
                Error(script, "Loot reward must be positive.");
            if (name == "CutterTrap")
            {
                float fraction = serialized.FindProperty("lossFraction").floatValue;
                if (!float.IsFinite(fraction) || fraction < 0 || fraction > 1)
                    Error(script, "Loss fraction must be finite and between 0 and 1.");
            }
            if (name == "LootItem" && script.GetComponentsInChildren<Collider>(true)
                .Any(c => c.enabled && !c.isTrigger))
                Error(script, "Coin has a solid collider: move physics to the pickup root and keep the visual child collider-free.");
        }

        if (!gameplayScene) return issues;
        foreach (var service in new[] { typeof(IRunSession), typeof(ILootInventory), typeof(IPlayerControl) })
        {
            int count = active.Count(service.IsInstanceOfType);
            if (count != 1) Error(roots.FirstOrDefault(), $"Expected one active {service.Name}; found {count}.");
        }
        var exits = active.Where(c => c is ILevelExit).Select(c => c.transform).Distinct().ToArray();
        if (exits.Length != 1) Error(roots.FirstOrDefault(), $"Expected one active exit; found {exits.Length}.");
        foreach (var session in active.OfType<RunSession>())
        {
            var data = new SerializedObject(session);
            foreach (string field in new[] { "_playerSource", "_lootSource", "_config" })
                if (data.FindProperty(field).objectReferenceValue == null) Error(session, $"Game template is missing {field}.");
            if (data.FindProperty("_playerSource").objectReferenceValue is not IPlayerControl)
                Error(session, "Player source must implement IPlayerControl.");
            if (data.FindProperty("_lootSource").objectReferenceValue is not ILootInventory)
                Error(session, "Loot source must implement ILootInventory.");
        }
        foreach (var player in active.Where(c => c is IPlayerControl))
            if (player.GetComponent<Rigidbody>() == null)
                Error(player, "Player requires a Rigidbody for trigger contacts.");
        foreach (var inventory in active.Where(c => c.GetType().Name == "LootInventory"))
            if (new SerializedObject(inventory).FindProperty("config").objectReferenceValue == null)
                Error(inventory, "Inventory requires GameConfig for bag weight.");
        if (active.Any(c => c.GetType().Name == "CutterTrap"))
        {
            var bags = objects.Where(obj => obj.activeInHierarchy && obj.layer == LayerMask.NameToLayer("Bag"))
                .SelectMany(obj => obj.GetComponents<Collider>()).Where(c => c.enabled).ToArray();
            if (bags.Length == 0 || bags.Any(c => c.attachedRigidbody == null))
                Error(roots.FirstOrDefault(), "Cutter requires a Bag collider attached to a Rigidbody.");
        }
        foreach (var pair in new[] { ("Player", "Loot"), ("Player", "Alarm"), ("Player", "Exit"), ("Bag", "Cutter"), ("Player", "Obstacle") })
        {
            int a = LayerMask.NameToLayer(pair.Item1), b = LayerMask.NameToLayer(pair.Item2);
            if (a >= 0 && b >= 0 && Physics.GetIgnoreLayerCollision(a, b))
                Error(roots.FirstOrDefault(), $"Physics disables {pair.Item1}/{pair.Item2} contacts.");
        }
        return issues;
    }

    [MenuItem("Tools/FunFlow/Validate Current Level")]
    public static void ValidateCurrent()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        var roots = stage != null ? new[] { stage.prefabContentsRoot } : UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        Report(Validate(roots, stage == null), stage != null ? stage.assetPath : roots.FirstOrDefault()?.scene.path);
    }

    [MenuItem("Tools/FunFlow/Validate All Game Levels")]
    public static void ValidateAll()
    {
        foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            var scene = EditorSceneManager.OpenPreviewScene(entry.path);
            try
            {
                var roots = scene.GetRootGameObjects();
                if (roots.Any(root => root.GetComponentInChildren<RunSession>(true) != null))
                    Report(Validate(roots, true), entry.path);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }

    private static void Report(List<Issue> issues, string path)
    {
        foreach (var issue in issues) Debug.LogError($"Level validation [{path}]: {issue.Message}", issue.Context);
        if (issues.Count == 0) Debug.Log($"Level validation [{path}]: passed.");
    }
}
#endif
