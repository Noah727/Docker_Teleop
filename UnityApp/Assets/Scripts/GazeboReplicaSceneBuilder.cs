using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
[DefaultExecutionOrder(-850)]
public class GazeboReplicaSceneBuilder : MonoBehaviour
{
    [Header("Scene Bootstrap")]
    public bool rebuildOnEnable = true;
    public bool applyInEditMode = true;
    public string workspaceRootName = "GazeboWorkspace";
    public Vector3 workspaceInitialPosition = new Vector3(0f, 0.78f, 2f);
    public Vector3 workspaceInitialEuler = Vector3.zero;

    [Header("Existing Scene Objects")]
    public string robotRootName = "ur5e";
    public string syncGroupName = "GameObjects_sync";
    public bool parentRobotToWorkspace = true;
    public bool parentSyncedObjectsToWorkspace = true;
    public bool hideLegacyRoomAndDesk = true;

    [Header("Gazebo Table")]
    public string tableName = "Gazebo_Table";
    public Vector3 tableSize = new Vector3(2.0f, 0.8f, 2.0f);
    public Vector3 tableLocalPosition = new Vector3(0f, -0.4f, 0f);
    public Color tableColor = new Color(0.75f, 0.75f, 0.75f, 1f);

    [Header("Workspace Drag Skirt")]
    public string dragHandleRootName = "WorkspaceDragHandle_Ring";
    public Color dragHandleColor = new Color(0.1f, 0.72f, 1.0f, 0.88f);
    public float dragHandleYOffset = -0.78f;
    public float dragHandleThickness = 0.045f;
    public float dragHandleOutset = 0.055f;
    public string rotateHandleRootName = "WorkspaceRotateHandle_Ring";
    public Color rotateHandleColor = new Color(0.78f, 0.82f, 0.86f, 0.36f);
    public float rotateHandleThickness = 0.025f;
    public float rotateHandleOutset = 0.20f;
    public int rotateHandleSegments = 72;

    [Header("Workspace Handle Visibility")]
    public bool useXRayHandleMaterial = true;
    [Range(0.02f, 1f)]
    public float handleOccludedAlphaMultiplier = 0.28f;

    [Header("Synced Gazebo Objects")]
    public bool createMissingSyncedObjects = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapForGazeboReplicaScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name.IndexOf("GazeboReplica", System.StringComparison.OrdinalIgnoreCase) < 0)
            return;
        if (scene.name.IndexOf("DualArm", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return;

        if (FindAny<GazeboReplicaSceneBuilder>() != null)
            return;

        GameObject builder = new GameObject("GazeboReplicaSceneBuilder");
        builder.AddComponent<GazeboReplicaSceneBuilder>();
    }

    private void OnEnable()
    {
        if (IsDualArmReplicaScene())
            return;

        if (!Application.isPlaying && !applyInEditMode)
            return;

        if (rebuildOnEnable)
            BuildOrUpdateScene();
    }

    private static bool IsDualArmReplicaScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        return scene.IsValid()
            && scene.name.IndexOf("GazeboReplica", System.StringComparison.OrdinalIgnoreCase) >= 0
            && scene.name.IndexOf("DualArm", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public void BuildOrUpdateScene()
    {
        Transform workspace = EnsureWorkspaceRoot();
        if (workspace == null)
            return;

        if (hideLegacyRoomAndDesk)
            HideLegacyObjects();

        EnsureGazeboTable(workspace);
        EnsureWorkspaceHandles(workspace);
        EnsureSyncedObjectGroup(workspace);
        ParentExistingOperationalObjects(workspace);
        ConfigureSyncedPoseSubscribers(workspace);
        ConfigureSceneObjectPoseSyncManagers(workspace);
        ConfigureWorkspaceDragController(workspace);
        ConfigureHandPoseSender(workspace);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(gameObject);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    private Transform EnsureWorkspaceRoot()
    {
        GameObject workspace = GameObject.Find(workspaceRootName);
        bool created = false;
        if (workspace == null)
        {
            workspace = new GameObject(workspaceRootName);
            created = true;
        }

        if (created)
        {
            workspace.transform.position = workspaceInitialPosition;
            workspace.transform.rotation = Quaternion.Euler(workspaceInitialEuler);
            workspace.transform.localScale = Vector3.one;
        }
        else
        {
            workspace.transform.rotation = Quaternion.Euler(0f, workspace.transform.eulerAngles.y, 0f);
        }

        return workspace.transform;
    }

    private void EnsureGazeboTable(Transform workspace)
    {
        GameObject table = FindChildOrSceneObject(tableName, workspace);
        if (table == null)
            table = GameObject.CreatePrimitive(PrimitiveType.Cube);

        table.name = tableName;
        table.transform.SetParent(workspace, false);
        table.transform.localPosition = tableLocalPosition;
        table.transform.localRotation = Quaternion.identity;
        table.transform.localScale = tableSize;
        MarkWorkspaceMember(table);
        ApplyMaterial(table, "Gazebo_Table_Material", tableColor);

        Collider collider = table.GetComponent<Collider>();
        if (collider != null)
            collider.enabled = false;
    }

    private void EnsureWorkspaceHandles(Transform workspace)
    {
        EnsureHandleRing(
            workspace,
            dragHandleRootName,
            "WorkspaceDragHandle",
            dragHandleColor,
            dragHandleOutset,
            dragHandleThickness);

        EnsureCircularHandleRing(
            workspace,
            rotateHandleRootName,
            "WorkspaceRotateHandle",
            rotateHandleColor,
            rotateHandleOutset,
            rotateHandleThickness);
    }

    private void EnsureCircularHandleRing(Transform workspace, string rootName, string segmentPrefix, Color color, float outset, float thickness)
    {
        GameObject handleRoot = FindChildOrSceneObject(rootName, workspace);
        if (handleRoot == null)
            handleRoot = new GameObject(rootName);

        handleRoot.name = rootName;
        handleRoot.transform.SetParent(workspace, false);
        handleRoot.transform.localPosition = Vector3.zero;
        handleRoot.transform.localRotation = Quaternion.identity;
        handleRoot.transform.localScale = Vector3.one;
        MarkWorkspaceMember(handleRoot);

        int segmentCount = Mathf.Clamp(rotateHandleSegments, 24, 144);
        float y = dragHandleYOffset;
        float radius = Mathf.Max(tableSize.x, tableSize.z) * 0.5f + outset;
        float segmentLength = (2f * Mathf.PI * radius / segmentCount) * 1.08f;
        float radialThickness = Mathf.Max(0.01f, thickness);
        string materialName = $"{segmentPrefix}_Material";

        RemoveStaleHandleChildren(handleRoot.transform, segmentPrefix, segmentCount);

        for (int i = 0; i < segmentCount; i++)
        {
            float angle = (2f * Mathf.PI * i) / segmentCount;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Vector3 localPosition = new Vector3(radial.x * radius, y, radial.z * radius);
            Quaternion localRotation = Quaternion.FromToRotation(Vector3.right, tangent);
            Vector3 localScale = new Vector3(segmentLength, thickness, radialThickness);

            EnsureHandleBar(
                handleRoot.transform,
                $"{segmentPrefix}_Segment_{i:00}",
                localPosition,
                localScale,
                color,
                materialName,
                localRotation);
        }
    }

    private void EnsureHandleRing(Transform workspace, string rootName, string barPrefix, Color color, float outset, float thickness)
    {
        GameObject handleRoot = FindChildOrSceneObject(rootName, workspace);
        if (handleRoot == null)
            handleRoot = new GameObject(rootName);

        handleRoot.name = rootName;
        handleRoot.transform.SetParent(workspace, false);
        handleRoot.transform.localPosition = Vector3.zero;
        handleRoot.transform.localRotation = Quaternion.identity;
        handleRoot.transform.localScale = Vector3.one;
        MarkWorkspaceMember(handleRoot);

        float halfX = tableSize.x * 0.5f + outset;
        float halfZ = tableSize.z * 0.5f + outset;
        float y = dragHandleYOffset;
        float t = thickness;

        EnsureHandleBar(handleRoot.transform, $"{barPrefix}_Front", new Vector3(0f, y, halfZ), new Vector3(tableSize.x + outset * 2f, t, t), color, $"{barPrefix}_Material");
        EnsureHandleBar(handleRoot.transform, $"{barPrefix}_Back", new Vector3(0f, y, -halfZ), new Vector3(tableSize.x + outset * 2f, t, t), color, $"{barPrefix}_Material");
        EnsureHandleBar(handleRoot.transform, $"{barPrefix}_Left", new Vector3(-halfX, y, 0f), new Vector3(t, t, tableSize.z + outset * 2f), color, $"{barPrefix}_Material");
        EnsureHandleBar(handleRoot.transform, $"{barPrefix}_Right", new Vector3(halfX, y, 0f), new Vector3(t, t, tableSize.z + outset * 2f), color, $"{barPrefix}_Material");
    }

    private void EnsureHandleBar(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color, string materialName)
    {
        EnsureHandleBar(parent, name, localPosition, localScale, color, materialName, Quaternion.identity);
    }

    private void EnsureHandleBar(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color, string materialName, Quaternion localRotation)
    {
        Transform existing = parent.Find(name);
        GameObject bar = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = name;
        bar.transform.SetParent(parent, false);
        bar.transform.localPosition = localPosition;
        bar.transform.localRotation = localRotation;
        bar.transform.localScale = localScale;
        ApplyHandleMaterial(bar, materialName, color);

        Collider collider = bar.GetComponent<Collider>();
        if (collider != null)
            collider.enabled = true;
    }

    private void RemoveStaleHandleChildren(Transform parent, string keepPrefix, int keepCount)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child == null)
                continue;

            if (IsExpectedHandleSegment(child.name, keepPrefix, keepCount))
                continue;

            DestroyGeneratedObject(child.gameObject);
        }
    }

    private static bool IsExpectedHandleSegment(string objectName, string prefix, int keepCount)
    {
        string segmentPrefix = $"{prefix}_Segment_";
        if (string.IsNullOrWhiteSpace(objectName) || !objectName.StartsWith(segmentPrefix, System.StringComparison.OrdinalIgnoreCase))
            return false;

        string indexText = objectName.Substring(segmentPrefix.Length);
        if (!int.TryParse(indexText, out int index))
            return false;

        return index >= 0 && index < keepCount;
    }

    private static void DestroyGeneratedObject(GameObject go)
    {
        if (go == null)
            return;

        if (Application.isPlaying)
            Destroy(go);
        else
            DestroyImmediate(go);
    }

    private void EnsureSyncedObjectGroup(Transform workspace)
    {
        GameObject group = GameObject.Find(syncGroupName);
        if (group == null)
            group = new GameObject(syncGroupName);

        if (parentSyncedObjectsToWorkspace)
            group.transform.SetParent(workspace, false);
        group.transform.localPosition = Vector3.zero;
        group.transform.localRotation = Quaternion.identity;
        group.transform.localScale = Vector3.one;
        MarkWorkspaceMember(group);

        if (!createMissingSyncedObjects)
            return;

        EnsureSyncCube(group.transform, "Sync_RedCube", new Vector3(0.12779f, 0.020f, 0.59938f), new Vector3(0.020f, 0.040f, 0.020f), new Color(0.85f, 0.12f, 0.12f, 1f));
        EnsureSyncCube(group.transform, "Sync_GreenCube", new Vector3(-0.05621f, 0.020f, 0.74638f), new Vector3(0.020f, 0.040f, 0.020f), new Color(0.10f, 0.70f, 0.18f, 1f));
        EnsureSyncCylinder(group.transform, "Sync_RedCylinder", new Vector3(-0.20921f, 0.024f, 0.54938f), 0.010f, 0.048f, new Color(0.85f, 0.12f, 0.12f, 1f));
        EnsureSyncCylinder(group.transform, "Sync_GreenCylinder", new Vector3(-0.24021f, 0.024f, 0.66438f), 0.010f, 0.048f, new Color(0.10f, 0.70f, 0.18f, 1f));
        EnsureSyncCube(group.transform, "Sync_Plate_A", new Vector3(-0.35221f, 0.0025f, 0.58938f), new Vector3(0.100f, 0.005f, 0.100f), new Color(0.60f, 0.80f, 0.95f, 1f));
        EnsureSyncCube(group.transform, "Sync_Plate_B", new Vector3(-0.01721f, 0.0025f, 0.59638f), new Vector3(0.100f, 0.005f, 0.100f), new Color(0.60f, 0.80f, 0.95f, 1f));
    }

    private void EnsureSyncCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject go = GameObject.Find(name);
        if (go == null)
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);

        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;
        MarkWorkspaceMember(go);
        ApplyMaterial(go, name + "_Material", color);
        DisableCollider(go);
    }

    private void EnsureSyncCylinder(Transform parent, string name, Vector3 localPosition, float radius, float height, Color color)
    {
        GameObject go = GameObject.Find(name);
        if (go == null)
            go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        MarkWorkspaceMember(go);
        ApplyMaterial(go, name + "_Material", color);
        DisableCollider(go);
    }

    private void DisableCollider(GameObject go)
    {
        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            collider.enabled = false;
    }

    private void ParentExistingOperationalObjects(Transform workspace)
    {
        if (parentRobotToWorkspace)
        {
            GameObject robot = GameObject.Find(robotRootName);
            if (robot != null && robot.transform != workspace)
            {
                robot.transform.SetParent(workspace, false);
                robot.transform.localPosition = Vector3.zero;
                robot.transform.localRotation = Quaternion.identity;
                robot.transform.localScale = Vector3.one;
                MarkWorkspaceMember(robot);
            }
        }

        if (parentSyncedObjectsToWorkspace)
        {
            GameObject syncGroup = GameObject.Find(syncGroupName);
            if (syncGroup != null && syncGroup.transform != workspace && syncGroup.transform.parent != workspace)
            {
                syncGroup.transform.SetParent(workspace, false);
                syncGroup.transform.localPosition = Vector3.zero;
                syncGroup.transform.localRotation = Quaternion.identity;
                syncGroup.transform.localScale = Vector3.one;
                MarkWorkspaceMember(syncGroup);
            }
        }
    }

    private void ConfigureSyncedPoseSubscribers(Transform workspace)
    {
        GazeboPoseStampedSubscriber[] subscribers = FindAll<GazeboPoseStampedSubscriber>();
        foreach (GazeboPoseStampedSubscriber subscriber in subscribers)
        {
            if (subscriber == null)
                continue;

            subscriber.workspaceRootName = workspaceRootName;
            subscriber.workspaceRoot = workspace;
            subscriber.applyWorkspaceTransform = true;
        }
    }

    private void ConfigureSceneObjectPoseSyncManagers(Transform workspace)
    {
        SceneObjectPoseSyncManager[] managers = FindAll<SceneObjectPoseSyncManager>();
        foreach (SceneObjectPoseSyncManager manager in managers)
        {
            if (manager == null)
                continue;

            manager.workspaceRootName = workspaceRootName;
            manager.workspaceRoot = workspace;
            manager.applyWorkspaceTransform = true;
            manager.referenceFrame = workspace;
        }
    }

    private void ConfigureWorkspaceDragController(Transform workspace)
    {
        WorkspaceDragController dragger = FindAny<WorkspaceDragController>();
        if (dragger == null)
            return;

        dragger.workspaceRootName = workspaceRootName;
        dragger.workspaceRoot = workspace;
        dragger.dragHandleNameContains = "WorkspaceDragHandle";
        dragger.rotateHandleNameContains = "WorkspaceRotateHandle";
        dragger.requireRayHitOnHandle = true;
        dragger.allowVerticalDrag = true;
        dragger.translateByRayGrabPoint = true;
        dragger.allowYawRotation = true;
        dragger.yawRotationScale = -1.0f;
        dragger.hideHandlesUnlessRayHovering = true;
        dragger.disableHandleCollidersWhenRayHidden = true;
        dragger.keepHandlesVisibleWhileDragging = true;
        dragger.operationalRootNames = new[] { robotRootName, syncGroupName };
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

    private void ConfigureHandPoseSender(Transform workspace)
    {
        HandPoseSender sender = FindAny<HandPoseSender>();
        if (sender == null || workspace == null)
            return;

        sender.sendRelativeToHeadset = false;
        sender.sendRelativeToControlFrame = false;
        sender.controlFrameName = workspaceRootName;
        sender.controlFrameTransform = workspace;
        sender.includeWorkspacePoseInControls = true;
        sender.mappingMode = "unity_world_delta";
    }

    private void HideLegacyObjects()
    {
        string[] tokens =
        {
            "Desk", "RoomShell", "Room_Wall", "Room_Ceiling", "FloorPlank",
            "Bookshelf", "Shelf", "Plant", "WoodWall", "Wallpaper",
            "CeilingLightRig", "Light_Bulb", "Light_Canopy", "Light_Stem"
        };

        Transform[] transforms = FindAll<Transform>();
        foreach (Transform t in transforms)
        {
            if (t == null || !t.gameObject.scene.IsValid() || t.hideFlags != HideFlags.None)
                continue;

            if (IsOperationalObject(t.name))
                continue;

            foreach (string token in tokens)
            {
                if (t.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    t.gameObject.SetActive(false);
                    break;
                }
            }
        }
    }

    private bool IsOperationalObject(string objectName)
    {
        return objectName == workspaceRootName
            || objectName == tableName
            || objectName.IndexOf("WorkspaceDragHandle", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("WorkspaceRotateHandle", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName == robotRootName
            || objectName == syncGroupName
            || objectName.StartsWith("Sync_", System.StringComparison.OrdinalIgnoreCase)
            || objectName.IndexOf("NetworkSender", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("QuestMRFeatures", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("OVRCameraRig", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("GripperDataCamera", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("Teleop", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("RobotBaseView", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private GameObject FindChildOrSceneObject(string name, Transform preferredParent)
    {
        if (preferredParent != null)
        {
            Transform child = preferredParent.Find(name);
            if (child != null)
                return child.gameObject;
        }
        return GameObject.Find(name);
    }

    private void ApplyMaterial(GameObject go, string materialName, Color color)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null)
            return;

        Material material = renderer.sharedMaterial;
        if (material == null || material.name != materialName)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
            material = new Material(shader) { name = materialName };
            renderer.sharedMaterial = material;
        }

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (color.a < 0.999f)
            ConfigureTransparentMaterial(material);
    }

    private void ApplyHandleMaterial(GameObject go, string materialName, Color color)
    {
        if (!useXRayHandleMaterial)
        {
            ApplyMaterial(go, materialName, color);
            return;
        }

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null)
            return;

        Shader shader = Shader.Find("Custom/XRayTransparentHandle")
            ?? Resources.Load<Shader>("Shaders/XRayTransparentHandle");
        if (shader == null)
        {
            ApplyMaterial(go, materialName, color);
            return;
        }

        string xrayMaterialName = materialName + "_XRay";
        Material material = renderer.sharedMaterial;
        if (material == null || material.name != xrayMaterialName || material.shader != shader)
        {
            material = new Material(shader) { name = xrayMaterialName };
            renderer.sharedMaterial = material;
        }

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_OccludedAlphaMultiplier"))
            material.SetFloat("_OccludedAlphaMultiplier", handleOccludedAlphaMultiplier);

        material.renderQueue = 3020;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        material.renderQueue = 3000;
        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
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

#if UNITY_EDITOR
    [MenuItem("Tools/Gazebo Replica/Rebuild Workspace In Active Scene")]
    public static void RebuildWorkspaceInActiveScene()
    {
        GazeboReplicaSceneBuilder builder = FindAny<GazeboReplicaSceneBuilder>();
        if (builder == null)
        {
            GameObject go = new GameObject("GazeboReplicaSceneBuilder");
            builder = go.AddComponent<GazeboReplicaSceneBuilder>();
        }
        builder.BuildOrUpdateScene();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
#endif
}
