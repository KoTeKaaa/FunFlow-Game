using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

public class DifficultyLevelBuilder : EditorWindow
{
    private GameObject obstaclePrefab;
    private GameObject lootPrefab;
    private GameObject cutterPrefab;
    private GameObject alarmPrefab;
    private GameObject showcasePrefab;

    [MenuItem("Tools/FunFlow/Build Level by Difficulty")]
    public static void ShowWindow()
    {
        GetWindow<DifficultyLevelBuilder>("Генератор уровней");
    }

    private void OnGUI()
    {
        GUILayout.Label("Назначьте префабы из папки Elements:", EditorStyles.boldLabel);
        
        obstaclePrefab = (GameObject)EditorGUILayout.ObjectField("Колонна (Obstacle)", obstaclePrefab, typeof(GameObject), false);
        lootPrefab = (GameObject)EditorGUILayout.ObjectField("Монетка (Loot)", lootPrefab, typeof(GameObject), false);
        cutterPrefab = (GameObject)EditorGUILayout.ObjectField("Резак (CutterTrap)", cutterPrefab, typeof(GameObject), false);
        alarmPrefab = (GameObject)EditorGUILayout.ObjectField("Сигнализация (AlarmZone)", alarmPrefab, typeof(GameObject), false);
        showcasePrefab = (GameObject)EditorGUILayout.ObjectField("Витрина (Showcase)", showcasePrefab, typeof(GameObject), false);

        GUILayout.Space(20);
        GUILayout.Label("Выберите сложность трассы:", EditorStyles.boldLabel);

        if (GUILayout.Button("🟩 ЛЕГКАЯ (Обучающая)", GUILayout.Height(35))) BuildLevel(1);
        if (GUILayout.Button("🟨 СРЕДНЯЯ (Стандартная)", GUILayout.Height(35))) BuildLevel(2);
        if (GUILayout.Button("🟥 СЛОЖНАЯ (Хардкор)", GUILayout.Height(35))) BuildLevel(3);
    }

    private void BuildLevel(int difficulty)
    {
        if (obstaclePrefab == null || lootPrefab == null || cutterPrefab == null || alarmPrefab == null || showcasePrefab == null)
        {
            Debug.LogError("Ошибка: Сначала назначьте все 5 префабов в окне генератора!");
            return;
        }

        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        Transform parentTransform = prefabStage != null ? prefabStage.prefabContentsRoot.transform : null;

        string containerName = "Generated_Track";
        Transform existingContainer = parentTransform != null ? parentTransform.Find(containerName) : GameObject.Find(containerName)?.transform;
        
        if (existingContainer != null) DestroyImmediate(existingContainer.gameObject);
        
        GameObject container = new GameObject(containerName);
        if (parentTransform != null) container.transform.SetParent(parentTransform, false);

        switch (difficulty)
        {
            case 1: BuildEasyTrack(container.transform); break;
            case 2: BuildMediumTrack(container.transform); break;
            case 3: BuildHardTrack(container.transform); break;
        }

        if (prefabStage != null) EditorSceneManager.MarkSceneDirty(prefabStage.scene);
    }

    // ==========================================
    // ЛЕГКАЯ: Плавные дуги и обучение механикам
    // ==========================================
    private void BuildEasyTrack(Transform parent)
    {
        Spawn(lootPrefab, parent, 10f, -2f);
        Spawn(lootPrefab, parent, 12f, -1f);
        Spawn(lootPrefab, parent, 14f, 0f);
        Spawn(lootPrefab, parent, 16f, 1f);
        Spawn(lootPrefab, parent, 18f, 2f);

        Spawn(obstaclePrefab, parent, 28f, 0f);
        Spawn(lootPrefab, parent, 28f, 3f);
        Spawn(lootPrefab, parent, 28f, -3f);

        Spawn(cutterPrefab, parent, 45f, 0f);
        Spawn(lootPrefab, parent, 48f, 0f);

        Spawn(alarmPrefab, parent, 60f, 0f);
        Spawn(showcasePrefab, parent, 75f, 0f);
    }

    // ==========================================
    // СРЕДНЯЯ: Выбор, асимметрия и приманки
    // ==========================================
    private void BuildMediumTrack(Transform parent)
    {
        Spawn(lootPrefab, parent, 8f, 0f);
        Spawn(lootPrefab, parent, 12f, 0f);

        Spawn(obstaclePrefab, parent, 22f, 2f);
        Spawn(lootPrefab, parent, 22f, -2f); 
        Spawn(obstaclePrefab, parent, 28f, -1.5f);
        Spawn(lootPrefab, parent, 28f, 2.5f); 

        Spawn(cutterPrefab, parent, 40f, 0f);
        Spawn(lootPrefab, parent, 40f, 0f); 
        Spawn(lootPrefab, parent, 40f, -3f); 

        Spawn(alarmPrefab, parent, 55f, 2f); 
        Spawn(obstaclePrefab, parent, 55f, -2f);
        Spawn(lootPrefab, parent, 55f, 0f); 

        Spawn(showcasePrefab, parent, 75f, 0f);
    }

    // ==========================================
    // СЛОЖНАЯ (ОБНОВЛЕННАЯ): Высокая плотность элементов без узких горлышек
    // ==========================================
    private void BuildHardTrack(Transform parent)
    {
        // Старт: Сразу заставляем двигаться широкими зигзагами
        Spawn(lootPrefab, parent, 8f, -3f);
        Spawn(obstaclePrefab, parent, 12f, 0f);
        Spawn(lootPrefab, parent, 16f, 3f);
        Spawn(obstaclePrefab, parent, 20f, -1.5f);
        Spawn(lootPrefab, parent, 22f, -3f);
        
        // Массовое скопление препятствий (проходы широкие, но нужно постоянно рулить)
        Spawn(obstaclePrefab, parent, 30f, 2.5f);
        Spawn(obstaclePrefab, parent, 33f, -2.5f);
        Spawn(obstaclePrefab, parent, 36f, 1.5f);
        Spawn(obstaclePrefab, parent, 39f, -3f);
        Spawn(obstaclePrefab, parent, 42f, 2f);
        Spawn(lootPrefab, parent, 31f, -1f);
        Spawn(lootPrefab, parent, 37f, -1f);

        // Зона повышенной опасности: Сигналка + Два резака по краям
        Spawn(alarmPrefab, parent, 52f, 0f);
        Spawn(cutterPrefab, parent, 52f, -2.5f);
        Spawn(cutterPrefab, parent, 52f, 2.5f);
        Spawn(lootPrefab, parent, 52f, 0f); // Безопасный, но тревожный центр

        // Смена паттерна: Резак по центру + препятствия по краям
        Spawn(cutterPrefab, parent, 62f, 0f);
        Spawn(obstaclePrefab, parent, 62f, -3f);
        Spawn(obstaclePrefab, parent, 62f, 3f);
        Spawn(lootPrefab, parent, 64f, -1.5f);
        Spawn(lootPrefab, parent, 64f, 1.5f);

        // Финальный рывок: еще одна зона тревоги с раскиданным лутом перед финишем
        Spawn(alarmPrefab, parent, 72f, 0f);
        Spawn(lootPrefab, parent, 74f, 2f);
        Spawn(lootPrefab, parent, 76f, -2f);
        Spawn(cutterPrefab, parent, 78f, -1.5f);
        Spawn(cutterPrefab, parent, 81f, 1.5f);

        // Главный приз
        Spawn(showcasePrefab, parent, 88f, 0f);
    }

    private void Spawn(GameObject prefab, Transform parent, float x, float z)
    {
        GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        newObj.transform.localPosition = new Vector3(x, 1f, z); 
    }
}