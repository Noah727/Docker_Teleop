#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RecreateOVRHands
{
    [MenuItem("Tools/Hand Teleop/Recreate OVRHands")]
    public static void Recreate()
    {
        var existing = GameObject.Find("OVRHands");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        const string prefabPath = "Packages/com.meta.xr.sdk.interaction.ovr/Runtime/Prefabs/OVRHands.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[RecreateOVRHands] Could not load prefab at {prefabPath}");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (instance == null)
        {
            Debug.LogError("[RecreateOVRHands] Failed to instantiate OVRHands prefab.");
            return;
        }

        instance.name = "OVRHands";
        instance.transform.SetParent(null);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        instance.SetActive(true);

        EditorSceneManager.MarkSceneDirty(instance.scene);
        EditorSceneManager.SaveScene(instance.scene);
        Selection.activeObject = instance;
        Debug.Log("[RecreateOVRHands] OVRHands prefab recreated and scene saved.");
    }
}
#endif
