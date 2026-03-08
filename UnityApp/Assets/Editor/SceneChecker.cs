using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

public class SceneChecker : EditorWindow
{
    [MenuItem("Tools/Check Scene Contents")]
    public static void CheckScene()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        Debug.Log($"Active Scene: {currentScene.name} (Path: {currentScene.path})");
        
        GameObject[] roots = currentScene.GetRootGameObjects();
        Debug.Log($"Root Objects Count: {roots.Length}");
        
        foreach (var root in roots)
        {
            Debug.Log($"- {root.name}");
            foreach(Transform child in root.transform)
            {
                Debug.Log($"  -- {child.name}");
            }
        }
        
        EditorUtility.DisplayDialog("Scene Check", $"Logged {roots.Length} root objects to Console.", "OK");
    }
}
