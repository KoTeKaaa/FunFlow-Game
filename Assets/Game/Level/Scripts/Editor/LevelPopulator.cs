using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement; // Требуется для работы с режимом префабов

public class LevelPopulator : EditorWindow
{
    private GameObject itemPrefab;
    private int count = 10;
    private float startX = 20f;
    private float endX = 80f;
    private float corridorWidth = 8f;
    
    // Новое поле для имени папки
    private string containerName = "Generated_Items"; 

    [MenuItem("Tools/FunFlow/Populate Level")]
    public static void ShowWindow()
    {
        GetWindow<LevelPopulator>("Генератор препятствий");
    }

    private void OnGUI()
    {
        GUILayout.Label("Массовая расстановка префабов", EditorStyles.boldLabel);
        
        itemPrefab = (GameObject)EditorGUILayout.ObjectField("Префаб объекта", itemPrefab, typeof(GameObject), false);
        count = EditorGUILayout.IntField("Количество", count);
        containerName = EditorGUILayout.TextField("Имя папки-контейнера", containerName);
        
        GUILayout.Space(10);
        startX = EditorGUILayout.FloatField("Начало зоны (X)", startX);
        endX = EditorGUILayout.FloatField("Конец зоны (X)", endX);
        corridorWidth = EditorGUILayout.FloatField("Ширина раскидывания (Z)", corridorWidth);

        GUILayout.Space(15);
        if (GUILayout.Button("Сгенерировать случайным образом"))
        {
            Generate();
        }
    }

    private void Generate()
    {
        if (itemPrefab == null)
        {
            Debug.LogWarning("Назначьте префаб для генерации!");
            return;
        }

        // 1. Ищем уже существующую папку с таким именем
        GameObject container = GameObject.Find(containerName);
        
        // 2. Если папки нет, создаем её
        if (container == null)
        {
            container = new GameObject(containerName);
            
            // Проверяем, находимся ли мы сейчас в режиме редактирования префаба
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                // Помещаем созданную папку внутрь корня префаба
                container.transform.SetParent(prefabStage.prefabContentsRoot.transform, false);
            }
        }

        // 3. Генерируем объекты внутрь этой папки
        for (int i = 0; i < count; i++)
        {
            float randomX = Random.Range(startX, endX);
            float randomZ = Random.Range(-corridorWidth / 2f, corridorWidth / 2f);
            
            GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(itemPrefab, container.transform);
            newObj.transform.localPosition = new Vector3(randomX, itemPrefab.transform.localPosition.y, randomZ);
        }
        
        Debug.Log($"Сгенерировано {count} объектов внутри '{container.name}'");
    }
}
