using UnityEngine;

[DefaultExecutionOrder(520)]
public class VRDraggableWindow : MonoBehaviour
{
    public enum DragController
    {
        Left,
        Right,
        Either
    }

    public enum DragButton
    {
        Trigger,
        Grip,
        Thumbstick
    }

    [Header("Target")]
    public RectTransform windowRoot;
    public string handleNameContains = "DragHandle";
    public bool requireHandleHit = true;
    public bool allowWholeWindowFallback = false;

    [Header("Input")]
    public DragController dragController = DragController.Left;
    public DragButton dragButton = DragButton.Trigger;
    public float buttonThreshold = 0.55f;
    public float rayMaxDistance = 3.0f;
    public bool requireRightGripReleased = true;
    public float rightGripBlockThreshold = 0.55f;
    [Tooltip("Keep testing the handle while the drag button is held. This makes VR dragging forgiving when the user presses before the ray is perfectly on the handle.")]
    public bool retryHandleHitWhileButtonHeld = true;

    [Header("Motion")]
    public bool keepOriginalRotationWhileDragging = true;
    public bool faceHeadsetWhileDragging = false;
    public bool faceHeadsetOnRelease = false;
    public bool yawOnlyHeadsetFacing = true;
    public string headsetName = "CenterEyeAnchor";
    public bool persistPoseThisSession = true;

    public bool IsDragging { get; private set; }
    public string LastStatus { get; private set; } = "not initialized";

    private Transform controllerTransform;
    private Transform leftControllerTransform;
    private Transform rightControllerTransform;
    private bool buttonWasHeld;
    private Vector3 localOffsetFromController;
    private Quaternion dragRotation;
    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private bool hasSavedPose;
    private Transform headsetTransform;

    private void Awake()
    {
        if (windowRoot == null)
            windowRoot = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        if (windowRoot == null)
            windowRoot = GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (windowRoot == null)
        {
            LastStatus = "no RectTransform";
            return;
        }

        ResolveControllerTransform();
        bool buttonHeld = IsDragButtonHeld();
        bool teleopHeld =
            OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch) >= rightGripBlockThreshold ||
            OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch) >= rightGripBlockThreshold;
        bool blocked = requireRightGripReleased && teleopHeld;

        if (blocked || controllerTransform == null || !buttonHeld)
        {
            EndDrag(savePose: true);
            buttonWasHeld = buttonHeld;
            LastStatus = $"idle button={buttonHeld}, blocked={blocked}, controller={(controllerTransform ? controllerTransform.name : "NULL")}";
            return;
        }

        if (!IsDragging && (retryHandleHitWhileButtonHeld || !buttonWasHeld))
            BeginDragIfHit();

        if (IsDragging)
        {
            windowRoot.position = controllerTransform.position + (controllerTransform.rotation * localOffsetFromController);
            if (faceHeadsetWhileDragging)
                windowRoot.rotation = GetHeadsetFacingRotation(windowRoot.rotation);
            else if (!keepOriginalRotationWhileDragging)
                windowRoot.rotation = controllerTransform.rotation * dragRotation;
            else
                windowRoot.rotation = dragRotation;
        }

        buttonWasHeld = buttonHeld;
        LastStatus = $"dragging={IsDragging}";
    }

    private void BeginDragIfHit()
    {
        if (requireHandleHit && !RayHitsHandle())
            return;

        localOffsetFromController = Quaternion.Inverse(controllerTransform.rotation) * (windowRoot.position - controllerTransform.position);
        dragRotation = keepOriginalRotationWhileDragging
            ? windowRoot.rotation
            : Quaternion.Inverse(controllerTransform.rotation) * windowRoot.rotation;
        IsDragging = true;
    }

    private void EndDrag(bool savePose)
    {
        if (!IsDragging)
            return;

        IsDragging = false;
        if (faceHeadsetOnRelease)
            windowRoot.rotation = GetHeadsetFacingRotation(windowRoot.rotation);

        if (savePose && persistPoseThisSession)
        {
            savedPosition = windowRoot.position;
            savedRotation = windowRoot.rotation;
            hasSavedPose = true;
        }
    }

    public void RestoreSessionPoseIfAny()
    {
        if (!hasSavedPose || windowRoot == null)
            return;
        windowRoot.position = savedPosition;
        windowRoot.rotation = savedRotation;
    }

    public bool FaceHeadsetNow()
    {
        if (windowRoot == null)
            windowRoot = GetComponent<RectTransform>();
        if (windowRoot == null)
            return false;

        Quaternion newRotation = GetHeadsetFacingRotation(windowRoot.rotation);
        if (Quaternion.Angle(newRotation, windowRoot.rotation) < 0.01f)
            return ResolveHeadsetTransform() != null;

        windowRoot.rotation = newRotation;
        return true;
    }

    private bool RayHitsHandle()
    {
        if (controllerTransform == null || windowRoot == null)
            return false;

        Ray ray = new Ray(controllerTransform.position, controllerTransform.forward);
        foreach (RectTransform handle in FindHandleRects())
        {
            if (handle != null && RectRayHit(handle, ray))
                return true;
        }

        return allowWholeWindowFallback && RectRayHit(windowRoot, ray);
    }

    private RectTransform[] FindHandleRects()
    {
        RectTransform[] rects = windowRoot.GetComponentsInChildren<RectTransform>(includeInactive: true);
        var matches = new System.Collections.Generic.List<RectTransform>();
        foreach (RectTransform rect in rects)
        {
            if (rect == null || rect == windowRoot)
                continue;
            if (string.IsNullOrWhiteSpace(handleNameContains) || rect.name.IndexOf(handleNameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                matches.Add(rect);
        }
        return matches.ToArray();
    }

    private Quaternion GetHeadsetFacingRotation(Quaternion fallback)
    {
        Transform headset = ResolveHeadsetTransform();
        if (headset == null || windowRoot == null)
            return fallback;

        Vector3 toPanel = windowRoot.position - headset.position;
        if (yawOnlyHeadsetFacing)
            toPanel.y = 0f;
        if (toPanel.sqrMagnitude < 0.0001f)
            return fallback;

        // World-space canvases face the camera when their forward points away from it.
        return Quaternion.LookRotation(toPanel.normalized, Vector3.up);
    }

    private Transform ResolveHeadsetTransform()
    {
        if (headsetTransform != null && headsetTransform.gameObject.activeInHierarchy)
            return headsetTransform;

        GameObject go =
            GameObject.Find("OVRCameraRig/TrackingSpace/CenterEyeAnchor") ??
            GameObject.Find(headsetName) ??
            (Camera.main != null ? Camera.main.gameObject : null);
        headsetTransform = go != null ? go.transform : null;
        return headsetTransform;
    }

    private bool RectRayHit(RectTransform rect, Ray ray)
    {
        Plane plane = new Plane(rect.forward, rect.position);
        if (!plane.Raycast(ray, out float distance))
            return false;
        if (distance < 0f || distance > Mathf.Max(0.05f, rayMaxDistance))
            return false;

        Vector3 hit = ray.GetPoint(distance);
        Vector3 local = rect.InverseTransformPoint(hit);
        return rect.rect.Contains(new Vector2(local.x, local.y));
    }

    private void ResolveControllerTransform()
    {
        if (dragController != DragController.Either && controllerTransform != null && controllerTransform.gameObject.activeInHierarchy)
            return;

        if (dragController == DragController.Left || dragController == DragController.Either)
            leftControllerTransform = ResolveControllerObject(DragController.Left);
        if (dragController == DragController.Right || dragController == DragController.Either)
            rightControllerTransform = ResolveControllerObject(DragController.Right);

        if (dragController == DragController.Left)
            controllerTransform = leftControllerTransform;
        else if (dragController == DragController.Right)
            controllerTransform = rightControllerTransform;
        else if (!IsDragging)
            controllerTransform = ChoosePressedControllerTransform() ?? leftControllerTransform ?? rightControllerTransform;
    }

    private static Transform ResolveControllerObject(DragController controller)
    {
        GameObject controllerObject = controller == DragController.Left
            ? GameObject.Find("OVRCameraRig/TrackingSpace/LeftControllerAnchor") ?? GameObject.Find("LeftControllerAnchor")
            : GameObject.Find("OVRCameraRig/TrackingSpace/RightControllerAnchor") ?? GameObject.Find("RightControllerAnchor");
        return controllerObject != null ? controllerObject.transform : null;
    }

    private Transform ChoosePressedControllerTransform()
    {
        if (IsDragButtonHeldFor(DragController.Left) && leftControllerTransform != null)
            return leftControllerTransform;
        if (IsDragButtonHeldFor(DragController.Right) && rightControllerTransform != null)
            return rightControllerTransform;
        return null;
    }

    private bool IsDragButtonHeld()
    {
        if (dragController == DragController.Either)
        {
            if (IsDragging && controllerTransform != null)
            {
                if (controllerTransform == leftControllerTransform)
                    return IsDragButtonHeldFor(DragController.Left);
                if (controllerTransform == rightControllerTransform)
                    return IsDragButtonHeldFor(DragController.Right);
            }
            return IsDragButtonHeldFor(DragController.Left) || IsDragButtonHeldFor(DragController.Right);
        }

        return IsDragButtonHeldFor(dragController);
    }

    private bool IsDragButtonHeldFor(DragController controllerSide)
    {
        OVRInput.Controller controller = controllerSide == DragController.Left
            ? OVRInput.Controller.LTouch
            : OVRInput.Controller.RTouch;

        if (dragButton == DragButton.Trigger)
            return GetTriggerValue(controllerSide, controller) >= Mathf.Clamp(buttonThreshold, 0.05f, 0.95f);

        if (dragButton == DragButton.Grip)
            return GetGripValue(controllerSide, controller) >= Mathf.Clamp(buttonThreshold, 0.05f, 0.95f);

        return controllerSide == DragController.Left
            ? OVRInput.Get(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch) || OVRInput.Get(OVRInput.RawButton.LThumbstick)
            : OVRInput.Get(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch) || OVRInput.Get(OVRInput.RawButton.RThumbstick);
    }

    private static float GetTriggerValue(DragController controllerSide, OVRInput.Controller directController)
    {
        if (controllerSide == DragController.Left)
        {
            return Mathf.Max(
                OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, directController),
                OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.Touch));
        }

        return Mathf.Max(
            OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, directController),
            OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger, OVRInput.Controller.Touch));
    }

    private static float GetGripValue(DragController controllerSide, OVRInput.Controller directController)
    {
        if (controllerSide == DragController.Left)
        {
            return Mathf.Max(
                OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, directController),
                OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.Touch));
        }

        return Mathf.Max(
            OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, directController),
            OVRInput.Get(OVRInput.Axis1D.SecondaryHandTrigger, OVRInput.Controller.Touch));
    }
}
