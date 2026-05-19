using UnityEngine;
using UnityEditor;

public class FixPluginImportSettings : MonoBehaviour
{
    [MenuItem("Tools/Fix Robotics Plugin Settings")]
    public static void Fix()
    {
        // Paths from the error log
        string[] pathsToExludeFromAndroid = new string[]
        {
            "Packages/com.unity.robotics.urdf-importer/Runtime/UnityMeshImporter/Plugins/AssimpNet/Native/win/x86/assimp.dll",
            "Packages/com.unity.robotics.urdf-importer/Runtime/UnityMeshImporter/Plugins/AssimpNet/Native/win/x86_64/assimp.dll",
            "Packages/com.unity.robotics.urdf-importer/Runtime/UnityMeshImporter/Plugins/AssimpNet/Native/mac/assimp.bundle", // Good measure
            "Packages/com.unity.robotics.urdf-importer/Runtime/UnityMeshImporter/Plugins/AssimpNet/Native/linux/libassimp.so" // Good measure
        };

        foreach (var path in pathsToExludeFromAndroid)
        {
            PluginImporter importer = AssetImporter.GetAtPath(path) as PluginImporter;
            if (importer != null)
            {
                // Disable for Android
                importer.SetCompatibleWithPlatform(BuildTarget.Android, false);
                
                // Also ensure it doesn't fallback to "Any Platform" if we can help it
                // Usually these are Any Platform = True by default.
                // We just strictly check Android = False.
                
                importer.SaveAndReimport();
                Debug.Log($"Fixed settings for: {path}");
            }
            else
            {
                // This might happen if the package is immutable or path is slightly different.
                // Try searching if strict path fails.
                Debug.LogWarning($"Could not find plugin at: {path}");
            }
        }
        
        Debug.Log("Finished checking plugins.");
    }
}
