using UnityEngine;

[DefaultExecutionOrder(515)]
public class FloatingSceneCameraController : MonoBehaviour
{
    private enum CameraManipulationMode
    {
        None,
        Translate,
        Rotate
    }

    public string cameraObjectName = "FloatingDataCamera";
    public string workspaceRootName = "GazeboWorkspace";
    public Vector3 defaultLocalPosition = new Vector3(0f, 1.15f, 0.72f);
    public Vector3 defaultLocalEuler = new Vector3(58f, 0f, 0f);
    [Range(20f, 140f)] public float fieldOfView = 80f;
    public int previewWidth = 640;
    public int previewHeight = 360;

    [Header("Marker")]
    public Color markerColor = new Color(0.24f, 0.78f, 1.0f, 0.22f);
    public Vector3 bodySize = new Vector3(0.055f, 0.035f, 0.045f);
    public float frustumLength = 0.10f;
    public float frustumHalfSize = 0.0375f;
    public float lineWidth = 0.004f;
    public float rotationRingRadius = 0.115f;
    public float rotationRingHitTolerance = 0.025f;
    public int rotationRingSegments = 72;
    public float rotationRingLineWidth = 0.004f;
    public float rotationRingSensitivity = 1.0f;
    public Color xRotationRingColor = new Color(1.0f, 0.24f, 0.20f, 0.42f);
    public Color yRotationRingColor = new Color(0.22f, 0.95f, 0.32f, 0.42f);
    public Color zRotationRingColor = new Color(0.30f, 0.58f, 1.0f, 0.42f);

    [Header("Drag")]
    public bool allowControllerRayDrag = true;
    public float triggerThreshold = 0.55f;
    public float gripBlockThreshold = 0.55f;
    public float rayMaxDistance = 4.0f;

    [Header("Ray Contact Feedback")]
    public bool showRayContactMarker = true;
    [Tooltip("Show the camera contact marker once the trigger is lightly touched, before a drag begins.")]
    public float hoverTriggerThreshold = 0.05f;
    [Tooltip("Keep hover feedback alive while ControllerRayVisual is visible during its linger window.")]
    public bool showContactMarkerWhileRayVisualVisible = true;
    public float contactMarkerScale = 0.025f;
    public float contactMarkerSurfaceOffset = 0.004f;
    public Color contactMarkerColor = new Color(1.0f, 0.86f, 0.08f, 0.94f);
    [Range(0.0f, 1.0f)] public float occludedContactAlphaMultiplier = 0.55f;

    public Camera SourceCamera { get; private set; }
    public Texture PreviewTexture => SourceCamera != null ? SourceCamera.targetTexture : null;
    public string LastStatus { get; private set; } = "not initialized";

    private GameObject cameraObject;
    private GameObject markerRoot;
    private Material markerMaterial;
    private RenderTexture renderTexture;
    private Transform leftController;
    private Transform rightController;
    private Transform dragController;
    private bool dragging;
    private CameraManipulationMode manipulationMode = CameraManipulationMode.None;
    private float dragDistance;
    private Vector3 activeRotationAxisLocal = Vector3.forward;
    private Quaternion rotationStartLocalRotation = Quaternion.identity;
    private Vector3 rotationPlaneCenterWorld;
    private Vector3 rotationPlaneNormalWorld;
    private Vector3 rotationBasisUWorld;
    private Vector3 rotationBasisVWorld;
    private float rotationStartAngle;
    private Transform contactMarker;
    private Material contactMarkerMaterial;

    private void Start()
    {
        EnsureCameraObject();
    }

    private void Update()
    {
        EnsureCameraObject();
        if (allowControllerRayDrag)
            UpdateControllerDrag();
        LastStatus = SourceCamera != null
            ? $"floating camera pos={SourceCamera.transform.localPosition} euler={SourceCamera.transform.localEulerAngles} fov={SourceCamera.fieldOfView:F1}"
            : "floating camera missing";
    }

    public void ApplyLocalPose(Vector3 localPosition, Vector3 localEuler, float fov)
    {
        EnsureCameraObject();
        if (cameraObject == null)
            return;

        cameraObject.transform.localPosition = localPosition;
        cameraObject.transform.localRotation = Quaternion.Euler(localEuler);
        fieldOfView = Mathf.Clamp(fov, 20f, 140f);
        if (SourceCamera != null)
            SourceCamera.fieldOfView = fieldOfView;
    }

    public Vector3 LocalPosition => cameraObject != null ? cameraObject.transform.localPosition : defaultLocalPosition;
    public Vector3 LocalEuler => cameraObject != null ? NormalizeEuler(cameraObject.transform.localEulerAngles) : defaultLocalEuler;

    private void EnsureCameraObject()
    {
        if (cameraObject == null)
        {
            cameraObject = GameObject.Find(cameraObjectName);
            if (cameraObject == null)
            {
                cameraObject = new GameObject(cameraObjectName);
                Transform workspace = ResolveWorkspace();
                if (workspace != null)
                    cameraObject.transform.SetParent(workspace, false);
                cameraObject.transform.localPosition = defaultLocalPosition;
                cameraObject.transform.localRotation = Quaternion.Euler(defaultLocalEuler);
            }
        }

        Transform workspaceRoot = ResolveWorkspace();
        if (workspaceRoot != null && cameraObject.transform.parent != workspaceRoot)
            cameraObject.transform.SetParent(workspaceRoot, true);

        GazeboWorkspaceMember member = cameraObject.GetComponent<GazeboWorkspaceMember>();
        if (member == null)
            member = cameraObject.AddComponent<GazeboWorkspaceMember>();
        member.keepWorldPoseWhenParented = true;

        SourceCamera = cameraObject.GetComponent<Camera>();
        if (SourceCamera == null)
            SourceCamera = cameraObject.AddComponent<Camera>();
        SourceCamera.enabled = true;
        SourceCamera.depth = -80f;
        SourceCamera.fieldOfView = Mathf.Clamp(fieldOfView, 20f, 140f);
        EnsureRenderTexture();
        EnsureMarker();
    }

    private void EnsureRenderTexture()
    {
        if (SourceCamera == null)
            return;

        if (renderTexture != null && renderTexture.width == previewWidth && renderTexture.height == previewHeight)
        {
            SourceCamera.targetTexture = renderTexture;
            return;
        }

        if (renderTexture != null)
            Destroy(renderTexture);

        renderTexture = new RenderTexture(Mathf.Max(16, previewWidth), Mathf.Max(16, previewHeight), 16, RenderTextureFormat.ARGB32)
        {
            name = "FloatingDataCamera_RT"
        };
        renderTexture.Create();
        SourceCamera.targetTexture = renderTexture;
    }

    private void EnsureMarker()
    {
        if (cameraObject == null)
            return;

        if (markerRoot == null)
        {
            Transform existing = cameraObject.transform.Find("FloatingCameraMarker");
            markerRoot = existing != null ? existing.gameObject : new GameObject("FloatingCameraMarker");
            markerRoot.transform.SetParent(cameraObject.transform, false);
            markerRoot.transform.localPosition = Vector3.zero;
            markerRoot.transform.localRotation = Quaternion.identity;
            markerRoot.transform.localScale = Vector3.one;
        }

        EnsureMarkerMaterial();
        EnsureCameraBody();
        EnsureFrustum();
        EnsureRotationRings();
        ApplyMarkerVisualSettings();
    }

    private void EnsureCameraBody()
    {
        Transform existing = markerRoot.transform.Find("FloatingCameraBody");
        GameObject body = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "FloatingCameraBody";
        body.transform.SetParent(markerRoot.transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = bodySize;
        Collider bodyCollider = body.GetComponent<Collider>();
        if (bodyCollider != null)
            bodyCollider.isTrigger = true;
        Renderer bodyRenderer = body.GetComponent<Renderer>();
        if (bodyRenderer != null)
        {
            bodyRenderer.sharedMaterial = markerMaterial;
            bodyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bodyRenderer.receiveShadows = false;
        }
    }

    private void EnsureFrustum()
    {
        Vector3 forward = Vector3.forward * Mathf.Max(0.02f, frustumLength);
        float half = Mathf.Max(0.01f, frustumHalfSize);
        CreateLine(
            "FloatingCameraFrustum",
            new[]
            {
                Vector3.zero, forward + new Vector3(-half, half, 0f),
                Vector3.zero, forward + new Vector3(half, half, 0f),
                Vector3.zero, forward + new Vector3(half, -half, 0f),
                Vector3.zero, forward + new Vector3(-half, -half, 0f),
                forward + new Vector3(-half, half, 0f),
                forward + new Vector3(half, half, 0f),
                forward + new Vector3(half, -half, 0f),
                forward + new Vector3(-half, -half, 0f),
                forward + new Vector3(-half, half, 0f),
            });
    }

    private void EnsureRotationRings()
    {
        CreateRing("FloatingCameraRotateRing_X", Vector3.right, xRotationRingColor);
        CreateRing("FloatingCameraRotateRing_Y", Vector3.up, yRotationRingColor);
        CreateRing("FloatingCameraRotateRing_Z", Vector3.forward, zRotationRingColor);
    }

    private void CreateRing(string name, Vector3 axisLocal, Color color)
    {
        GetRingBasis(axisLocal, out Vector3 uLocal, out Vector3 vLocal);
        int segments = Mathf.Clamp(rotationRingSegments, 24, 160);
        Vector3[] points = new Vector3[segments + 1];
        float radius = Mathf.Max(0.025f, rotationRingRadius);
        for (int i = 0; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            points[i] = ((Mathf.Cos(angle) * uLocal) + (Mathf.Sin(angle) * vLocal)) * radius;
        }

        CreateLine(name, points, color, Mathf.Max(0.001f, rotationRingLineWidth), loop: false);
    }

    private void CreateLine(string name, Vector3[] points)
    {
        CreateLine(name, points, markerColor, lineWidth, loop: false);
    }

    private void CreateLine(string name, Vector3[] points, Color color, float width, bool loop)
    {
        Transform existing = markerRoot.transform.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        go.transform.SetParent(markerRoot.transform, false);
        LineRenderer line = go.GetComponent<LineRenderer>();
        if (line == null)
            line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = loop;
        line.positionCount = points.Length;
        line.SetPositions(points);
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.material = markerMaterial;
    }

    private void EnsureMarkerMaterial()
    {
        if (markerMaterial != null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        markerMaterial = new Material(shader) { name = "FloatingDataCameraMarker_Material" };
        ConfigureTransparentMaterial(markerMaterial);
        SetMaterialColor(markerMaterial, markerColor);
    }

    private void ApplyMarkerVisualSettings()
    {
        if (markerRoot == null)
            return;

        SetMaterialColor(markerMaterial, markerColor);
        foreach (Renderer renderer in markerRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null)
                renderer.sharedMaterial = markerMaterial;
        }

        foreach (LineRenderer line in markerRoot.GetComponentsInChildren<LineRenderer>(true))
        {
            if (line == null)
                continue;

            line.material = markerMaterial;
            if (line.name.IndexOf("_X", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                line.startColor = xRotationRingColor;
                line.endColor = xRotationRingColor;
                line.startWidth = rotationRingLineWidth;
                line.endWidth = rotationRingLineWidth;
            }
            else if (line.name.IndexOf("_Y", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                line.startColor = yRotationRingColor;
                line.endColor = yRotationRingColor;
                line.startWidth = rotationRingLineWidth;
                line.endWidth = rotationRingLineWidth;
            }
            else if (line.name.IndexOf("_Z", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                line.startColor = zRotationRingColor;
                line.endColor = zRotationRingColor;
                line.startWidth = rotationRingLineWidth;
                line.endWidth = rotationRingLineWidth;
            }
            else
            {
                line.startColor = markerColor;
                line.endColor = markerColor;
                line.startWidth = lineWidth;
                line.endWidth = lineWidth;
            }
        }
    }

    private void UpdateControllerDrag()
    {
        ResolveControllers();
        float leftTrigger = GetTriggerValue(true);
        float rightTrigger = GetTriggerValue(false);
        bool triggerHeld = leftTrigger >= triggerThreshold || rightTrigger >= triggerThreshold;
        bool hoverIntent = leftTrigger >= hoverTriggerThreshold
            || rightTrigger >= hoverTriggerThreshold
            || (showContactMarkerWhileRayVisualVisible && IsControllerRayVisualVisible());
        bool gripHeld = GetGripValue(true) >= gripBlockThreshold || GetGripValue(false) >= gripBlockThreshold;

        bool contactHit = false;
        Vector3 contactPoint = Vector3.zero;
        Transform contactController = null;
        if (!gripHeld && hoverIntent)
            contactHit = TryFindCameraContact(out contactPoint, out contactController);
        UpdateRayContactMarker(contactHit, contactPoint, contactController);

        if (gripHeld || !triggerHeld)
        {
            EndCameraManipulation();
            return;
        }

        if (!dragging)
            BeginManipulationIfHit();

        if (!dragging || dragController == null || cameraObject == null)
            return;

        if (manipulationMode == CameraManipulationMode.Translate)
            cameraObject.transform.position = dragController.position + dragController.forward * dragDistance;
        else if (manipulationMode == CameraManipulationMode.Rotate)
            ApplyRotationDrag();
    }

    private void BeginManipulationIfHit()
    {
        if (TryBeginManipulationWithController(leftController))
            return;
        TryBeginManipulationWithController(rightController);
    }

    private bool TryBeginManipulationWithController(Transform controller)
    {
        if (controller == null)
            return false;

        if (TryRayHitRotationRing(controller, out RotationRingHit ringHit))
        {
            dragController = controller;
            manipulationMode = CameraManipulationMode.Rotate;
            activeRotationAxisLocal = ringHit.axisLocal;
            rotationStartLocalRotation = cameraObject.transform.localRotation;
            rotationPlaneCenterWorld = ringHit.centerWorld;
            rotationPlaneNormalWorld = ringHit.axisWorld;
            rotationBasisUWorld = ringHit.basisUWorld;
            rotationBasisVWorld = ringHit.basisVWorld;
            rotationStartAngle = ringHit.angleDegrees;
            dragging = true;
            return true;
        }

        if (TryRayHitController(controller, out float distance))
        {
            dragController = controller;
            dragDistance = distance;
            manipulationMode = CameraManipulationMode.Translate;
            dragging = true;
            return true;
        }

        return false;
    }

    private void ApplyRotationDrag()
    {
        Ray ray = new Ray(dragController.position, dragController.forward);
        Plane plane = new Plane(rotationPlaneNormalWorld, rotationPlaneCenterWorld);
        if (!plane.Raycast(ray, out float distance) || distance < 0f || distance > Mathf.Max(0.05f, rayMaxDistance))
            return;

        Vector3 point = ray.GetPoint(distance);
        float angle = AngleOnRotationPlane(point);
        float delta = Mathf.DeltaAngle(rotationStartAngle, angle) * rotationRingSensitivity;
        cameraObject.transform.localRotation = rotationStartLocalRotation * Quaternion.AngleAxis(delta, activeRotationAxisLocal);
    }

    private void EndCameraManipulation()
    {
        dragging = false;
        dragController = null;
        manipulationMode = CameraManipulationMode.None;
    }

    private bool TryRayHitController(Transform controller, out float distance)
    {
        distance = 0f;
        if (controller == null || markerRoot == null)
            return false;

        Ray ray = new Ray(controller.position, controller.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Max(0.05f, rayMaxDistance), ~0, QueryTriggerInteraction.Collide);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;
            if (hit.collider.transform == markerRoot.transform || hit.collider.transform.IsChildOf(markerRoot.transform))
            {
                distance = hit.distance;
                return true;
            }
        }
        return false;
    }

    private bool TryFindCameraContact(out Vector3 hitPoint, out Transform hitController)
    {
        hitPoint = Vector3.zero;
        hitController = null;

        bool found = false;
        float bestDistance = float.PositiveInfinity;
        if (TryFindCameraContactWithController(leftController, ref bestDistance, out Vector3 leftPoint))
        {
            hitPoint = leftPoint;
            hitController = leftController;
            found = true;
        }

        if (TryFindCameraContactWithController(rightController, ref bestDistance, out Vector3 rightPoint))
        {
            hitPoint = rightPoint;
            hitController = rightController;
            found = true;
        }

        return found;
    }

    private bool TryFindCameraContactWithController(Transform controller, ref float bestDistance, out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        if (controller == null || cameraObject == null || markerRoot == null)
            return false;

        bool found = false;
        if (TryRayHitRotationRing(controller, out RotationRingHit ringHit) && ringHit.distance < bestDistance)
        {
            bestDistance = ringHit.distance;
            hitPoint = ringHit.pointWorld;
            found = true;
        }

        Ray ray = new Ray(controller.position, controller.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Max(0.05f, rayMaxDistance), ~0, QueryTriggerInteraction.Collide);
        if (hits == null)
            return found;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.distance >= bestDistance)
                continue;

            if (hit.collider.transform == markerRoot.transform || hit.collider.transform.IsChildOf(markerRoot.transform))
            {
                bestDistance = hit.distance;
                hitPoint = hit.point;
                found = true;
            }
        }

        return found;
    }

    private void UpdateRayContactMarker(bool visible, Vector3 hitPoint, Transform controller)
    {
        if (!showRayContactMarker || !visible || controller == null)
        {
            if (contactMarker != null)
                contactMarker.gameObject.SetActive(false);
            return;
        }

        EnsureContactMarker();
        if (contactMarker == null)
            return;

        Vector3 towardController = controller.position - hitPoint;
        Vector3 markerPosition = hitPoint;
        if (towardController.sqrMagnitude > 0.000001f)
            markerPosition += towardController.normalized * Mathf.Max(0.0f, contactMarkerSurfaceOffset);

        contactMarker.gameObject.SetActive(true);
        contactMarker.position = markerPosition;
        contactMarker.rotation = Quaternion.identity;
        contactMarker.localScale = Vector3.one * Mathf.Max(0.002f, contactMarkerScale);
        SetContactMarkerColor(contactMarkerColor);
    }

    private void EnsureContactMarker()
    {
        if (contactMarker != null)
            return;

        GameObject existing = GameObject.Find("FloatingCameraContactMarker");
        GameObject marker = existing != null ? existing : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "FloatingCameraContactMarker";
        Collider collider = marker.GetComponent<Collider>();
        if (collider != null)
            collider.enabled = false;

        contactMarker = marker.transform;
        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            contactMarkerMaterial = CreateContactMarkerMaterial();
            renderer.sharedMaterial = contactMarkerMaterial;
        }

        marker.SetActive(false);
    }

    private Material CreateContactMarkerMaterial()
    {
        Shader shader = Shader.Find("Custom/XRayTransparentHandle")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Color")
            ?? Shader.Find("Standard");
        Material material = new Material(shader) { name = "FloatingCameraContactMarker_XRay_Material" };
        material.renderQueue = 3030;
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
        if (material.HasProperty("_OccludedAlphaMultiplier"))
            material.SetFloat("_OccludedAlphaMultiplier", occludedContactAlphaMultiplier);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        SetMaterialColor(material, contactMarkerColor);
        return material;
    }

    private void SetContactMarkerColor(Color color)
    {
        if (contactMarkerMaterial == null)
            return;
        SetMaterialColor(contactMarkerMaterial, color);
        if (contactMarkerMaterial.HasProperty("_OccludedAlphaMultiplier"))
            contactMarkerMaterial.SetFloat("_OccludedAlphaMultiplier", occludedContactAlphaMultiplier);
    }

    private bool TryRayHitRotationRing(Transform controller, out RotationRingHit bestHit)
    {
        bestHit = default;
        if (controller == null || cameraObject == null)
            return false;

        Ray ray = new Ray(controller.position, controller.forward);
        bool found = false;
        float bestDistance = float.PositiveInfinity;
        TryCandidateRingHit(ray, Vector3.right, ref found, ref bestDistance, ref bestHit);
        TryCandidateRingHit(ray, Vector3.up, ref found, ref bestDistance, ref bestHit);
        TryCandidateRingHit(ray, Vector3.forward, ref found, ref bestDistance, ref bestHit);
        return found;
    }

    private void TryCandidateRingHit(Ray ray, Vector3 axisLocal, ref bool found, ref float bestDistance, ref RotationRingHit bestHit)
    {
        Vector3 centerWorld = cameraObject.transform.position;
        Vector3 axisWorld = cameraObject.transform.TransformDirection(axisLocal).normalized;
        Plane plane = new Plane(axisWorld, centerWorld);
        if (!plane.Raycast(ray, out float distance) || distance < 0f || distance > Mathf.Max(0.05f, rayMaxDistance))
            return;

        Vector3 pointWorld = ray.GetPoint(distance);
        Vector3 localPoint = cameraObject.transform.InverseTransformPoint(pointWorld);
        float radius = ProjectedRingRadius(localPoint, axisLocal);
        if (Mathf.Abs(radius - Mathf.Max(0.025f, rotationRingRadius)) > Mathf.Max(0.002f, rotationRingHitTolerance))
            return;

        if (distance >= bestDistance)
            return;

        GetRingBasis(axisLocal, out Vector3 uLocal, out Vector3 vLocal);
        Vector3 basisUWorld = cameraObject.transform.TransformDirection(uLocal).normalized;
        Vector3 basisVWorld = cameraObject.transform.TransformDirection(vLocal).normalized;
        Vector3 radial = (pointWorld - centerWorld).normalized;
        bestHit = new RotationRingHit
        {
            axisLocal = axisLocal,
            axisWorld = axisWorld,
            centerWorld = centerWorld,
            basisUWorld = basisUWorld,
            basisVWorld = basisVWorld,
            angleDegrees = Mathf.Atan2(Vector3.Dot(radial, basisVWorld), Vector3.Dot(radial, basisUWorld)) * Mathf.Rad2Deg,
            pointWorld = pointWorld,
            distance = distance
        };
        bestDistance = distance;
        found = true;
    }

    private float AngleOnRotationPlane(Vector3 pointWorld)
    {
        Vector3 radial = pointWorld - rotationPlaneCenterWorld;
        if (radial.sqrMagnitude < 0.000001f)
            return rotationStartAngle;
        radial.Normalize();
        return Mathf.Atan2(Vector3.Dot(radial, rotationBasisVWorld), Vector3.Dot(radial, rotationBasisUWorld)) * Mathf.Rad2Deg;
    }

    private static float ProjectedRingRadius(Vector3 localPoint, Vector3 axisLocal)
    {
        if (axisLocal == Vector3.right)
            return new Vector2(localPoint.y, localPoint.z).magnitude;
        if (axisLocal == Vector3.up)
            return new Vector2(localPoint.x, localPoint.z).magnitude;
        return new Vector2(localPoint.x, localPoint.y).magnitude;
    }

    private static void GetRingBasis(Vector3 axisLocal, out Vector3 uLocal, out Vector3 vLocal)
    {
        if (axisLocal == Vector3.right)
        {
            uLocal = Vector3.up;
            vLocal = Vector3.forward;
        }
        else if (axisLocal == Vector3.up)
        {
            uLocal = Vector3.forward;
            vLocal = Vector3.right;
        }
        else
        {
            uLocal = Vector3.right;
            vLocal = Vector3.up;
        }
    }

    private Transform ResolveWorkspace()
    {
        GameObject workspace = GameObject.Find(workspaceRootName);
        return workspace != null ? workspace.transform : null;
    }

    private void ResolveControllers()
    {
        if (leftController == null || !leftController.gameObject.activeInHierarchy)
        {
            GameObject left = GameObject.Find("OVRCameraRig/TrackingSpace/LeftControllerAnchor") ?? GameObject.Find("LeftControllerAnchor");
            if (left != null)
                leftController = left.transform;
        }
        if (rightController == null || !rightController.gameObject.activeInHierarchy)
        {
            GameObject right = GameObject.Find("OVRCameraRig/TrackingSpace/RightControllerAnchor") ?? GameObject.Find("RightControllerAnchor");
            if (right != null)
                rightController = right.transform;
        }
    }

    private static float GetTriggerValue(bool left)
    {
        return left
            ? Mathf.Max(OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch), OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.Touch))
            : Mathf.Max(OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch), OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger, OVRInput.Controller.Touch));
    }

    private static float GetGripValue(bool left)
    {
        return left
            ? Mathf.Max(OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch), OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.Touch))
            : Mathf.Max(OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch), OVRInput.Get(OVRInput.Axis1D.SecondaryHandTrigger, OVRInput.Controller.Touch));
    }

    private static Vector3 NormalizeEuler(Vector3 euler)
    {
        return new Vector3(NormalizeAngle(euler.x), NormalizeAngle(euler.y), NormalizeAngle(euler.z));
    }

    private static float NormalizeAngle(float degrees)
    {
        return Mathf.Repeat(degrees + 180f, 360f) - 180f;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        material.renderQueue = 3000;
        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 3f);
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_AlphaClip"))
            material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private bool IsControllerRayVisualVisible()
    {
        GameObject leftRay = GameObject.Find("LeftControllerAimRay");
        if (leftRay != null && leftRay.activeInHierarchy)
            return true;

        GameObject rightRay = GameObject.Find("RightControllerAimRay");
        return rightRay != null && rightRay.activeInHierarchy;
    }

    private struct RotationRingHit
    {
        public Vector3 axisLocal;
        public Vector3 axisWorld;
        public Vector3 centerWorld;
        public Vector3 basisUWorld;
        public Vector3 basisVWorld;
        public Vector3 pointWorld;
        public float distance;
        public float angleDegrees;
    }
}
