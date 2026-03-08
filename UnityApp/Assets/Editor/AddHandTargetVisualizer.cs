#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AddHandTargetVisualizer
{
    [MenuItem("Tools/Hand Teleop/Add Hand Target Visualizer")]
    public static void AddToScene()
    {
        var existing = Object.FindObjectOfType<HandTargetPoseVisualizer>();
        if (existing != null)
        {
            Debug.Log("[AddHandTargetVisualizer] HandTargetPoseVisualizer already exists in scene.");
            Selection.activeObject = existing.gameObject;
            return;
        }

        var host = GameObject.Find("NetworkSender");
        if (host == null)
        {
            host = new GameObject("HandTargetVisualizer");
        }

        var component = host.GetComponent<HandTargetPoseVisualizer>();
        if (component == null)
        {
            component = host.AddComponent<HandTargetPoseVisualizer>();
        }

        EditorSceneManager.MarkSceneDirty(host.scene);
        EditorSceneManager.SaveScene(host.scene);

        Selection.activeObject = host;
        Debug.Log("[AddHandTargetVisualizer] Added HandTargetPoseVisualizer and saved scene.");
    }
}
#endif
