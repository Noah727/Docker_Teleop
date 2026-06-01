using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-900)]
public class QuestPassthroughController : MonoBehaviour
{
    public static QuestPassthroughController ActiveInstance { get; private set; }

    [Header("Passthrough")]
    public bool enablePassthroughOnStart = true;
    [Range(0.0f, 1.0f)] public float passthroughOpacity = 1.0f;
    public bool useUnderlay = true;
    public bool disableEdgeRendering = true;

    [Header("Virtual Room Visibility")]
    public bool hideVirtualRoomOnStart = true;
    public string[] hideObjectNameContains =
    {
        "RoomShell", "RoomDecor", "Room_Wall", "Room_Ceiling", "FloorPlank",
        "Bookshelf", "Shelf", "Plant", "WoodWall", "Wallpaper"
    };
    public string[] keepObjectNameContains =
    {
        "QuestMRFeatures", "OVRCameraRig", "Main Camera", "NetworkSender", "Desk",
        "GazeboWorkspace", "Gazebo_Table", "WorkspaceDragHandle", "WorkspaceRotateHandle",
        "ur5e", "base_link", "GameObjects_sync", "Sync_", "GripperDataCamera",
        "GripperCameraFloatingPanel", "Teleop_Button_Instructions", "Teleop_Runtime_DebugPanel",
        "RobotBaseViewWindow", "Instructions"
    };

    [Header("Cameras")]
    public bool configureHeadsetCamerasForPassthrough = true;
    public Color passthroughCameraClearColor = new Color(0f, 0f, 0f, 0f);

    public bool IsPassthroughConfigured { get; private set; }
    public bool RoomHidden { get; private set; }
    public string LastStatus { get; private set; } = "Not configured";

    private OVRPassthroughLayer passthroughLayer;

    private void Awake()
    {
        ActiveInstance = this;
    }

    private void Start()
    {
        if (enablePassthroughOnStart)
            ApplyPassthroughMode();
    }

    public void ApplyPassthroughMode()
    {
        OVRManager manager = FindAny<OVRManager>();
        if (manager != null)
            manager.isInsightPassthroughEnabled = true;

        passthroughLayer = FindAny<OVRPassthroughLayer>();
        if (passthroughLayer == null)
        {
            GameObject layerObject = new GameObject("Quest_Passthrough_Underlay");
            Transform parent = manager != null ? manager.transform : transform;
            layerObject.transform.SetParent(parent, false);
            passthroughLayer = layerObject.AddComponent<OVRPassthroughLayer>();
        }

        passthroughLayer.hidden = false;
        passthroughLayer.overlayType = useUnderlay ? OVROverlay.OverlayType.Underlay : OVROverlay.OverlayType.Overlay;
        passthroughLayer.textureOpacity = passthroughOpacity;
        passthroughLayer.edgeRenderingEnabled = !disableEdgeRendering;

        if (configureHeadsetCamerasForPassthrough)
            ConfigureHeadsetCameras();

        if (hideVirtualRoomOnStart)
            SetVirtualRoomVisible(false);

        IsPassthroughConfigured = manager != null && passthroughLayer != null;
        LastStatus = IsPassthroughConfigured
            ? $"MR passthrough ON, roomHidden={RoomHidden}"
            : "MR passthrough requested, but OVRManager/OVRPassthroughLayer was not ready";

        Debug.Log($"[QuestPassthroughController] {LastStatus}");
    }

    public void SetVirtualRoomVisible(bool visible)
    {
        int changed = 0;
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform candidate in transforms)
        {
            if (candidate == null)
                continue;

            GameObject go = candidate.gameObject;
            if (!go.scene.IsValid() || go.hideFlags != HideFlags.None)
                continue;

            if (IsKept(go.name) || !IsHiddenRoomObject(go.name))
                continue;

            if (go.activeSelf != visible)
            {
                go.SetActive(visible);
                changed++;
            }
        }

        RoomHidden = !visible;
        LastStatus = $"Virtual room visible={visible}, changed={changed}";
        Debug.Log($"[QuestPassthroughController] {LastStatus}");
    }

    private void ConfigureHeadsetCameras()
    {
        Camera[] cameras = FindAll<Camera>();
        foreach (Camera camera in cameras)
        {
            if (camera == null || !camera.gameObject.scene.IsValid())
                continue;

            if (camera.targetTexture != null)
                continue;

            string n = camera.name;
            if (n.Contains("GripperDataCamera") || n.Contains("RobotBaseViewCamera"))
                continue;

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = passthroughCameraClearColor;
        }
    }

    private bool IsHiddenRoomObject(string objectName)
    {
        if (string.IsNullOrEmpty(objectName) || hideObjectNameContains == null)
            return false;

        foreach (string token in hideObjectNameContains)
        {
            if (!string.IsNullOrWhiteSpace(token) && objectName.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private bool IsKept(string objectName)
    {
        if (string.IsNullOrEmpty(objectName) || keepObjectNameContains == null)
            return false;

        foreach (string token in keepObjectNameContains)
        {
            if (!string.IsNullOrWhiteSpace(token) && objectName.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
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
}
