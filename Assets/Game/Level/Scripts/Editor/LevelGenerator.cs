using UnityEngine;
using UnityEditor;

public class LevelGenerator : EditorWindow
{
    // Настройки уровня по умолчанию
    private float levelLength = 100f;
    private float corridorWidth = 10f;
    private float wallHeight = 2f;
    private float finishZoneDepth = 5f;

    [MenuItem("Tools/FunFlow/Generate Basic Level")]
    public static void ShowWindow()
    {
        GetWindow<LevelGenerator>("Level Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Level Settings", EditorStyles.boldLabel);
        levelLength = EditorGUILayout.FloatField("Level Length (+X)", levelLength);
        corridorWidth = EditorGUILayout.FloatField("Corridor Width (Z)", corridorWidth);
        wallHeight = EditorGUILayout.FloatField("Wall Height (Y)", wallHeight);

        if (GUILayout.Button("Generate Museum Corridor"))
        {
            GenerateLevel();
        }
    }

    private void GenerateLevel()
    {
        // 1. Создаем корневой объект для уровня
        GameObject levelRoot = new GameObject("MuseumLevel");
        
        // 2. Создаем пол (Плоскость XZ, слой Ground - 9)
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.parent = levelRoot.transform;
        // 1 юнит = 1 метр. Центр пола смещен по X, чтобы начинаться от нуля.
        floor.transform.localScale = new Vector3(levelLength, 0.5f, corridorWidth);
        floor.transform.position = new Vector3(levelLength / 2f, -0.25f, 0f);
        
        // Назначаем слой. В коде слои представлены целыми числами (согласно team-guide.md).
        floor.layer = 9; 

        // 3. Создаем стены (Слой Obstacle - 10)
        GameObject wallLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallLeft.name = "WallLeft";
        wallLeft.transform.parent = levelRoot.transform;
        wallLeft.transform.localScale = new Vector3(levelLength, wallHeight, 1f);
        wallLeft.transform.position = new Vector3(levelLength / 2f, wallHeight / 2f, corridorWidth / 2f + 0.5f);
        wallLeft.layer = 10;

        GameObject wallRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallRight.name = "WallRight";
        wallRight.transform.parent = levelRoot.transform;
        wallRight.transform.localScale = new Vector3(levelLength, wallHeight, 1f);
        wallRight.transform.position = new Vector3(levelLength / 2f, wallHeight / 2f, -(corridorWidth / 2f + 0.5f));
        wallRight.layer = 10;

        GameObject startWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        startWall.name = "StartWall_Back";
        startWall.transform.parent = levelRoot.transform;
        startWall.transform.localScale = new Vector3(1f, wallHeight, corridorWidth + 2f);
        startWall.transform.position = new Vector3(-0.5f, wallHeight / 2f, 0f);
        startWall.layer = 10; // Не дает игроку вылететь назад

        // 4. Создаем триггер выхода (Слой Exit - 15) в самом конце коридора
        GameObject exitTrigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
        exitTrigger.name = "ExitTrigger";
        exitTrigger.transform.parent = levelRoot.transform;
        exitTrigger.transform.localScale = new Vector3(finishZoneDepth, wallHeight, corridorWidth);
        exitTrigger.transform.position = new Vector3(levelLength + (finishZoneDepth / 2f), wallHeight / 2f, 0f);
        exitTrigger.layer = 15;

        // Делаем выход триггером и отключаем MeshRenderer для прозрачности
        Collider exitCollider = exitTrigger.GetComponent<Collider>();
        exitCollider.isTrigger = true;
        
        MeshRenderer exitRenderer = exitTrigger.GetComponent<MeshRenderer>();
        if (exitRenderer != null)
        {
            exitRenderer.enabled = false;
        }

        Debug.Log("Museum Corridor successfully generated!");
        Selection.activeGameObject = levelRoot;
    }
}