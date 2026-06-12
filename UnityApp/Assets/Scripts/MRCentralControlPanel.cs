using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;
using System.Globalization;
using RosMessageTypes.Std;
using Unity.Robotics.ROSTCPConnector;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
[DefaultExecutionOrder(510)]
public class MRCentralControlPanel : MonoBehaviour
{
    private const string HandleName = "DragHandle";

    private enum PanelPage
    {
        Controls,
        Camera,
        Attachment,
        Haptics,
        TaskSwitch,
        Debug
    }

    private enum CameraPreviewMode
    {
        LeftWrist,
        RightWrist,
        Floating
    }

    [Header("Panel")]
    public bool createPanel = true;
    public string panelName = "MR_Central_ControlPanel";
    public Vector3 panelWorldPosition = new Vector3(0f, 1.7f, 1.0f);
    public Vector3 panelWorldEuler = Vector3.zero;
    public Vector3 panelWorldScale = new Vector3(0.00115f, 0.00115f, 0.00115f);
    public Vector2 panelSize = new Vector2(780f, 620f);
    public Color panelColor = new Color(0.018f, 0.021f, 0.021f, 0.90f);
    public Color accentColor = new Color(1.0f, 0.72f, 0.36f, 1.0f);
    public Color inactiveTabColor = new Color(0.10f, 0.11f, 0.11f, 0.86f);
    public Color activeTabColor = new Color(0.26f, 0.18f, 0.10f, 0.96f);
    public Color actionButtonColor = new Color(0.15f, 0.10f, 0.05f, 0.96f);
    public Color resetButtonColor = new Color(0.20f, 0.08f, 0.04f, 0.96f);
    public Color cameraButtonColor = new Color(0.06f, 0.14f, 0.18f, 0.96f);
    public Color recordActiveButtonColor = new Color(0.62f, 0.08f, 0.06f, 0.98f);
    public Color buttonTextColor = new Color(1.0f, 0.94f, 0.78f, 1.0f);
    public Color dragHandleColor = new Color(0.64f, 0.64f, 0.64f, 0.62f);

    [Header("Input")]
    public string leftControllerName = "LeftControllerAnchor";
    public string rightControllerName = "RightControllerAnchor";
    public float triggerThreshold = 0.55f;
    public float rightGripBlockThreshold = 0.55f;
    public float rayMaxDistance = 3.0f;
    public Color contentViewportColor = new Color(0.0f, 0.0f, 0.0f, 0.26f);

    [Header("Camera Preview")]
    public Vector2 previewSize = new Vector2(560f, 220f);

    [Header("Task Manager")]
    public string taskManagerSelectTopic = "/task_manager/select_task";
    public string taskManagerStatusTopic = "/task_manager/status";
    public string[] taskSwitchTaskNames = { "pick_place_basic", "rubik_2x2", "cable_insertion" };

    [Header("Workspace Reset")]
    public string workspaceRootName = "GazeboWorkspace";
    public string headsetName = "CenterEyeAnchor";
    public float workspaceResetHeight = 0.78f;
    public Vector3 workspaceResetOffsetFromHeadset = Vector3.zero;
    [FormerlySerializedAs("workspaceResetYawToZero")]
    [Tooltip("If enabled, workspace/view reset uses the headset's current yaw while keeping the workspace level.")]
    public bool workspaceResetYawToCurrentHeadset = true;
    [Tooltip("When the user is looking down/up, use headset up/right vectors to recover yaw instead of falling back to the old workspace angle.")]
    public bool robustHeadsetYawForWorkspaceReset = true;

    public string LastStatus { get; private set; } = "not initialized";

    private GameObject panel;
    private RectTransform panelRect;
    private RectTransform headerLayer;
    private RectTransform actionLayer;
    private RectTransform contentLayer;
    private RectTransform statusLayer;
    private RectTransform tabsLayer;
    private RectTransform handlesLayer;
    private RectTransform controlsTab;
    private RectTransform cameraTab;
    private RectTransform attachmentTab;
    private RectTransform hapticsTab;
    private RectTransform taskSwitchTab;
    private RectTransform debugTab;
    private RectTransform swapHandsButton;
    private RectTransform resetObjectsButton;
    private RectTransform resetLeftRobotButton;
    private RectTransform resetRightRobotButton;
    private RectTransform overheadViewButton;
    private RectTransform recordButton;
    private RectTransform captureButton;
    private RectTransform leftWristCameraButton;
    private RectTransform rightWristCameraButton;
    private RectTransform floatingCameraButton;
    private RectTransform attachmentAdjustButton;
    private RectTransform resetLeftAttachmentButton;
    private RectTransform resetRightAttachmentButton;
    private RectTransform resetAllAttachmentButton;
    private RectTransform applyLeftAttachmentButton;
    private RectTransform applyRightAttachmentButton;
    private RectTransform hapticOutputButton;
    private RectTransform hapticRosButton;
    private RectTransform hapticGainDownButton;
    private RectTransform hapticGainUpButton;
    private RectTransform hapticGainResetButton;
    private RectTransform taskPickPlaceButton;
    private RectTransform taskRubikButton;
    private RectTransform taskCableButton;
    private RectTransform applyFloatingCameraButton;
    private RectTransform dragHandle;
    private RectTransform resizeHandle;
    private readonly RectTransform[] resizeHandles = new RectTransform[4];
    private UICornerDragHandle.Corner activeResizeCorner;
    private RectTransform contentScrollArea;
    private RectTransform contentScrollbarRect;
    private Scrollbar contentScrollbar;
    private Text titleText;
    private Text contentText;
    private Text statusText;
    private Text swapHandsButtonText;
    private Text recordButtonText;
    private RawImage previewImage;
    private PanelPage page = PanelPage.Controls;
    private CameraPreviewMode cameraPreviewMode = CameraPreviewMode.RightWrist;
    private Transform leftController;
    private Transform rightController;
    private bool leftTriggerWasHeld;
    private bool rightTriggerWasHeld;
    private bool panelPoseInitialized;
    private bool panelInitialHeadsetFacingApplied;
    private HandPoseSender sender;
    private WorkspaceDragController workspaceDrag;
    private GripperCameraRecorder recorder;
    private GripperCameraRecorder leftRecorder;
    private GripperCameraRecorder rightRecorder;
    private FloatingSceneCameraController floatingCamera;
    private QuestPassthroughController passthrough;
    private QuestHapticFeedbackController haptics;
    private VRDraggableWindow panelDragger;
    private bool resizeDragging;
    private Transform resizeController;
    private Vector2 resizeStartLocalPoint;
    private Vector2 resizeStartSize;
    private InputField leftAttachmentPosInput;
    private InputField leftAttachmentRotInput;
    private InputField rightAttachmentPosInput;
    private InputField rightAttachmentRotInput;
    private InputField floatingCameraPosInput;
    private InputField floatingCameraRotInput;
    private InputField floatingCameraFovInput;
    private ROSConnection ros;
    private bool taskManagerRosReady;
    private string taskManagerStatus = "Task manager: waiting for ROS-TCP.";
    private static Sprite roundedSprite;

    private void OnEnable()
    {
        if (!Application.isPlaying)
            RefreshPanelInEditor();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        EditorApplication.delayCall += () =>
        {
            if (this != null)
                RefreshPanelInEditor();
        };
    }

    [MenuItem("Tools/MR/Refresh Central Control Panel")]
    private static void RefreshCentralControlPanelMenu()
    {
        MRCentralControlPanel controlPanel = FindAny<MRCentralControlPanel>();
        if (controlPanel == null)
        {
            Debug.LogWarning("[MRCentralControlPanel] No MRCentralControlPanel found in the active scene.");
            return;
        }

        controlPanel.RefreshPanelInEditor();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[MRCentralControlPanel] Refreshed central control panel in the editor.");
    }
#endif

    private void Start()
    {
        CleanupLegacyWindows();
        ResolveReferences();
        EnsurePanel();
        UpdatePanel(force: true);
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            RefreshPanelInEditor();
            return;
        }

        CleanupLegacyWindows();
        ResolveReferences();
        EnsurePanel();
        HandleControllerClicks();
        UpdatePanel(force: false);
    }

    private void EnsurePanel()
    {
        if (!createPanel)
            return;

        bool createdPanel = false;
        if (panel == null)
        {
            panel = GameObject.Find(panelName);
            if (panel == null)
            {
                panel = new GameObject(panelName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
                createdPanel = true;
            }
        }

        panelRect = panel.GetComponent<RectTransform>();
        panelRect.sizeDelta = panelSize;
        bool shouldInitializePanelPose = createdPanel || !panelPoseInitialized;
        if (shouldInitializePanelPose)
        {
            panel.transform.position = panelWorldPosition;
            panel.transform.rotation = Quaternion.Euler(panelWorldEuler);
            panelPoseInitialized = true;
            panelInitialHeadsetFacingApplied = false;
        }
        panel.transform.localScale = panelWorldScale;
        SetLayerRecursively(panel, 5);

        Canvas canvas = panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 70;
        if (canvas.worldCamera == null)
            canvas.worldCamera = Camera.main;

        Image background = panel.GetComponent<Image>();
        ApplyRoundedImage(background, panelColor);
        EnsurePanelLayers();
        EnsurePanelSectionBackings();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Font.CreateDynamicFontFromOSFont("Arial", 16);

        titleText = EnsurePanelText("Title", headerLayer, font, "MR Control Panel", 24, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -18f), new Vector2(-48f, 36f), new Vector2(0f, 1f));

        float tabWidth = 106f;
        float tabGap = 8f;
        float tabY = -panelSize.y + 32f;
        float firstTabX = (panelSize.x - ((tabWidth * 6f) + (tabGap * 5f))) * 0.5f + (tabWidth * 0.5f);
        controlsTab = EnsureTab("Tab_Controls", "Controls", font, new Vector2(firstTabX, tabY));
        cameraTab = EnsureTab("Tab_Camera", "Camera", font, new Vector2(firstTabX + tabWidth + tabGap, tabY));
        attachmentTab = EnsureTab("Tab_Attachment", "Attach", font, new Vector2(firstTabX + (tabWidth + tabGap) * 2f, tabY));
        hapticsTab = EnsureTab("Tab_Haptics", "Haptics", font, new Vector2(firstTabX + (tabWidth + tabGap) * 3f, tabY));
        taskSwitchTab = EnsureTab("Tab_TaskSwitch", "Tasks", font, new Vector2(firstTabX + (tabWidth + tabGap) * 4f, tabY));
        debugTab = EnsureTab("Tab_Debug", "Debug", font, new Vector2(firstTabX + (tabWidth + tabGap) * 5f, tabY));

        EnsureFixedContentView(font);
        contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
        contentText.verticalOverflow = VerticalWrapMode.Truncate;

        statusText = EnsurePanelText("Status", statusLayer, font, "", 14, FontStyle.Normal, TextAnchor.LowerLeft);
        SetRect(statusText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 66f), new Vector2(-48f, 30f), new Vector2(0f, 0f));

        previewImage = EnsurePanelRawImage("CameraPreview", contentLayer);
        SetRect(previewImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -292f), CameraPreviewSize(), new Vector2(0.5f, 0.5f));

        RemovePanelChildIfExists("ModeButton");
        swapHandsButton = EnsureButtonRect("SwapHandsButton", "Swap Hands", font, new Vector2(88f, -88f), new Vector2(140f, 38f));
        resetLeftRobotButton = EnsureButtonRect("ResetLeftRobotButton", "Left Arm", font, new Vector2(236f, -88f), new Vector2(126f, 38f));
        resetRightRobotButton = EnsureButtonRect("ResetRightRobotButton", "Right Arm", font, new Vector2(370f, -88f), new Vector2(126f, 38f));
        resetObjectsButton = EnsureButtonRect("ResetObjectsButton", "Objects", font, new Vector2(504f, -88f), new Vector2(126f, 38f));
        overheadViewButton = EnsureButtonRect("OverheadViewButton", "Overhead", font, new Vector2(644f, -88f), new Vector2(132f, 38f));
        RemovePanelChildIfExists("ResetButton");
        RemovePanelChildIfExists("OverheadCameraButton");
        RemovePanelChildIfExists("ResetRobotsButton");
        RemovePanelChildIfExists("ResetAllButton");
        RemovePanelChildIfExists("ResetWorkspaceButton");
        RemovePanelChildIfExists("RubikTwistXButton");
        RemovePanelChildIfExists("RubikTwistYButton");
        RemovePanelChildIfExists("RubikTwistZButton");
        RemovePanelChildIfExists("RubikShuffleButton");
        RemovePanelChildIfExists("RubikResetButton");
        recordButton = EnsureButtonRect("RecordButton", "Record", font, new Vector2(560f, -88f), new Vector2(132f, 38f));
        captureButton = EnsureButtonRect("CaptureButton", "Capture", font, new Vector2(696f, -88f), new Vector2(112f, 38f));
        RemovePanelChildIfExists("WristCameraButton");
        leftWristCameraButton = EnsureButtonRect("LeftWristCameraButton", "Left Wrist", font, new Vector2(88f, -88f), new Vector2(120f, 36f));
        rightWristCameraButton = EnsureButtonRect("RightWristCameraButton", "Right Wrist", font, new Vector2(220f, -88f), new Vector2(128f, 36f));
        floatingCameraButton = EnsureButtonRect("FloatingCameraButton", "Floating", font, new Vector2(364f, -88f), new Vector2(126f, 36f));
        attachmentAdjustButton = EnsureButtonRect("AttachmentAdjustButton", "Hold X/A", font, new Vector2(132f, -88f), new Vector2(210f, 38f));
        resetLeftAttachmentButton = EnsureButtonRect("ResetLeftAttachmentButton", "Reset L", font, new Vector2(326f, -88f), new Vector2(110f, 38f));
        resetRightAttachmentButton = EnsureButtonRect("ResetRightAttachmentButton", "Reset R", font, new Vector2(444f, -88f), new Vector2(110f, 38f));
        resetAllAttachmentButton = EnsureButtonRect("ResetAllAttachmentButton", "Reset Both", font, new Vector2(586f, -88f), new Vector2(150f, 38f));
        applyLeftAttachmentButton = EnsureButtonRect("ApplyLeftAttachmentButton", "Apply L", font, new Vector2(684f, -158f), new Vector2(112f, 34f));
        applyRightAttachmentButton = EnsureButtonRect("ApplyRightAttachmentButton", "Apply R", font, new Vector2(684f, -210f), new Vector2(112f, 34f));
        hapticOutputButton = EnsureButtonRect("HapticOutputButton", "Haptics", font, new Vector2(110f, -88f), new Vector2(150f, 38f));
        hapticRosButton = EnsureButtonRect("HapticRosButton", "ROS Contact", font, new Vector2(280f, -88f), new Vector2(170f, 38f));
        hapticGainDownButton = EnsureButtonRect("HapticGainDownButton", "Gain -", font, new Vector2(458f, -88f), new Vector2(110f, 38f));
        hapticGainUpButton = EnsureButtonRect("HapticGainUpButton", "Gain +", font, new Vector2(580f, -88f), new Vector2(110f, 38f));
        hapticGainResetButton = EnsureButtonRect("HapticGainResetButton", "Gain 1x", font, new Vector2(704f, -88f), new Vector2(120f, 38f));
        taskPickPlaceButton = EnsureButtonRect("TaskPickPlaceButton", "Pick/Place", font, new Vector2(126f, -88f), new Vector2(180f, 38f));
        taskRubikButton = EnsureButtonRect("TaskRubikButton", "Rubik 2x2", font, new Vector2(326f, -88f), new Vector2(180f, 38f));
        taskCableButton = EnsureButtonRect("TaskCableButton", "Cable Insert", font, new Vector2(526f, -88f), new Vector2(190f, 38f));
        applyFloatingCameraButton = EnsureButtonRect("ApplyFloatingCameraButton", "Apply Camera", font, new Vector2(610f, -488f), new Vector2(170f, 34f));
        leftAttachmentPosInput = EnsureInputField("LeftAttachmentPosInput", "L pos x,y,z", font, new Vector2(178f, -158f), new Vector2(280f, 32f));
        leftAttachmentRotInput = EnsureInputField("LeftAttachmentRotInput", "L rot deg x,y,z", font, new Vector2(474f, -158f), new Vector2(280f, 32f));
        rightAttachmentPosInput = EnsureInputField("RightAttachmentPosInput", "R pos x,y,z", font, new Vector2(178f, -210f), new Vector2(280f, 32f));
        rightAttachmentRotInput = EnsureInputField("RightAttachmentRotInput", "R rot deg x,y,z", font, new Vector2(474f, -210f), new Vector2(280f, 32f));
        floatingCameraPosInput = EnsureInputField("FloatingCameraPosInput", "cam pos x,y,z", font, new Vector2(180f, -444f), new Vector2(292f, 32f));
        floatingCameraRotInput = EnsureInputField("FloatingCameraRotInput", "cam rot x,y,z", font, new Vector2(486f, -444f), new Vector2(292f, 32f));
        floatingCameraFovInput = EnsureInputField("FloatingCameraFovInput", "fov", font, new Vector2(430f, -488f), new Vector2(86f, 32f));
        swapHandsButtonText = swapHandsButton.GetComponentInChildren<Text>();
        recordButtonText = recordButton.GetComponentInChildren<Text>();

        dragHandle = EnsureDragDashHandle();
        Color resizeColor = ResizeHandleColor();
        Vector2 resizeSize = new Vector2(44f, 44f);
        float resizeOffset = 4f;
        resizeHandles[(int)UICornerDragHandle.Corner.BottomLeft] = UICornerDragHandle.Ensure(
            handlesLayer,
            "ResizeHandle_BottomLeft",
            resizeColor,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(-resizeOffset, -resizeOffset),
            resizeSize,
            UICornerDragHandle.Corner.BottomLeft);
        resizeHandles[(int)UICornerDragHandle.Corner.BottomRight] = UICornerDragHandle.Ensure(
            handlesLayer,
            "ResizeHandle_BottomRight",
            resizeColor,
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(resizeOffset, -resizeOffset),
            resizeSize,
            UICornerDragHandle.Corner.BottomRight);
        resizeHandles[(int)UICornerDragHandle.Corner.TopLeft] = UICornerDragHandle.Ensure(
            handlesLayer,
            "ResizeHandle_TopLeft",
            resizeColor,
            new Vector2(0f, 1f),
            new Vector2(1f, 0f),
            new Vector2(-resizeOffset, resizeOffset),
            resizeSize,
            UICornerDragHandle.Corner.TopLeft);
        resizeHandles[(int)UICornerDragHandle.Corner.TopRight] = UICornerDragHandle.Ensure(
            handlesLayer,
            "ResizeHandle_TopRight",
            resizeColor,
            new Vector2(1f, 1f),
            new Vector2(0f, 0f),
            new Vector2(resizeOffset, resizeOffset),
            resizeSize,
            UICornerDragHandle.Corner.TopRight);
        resizeHandle = resizeHandles[(int)UICornerDragHandle.Corner.BottomLeft];
        SetLayerRecursively(panel, 5);

        panelDragger = panel.GetComponent<VRDraggableWindow>();
        if (panelDragger == null)
            panelDragger = panel.AddComponent<VRDraggableWindow>();
        panelDragger.windowRoot = panelRect;
        panelDragger.handleNameContains = HandleName;
        panelDragger.requireHandleHit = true;
        panelDragger.allowWholeWindowFallback = false;
        panelDragger.dragController = VRDraggableWindow.DragController.Either;
        panelDragger.dragButton = VRDraggableWindow.DragButton.Trigger;
        panelDragger.requireRightGripReleased = true;
        panelDragger.retryHandleHitWhileButtonHeld = true;
        panelDragger.faceHeadsetWhileDragging = true;
        panelDragger.faceHeadsetOnRelease = true;
        // Keep the panel roll-level, but allow pitch so it tilts toward the user's eyes instead of staying vertical.
        panelDragger.yawOnlyHeadsetFacing = false;

        BindPanelButtonActions();

        if (Application.isPlaying && !panelInitialHeadsetFacingApplied && !panelDragger.IsDragging)
            panelInitialHeadsetFacingApplied = panelDragger.FaceHeadsetNow();
    }

    public void RefreshPanelInEditor()
    {
        if (Application.isPlaying || !createPanel)
            return;

        CleanupLegacyWindows();
        ResolveReferences();
        EnsurePanel();
        UpdatePanel(force: true);
    }

    private void EnsurePanelLayers()
    {
        headerLayer = EnsurePanelLayer("Layer_01_Header");
        actionLayer = EnsurePanelLayer("Layer_02_Actions");
        contentLayer = EnsurePanelLayer("Layer_03_Content");
        statusLayer = EnsurePanelLayer("Layer_04_Status");
        tabsLayer = EnsurePanelLayer("Layer_05_Tabs");
        handlesLayer = EnsurePanelLayer("Layer_06_Handles");

        headerLayer.SetSiblingIndex(0);
        contentLayer.SetSiblingIndex(1);
        actionLayer.SetSiblingIndex(2);
        statusLayer.SetSiblingIndex(3);
        tabsLayer.SetSiblingIndex(4);
        handlesLayer.SetSiblingIndex(5);
    }

    private RectTransform EnsurePanelLayer(string layerName)
    {
        Transform existing = panel.transform.Find(layerName);
        GameObject go;
        if (existing == null)
        {
            go = new GameObject(layerName, typeof(RectTransform));
            go.transform.SetParent(panel.transform, false);
        }
        else
        {
            go = existing.gameObject;
        }

        RectTransform rect = go.GetComponent<RectTransform>();
        SetRect(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        go.layer = panel.layer;
        return rect;
    }

    private void EnsurePanelSectionBackings()
    {
        Color strip = new Color(0f, 0f, 0f, 0.18f);
        Color strongerStrip = new Color(0f, 0f, 0f, 0.24f);

        RectTransform headerBacking = EnsureRectObject("Header_Background", headerLayer, typeof(Image));
        SetRect(headerBacking, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -10f), new Vector2(-32f, 52f), new Vector2(0f, 1f));
        ApplySectionImage(headerBacking, strip);
        headerBacking.SetAsFirstSibling();

        RectTransform actionBacking = EnsureRectObject("Actions_Background", actionLayer, typeof(Image));
        SetRect(actionBacking, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -64f), new Vector2(-32f, 100f), new Vector2(0f, 1f));
        ApplySectionImage(actionBacking, strip);
        actionBacking.SetAsFirstSibling();

        RectTransform statusBacking = EnsureRectObject("Status_Background", statusLayer, typeof(Image));
        SetRect(statusBacking, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 58f), new Vector2(-32f, 44f), new Vector2(0f, 0f));
        ApplySectionImage(statusBacking, strip);
        statusBacking.SetAsFirstSibling();

        RectTransform tabsBacking = EnsureRectObject("Tabs_Background", tabsLayer, typeof(Image));
        SetRect(tabsBacking, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 8f), new Vector2(-32f, 50f), new Vector2(0f, 0f));
        ApplySectionImage(tabsBacking, strongerStrip);
        tabsBacking.SetAsFirstSibling();
    }

    private void ApplySectionImage(RectTransform rect, Color color)
    {
        if (rect == null)
            return;
        Image image = rect.GetComponent<Image>();
        ApplyRoundedImage(image, color);
        if (image != null)
            image.raycastTarget = false;
    }

    private RectTransform EnsureTab(string name, string label, Font font, Vector2 anchoredPosition)
    {
        RectTransform rect = EnsureButtonRect(name, label, font, anchoredPosition, new Vector2(106f, 36f), tabsLayer);
        return rect;
    }

    private RectTransform EnsureButtonRect(string name, string label, Font font, Vector2 anchoredPosition, Vector2 size)
    {
        return EnsureButtonRect(name, label, font, anchoredPosition, size, actionLayer);
    }

    private RectTransform EnsureButtonRect(string name, string label, Font font, Vector2 anchoredPosition, Vector2 size, Transform parent)
    {
        Transform targetParent = parent != null ? parent : panel.transform;
        Transform existing = FindPanelChild(name);
        GameObject go;
        if (existing == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(targetParent, false);
            Text text = EnsureText("Text", go.GetComponent<RectTransform>(), font, label, 15, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        }
        else
        {
            go = existing.gameObject;
            if (go.transform.parent != targetParent)
                go.transform.SetParent(targetParent, false);
            if (go.GetComponent<CanvasRenderer>() == null)
                go.AddComponent<CanvasRenderer>();
            if (go.GetComponent<Image>() == null)
                go.AddComponent<Image>();
            if (go.GetComponent<Button>() == null)
                go.AddComponent<Button>();
        }

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text childText = go.GetComponentInChildren<Text>();
        if (childText != null)
            childText.text = label;

        StyleButton(rect, actionButtonColor, buttonTextColor);
        Button button = go.GetComponent<Button>();
        if (button != null)
            button.targetGraphic = go.GetComponent<Image>();
        return rect;
    }

    private RectTransform EnsureDragDashHandle()
    {
        RectTransform handleRect = EnsurePanelRectObject(HandleName, handlesLayer, typeof(Image));
        SetRect(
            handleRect,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, -18f),
            new Vector2(128f, 18f),
            new Vector2(0.5f, 1f));
        handleRect.SetAsLastSibling();

        for (int i = handleRect.childCount - 1; i >= 0; i--)
            handleRect.GetChild(i).gameObject.SetActive(false);

        Image image = handleRect.GetComponent<Image>();
        ApplyRoundedImage(image, dragHandleColor);
        image.raycastTarget = true;
        return handleRect;
    }

    private void BindPanelButtonActions()
    {
        BindButton(controlsTab, () => SetPage(PanelPage.Controls));
        BindButton(cameraTab, () => SetPage(PanelPage.Camera));
        BindButton(attachmentTab, () => SetPage(PanelPage.Attachment));
        BindButton(hapticsTab, () => SetPage(PanelPage.Haptics));
        BindButton(taskSwitchTab, () => SetPage(PanelPage.TaskSwitch));
        BindButton(debugTab, () => SetPage(PanelPage.Debug));

        BindButton(swapHandsButton, () => { if (sender != null) sender.ToggleControllerArmSwap(); });
        BindButton(resetObjectsButton, () => { if (sender != null) sender.RequestResetObjects(); });
        BindButton(resetLeftRobotButton, () => { if (sender != null) sender.RequestResetLeftRobot(); });
        BindButton(resetRightRobotButton, () => { if (sender != null) sender.RequestResetRightRobot(); });
        BindButton(overheadViewButton, ResetOverheadView);

        BindButton(recordButton, ToggleSelectedRecording);
        BindButton(captureButton, CaptureSelectedCameraFrame);
        BindButton(leftWristCameraButton, () => SelectCameraPreview(CameraPreviewMode.LeftWrist));
        BindButton(rightWristCameraButton, () => SelectCameraPreview(CameraPreviewMode.RightWrist));
        BindButton(floatingCameraButton, () => SelectCameraPreview(CameraPreviewMode.Floating));
        BindButton(applyFloatingCameraButton, ApplyFloatingCameraInputs);

        BindButton(attachmentAdjustButton, () => { if (sender != null) sender.SetAttachmentAdjustmentMode(false); });
        BindButton(resetLeftAttachmentButton, () => { if (sender != null) sender.ResetLeftAttachmentOffset(); });
        BindButton(resetRightAttachmentButton, () => { if (sender != null) sender.ResetRightAttachmentOffset(); });
        BindButton(resetAllAttachmentButton, () => { if (sender != null) sender.ResetAttachmentOffsets(); });
        BindButton(applyLeftAttachmentButton, () => ApplyAttachmentInputs(left: true));
        BindButton(applyRightAttachmentButton, () => ApplyAttachmentInputs(left: false));

        BindButton(hapticOutputButton, () => { if (haptics != null) haptics.ToggleHapticOutput(); });
        BindButton(hapticRosButton, () => { if (haptics != null) haptics.ToggleRosContactHaptics(); });
        BindButton(hapticGainDownButton, () => { if (haptics != null) haptics.AdjustOutputGain(-0.1f); });
        BindButton(hapticGainUpButton, () => { if (haptics != null) haptics.AdjustOutputGain(0.1f); });
        BindButton(hapticGainResetButton, () => { if (haptics != null) haptics.ResetOutputGain(); });

        BindButton(taskPickPlaceButton, () => PublishTaskSelection("pick_place_basic"));
        BindButton(taskRubikButton, () => PublishTaskSelection("rubik_2x2"));
        BindButton(taskCableButton, () => PublishTaskSelection("cable_insertion"));
    }

    private void BindButton(RectTransform rect, UnityAction action)
    {
        if (rect == null || action == null)
            return;

        Button button = rect.GetComponent<Button>();
        if (button == null)
            button = rect.gameObject.AddComponent<Button>();
        Image image = rect.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = true;
            button.targetGraphic = image;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private InputField EnsureInputField(string name, string placeholder, Font font, Vector2 anchoredPosition, Vector2 size)
    {
        Transform targetParent = actionLayer != null ? actionLayer : panel.transform;
        Transform existing = FindPanelChild(name);
        GameObject go;
        if (existing == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
            go.transform.SetParent(targetParent, false);

            Text text = EnsureText("Text", go.GetComponent<RectTransform>(), font, "", 14, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-20f, 0f), new Vector2(0.5f, 0.5f));

            Text placeholderText = EnsureText("Placeholder", go.GetComponent<RectTransform>(), font, placeholder, 14, FontStyle.Italic, TextAnchor.MiddleLeft);
            placeholderText.color = new Color(1f, 0.94f, 0.78f, 0.42f);
            SetRect(placeholderText.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-20f, 0f), new Vector2(0.5f, 0.5f));

            InputField field = go.GetComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholderText;
            field.lineType = InputField.LineType.SingleLine;
            field.contentType = InputField.ContentType.Standard;
        }
        else
        {
            go = existing.gameObject;
            if (go.transform.parent != targetParent)
                go.transform.SetParent(targetParent, false);
            if (go.GetComponent<CanvasRenderer>() == null)
                go.AddComponent<CanvasRenderer>();
            if (go.GetComponent<Image>() == null)
                go.AddComponent<Image>();
            if (go.GetComponent<InputField>() == null)
                go.AddComponent<InputField>();
        }

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        ApplyRoundedImage(image, new Color(0.04f, 0.045f, 0.045f, 0.94f));

        InputField input = go.GetComponent<InputField>();
        if (input.textComponent == null)
            input.textComponent = go.GetComponentInChildren<Text>();
        return input;
    }

    private Text EnsureText(string name, Transform parent, Font font, string text, int fontSize, FontStyle fontStyle, TextAnchor alignment)
    {
        Transform existing = parent.Find(name);
        GameObject go;
        if (existing == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
        }
        else
        {
            go = existing.gameObject;
        }

        Text uiText = go.GetComponent<Text>();
        uiText.font = font;
        uiText.text = text;
        uiText.fontSize = fontSize;
        uiText.fontStyle = fontStyle;
        uiText.alignment = alignment;
        uiText.color = new Color(0.96f, 0.92f, 0.84f, 1.0f);
        return uiText;
    }

    private Text EnsurePanelText(string name, Transform parent, Font font, string text, int fontSize, FontStyle fontStyle, TextAnchor alignment)
    {
        Transform targetParent = parent != null ? parent : panel.transform;
        Transform existing = FindPanelChild(name);
        GameObject go;
        if (existing == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(targetParent, false);
        }
        else
        {
            go = existing.gameObject;
            if (go.transform.parent != targetParent)
                go.transform.SetParent(targetParent, false);
            if (go.GetComponent<CanvasRenderer>() == null)
                go.AddComponent<CanvasRenderer>();
            if (go.GetComponent<Text>() == null)
                go.AddComponent<Text>();
        }

        Text uiText = go.GetComponent<Text>();
        uiText.font = font;
        uiText.text = text;
        uiText.fontSize = fontSize;
        uiText.fontStyle = fontStyle;
        uiText.alignment = alignment;
        uiText.color = new Color(0.96f, 0.92f, 0.84f, 1.0f);
        return uiText;
    }

    private RawImage EnsureRawImage(string name, Transform parent)
    {
        Transform existing = parent.Find(name);
        GameObject go;
        if (existing == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
        }
        else
        {
            go = existing.gameObject;
        }

        RawImage image = go.GetComponent<RawImage>();
        image.color = Color.white;
        return image;
    }

    private RawImage EnsurePanelRawImage(string name, Transform parent)
    {
        Transform targetParent = parent != null ? parent : panel.transform;
        Transform existing = FindPanelChild(name);
        GameObject go;
        if (existing == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(targetParent, false);
        }
        else
        {
            go = existing.gameObject;
            if (go.transform.parent != targetParent)
                go.transform.SetParent(targetParent, false);
            if (go.GetComponent<CanvasRenderer>() == null)
                go.AddComponent<CanvasRenderer>();
            if (go.GetComponent<RawImage>() == null)
                go.AddComponent<RawImage>();
        }

        RawImage image = go.GetComponent<RawImage>();
        image.color = Color.white;
        return image;
    }

    private void EnsureFixedContentView(Font font)
    {
        contentScrollArea = EnsurePanelRectObject("ContentScrollArea", contentLayer, typeof(Image));
        Image areaImage = contentScrollArea.GetComponent<Image>();
        ApplyRoundedImage(areaImage, contentViewportColor);

        ScrollRect staleScrollRect = contentScrollArea.GetComponent<ScrollRect>();
        if (staleScrollRect != null)
            staleScrollRect.enabled = false;

        Transform staleViewport = contentScrollArea.Find("Viewport");
        if (staleViewport != null)
            staleViewport.gameObject.SetActive(false);

        Transform staleScrollbar = contentScrollArea.Find("ContentScrollbar");
        if (staleScrollbar != null)
            staleScrollbar.gameObject.SetActive(false);
        contentScrollbarRect = staleScrollbar as RectTransform;
        contentScrollbar = null;

        contentText = EnsureText("FixedContent", contentScrollArea, font, "", 15, FontStyle.Normal, TextAnchor.UpperLeft);
        SetRect(contentText.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 12f), new Vector2(-16f, -12f), new Vector2(0.5f, 0.5f));

        Transform legacyContent = FindPanelChild("Content");
        if (legacyContent != null && legacyContent != contentText.transform)
        {
            if (Application.isPlaying)
                Destroy(legacyContent.gameObject);
            else
                DestroyImmediate(legacyContent.gameObject);
        }
    }

    private Scrollbar EnsureVerticalScrollbar()
    {
        contentScrollbarRect = EnsureRectObject("ContentScrollbar", contentScrollArea, typeof(Image), typeof(Scrollbar));
        SetRect(contentScrollbarRect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-10f, 0f), new Vector2(18f, -18f), new Vector2(1f, 0.5f));
        Image background = contentScrollbarRect.GetComponent<Image>();
        ApplyRoundedImage(background, new Color(1f, 0.94f, 0.78f, 0.13f));

        RectTransform slidingArea = EnsureRectObject("SlidingArea", contentScrollbarRect);
        SetRect(slidingArea, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(-6f, -6f), new Vector2(0.5f, 0.5f));

        Transform legacyHandle = contentScrollbarRect.Find("Handle");
        if (legacyHandle != null && legacyHandle.parent == contentScrollbarRect)
            legacyHandle.SetParent(slidingArea, false);

        RectTransform scrollbarHandle = EnsureRectObject("Handle", slidingArea, typeof(Image));
        SetRect(scrollbarHandle, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        Image handleImage = scrollbarHandle.GetComponent<Image>();
        ApplyRoundedImage(handleImage, new Color(1f, 0.72f, 0.36f, 0.92f));

        Scrollbar scrollbar = contentScrollbarRect.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = scrollbarHandle;
        return scrollbar;
    }

    private RectTransform EnsureRectObject(string name, Transform parent, params System.Type[] components)
    {
        Transform existing = parent.Find(name);
        GameObject go;
        if (existing == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
        }
        else
        {
            go = existing.gameObject;
        }

        foreach (System.Type componentType in components)
        {
            if (go.GetComponent(componentType) == null)
                go.AddComponent(componentType);
        }

        return go.GetComponent<RectTransform>();
    }

    private RectTransform EnsurePanelRectObject(string name, Transform parent, params System.Type[] components)
    {
        Transform targetParent = parent != null ? parent : panel.transform;
        Transform existing = FindPanelChild(name);
        GameObject go;
        if (existing == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(targetParent, false);
        }
        else
        {
            go = existing.gameObject;
            if (go.transform.parent != targetParent)
                go.transform.SetParent(targetParent, false);
            if (go.GetComponent<CanvasRenderer>() == null)
                go.AddComponent<CanvasRenderer>();
        }

        foreach (System.Type componentType in components)
        {
            if (go.GetComponent(componentType) == null)
                go.AddComponent(componentType);
        }

        return go.GetComponent<RectTransform>();
    }

    private Transform FindPanelChild(string childName)
    {
        if (panel == null || string.IsNullOrWhiteSpace(childName))
            return null;

        Transform direct = panel.transform.Find(childName);
        if (direct != null)
            return direct;

        RectTransform[] children = panel.GetComponentsInChildren<RectTransform>(includeInactive: true);
        foreach (RectTransform child in children)
        {
            if (child != null && child.name == childName)
                return child;
        }
        return null;
    }

    private void UpdatePanel(bool force)
    {
        if (panel == null)
            return;

        SetTabColor(controlsTab, page == PanelPage.Controls);
        SetTabColor(cameraTab, page == PanelPage.Camera);
        SetTabColor(attachmentTab, page == PanelPage.Attachment);
        SetTabColor(hapticsTab, page == PanelPage.Haptics);
        SetTabColor(taskSwitchTab, page == PanelPage.TaskSwitch);
        SetTabColor(debugTab, page == PanelPage.Debug);

        bool showCamera = page == PanelPage.Camera;
        bool showControls = page == PanelPage.Controls;
        bool showAttachment = page == PanelPage.Attachment;
        bool showHaptics = page == PanelPage.Haptics;
        bool showTaskSwitch = page == PanelPage.TaskSwitch;
        ApplyPageLayout(showCamera);
        if (contentScrollArea != null)
            contentScrollArea.gameObject.SetActive(true);
        if (previewImage != null)
        {
            previewImage.gameObject.SetActive(showCamera);
            if (showCamera)
                previewImage.texture = ResolvePreviewTexture();
        }
        SetButtonActive(swapHandsButton, showControls);
        SetButtonActive(resetObjectsButton, showControls);
        SetButtonActive(resetLeftRobotButton, showControls);
        SetButtonActive(resetRightRobotButton, showControls);
        SetButtonActive(overheadViewButton, showControls);
        if (recordButton != null)
            recordButton.gameObject.SetActive(showCamera);
        if (captureButton != null)
            captureButton.gameObject.SetActive(showCamera);
        SetButtonActive(leftWristCameraButton, showCamera);
        SetButtonActive(rightWristCameraButton, showCamera);
        SetButtonActive(floatingCameraButton, showCamera);
        SetButtonActive(attachmentAdjustButton, showAttachment);
        SetButtonActive(resetLeftAttachmentButton, showAttachment);
        SetButtonActive(resetRightAttachmentButton, showAttachment);
        SetButtonActive(resetAllAttachmentButton, showAttachment);
        SetButtonActive(applyLeftAttachmentButton, showAttachment);
        SetButtonActive(applyRightAttachmentButton, showAttachment);
        SetButtonActive(hapticOutputButton, showHaptics);
        SetButtonActive(hapticRosButton, showHaptics);
        SetButtonActive(hapticGainDownButton, showHaptics);
        SetButtonActive(hapticGainUpButton, showHaptics);
        SetButtonActive(hapticGainResetButton, showHaptics);
        SetButtonActive(taskPickPlaceButton, showTaskSwitch);
        SetButtonActive(taskRubikButton, showTaskSwitch);
        SetButtonActive(taskCableButton, showTaskSwitch);
        SetButtonActive(applyFloatingCameraButton, showCamera && cameraPreviewMode == CameraPreviewMode.Floating);
        SetInputActive(leftAttachmentPosInput, showAttachment);
        SetInputActive(leftAttachmentRotInput, showAttachment);
        SetInputActive(rightAttachmentPosInput, showAttachment);
        SetInputActive(rightAttachmentRotInput, showAttachment);
        SetInputActive(floatingCameraPosInput, showCamera && cameraPreviewMode == CameraPreviewMode.Floating);
        SetInputActive(floatingCameraRotInput, showCamera && cameraPreviewMode == CameraPreviewMode.Floating);
        SetInputActive(floatingCameraFovInput, showCamera && cameraPreviewMode == CameraPreviewMode.Floating);

        if (swapHandsButtonText != null)
            swapHandsButtonText.text = sender != null && sender.swapControllerArmControl ? "Hands Swapped" : "Swap Hands";
        if (recordButtonText != null)
            recordButtonText.text = SelectedRecorder() != null && SelectedRecorder().IsRecording ? "Stop Rec" : "Start Rec";
        Text attachmentAdjustText = attachmentAdjustButton != null ? attachmentAdjustButton.GetComponentInChildren<Text>() : null;
        if (attachmentAdjustText != null)
            attachmentAdjustText.text = "Hold X/A";
        Text hapticOutputText = hapticOutputButton != null ? hapticOutputButton.GetComponentInChildren<Text>() : null;
        if (hapticOutputText != null)
            hapticOutputText.text = haptics != null && haptics.hapticOutputEnabled ? "Haptics ON" : "Haptics OFF";
        Text hapticRosText = hapticRosButton != null ? hapticRosButton.GetComponentInChildren<Text>() : null;
        if (hapticRosText != null)
            hapticRosText.text = haptics != null && haptics.enableRosContactHaptics ? "ROS Contact ON" : "ROS Contact OFF";

        StyleButton(swapHandsButton, sender != null && sender.swapControllerArmControl ? activeTabColor : actionButtonColor, buttonTextColor);
        StyleButton(resetObjectsButton, resetButtonColor, buttonTextColor);
        StyleButton(resetLeftRobotButton, resetButtonColor, buttonTextColor);
        StyleButton(resetRightRobotButton, resetButtonColor, buttonTextColor);
        StyleButton(overheadViewButton, actionButtonColor, buttonTextColor);
        StyleButton(recordButton, SelectedRecorder() != null && SelectedRecorder().IsRecording ? recordActiveButtonColor : cameraButtonColor, buttonTextColor);
        StyleButton(captureButton, cameraButtonColor, buttonTextColor);
        StyleButton(leftWristCameraButton, cameraPreviewMode == CameraPreviewMode.LeftWrist ? activeTabColor : cameraButtonColor, buttonTextColor);
        StyleButton(rightWristCameraButton, cameraPreviewMode == CameraPreviewMode.RightWrist ? activeTabColor : cameraButtonColor, buttonTextColor);
        StyleButton(floatingCameraButton, cameraPreviewMode == CameraPreviewMode.Floating ? activeTabColor : cameraButtonColor, buttonTextColor);
        StyleButton(attachmentAdjustButton, actionButtonColor, buttonTextColor);
        StyleButton(resetLeftAttachmentButton, resetButtonColor, buttonTextColor);
        StyleButton(resetRightAttachmentButton, resetButtonColor, buttonTextColor);
        StyleButton(resetAllAttachmentButton, resetButtonColor, buttonTextColor);
        StyleButton(applyLeftAttachmentButton, actionButtonColor, buttonTextColor);
        StyleButton(applyRightAttachmentButton, actionButtonColor, buttonTextColor);
        StyleButton(hapticOutputButton, haptics != null && haptics.hapticOutputEnabled ? activeTabColor : resetButtonColor, buttonTextColor);
        StyleButton(hapticRosButton, haptics != null && haptics.enableRosContactHaptics ? activeTabColor : resetButtonColor, buttonTextColor);
        StyleButton(hapticGainDownButton, actionButtonColor, buttonTextColor);
        StyleButton(hapticGainUpButton, actionButtonColor, buttonTextColor);
        StyleButton(hapticGainResetButton, actionButtonColor, buttonTextColor);
        StyleButton(taskPickPlaceButton, actionButtonColor, buttonTextColor);
        StyleButton(taskRubikButton, actionButtonColor, buttonTextColor);
        StyleButton(taskCableButton, actionButtonColor, buttonTextColor);
        StyleButton(applyFloatingCameraButton, actionButtonColor, buttonTextColor);
        RefreshInputValuesIfIdle();

        if (contentText != null)
            contentText.text = BuildContentText();
        RefreshFixedContent();

        if (statusText != null)
            statusText.text = BuildStatusText();

        UpdateCornerHandleVisibility();
        LastStatus = $"page={page}, camera={cameraPreviewMode}, recorder={(SelectedRecorder() != null ? "ok" : "missing")}, sender={(sender != null ? "ok" : "missing")}";
    }

    private string BuildContentText()
    {
        if (page == PanelPage.Camera)
        {
            GripperCameraRecorder activeRecorder = SelectedRecorder();
            string session = activeRecorder != null && !string.IsNullOrWhiteSpace(activeRecorder.CurrentSessionFolder)
                ? activeRecorder.CurrentSessionFolder
                : "No recording session yet.";
            string leftStatus = leftRecorder != null ? "left wrist: ready" : "left wrist: missing";
            string rightStatus = rightRecorder != null ? "right wrist: ready" : "right wrist: missing";
            GripperCameraRecorder floatingRecorder = Application.isPlaying && floatingCamera != null ? floatingCamera.Recorder : null;
            string floatingRecorderStatus = Application.isPlaying
                ? (floatingRecorder != null ? "ready" : "missing")
                : (floatingCamera != null && floatingCamera.enableRecording ? "runtime-created" : "disabled");
            string floating = floatingCamera != null
                ? $"floating: ready, recorder={floatingRecorderStatus}"
                : "floating camera: missing";
            return "CAMERAS\n\n" +
                "Select Left Wrist, Right Wrist, or Floating.\n" +
                "Record/Capture uses the selected camera.\n\n" +
                leftStatus + "\n" +
                rightStatus + "\n" +
                floating + "\n\n" +
                "Floating: trigger-drag camera body. Use color rings to rotate. Type pos/rot/FOV, then Apply.\n\n" +
                "Last session:\n" + session;
        }

        if (page == PanelPage.Attachment)
        {
            string attach = sender != null ? sender.AttachmentOffsetStatus : "HandPoseSender: missing";
            return "ATTACHMENT\n\n" +
                "Y/B toggles attachment per arm.\n" +
                "Grip still engages teleop.\n" +
                "Hold X/A while attached to freeze, move controller to desired gripper offset, release to save.\n\n" +
                "Manual fields: pos x,y,z meters; rot x,y,z degrees.\n" +
                "Reset buttons clear saved headset offsets.\n\n" +
                attach;
        }

        if (page == PanelPage.Haptics)
        {
            string hp = haptics != null ? haptics.LastStatus : "QuestHapticFeedbackController: missing";
            return "HAPTICS\n\n" +
                "Default: Gazebo contact pulse only.\n" +
                "Two short taps when both gripper sides contact/pinch.\n" +
                "No continuous buzz during hold.\n" +
                "EE-error vibration should stay off for normal use.\n\n" +
                "Use buttons to toggle output, ROS contact haptics, and gain.\n\n" +
                hp;
        }

        if (page == PanelPage.Debug)
        {
            string senderText = sender != null ? sender.LastStatus : "HandPoseSender: missing";
            string workspaceText = workspaceDrag != null ? workspaceDrag.LastStatus : "WorkspaceDragController: missing";
            string passText = passthrough != null ? passthrough.LastStatus : "Passthrough: missing";
            string hapticText = haptics != null ? haptics.LastStatus : "Haptics: missing";
            string recorderText = SelectedRecorder() != null ? $"Recorder {cameraPreviewMode}: {(SelectedRecorder().IsRecording ? "REC" : "idle")}" : $"Recorder {cameraPreviewMode}: missing";
            string panelDragText = panelDragger != null ? $"Panel drag: {panelDragger.LastStatus}" : "Panel drag: missing";
            return "DEBUG\n\n" +
                "Control mode: terminal-controlled backend setting\n" +
                senderText + "\n" +
                workspaceText + "\n" +
                panelDragText + "\n" +
                hapticText + "\n" +
                passText + "\n" +
                recorderText;
        }

        if (page == PanelPage.TaskSwitch)
        {
            return "TASKS\n\n" +
                "Buttons publish to /task_manager/select_task.\n" +
                "Runtime manager hot-swaps Gazebo task models and tells Unity to rebuild TaskGroup_Main.\n\n" +
                "For stable demos, current Pick/Place already includes cable insertion objects.\n\n" +
                taskManagerStatus;
        }

        return
            "CONTROLS\n\n" +
            "Grip hold: engage that arm.\n" +
            "Trigger tap while gripping: open/close that gripper.\n" +
            "X/A hold: rotation mode for left/right arm.\n" +
            "Y/B tap: attachment mode for left/right arm.\n" +
            "Trigger while not gripping: use workspace/panel handles.\n\n" +
            "Optional keyboard/thumbstick modes are terminal commands.\n" +
            "Swap Hands: exchange controller-to-arm assignment.\n" +
            "Reset buttons: left arm, right arm, objects, or overhead workspace view.\n\n" +
            "Drag panel from bottom notch; resize from curved corner notches.";
    }

    private string BuildStatusText()
    {
        string teleop = sender != null
            ? $"L {(sender.IsLeftTeleopHeld ? "on" : "off")} / R {(sender.IsRightTeleopHeld ? "on" : "off")}"
            : "teleop ?";
        string attach = sender != null
            ? $"attach L {(sender.IsLeftAttachmentModeActive ? "on" : "off")} / R {(sender.IsRightAttachmentModeActive ? "on" : "off")}"
            : "attach ?";
        string assignment = sender != null ? sender.ControllerArmAssignmentLabel : "hands ?";
        GripperCameraRecorder activeRecorder = SelectedRecorder();
        string rec = activeRecorder != null && activeRecorder.IsRecording ? "REC" : "idle";
        return $"{assignment} | {teleop} | {attach} | {cameraPreviewMode} {rec} | page {page}";
    }

    private void HandleControllerClicks()
    {
        ResolveControllerTransforms();
        if (leftController == null && rightController == null)
            return;

        bool gripHeld =
            GetGripValue(VRDraggableWindow.DragController.Right) >= rightGripBlockThreshold ||
            GetGripValue(VRDraggableWindow.DragController.Left) >= rightGripBlockThreshold;
        bool leftTriggerHeld = GetTriggerValue(VRDraggableWindow.DragController.Left) >= triggerThreshold;
        bool rightTriggerHeld = GetTriggerValue(VRDraggableWindow.DragController.Right) >= triggerThreshold;
        bool leftTriggerDown = leftTriggerHeld && !leftTriggerWasHeld;
        bool rightTriggerDown = rightTriggerHeld && !rightTriggerWasHeld;
        leftTriggerWasHeld = leftTriggerHeld;
        rightTriggerWasHeld = rightTriggerHeld;

        if (gripHeld)
        {
            EndContentScroll();
            return;
        }

        if (resizeDragging)
        {
            bool activeTriggerHeld =
                (resizeController == leftController && leftTriggerHeld) ||
                (resizeController == rightController && rightTriggerHeld);
            if (activeTriggerHeld)
                UpdateResizeDrag(resizeController);
            else
                EndResizeDrag();
            return;
        }

        if (leftTriggerDown && TryHandlePanelClick(leftController))
            return;
        if (leftTriggerDown && TryBeginResizeDrag(leftController))
            return;
        if (rightTriggerDown)
        {
            if (TryHandlePanelClick(rightController))
                return;
            if (TryBeginResizeDrag(rightController))
                return;
        }
    }

    private bool TryHandlePanelClick(Transform controller)
    {
        if (controller == null)
            return false;

        Ray ray = new Ray(controller.position, controller.forward);
        if (RectRayHit(controlsTab, ray))
        {
            SetPage(PanelPage.Controls);
            return true;
        }
        else if (RectRayHit(cameraTab, ray))
        {
            SetPage(PanelPage.Camera);
            return true;
        }
        else if (RectRayHit(attachmentTab, ray))
        {
            SetPage(PanelPage.Attachment);
            return true;
        }
        else if (RectRayHit(hapticsTab, ray))
        {
            SetPage(PanelPage.Haptics);
            return true;
        }
        else if (RectRayHit(taskSwitchTab, ray))
        {
            SetPage(PanelPage.TaskSwitch);
            return true;
        }
        else if (RectRayHit(debugTab, ray))
        {
            SetPage(PanelPage.Debug);
            return true;
        }
        else if (page == PanelPage.Controls && RectRayHit(swapHandsButton, ray) && sender != null)
        {
            sender.ToggleControllerArmSwap();
            return true;
        }
        else if (page == PanelPage.Controls && RectRayHit(resetObjectsButton, ray) && sender != null)
        {
            sender.RequestResetObjects();
            return true;
        }
        else if (page == PanelPage.Controls && RectRayHit(resetLeftRobotButton, ray) && sender != null)
        {
            sender.RequestResetLeftRobot();
            return true;
        }
        else if (page == PanelPage.Controls && RectRayHit(resetRightRobotButton, ray) && sender != null)
        {
            sender.RequestResetRightRobot();
            return true;
        }
        else if (page == PanelPage.Controls && RectRayHit(overheadViewButton, ray))
        {
            ResetOverheadView();
            return true;
        }
        else if (page == PanelPage.Camera && RectRayHit(recordButton, ray))
        {
            ToggleSelectedRecording();
            return true;
        }
        else if (page == PanelPage.Camera && RectRayHit(captureButton, ray))
        {
            CaptureSelectedCameraFrame();
            return true;
        }
        else if (page == PanelPage.Camera && RectRayHit(leftWristCameraButton, ray))
        {
            SelectCameraPreview(CameraPreviewMode.LeftWrist);
            return true;
        }
        else if (page == PanelPage.Camera && RectRayHit(rightWristCameraButton, ray))
        {
            SelectCameraPreview(CameraPreviewMode.RightWrist);
            return true;
        }
        else if (page == PanelPage.Camera && RectRayHit(floatingCameraButton, ray))
        {
            SelectCameraPreview(CameraPreviewMode.Floating);
            return true;
        }
        else if (page == PanelPage.Camera && RectRayHit(applyFloatingCameraButton, ray))
        {
            ApplyFloatingCameraInputs();
            return true;
        }
        else if (page == PanelPage.Camera && TryActivateInputField(ray, floatingCameraPosInput))
        {
            return true;
        }
        else if (page == PanelPage.Camera && TryActivateInputField(ray, floatingCameraRotInput))
        {
            return true;
        }
        else if (page == PanelPage.Camera && TryActivateInputField(ray, floatingCameraFovInput))
        {
            return true;
        }
        else if (page == PanelPage.Attachment && RectRayHit(attachmentAdjustButton, ray) && sender != null)
        {
            sender.SetAttachmentAdjustmentMode(false);
            return true;
        }
        else if (page == PanelPage.Attachment && RectRayHit(resetLeftAttachmentButton, ray) && sender != null)
        {
            sender.ResetLeftAttachmentOffset();
            return true;
        }
        else if (page == PanelPage.Attachment && RectRayHit(resetRightAttachmentButton, ray) && sender != null)
        {
            sender.ResetRightAttachmentOffset();
            return true;
        }
        else if (page == PanelPage.Attachment && RectRayHit(resetAllAttachmentButton, ray) && sender != null)
        {
            sender.ResetAttachmentOffsets();
            return true;
        }
        else if (page == PanelPage.Attachment && RectRayHit(applyLeftAttachmentButton, ray))
        {
            ApplyAttachmentInputs(left: true);
            return true;
        }
        else if (page == PanelPage.Attachment && RectRayHit(applyRightAttachmentButton, ray))
        {
            ApplyAttachmentInputs(left: false);
            return true;
        }
        else if (page == PanelPage.Attachment && TryActivateInputField(ray, leftAttachmentPosInput))
        {
            return true;
        }
        else if (page == PanelPage.Attachment && TryActivateInputField(ray, leftAttachmentRotInput))
        {
            return true;
        }
        else if (page == PanelPage.Attachment && TryActivateInputField(ray, rightAttachmentPosInput))
        {
            return true;
        }
        else if (page == PanelPage.Attachment && TryActivateInputField(ray, rightAttachmentRotInput))
        {
            return true;
        }
        else if (page == PanelPage.Haptics && RectRayHit(hapticOutputButton, ray) && haptics != null)
        {
            haptics.ToggleHapticOutput();
            return true;
        }
        else if (page == PanelPage.Haptics && RectRayHit(hapticRosButton, ray) && haptics != null)
        {
            haptics.ToggleRosContactHaptics();
            return true;
        }
        else if (page == PanelPage.Haptics && RectRayHit(hapticGainDownButton, ray) && haptics != null)
        {
            haptics.AdjustOutputGain(-0.1f);
            return true;
        }
        else if (page == PanelPage.Haptics && RectRayHit(hapticGainUpButton, ray) && haptics != null)
        {
            haptics.AdjustOutputGain(0.1f);
            return true;
        }
        else if (page == PanelPage.Haptics && RectRayHit(hapticGainResetButton, ray) && haptics != null)
        {
            haptics.ResetOutputGain();
            return true;
        }
        else if (page == PanelPage.TaskSwitch && RectRayHit(taskPickPlaceButton, ray))
        {
            PublishTaskSelection("pick_place_basic");
            return true;
        }
        else if (page == PanelPage.TaskSwitch && RectRayHit(taskRubikButton, ray))
        {
            PublishTaskSelection("rubik_2x2");
            return true;
        }
        else if (page == PanelPage.TaskSwitch && RectRayHit(taskCableButton, ray))
        {
            PublishTaskSelection("cable_insertion");
            return true;
        }

        return false;
    }

    private void SetPage(PanelPage nextPage)
    {
        if (page == nextPage)
            return;
        page = nextPage;
    }

    private void SelectCameraPreview(CameraPreviewMode nextMode)
    {
        cameraPreviewMode = nextMode;
        ResetContentScrollToTop();
        LastStatus = $"camera preview selected: {cameraPreviewMode}";
    }

    private void ToggleSelectedRecording()
    {
        GripperCameraRecorder activeRecorder = SelectedRecorder();
        if (activeRecorder == null)
        {
            LastStatus = $"record ignored: no recorder found for {cameraPreviewMode}.";
            return;
        }

        activeRecorder.ToggleRecording();
        LastStatus = activeRecorder.IsRecording
            ? $"recording started: {cameraPreviewMode}"
            : $"recording stopped: {cameraPreviewMode}";
    }

    private void CaptureSelectedCameraFrame()
    {
        GripperCameraRecorder activeRecorder = SelectedRecorder();
        if (activeRecorder == null)
        {
            LastStatus = $"capture ignored: no recorder found for {cameraPreviewMode}.";
            return;
        }

        activeRecorder.CaptureOneFrame();
        LastStatus = $"captured frame: {cameraPreviewMode}";
    }

    private void ResetContentScrollToTop()
    {
    }

    private void SetButtonActive(RectTransform button, bool active)
    {
        if (button != null)
            button.gameObject.SetActive(active);
    }

    private void SetInputActive(InputField input, bool active)
    {
        if (input != null)
            input.gameObject.SetActive(active);
    }

    private void ApplyPageLayout(bool showCamera)
    {
        ApplyActionLayout(showCamera);

        if (contentScrollArea != null)
        {
            float contentTop;
            const float contentBottomReserve = 112f;
            if (showCamera)
            {
                bool floating = cameraPreviewMode == CameraPreviewMode.Floating;
                contentTop = floating ? 472f : 374f;
            }
            else if (page == PanelPage.Attachment)
            {
                contentTop = 262f;
            }
            else if (page == PanelPage.Controls)
            {
                contentTop = 172f;
            }
            else if (page == PanelPage.Debug)
            {
                contentTop = 86f;
            }
            else
            {
                contentTop = 138f;
            }
            LayoutContentArea(contentTop, contentBottomReserve);
        }

        if (previewImage != null)
            SetRect(previewImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -252f), CameraPreviewSize(), new Vector2(0.5f, 0.5f));
    }

    private void LayoutContentArea(float topPixels, float bottomReservePixels)
    {
        float height = Mathf.Max(56f, panelSize.y - topPixels - bottomReservePixels);
        SetRect(
            contentScrollArea,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(24f, -topPixels),
            new Vector2(-48f, height),
            new Vector2(0f, 1f));
    }

    private void ApplyActionLayout(bool showCamera)
    {
        if (page == PanelPage.Controls)
        {
            LayoutRow(
                new[] { swapHandsButton, resetLeftRobotButton, resetRightRobotButton, resetObjectsButton, overheadViewButton },
                new[] { 150f, 122f, 122f, 122f, 132f },
                -88f,
                38f,
                12f);
        }
        else if (showCamera)
        {
            LayoutRow(
                new[] { leftWristCameraButton, rightWristCameraButton, floatingCameraButton, recordButton, captureButton },
                new[] { 122f, 130f, 124f, 122f, 112f },
                -88f,
                38f,
                10f);

            if (cameraPreviewMode == CameraPreviewMode.Floating)
            {
                SetInputTransform(floatingCameraPosInput, new Vector2(180f, -374f), new Vector2(292f, 32f));
                SetInputTransform(floatingCameraRotInput, new Vector2(486f, -374f), new Vector2(292f, 32f));
                SetInputTransform(floatingCameraFovInput, new Vector2(430f, -418f), new Vector2(86f, 32f));
                SetButtonTransform(applyFloatingCameraButton, new Vector2(610f, -418f), new Vector2(170f, 34f));
            }
        }
        else if (page == PanelPage.Attachment)
        {
            LayoutRow(
                new[] { attachmentAdjustButton, resetLeftAttachmentButton, resetRightAttachmentButton, resetAllAttachmentButton },
                new[] { 210f, 112f, 112f, 152f },
                -88f,
                38f,
                10f);
            SetInputTransform(leftAttachmentPosInput, new Vector2(178f, -142f), new Vector2(280f, 32f));
            SetInputTransform(leftAttachmentRotInput, new Vector2(474f, -142f), new Vector2(280f, 32f));
            SetButtonTransform(applyLeftAttachmentButton, new Vector2(684f, -184f), new Vector2(112f, 34f));
            SetInputTransform(rightAttachmentPosInput, new Vector2(178f, -184f), new Vector2(280f, 32f));
            SetInputTransform(rightAttachmentRotInput, new Vector2(474f, -184f), new Vector2(280f, 32f));
            SetButtonTransform(applyRightAttachmentButton, new Vector2(684f, -226f), new Vector2(112f, 34f));
        }
        else if (page == PanelPage.Haptics)
        {
            LayoutRow(
                new[] { hapticOutputButton, hapticRosButton, hapticGainDownButton, hapticGainUpButton, hapticGainResetButton },
                new[] { 150f, 170f, 110f, 110f, 120f },
                -88f,
                38f,
                10f);
        }
        else if (page == PanelPage.TaskSwitch)
        {
            LayoutRow(
                new[] { taskPickPlaceButton, taskRubikButton, taskCableButton },
                new[] { 180f, 180f, 190f },
                -88f,
                38f,
                14f);
        }
    }

    private void LayoutRow(RectTransform[] rects, float[] widths, float y, float height, float gap)
    {
        if (rects == null || widths == null || rects.Length != widths.Length)
            return;

        float totalWidth = Mathf.Max(0f, gap * Mathf.Max(0, rects.Length - 1));
        for (int i = 0; i < widths.Length; i++)
            totalWidth += widths[i];

        float x = (panelSize.x - totalWidth) * 0.5f;
        for (int i = 0; i < rects.Length; i++)
        {
            SetButtonTransform(rects[i], new Vector2(x + widths[i] * 0.5f, y), new Vector2(widths[i], height));
            x += widths[i] + gap;
        }
    }

    private static void SetButtonTransform(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        if (rect == null)
            return;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void SetInputTransform(InputField input, Vector2 anchoredPosition, Vector2 size)
    {
        if (input == null)
            return;
        RectTransform rect = input.GetComponent<RectTransform>();
        if (rect == null)
            return;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private void RefreshFixedContent()
    {
        if (contentText == null || contentScrollArea == null)
            return;
        if (!contentScrollArea.gameObject.activeInHierarchy)
            return;

        Canvas.ForceUpdateCanvases();
        SetRect(contentText.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 12f), new Vector2(-16f, -12f), new Vector2(0.5f, 0.5f));

        if (contentScrollbarRect != null)
            contentScrollbarRect.gameObject.SetActive(false);
        if (contentScrollbar != null)
            contentScrollbar.interactable = false;
    }

    private Vector2 CameraPreviewSize()
    {
        return new Vector2(
            Mathf.Min(previewSize.x, panelSize.x - 80f),
            Mathf.Min(previewSize.y, 190f));
    }

    private Texture ResolvePreviewTexture()
    {
        if (cameraPreviewMode == CameraPreviewMode.Floating)
            return floatingCamera != null ? floatingCamera.PreviewTexture : null;

        GripperCameraRecorder activeRecorder = SelectedRecorder();
        return activeRecorder != null ? activeRecorder.PreviewTexture : null;
    }

    private GripperCameraRecorder SelectedRecorder()
    {
        if (cameraPreviewMode == CameraPreviewMode.LeftWrist)
            return leftRecorder != null ? leftRecorder : recorder;
        if (cameraPreviewMode == CameraPreviewMode.RightWrist)
            return rightRecorder != null ? rightRecorder : recorder;
        if (cameraPreviewMode == CameraPreviewMode.Floating)
            return floatingCamera != null ? floatingCamera.Recorder : null;
        return recorder;
    }

    private void RefreshInputValuesIfIdle()
    {
        if (sender != null)
        {
            SetInputTextIfNotFocused(leftAttachmentPosInput, FormatVector(sender.leftAttachmentPositionOffset));
            SetInputTextIfNotFocused(leftAttachmentRotInput, FormatVector(sender.leftAttachmentRotationOffsetEuler));
            SetInputTextIfNotFocused(rightAttachmentPosInput, FormatVector(sender.rightAttachmentPositionOffset));
            SetInputTextIfNotFocused(rightAttachmentRotInput, FormatVector(sender.rightAttachmentRotationOffsetEuler));
        }

        if (floatingCamera != null)
        {
            SetInputTextIfNotFocused(floatingCameraPosInput, FormatVector(floatingCamera.LocalPosition));
            SetInputTextIfNotFocused(floatingCameraRotInput, FormatVector(floatingCamera.LocalEuler));
            SetInputTextIfNotFocused(floatingCameraFovInput, floatingCamera.fieldOfView.ToString("F1", CultureInfo.InvariantCulture));
        }
    }

    private static void SetInputTextIfNotFocused(InputField field, string value)
    {
        if (field == null || field.isFocused)
            return;
        field.text = value;
    }

    private void ApplyAttachmentInputs(bool left)
    {
        if (sender == null)
            return;

        InputField posField = left ? leftAttachmentPosInput : rightAttachmentPosInput;
        InputField rotField = left ? leftAttachmentRotInput : rightAttachmentRotInput;
        Vector3 currentPos = left ? sender.leftAttachmentPositionOffset : sender.rightAttachmentPositionOffset;
        Vector3 currentRot = left ? sender.leftAttachmentRotationOffsetEuler : sender.rightAttachmentRotationOffsetEuler;
        Vector3 pos = ParseVector3(posField != null ? posField.text : "", currentPos);
        Vector3 rot = ParseVector3(rotField != null ? rotField.text : "", currentRot);

        if (left)
            sender.SetLeftAttachmentOffset(pos, rot);
        else
            sender.SetRightAttachmentOffset(pos, rot);
    }

    private void ApplyFloatingCameraInputs()
    {
        if (floatingCamera == null)
            return;

        Vector3 pos = ParseVector3(floatingCameraPosInput != null ? floatingCameraPosInput.text : "", floatingCamera.LocalPosition);
        Vector3 rot = ParseVector3(floatingCameraRotInput != null ? floatingCameraRotInput.text : "", floatingCamera.LocalEuler);
        float fov = ParseFloat(floatingCameraFovInput != null ? floatingCameraFovInput.text : "", floatingCamera.fieldOfView);
        floatingCamera.ApplyLocalPose(pos, rot, fov);
    }

    private bool TryActivateInputField(Ray ray, InputField field)
    {
        if (field == null || !field.gameObject.activeInHierarchy)
            return false;
        RectTransform rect = field.GetComponent<RectTransform>();
        if (!RectRayHit(rect, ray))
            return false;
        field.ActivateInputField();
        return true;
    }

    private static Vector3 ParseVector3(string raw, Vector3 fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        string[] parts = raw.Replace("(", "").Replace(")", "").Split(',');
        if (parts.Length != 3)
            return fallback;

        return new Vector3(
            ParseFloat(parts[0], fallback.x),
            ParseFloat(parts[1], fallback.y),
            ParseFloat(parts[2], fallback.z));
    }

    private static float ParseFloat(string raw, float fallback)
    {
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            return value;
        return fallback;
    }

    private static string FormatVector(Vector3 value)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:F3},{1:F3},{2:F3}", value.x, value.y, value.z);
    }

    private void SetChildActive(string childName, bool active)
    {
        if (panel == null)
            return;
        Transform child = FindPanelChild(childName);
        if (child != null)
            child.gameObject.SetActive(active);
    }

    private void RemovePanelChildIfExists(string childName)
    {
        if (panel == null)
            return;

        Transform child = FindPanelChild(childName);
        if (child == null)
            return;

        if (Application.isPlaying)
            Destroy(child.gameObject);
        else
            DestroyImmediate(child.gameObject);
    }

    private void ResetWorkspaceUnderHeadset()
    {
        Transform workspace = ResolveWorkspaceRoot();
        Transform headset = ResolveHeadsetTransform();
        if (workspace == null || headset == null)
        {
            LastStatus = $"workspace reset failed: workspace={(workspace ? workspace.name : "NULL")}, headset={(headset ? headset.name : "NULL")}";
            return;
        }

        Vector3 targetPosition = headset.position + workspaceResetOffsetFromHeadset;
        targetPosition.y = workspaceResetHeight;
        Quaternion targetRotation = ComputeWorkspaceResetRotation(headset, workspace);

        if (workspaceDrag != null)
        {
            workspaceDrag.SetWorkspacePose(targetPosition, targetRotation);
        }
        else
        {
            workspace.SetPositionAndRotation(targetPosition, targetRotation);
        }

        LastStatus = $"workspace reset to {targetPosition}, yaw={targetRotation.eulerAngles.y:F1}, headset={headset.name}";
    }

    private void ResetOverheadView()
    {
        ResetWorkspaceUnderHeadset();
        LastStatus = $"overhead view reset only; robots/task reset not requested. {LastStatus}";
    }

    private Quaternion ComputeWorkspaceResetRotation(Transform headset, Transform workspace)
    {
        if (!workspaceResetYawToCurrentHeadset || headset == null)
            return Quaternion.Euler(0f, workspace != null ? workspace.eulerAngles.y : 0f, 0f);

        Vector3 horizontalForward = Vector3.ProjectOnPlane(headset.forward, Vector3.up);
        if (robustHeadsetYawForWorkspaceReset && horizontalForward.sqrMagnitude < 0.0025f)
        {
            Vector3 horizontalUp = Vector3.ProjectOnPlane(headset.up, Vector3.up);
            if (headset.forward.y > 0f)
                horizontalUp = -horizontalUp;
            if (horizontalUp.sqrMagnitude >= 0.000001f)
                horizontalForward = horizontalUp;
        }

        if (robustHeadsetYawForWorkspaceReset && horizontalForward.sqrMagnitude < 0.000001f)
        {
            Vector3 horizontalRight = Vector3.ProjectOnPlane(headset.right, Vector3.up);
            if (horizontalRight.sqrMagnitude >= 0.000001f)
                horizontalForward = Vector3.Cross(horizontalRight.normalized, Vector3.up);
        }

        if (horizontalForward.sqrMagnitude < 0.000001f)
        {
            float fallbackYaw = workspace != null ? workspace.eulerAngles.y : 0f;
            return Quaternion.Euler(0f, fallbackYaw, 0f);
        }

        return Quaternion.LookRotation(horizontalForward.normalized, Vector3.up);
    }

    private Transform ResolveWorkspaceRoot()
    {
        if (workspaceDrag != null && workspaceDrag.workspaceRoot != null)
            return workspaceDrag.workspaceRoot;
        GameObject go = GameObject.Find(workspaceRootName);
        return go != null ? go.transform : null;
    }

    private Transform ResolveHeadsetTransform()
    {
        GameObject centerEye =
            GameObject.Find("OVRCameraRig/TrackingSpace/CenterEyeAnchor") ??
            (!string.IsNullOrWhiteSpace(headsetName) ? GameObject.Find(headsetName) : null) ??
            GameObject.Find("CenterEyeAnchor") ??
            GameObject.Find("Main Camera");
        if (centerEye != null)
            return centerEye.transform;
        if (Camera.main != null)
            return Camera.main.transform;
        return null;
    }

    private bool RectRayHit(RectTransform rect, Ray ray)
    {
        return TryRectRayLocalPoint(rect, ray, out _);
    }

    private bool TryRectRayLocalPoint(RectTransform rect, Ray ray, out Vector2 localPoint)
    {
        if (!TryRectRayPlaneLocalPoint(rect, ray, out localPoint))
            return false;
        return rect.rect.Contains(localPoint);
    }

    private bool TryRectRayPlaneLocalPoint(RectTransform rect, Ray ray, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        if (rect == null || !rect.gameObject.activeInHierarchy)
            return false;

        Plane plane = new Plane(rect.forward, rect.position);
        if (!plane.Raycast(ray, out float distance))
            return false;
        if (distance < 0f || distance > Mathf.Max(0.05f, rayMaxDistance))
            return false;

        Vector3 point = ray.GetPoint(distance);
        Vector3 local = rect.InverseTransformPoint(point);
        localPoint = new Vector2(local.x, local.y);
        return true;
    }

    private void EndContentScroll()
    {
    }

    private bool TryBeginResizeDrag(Transform controller)
    {
        if (controller == null || resizeHandles == null)
            return false;

        Ray ray = new Ray(controller.position, controller.forward);
        RectTransform hitHandle = null;
        UICornerDragHandle.Corner hitCorner = UICornerDragHandle.Corner.BottomLeft;
        for (int i = 0; i < resizeHandles.Length; i++)
        {
            RectTransform candidate = resizeHandles[i];
            if (candidate == null)
                continue;
            if (TryRectRayLocalPoint(candidate, ray, out _))
            {
                hitHandle = candidate;
                hitCorner = (UICornerDragHandle.Corner)i;
                break;
            }
        }
        if (hitHandle == null)
            return false;
        if (!TryRectRayPlaneLocalPoint(panelRect, ray, out Vector2 localPoint))
            return false;

        resizeDragging = true;
        resizeController = controller;
        resizeStartLocalPoint = localPoint;
        resizeStartSize = panelSize;
        activeResizeCorner = hitCorner;
        if (panelDragger != null)
            panelDragger.enabled = false;
        return true;
    }

    private void UpdateResizeDrag(Transform controller)
    {
        if (controller == null || panelRect == null)
            return;

        Ray ray = new Ray(controller.position, controller.forward);
        if (!TryRectRayPlaneLocalPoint(panelRect, ray, out Vector2 localPoint))
            return;

        Vector2 delta = localPoint - resizeStartLocalPoint;
        bool leftCorner = activeResizeCorner == UICornerDragHandle.Corner.BottomLeft ||
            activeResizeCorner == UICornerDragHandle.Corner.TopLeft;
        bool bottomCorner = activeResizeCorner == UICornerDragHandle.Corner.BottomLeft ||
            activeResizeCorner == UICornerDragHandle.Corner.BottomRight;
        panelSize = new Vector2(
            Mathf.Clamp(resizeStartSize.x + (leftCorner ? -delta.x : delta.x), 440f, 920f),
            Mathf.Clamp(resizeStartSize.y + (bottomCorner ? -delta.y : delta.y), 330f, 720f));
        panelRect.sizeDelta = panelSize;
        RefreshFixedContent();
    }

    private void EndResizeDrag()
    {
        resizeDragging = false;
        resizeController = null;
        if (panelDragger != null)
            panelDragger.enabled = true;
    }

    private void UpdateCornerHandleVisibility()
    {
        Color resizeColor = ResizeHandleColor();
        if (dragHandle != null)
        {
            Image image = dragHandle.GetComponent<Image>();
            if (image != null)
                ApplyRoundedImage(image, dragHandleColor);
        }

        for (int i = 0; i < resizeHandles.Length; i++)
        {
            RectTransform handle = resizeHandles[i];
            bool visible = handle != null && (IsAnyControllerRayHitting(handle) || (resizeDragging && (int)activeResizeCorner == i));
            UICornerDragHandle.SetVisible(handle, visible, resizeColor);
        }
    }

    private static Color ResizeHandleColor()
    {
        return new Color(0.68f, 0.68f, 0.68f, 0.56f);
    }

    private bool IsAnyControllerRayHitting(RectTransform rect)
    {
        if (rect == null)
            return false;
        if (leftController != null && TryRectRayLocalPoint(rect, new Ray(leftController.position, leftController.forward), out _))
            return true;
        if (rightController != null && TryRectRayLocalPoint(rect, new Ray(rightController.position, rightController.forward), out _))
            return true;
        return false;
    }

    private static float GetTriggerValue(VRDraggableWindow.DragController controllerSide)
    {
        if (controllerSide == VRDraggableWindow.DragController.Left)
        {
            return Mathf.Max(
                OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch),
                OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.Touch));
        }

        return Mathf.Max(
            OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch),
            OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger, OVRInput.Controller.Touch));
    }

    private static float GetGripValue(VRDraggableWindow.DragController controllerSide)
    {
        if (controllerSide == VRDraggableWindow.DragController.Left)
        {
            return Mathf.Max(
                OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch),
                OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.Touch));
        }

        return Mathf.Max(
            OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch),
            OVRInput.Get(OVRInput.Axis1D.SecondaryHandTrigger, OVRInput.Controller.Touch));
    }

    private void ResolveReferences()
    {
        if (sender == null)
            sender = FindAny<HandPoseSender>();
        if (workspaceDrag == null)
            workspaceDrag = WorkspaceDragController.ActiveInstance ?? FindAny<WorkspaceDragController>();
        ResolveWristRecorders();
        if (floatingCamera == null)
            floatingCamera = FindAny<FloatingSceneCameraController>();
        if (passthrough == null)
            passthrough = QuestPassthroughController.ActiveInstance ?? FindAny<QuestPassthroughController>();
        if (haptics == null)
            haptics = QuestHapticFeedbackController.ActiveInstance ?? FindAny<QuestHapticFeedbackController>();
        ResolveTaskManagerRos();
    }

    private void ResolveWristRecorders()
    {
        if (leftRecorder != null && rightRecorder != null && recorder != null)
            return;

        GripperCameraRecorder[] recorders = FindAll<GripperCameraRecorder>();
        foreach (GripperCameraRecorder candidate in recorders)
        {
            if (candidate == null)
                continue;

            if (recorder == null)
                recorder = candidate;

            string path = BuildTransformPath(candidate.transform).ToLowerInvariant();
            if (leftRecorder == null && path.Contains("left"))
                leftRecorder = candidate;
            if (rightRecorder == null && path.Contains("right"))
                rightRecorder = candidate;
        }

        if (leftRecorder == null && recorders.Length > 0)
            leftRecorder = recorders[0];
        if (rightRecorder == null)
        {
            foreach (GripperCameraRecorder candidate in recorders)
            {
                if (candidate != null && candidate != leftRecorder)
                {
                    rightRecorder = candidate;
                    break;
                }
            }
        }
        if (rightRecorder == null)
            rightRecorder = leftRecorder;
        if (recorder == null)
            recorder = rightRecorder != null ? rightRecorder : leftRecorder;
    }

    private static string BuildTransformPath(Transform transform)
    {
        if (transform == null)
            return "";

        string path = transform.name;
        Transform current = transform.parent;
        int guard = 0;
        while (current != null && guard++ < 16)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }

    private void ResolveTaskManagerRos()
    {
        if (!Application.isPlaying || taskManagerRosReady)
            return;

        try
        {
            ros = ROSConnection.GetOrCreateInstance();
            ros.RegisterPublisher<StringMsg>(taskManagerSelectTopic);
            ros.Subscribe<StringMsg>(taskManagerStatusTopic, OnTaskManagerStatus);
            taskManagerRosReady = true;
            taskManagerStatus = "Task manager: ROS subscriptions ready.";
        }
        catch (System.Exception e)
        {
            taskManagerStatus = $"Task manager ROS not ready: {e.Message}";
        }
    }

    private void OnTaskManagerStatus(StringMsg msg)
    {
        if (msg == null || string.IsNullOrWhiteSpace(msg.data))
            return;
        taskManagerStatus = msg.data;
    }

    private void PublishTaskSelection(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName))
            return;

        ResolveTaskManagerRos();
        if (ros == null || !taskManagerRosReady)
        {
            taskManagerStatus = $"Task manager unavailable; cannot select {taskName}.";
            return;
        }

        try
        {
            ros.Publish(taskManagerSelectTopic, new StringMsg(taskName));
            taskManagerStatus = $"Requested task switch: {taskName}";
        }
        catch (System.Exception e)
        {
            taskManagerStatus = $"Task switch publish failed: {e.Message}";
        }
    }

    private void ResolveControllerTransforms()
    {
        if (leftController == null || !leftController.gameObject.activeInHierarchy)
        {
            GameObject go =
                GameObject.Find("OVRCameraRig/TrackingSpace/LeftControllerAnchor") ??
                GameObject.Find(leftControllerName);
            if (go != null)
                leftController = go.transform;
        }

        if (rightController == null || !rightController.gameObject.activeInHierarchy)
        {
            GameObject rightGo =
                GameObject.Find("OVRCameraRig/TrackingSpace/RightControllerAnchor") ??
                GameObject.Find(rightControllerName);
            if (rightGo != null)
                rightController = rightGo.transform;
        }
    }

    private void CleanupLegacyWindows()
    {
        foreach (TeleopRuntimeDebugPanel debugPanel in FindAll<TeleopRuntimeDebugPanel>())
        {
            if (debugPanel != null)
            {
                debugPanel.createDebugPanel = false;
                debugPanel.enabled = false;
            }
        }

        foreach (TeleopInstructionBoard board in FindAll<TeleopInstructionBoard>())
        {
            if (board != null)
            {
                board.createInstructionBoard = false;
                board.enabled = false;
            }
        }

        foreach (GripperCameraRecorder cameraRecorder in FindAll<GripperCameraRecorder>())
        {
            if (cameraRecorder != null)
                cameraRecorder.createFloatingPanel = false;
        }

        bool firstPersonScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.IndexOf(
            "FirstPerson",
            System.StringComparison.OrdinalIgnoreCase) >= 0;

        foreach (RobotViewpointController robotView in FindAll<RobotViewpointController>())
        {
            if (robotView != null)
            {
                robotView.createRobotViewScreen = firstPersonScene;
                robotView.enabled = firstPersonScene;
            }
        }

        HideLegacyObject("Teleop_Runtime_DebugPanel");
        HideLegacyObject("Teleop_Button_Instructions");
        HideLegacyObject("GripperCameraFloatingPanel");
        if (!firstPersonScene)
            HideLegacyObject("RobotBaseViewWindow");
    }

    private static void HideLegacyObject(string objectName)
    {
        GameObject go = GameObject.Find(objectName);
        if (go != null)
            go.SetActive(false);
    }

    private void SetTabColor(RectTransform tab, bool active)
    {
        if (tab == null)
            return;
        Image image = tab.GetComponent<Image>();
        if (image != null)
            image.color = active ? activeTabColor : inactiveTabColor;

        Text text = tab.GetComponentInChildren<Text>();
        if (text != null)
            text.color = active ? accentColor : buttonTextColor;
    }

    private void StyleButton(RectTransform button, Color backgroundColor, Color textColor)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();
        if (image != null)
            ApplyRoundedImage(image, backgroundColor);

        Text text = button.GetComponentInChildren<Text>();
        if (text == null)
            return;

        text.color = textColor;
        text.fontStyle = FontStyle.Bold;

        Outline outline = text.GetComponent<Outline>();
        if (outline == null)
            outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.72f);
        outline.effectDistance = new Vector2(1.25f, -1.25f);
    }

    private static void ApplyRoundedImage(Image image, Color color)
    {
        if (image == null)
            return;

        if (roundedSprite == null)
            roundedSprite = CreateRoundedSprite();
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
    }

    private static Sprite CreateRoundedSprite()
    {
        const int size = 64;
        const int radius = 14;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "MRControlPanel_RoundedSprite"
        };
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color solid = Color.white;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x, x - (size - radius - 1), 0);
                float dy = Mathf.Max(radius - y, y - (size - radius - 1), 0);
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = 1f - Mathf.Clamp01(distance - radius + 1f);
                texture.SetPixel(x, y, alpha >= 1f ? solid : new Color(1f, 1f, 1f, alpha));
                if (alpha <= 0f)
                    texture.SetPixel(x, y, clear);
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
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
