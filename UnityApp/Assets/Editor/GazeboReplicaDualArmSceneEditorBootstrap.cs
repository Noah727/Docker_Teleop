#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GazeboReplicaDualArmSceneEditorBootstrap
{
    private const string SceneToken = "DualArm";
    private const string BuilderName = "GazeboReplicaDualArmSceneBuilder";
    private const string DualArmScenePath = "Assets/Scenes/GazeboReplica_DualArm_MR.unity";
    private static bool queued;

    static GazeboReplicaDualArmSceneEditorBootstrap()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChangedInEditMode;
        EditorApplication.delayCall += QueueActiveSceneRebuild;
    }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += QueueActiveSceneRebuild;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        QueueSceneRebuild(scene);
    }

    private static void OnActiveSceneChangedInEditMode(Scene previousScene, Scene newScene)
    {
        QueueSceneRebuild(newScene);
    }

    private static void QueueActiveSceneRebuild()
    {
        QueueSceneRebuild(SceneManager.GetActiveScene());
    }

    private static void QueueSceneRebuild(Scene scene)
    {
        if (queued || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (!IsDualArmScene(scene))
            return;

        queued = true;
        EditorApplication.delayCall += () =>
        {
            queued = false;
            RebuildScene(SceneManager.GetActiveScene());
        };
    }

    private static bool IsDualArmScene(Scene scene)
    {
        return scene.IsValid()
            && scene.name.IndexOf("GazeboReplica", System.StringComparison.OrdinalIgnoreCase) >= 0
            && scene.name.IndexOf(SceneToken, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void RebuildScene(Scene scene)
    {
        if (!IsDualArmScene(scene))
            return;

        GazeboReplicaDualArmSceneBuilder builder = FindDualArmBuilder();
        if (builder == null)
        {
            GameObject go = GameObject.Find(BuilderName);
            if (go == null)
                go = new GameObject(BuilderName);
            builder = go.GetComponent<GazeboReplicaDualArmSceneBuilder>();
            if (builder == null)
                builder = go.AddComponent<GazeboReplicaDualArmSceneBuilder>();
        }

        builder.BuildOrUpdateScene();
        EditorUtility.SetDirty(builder);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static GazeboReplicaDualArmSceneBuilder FindDualArmBuilder()
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<GazeboReplicaDualArmSceneBuilder>(FindObjectsInactive.Include);
#else
        return UnityEngine.Object.FindObjectOfType<GazeboReplicaDualArmSceneBuilder>(true);
#endif
    }

    public static void RebuildDualArmSceneFile()
    {
        Scene scene = EditorSceneManager.OpenScene(DualArmScenePath, OpenSceneMode.Single);
        RebuildScene(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }
}
#endif
