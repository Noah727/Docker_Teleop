using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public class TeleopInstructionBoard : MonoBehaviour
{
    public bool createInstructionBoard = true;
    public bool rebuildInstructionBoardFromSettings = false;

    public string boardName = "Teleop_Button_Instructions";
    public Vector3 boardWorldPosition = new Vector3(1.0f, 1.3f, 1.8f);
    public Vector3 boardWorldEuler = new Vector3(0.0f, 90.0f, 0.0f);
    public Vector3 boardWorldScale = new Vector3(0.00155f, 0.00155f, 0.00155f);
    public Vector2 boardSize = new Vector2(620.0f, 580.0f);

    public Color backgroundColor = new Color(0.08f, 0.07f, 0.055f, 0.88f);
    public Color accentColor = new Color(0.90f, 0.55f, 0.25f, 1.0f);
    public Color titleColor = new Color(1.0f, 0.88f, 0.66f, 1.0f);
    public Color textColor = new Color(0.96f, 0.92f, 0.84f, 1.0f);
    public Color dragHandleColor = new Color(1.0f, 0.92f, 0.72f, 0.70f);
    public bool makeBoardDraggable = true;

    public string title = "ROBOT CONTROLS";

    [TextArea(8, 18)]
    public string instructions =
        "RIGHT CONTROLLER\n" +
        "Grip hold: engage right-arm teleop\n" +
        "Trigger tap while gripping: right gripper open / close\n" +
        "A hold: right-arm rotation mode\n" +
        "B tap: right-arm attachment mode\n" +
        "Thumbstick press: clutch; release to recenter hand reference\n\n" +
        "LEFT CONTROLLER\n" +
        "Grip hold: engage left-arm teleop\n" +
        "Trigger tap while gripping: left gripper open / close\n" +
        "X hold: left-arm rotation mode\n" +
        "Y tap: toggle left-arm attachment mode\n\n" +
        "CONTROL PANEL\n" +
        "Use panel for recording, reset, cameras,\n" +
        "haptics, and task controls.\n\n" +
        "OPTIONAL CONTROLS\n" +
        "Thumbstick/gamepad and keyboard modes are\n" +
        "right-arm backend commands from terminal.\n\n" +
        "Release right grip, point left controller at panel,\n" +
        "hold left trigger to drag it.";

    void OnEnable()
    {
        if (createInstructionBoard)
            CreateOrUpdateBoard();
        else
            HideExistingBoard();
    }

    void OnValidate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null)
                    return;

                if (createInstructionBoard)
                    CreateOrUpdateBoard();
                else
                    HideExistingBoard();
            };
        }
#endif
    }

    public void CreateOrUpdateBoard()
    {
        if (!createInstructionBoard)
        {
            HideExistingBoard();
            return;
        }

        GameObject board = GameObject.Find(boardName);
        bool needsBuild = board == null || rebuildInstructionBoardFromSettings || !HasExpectedChildren(board);

        if (needsBuild)
        {
            if (board != null)
                DestroyBoard(board);

            board = BuildBoard();
            rebuildInstructionBoardFromSettings = false;
        }

        ApplyBoardTransform(board);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
            if (board != null)
                EditorUtility.SetDirty(board);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    bool HasExpectedChildren(GameObject board)
    {
        return board.transform.Find("Background") != null
            && board.transform.Find("AccentBar") != null
            && board.transform.Find("DragHandle") != null
            && board.transform.Find("Title") != null
            && board.transform.Find("Instructions") != null;
    }

    GameObject BuildBoard()
    {
        GameObject board = new GameObject(boardName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = board.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = board.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10.0f;

        RectTransform rect = board.GetComponent<RectTransform>();
        rect.sizeDelta = boardSize;

        CreateImage(board.transform, "Background", backgroundColor, Vector2.zero, boardSize);
        CreateImage(board.transform, "AccentBar", accentColor, new Vector2(0.0f, boardSize.y * 0.5f - 18.0f), new Vector2(boardSize.x, 14.0f));
        EnsureDragHandle(board);

        CreateText(
            board.transform,
            "Title",
            title,
            titleColor,
            38,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Vector2(0.0f, boardSize.y * 0.5f - 56.0f),
            new Vector2(boardSize.x - 44.0f, 52.0f));

        CreateText(
            board.transform,
            "Instructions",
            instructions,
            textColor,
            20,
            FontStyle.Normal,
            TextAnchor.UpperLeft,
            new Vector2(0.0f, -40.0f),
            new Vector2(boardSize.x - 64.0f, boardSize.y - 118.0f));

        ApplyBoardTransform(board);
        EnsureDraggableWindow(board);
        return board;
    }

    void HideExistingBoard()
    {
        GameObject board = GameObject.Find(boardName);
        if (board == null || !board.activeSelf)
            return;

        board.SetActive(false);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(board);
            EditorSceneManager.MarkSceneDirty(board.scene);
        }
#endif
    }

    void ApplyBoardTransform(GameObject board)
    {
        if (board == null)
            return;

        RectTransform rect = board.GetComponent<RectTransform>();
        rect.sizeDelta = boardSize;
        board.transform.position = boardWorldPosition;
        board.transform.rotation = Quaternion.Euler(boardWorldEuler);
        board.transform.localScale = boardWorldScale;
        EnsureDragHandle(board);
        EnsureDraggableWindow(board);
    }

    void EnsureDragHandle(GameObject board)
    {
        if (board == null)
            return;

        UICornerDragHandle.Ensure(
            board.transform,
            "DragHandle",
            dragHandleColor,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(boardSize.x * 0.5f - 28.0f, -boardSize.y * 0.5f + 28.0f),
            new Vector2(52.0f, 52.0f));
    }

    void EnsureDraggableWindow(GameObject board)
    {
        if (!makeBoardDraggable || board == null)
            return;

        RectTransform rect = board.GetComponent<RectTransform>();
        VRDraggableWindow dragger = board.GetComponent<VRDraggableWindow>();
        if (dragger == null)
            dragger = board.AddComponent<VRDraggableWindow>();

        dragger.windowRoot = rect;
        dragger.handleNameContains = "DragHandle";
        dragger.requireHandleHit = true;
        dragger.allowWholeWindowFallback = false;
        dragger.dragController = VRDraggableWindow.DragController.Left;
        dragger.dragButton = VRDraggableWindow.DragButton.Trigger;
        dragger.requireRightGripReleased = true;
    }

    Image CreateImage(Transform parent, string name, Color color, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = child.GetComponent<Image>();
        image.color = color;
        return image;
    }

    Text CreateText(
        Transform parent,
        string name,
        string text,
        Color color,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text label = child.GetComponent<Text>();
        label.text = text;
        label.color = color;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = alignment;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (label.font == null)
            label.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
        return label;
    }

    void DestroyBoard(GameObject board)
    {
        if (Application.isPlaying)
            Destroy(board);
        else
            DestroyImmediate(board);
    }
}
