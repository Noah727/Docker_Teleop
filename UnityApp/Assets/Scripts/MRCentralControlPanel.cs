using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using System.Globalization;

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
        Debug
    }

    private enum CameraPreviewMode
    {
        Wrist,
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
    public float contentScrollSensitivity = 1.0f;
    public Color contentViewportColor = new Color(0.0f, 0.0f, 0.0f, 0.26f);

    [Header("Camera Preview")]
    public Vector2 previewSize = new Vector2(560f, 220f);

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
    private RectTransform controlsTab;
    private RectTransform cameraTab;
    private RectTransform attachmentTab;
    private RectTransform hapticsTab;
    private RectTransform debugTab;
    private RectTransform modeButton;
    private RectTransform resetObjectsButton;
    private RectTransform resetRobotsButton;
    private RectTransform resetAllButton;
    private RectTransform resetWorkspaceButton;
    private RectTransform overheadViewButton;
    private RectTransform recordButton;
    private RectTransform captureButton;
    private RectTransform wristCameraButton;
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
    private RectTransform applyFloatingCameraButton;
    private RectTransform dragHandle;
    private RectTransform resizeHandle;
    private readonly RectTransform[] resizeHandles = new RectTransform[4];
    private UICornerDragHandle.Corner activeResizeCorner;
    private RectTransform contentScrollArea;
    private RectTransform contentViewport;
    private RectTransform scrollContent;
    private RectTransform contentScrollbarRect;
    private RectTransform scrollbarHandle;
    private RectTransform contentScrollDragRect;
    private ScrollRect contentScrollRect;
    private Scrollbar contentScrollbar;
    private Text titleText;
    private Text contentText;
    private Text statusText;
    private Text modeButtonText;
    private Text recordButtonText;
    private RawImage previewImage;
    private PanelPage page = PanelPage.Controls;
    private CameraPreviewMode cameraPreviewMode = CameraPreviewMode.Wrist;
    private Transform leftController;
    private Transform rightController;
    private bool leftTriggerWasHeld;
    private bool rightTriggerWasHeld;
    private bool panelPoseInitialized;
    private bool panelInitialHeadsetFacingApplied;
    private HandPoseSender sender;
    private WorkspaceDragController workspaceDrag;
    private GripperCameraRecorder recorder;
    private FloatingSceneCameraController floatingCamera;
    private QuestPassthroughController passthrough;
    private QuestHapticFeedbackController haptics;
    private VRDraggableWindow panelDragger;
    private bool contentScrollDragging;
    private bool contentScrollDraggingScrollbar;
    private Transform contentScrollController;
    private float contentScrollStartLocalY;
    private float contentScrollStartNormalized;
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

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Font.CreateDynamicFontFromOSFont("Arial", 16);

        titleText = EnsureText("Title", panelRect, font, "MR Control Panel", 24, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -18f), new Vector2(-48f, 36f), new Vector2(0f, 1f));

        float tabWidth = 118f;
        float tabGap = 10f;
        float tabY = -panelSize.y + 32f;
        float firstTabX = (panelSize.x - ((tabWidth * 5f) + (tabGap * 4f))) * 0.5f + (tabWidth * 0.5f);
        controlsTab = EnsureTab("Tab_Controls", "Controls", font, new Vector2(firstTabX, tabY));
        cameraTab = EnsureTab("Tab_Camera", "Camera", font, new Vector2(firstTabX + tabWidth + tabGap, tabY));
        attachmentTab = EnsureTab("Tab_Attachment", "Attach", font, new Vector2(firstTabX + (tabWidth + tabGap) * 2f, tabY));
        hapticsTab = EnsureTab("Tab_Haptics", "Haptics", font, new Vector2(firstTabX + (tabWidth + tabGap) * 3f, tabY));
        debugTab = EnsureTab("Tab_Debug", "Debug", font, new Vector2(firstTabX + (tabWidth + tabGap) * 4f, tabY));

        EnsureContentScrollView(font);
        contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
        contentText.verticalOverflow = VerticalWrapMode.Overflow;

        statusText = EnsureText("Status", panelRect, font, "", 14, FontStyle.Normal, TextAnchor.LowerLeft);
        SetRect(statusText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 66f), new Vector2(-48f, 30f), new Vector2(0f, 0f));

        previewImage = EnsureRawImage("CameraPreview", panelRect);
        SetRect(previewImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -292f), CameraPreviewSize(), new Vector2(0.5f, 0.5f));

        modeButton = EnsureButtonRect("ModeButton", "Mode", font, new Vector2(94f, -88f), new Vector2(136f, 38f));
        resetObjectsButton = EnsureButtonRect("ResetObjectsButton", "Objects", font, new Vector2(246f, -88f), new Vector2(136f, 38f));
        resetRobotsButton = EnsureButtonRect("ResetRobotsButton", "Robots", font, new Vector2(398f, -88f), new Vector2(136f, 38f));
        resetAllButton = EnsureButtonRect("ResetAllButton", "All Reset", font, new Vector2(550f, -88f), new Vector2(136f, 38f));
        resetWorkspaceButton = EnsureButtonRect("ResetWorkspaceButton", "Workspace Reset", font, new Vector2(260f, -138f), new Vector2(220f, 38f));
        overheadViewButton = EnsureButtonRect("OverheadViewButton", "Overhead View", font, new Vector2(518f, -138f), new Vector2(220f, 38f));
        RemovePanelChildIfExists("ResetButton");
        RemovePanelChildIfExists("OverheadCameraButton");
        RemovePanelChildIfExists("RubikTwistXButton");
        RemovePanelChildIfExists("RubikTwistYButton");
        RemovePanelChildIfExists("RubikTwistZButton");
        RemovePanelChildIfExists("RubikShuffleButton");
        RemovePanelChildIfExists("RubikResetButton");
        recordButton = EnsureButtonRect("RecordButton", "Record", font, new Vector2(560f, -88f), new Vector2(132f, 38f));
        captureButton = EnsureButtonRect("CaptureButton", "Capture", font, new Vector2(696f, -88f), new Vector2(112f, 38f));
        wristCameraButton = EnsureButtonRect("WristCameraButton", "Wrist", font, new Vector2(104f, -88f), new Vector2(136f, 36f));
        floatingCameraButton = EnsureButtonRect("FloatingCameraButton", "Floating", font, new Vector2(254f, -88f), new Vector2(144f, 36f));
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
        applyFloatingCameraButton = EnsureButtonRect("ApplyFloatingCameraButton", "Apply Camera", font, new Vector2(610f, -488f), new Vector2(170f, 34f));
        leftAttachmentPosInput = EnsureInputField("LeftAttachmentPosInput", "L pos x,y,z", font, new Vector2(178f, -158f), new Vector2(280f, 32f));
        leftAttachmentRotInput = EnsureInputField("LeftAttachmentRotInput", "L rot deg x,y,z", font, new Vector2(474f, -158f), new Vector2(280f, 32f));
        rightAttachmentPosInput = EnsureInputField("RightAttachmentPosInput", "R pos x,y,z", font, new Vector2(178f, -210f), new Vector2(280f, 32f));
        rightAttachmentRotInput = EnsureInputField("RightAttachmentRotInput", "R rot deg x,y,z", font, new Vector2(474f, -210f), new Vector2(280f, 32f));
        floatingCameraPosInput = EnsureInputField("FloatingCameraPosInput", "cam pos x,y,z", font, new Vector2(180f, -444f), new Vector2(292f, 32f));
        floatingCameraRotInput = EnsureInputField("FloatingCameraRotInput", "cam rot x,y,z", font, new Vector2(486f, -444f), new Vector2(292f, 32f));
        floatingCameraFovInput = EnsureInputField("FloatingCameraFovInput", "fov", font, new Vector2(430f, -488f), new Vector2(86f, 32f));
        modeButtonText = modeButton.GetComponentInChildren<Text>();
        recordButtonText = recordButton.GetComponentInChildren<Text>();

        dragHandle = EnsureDragDashHandle();
        Color resizeColor = ResizeHandleColor();
        Vector2 resizeSize = new Vector2(44f, 44f);
        float resizeOffset = 4f;
        resizeHandles[(int)UICornerDragHandle.Corner.BottomLeft] = UICornerDragHandle.Ensure(
            panel.transform,
            "ResizeHandle_BottomLeft",
            resizeColor,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(-resizeOffset, -resizeOffset),
            resizeSize,
            UICornerDragHandle.Corner.BottomLeft);
        resizeHandles[(int)UICornerDragHandle.Corner.BottomRight] = UICornerDragHandle.Ensure(
            panel.transform,
            "ResizeHandle_BottomRight",
            resizeColor,
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(resizeOffset, -resizeOffset),
            resizeSize,
            UICornerDragHandle.Corner.BottomRight);
        resizeHandles[(int)UICornerDragHandle.Corner.TopLeft] = UICornerDragHandle.Ensure(
            panel.transform,
            "ResizeHandle_TopLeft",
            resizeColor,
            new Vector2(0f, 1f),
            new Vector2(1f, 0f),
            new Vector2(-resizeOffset, resizeOffset),
            resizeSize,
            UICornerDragHandle.Corner.TopLeft);
        resizeHandles[(int)UICornerDragHandle.Corner.TopRight] = UICornerDragHandle.Ensure(
            panel.transform,
            "ResizeHandle_TopRight",
            resizeColor,
            new Vector2(1f, 1f),
            new Vector2(0f, 0f),
            new Vector2(resizeOffset, resizeOffset),
            resizeSize,
            UICornerDragHandle.Corner.TopRight);
        resizeHandle = resizeHandles[(int)UICornerDragHandle.Corner.BottomLeft];

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

    private RectTransform EnsureTab(string name, string label, Font font, Vector2 anchoredPosition)
    {
        RectTransform rect = EnsureButtonRect(name, label, font, anchoredPosition, new Vector2(118f, 36f));
        return rect;
    }

    private RectTransform EnsureButtonRect(string name, string label, Font font, Vector2 anchoredPosition, Vector2 size)
    {
        Transform existing = panel.transform.Find(name);
        GameObject go;
        if (existing == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(panel.transform, false);
            Text text = EnsureText("Text", go.GetComponent<RectTransform>(), font, label, 15, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        }
        else
        {
            go = existing.gameObject;
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
        return rect;
    }

    private RectTransform EnsureDragDashHandle()
    {
        RectTransform handleRect = EnsureRectObject(HandleName, panel.transform, typeof(Image));
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

    private InputField EnsureInputField(string name, string placeholder, Font font, Vector2 anchoredPosition, Vector2 size)
    {
        Transform existing = panel.transform.Find(name);
        GameObject go;
        if (existing == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
            go.transform.SetParent(panel.transform, false);

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

    private void EnsureContentScrollView(Font font)
    {
        contentScrollArea = EnsureRectObject("ContentScrollArea", panel.transform, typeof(ScrollRect), typeof(Image));
        Image areaImage = contentScrollArea.GetComponent<Image>();
        ApplyRoundedImage(areaImage, contentViewportColor);

        contentViewport = EnsureRectObject("Viewport", contentScrollArea, typeof(Image), typeof(Mask));
        Image viewportImage = contentViewport.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
        Mask mask = contentViewport.GetComponent<Mask>();
        mask.showMaskGraphic = false;
        SetRect(contentViewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));

        scrollContent = EnsureRectObject("ScrollContent", contentViewport);
        SetRect(scrollContent, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 100f), new Vector2(0.5f, 1f));

        contentText = EnsureText("Content", scrollContent, font, "", 17, FontStyle.Normal, TextAnchor.UpperLeft);
        SetRect(contentText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -8f), new Vector2(-24f, 100f), new Vector2(0f, 1f));

        contentScrollRect = contentScrollArea.GetComponent<ScrollRect>();
        contentScrollRect.viewport = contentViewport;
        contentScrollRect.content = scrollContent;
        contentScrollRect.horizontal = false;
        contentScrollRect.vertical = true;
        contentScrollRect.movementType = ScrollRect.MovementType.Clamped;
        contentScrollRect.inertia = false;
        contentScrollRect.scrollSensitivity = 18f;
        contentScrollbar = EnsureVerticalScrollbar();
        contentScrollRect.verticalScrollbar = contentScrollbar;
        contentScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        Transform legacyContent = panel.transform.Find("Content");
        if (legacyContent != null && legacyContent != contentText.transform)
            legacyContent.gameObject.SetActive(false);
    }

    private Scrollbar EnsureVerticalScrollbar()
    {
        contentScrollbarRect = EnsureRectObject("ContentScrollbar", contentScrollArea, typeof(Image), typeof(Scrollbar));
        SetRect(contentScrollbarRect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-12f, 0f), new Vector2(16f, -16f), new Vector2(1f, 0.5f));
        Image background = contentScrollbarRect.GetComponent<Image>();
        ApplyRoundedImage(background, new Color(1f, 0.94f, 0.78f, 0.13f));

        RectTransform slidingArea = EnsureRectObject("SlidingArea", contentScrollbarRect);
        SetRect(slidingArea, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(-4f, -4f), new Vector2(0.5f, 0.5f));

        Transform legacyHandle = contentScrollbarRect.Find("Handle");
        if (legacyHandle != null && legacyHandle.parent == contentScrollbarRect)
            legacyHandle.SetParent(slidingArea, false);

        scrollbarHandle = EnsureRectObject("Handle", slidingArea, typeof(Image));
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

    private void UpdatePanel(bool force)
    {
        if (panel == null)
            return;

        SetTabColor(controlsTab, page == PanelPage.Controls);
        SetTabColor(cameraTab, page == PanelPage.Camera);
        SetTabColor(attachmentTab, page == PanelPage.Attachment);
        SetTabColor(hapticsTab, page == PanelPage.Haptics);
        SetTabColor(debugTab, page == PanelPage.Debug);

        bool showCamera = page == PanelPage.Camera;
        bool showControls = page == PanelPage.Controls;
        bool showAttachment = page == PanelPage.Attachment;
        bool showHaptics = page == PanelPage.Haptics;
        ApplyPageLayout(showCamera);
        if (contentScrollArea != null)
            contentScrollArea.gameObject.SetActive(true);
        if (previewImage != null)
        {
            previewImage.gameObject.SetActive(showCamera);
            if (showCamera)
                previewImage.texture = ResolvePreviewTexture();
        }
        if (modeButton != null)
            modeButton.gameObject.SetActive(showControls);
        SetButtonActive(resetObjectsButton, showControls);
        SetButtonActive(resetRobotsButton, showControls);
        SetButtonActive(resetAllButton, showControls);
        SetButtonActive(resetWorkspaceButton, showControls);
        SetButtonActive(overheadViewButton, showControls);
        if (recordButton != null)
            recordButton.gameObject.SetActive(showCamera);
        if (captureButton != null)
            captureButton.gameObject.SetActive(showCamera);
        SetButtonActive(wristCameraButton, showCamera);
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
        SetButtonActive(applyFloatingCameraButton, showCamera);
        SetInputActive(leftAttachmentPosInput, showAttachment);
        SetInputActive(leftAttachmentRotInput, showAttachment);
        SetInputActive(rightAttachmentPosInput, showAttachment);
        SetInputActive(rightAttachmentRotInput, showAttachment);
        SetInputActive(floatingCameraPosInput, showCamera && cameraPreviewMode == CameraPreviewMode.Floating);
        SetInputActive(floatingCameraRotInput, showCamera && cameraPreviewMode == CameraPreviewMode.Floating);
        SetInputActive(floatingCameraFovInput, showCamera && cameraPreviewMode == CameraPreviewMode.Floating);

        if (modeButtonText != null)
            modeButtonText.text = sender != null && sender.IsGamepadModeActive ? "Hand Pose" : "Thumbstick";
        if (recordButtonText != null)
            recordButtonText.text = recorder != null && recorder.IsRecording ? "Stop Rec" : "Start Rec";
        Text attachmentAdjustText = attachmentAdjustButton != null ? attachmentAdjustButton.GetComponentInChildren<Text>() : null;
        if (attachmentAdjustText != null)
            attachmentAdjustText.text = "Hold X/A";
        Text hapticOutputText = hapticOutputButton != null ? hapticOutputButton.GetComponentInChildren<Text>() : null;
        if (hapticOutputText != null)
            hapticOutputText.text = haptics != null && haptics.hapticOutputEnabled ? "Haptics ON" : "Haptics OFF";
        Text hapticRosText = hapticRosButton != null ? hapticRosButton.GetComponentInChildren<Text>() : null;
        if (hapticRosText != null)
            hapticRosText.text = haptics != null && haptics.enableRosContactHaptics ? "ROS Contact ON" : "ROS Contact OFF";

        StyleButton(modeButton, actionButtonColor, buttonTextColor);
        StyleButton(resetObjectsButton, resetButtonColor, buttonTextColor);
        StyleButton(resetRobotsButton, resetButtonColor, buttonTextColor);
        StyleButton(resetAllButton, resetButtonColor, buttonTextColor);
        StyleButton(resetWorkspaceButton, resetButtonColor, buttonTextColor);
        StyleButton(overheadViewButton, actionButtonColor, buttonTextColor);
        StyleButton(recordButton, recorder != null && recorder.IsRecording ? recordActiveButtonColor : cameraButtonColor, buttonTextColor);
        StyleButton(captureButton, cameraButtonColor, buttonTextColor);
        StyleButton(wristCameraButton, cameraPreviewMode == CameraPreviewMode.Wrist ? activeTabColor : cameraButtonColor, buttonTextColor);
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
        StyleButton(applyFloatingCameraButton, actionButtonColor, buttonTextColor);
        RefreshInputValuesIfIdle();

        if (contentText != null)
            contentText.text = BuildContentText();
        RefreshScrollableContent();

        if (statusText != null)
            statusText.text = BuildStatusText();

        UpdateCornerHandleVisibility();
        LastStatus = $"page={page}, recorder={(recorder != null ? "ok" : "missing")}, sender={(sender != null ? "ok" : "missing")}";
    }

    private string BuildContentText()
    {
        if (page == PanelPage.Camera)
        {
            string session = recorder != null && !string.IsNullOrWhiteSpace(recorder.CurrentSessionFolder)
                ? recorder.CurrentSessionFolder
                : "No recording session yet.";
            string floating = floatingCamera != null ? floatingCamera.LastStatus : "floating camera: missing";
            return "CAMERAS\n\nView buttons switch the preview source: Wrist or Floating.\n\nFloating camera: release teleop grip, then trigger-drag the yellow camera body to move it. Trigger-drag one of the three colored rings to rotate it around X/Y/Z. Fine tune with pos / rot / FOV fields, then press Apply Camera.\n\n" + floating + "\n\nLast recording session:\n" + session;
        }

        if (page == PanelPage.Attachment)
        {
            string attach = sender != null ? sender.AttachmentOffsetStatus : "HandPoseSender: missing";
            return "ATTACHMENT MODE\n\nThe backend default offset is attachment_tool_rotation_offset_xyzw in the dual-arm tuning YAML files. This page sends a saved Unity-side offset on top of that default.\n\nCalibration: enable attachment mode for an arm, hold that arm's grip, then hold left X / right A. The arm freezes. Move the controller to the pose you want relative to the visible gripper, then release X/A to save the new offset.\n\nSaved offsets are stored locally on the headset and auto-applied next launch. Reset L, Reset R, or Reset Both clears the saved offset back to backend default behavior.\n\nManual fields use:\nposition offset: x,y,z meters in Unity workspace axes\nrotation offset: x,y,z degrees\n\n" + attach;
        }

        if (page == PanelPage.Haptics)
        {
            string hp = haptics != null ? haptics.LastStatus : "QuestHapticFeedbackController: missing";
            return "HAPTICS\n\nBackend contact detection is Gazebo-authoritative. It checks gripper probe points against the generated SDF object geometry, then sends per-arm amplitudes to the headset.\n\nVibration types:\nshort two-tap pulse: first secure gripper/object contact while closing\ncontinuous buzz: low steady contact feedback while contact persists\nproximity ramp: light feedback while closing near an object\n\nUse the buttons above to toggle headset output, toggle ROS contact haptics, and tune headset gain.\n\n" + hp;
        }

        if (page == PanelPage.Debug)
        {
            string modeText = sender != null ? $"Control mode: {sender.ControlModeLabel}" : "Control mode: unknown";
            string senderText = sender != null ? sender.LastStatus : "HandPoseSender: missing";
            string workspaceText = workspaceDrag != null ? workspaceDrag.LastStatus : "WorkspaceDragController: missing";
            string passText = passthrough != null ? passthrough.LastStatus : "Passthrough: missing";
            string hapticText = haptics != null ? haptics.LastStatus : "Haptics: missing";
            string recorderText = recorder != null ? $"Recorder: {(recorder.IsRecording ? "REC" : "idle")}" : "Recorder: missing";
            string panelDragText = panelDragger != null ? $"Panel drag: {panelDragger.LastStatus}" : "Panel drag: missing";
            return "DEBUG\n\nTrigger-drag this text area to scroll.\n\n" + modeText + "\n\n" + senderText + "\n\n" + workspaceText + "\n\n" + panelDragText + "\n\n" + hapticText + "\n\n" + passText + "\n\n" + recorderText;
        }

        return
            "CONTROLS\n\n" +
            "Trigger-drag this text area to scroll.\n\n" +
            "Left grip hold: engage left-arm teleop.\n" +
            "Right grip hold: engage right-arm teleop.\n" +
            "Left/right trigger tap while gripping: toggle that gripper open / close.\n" +
            "Left X hold: left-arm rotation mode.\n" +
            "Right A hold: right-arm rotation mode.\n" +
            "Left Y tap: toggle left attachment mode.\n" +
            "Right B tap: toggle right attachment mode.\n" +
            "Trigger hold while not gripping: drag/rotate workspace handles.\n" +
            "Panel Mode button: switch hand-pose / thumbstick mode.\n" +
            "Reset buttons: Objects, Robots, All Reset, Workspace, or Overhead View.\n\n" +
            "Teleop still requires that arm's grip hold before the arm moves.\n" +
            "Panel: point either controller at tabs/buttons. Drag from the floating notch; resize from corner L-notches.";
    }

    private string BuildStatusText()
    {
        string teleop = sender != null
            ? $"L {(sender.IsLeftTeleopHeld ? "on" : "off")} / R {(sender.IsRightTeleopHeld ? "on" : "off")}"
            : "teleop ?";
        string attach = sender != null
            ? $"attach L {(sender.IsLeftAttachmentModeActive ? "on" : "off")} / R {(sender.IsRightAttachmentModeActive ? "on" : "off")}"
            : "attach ?";
        string mode = sender != null ? sender.ControlModeLabel : "mode ?";
        string rec = recorder != null && recorder.IsRecording ? "REC" : "idle";
        return $"{mode} | {teleop} | {attach} | camera {rec} | page {page}";
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

        if (contentScrollDragging)
        {
            bool activeTriggerHeld =
                (contentScrollController == leftController && leftTriggerHeld) ||
                (contentScrollController == rightController && rightTriggerHeld);
            if (activeTriggerHeld)
                UpdateContentScrollDrag(contentScrollController);
            else
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
        if (leftTriggerDown && TryBeginContentScroll(leftController))
            return;
        if (rightTriggerDown)
        {
            if (TryHandlePanelClick(rightController))
                return;
            if (TryBeginResizeDrag(rightController))
                return;
            TryBeginContentScroll(rightController);
        }
    }

    private bool TryHandlePanelClick(Transform controller)
    {
        if (controller == null)
            return false;

        Ray ray = new Ray(controller.position, controller.forward);
        if (RectRayHit(controlsTab, ray))
        {
            page = PanelPage.Controls;
            return true;
        }
        else if (RectRayHit(cameraTab, ray))
        {
            page = PanelPage.Camera;
            return true;
        }
        else if (RectRayHit(attachmentTab, ray))
        {
            page = PanelPage.Attachment;
            return true;
        }
        else if (RectRayHit(hapticsTab, ray))
        {
            page = PanelPage.Haptics;
            return true;
        }
        else if (RectRayHit(debugTab, ray))
        {
            page = PanelPage.Debug;
            return true;
        }
        else if (page == PanelPage.Controls && RectRayHit(modeButton, ray) && sender != null)
        {
            sender.ToggleControlMode();
            return true;
        }
        else if (page == PanelPage.Controls && RectRayHit(resetObjectsButton, ray) && sender != null)
        {
            sender.RequestResetObjects();
            return true;
        }
        else if (page == PanelPage.Controls && RectRayHit(resetRobotsButton, ray) && sender != null)
        {
            sender.RequestResetRobots();
            return true;
        }
        else if (page == PanelPage.Controls && RectRayHit(resetAllButton, ray) && sender != null)
        {
            sender.RequestResetAll();
            return true;
        }
        else if (page == PanelPage.Controls && RectRayHit(resetWorkspaceButton, ray))
        {
            ResetWorkspaceUnderHeadset();
            return true;
        }
        else if (page == PanelPage.Controls && RectRayHit(overheadViewButton, ray))
        {
            ResetWorkspaceUnderHeadset();
            return true;
        }
        else if (page == PanelPage.Camera && RectRayHit(recordButton, ray) && recorder != null)
        {
            recorder.ToggleRecording();
            return true;
        }
        else if (page == PanelPage.Camera && RectRayHit(captureButton, ray) && recorder != null)
        {
            recorder.CaptureOneFrame();
            return true;
        }
        else if (page == PanelPage.Camera && RectRayHit(wristCameraButton, ray))
        {
            cameraPreviewMode = CameraPreviewMode.Wrist;
            return true;
        }
        else if (page == PanelPage.Camera && RectRayHit(floatingCameraButton, ray))
        {
            cameraPreviewMode = CameraPreviewMode.Floating;
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

        return false;
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
        ApplyCameraControlLayout(showCamera);

        if (contentScrollArea != null)
        {
            Vector2 contentPosition;
            Vector2 contentSize;
            if (showCamera)
            {
                bool floating = cameraPreviewMode == CameraPreviewMode.Floating;
                contentPosition = floating ? new Vector2(24f, -520f) : new Vector2(24f, -428f);
                contentSize = floating ? new Vector2(-48f, 74f) : new Vector2(-48f, 152f);
            }
            else if (page == PanelPage.Attachment)
            {
                contentPosition = new Vector2(24f, -260f);
                contentSize = new Vector2(-48f, 238f);
            }
            else
            {
                contentPosition = new Vector2(24f, -188f);
                contentSize = new Vector2(-48f, 310f);
            }
            SetRect(contentScrollArea, new Vector2(0f, 1f), new Vector2(1f, 1f), contentPosition, contentSize, new Vector2(0f, 1f));
        }

        if (previewImage != null)
            SetRect(previewImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -272f), CameraPreviewSize(), new Vector2(0.5f, 0.5f));
    }

    private void ApplyCameraControlLayout(bool showCamera)
    {
        if (!showCamera)
            return;

        SetButtonTransform(wristCameraButton, new Vector2(104f, -88f), new Vector2(136f, 36f));
        SetButtonTransform(floatingCameraButton, new Vector2(254f, -88f), new Vector2(144f, 36f));
        SetButtonTransform(recordButton, new Vector2(548f, -88f), new Vector2(132f, 38f));
        SetButtonTransform(captureButton, new Vector2(690f, -88f), new Vector2(112f, 38f));

        SetInputTransform(floatingCameraPosInput, new Vector2(180f, -414f), new Vector2(292f, 32f));
        SetInputTransform(floatingCameraRotInput, new Vector2(486f, -414f), new Vector2(292f, 32f));
        SetInputTransform(floatingCameraFovInput, new Vector2(430f, -458f), new Vector2(86f, 32f));
        SetButtonTransform(applyFloatingCameraButton, new Vector2(610f, -458f), new Vector2(170f, 34f));
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

    private void RefreshScrollableContent()
    {
        if (contentText == null || scrollContent == null || contentViewport == null || contentScrollRect == null || contentScrollArea == null)
            return;
        if (!contentScrollArea.gameObject.activeInHierarchy)
            return;

        float previousScroll = contentScrollRect.verticalNormalizedPosition;
        Canvas.ForceUpdateCanvases();
        float viewportHeight = Mathf.Max(1f, contentViewport.rect.height);
        float textHeight = Mathf.Max(viewportHeight - 16f, contentText.preferredHeight + 16f);
        float contentHeight = Mathf.Max(viewportHeight, textHeight + 16f);

        SetRect(scrollContent, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, contentHeight), new Vector2(0.5f, 1f));
        SetRect(contentText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -8f), new Vector2(-24f, textHeight), new Vector2(0f, 1f));

        if (!CanScrollContent())
            contentScrollRect.verticalNormalizedPosition = 1f;
        else
            contentScrollRect.verticalNormalizedPosition = Mathf.Clamp01(previousScroll);
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

        return recorder != null ? recorder.PreviewTexture : null;
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
        Transform child = panel.transform.Find(childName);
        if (child != null)
            child.gameObject.SetActive(active);
    }

    private void RemovePanelChildIfExists(string childName)
    {
        if (panel == null)
            return;

        Transform child = panel.transform.Find(childName);
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
        if (rect == null)
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

    private bool TryBeginContentScroll(Transform controller)
    {
        if (controller == null || contentScrollRect == null || contentViewport == null)
            return false;
        if (!contentViewport.gameObject.activeInHierarchy)
            return false;

        Ray ray = new Ray(controller.position, controller.forward);
        Vector2 scrollbarLocalPoint = Vector2.zero;
        Vector2 viewportLocalPoint = Vector2.zero;
        bool hitScrollbar = contentScrollbarRect != null && TryRectRayLocalPoint(contentScrollbarRect, ray, out scrollbarLocalPoint);
        bool hitViewport = TryRectRayLocalPoint(contentViewport, ray, out viewportLocalPoint);
        if (!hitScrollbar && !hitViewport)
            return false;
        if (!CanScrollContent())
            return true;

        contentScrollDragging = true;
        contentScrollDraggingScrollbar = hitScrollbar;
        contentScrollController = controller;
        contentScrollDragRect = hitScrollbar ? contentScrollbarRect : contentViewport;
        contentScrollStartLocalY = hitScrollbar ? scrollbarLocalPoint.y : viewportLocalPoint.y;
        contentScrollStartNormalized = contentScrollRect.verticalNormalizedPosition;
        if (hitScrollbar)
            SetScrollFromScrollbarLocalPoint(scrollbarLocalPoint);
        return true;
    }

    private void UpdateContentScrollDrag(Transform controller)
    {
        if (controller == null || contentScrollRect == null || contentViewport == null)
            return;

        Ray ray = new Ray(controller.position, controller.forward);
        RectTransform dragRect = contentScrollDragRect != null ? contentScrollDragRect : contentViewport;
        if (!TryRectRayPlaneLocalPoint(dragRect, ray, out Vector2 localPoint))
            return;

        if (contentScrollDraggingScrollbar)
        {
            SetScrollFromScrollbarLocalPoint(localPoint);
            return;
        }

        float hiddenHeight = HiddenScrollHeight();
        if (hiddenHeight <= 1f)
            return;

        float localDelta = localPoint.y - contentScrollStartLocalY;
        float normalizedDelta = (localDelta / hiddenHeight) * Mathf.Max(0.1f, contentScrollSensitivity);
        contentScrollRect.verticalNormalizedPosition = Mathf.Clamp01(contentScrollStartNormalized - normalizedDelta);
    }

    private void SetScrollFromScrollbarLocalPoint(Vector2 localPoint)
    {
        if (contentScrollRect == null || contentScrollbarRect == null)
            return;

        Rect rect = contentScrollbarRect.rect;
        float normalized = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);
        contentScrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalized);
    }

    private void EndContentScroll()
    {
        contentScrollDragging = false;
        contentScrollDraggingScrollbar = false;
        contentScrollController = null;
        contentScrollDragRect = null;
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
        RefreshScrollableContent();
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

    private bool CanScrollContent()
    {
        return HiddenScrollHeight() > 1f;
    }

    private float HiddenScrollHeight()
    {
        if (scrollContent == null || contentViewport == null)
            return 0f;
        return Mathf.Max(0f, scrollContent.rect.height - contentViewport.rect.height);
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
        if (recorder == null)
            recorder = FindAny<GripperCameraRecorder>();
        if (floatingCamera == null)
            floatingCamera = FindAny<FloatingSceneCameraController>();
        if (passthrough == null)
            passthrough = QuestPassthroughController.ActiveInstance ?? FindAny<QuestPassthroughController>();
        if (haptics == null)
            haptics = QuestHapticFeedbackController.ActiveInstance ?? FindAny<QuestHapticFeedbackController>();
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
