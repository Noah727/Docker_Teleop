using UnityEngine;

[DefaultExecutionOrder(530)]
public class ControllerRayVisual : MonoBehaviour
{
    public enum RayController
    {
        Left,
        Right
    }

    [System.Serializable]
    public class RaySpec
    {
        public RayController controller = RayController.Right;
        public bool enabled = true;
        public string rayObjectName = "RightControllerAimRay";
        public Color idleColor = new Color(0.25f, 0.85f, 1.0f, 0.24f);
        public Color hitColor = new Color(0.25f, 1.0f, 0.35f, 0.45f);
    }

    [Header("Rays")]
    public RaySpec leftRay = new RaySpec
    {
        controller = RayController.Left,
        enabled = true,
        rayObjectName = "LeftControllerAimRay",
        idleColor = new Color(0.55f, 0.80f, 1.0f, 0.22f),
        hitColor = new Color(0.25f, 1.0f, 0.35f, 0.45f),
    };

    public RaySpec rightRay = new RaySpec
    {
        controller = RayController.Right,
        enabled = true,
        rayObjectName = "RightControllerAimRay",
        idleColor = new Color(0.25f, 0.85f, 1.0f, 0.22f),
        hitColor = new Color(1.0f, 0.85f, 0.20f, 0.45f),
    };

    [Header("Visibility")]
    public float maxDistance = 4.0f;
    public float lineWidth = 0.004f;
    public float hitDotScale = 0.025f;
    public bool showOnlyOnInteractionIntent = true;
    [Range(0.01f, 0.95f)] public float indexTriggerIntentThreshold = 0.10f;
    public bool showOnGripIntent = false;
    [Range(0.01f, 0.95f)] public float handTriggerIntentThreshold = 0.20f;
    [Tooltip("After the trigger/grip shows the ray, keep it visible for this long so the user can see where they are pointing.")]
    public float lingerAfterInteractionIntentSec = 5.0f;
    public bool hideLeftRayWhileTeleopHeld = true;
    public bool hideRightRayWhileTeleopHeld = true;
    public float leftGripTeleopThreshold = 0.55f;
    public float rightGripTeleopThreshold = 0.55f;
    public string[] interactableNameContains =
    {
        "WorkspaceDragHandle",
        "WorkspaceRotateHandle",
        "DragHandle",
        "ToggleRecordingButton",
        "CaptureFrameButton",
        "FloatingCameraBody"
    };

    public string LastStatus { get; private set; } = "not initialized";

    private RayRuntime leftRuntime;
    private RayRuntime rightRuntime;
    private Material lineMaterial;
    private Material dotMaterial;

    private class RayRuntime
    {
        public Transform controllerTransform;
        public GameObject root;
        public LineRenderer line;
        public Transform dot;
        public string hitName = "none";
        public float visibleUntilTime;
    }

    private void Start()
    {
        EnsureMaterials();
        EnsureRay(leftRay, ref leftRuntime);
        EnsureRay(rightRay, ref rightRuntime);
    }

    private void Update()
    {
        EnsureMaterials();
        UpdateRay(leftRay, ref leftRuntime);
        UpdateRay(rightRay, ref rightRuntime);
        LastStatus = $"left={Status(leftRuntime)}, right={Status(rightRuntime)}";
    }

    private void UpdateRay(RaySpec spec, ref RayRuntime runtime)
    {
        if (runtime == null)
            EnsureRay(spec, ref runtime);

        if (runtime == null || runtime.root == null)
            return;

        ResolveController(spec, runtime);
        bool leftTeleopHeld = GetGripValue(RayController.Left) >= leftGripTeleopThreshold;
        bool rightTeleopHeld = GetGripValue(RayController.Right) >= rightGripTeleopThreshold;
        bool visible = spec.enabled && runtime.controllerTransform != null && IsControllerConnected(spec.controller);
        if (HasInteractionIntent(spec.controller))
            runtime.visibleUntilTime = Time.unscaledTime + Mathf.Max(0.0f, lingerAfterInteractionIntentSec);
        if (showOnlyOnInteractionIntent && Time.unscaledTime > runtime.visibleUntilTime)
            visible = false;
        if (spec.controller == RayController.Left && hideLeftRayWhileTeleopHeld && leftTeleopHeld)
            visible = false;
        if (spec.controller == RayController.Right && hideRightRayWhileTeleopHeld && rightTeleopHeld)
            visible = false;

        runtime.root.SetActive(visible);
        if (!visible)
        {
            runtime.hitName = "hidden";
            return;
        }

        Vector3 start = runtime.controllerTransform.position;
        Vector3 direction = runtime.controllerTransform.forward;
        bool hit = TryFindInteractableHit(start, direction, out Vector3 end, out string hitName);
        runtime.hitName = hit ? hitName : "none";

        if (!hit)
            end = start + direction * Mathf.Max(0.1f, maxDistance);

        Color color = hit ? spec.hitColor : spec.idleColor;
        runtime.line.startColor = color;
        runtime.line.endColor = color;
        runtime.line.startWidth = lineWidth;
        runtime.line.endWidth = lineWidth * 0.55f;
        runtime.line.SetPosition(0, start);
        runtime.line.SetPosition(1, end);

        runtime.dot.gameObject.SetActive(hit);
        if (hit)
        {
            runtime.dot.position = end;
            runtime.dot.localScale = Vector3.one * hitDotScale;
            Renderer renderer = runtime.dot.GetComponent<Renderer>();
            if (renderer != null)
                SetMaterialColor(renderer.sharedMaterial, color);
        }
    }

    private bool TryFindInteractableHit(Vector3 start, Vector3 direction, out Vector3 hitPoint, out string hitName)
    {
        Ray ray = new Ray(start, direction);
        float bestDistance = Mathf.Max(0.05f, maxDistance) + 1.0f;
        hitPoint = start + direction * Mathf.Max(0.1f, maxDistance);
        hitName = "none";
        bool found = false;

        RaycastHit[] physicsHits = Physics.RaycastAll(ray, Mathf.Max(0.05f, maxDistance), ~0, QueryTriggerInteraction.Collide);
        if (physicsHits != null)
        {
            foreach (RaycastHit hit in physicsHits)
            {
                if (hit.collider == null || hit.distance > bestDistance)
                    continue;

                Transform t = hit.collider.transform;
                while (t != null)
                {
                    if (NameIsInteractable(t.name))
                    {
                        bestDistance = hit.distance;
                        hitPoint = hit.point;
                        hitName = t.name;
                        found = true;
                        break;
                    }
                    t = t.parent;
                }
            }
        }

        VRDraggableWindow[] windows = FindAll<VRDraggableWindow>();
        foreach (VRDraggableWindow window in windows)
        {
            if (window == null || !window.isActiveAndEnabled || window.windowRoot == null)
                continue;

            RectTransform target = FindWindowHandle(window);
            if (target != null && RectRayHit(target, ray, out float distance, out Vector3 point) && distance < bestDistance)
            {
                bestDistance = distance;
                hitPoint = point;
                hitName = target.name;
                found = true;
            }

            if (RectRayHit(window.windowRoot, ray, out distance, out point) && distance < bestDistance)
            {
                bestDistance = distance;
                hitPoint = point;
                hitName = window.windowRoot.name;
                found = true;
            }
        }

        return found;
    }

    private RectTransform FindWindowHandle(VRDraggableWindow window)
    {
        RectTransform root = window.windowRoot;
        RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(includeInactive: true);
        foreach (RectTransform rect in rects)
        {
            if (rect == null || rect == root)
                continue;
            if (string.IsNullOrWhiteSpace(window.handleNameContains) || rect.name.IndexOf(window.handleNameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return rect;
        }
        return null;
    }

    private bool RectRayHit(RectTransform rect, Ray ray, out float distance, out Vector3 point)
    {
        distance = 0f;
        point = Vector3.zero;
        Plane plane = new Plane(rect.forward, rect.position);
        if (!plane.Raycast(ray, out distance))
            return false;
        if (distance < 0f || distance > Mathf.Max(0.05f, maxDistance))
            return false;

        point = ray.GetPoint(distance);
        Vector3 local = rect.InverseTransformPoint(point);
        return rect.rect.Contains(new Vector2(local.x, local.y));
    }

    private bool NameIsInteractable(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName) || interactableNameContains == null)
            return false;

        foreach (string token in interactableNameContains)
        {
            if (!string.IsNullOrWhiteSpace(token) && objectName.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private void EnsureRay(RaySpec spec, ref RayRuntime runtime)
    {
        if (runtime == null)
            runtime = new RayRuntime();

        if (runtime.root == null)
        {
            runtime.root = GameObject.Find(spec.rayObjectName);
            if (runtime.root == null)
                runtime.root = new GameObject(spec.rayObjectName);
        }

        if (runtime.line == null)
        {
            runtime.line = runtime.root.GetComponent<LineRenderer>();
            if (runtime.line == null)
                runtime.line = runtime.root.AddComponent<LineRenderer>();
            runtime.line.useWorldSpace = true;
            runtime.line.positionCount = 2;
            runtime.line.material = lineMaterial;
        }

        if (runtime.dot == null)
        {
            Transform existing = runtime.root.transform.Find("HitDot");
            if (existing != null)
            {
                runtime.dot = existing;
            }
            else
            {
                GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.name = "HitDot";
                dot.transform.SetParent(runtime.root.transform, false);
                Collider collider = dot.GetComponent<Collider>();
                if (collider != null)
                    collider.enabled = false;
                runtime.dot = dot.transform;
            }

            Renderer renderer = runtime.dot.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = dotMaterial;
        }
    }

    private void ResolveController(RaySpec spec, RayRuntime runtime)
    {
        if (runtime.controllerTransform != null && runtime.controllerTransform.gameObject.activeInHierarchy)
            return;

        GameObject controllerObject = spec.controller == RayController.Left
            ? GameObject.Find("OVRCameraRig/TrackingSpace/LeftControllerAnchor") ?? GameObject.Find("LeftControllerAnchor")
            : GameObject.Find("OVRCameraRig/TrackingSpace/RightControllerAnchor") ?? GameObject.Find("RightControllerAnchor");

        if (controllerObject != null)
            runtime.controllerTransform = controllerObject.transform;
    }

    private bool IsControllerConnected(RayController controller)
    {
        OVRInput.Controller ovr = controller == RayController.Left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        return (OVRInput.GetConnectedControllers() & ovr) != OVRInput.Controller.None;
    }

    private void EnsureMaterials()
    {
        if (lineMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            lineMaterial = new Material(shader) { name = "ControllerRay_Line_Material" };
            ConfigureTransparentMaterial(lineMaterial);
        }

        if (dotMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            dotMaterial = new Material(shader) { name = "ControllerRay_Dot_Material" };
            ConfigureTransparentMaterial(dotMaterial);
        }
    }

    private bool HasInteractionIntent(RayController controller)
    {
        float index = GetTriggerValue(controller);
        float hand = GetGripValue(controller);
        return index >= indexTriggerIntentThreshold || (showOnGripIntent && hand >= handTriggerIntentThreshold);
    }

    private static float GetTriggerValue(RayController controller)
    {
        if (controller == RayController.Left)
        {
            return Mathf.Max(
                OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch),
                OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.Touch));
        }

        return Mathf.Max(
            OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch),
            OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger, OVRInput.Controller.Touch));
    }

    private static float GetGripValue(RayController controller)
    {
        if (controller == RayController.Left)
        {
            return Mathf.Max(
                OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch),
                OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.Touch));
        }

        return Mathf.Max(
            OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch),
            OVRInput.Get(OVRInput.Axis1D.SecondaryHandTrigger, OVRInput.Controller.Touch));
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

    private void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private string Status(RayRuntime runtime)
    {
        if (runtime == null || runtime.root == null)
            return "missing";
        return runtime.root.activeSelf ? runtime.hitName : "hidden";
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
