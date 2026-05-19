using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class GripperCameraRecorder : MonoBehaviour
{
    private const string RuntimeMarkerName = "GripperDataCamera_VisibleMarker";
    private const string FloatingPanelName = "GripperCameraFloatingPanel";
    private const string FloatingPanelPrefsPrefix = "GripperCameraRecorder.FloatingPanel.";

    public enum PanelDragController
    {
        Left,
        Right
    }

    public enum PanelDragButton
    {
        Trigger,
        Grip,
        Thumbstick
    }

    [Header("Camera")]
    public Camera sourceCamera;
    public int width = 1280;
    public int height = 720;
    public int depthBits = 24;
    public bool assignRuntimeRenderTexture = true;
    public bool excludeUnityUiLayerFromRecording = true;

    [Header("Recording")]
    public bool recordOnPlay = false;
    public KeyCode toggleRecordingKey = KeyCode.R;
    public KeyCode captureOneFrameKey = KeyCode.P;
    public int captureEveryNFrames = 5;
    public string outputFolderName = "GripperCameraRecordings";
    public bool logOutputPathOnStart = true;
    public bool forceRenderBeforeCapture = false;
    public bool enableKeyboardShortcuts = true;
    public bool toggleRecordingWithLeftX = true;
    public bool debugLeftXInput = true;
    public float leftXToggleDebounceSec = 0.25f;

    [Header("Scene Marker")]
    public bool showSceneMarker = true;
    public bool createRuntimeSceneMarker = true;
    public bool rebuildSceneMarkerFromSettings = false;
    public int runtimeSceneMarkerLayer = 0;
    public Color markerColor = new Color(0.2f, 0.85f, 1.0f, 1.0f);
    public Vector3 markerBoxSize = new Vector3(0.035f, 0.02f, 0.025f);
    public float markerForwardLength = 0.12f;
    public float markerFrustumHalfSize = 0.035f;
    public float markerLineWidth = 0.004f;

    [Header("Floating Control Panel")]
    public bool createFloatingPanel = true;
    public bool rebuildFloatingPanelFromSettings = false;
    public bool floatingPanelFixedInScene = true;
    public Transform floatingPanelParent;
    public Vector3 floatingPanelWorldPosition = new Vector3(-0.65f, 1.25f, 1.0f);
    public Vector3 floatingPanelWorldEuler = Vector3.zero;
    public bool floatingPanelFaceMainCameraOnCreate = true;
    public Vector3 floatingPanelLocalPosition = new Vector3(-0.45f, -0.04f, 1.0f);
    public Vector3 floatingPanelLocalEuler = Vector3.zero;
    public Vector3 floatingPanelLocalScale = new Vector3(0.0015f, 0.0015f, 0.0015f);
    public Vector2 floatingPanelSize = new Vector2(420f, 310f);
    public Vector2 previewSize = new Vector2(360f, 200f);
    public Color panelColor = new Color(0.02f, 0.025f, 0.025f, 0.82f);
    public Color recordingColor = new Color(1.0f, 0.18f, 0.12f, 1.0f);
    public Color idleColor = new Color(0.18f, 0.78f, 1.0f, 1.0f);
    [Tooltip("If the runtime/editor panel exists but is missing required children, rebuild the default controls.")]
    public bool rebuildFloatingPanelWhenRequiredChildrenMissing = true;

    [Header("Floating Panel Drag")]
    public bool enableFloatingPanelControllerDrag = true;
    public bool requireTeleopDisengagedForPanelDrag = true;
    public PanelDragController floatingPanelDragController = PanelDragController.Left;
    public PanelDragButton floatingPanelDragButton = PanelDragButton.Trigger;
    public float floatingPanelDragButtonThreshold = 0.55f;
    public float floatingPanelDragRayMaxDistance = 3.0f;
    public float rightGripTeleopDragBlockThreshold = 0.55f;
    public bool persistFloatingPanelDraggedPose = true;

    private RenderTexture runtimeRenderTexture;
    private Texture2D readbackTexture;
    private bool ownsRenderTexture;
    private bool recording;
    private int frameCounter;
    private string sessionFolder;
    private Text statusText;
    private Text toggleButtonText;
    private RawImage previewImage;
    private Button toggleButton;
    private Button captureButton;
    private GameObject floatingPanel;
    private GameObject runtimeSceneMarker;
    private Material runtimeMarkerMaterial;
    private Transform cachedLeftControllerTransform;
    private Transform cachedRightControllerTransform;
    private bool floatingPanelDragActive;
    private bool floatingPanelDragButtonWasHeld;
    private bool floatingPanelSavedPoseLoaded;
    private Vector3 floatingPanelDragLocalOffsetFromController;
    private Quaternion floatingPanelDragWorldRotation;
    private bool leftXWasHeld;
    private float lastLeftXToggleTime = -999f;
    private bool legacyInputUnavailable;
#if UNITY_EDITOR
    private bool editorMarkerRefreshQueued;
    private bool editorMarkerForceRebuildQueued;
    private bool editorFloatingPanelForceRebuildQueued;
#endif

    private void OnEnable()
    {
        ResolveSourceCamera();

        if (!Application.isPlaying)
        {
            CreateOrUpdateRuntimeSceneMarker();
            CreateOrUpdateFloatingPanel();
        }
    }

    private void OnValidate()
    {
        bool rebuildMarker = rebuildSceneMarkerFromSettings;
        bool rebuildPanel = rebuildFloatingPanelFromSettings;
        rebuildSceneMarkerFromSettings = false;
        rebuildFloatingPanelFromSettings = false;

        captureEveryNFrames = Mathf.Max(1, captureEveryNFrames);
        width = Mathf.Max(16, width);
        height = Mathf.Max(16, height);
        runtimeSceneMarkerLayer = Mathf.Clamp(runtimeSceneMarkerLayer, 0, 31);
        markerForwardLength = Mathf.Max(0.01f, markerForwardLength);
        markerFrustumHalfSize = Mathf.Max(0.005f, markerFrustumHalfSize);
        markerLineWidth = Mathf.Max(0.0005f, markerLineWidth);
        floatingPanelDragButtonThreshold = Mathf.Clamp(floatingPanelDragButtonThreshold, 0.05f, 0.95f);
        floatingPanelDragRayMaxDistance = Mathf.Max(0.05f, floatingPanelDragRayMaxDistance);
        rightGripTeleopDragBlockThreshold = Mathf.Clamp(rightGripTeleopDragBlockThreshold, 0.05f, 0.95f);

        if (!Application.isPlaying)
        {
            ResolveSourceCamera();
#if UNITY_EDITOR
            QueueEditorRefresh(rebuildMarker, rebuildPanel);
#else
            CreateOrUpdateRuntimeSceneMarker(rebuildMarker);
            CreateOrUpdateFloatingPanel(rebuildPanel);
#endif
        }
    }

    private void Awake()
    {
        ResolveSourceCamera();

        if (!Application.isPlaying)
        {
            CreateOrUpdateRuntimeSceneMarker();
            CreateOrUpdateFloatingPanel();
            return;
        }

        if (excludeUnityUiLayerFromRecording && sourceCamera != null)
            sourceCamera.cullingMask &= ~(1 << 5);

        captureEveryNFrames = Mathf.Max(1, captureEveryNFrames);
        width = Mathf.Max(16, width);
        height = Mathf.Max(16, height);

        EnsureRuntimeRenderTexture();
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        if (logOutputPathOnStart)
        {
            Debug.Log($"[GripperCameraRecorder] Recording root: {GetRecordingRoot()}");
            Debug.Log("[GripperCameraRecorder] Press left-controller X or R to start/stop recording, P to capture one frame.");
        }

        if (createRuntimeSceneMarker)
            CreateOrUpdateRuntimeSceneMarker();

        CreateOrUpdateFloatingPanel();

        UpdateFloatingPanel();

        if (recordOnPlay)
            StartRecording();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            UpdateRuntimeSceneMarkerVisibility();
            return;
        }

        if (KeyboardKeyDown(toggleRecordingKey) || LeftControllerXPressed())
        {
            ToggleRecording();
        }

        if (KeyboardKeyDown(captureOneFrameKey))
        {
            CaptureOneFrame();
        }

        UpdateRuntimeSceneMarkerVisibility();
        UpdateFloatingPanelControllerDrag();
        UpdateFloatingPanel();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        if (!recording)
            return;

        if ((frameCounter++ % captureEveryNFrames) == 0)
            StartCoroutine(CaptureFrame());
    }

    public void StartRecording()
    {
        EnsureSessionFolder(DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        frameCounter = 0;
        recording = true;
        Debug.Log($"[GripperCameraRecorder] Started recording to: {sessionFolder}");
        UpdateFloatingPanel();
    }

    public void StopRecording()
    {
        recording = false;
        Debug.Log($"[GripperCameraRecorder] Stopped recording. Last session: {sessionFolder}");
        UpdateFloatingPanel();
    }

    public void ToggleRecording()
    {
        if (recording)
            StopRecording();
        else
            StartRecording();
    }

    public void CaptureOneFrame()
    {
        EnsureSessionFolder("single_frame");
        StartCoroutine(CaptureFrame());
        Debug.Log($"[GripperCameraRecorder] Captured one frame to: {sessionFolder}");
        UpdateFloatingPanel();
    }

    private bool LeftControllerXPressed()
    {
        if (!toggleRecordingWithLeftX)
            return false;

        bool buttonOneLeft = OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.LTouch);
        bool buttonThreeLeft = OVRInput.Get(OVRInput.Button.Three, OVRInput.Controller.LTouch);
        bool buttonThreeTouch = OVRInput.Get(OVRInput.Button.Three, OVRInput.Controller.Touch);
        bool buttonThreeActive = OVRInput.Get(OVRInput.Button.Three);
        bool rawXLeft = OVRInput.Get(OVRInput.RawButton.X, OVRInput.Controller.LTouch);
        bool rawXAny = OVRInput.Get(OVRInput.RawButton.X);

        bool held = buttonOneLeft || buttonThreeLeft || buttonThreeTouch || buttonThreeActive || rawXLeft || rawXAny;
        bool risingEdge = held && !leftXWasHeld;
        leftXWasHeld = held;

        if (!risingEdge)
            return false;

        float now = Time.unscaledTime;
        if (now - lastLeftXToggleTime < Mathf.Max(0.05f, leftXToggleDebounceSec))
            return false;

        lastLeftXToggleTime = now;

        if (debugLeftXInput)
        {
            Debug.Log(
                $"[GripperCameraRecorder] Left X toggle detected. " +
                $"Button.One/LTouch={buttonOneLeft}, Button.Three/LTouch={buttonThreeLeft}, " +
                $"Button.Three/Touch={buttonThreeTouch}, Button.Three/Active={buttonThreeActive}, " +
                $"RawButton.X/LTouch={rawXLeft}, RawButton.X/Any={rawXAny}, " +
                $"connected={OVRInput.GetConnectedControllers()}"
            );
        }

        return true;
    }

    private bool KeyboardKeyDown(KeyCode key)
    {
        if (!enableKeyboardShortcuts || legacyInputUnavailable)
            return false;

        try
        {
            return Input.GetKeyDown(key);
        }
        catch (InvalidOperationException)
        {
            legacyInputUnavailable = true;
            Debug.LogWarning("[GripperCameraRecorder] Legacy keyboard shortcuts disabled because this project uses the new Input System only. Headset left X recording control still works.");
            return false;
        }
    }

    private IEnumerator CaptureFrame()
    {
        yield return new WaitForEndOfFrame();
        CaptureCurrentFrame();
    }

    private void CaptureCurrentFrame()
    {
        if (sourceCamera == null)
            return;

        bool markerWasActive = runtimeSceneMarker != null && runtimeSceneMarker.activeSelf;
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture temporaryTexture = null;

        if (markerWasActive)
            runtimeSceneMarker.SetActive(false);

        try
        {
            RenderTexture targetTexture = sourceCamera.targetTexture;
            bool createdTemporaryTarget = false;

            if (targetTexture == null)
            {
                temporaryTexture = RenderTexture.GetTemporary(width, height, depthBits, RenderTextureFormat.ARGB32);
                sourceCamera.targetTexture = temporaryTexture;
                targetTexture = temporaryTexture;
                createdTemporaryTarget = true;
            }

            // If the camera already renders the live preview RT each frame, avoid a second full render.
            if (forceRenderBeforeCapture || createdTemporaryTarget)
                sourceCamera.Render();
            RenderTexture.active = targetTexture;

            if (readbackTexture == null || readbackTexture.width != targetTexture.width || readbackTexture.height != targetTexture.height)
                readbackTexture = new Texture2D(targetTexture.width, targetTexture.height, TextureFormat.RGB24, false);

            readbackTexture.ReadPixels(new Rect(0, 0, targetTexture.width, targetTexture.height), 0, 0);
            readbackTexture.Apply(false);

            string fileName = $"gripper_camera_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Time.frameCount:D08}.png";
            string filePath = Path.Combine(sessionFolder, fileName);
            File.WriteAllBytes(filePath, readbackTexture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previousActive;

            if (temporaryTexture != null)
            {
                sourceCamera.targetTexture = null;
                RenderTexture.ReleaseTemporary(temporaryTexture);
            }

            if (markerWasActive)
                runtimeSceneMarker.SetActive(true);
        }
    }

    private void EnsureSessionFolder(string sessionName)
    {
        string root = GetRecordingRoot();
        sessionFolder = Path.Combine(root, sessionName);
        Directory.CreateDirectory(sessionFolder);
    }

    private string GetRecordingRoot()
    {
        return Path.Combine(Application.persistentDataPath, outputFolderName);
    }

    private void CreateOrUpdateFloatingPanel(bool rebuildFromSettings = false)
    {
        ResolveFloatingPanel();

        if (!createFloatingPanel)
        {
            if (floatingPanel != null)
                floatingPanel.SetActive(false);
#if UNITY_EDITOR
            MarkSceneDirtyInEditor();
#endif
            return;
        }

        Transform parent = floatingPanelFixedInScene ? null : ResolvePanelParent();
        if (!floatingPanelFixedInScene && parent == null)
        {
            Debug.LogWarning("[GripperCameraRecorder] Could not create floating panel because no headset/Main Camera transform was found.");
            return;
        }

        bool createdPanel = false;
        if (floatingPanel == null)
        {
            floatingPanel = new GameObject(FloatingPanelName, typeof(RectTransform));
            floatingPanel.hideFlags = HideFlags.None;
            createdPanel = true;
        }

        floatingPanel.SetActive(true);
        if (Application.isPlaying)
            EnsureEventSystem();

        RectTransform rect = floatingPanel.GetComponent<RectTransform>();
        if (createdPanel || rebuildFromSettings)
        {
            rect.SetParent(parent, false);
            ApplyFloatingPanelTransform(rect);
            rect.sizeDelta = floatingPanelSize;
            SetLayerRecursively(floatingPanel, 5);
        }
        ApplySavedFloatingPanelPoseOnce(rect);

        bool missingRequiredChildren = !HasRequiredFloatingPanelChildren();
        bool shouldBuildDefaultPanel =
            createdPanel ||
            rebuildFromSettings ||
            floatingPanel.transform.childCount == 0 ||
            (rebuildFloatingPanelWhenRequiredChildrenMissing && missingRequiredChildren);

        if (!createdPanel && !rebuildFromSettings && missingRequiredChildren && rebuildFloatingPanelWhenRequiredChildrenMissing)
            Debug.LogWarning("[GripperCameraRecorder] Floating panel was missing required UI children; rebuilding the default control panel.");

        EnsureFloatingPanelRootComponents(rect, shouldBuildDefaultPanel);
        if (!shouldBuildDefaultPanel)
        {
            ResolveFloatingPanelReferences();
            BindFloatingPanelButtons();
            UpdateFloatingPanel();
            return;
        }

        ClearFloatingPanelChildren(rect);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Font.CreateDynamicFontFromOSFont("Arial", 16);

        Text title = CreateText("Title", rect, font, "Control Panel", 20, TextAnchor.MiddleLeft);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -18f), new Vector2(220f, 32f));

        statusText = CreateText("Status", rect, font, "IDLE", 16, TextAnchor.MiddleRight);
        SetRect(statusText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -18f), new Vector2(150f, 32f));

        previewImage = CreateRawImage("Preview", rect);
        SetRect(previewImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -138f), previewSize);
        EnsureRuntimeRenderTexture();
        if (sourceCamera != null)
            previewImage.texture = sourceCamera.targetTexture;

        toggleButton = CreateButton("ToggleRecordingButton", rect, font, "X / REC", new Color(0.08f, 0.20f, 0.24f, 0.95f));
        SetRect(toggleButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(108f, 42f), new Vector2(170f, 46f));
        toggleButtonText = toggleButton.GetComponentInChildren<Text>();

        captureButton = CreateButton("CaptureFrameButton", rect, font, "Capture Frame", new Color(0.14f, 0.14f, 0.14f, 0.95f));
        SetRect(captureButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-108f, 42f), new Vector2(170f, 46f));

        Text hint = CreateText("Hint", rect, font, "Left X toggles recording", 13, TextAnchor.MiddleCenter);
        SetRect(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(360f, 24f));

        BindFloatingPanelButtons();
        UpdateFloatingPanel();
#if UNITY_EDITOR
        MarkSceneDirtyInEditor();
#endif
    }

    private void EnsureFloatingPanelRootComponents(RectTransform rect, bool applySettings)
    {
        Canvas canvas = floatingPanel.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = floatingPanel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;
        }
        else if (applySettings)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;
        }

        if (canvas.worldCamera == null)
            canvas.worldCamera = Camera.main;

        if (floatingPanel.GetComponent<GraphicRaycaster>() == null)
            floatingPanel.AddComponent<GraphicRaycaster>();

        Image background = floatingPanel.GetComponent<Image>();
        if (background == null)
        {
            background = floatingPanel.AddComponent<Image>();
            background.color = panelColor;
        }
        else if (applySettings)
        {
            background.color = panelColor;
        }

        if (applySettings)
            rect.sizeDelta = floatingPanelSize;
    }

    private void ResolveFloatingPanelReferences()
    {
        if (floatingPanel == null)
            return;

        statusText = FindFloatingPanelChildComponent<Text>("Status");
        previewImage = FindFloatingPanelChildComponent<RawImage>("Preview");
        toggleButton = FindFloatingPanelChildComponent<Button>("ToggleRecordingButton");
        captureButton = FindFloatingPanelChildComponent<Button>("CaptureFrameButton");
        toggleButtonText = toggleButton != null ? toggleButton.GetComponentInChildren<Text>() : null;
    }

    private bool HasRequiredFloatingPanelChildren()
    {
        if (floatingPanel == null)
            return false;

        return
            FindFloatingPanelChildComponent<Text>("Status") != null &&
            FindFloatingPanelChildComponent<RawImage>("Preview") != null &&
            FindFloatingPanelChildComponent<Button>("ToggleRecordingButton") != null &&
            FindFloatingPanelChildComponent<Button>("CaptureFrameButton") != null;
    }

    private T FindFloatingPanelChildComponent<T>(string childName) where T : Component
    {
        if (floatingPanel == null)
            return null;

        Transform child = floatingPanel.transform.Find(childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private void BindFloatingPanelButtons()
    {
        if (!Application.isPlaying)
            return;

        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(ToggleRecording);
            toggleButton.onClick.AddListener(ToggleRecording);
        }

        if (captureButton != null)
        {
            captureButton.onClick.RemoveListener(CaptureOneFrame);
            captureButton.onClick.AddListener(CaptureOneFrame);
        }
    }

    private static void ClearFloatingPanelChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
            DestroyObjectSafe(root.GetChild(i).gameObject);
    }

    private void ApplyFloatingPanelTransform(RectTransform rect)
    {
        if (floatingPanelFixedInScene)
        {
            rect.position = floatingPanelWorldPosition;
            rect.rotation = Quaternion.Euler(floatingPanelWorldEuler);
            if (floatingPanelFaceMainCameraOnCreate && Camera.main != null)
            {
                Vector3 fromCamera = rect.position - Camera.main.transform.position;
                if (fromCamera.sqrMagnitude > 0.0001f)
                    rect.rotation = Quaternion.LookRotation(fromCamera.normalized, Vector3.up);
            }
            rect.localScale = floatingPanelLocalScale;
            return;
        }

        rect.localPosition = floatingPanelLocalPosition;
        rect.localRotation = Quaternion.Euler(floatingPanelLocalEuler);
        rect.localScale = floatingPanelLocalScale;
    }

    private Transform ResolvePanelParent()
    {
        if (floatingPanelParent != null)
            return floatingPanelParent;

        GameObject headset =
            GameObject.Find("OVRCameraRig/TrackingSpace/CenterEyeAnchor") ??
            GameObject.Find("CenterEyeAnchor");
        if (headset != null)
        {
            floatingPanelParent = headset.transform;
            return floatingPanelParent;
        }

        if (Camera.main != null)
        {
            floatingPanelParent = Camera.main.transform;
            return floatingPanelParent;
        }

        return null;
    }

    private void UpdateFloatingPanel()
    {
        EnsureRuntimeRenderTexture();

        if (previewImage != null && sourceCamera != null && previewImage.texture != sourceCamera.targetTexture)
            previewImage.texture = sourceCamera.targetTexture;

        if (statusText != null)
        {
            statusText.text = recording ? "REC" : "IDLE";
            statusText.color = recording ? recordingColor : idleColor;
        }

        if (toggleButtonText != null)
            toggleButtonText.text = recording ? "Stop" : "Record";
    }

    private void UpdateFloatingPanelControllerDrag()
    {
        if (!enableFloatingPanelControllerDrag || !floatingPanelFixedInScene || floatingPanel == null || !floatingPanel.activeSelf)
        {
            floatingPanelDragActive = false;
            floatingPanelDragButtonWasHeld = false;
            return;
        }

        RectTransform rect = floatingPanel.GetComponent<RectTransform>();
        Transform controller = ResolveFloatingPanelDragController();
        if (rect == null || controller == null)
            return;

        bool buttonHeld = IsFloatingPanelDragButtonHeld();
        bool teleopDisengaged = !IsRightGripTeleopHeld();
        bool canDrag = !requireTeleopDisengagedForPanelDrag || teleopDisengaged;

        if (!canDrag)
        {
            if (floatingPanelDragActive)
                SaveFloatingPanelPose(rect);

            floatingPanelDragActive = false;
            floatingPanelDragButtonWasHeld = buttonHeld;
            return;
        }

        if (buttonHeld && !floatingPanelDragButtonWasHeld && PanelRayHits(rect, controller))
            BeginFloatingPanelDrag(rect, controller);

        if (floatingPanelDragActive && buttonHeld)
        {
            rect.position = controller.position + (controller.rotation * floatingPanelDragLocalOffsetFromController);
            rect.rotation = floatingPanelDragWorldRotation;
        }

        if (floatingPanelDragActive && !buttonHeld)
        {
            floatingPanelDragActive = false;
            SaveFloatingPanelPose(rect);
        }

        floatingPanelDragButtonWasHeld = buttonHeld;
    }

    private void BeginFloatingPanelDrag(RectTransform rect, Transform controller)
    {
        floatingPanelDragActive = true;
        floatingPanelDragLocalOffsetFromController = Quaternion.Inverse(controller.rotation) * (rect.position - controller.position);
        floatingPanelDragWorldRotation = rect.rotation;
    }

    private bool PanelRayHits(RectTransform rect, Transform controller)
    {
        Ray ray = new Ray(controller.position, controller.forward);
        Plane panelPlane = new Plane(rect.forward, rect.position);
        if (!panelPlane.Raycast(ray, out float distance))
            return false;

        if (distance < 0f || distance > Mathf.Max(0.05f, floatingPanelDragRayMaxDistance))
            return false;

        Vector3 hitPoint = ray.GetPoint(distance);
        Vector3 localPoint = rect.InverseTransformPoint(hitPoint);
        return rect.rect.Contains(new Vector2(localPoint.x, localPoint.y));
    }

    private Transform ResolveFloatingPanelDragController()
    {
        if (floatingPanelDragController == PanelDragController.Left)
        {
            if (cachedLeftControllerTransform == null)
            {
                GameObject left =
                    GameObject.Find("OVRCameraRig/TrackingSpace/LeftControllerAnchor") ??
                    GameObject.Find("LeftControllerAnchor");
                if (left != null)
                    cachedLeftControllerTransform = left.transform;
            }
            return cachedLeftControllerTransform;
        }

        if (cachedRightControllerTransform == null)
        {
            GameObject right =
                GameObject.Find("OVRCameraRig/TrackingSpace/RightControllerAnchor") ??
                GameObject.Find("RightControllerAnchor");
            if (right != null)
                cachedRightControllerTransform = right.transform;
        }
        return cachedRightControllerTransform;
    }

    private bool IsFloatingPanelDragButtonHeld()
    {
        OVRInput.Controller controller = floatingPanelDragController == PanelDragController.Left
            ? OVRInput.Controller.LTouch
            : OVRInput.Controller.RTouch;

        if (floatingPanelDragButton == PanelDragButton.Trigger)
            return OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controller) >= Mathf.Max(0.05f, floatingPanelDragButtonThreshold);

        if (floatingPanelDragButton == PanelDragButton.Grip)
            return OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, controller) >= Mathf.Max(0.05f, floatingPanelDragButtonThreshold);

        if (floatingPanelDragController == PanelDragController.Left)
        {
            return OVRInput.Get(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch) ||
                   OVRInput.Get(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.Touch) ||
                   OVRInput.Get(OVRInput.RawButton.LThumbstick);
        }

        return OVRInput.Get(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch) ||
               OVRInput.Get(OVRInput.Button.SecondaryThumbstick, OVRInput.Controller.Touch) ||
               OVRInput.Get(OVRInput.RawButton.RThumbstick);
    }

    private bool IsRightGripTeleopHeld()
    {
        return OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch) >= Mathf.Max(0.05f, rightGripTeleopDragBlockThreshold);
    }

    private void ApplySavedFloatingPanelPoseOnce(RectTransform rect)
    {
        if (!Application.isPlaying || !floatingPanelFixedInScene || !persistFloatingPanelDraggedPose || floatingPanelSavedPoseLoaded)
            return;

        floatingPanelSavedPoseLoaded = true;

        if (!PlayerPrefs.HasKey(FloatingPanelPrefsPrefix + "px"))
            return;

        rect.position = new Vector3(
            PlayerPrefs.GetFloat(FloatingPanelPrefsPrefix + "px", rect.position.x),
            PlayerPrefs.GetFloat(FloatingPanelPrefsPrefix + "py", rect.position.y),
            PlayerPrefs.GetFloat(FloatingPanelPrefsPrefix + "pz", rect.position.z)
        );
        rect.rotation = Quaternion.Euler(
            PlayerPrefs.GetFloat(FloatingPanelPrefsPrefix + "rx", rect.eulerAngles.x),
            PlayerPrefs.GetFloat(FloatingPanelPrefsPrefix + "ry", rect.eulerAngles.y),
            PlayerPrefs.GetFloat(FloatingPanelPrefsPrefix + "rz", rect.eulerAngles.z)
        );
    }

    private void SaveFloatingPanelPose(RectTransform rect)
    {
        if (!persistFloatingPanelDraggedPose || rect == null)
            return;

        Vector3 pos = rect.position;
        Vector3 euler = rect.eulerAngles;
        PlayerPrefs.SetFloat(FloatingPanelPrefsPrefix + "px", pos.x);
        PlayerPrefs.SetFloat(FloatingPanelPrefsPrefix + "py", pos.y);
        PlayerPrefs.SetFloat(FloatingPanelPrefsPrefix + "pz", pos.z);
        PlayerPrefs.SetFloat(FloatingPanelPrefsPrefix + "rx", euler.x);
        PlayerPrefs.SetFloat(FloatingPanelPrefsPrefix + "ry", euler.y);
        PlayerPrefs.SetFloat(FloatingPanelPrefsPrefix + "rz", euler.z);
        PlayerPrefs.Save();
    }

    private static Text CreateText(string name, Transform parent, Font font, string text, int fontSize, TextAnchor alignment)
    {
        GameObject go = new GameObject(name);
        SetLayerRecursively(go, 5);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        Text uiText = go.AddComponent<Text>();
        uiText.font = font;
        uiText.text = text;
        uiText.fontSize = fontSize;
        uiText.color = Color.white;
        uiText.alignment = alignment;
        return uiText;
    }

    private static RawImage CreateRawImage(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        SetLayerRecursively(go, 5);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        RawImage image = go.AddComponent<RawImage>();
        image.color = Color.white;
        return image;
    }

    private static Button CreateButton(string name, Transform parent, Font font, string label, Color color)
    {
        GameObject go = new GameObject(name);
        SetLayerRecursively(go, 5);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        Button button = go.AddComponent<Button>();

        Text text = CreateText("Text", rect, font, label, 15, TextAnchor.MiddleCenter);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return button;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private static void EnsureEventSystem()
    {
        EventSystem existing = FindObjectOfType<EventSystem>();
        if (existing != null)
        {
            if (existing.GetComponent<BaseInputModule>() == null)
                existing.gameObject.AddComponent<InputSystemUIInputModule>();
            return;
        }

        GameObject go = new GameObject("GripperCameraUI_EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private void CreateOrUpdateRuntimeSceneMarker(bool rebuildFromSettings = false)
    {
        ResolveRuntimeSceneMarker();

        if (!createRuntimeSceneMarker || !showSceneMarker)
        {
            if (runtimeSceneMarker != null)
                runtimeSceneMarker.SetActive(false);
#if UNITY_EDITOR
            MarkSceneDirtyInEditor();
#endif
            return;
        }

        bool createdMarker = false;
        if (runtimeSceneMarker == null)
        {
            runtimeSceneMarker = new GameObject(RuntimeMarkerName);
            runtimeSceneMarker.transform.SetParent(transform, false);
            runtimeSceneMarker.transform.localPosition = Vector3.zero;
            runtimeSceneMarker.transform.localRotation = Quaternion.identity;
            runtimeSceneMarker.transform.localScale = Vector3.one;
            createdMarker = true;
        }

        runtimeSceneMarker.hideFlags = HideFlags.None;
        runtimeSceneMarker.SetActive(true);
        runtimeSceneMarker.layer = runtimeSceneMarkerLayer;

        bool shouldBuildDefaultMarker = createdMarker || rebuildFromSettings || runtimeSceneMarker.transform.childCount == 0;
        if (!shouldBuildDefaultMarker)
        {
            EnsureExistingMarkerHasMaterial();
            return;
        }

        ClearMarkerChildren(runtimeSceneMarker.transform);
        SetLayerRecursively(runtimeSceneMarker, runtimeSceneMarkerLayer);
        if (runtimeMarkerMaterial == null)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Standard");

            if (shader == null)
            {
                Debug.LogWarning("[GripperCameraRecorder] Could not find a marker shader; runtime camera marker may not render.");
                return;
            }

            runtimeMarkerMaterial = new Material(shader)
            {
                name = "GripperDataCameraMarker_Material"
            };
            ApplyMaterialColor(runtimeMarkerMaterial, markerColor);
        }
        else
        {
            ApplyMaterialColor(runtimeMarkerMaterial, markerColor);
        }

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "CameraBody";
        body.transform.SetParent(runtimeSceneMarker.transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = markerBoxSize;
        SetLayerRecursively(body, runtimeSceneMarkerLayer);

        Collider collider = body.GetComponent<Collider>();
        if (collider != null)
            DestroyObjectSafe(collider);

        Renderer renderer = body.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = runtimeMarkerMaterial;

        Vector3 forward = Vector3.forward * Mathf.Max(0.01f, markerForwardLength);
        float half = Mathf.Max(0.005f, markerFrustumHalfSize);
        Vector3 topLeft = forward + new Vector3(-half, half, 0f);
        Vector3 topRight = forward + new Vector3(half, half, 0f);
        Vector3 bottomLeft = forward + new Vector3(-half, -half, 0f);
        Vector3 bottomRight = forward + new Vector3(half, -half, 0f);

        CreateMarkerLine(
            "Frustum",
            new[]
            {
                Vector3.zero, topLeft,
                Vector3.zero, topRight,
                Vector3.zero, bottomRight,
                Vector3.zero, bottomLeft,
                topLeft, topRight, bottomRight, bottomLeft, topLeft
            },
            runtimeSceneMarker.transform
        );

#if UNITY_EDITOR
        MarkSceneDirtyInEditor();
#endif
    }

    private void CreateMarkerLine(string name, Vector3[] points, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = runtimeSceneMarkerLayer;

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = points.Length;
        line.SetPositions(points);
        line.startWidth = markerLineWidth;
        line.endWidth = markerLineWidth;
        line.startColor = markerColor;
        line.endColor = markerColor;
        line.material = runtimeMarkerMaterial;
    }

    private static void ApplyMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (material.HasProperty("_TintColor"))
            material.SetColor("_TintColor", color);
    }

    private void EnsureExistingMarkerHasMaterial()
    {
        if (runtimeSceneMarker == null)
            return;

        Renderer[] renderers = runtimeSceneMarker.GetComponentsInChildren<Renderer>(true);
        LineRenderer[] lines = runtimeSceneMarker.GetComponentsInChildren<LineRenderer>(true);

        bool needsMaterial = false;
        foreach (Renderer renderer in renderers)
            needsMaterial |= renderer != null && renderer.sharedMaterial == null;
        foreach (LineRenderer line in lines)
            needsMaterial |= line != null && line.sharedMaterial == null;

        if (!needsMaterial)
            return;

        EnsureRuntimeMarkerMaterial();

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && renderer.sharedMaterial == null)
                renderer.sharedMaterial = runtimeMarkerMaterial;
        }

        foreach (LineRenderer line in lines)
        {
            if (line != null && line.sharedMaterial == null)
                line.sharedMaterial = runtimeMarkerMaterial;
        }
    }

    private void EnsureRuntimeMarkerMaterial()
    {
        if (runtimeMarkerMaterial != null)
            return;

        Shader shader =
            Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Unlit/Color") ??
            Shader.Find("Sprites/Default") ??
            Shader.Find("Standard");

        if (shader == null)
        {
            Debug.LogWarning("[GripperCameraRecorder] Could not find a marker shader; existing camera marker may not render.");
            return;
        }

        runtimeMarkerMaterial = new Material(shader)
        {
            name = "GripperDataCameraMarker_Material"
        };
        ApplyMaterialColor(runtimeMarkerMaterial, markerColor);
    }

    private static void ClearMarkerChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
            DestroyObjectSafe(root.GetChild(i).gameObject);
    }

    private void UpdateRuntimeSceneMarkerVisibility()
    {
        ResolveRuntimeSceneMarker();

        if (runtimeSceneMarker == null)
        {
            if (createRuntimeSceneMarker && showSceneMarker)
                CreateOrUpdateRuntimeSceneMarker();
            return;
        }

        runtimeSceneMarker.SetActive(createRuntimeSceneMarker && showSceneMarker);
    }

    private void OnDestroy()
    {
        if (Application.isPlaying && runtimeSceneMarker != null)
            Destroy(runtimeSceneMarker);

        if (Application.isPlaying && runtimeMarkerMaterial != null)
            DestroyObjectSafe(runtimeMarkerMaterial);

        if (readbackTexture != null)
            DestroyObjectSafe(readbackTexture);

        if (ownsRenderTexture && runtimeRenderTexture != null)
        {
            if (sourceCamera != null && sourceCamera.targetTexture == runtimeRenderTexture)
                sourceCamera.targetTexture = null;

            runtimeRenderTexture.Release();
            DestroyObjectSafe(runtimeRenderTexture);
        }
    }

    private void ResolveSourceCamera()
    {
        if (sourceCamera == null)
            sourceCamera = GetComponent<Camera>();
    }

    private void EnsureRuntimeRenderTexture()
    {
        if (!Application.isPlaying || !assignRuntimeRenderTexture || sourceCamera == null)
            return;

        if (sourceCamera.targetTexture != null)
        {
            width = sourceCamera.targetTexture.width;
            height = sourceCamera.targetTexture.height;
            return;
        }

        runtimeRenderTexture = new RenderTexture(width, height, depthBits, RenderTextureFormat.ARGB32)
        {
            name = "GripperDataCamera_RT"
        };
        runtimeRenderTexture.Create();
        sourceCamera.targetTexture = runtimeRenderTexture;
        ownsRenderTexture = true;
    }

    private void ResolveRuntimeSceneMarker()
    {
        if (runtimeSceneMarker != null)
            return;

        Transform existing = transform.Find(RuntimeMarkerName);
        if (existing != null)
            runtimeSceneMarker = existing.gameObject;
    }

    private void ResolveFloatingPanel()
    {
        if (floatingPanel != null)
            return;

        GameObject existing = GameObject.Find(FloatingPanelName);
        if (existing != null)
            floatingPanel = existing;
    }

    private static void DestroyObjectSafe(UnityEngine.Object obj)
    {
        if (obj == null)
            return;

        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }

#if UNITY_EDITOR
    private void MarkSceneDirtyInEditor()
    {
        if (Application.isPlaying || !gameObject.scene.IsValid())
            return;

        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    private void QueueEditorRefresh(bool forceMarkerRebuild, bool forcePanelRebuild)
    {
        editorMarkerForceRebuildQueued |= forceMarkerRebuild;
        editorFloatingPanelForceRebuildQueued |= forcePanelRebuild;

        if (editorMarkerRefreshQueued)
            return;

        editorMarkerRefreshQueued = true;
        EditorApplication.delayCall += RefreshEditorObjectsAfterValidate;
    }

    private void RefreshEditorObjectsAfterValidate()
    {
        editorMarkerRefreshQueued = false;

        if (this == null || Application.isPlaying)
            return;

        bool forceMarkerRebuild = editorMarkerForceRebuildQueued;
        bool forcePanelRebuild = editorFloatingPanelForceRebuildQueued;
        editorMarkerForceRebuildQueued = false;
        editorFloatingPanelForceRebuildQueued = false;
        ResolveSourceCamera();
        CreateOrUpdateRuntimeSceneMarker(forceMarkerRebuild);
        CreateOrUpdateFloatingPanel(forcePanelRebuild);
    }
#endif

    private void OnDrawGizmos()
    {
        if (!showSceneMarker)
            return;

        DrawCameraMarker();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showSceneMarker)
            return;

        DrawCameraMarker();
    }

    private void DrawCameraMarker()
    {
        Color previousColor = Gizmos.color;
        Matrix4x4 previousMatrix = Gizmos.matrix;

        Gizmos.color = markerColor;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        Gizmos.DrawWireCube(Vector3.zero, markerBoxSize);

        Vector3 forward = Vector3.forward * Mathf.Max(0.01f, markerForwardLength);

        float half = Mathf.Max(0.005f, markerFrustumHalfSize);
        Vector3 topLeft = forward + new Vector3(-half, half, 0f);
        Vector3 topRight = forward + new Vector3(half, half, 0f);
        Vector3 bottomLeft = forward + new Vector3(-half, -half, 0f);
        Vector3 bottomRight = forward + new Vector3(half, -half, 0f);

        Gizmos.DrawLine(Vector3.zero, topLeft);
        Gizmos.DrawLine(Vector3.zero, topRight);
        Gizmos.DrawLine(Vector3.zero, bottomLeft);
        Gizmos.DrawLine(Vector3.zero, bottomRight);
        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
        Gizmos.DrawLine(bottomLeft, topLeft);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
