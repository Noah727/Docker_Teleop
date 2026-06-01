using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public class RobotViewpointController : MonoBehaviour
{
    public static RobotViewpointController ActiveInstance { get; private set; }

    [Header("Robot View Camera")]
    public bool createRobotBaseViewCamera = true;
    public string cameraName = "RobotBaseViewCamera";
    public string robotBaseObjectName = "base_link";
    public string robotRootObjectName = "ur5e";
    public Transform robotBase;
    public Vector3 cameraLocalOffset = new Vector3(0.0f, 1.15f, 0.0f);
    public Vector3 lookAtLocalPoint = new Vector3(0.0f, 0.0f, 0.25f);
    [Range(35f, 140f)] public float fieldOfView = 95f;
    public int renderWidth = 960;
    public int renderHeight = 720;
    public int depthBits = 24;

    [Header("Robot View Screen")]
    public bool createRobotViewScreen = true;
    public string screenName = "RobotBaseViewWindow";
    public Vector3 screenWorldPosition = new Vector3(0.95f, 1.35f, 1.25f);
    public Vector3 screenWorldEuler = new Vector3(0f, -35f, 0f);
    public Vector3 screenWorldScale = new Vector3(0.0013f, 0.0013f, 0.0013f);
    public Vector2 screenSize = new Vector2(360f, 285f);
    public Color screenBackgroundColor = new Color(0.02f, 0.025f, 0.025f, 0.72f);
    public bool makeScreenDraggable = true;
    public Color dragHandleColor = new Color(1.0f, 0.92f, 0.72f, 0.70f);

    [Header("Controller/Hand Proxy")]
    public bool createControllerProxy = true;
    public string proxyName = "RobotView_RightControllerProxy";
    public Transform rightControllerTransform;
    public Color proxyColor = new Color(1.0f, 0.62f, 0.16f, 1.0f);
    public float proxySphereRadius = 0.025f;
    public float proxyForwardLength = 0.14f;
    public float proxyLineWidth = 0.006f;

    public Camera RobotViewCamera { get; private set; }
    public RenderTexture RobotViewTexture { get; private set; }
    public Transform ControllerProxy => controllerProxy != null ? controllerProxy.transform : null;
    public string LastStatus { get; private set; } = "Not initialized";

    private GameObject controllerProxy;
    private GameObject robotViewScreen;
    private RawImage robotViewImage;
    private LineRenderer proxyForwardLine;
    private Material proxyMaterial;

    private void Awake()
    {
        ActiveInstance = this;
    }

    private void Start()
    {
        ResolveReferences();
        EnsureRobotViewCamera();
        EnsureRobotViewScreen();
        EnsureControllerProxy();
        UpdateStatus();
    }

    private void LateUpdate()
    {
        ResolveReferences();
        UpdateRobotViewCameraPose();
        UpdateRobotViewScreen();
        UpdateControllerProxyPose();
        UpdateStatus();
    }

    public void ResolveReferences()
    {
        if (robotBase == null)
        {
            GameObject baseObject = GameObject.Find(robotBaseObjectName) ?? GameObject.Find(robotRootObjectName);
            if (baseObject != null)
                robotBase = baseObject.transform;
        }

        if (rightControllerTransform == null)
        {
            GameObject right = GameObject.Find("OVRCameraRig/TrackingSpace/RightControllerAnchor") ?? GameObject.Find("RightControllerAnchor");
            if (right != null)
                rightControllerTransform = right.transform;
        }
    }

    public void EnsureRobotViewCamera()
    {
        if (!createRobotBaseViewCamera)
            return;

        if (RobotViewCamera == null)
        {
            GameObject cameraObject = GameObject.Find(cameraName);
            if (cameraObject == null)
                cameraObject = new GameObject(cameraName);

            RobotViewCamera = cameraObject.GetComponent<Camera>();
            if (RobotViewCamera == null)
                RobotViewCamera = cameraObject.AddComponent<Camera>();
        }

        if (RobotViewTexture == null || RobotViewTexture.width != renderWidth || RobotViewTexture.height != renderHeight)
        {
            if (RobotViewTexture != null)
                RobotViewTexture.Release();

            RobotViewTexture = new RenderTexture(Mathf.Max(64, renderWidth), Mathf.Max(64, renderHeight), Mathf.Max(0, depthBits), RenderTextureFormat.ARGB32)
            {
                name = "RobotBaseView_RT"
            };
            RobotViewTexture.Create();
        }

        RobotViewCamera.targetTexture = RobotViewTexture;
        RobotViewCamera.fieldOfView = fieldOfView;
        RobotViewCamera.nearClipPlane = 0.02f;
        RobotViewCamera.farClipPlane = 10f;
        RobotViewCamera.clearFlags = CameraClearFlags.SolidColor;
        RobotViewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
    }

    public void EnsureRobotViewScreen()
    {
        if (!createRobotViewScreen || RobotViewTexture == null)
            return;

        if (robotViewScreen == null)
        {
            robotViewScreen = GameObject.Find(screenName);
            if (robotViewScreen == null)
                robotViewScreen = new GameObject(screenName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
        }

        RectTransform rect = robotViewScreen.GetComponent<RectTransform>();
        rect.sizeDelta = screenSize;
        robotViewScreen.transform.position = screenWorldPosition;
        robotViewScreen.transform.rotation = Quaternion.Euler(screenWorldEuler);
        robotViewScreen.transform.localScale = screenWorldScale;
        SetLayerRecursively(robotViewScreen, 5);

        Canvas canvas = robotViewScreen.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 54;

        Image background = robotViewScreen.GetComponent<Image>();
        background.color = screenBackgroundColor;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        Transform title = robotViewScreen.transform.Find("Title");
        if (title == null)
        {
            GameObject titleObject = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            titleObject.transform.SetParent(robotViewScreen.transform, false);
            title = titleObject.transform;
            Text titleText = titleObject.GetComponent<Text>();
            titleText.font = font;
            titleText.text = "Robot View";
            titleText.fontSize = 18;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.color = new Color(0.86f, 0.96f, 1.0f, 1.0f);
        }
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -8f);
        titleRect.sizeDelta = new Vector2(-28f, 28f);

        Transform preview = robotViewScreen.transform.Find("Preview");
        if (preview == null)
        {
            GameObject previewObject = new GameObject("Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            previewObject.transform.SetParent(robotViewScreen.transform, false);
            preview = previewObject.transform;
        }
        RectTransform previewRect = preview.GetComponent<RectTransform>();
        previewRect.anchorMin = new Vector2(0f, 0f);
        previewRect.anchorMax = new Vector2(1f, 1f);
        previewRect.offsetMin = new Vector2(18f, 18f);
        previewRect.offsetMax = new Vector2(-18f, -42f);

        robotViewImage = preview.GetComponent<RawImage>();
        robotViewImage.color = Color.white;
        robotViewImage.texture = RobotViewTexture;

        EnsureDragHandle(rect);
        EnsureDraggableWindow(rect);
    }

    private void EnsureDragHandle(RectTransform screenRect)
    {
        UICornerDragHandle.Ensure(
            robotViewScreen.transform,
            "DragHandle",
            dragHandleColor,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-10f, 10f),
            new Vector2(42f, 42f));
    }

    private void EnsureDraggableWindow(RectTransform screenRect)
    {
        if (!makeScreenDraggable)
            return;

        VRDraggableWindow dragger = robotViewScreen.GetComponent<VRDraggableWindow>();
        if (dragger == null)
            dragger = robotViewScreen.AddComponent<VRDraggableWindow>();

        dragger.windowRoot = screenRect;
        dragger.handleNameContains = "DragHandle";
        dragger.requireHandleHit = true;
        dragger.allowWholeWindowFallback = false;
        dragger.dragController = VRDraggableWindow.DragController.Left;
        dragger.dragButton = VRDraggableWindow.DragButton.Trigger;
        dragger.requireRightGripReleased = true;
    }

    public void EnsureControllerProxy()
    {
        if (!createControllerProxy)
            return;

        if (controllerProxy == null)
        {
            GameObject existing = GameObject.Find(proxyName);
            controllerProxy = existing != null ? existing : new GameObject(proxyName);
        }

        EnsureProxyMaterial();

        Transform body = controllerProxy.transform.Find("Body");
        if (body == null)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Body";
            sphere.transform.SetParent(controllerProxy.transform, false);
            DestroyIfPresent(sphere.GetComponent<Collider>());
            body = sphere.transform;
        }
        body.localPosition = Vector3.zero;
        body.localRotation = Quaternion.identity;
        body.localScale = Vector3.one * Mathf.Max(0.005f, proxySphereRadius * 2f);

        Renderer renderer = body.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = proxyMaterial;

        if (proxyForwardLine == null)
        {
            Transform line = controllerProxy.transform.Find("ForwardLine");
            if (line == null)
            {
                GameObject lineObject = new GameObject("ForwardLine");
                lineObject.transform.SetParent(controllerProxy.transform, false);
                proxyForwardLine = lineObject.AddComponent<LineRenderer>();
            }
            else
            {
                proxyForwardLine = line.GetComponent<LineRenderer>() ?? line.gameObject.AddComponent<LineRenderer>();
            }
        }

        proxyForwardLine.useWorldSpace = false;
        proxyForwardLine.positionCount = 2;
        proxyForwardLine.SetPosition(0, Vector3.zero);
        proxyForwardLine.SetPosition(1, Vector3.forward * Mathf.Max(0.01f, proxyForwardLength));
        proxyForwardLine.startWidth = proxyLineWidth;
        proxyForwardLine.endWidth = proxyLineWidth;
        proxyForwardLine.startColor = proxyColor;
        proxyForwardLine.endColor = proxyColor;
        proxyForwardLine.material = proxyMaterial;
    }

    private void UpdateRobotViewScreen()
    {
        if (createRobotViewScreen && robotViewScreen == null)
            EnsureRobotViewScreen();
        if (robotViewImage != null && robotViewImage.texture != RobotViewTexture)
            robotViewImage.texture = RobotViewTexture;
    }

    private void UpdateRobotViewCameraPose()
    {
        if (RobotViewCamera == null || robotBase == null)
            return;

        Vector3 position = robotBase.TransformPoint(cameraLocalOffset);
        Vector3 lookAt = robotBase.TransformPoint(lookAtLocalPoint);
        Vector3 forward = lookAt - position;
        if (forward.sqrMagnitude < 1e-6f)
            forward = Vector3.down;

        Vector3 up = robotBase.forward.sqrMagnitude > 1e-6f ? robotBase.forward : Vector3.forward;
        RobotViewCamera.transform.position = position;
        RobotViewCamera.transform.rotation = Quaternion.LookRotation(forward.normalized, up.normalized);
    }

    private void UpdateControllerProxyPose()
    {
        if (controllerProxy == null)
            return;

        bool valid = rightControllerTransform != null && rightControllerTransform.gameObject.activeInHierarchy;
        controllerProxy.SetActive(createControllerProxy && valid);
        if (!valid)
            return;

        controllerProxy.transform.position = rightControllerTransform.position;
        controllerProxy.transform.rotation = rightControllerTransform.rotation;
    }

    private void UpdateStatus()
    {
        LastStatus = $"robotBase={(robotBase ? robotBase.name : "NULL")}, camera={(RobotViewCamera ? "ON" : "OFF")}, screen={(robotViewScreen ? "ON" : "OFF")}, proxy={(controllerProxy && controllerProxy.activeSelf ? "ON" : "OFF")}";
    }

    private void EnsureProxyMaterial()
    {
        if (proxyMaterial != null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogWarning("[RobotViewpointController] Could not find a shader for the controller proxy.");
            return;
        }
        proxyMaterial = new Material(shader) { name = "RobotViewProxy_Material" };
        if (proxyMaterial.HasProperty("_BaseColor"))
            proxyMaterial.SetColor("_BaseColor", proxyColor);
        if (proxyMaterial.HasProperty("_Color"))
            proxyMaterial.SetColor("_Color", proxyColor);
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private static void DestroyIfPresent(Object obj)
    {
        if (obj == null)
            return;
        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }

    private void OnDestroy()
    {
        if (RobotViewTexture != null)
        {
            RobotViewTexture.Release();
            DestroyIfPresent(RobotViewTexture);
        }

        if (proxyMaterial != null)
            DestroyIfPresent(proxyMaterial);
    }
}
