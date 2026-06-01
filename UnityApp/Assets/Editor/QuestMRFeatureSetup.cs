using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class QuestMRFeatureSetup
{
    private const string OculusProjectConfigPath = "Assets/Oculus/OculusProjectConfig.asset";

    [MenuItem("Tools/Quest MR Features/Add Runtime Feature Root To Scene")]
    public static void AddRuntimeFeatureRootToScene()
    {
        GameObject root = GameObject.Find("QuestMRFeatures");
        if (root == null)
        {
            root = new GameObject("QuestMRFeatures");
            Undo.RegisterCreatedObjectUndo(root, "Create Quest MR feature root");
        }

        AddIfMissing<QuestMRFeatureBootstrap>(root);
        AddIfMissing<QuestPassthroughController>(root);
        AddIfMissing<MRCentralControlPanel>(root);
        AddIfMissing<WorkspaceDragController>(root);
        AddIfMissing<ControllerRayVisual>(root);

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[QuestMRFeatureSetup] Added QuestMRFeatures root and runtime feature components to the active scene.");
    }

    [MenuItem("Tools/Quest MR Features/Enable Meta Passthrough Project Setting")]
    public static void EnableMetaPassthroughProjectSetting()
    {
        Object asset = AssetDatabase.LoadAssetAtPath<Object>(OculusProjectConfigPath);
        if (asset == null)
        {
            Debug.LogWarning($"[QuestMRFeatureSetup] Could not load {OculusProjectConfigPath}");
            return;
        }

        SerializedObject serialized = new SerializedObject(asset);
        SerializedProperty support = serialized.FindProperty("_insightPassthroughSupport");
        if (support == null)
        {
            Debug.LogWarning("[QuestMRFeatureSetup] _insightPassthroughSupport property not found.");
            return;
        }

        support.enumValueIndex = 1; // OVRProjectConfig.FeatureSupport.Supported
        SerializedProperty obsoleteBool = serialized.FindProperty("insightPassthroughEnabled");
        if (obsoleteBool != null)
            obsoleteBool.boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        Debug.Log("[QuestMRFeatureSetup] Meta passthrough support set to Supported.");
    }

    private static T AddIfMissing<T>(GameObject root) where T : Component
    {
        T component = root.GetComponent<T>();
        if (component == null)
            component = Undo.AddComponent<T>(root);
        return component;
    }
}
