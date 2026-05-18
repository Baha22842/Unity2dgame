#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class LevelBootstrapper : EditorWindow
{
    [MenuItem("Tools/Diplom/Generate Basic Level")]
    public static void ShowWindow()
    {
        GetWindow<LevelBootstrapper>("Level Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Создание базового шаблона уровня", EditorStyles.boldLabel);

        if (GUILayout.Button("Сгенерировать Уровень"))
        {
            GenerateLevel();
        }
    }

    private void GenerateLevel()
    {
        GameObject levelRoot = new GameObject("GeneratedLevel");

        // Ground Platform
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.SetParent(levelRoot.transform);
        ground.transform.position = new Vector3(0, -2, 0);
        ground.transform.localScale = new Vector3(30, 1, 1);

        BoxCollider2D bc2d = ground.AddComponent<BoxCollider2D>();
        DestroyImmediate(ground.GetComponent<BoxCollider>()); // Remove 3D collider
        ground.layer = LayerMask.NameToLayer("Ground"); // Set to Ground layer if exists

        // Player Spawn Point
        GameObject spawn = new GameObject("PlayerSpawn");
        spawn.transform.SetParent(levelRoot.transform);
        spawn.transform.position = new Vector3(-10, -1, 0);

        // Simple Platform
        GameObject plat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plat.name = "Platform";
        plat.transform.SetParent(levelRoot.transform);
        plat.transform.position = new Vector3(-2, 1, 0);
        plat.transform.localScale = new Vector3(4, 0.5f, 1);
        plat.AddComponent<BoxCollider2D>();
        DestroyImmediate(plat.GetComponent<BoxCollider>());

        // Try load some existing prefabs if they exist in a standard folder path
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });

        GameObject bossPrefab = null;
        GameObject enemyPrefab = null;
        GameObject artifactPrefab = null;
        GameObject portalPrefab = null;

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("Boss")) bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            else if (path.Contains("Enemy")) enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            else if (path.Contains("Artifact")) artifactPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            else if (path.Contains("Portal")) portalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        // Boss
        if (bossPrefab != null)
        {
            GameObject b = (GameObject)PrefabUtility.InstantiatePrefab(bossPrefab);
            b.transform.SetParent(levelRoot.transform);
            b.transform.position = new Vector3(10, -1, 0);
        }

        // Enemy
        if (enemyPrefab != null)
        {
            GameObject e = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
            e.transform.SetParent(levelRoot.transform);
            e.transform.position = new Vector3(2, -1, 0);
        }

        // Artifact
        if (artifactPrefab != null)
        {
            GameObject a = (GameObject)PrefabUtility.InstantiatePrefab(artifactPrefab);
            a.transform.SetParent(levelRoot.transform);
            a.transform.position = new Vector3(-2, 2.5f, 0);
        }

        // Portal
        if (portalPrefab != null)
        {
            GameObject p = (GameObject)PrefabUtility.InstantiatePrefab(portalPrefab);
            p.transform.SetParent(levelRoot.transform);
            p.transform.position = new Vector3(14, -1, 0);
        }

        Debug.Log("Базовый уровень успешно сгенерирован!");
    }
}
#endif
