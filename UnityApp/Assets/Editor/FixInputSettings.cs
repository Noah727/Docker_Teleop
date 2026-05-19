using UnityEngine;
using UnityEditor;

public class FixInputSettings : MonoBehaviour
{
    [MenuItem("Tools/Fix Input Settings")]
    public static void Fix()
    {
        // Set to "Input Manager (Old)" which is value 0.
        // Value 1 is "Input System Package (New)"
        // Value 2 is "Both" (which causes the error)
        
        // We use SerializedObject because the API might require a restart and prompt.
        // But setting it via API usually just marks it for restart.
        
        var projectSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];
        SerializedObject manager = new SerializedObject(projectSettings);
        SerializedProperty prop = manager.FindProperty("activeInputHandler");
        
        if (prop != null)
        {
            if (prop.intValue != 0)
            {
                prop.intValue = 0; // Set to Old Input Manager
                manager.ApplyModifiedProperties();
                Debug.Log("Switched Active Input Handling to 'Input Manager (Old)'. Editor might restart.");
            }
            else
            {
                Debug.Log("Active Input Handling is already 'Input Manager (Old)'.");
            }
        }
        else
        {
            Debug.LogError("Could not find activeInputHandler property.");
        }
    }
}
