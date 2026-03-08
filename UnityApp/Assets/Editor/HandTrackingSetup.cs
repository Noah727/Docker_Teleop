using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class HandTrackingSetup : EditorWindow
{
    [MenuItem("Tools/Setup Hand Tracking Scene")]
    public static void SetupScene()
    {
        // Simple safe setup
        string scenePath = "Assets/Scenes/HandTrackingTeleop.unity";
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, scenePath);

        // Try to find OVRCameraRig
        string[] guids = AssetDatabase.FindAssets("OVRCameraRig t:Prefab");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab)
            {
                PrefabUtility.InstantiatePrefab(prefab);
            }
        }
        else
        {
            EditorUtility.DisplayDialog("Info", "OVRCameraRig not found in search, but scene created.", "OK");
        }
        
        // Add Teleop Manager
        GameObject teleop = new GameObject("TeleopManager");
        teleop.AddComponent(System.Type.GetType("Teleoperation.HandPosePublisher, Assembly-CSharp"));
        teleop.AddComponent(System.Type.GetType("Teleoperation.GripperController, Assembly-CSharp"));

        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Success", "Scene created! Check 'HandTrackingTeleop' in Scenes.", "OK");
    }
}
