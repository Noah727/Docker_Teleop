using UnityEngine;

public class ControllerDebugVisuals : MonoBehaviour
{
    [Header("Auto-Find Anchors")]
    public Transform leftControllerAnchor;
    public Transform rightControllerAnchor;

    [Header("Visual Settings")]
    public float markerScale = 0.06f;
    public bool createOnlyIfNoRenderer = true;
    public bool makeExistingControllerRenderersTransparent = true;
    [Range(0.05f, 1.0f)] public float idleAlpha = 0.48f;
    [Range(0.05f, 1.0f)] public float teleopGripHeldAlpha = 0.24f;

    private GameObject leftMarker;
    private GameObject rightMarker;

    void Start()
    {
        ResolveAnchors();
        CreateOrAttachMarkers();
    }

    void Update()
    {
        float leftAlpha = GetGripValue(true) >= 0.55f ? teleopGripHeldAlpha : idleAlpha;
        float rightAlpha = GetGripValue(false) >= 0.55f ? teleopGripHeldAlpha : idleAlpha;
        SetMarkerAlpha(leftMarker, leftAlpha);
        SetMarkerAlpha(rightMarker, rightAlpha);
        if (makeExistingControllerRenderersTransparent)
        {
            SetAnchorRenderersAlpha(leftControllerAnchor, leftAlpha);
            SetAnchorRenderersAlpha(rightControllerAnchor, rightAlpha);
        }
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
            ConfigureTransparentMaterial(mat);
            mat.color = new Color(color.r, color.g, color.b, idleAlpha);
            renderer.material = mat;
        }

        var collider = marker.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;

        return marker;
    }

    private static float GetGripValue(bool left)
    {
        return left
            ? Mathf.Max(OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch), OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.Touch))
            : Mathf.Max(OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch), OVRInput.Get(OVRInput.Axis1D.SecondaryHandTrigger, OVRInput.Controller.Touch));
    }

    private static void SetMarkerAlpha(GameObject marker, float alpha)
    {
        if (marker == null)
            return;
        Renderer[] renderers = marker.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer.material == null)
                continue;
            Color color = renderer.material.color;
            color.a = alpha;
            renderer.material.color = color;
        }
    }

    private static void SetAnchorRenderersAlpha(Transform anchor, float alpha)
    {
        if (anchor == null)
            return;
        Renderer[] renderers = anchor.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;
            foreach (Material material in renderer.materials)
            {
                ConfigureTransparentMaterial(material);
                Color color = material.color;
                color.a = alpha;
                material.color = color;
            }
        }
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;
        material.renderQueue = 3000;
        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 3f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_ALPHABLEND_ON");
    }
}
