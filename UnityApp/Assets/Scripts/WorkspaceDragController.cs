using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(250)]
public class WorkspaceDragController : MonoBehaviour
{
    public static WorkspaceDragController ActiveInstance { get; private set; }

    private enum ManipulationMode
    {
        None,
        Translate,
        Rotate
    }

    [Header("Workspace Root")]
    public string workspaceRootName = "GazeboWorkspace";
    public Transform workspaceRoot;
    public bool autoResolveWorkspaceRoot = true;
    public float rootRefreshIntervalSec = 1.0f;

    [Header("Drag Handle")]
    public string dragHandleNameContains = "WorkspaceDragHandle";
    public string rotateHandleNameContains = "WorkspaceRotateHandle";
    public bool requireRayHitOnHandle = true;
    public float rayMaxDistance = 4.0f;
    public string[] operationalRootNames = { "ur5e", "GameObjects_sync" };
    [Tooltip("Automatically make GazeboWorkspaceMember roots children of the workspace and include them in drag pose capture.")]
    public bool autoBindWorkspaceMembers = true;
    [Tooltip("Fallback names treated as workspace members even if the marker component is missing. Useful for older scenes.")]
    public string[] workspaceMemberNameContains =
    {
        "Gazebo_Table",
        "GameObjects_sync",
        "left_ur5e",
        "right_ur5e",
        "WorkspaceDragHandle",
        "WorkspaceRotateHandle"
    };

    [Header("Drag Input")]
    public OVRInput.Controller dragController = OVRInput.Controller.RTouch;
    [Tooltip("Let either Quest controller select/drag workspace handles. The serialized dragController remains the fallback/preferred hand.")]
    public bool allowEitherController = true;
    [Range(0.05f, 0.95f)] public float dragTriggerThreshold = 0.55f;
    [Range(0.05f, 0.95f)] public float rightGripTeleopBlockThreshold = 0.55f;
    public bool requireRightGripReleased = true;
    public bool retryHandleHitWhileTriggerHeld = true;

    [Header("Motion")]
    [Tooltip("If enabled, the user can raise/lower the workspace while dragging. Pitch/roll are always locked.")]
    public bool allowVerticalDrag = true;
    [Tooltip("Attach the grabbed workspace point to a fixed-depth point on the controller ray.")]
    public bool translateByRayGrabPoint = true;
    [Tooltip("Fallback scale for legacy controller-position translation if ray-grab translation is disabled.")]
    public float dragScale = 1.0f;
    [Tooltip("Use the outer rotate ring to rotate the workspace around room-up.")]
    public bool allowYawRotation = true;
    public float yawRotationScale = -1.0f;

    [Header("Handle Contact Feedback")]
    [Tooltip("Show a small x-ray marker where the controller ray intersects the workspace skirt/ring. This stays visible even when the table mesh blocks the view.")]
    public bool showHandleContactMarker = true;
    [Tooltip("Show the marker while the ray is merely hovering over a workspace handle, before the drag threshold is reached.")]
    public bool showContactMarkerOnHover = true;
    [Tooltip("Also keep the marker alive while ControllerRayVisual's ray object is visible during its linger window.")]
    public bool showContactMarkerWhileRayVisualVisible = true;
    [Range(0.0f, 0.95f)]
    public float hoverTriggerThreshold = 0.05f;
    public float contactMarkerScale = 0.035f;
    public float contactMarkerSurfaceOffset = 0.006f;
    public Color translateContactColor = new Color(1.0f, 0.85f, 0.20f, 0.92f);
    public Color rotateContactColor = new Color(1.0f, 0.85f, 0.20f, 0.92f);
    [Range(0.0f, 1.0f)] public float occludedContactAlphaMultiplier = 0.55f;

    [Header("Handle Visibility")]
    [Tooltip("Hide the workspace skirt/ring unless the controller ray is active or the handle is being dragged.")]
    public bool hideHandlesUnlessRayHovering = true;
    [Tooltip("Disable workspace handle colliders while the controller ray is hidden. The colliders are re-enabled while the ray is visible so hover detection still works.")]
    public bool disableHandleCollidersWhenRayHidden = true;
    [Tooltip("Keep the active handle visible during a drag even if the ray slips slightly off the mesh.")]
    public bool keepHandlesVisibleWhileDragging = true;
    [Range(0.05f, 5.0f)] public float handleCacheRefreshIntervalSec = 0.5f;

    public bool IsDragging { get; private set; }
    public string LastStatus { get; private set; } = "not initialized";

    private Transform controllerTransform;
    private Vector3 controllerStartPosition;
    private float controllerStartYaw;
    private Vector3 workspaceStartPosition;
    private Quaternion workspaceStartRotation;
    private float workspaceStartYaw;
    private float rayGrabDistance;
    private Vector3 workspaceOffsetFromGrabPoint;
    private float rotationPlaneY;
    private float rotationStartAngle;
    private bool triggerWasHeld;
    private OVRInput.Controller activeDragController = OVRInput.Controller.None;
    private float nextRootRefreshTime;
    private string activeHandleName = "none";
    private ManipulationMode activeMode = ManipulationMode.None;
    private Transform contactMarker;
    private Material contactMarkerMaterial;
    private readonly List<OperationalRootPose> operationalRootStartPoses = new List<OperationalRootPose>();
    private readonly List<Renderer> workspaceHandleRenderers = new List<Renderer>();
    private readonly List<Collider> workspaceHandleColliders = new List<Collider>();
    private float nextHandleCacheRefreshTime;

    private class OperationalRootPose
    {
        public Transform root;
        public Vector3 rootPosition;
        public Quaternion rootRotation;
        public ArticulationBody articulationRoot;
        public Vector3 articulationPosition;
        public Quaternion articulationRotation;
    }

    private void Awake()
    {
        ActiveInstance = this;
    }

    private void Start()
    {
        ResolveActiveControllerTransform();
        ResolveWorkspaceRoot();
        UpdateWorkspaceHandleInteractivity(false);
        UpdateWorkspaceHandleRenderVisibility(false);
    }

    private void Update()
    {
        ResolveActiveControllerTransform();

        if (autoResolveWorkspaceRoot && Time.unscaledTime >= nextRootRefreshTime)
            ResolveWorkspaceRoot();

        EnsureOperationalRootsParented();

        bool activeControllerReady = controllerTransform != null
            && activeDragController != OVRInput.Controller.None
            && IsControllerConnected(activeDragController);
        bool teleopHeld = activeControllerReady && GetGripValue(activeDragController) >= rightGripTeleopBlockThreshold;
        float triggerValue = activeControllerReady ? GetTriggerValue(activeDragController) : 0f;
        bool triggerHeld = triggerValue >= dragTriggerThreshold;
        bool controllerRayVisualVisible = activeControllerReady && IsControllerRayVisualVisible(activeDragController);
        bool hoverIntent = showContactMarkerOnHover
            && (triggerValue >= Mathf.Max(0.0f, hoverTriggerThreshold)
                || (showContactMarkerWhileRayVisualVisible && controllerRayVisualVisible));
        bool blocked = requireRightGripReleased && teleopHeld;
        bool controllerRayActive = !blocked
            && activeControllerReady
            && workspaceRoot != null
            && (triggerHeld || hoverIntent || controllerRayVisualVisible);

        UpdateWorkspaceHandleInteractivity(controllerRayActive || IsDragging);

        bool contactHit = false;
        ManipulationMode contactMode = ManipulationMode.None;
        Vector3 contactPoint = Vector3.zero;
        if (!blocked && activeControllerReady && workspaceRoot != null && (triggerHeld || hoverIntent))
        {
            contactHit = RayHitsWorkspaceHandle(
                out _,
                out contactMode,
                out contactPoint,
                out _);
        }
        UpdateHandleContactMarker(contactHit, contactPoint, contactMode);

        if (blocked || !triggerHeld || !activeControllerReady || workspaceRoot == null)
        {
            EndDrag();
            UpdateWorkspaceHandleRenderVisibility(controllerRayActive || (keepHandlesVisibleWhileDragging && IsDragging));
            if (!controllerRayActive)
                UpdateWorkspaceHandleInteractivity(false);
            triggerWasHeld = triggerHeld;
            LastStatus = $"idle root={(workspaceRoot ? workspaceRoot.name : "NULL")}, handle={activeHandleName}, mode={activeMode}, trigger={triggerHeld}, teleopHeld={teleopHeld}, controller={ActiveControllerLabel()}";
            return;
        }

        if (!IsDragging && (retryHandleHitWhileTriggerHeld || !triggerWasHeld))
            BeginDragIfHandleHit();

        if (IsDragging)
            ApplyDrag();

        UpdateWorkspaceHandleRenderVisibility(controllerRayActive || (keepHandlesVisibleWhileDragging && IsDragging));

        triggerWasHeld = triggerHeld;
        LastStatus = $"dragging={IsDragging}, mode={activeMode}, root={(workspaceRoot ? workspaceRoot.name : "NULL")}, handle={activeHandleName}, teleopHeld={teleopHeld}, controller={ActiveControllerLabel()}";
    }

    private void UpdateWorkspaceHandleInteractivity(bool interactive)
    {
        if (!hideHandlesUnlessRayHovering || !disableHandleCollidersWhenRayHidden)
        {
            SetWorkspaceHandleCollidersEnabled(true);
            return;
        }

        SetWorkspaceHandleCollidersEnabled(interactive);
    }

    private void UpdateWorkspaceHandleRenderVisibility(bool visible)
    {
        if (!hideHandlesUnlessRayHovering)
        {
            SetWorkspaceHandleRenderersEnabled(true);
            return;
        }

        SetWorkspaceHandleRenderersEnabled(visible);
    }

    private void SetWorkspaceHandleRenderersEnabled(bool enabled)
    {
        RefreshWorkspaceHandleCacheIfNeeded();
        foreach (Renderer renderer in workspaceHandleRenderers)
        {
            if (renderer != null)
                renderer.enabled = enabled;
        }
    }

    private void SetWorkspaceHandleCollidersEnabled(bool enabled)
    {
        RefreshWorkspaceHandleCacheIfNeeded();
        foreach (Collider collider in workspaceHandleColliders)
        {
            if (collider != null)
                collider.enabled = enabled;
        }
    }

    private void RefreshWorkspaceHandleCacheIfNeeded()
    {
        if (Time.unscaledTime < nextHandleCacheRefreshTime && (workspaceHandleRenderers.Count > 0 || workspaceHandleColliders.Count > 0))
            return;

        nextHandleCacheRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, handleCacheRefreshIntervalSec);
        workspaceHandleRenderers.Clear();
        workspaceHandleColliders.Clear();

        HashSet<Renderer> renderers = new HashSet<Renderer>();
        HashSet<Collider> colliders = new HashSet<Collider>();
        Transform[] transforms = FindAll<Transform>();
        foreach (Transform transform in transforms)
        {
            if (transform == null || !NameMatchesHandle(transform.name))
                continue;

            Renderer[] childRenderers = transform.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in childRenderers)
            {
                if (renderer != null && renderers.Add(renderer))
                    workspaceHandleRenderers.Add(renderer);
            }

            Collider[] childColliders = transform.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in childColliders)
            {
                if (collider != null && colliders.Add(collider))
                    workspaceHandleColliders.Add(collider);
            }
        }
    }

    private void BeginDragIfHandleHit()
    {
        if (workspaceRoot == null || controllerTransform == null)
            return;

        ManipulationMode hitMode = ManipulationMode.Translate;
        float hitDistance = 0.75f;
        Vector3 hitPoint = controllerTransform.position + controllerTransform.forward * hitDistance;

        if (requireRayHitOnHandle && !RayHitsWorkspaceHandle(out activeHandleName, out hitMode, out hitPoint, out hitDistance))
        {
            activeHandleName = "miss";
            activeMode = ManipulationMode.None;
            return;
        }
        else if (!requireRayHitOnHandle)
        {
            hitMode = ManipulationMode.Translate;
            hitDistance = 0.75f;
            hitPoint = controllerTransform.position + controllerTransform.forward * hitDistance;
            activeHandleName = "no-hit-required";
        }

        controllerStartPosition = controllerTransform.position;
        controllerStartYaw = GetYaw(controllerTransform.rotation);
        workspaceStartPosition = workspaceRoot.position;
        workspaceStartRotation = workspaceRoot.rotation;
        workspaceStartYaw = GetYaw(workspaceRoot.rotation);
        activeMode = hitMode;
        rayGrabDistance = Mathf.Clamp(hitDistance, 0.05f, Mathf.Max(0.05f, rayMaxDistance));
        workspaceOffsetFromGrabPoint = workspaceStartPosition - hitPoint;
        rotationPlaneY = hitPoint.y;
        rotationStartAngle = HorizontalAngle(workspaceStartPosition, hitPoint);
        CaptureOperationalRootStartPoses();
        IsDragging = true;
        Debug.Log($"[WorkspaceDragController] Drag started on {activeHandleName} ({activeMode}) with {ActiveControllerLabel()}.");
    }

    private void ApplyDrag()
    {
        Vector3 newWorkspacePosition = workspaceStartPosition;
        Quaternion newWorkspaceRotation = workspaceStartRotation;

        if (activeMode == ManipulationMode.Translate)
        {
            newWorkspacePosition = translateByRayGrabPoint
                ? ComputeRayGrabWorkspacePosition()
                : workspaceStartPosition + ((controllerTransform.position - controllerStartPosition) * dragScale);
            if (!allowVerticalDrag)
                newWorkspacePosition.y = workspaceStartPosition.y;
        }
        else if (activeMode == ManipulationMode.Rotate && allowYawRotation)
        {
            float currentAngle = rotationStartAngle;
            if (TryGetRayPointOnHorizontalPlane(rotationPlaneY, out Vector3 currentPoint))
                currentAngle = HorizontalAngle(workspaceStartPosition, currentPoint);
            else
                currentAngle = HorizontalAngle(workspaceStartPosition, controllerTransform.position + controllerTransform.forward * rayGrabDistance);

            float yawDelta = Mathf.DeltaAngle(rotationStartAngle, currentAngle) * yawRotationScale;
            newWorkspaceRotation = Quaternion.Euler(0f, workspaceStartYaw + yawDelta, 0f);
        }

        workspaceRoot.SetPositionAndRotation(newWorkspacePosition, newWorkspaceRotation);
        ApplyOperationalRootDrag(newWorkspacePosition, newWorkspaceRotation);
        Physics.SyncTransforms();
    }

    private void EndDrag()
    {
        if (!IsDragging)
            return;

        IsDragging = false;
        activeMode = ManipulationMode.None;
        operationalRootStartPoses.Clear();
        Debug.Log("[WorkspaceDragController] Drag ended.");
    }

    private void UpdateHandleContactMarker(bool visible, Vector3 hitPoint, ManipulationMode mode)
    {
        if (!showHandleContactMarker || !visible || controllerTransform == null)
        {
            if (contactMarker != null)
                contactMarker.gameObject.SetActive(false);
            return;
        }

        EnsureContactMarker();
        if (contactMarker == null)
            return;

        Vector3 towardController = controllerTransform.position - hitPoint;
        Vector3 markerPosition = hitPoint;
        if (towardController.sqrMagnitude > 0.000001f)
            markerPosition += towardController.normalized * Mathf.Max(0f, contactMarkerSurfaceOffset);

        contactMarker.gameObject.SetActive(true);
        contactMarker.position = markerPosition;
        contactMarker.rotation = Quaternion.identity;
        contactMarker.localScale = Vector3.one * Mathf.Max(0.002f, contactMarkerScale);

        Color color = mode == ManipulationMode.Rotate ? rotateContactColor : translateContactColor;
        SetContactMarkerColor(color);
    }

    private void EnsureContactMarker()
    {
        if (contactMarker != null)
            return;

        GameObject existing = GameObject.Find("WorkspaceHandleContactMarker");
        GameObject marker = existing != null ? existing : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "WorkspaceHandleContactMarker";
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
    }

    private Material CreateContactMarkerMaterial()
    {
        Shader shader = Shader.Find("Custom/XRayTransparentHandle")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Color")
            ?? Shader.Find("Standard");
        Material material = new Material(shader) { name = "WorkspaceHandleContactMarker_XRay_Material" };
        material.renderQueue = 3025;
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
        return material;
    }

    private void SetContactMarkerColor(Color color)
    {
        if (contactMarkerMaterial == null)
            return;
        if (contactMarkerMaterial.HasProperty("_Color"))
            contactMarkerMaterial.SetColor("_Color", color);
        if (contactMarkerMaterial.HasProperty("_BaseColor"))
            contactMarkerMaterial.SetColor("_BaseColor", color);
        if (contactMarkerMaterial.HasProperty("_OccludedAlphaMultiplier"))
            contactMarkerMaterial.SetFloat("_OccludedAlphaMultiplier", occludedContactAlphaMultiplier);
    }

    public void SetWorkspacePose(Vector3 position, Quaternion rotation)
    {
        ResolveWorkspaceRoot();
        if (workspaceRoot == null)
            return;

        EndDrag();
        workspaceStartPosition = workspaceRoot.position;
        workspaceStartRotation = workspaceRoot.rotation;
        CaptureOperationalRootStartPoses();

        Quaternion yawOnly = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
        workspaceRoot.SetPositionAndRotation(position, yawOnly);
        ApplyOperationalRootDrag(position, yawOnly);
    }

    private Vector3 ComputeRayGrabWorkspacePosition()
    {
        Vector3 grabPoint = controllerTransform.position + controllerTransform.forward * rayGrabDistance;
        return grabPoint + workspaceOffsetFromGrabPoint;
    }

    private void CaptureOperationalRootStartPoses()
    {
        operationalRootStartPoses.Clear();
        if (workspaceRoot == null)
            return;

        if (operationalRootNames != null)
        {
            foreach (string rootName in operationalRootNames)
            {
                if (string.IsNullOrWhiteSpace(rootName))
                    continue;

                GameObject go = GameObject.Find(rootName);
                if (go == null || go.transform == workspaceRoot)
                    continue;

                AddOperationalRootPose(go.transform);
            }
        }

        if (autoBindWorkspaceMembers)
        {
            foreach (Transform member in EnumerateWorkspaceMemberRoots())
                AddOperationalRootPose(member);
        }
    }

    private void AddOperationalRootPose(Transform root)
    {
        if (root == null || root == workspaceRoot)
            return;
        foreach (OperationalRootPose existing in operationalRootStartPoses)
        {
            if (existing != null && existing.root == root)
                return;
        }

        ArticulationBody articulationRoot = FindRootArticulationBody(root);
        operationalRootStartPoses.Add(new OperationalRootPose
        {
            root = root,
            rootPosition = root.position,
            rootRotation = root.rotation,
            articulationRoot = articulationRoot,
            articulationPosition = articulationRoot != null ? articulationRoot.transform.position : Vector3.zero,
            articulationRotation = articulationRoot != null ? articulationRoot.transform.rotation : Quaternion.identity,
        });
    }

    private void ApplyOperationalRootDrag(Vector3 newWorkspacePosition, Quaternion newWorkspaceRotation)
    {
        if (operationalRootStartPoses.Count == 0)
            return;

        Quaternion workspaceDeltaRotation = newWorkspaceRotation * Quaternion.Inverse(workspaceStartRotation);

        foreach (OperationalRootPose pose in operationalRootStartPoses)
        {
            if (pose == null || pose.root == null)
                continue;

            Vector3 newRootPosition = newWorkspacePosition + workspaceDeltaRotation * (pose.rootPosition - workspaceStartPosition);
            Quaternion newRootRotation = workspaceDeltaRotation * pose.rootRotation;
            pose.root.SetPositionAndRotation(newRootPosition, newRootRotation);

            if (pose.articulationRoot != null)
            {
                Vector3 newArticulationPosition = newWorkspacePosition + workspaceDeltaRotation * (pose.articulationPosition - workspaceStartPosition);
                Quaternion newArticulationRotation = workspaceDeltaRotation * pose.articulationRotation;
                pose.articulationRoot.TeleportRoot(newArticulationPosition, newArticulationRotation);
            }
        }
    }

    private void ResolveWorkspaceRoot()
    {
        nextRootRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, rootRefreshIntervalSec);
        if (workspaceRoot != null && workspaceRoot.gameObject.activeInHierarchy)
            return;

        if (string.IsNullOrWhiteSpace(workspaceRootName))
            return;

        GameObject go = GameObject.Find(workspaceRootName);
        if (go != null)
            workspaceRoot = go.transform;
    }

    private void EnsureOperationalRootsParented()
    {
        if (workspaceRoot == null)
            return;

        if (operationalRootNames != null)
        {
            foreach (string rootName in operationalRootNames)
            {
                if (string.IsNullOrWhiteSpace(rootName))
                    continue;

                GameObject go = GameObject.Find(rootName);
                if (go == null || go.transform == workspaceRoot || go.transform.parent == workspaceRoot)
                    continue;

                go.transform.SetParent(workspaceRoot, true);
            }
        }

        if (!autoBindWorkspaceMembers)
            return;

        foreach (Transform member in EnumerateWorkspaceMemberRoots())
        {
            if (member == null || member == workspaceRoot || member.parent == workspaceRoot)
                continue;

            GazeboWorkspaceMember marker = member.GetComponent<GazeboWorkspaceMember>();
            bool keepWorldPose = marker == null || marker.keepWorldPoseWhenParented;
            member.SetParent(workspaceRoot, keepWorldPose);
        }
    }

    private IEnumerable<Transform> EnumerateWorkspaceMemberRoots()
    {
        HashSet<Transform> seen = new HashSet<Transform>();
        GazeboWorkspaceMember[] markedMembers = FindAll<GazeboWorkspaceMember>();
        foreach (GazeboWorkspaceMember member in markedMembers)
        {
            if (member == null)
                continue;

            Transform root = FindTopWorkspaceMemberRoot(member.transform);
            if (root != null && root != workspaceRoot && seen.Add(root))
                yield return root;
        }

        Transform[] transforms = FindAll<Transform>();
        foreach (Transform transform in transforms)
        {
            if (transform == null || transform == workspaceRoot || !NameMatchesWorkspaceMember(transform.name))
                continue;

            Transform root = FindTopWorkspaceMemberRoot(transform);
            if (root != null && root != workspaceRoot && seen.Add(root))
                yield return root;
        }
    }

    private Transform FindTopWorkspaceMemberRoot(Transform candidate)
    {
        if (candidate == null || candidate == workspaceRoot)
            return null;

        Transform root = candidate;
        Transform current = candidate.parent;
        while (current != null && current != workspaceRoot)
        {
            if (current.GetComponent<GazeboWorkspaceMember>() != null || NameMatchesWorkspaceMember(current.name))
                root = current;
            current = current.parent;
        }
        return root;
    }

    private bool NameMatchesWorkspaceMember(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName) || workspaceMemberNameContains == null)
            return false;

        foreach (string token in workspaceMemberNameContains)
        {
            if (!string.IsNullOrWhiteSpace(token) && objectName.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static ArticulationBody FindRootArticulationBody(Transform root)
    {
        if (root == null)
            return null;

        ArticulationBody[] bodies = root.GetComponentsInChildren<ArticulationBody>(true);
        foreach (ArticulationBody body in bodies)
        {
            if (body != null && body.isRoot)
                return body;
        }

        return bodies != null && bodies.Length > 0 ? bodies[0] : null;
    }

    private bool RayHitsWorkspaceHandle(out string hitName, out ManipulationMode hitMode, out Vector3 hitPoint, out float hitDistance)
    {
        return RayHitsWorkspaceHandle(controllerTransform, out hitName, out hitMode, out hitPoint, out hitDistance);
    }

    private bool RayHitsWorkspaceHandle(Transform rayController, out string hitName, out ManipulationMode hitMode, out Vector3 hitPoint, out float hitDistance)
    {
        hitName = "none";
        hitMode = ManipulationMode.None;
        hitPoint = Vector3.zero;
        hitDistance = 0f;
        if (rayController == null)
            return false;

        Ray ray = new Ray(rayController.position, rayController.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Max(0.05f, rayMaxDistance), ~0, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            Transform t = hit.collider.transform;
            while (t != null)
            {
                if (NameMatchesHandle(t.name))
                {
                    hitName = t.name;
                    hitMode = NameMatchesRotateHandle(t.name) ? ManipulationMode.Rotate : ManipulationMode.Translate;
                    hitPoint = hit.point;
                    hitDistance = hit.distance;
                    return true;
                }
                if (t == workspaceRoot)
                    break;
                t = t.parent;
            }
        }

        return false;
    }

    private bool NameMatchesHandle(string objectName)
    {
        return NameMatchesTranslateHandle(objectName) || NameMatchesRotateHandle(objectName);
    }

    private bool NameMatchesTranslateHandle(string objectName)
    {
        if (string.IsNullOrWhiteSpace(dragHandleNameContains))
            return true;
        return objectName.IndexOf(dragHandleNameContains, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool NameMatchesRotateHandle(string objectName)
    {
        if (string.IsNullOrWhiteSpace(rotateHandleNameContains))
            return false;
        return objectName.IndexOf(rotateHandleNameContains, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool TryGetRayPointOnHorizontalPlane(float y, out Vector3 point)
    {
        Ray ray = new Ray(controllerTransform.position, controllerTransform.forward);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
        if (plane.Raycast(ray, out float distance) && distance > 0f && distance <= Mathf.Max(0.05f, rayMaxDistance))
        {
            point = ray.GetPoint(distance);
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    private static float HorizontalAngle(Vector3 center, Vector3 point)
    {
        Vector3 delta = point - center;
        return Mathf.Atan2(delta.z, delta.x) * Mathf.Rad2Deg;
    }

    private void ResolveActiveControllerTransform()
    {
        OVRInput.Controller selected = SelectActiveController();
        if (selected == OVRInput.Controller.None)
        {
            controllerTransform = null;
            activeDragController = OVRInput.Controller.None;
            return;
        }

        Transform selectedTransform = FindControllerTransform(selected);
        if (selectedTransform == null)
        {
            controllerTransform = null;
            activeDragController = OVRInput.Controller.None;
            return;
        }

        activeDragController = selected;
        controllerTransform = selectedTransform;
    }

    private OVRInput.Controller SelectActiveController()
    {
        if (IsDragging && activeDragController != OVRInput.Controller.None && FindControllerTransform(activeDragController) != null)
            return activeDragController;

        OVRInput.Controller preferred = NormalizeController(dragController);
        if (!allowEitherController)
            return preferred;

        OVRInput.Controller other = preferred == OVRInput.Controller.LTouch ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch;
        float preferredScore = ControllerSelectionScore(preferred, preferBonus: 0.1f);
        float otherScore = ControllerSelectionScore(other, preferBonus: 0.0f);

        if (otherScore > preferredScore)
            return other;
        if (preferredScore >= 0.0f)
            return preferred;
        if (otherScore >= 0.0f)
            return other;
        return OVRInput.Controller.None;
    }

    private float ControllerSelectionScore(OVRInput.Controller controller, float preferBonus)
    {
        Transform candidate = FindControllerTransform(controller);
        if (candidate == null || !IsControllerConnected(controller))
            return -1f;

        float triggerValue = GetTriggerValue(controller);
        bool rayVisible = IsControllerRayVisualVisible(controller);
        bool handleHit = RayHitsWorkspaceHandle(candidate, out _, out _, out _, out _);

        float score = preferBonus;
        if (triggerValue >= dragTriggerThreshold)
            score += 100f + triggerValue;
        else if (triggerValue >= Mathf.Max(0.0f, hoverTriggerThreshold))
            score += 50f + triggerValue;
        if (rayVisible)
            score += 20f;
        if (handleHit)
            score += 10f;
        return score;
    }

    private Transform FindControllerTransform(OVRInput.Controller controller)
    {
        OVRInput.Controller normalized = NormalizeController(controller);
        GameObject controllerObject = normalized == OVRInput.Controller.LTouch
            ? GameObject.Find("OVRCameraRig/TrackingSpace/LeftControllerAnchor") ?? GameObject.Find("LeftControllerAnchor")
            : GameObject.Find("OVRCameraRig/TrackingSpace/RightControllerAnchor") ?? GameObject.Find("RightControllerAnchor");

        return controllerObject != null && controllerObject.activeInHierarchy ? controllerObject.transform : null;
    }

    private static float GetYaw(Quaternion rotation)
    {
        return rotation.eulerAngles.y;
    }

    private static bool IsControllerConnected(OVRInput.Controller controller)
    {
        OVRInput.Controller normalized = NormalizeController(controller);
        return (OVRInput.GetConnectedControllers() & normalized) != OVRInput.Controller.None;
    }

    private static OVRInput.Controller NormalizeController(OVRInput.Controller controller)
    {
        return controller == OVRInput.Controller.LTouch ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
    }

    private static float GetTriggerValue(OVRInput.Controller controller)
    {
        OVRInput.Controller normalized = NormalizeController(controller);
        if (normalized == OVRInput.Controller.LTouch)
        {
            return Mathf.Max(
                OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch),
                OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.Touch));
        }

        return Mathf.Max(
            OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch),
            OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger, OVRInput.Controller.Touch));
    }

    private static float GetGripValue(OVRInput.Controller controller)
    {
        OVRInput.Controller normalized = NormalizeController(controller);
        if (normalized == OVRInput.Controller.LTouch)
        {
            return Mathf.Max(
                OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch),
                OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.Touch));
        }

        return Mathf.Max(
            OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch),
            OVRInput.Get(OVRInput.Axis1D.SecondaryHandTrigger, OVRInput.Controller.Touch));
    }

    private string ActiveControllerLabel()
    {
        if (controllerTransform == null || activeDragController == OVRInput.Controller.None)
            return "NULL";
        return $"{activeDragController}:{controllerTransform.name}";
    }

    private bool IsControllerRayVisualVisible(OVRInput.Controller controller)
    {
        GameObject activeControllerRay = GameObject.Find(
            NormalizeController(controller) == OVRInput.Controller.LTouch ? "LeftControllerAimRay" : "RightControllerAimRay");
        return activeControllerRay != null && activeControllerRay.activeInHierarchy;
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
