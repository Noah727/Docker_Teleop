using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(500)]
public class TeleopRuntimeDebugPanel : MonoBehaviour
{
    public bool createDebugPanel = true;
    public string panelName = "Teleop_Runtime_DebugPanel";
    public Vector3 panelWorldPosition = new Vector3(-0.92f, 1.62f, 1.15f);
    public Vector3 panelWorldEuler = new Vector3(0f, 25f, 0f);
    public Vector3 panelWorldScale = new Vector3(0.00125f, 0.00125f, 0.00125f);
    public Vector2 panelSize = new Vector2(520f, 250f);
    public Color backgroundColor = new Color(0.02f, 0.025f, 0.025f, 0.75f);
    public Color textColor = new Color(0.86f, 0.96f, 1.0f, 1.0f);
    public float updateIntervalSec = 0.2f;
    public bool makePanelDraggable = true;
    public Color dragHandleColor = new Color(1.0f, 0.92f, 0.72f, 0.70f);

    private GameObject panel;
    private Text debugText;
    private float nextUpdateTime;
    private QuestPassthroughController passthrough;
    private RobotViewpointController robotView;
    private QuestHapticFeedbackController haptics;
    private HandPoseSender handPoseSender;
    private WorkspaceDragController workspaceDrag;

    private void Start()
    {
        EnsurePanel();
        ResolveReferences();
        UpdateText(force: true);
    }

    private void Update()
    {
        ResolveReferences();
        UpdateText(force: false);
    }

    private void EnsurePanel()
    {
        if (!createDebugPanel)
            return;

        if (panel == null)
        {
            panel = GameObject.Find(panelName);
            if (panel == null)
                panel = new GameObject(panelName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
        }

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = panelSize;
        panel.transform.position = panelWorldPosition;
        panel.transform.rotation = Quaternion.Euler(panelWorldEuler);
        panel.transform.localScale = panelWorldScale;
        SetLayerRecursively(panel, 5);

        Canvas canvas = panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 55;

        Image background = panel.GetComponent<Image>();
        background.color = backgroundColor;

        Transform textTransform = panel.transform.Find("DebugText");
        if (textTransform == null)
        {
            GameObject textObject = new GameObject("DebugText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(panel.transform, false);
            textTransform = textObject.transform;
        }

        RectTransform textRect = textTransform.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 14f);
        textRect.offsetMax = new Vector2(-18f, -14f);

        debugText = textTransform.GetComponent<Text>();
        debugText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (debugText.font == null)
            debugText.font = Font.CreateDynamicFontFromOSFont("Arial", 16);
        debugText.fontSize = 17;
        debugText.alignment = TextAnchor.UpperLeft;
        debugText.horizontalOverflow = HorizontalWrapMode.Wrap;
        debugText.verticalOverflow = VerticalWrapMode.Overflow;
        debugText.color = textColor;

        EnsureDragHandle(rect);
        EnsureDraggableWindow(rect);
    }

    private void EnsureDragHandle(RectTransform panelRect)
    {
        UICornerDragHandle.Ensure(
            panel.transform,
            "DragHandle",
            dragHandleColor,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-12f, 12f),
            new Vector2(46f, 46f));
    }

    private void EnsureDraggableWindow(RectTransform panelRect)
    {
        if (!makePanelDraggable)
            return;

        VRDraggableWindow dragger = panel.GetComponent<VRDraggableWindow>();
        if (dragger == null)
            dragger = panel.AddComponent<VRDraggableWindow>();

        dragger.windowRoot = panelRect;
        dragger.handleNameContains = "DragHandle";
        dragger.requireHandleHit = true;
        dragger.allowWholeWindowFallback = false;
        dragger.dragController = VRDraggableWindow.DragController.Left;
        dragger.dragButton = VRDraggableWindow.DragButton.Trigger;
        dragger.requireRightGripReleased = true;
    }

    private void ResolveReferences()
    {
        if (passthrough == null)
            passthrough = QuestPassthroughController.ActiveInstance ?? FindAny<QuestPassthroughController>();
        if (robotView == null)
            robotView = RobotViewpointController.ActiveInstance ?? FindAny<RobotViewpointController>();
        if (haptics == null)
            haptics = QuestHapticFeedbackController.ActiveInstance ?? FindAny<QuestHapticFeedbackController>();
        if (handPoseSender == null)
            handPoseSender = FindAny<HandPoseSender>();
        if (workspaceDrag == null)
            workspaceDrag = WorkspaceDragController.ActiveInstance ?? FindAny<WorkspaceDragController>();
    }

    private void UpdateText(bool force)
    {
        if (debugText == null)
            return;

        if (!force && Time.unscaledTime < nextUpdateTime)
            return;
        nextUpdateTime = Time.unscaledTime + Mathf.Max(0.05f, updateIntervalSec);

        string mr = passthrough != null ? passthrough.LastStatus : "MR: no controller";
        string rv = robotView != null ? robotView.LastStatus : "robot view: no controller";
        string hp = haptics != null ? haptics.LastStatus : "haptics: no controller";
        string sender = handPoseSender != null ? handPoseSender.LastStatus : "sender: no HandPoseSender";
        string workspace = workspaceDrag != null ? workspaceDrag.LastStatus : "workspace drag: no controller";
        string teleop = handPoseSender != null && handPoseSender.IsTeleopHeld ? "ENGAGED" : "free";
        string gripper = "unknown";
        if (handPoseSender != null)
            gripper = handPoseSender.IsGripperClosing ? "closing" : (handPoseSender.IsGripperOpening ? "opening" : "idle");

        debugText.text =
            "RUNTIME DEBUG\n" +
            $"Teleop: {teleop} | Gripper: {gripper}\n" +
            $"Sender: {sender}\n" +
            $"Workspace: {workspace}\n" +
            $"{mr}\n" +
            $"Haptics: {hp}\n" +
            $"Robot view: {rv}";
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
}
