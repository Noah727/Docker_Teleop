using UnityEngine;

public class ControllerDebugVisuals : MonoBehaviour
{
    [Header("Auto-Find Anchors")]
    public Transform leftControllerAnchor;
    public Transform rightControllerAnchor;

    [Header("Visual Settings")]
    public float markerScale = 0.06f;
    public bool createOnlyIfNoRenderer = true;

    private GameObject leftMarker;
    private GameObject rightMarker;

    void Start()
    {
        ResolveAnchors();
        CreateOrAttachMarkers();
    }

    void ResolveAnchors()
    {
        if (leftControllerAnchor == null)
        {
            var left = GameObject.Find("OVRCameraRig/TrackingSpace/LeftControllerAnchor") ?? GameObject.Find("LeftControllerAnchor");
            if (left != null) leftControllerAnchor = left.transform;
        }

        if (rightControllerAnchor == null)
        {
            var right = GameObject.Find("OVRCameraRig/TrackingSpace/RightControllerAnchor") ?? GameObject.Find("RightControllerAnchor");
            if (right != null) rightControllerAnchor = right.transform;
        }
    }

    bool HasVisibleRenderer(Transform anchor)
    {
        if (anchor == null) return false;
        return anchor.GetComponentInChildren<Renderer>(true) != null;
    }

    void CreateOrAttachMarkers()
    {
        if (leftControllerAnchor != null && (!createOnlyIfNoRenderer || !HasVisibleRenderer(leftControllerAnchor)))
        {
            leftMarker = CreateMarker("LeftControllerMarker", Color.cyan);
            leftMarker.transform.SetParent(leftControllerAnchor, false);
            leftMarker.transform.localPosition = Vector3.zero;
            leftMarker.transform.localRotation = Quaternion.identity;
        }

        if (rightControllerAnchor != null && (!createOnlyIfNoRenderer || !HasVisibleRenderer(rightControllerAnchor)))
        {
            rightMarker = CreateMarker("RightControllerMarker", Color.green);
            rightMarker.transform.SetParent(rightControllerAnchor, false);
            rightMarker.transform.localPosition = Vector3.zero;
            rightMarker.transform.localRotation = Quaternion.identity;
        }
    }

    GameObject CreateMarker(string name, Color color)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = name;
        marker.transform.localScale = Vector3.one * markerScale;

        var renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            renderer.material = mat;
        }

        var collider = marker.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;

        return marker;
    }
}
