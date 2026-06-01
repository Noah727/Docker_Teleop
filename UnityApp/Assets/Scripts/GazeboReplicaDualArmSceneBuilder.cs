using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
[DefaultExecutionOrder(-830)]
public class GazeboReplicaDualArmSceneBuilder : MonoBehaviour
{
    [Header("Scene")]
    public bool rebuildOnEnable = true;
    public bool applyInEditMode = true;
    public string sceneNameToken = "DualArm";
    public string workspaceRootName = "GazeboWorkspace";
    public string sourceRobotName = "ur5e";
    public string leftRobotName = "left_ur5e";
    public string rightRobotName = "right_ur5e";
    public string syncGroupName = "GameObjects_sync";
    public string taskGroupsRootName = "TaskGroups";
    public string activeTaskGroupName = "TaskGroup_Main";
    public string taskProfileResourcePath = "TaskProfiles/active_task";
    public bool deleteObjectsNotInActiveTask = true;

    [Header("Base Gazebo Replica")]
    public bool configureBaseReplicaScene = true;

    [Header("Robot Placement")]
    public Vector3 leftRobotLocalPosition = new Vector3(-0.60f, 0f, 0f);
    public Vector3 rightRobotLocalPosition = new Vector3(0.60f, 0f, 0f);
    public Vector3 leftRobotLocalEuler = new Vector3(0f, 90f, 0f);
    public Vector3 rightRobotLocalEuler = new Vector3(0f, -90f, 0f);

    [Header("Task Object Placement")]
    public Vector3 taskGroupLocalPosition = new Vector3(0f, 0f, 0.64f);
    public Vector3 taskGroupLocalEuler = Vector3.zero;

    [Header("Overhead POV")]
    public string overheadCameraName = "DualArm_OverheadPOVCamera";
    public Vector3 overheadCameraLocalPosition = new Vector3(0f, 1.65f, 0.05f);
    public Vector3 overheadCameraLocalEuler = new Vector3(90f, 0f, 0f);
    public bool createDisabledOverheadCamera = false;

    [Header("Control Panel")]
    public bool ensureCentralControlPanel = true;
    public string centralControlPanelHostName = "MR_Central_ControlPanel_Controller";
    public bool deleteLegacyUiObjects = true;
    public string[] legacyUiObjectNames =
    {
        "Teleop_Button_Instructions",
        "Teleop_Runtime_DebugPanel",
        "GripperCameraFloatingPanel",
        "RobotBaseViewWindow"
    };

    [Header("Visual Markers")]
    public Color cameraMarkerColor = new Color(0.24f, 0.78f, 1.0f, 0.22f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapForDualArmScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name.IndexOf("DualArm", System.StringComparison.OrdinalIgnoreCase) < 0)
            return;

        if (FindAny<GazeboReplicaDualArmSceneBuilder>() != null)
            return;

        GameObject builder = new GameObject("GazeboReplicaDualArmSceneBuilder");
        builder.AddComponent<GazeboReplicaDualArmSceneBuilder>();
    }

    private void OnEnable()
    {
        if (!IsDualArmScene())
            return;
        if (!Application.isPlaying && !applyInEditMode)
            return;
        if (rebuildOnEnable)
            BuildOrUpdateScene();
    }

    public void BuildOrUpdateScene()
    {
        if (!IsDualArmScene())
            return;

        Transform workspace = ResolveWorkspaceRoot();
        if (workspace == null)
            return;

        if (configureBaseReplicaScene)
            EnsureBaseReplicaScene(workspace);
        EnsureDualRobotVisuals(workspace);
        UnityTaskProfile taskProfile = LoadTaskProfile();
        Transform activeTaskGroup = EnsureTaskGroup(workspace, taskProfile);
        RepositionTaskObjects(activeTaskGroup, taskProfile);
        ConfigureWorkspaceDragController(workspace);
        EnsureOverheadPovCamera(workspace);
        CleanupLegacyUi();
        EnsureCentralControlPanel();
        ConfigureCameraMarkerColors();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(gameObject);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    private bool IsDualArmScene()
    {
        Scene scene = gameObject.scene.IsValid() ? gameObject.scene : SceneManager.GetActiveScene();
        return scene.IsValid() && scene.name.IndexOf(sceneNameToken, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private Transform ResolveWorkspaceRoot()
    {
        GameObject workspace = GameObject.Find(workspaceRootName);
        if (workspace == null)
            workspace = new GameObject(workspaceRootName);
        return workspace.transform;
    }

    private void EnsureBaseReplicaScene(Transform workspace)
    {
        GazeboReplicaSceneBuilder baseBuilder = FindAny<GazeboReplicaSceneBuilder>();
        if (baseBuilder == null)
            baseBuilder = gameObject.AddComponent<GazeboReplicaSceneBuilder>();

        baseBuilder.rebuildOnEnable = false;
        baseBuilder.applyInEditMode = applyInEditMode;
        baseBuilder.workspaceRootName = workspaceRootName;
        baseBuilder.robotRootName = sourceRobotName;
        baseBuilder.syncGroupName = syncGroupName;
        baseBuilder.parentRobotToWorkspace = false;
        baseBuilder.parentSyncedObjectsToWorkspace = true;
        baseBuilder.hideLegacyRoomAndDesk = true;
        // In the dual-arm scene, task-profile objects are authored below TaskGroups/TaskGroup_Main.
        // Do not let the single-arm fallback builder create a second old-position Sync_* set.
        baseBuilder.createMissingSyncedObjects = false;
        baseBuilder.BuildOrUpdateScene();
    }

    private void EnsureDualRobotVisuals(Transform workspace)
    {
        GameObject template = GameObject.Find(sourceRobotName);
        GameObject left = GameObject.Find(leftRobotName);
        GameObject right = GameObject.Find(rightRobotName);

        if (template != null && template.name != leftRobotName && template.name != rightRobotName)
            template.SetActive(false);

        if (left == null)
            left = CreateRobotVisual(leftRobotName, template);
        if (right == null)
            right = CreateRobotVisual(rightRobotName, template);

        ConfigureRobotVisual(left, workspace, leftRobotLocalPosition, leftRobotLocalEuler);
        ConfigureRobotJointSync(left, "left_");
        ConfigureRobotVisual(right, workspace, rightRobotLocalPosition, rightRobotLocalEuler);
        ConfigureRobotJointSync(right, "right_");
    }

    private GameObject CreateRobotVisual(string robotName, GameObject template)
    {
        GameObject robot;
        if (template != null)
        {
            robot = Instantiate(template);
            robot.SetActive(true);
        }
        else
        {
            robot = CreatePlaceholderRobot(robotName);
        }

        robot.name = robotName;
        return robot;
    }

    private GameObject CreatePlaceholderRobot(string robotName)
    {
        GameObject root = new GameObject(robotName);

        GameObject baseCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseCylinder.name = "Base_Placeholder";
        baseCylinder.transform.SetParent(root.transform, false);
        baseCylinder.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        baseCylinder.transform.localScale = new Vector3(0.22f, 0.04f, 0.22f);

        GameObject reach = GameObject.CreatePrimitive(PrimitiveType.Cube);
        reach.name = "Arm_Reach_Placeholder";
        reach.transform.SetParent(root.transform, false);
        reach.transform.localPosition = new Vector3(0f, 0.35f, 0.28f);
        reach.transform.localScale = new Vector3(0.08f, 0.08f, 0.55f);

        return root;
    }

    private void ConfigureRobotVisual(GameObject robot, Transform workspace, Vector3 localPosition, Vector3 localEuler)
    {
        if (robot == null)
            return;

        robot.transform.SetParent(workspace, false);
        robot.transform.localPosition = localPosition;
        robot.transform.localRotation = Quaternion.Euler(localEuler);
        robot.transform.localScale = Vector3.one;
        MarkWorkspaceMember(robot);
    }

    private static void ConfigureRobotJointSync(GameObject robot, string rosJointPrefix)
    {
        if (robot == null)
            return;

        Ur5eTrajectorySubscriber subscriber = robot.GetComponent<Ur5eTrajectorySubscriber>();
        if (subscriber == null)
            subscriber = robot.AddComponent<Ur5eTrajectorySubscriber>();

        subscriber.enabled = true;
        subscriber.useJointStates = true;
        subscriber.jointStatesTopic = "/joint_states";
        subscriber.rosJointNamePrefix = rosJointPrefix;
        subscriber.subscribeTrajectoryWhileJointStatesEnabled = false;
        subscriber.autoFillByName = true;
        subscriber.visualizeGripperFromJointState = true;
        subscriber.useGripperArticulationBodies = false;
        subscriber.gripperJointName = "robotiq_hande_left_finger_joint";
        subscriber.leftGripperJointName = "robotiq_hande_left_finger_joint";
        subscriber.rightGripperJointName = "robotiq_hande_right_finger_joint";
        subscriber.gripperClosedPositionMeters = -0.025f;
        subscriber.gripperOpenPositionMeters = 0.0f;
        subscriber.leftFingerNameContains = "robotiq_hande_left_finger";
        subscriber.rightFingerNameContains = "robotiq_hande_right_finger";
        subscriber.leftFingerLocalAxis = Vector3.forward;
        subscriber.rightFingerLocalAxis = Vector3.forward;
        subscriber.debugGripperVisualSync = true;
        subscriber.gripperVisualDebugLogPeriodSec = 2.0f;
    }

    private Transform EnsureTaskGroup(Transform workspace, UnityTaskProfile taskProfile)
    {
        GameObject taskGroups = GameObject.Find(taskGroupsRootName);
        if (taskGroups == null)
            taskGroups = new GameObject(taskGroupsRootName);

        taskGroups.transform.SetParent(workspace, false);
        taskGroups.transform.localPosition = Vector3.zero;
        taskGroups.transform.localRotation = Quaternion.identity;
        taskGroups.transform.localScale = Vector3.one;
        MarkWorkspaceMember(taskGroups);

        string taskGroupName = taskProfile != null && !string.IsNullOrWhiteSpace(taskProfile.taskGroupName)
            ? taskProfile.taskGroupName
            : activeTaskGroupName;
        Transform taskGroup = taskGroups.transform.Find(taskGroupName);
        if (taskGroup == null)
        {
            GameObject go = new GameObject(taskGroupName);
            go.transform.SetParent(taskGroups.transform, false);
            taskGroup = go.transform;
        }

        taskGroup.localPosition = taskProfile != null ? taskProfile.TaskGroupLocalPosition : taskGroupLocalPosition;
        taskGroup.localRotation = Quaternion.Euler(taskProfile != null ? taskProfile.TaskGroupLocalEuler : taskGroupLocalEuler);
        taskGroup.localScale = Vector3.one;
        return taskGroup;
    }

    private void RepositionTaskObjects(Transform activeTaskGroup, UnityTaskProfile taskProfile)
    {
        if (activeTaskGroup == null)
            return;

        Transform syncGroup = ResolveCanonicalSyncGroup(activeTaskGroup);
        if (syncGroup == null)
            return;

        if (taskProfile != null && taskProfile.objects != null && taskProfile.objects.Length > 0)
        {
            HashSet<string> activeObjectNames = new HashSet<string>();
            foreach (UnityTaskObject taskObject in taskProfile.objects)
            {
                if (taskObject == null || string.IsNullOrWhiteSpace(taskObject.id))
                    continue;
                activeObjectNames.Add(taskObject.id);
                EnsureTaskObject(syncGroup, taskObject);
            }

            List<GameObject> staleTaskObjects = new List<GameObject>();
            for (int i = 0; i < syncGroup.childCount; i++)
            {
                Transform child = syncGroup.GetChild(i);
                if (!child.name.StartsWith("Sync_", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                bool isActiveTaskObject = activeObjectNames.Contains(child.name);
                if (isActiveTaskObject)
                {
                    child.gameObject.SetActive(true);
                }
                else if (deleteObjectsNotInActiveTask)
                {
                    staleTaskObjects.Add(child.gameObject);
                }
                else
                {
                    child.gameObject.SetActive(false);
                }
            }

            foreach (GameObject staleTaskObject in staleTaskObjects)
                DestroySceneObject(staleTaskObject);

            DestroyDuplicateSyncObjectsOutside(syncGroup, activeObjectNames);
            return;
        }

        SetLocalPositionIfFound(syncGroup, "Sync_RedCube", new Vector3(-0.055f, 0.020f, -0.060f));
        SetLocalPositionIfFound(syncGroup, "Sync_GreenCube", new Vector3(0.055f, 0.020f, -0.060f));
        SetLocalPositionIfFound(syncGroup, "Sync_RedCylinder", new Vector3(-0.055f, 0.024f, 0.060f));
        SetLocalPositionIfFound(syncGroup, "Sync_GreenCylinder", new Vector3(0.055f, 0.024f, 0.060f));
        SetLocalPositionIfFound(syncGroup, "Sync_Plate_A", new Vector3(-0.160f, 0.0025f, 0.000f));
        SetLocalPositionIfFound(syncGroup, "Sync_Plate_B", new Vector3(0.160f, 0.0025f, 0.000f));
    }

    private Transform ResolveCanonicalSyncGroup(Transform activeTaskGroup)
    {
        Transform canonical = activeTaskGroup.Find(syncGroupName);
        List<Transform> groups = new List<Transform>();
        foreach (Transform transform in FindAll<Transform>())
        {
            if (transform == null || transform.gameObject == null)
                continue;
            if (!transform.gameObject.scene.IsValid())
                continue;
            if (string.Equals(transform.name, syncGroupName, System.StringComparison.Ordinal))
            {
                groups.Add(transform);
                if (canonical == null)
                    canonical = transform;
            }
        }

        if (canonical == null)
        {
            GameObject go = new GameObject(syncGroupName);
            canonical = go.transform;
        }

        canonical.name = syncGroupName;
        canonical.SetParent(activeTaskGroup, false);
        canonical.localPosition = Vector3.zero;
        canonical.localRotation = Quaternion.identity;
        canonical.localScale = Vector3.one;
        MarkWorkspaceMember(canonical.gameObject);

        foreach (Transform group in groups)
        {
            if (group == null || group == canonical)
                continue;

            for (int i = group.childCount - 1; i >= 0; i--)
            {
                Transform child = group.GetChild(i);
                if (child == null || !child.name.StartsWith("Sync_", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (canonical.Find(child.name) == null)
                    child.SetParent(canonical, true);
            }

            DestroySceneObject(group.gameObject);
        }

        return canonical;
    }

    private UnityTaskProfile LoadTaskProfile()
    {
        if (string.IsNullOrWhiteSpace(taskProfileResourcePath))
            return null;

        TextAsset asset = Resources.Load<TextAsset>(taskProfileResourcePath);
        if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            return null;

        try
        {
            return JsonUtility.FromJson<UnityTaskProfile>(asset.text);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[GazeboReplicaDualArmSceneBuilder] Failed to parse task profile '{taskProfileResourcePath}': {ex.Message}");
            return null;
        }
    }

    private void EnsureTaskObject(Transform parent, UnityTaskObject taskObject)
    {
        PrimitiveType primitiveType = taskObject.IsCylinder ? PrimitiveType.Cylinder : PrimitiveType.Cube;
        GameObject go = ResolveCanonicalTaskObject(parent, taskObject.id, primitiveType);

        go.name = taskObject.id;
        go.SetActive(true);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = taskObject.LocalPosition;
        go.transform.localRotation = Quaternion.Euler(taskObject.LocalEuler);
        MarkWorkspaceMember(go);

        if (taskObject.IsRubik)
        {
            go.transform.localScale = Vector3.one;
            ConfigureRubikVisual(go, taskObject);
        }
        else if (taskObject.IsRubikCubie)
        {
            go.transform.localScale = Vector3.one;
            ConfigureRubikCubieVisual(go, taskObject);
        }
        else if (taskObject.IsPortBox)
        {
            go.transform.localScale = Vector3.one;
            ConfigurePortBoxVisual(go, taskObject);
        }
        else if (taskObject.IsCableRod)
        {
            go.transform.localScale = Vector3.one;
            ConfigureCableRodVisual(go, taskObject);
        }
        else
        {
            go.transform.localScale = taskObject.IsCylinder
                ? new Vector3(taskObject.radius * 2f, taskObject.height * 0.5f, taskObject.radius * 2f)
                : taskObject.Size;
            RemoveRubikVisuals(go.transform);
            ApplyTaskMaterial(go, taskObject.id + "_Material", taskObject.Color);
            DisableCollider(go);
        }
    }

    private GameObject ResolveCanonicalTaskObject(Transform parent, string objectName, PrimitiveType primitiveType)
    {
        Transform canonical = parent.Find(objectName);
        List<Transform> matches = new List<Transform>();
        foreach (Transform transform in FindAll<Transform>())
        {
            if (transform == null || transform.gameObject == null)
                continue;
            if (!transform.gameObject.scene.IsValid())
                continue;
            if (!string.Equals(transform.name, objectName, System.StringComparison.Ordinal))
                continue;

            matches.Add(transform);
            if (canonical == null)
                canonical = transform;
        }

        if (canonical == null)
        {
            GameObject go = GameObject.CreatePrimitive(primitiveType);
            canonical = go.transform;
        }

        foreach (Transform duplicate in matches)
        {
            if (duplicate != null && duplicate != canonical)
                DestroySceneObject(duplicate.gameObject);
        }

        return canonical.gameObject;
    }

    private void DestroyDuplicateSyncObjectsOutside(Transform canonicalSyncGroup, HashSet<string> activeObjectNames)
    {
        if (canonicalSyncGroup == null || activeObjectNames == null)
            return;

        List<GameObject> duplicates = new List<GameObject>();
        foreach (Transform transform in FindAll<Transform>())
        {
            if (transform == null || transform.gameObject == null)
                continue;
            if (!transform.gameObject.scene.IsValid())
                continue;
            if (!activeObjectNames.Contains(transform.name))
                continue;
            if (transform.parent == canonicalSyncGroup)
                continue;

            duplicates.Add(transform.gameObject);
        }

        foreach (GameObject duplicate in duplicates)
            DestroySceneObject(duplicate);
    }

    private static void SetLocalPositionIfFound(Transform parent, string childName, Vector3 localPosition)
    {
        Transform child = parent.Find(childName);
        if (child != null)
            child.localPosition = localPosition;
    }

    private static void ApplyTaskMaterial(GameObject go, string materialName, Color color)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null)
            return;
        renderer.enabled = true;
        Material material = renderer.sharedMaterial;
        if (material == null || material.name != materialName)
        {
            Shader shader = Shader.Find("Standard");
            material = new Material(shader != null ? shader : Shader.Find("Diffuse"));
            material.name = materialName;
            renderer.sharedMaterial = material;
        }
        material.color = color;
    }

    private static void ConfigureRubikVisual(GameObject root, UnityTaskObject taskObject)
    {
        if (root == null)
            return;

        Renderer rootRenderer = root.GetComponent<Renderer>();
        if (rootRenderer != null)
            rootRenderer.enabled = false;
        DisableCollider(root);

        EnsureRubikCore(root.transform, taskObject);
        EnsureRubikStickers(root.transform, taskObject);
    }

    private static void ConfigureRubikCubieVisual(GameObject root, UnityTaskObject taskObject)
    {
        if (root == null)
            return;

        Renderer rootRenderer = root.GetComponent<Renderer>();
        if (rootRenderer != null)
            rootRenderer.enabled = false;
        DisableCollider(root);
        RemoveRubikVisuals(root.transform);

        GameObject core = EnsureChildCube(root.transform, "RubikCubieCore");
        core.transform.localPosition = Vector3.zero;
        core.transform.localRotation = Quaternion.identity;
        core.transform.localScale = taskObject.Size;
        ApplyTaskMaterial(core, "RubikCubie_Plastic", taskObject.Color);
        DisableCollider(core);
        EnsureRubikCubieStickers(root.transform, taskObject);
    }

    private static void EnsureRubikCubieStickers(Transform root, UnityTaskObject taskObject)
    {
        Vector3 size = taskObject.Size;
        float minSize = Mathf.Max(0.001f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)));
        float thickness = Mathf.Max(minSize * 0.040f, 0.00055f);
        float stickerGap = Mathf.Max(0f, taskObject.stickerGap);
        float sx = Mathf.Max(0.001f, size.x - stickerGap);
        float sy = Mathf.Max(0.001f, size.y - stickerGap);
        float sz = Mathf.Max(0.001f, size.z - stickerGap);

        if (taskObject.rubikX > 0)
            EnsureSticker(root, "Sticker_X_Pos", new Vector3(size.x * 0.5f + thickness * 0.5f, 0f, 0f), new Vector3(thickness, sy, sz), new Color(0.85f, 0.05f, 0.04f, 1f));
        else if (taskObject.rubikX < 0)
            EnsureSticker(root, "Sticker_X_Neg", new Vector3(-(size.x * 0.5f + thickness * 0.5f), 0f, 0f), new Vector3(thickness, sy, sz), new Color(1.00f, 0.42f, 0.02f, 1f));

        if (taskObject.rubikY > 0)
            EnsureSticker(root, "Sticker_Y_Pos", new Vector3(0f, size.y * 0.5f + thickness * 0.5f, 0f), new Vector3(sx, thickness, sz), new Color(0.95f, 0.95f, 0.90f, 1f));
        else if (taskObject.rubikY < 0)
            EnsureSticker(root, "Sticker_Y_Neg", new Vector3(0f, -(size.y * 0.5f + thickness * 0.5f), 0f), new Vector3(sx, thickness, sz), new Color(0.95f, 0.85f, 0.06f, 1f));

        if (taskObject.rubikZ > 0)
            EnsureSticker(root, "Sticker_Z_Pos", new Vector3(0f, 0f, size.z * 0.5f + thickness * 0.5f), new Vector3(sx, sy, thickness), new Color(0.08f, 0.65f, 0.12f, 1f));
        else if (taskObject.rubikZ < 0)
            EnsureSticker(root, "Sticker_Z_Neg", new Vector3(0f, 0f, -(size.z * 0.5f + thickness * 0.5f)), new Vector3(sx, sy, thickness), new Color(0.04f, 0.18f, 0.85f, 1f));
    }

    private static void ConfigurePortBoxVisual(GameObject root, UnityTaskObject taskObject)
    {
        if (root == null)
            return;

        Renderer rootRenderer = root.GetComponent<Renderer>();
        if (rootRenderer != null)
            rootRenderer.enabled = false;
        DisableCollider(root);
        RemoveRubikVisuals(root.transform);

        GameObject body = EnsureChildCube(root.transform, "PortBoxBody");
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = taskObject.Size;
        ApplyTaskMaterial(body, "PortBox_Body", taskObject.Color);
        DisableCollider(body);

        Vector3 portSize = taskObject.PortSize;
        GameObject port = EnsureChildCube(root.transform, "PortVisual");
        port.transform.localPosition = new Vector3(0f, 0f, taskObject.Size.z * 0.5f + 0.0005f);
        port.transform.localRotation = Quaternion.identity;
        port.transform.localScale = new Vector3(portSize.x, portSize.y, 0.001f);
        ApplyTaskMaterial(port, "PortBox_Port", taskObject.PortColor);
        DisableCollider(port);
    }

    private static void ConfigureCableRodVisual(GameObject root, UnityTaskObject taskObject)
    {
        if (root == null)
            return;

        Renderer rootRenderer = root.GetComponent<Renderer>();
        if (rootRenderer != null)
            rootRenderer.enabled = false;
        DisableCollider(root);
        RemoveRubikVisuals(root.transform);

        GameObject rod = EnsureChildCube(root.transform, "CableRodBody");
        rod.transform.localPosition = Vector3.zero;
        rod.transform.localRotation = Quaternion.identity;
        rod.transform.localScale = taskObject.Size;
        ApplyTaskMaterial(rod, "CableRod_Body", taskObject.Color);
        DisableCollider(rod);

        Vector3 plugSize = taskObject.PlugSize;
        GameObject plug = EnsureChildCube(root.transform, "CablePlugEnd");
        plug.transform.localPosition = new Vector3(0f, 0f, -(taskObject.Size.z * 0.5f + plugSize.z * 0.5f));
        plug.transform.localRotation = Quaternion.identity;
        plug.transform.localScale = plugSize;
        ApplyTaskMaterial(plug, "CableRod_Plug", taskObject.PlugColor);
        DisableCollider(plug);
    }

    private static GameObject EnsureChildCube(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void EnsureSticker(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject sticker = EnsureChildCube(parent, name);
        sticker.transform.localPosition = localPosition;
        sticker.transform.localRotation = Quaternion.identity;
        sticker.transform.localScale = localScale;
        ApplyTaskMaterial(sticker, name + "_Material", color);
        DisableCollider(sticker);
    }

    private static void EnsureRubikCore(Transform cubeRoot, UnityTaskObject taskObject)
    {
        const string coreName = "RubikCore";
        Transform core = cubeRoot.Find(coreName);
        GameObject coreObject;
        if (core == null)
        {
            coreObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            coreObject.name = coreName;
            coreObject.transform.SetParent(cubeRoot, false);
        }
        else
        {
            coreObject = core.gameObject;
            coreObject.SetActive(true);
        }

        coreObject.transform.localPosition = Vector3.zero;
        coreObject.transform.localRotation = Quaternion.identity;
        coreObject.transform.localScale = taskObject.Size;
        ApplyTaskMaterial(coreObject, coreName + "_Material", taskObject.Color);
        DisableCollider(coreObject);
    }

    private static void EnsureRubikStickers(Transform cubeRoot, UnityTaskObject taskObject)
    {
        if (cubeRoot == null)
            return;

        const string prefix = "RubikSticker_";
        int order = Mathf.Max(1, taskObject.rubikOrder);
        Vector3 size = taskObject.Size;
        float minSize = Mathf.Max(0.001f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)));
        float thickness = Mathf.Max(minSize * 0.018f, 0.0005f);
        float gap = Mathf.Max(0f, taskObject.stickerGap);
        HashSet<string> active = new HashSet<string>();

        int index = 0;
        CreateRubikFace(cubeRoot, prefix, ref index, active, order, size, thickness, gap, Axis.X, 1f, new Color(0.85f, 0.05f, 0.04f, 1f));
        CreateRubikFace(cubeRoot, prefix, ref index, active, order, size, thickness, gap, Axis.X, -1f, new Color(1.00f, 0.42f, 0.02f, 1f));
        CreateRubikFace(cubeRoot, prefix, ref index, active, order, size, thickness, gap, Axis.Y, 1f, new Color(0.95f, 0.95f, 0.90f, 1f));
        CreateRubikFace(cubeRoot, prefix, ref index, active, order, size, thickness, gap, Axis.Y, -1f, new Color(0.95f, 0.85f, 0.06f, 1f));
        CreateRubikFace(cubeRoot, prefix, ref index, active, order, size, thickness, gap, Axis.Z, 1f, new Color(0.08f, 0.65f, 0.12f, 1f));
        CreateRubikFace(cubeRoot, prefix, ref index, active, order, size, thickness, gap, Axis.Z, -1f, new Color(0.04f, 0.18f, 0.85f, 1f));

        for (int i = cubeRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = cubeRoot.GetChild(i);
            if (child.name.StartsWith(prefix, System.StringComparison.Ordinal) && !active.Contains(child.name))
                DestroySceneObject(child.gameObject);
        }
    }

    private enum Axis
    {
        X,
        Y,
        Z
    }

    private static void CreateRubikFace(
        Transform cubeRoot,
        string prefix,
        ref int index,
        HashSet<string> active,
        int order,
        Vector3 size,
        float thickness,
        float gap,
        Axis normalAxis,
        float sign,
        Color color)
    {
        Vector3 axesLength = size;
        Axis uAxis = normalAxis == Axis.X ? Axis.Y : Axis.X;
        Axis vAxis = normalAxis == Axis.Z ? Axis.Y : Axis.Z;
        float uLength = GetAxis(axesLength, uAxis);
        float vLength = GetAxis(axesLength, vAxis);
        float uTile = Mathf.Max(uLength / order - gap, uLength / order * 0.5f);
        float vTile = Mathf.Max(vLength / order - gap, vLength / order * 0.5f);

        for (int u = 0; u < order; u++)
        {
            for (int v = 0; v < order; v++)
            {
                string name = prefix + index.ToString("00");
                active.Add(name);
                Transform sticker = cubeRoot.Find(name);
                GameObject stickerObject;
                if (sticker == null)
                {
                    stickerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    stickerObject.name = name;
                    stickerObject.transform.SetParent(cubeRoot, false);
                }
                else
                {
                    stickerObject = sticker.gameObject;
                    stickerObject.SetActive(true);
                }

                Vector3 localPosition = Vector3.zero;
                SetAxis(ref localPosition, normalAxis, sign * (GetAxis(size, normalAxis) * 0.5f + thickness * 0.5f));
                SetAxis(ref localPosition, uAxis, RubikOffset(u, order, uLength));
                SetAxis(ref localPosition, vAxis, RubikOffset(v, order, vLength));

                Vector3 localScale = Vector3.zero;
                SetAxis(ref localScale, normalAxis, thickness);
                SetAxis(ref localScale, uAxis, uTile);
                SetAxis(ref localScale, vAxis, vTile);

                stickerObject.transform.localPosition = localPosition;
                stickerObject.transform.localRotation = Quaternion.identity;
                stickerObject.transform.localScale = localScale;
                ApplyTaskMaterial(stickerObject, name + "_Material", color);
                DisableCollider(stickerObject);
                index++;
            }
        }
    }

    private static float RubikOffset(int index, int order, float length)
    {
        float cell = length / order;
        return -length * 0.5f + cell * 0.5f + index * cell;
    }

    private static float GetAxis(Vector3 value, Axis axis)
    {
        switch (axis)
        {
            case Axis.X:
                return value.x;
            case Axis.Y:
                return value.y;
            default:
                return value.z;
        }
    }

    private static void SetAxis(ref Vector3 value, Axis axis, float component)
    {
        switch (axis)
        {
            case Axis.X:
                value.x = component;
                break;
            case Axis.Y:
                value.y = component;
                break;
            default:
                value.z = component;
                break;
        }
    }

    private static void RemoveRubikVisuals(Transform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child.name.StartsWith("RubikSticker_", System.StringComparison.Ordinal) ||
                child.name.StartsWith("Sticker_", System.StringComparison.Ordinal) ||
                string.Equals(child.name, "RubikCore", System.StringComparison.Ordinal) ||
                string.Equals(child.name, "RubikCubieCore", System.StringComparison.Ordinal) ||
                string.Equals(child.name, "PortBoxBody", System.StringComparison.Ordinal) ||
                string.Equals(child.name, "PortVisual", System.StringComparison.Ordinal) ||
                string.Equals(child.name, "CableRodBody", System.StringComparison.Ordinal) ||
                string.Equals(child.name, "CablePlugEnd", System.StringComparison.Ordinal))
                DestroySceneObject(child.gameObject);
        }
    }

    private static void DestroySceneObject(GameObject go)
    {
        if (go == null)
            return;

        if (Application.isPlaying)
            Destroy(go);
        else
            DestroyImmediate(go);
    }

    private static void DisableCollider(GameObject go)
    {
        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            collider.enabled = false;
    }

    private void ConfigureWorkspaceDragController(Transform workspace)
    {
        WorkspaceDragController dragger = FindAny<WorkspaceDragController>();
        if (dragger == null)
            return;

        dragger.workspaceRootName = workspaceRootName;
        dragger.workspaceRoot = workspace;
        dragger.operationalRootNames = new[] { leftRobotName, rightRobotName, taskGroupsRootName };
        dragger.yawRotationScale = -1.0f;
        dragger.hideHandlesUnlessRayHovering = true;
        dragger.disableHandleCollidersWhenRayHidden = true;
        dragger.keepHandlesVisibleWhileDragging = true;
    }

    private void EnsureOverheadPovCamera(Transform workspace)
    {
        if (!createDisabledOverheadCamera)
        {
            GameObject staleCamera = GameObject.Find(overheadCameraName);
            if (staleCamera != null)
                DestroySceneObject(staleCamera);
            return;
        }

        GameObject cameraObject = GameObject.Find(overheadCameraName);
        if (cameraObject == null)
            cameraObject = new GameObject(overheadCameraName);

        cameraObject.transform.SetParent(workspace, false);
        cameraObject.transform.localPosition = overheadCameraLocalPosition;
        cameraObject.transform.localRotation = Quaternion.Euler(overheadCameraLocalEuler);
        cameraObject.transform.localScale = Vector3.one;
        MarkWorkspaceMember(cameraObject);

        Camera camera = cameraObject.GetComponent<Camera>();
        if (camera == null)
            camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.fieldOfView = 90f;
    }

    private void EnsureCentralControlPanel()
    {
        if (!ensureCentralControlPanel)
            return;

        MRCentralControlPanel controlPanel = FindAny<MRCentralControlPanel>();
        if (controlPanel == null)
        {
            GameObject host = GameObject.Find(centralControlPanelHostName);
            if (host == null)
                host = new GameObject(centralControlPanelHostName);
            controlPanel = host.AddComponent<MRCentralControlPanel>();
        }

        controlPanel.gameObject.SetActive(true);
        controlPanel.enabled = true;
        controlPanel.createPanel = true;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            controlPanel.RefreshPanelInEditor();
            EditorUtility.SetDirty(controlPanel);
        }
#endif
    }

    private void ConfigureCameraMarkerColors()
    {
        foreach (GripperCameraRecorder recorder in FindAll<GripperCameraRecorder>())
        {
            if (recorder == null)
                continue;
            recorder.markerColor = cameraMarkerColor;
            recorder.rebuildSceneMarkerFromSettings = true;
        }

        foreach (FloatingSceneCameraController floatingCamera in FindAll<FloatingSceneCameraController>())
        {
            if (floatingCamera == null)
                continue;
            floatingCamera.markerColor = cameraMarkerColor;
        }
    }

    private void CleanupLegacyUi()
    {
        if (!deleteLegacyUiObjects)
            return;

        if (legacyUiObjectNames != null)
        {
            foreach (string objectName in legacyUiObjectNames)
            {
                if (string.IsNullOrWhiteSpace(objectName))
                    continue;

                foreach (Transform transform in FindAll<Transform>())
                {
                    if (transform == null || transform.name != objectName)
                        continue;

                    DestroySceneObject(transform.gameObject);
                }
            }
        }

        foreach (TeleopInstructionBoard board in FindAll<TeleopInstructionBoard>())
            DestroySceneComponent(board);

        foreach (TeleopRuntimeDebugPanel debugPanel in FindAll<TeleopRuntimeDebugPanel>())
            DestroySceneComponent(debugPanel);

        foreach (GripperCameraRecorder recorder in FindAll<GripperCameraRecorder>())
        {
            if (recorder != null)
                recorder.createFloatingPanel = false;
        }
    }

    private static void MarkWorkspaceMember(GameObject go)
    {
        if (go == null)
            return;
        GazeboWorkspaceMember member = go.GetComponent<GazeboWorkspaceMember>();
        if (member == null)
            member = go.AddComponent<GazeboWorkspaceMember>();
        member.keepWorldPoseWhenParented = true;
    }

    private static T FindAny<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
        return Object.FindObjectOfType<T>(true);
#endif
    }

    private static T[] FindAll<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<T>(true);
#endif
    }

    private static void DestroySceneComponent(Component component)
    {
        if (component == null)
            return;

        if (Application.isPlaying)
            Destroy(component);
        else
            DestroyImmediate(component);
    }

#pragma warning disable 0649
    [System.Serializable]
    private class UnityTaskProfile
    {
        public string taskProfile;
        public string taskGroupName;
        public SerializableVector3 taskGroupLocalPosition;
        public SerializableVector3 taskGroupLocalEuler;
        public UnityTaskObject[] objects;

        public Vector3 TaskGroupLocalPosition => taskGroupLocalPosition.ToVector3();
        public Vector3 TaskGroupLocalEuler => taskGroupLocalEuler.ToVector3();
    }

    [System.Serializable]
    private class UnityTaskObject
    {
        public string id;
        public string type;
        public SerializableVector3 localPosition;
        public SerializableVector3 localEuler;
        public SerializableVector3 size;
        public float radius = 0.01f;
        public float height = 0.048f;
        public int rubikOrder = 2;
        public int rubikX;
        public int rubikY;
        public int rubikZ;
        public float stickerGap = 0.002f;
        public SerializableVector3 portSize;
        public SerializableColor portColor;
        public SerializableVector3 plugSize;
        public SerializableColor plugColor;
        public SerializableColor color;

        public Vector3 LocalPosition => localPosition.ToVector3();
        public Vector3 LocalEuler => localEuler.ToVector3();
        public Vector3 Size => size.ToVector3(new Vector3(0.02f, 0.04f, 0.02f));
        public Vector3 PortSize => portSize.ToVector3(new Vector3(0.018f, 0.006f, 0.014f));
        public Vector3 PlugSize => plugSize.ToVector3(new Vector3(0.016f, 0.010f, 0.016f));
        public Color Color => color.ToColor(new Color(0.8f, 0.8f, 0.8f, 1f));
        public Color PortColor => portColor.ToColor(new Color(0.01f, 0.01f, 0.012f, 1f));
        public Color PlugColor => plugColor.ToColor(new Color(0.12f, 0.12f, 0.13f, 1f));
        public bool IsCylinder => string.Equals(type, "cylinder", System.StringComparison.OrdinalIgnoreCase);
        public bool IsRubik => string.Equals(type, "rubik_2x2", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "rubik", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "rubiks_cube", System.StringComparison.OrdinalIgnoreCase);
        public bool IsRubikCubie => string.Equals(type, "rubik_cubie", System.StringComparison.OrdinalIgnoreCase);
        public bool IsPortBox => string.Equals(type, "port_box", System.StringComparison.OrdinalIgnoreCase);
        public bool IsCableRod => string.Equals(type, "cable_rod", System.StringComparison.OrdinalIgnoreCase);
    }

    [System.Serializable]
    private struct SerializableVector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }

        public Vector3 ToVector3(Vector3 fallback)
        {
            if (x == 0f && y == 0f && z == 0f)
                return fallback;
            return ToVector3();
        }
    }

    [System.Serializable]
    private struct SerializableColor
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public Color ToColor(Color fallback)
        {
            if (r == 0f && g == 0f && b == 0f && a == 0f)
                return fallback;
            return new Color(r, g, b, a);
        }
    }
#pragma warning restore 0649

#if UNITY_EDITOR
    [MenuItem("Tools/Gazebo Replica/Rebuild Dual Arm Workspace In Active Scene")]
    public static void RebuildDualArmWorkspaceInActiveScene()
    {
        GazeboReplicaDualArmSceneBuilder builder = FindAny<GazeboReplicaDualArmSceneBuilder>();
        if (builder == null)
        {
            GameObject go = new GameObject("GazeboReplicaDualArmSceneBuilder");
            builder = go.AddComponent<GazeboReplicaDualArmSceneBuilder>();
        }
        builder.BuildOrUpdateScene();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
#endif
}
