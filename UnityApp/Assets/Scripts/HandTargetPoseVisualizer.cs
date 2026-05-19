using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class HandTargetPoseVisualizer : MonoBehaviour
{
    [Header("ROS")]
    public string topicName = "/hand_target_pose";

    [Header("Visualization")]
    public Transform marker;
    public float markerScale = 0.12f;
    public bool convertFromRosToUnity = true;

    [Header("Optional Scene Alignment")]
    [Tooltip("If set, marker pose is applied relative to this transform (use your robot base/root).")]
    public Transform referenceFrame;

    private ROSConnection ros;
    private readonly object poseLock = new object();
    private Vector3 latestUnityPosition;
    private bool hasPose;

    private void Awake()
    {
        if (marker == null)
        {
            var markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            markerObject.name = "HandTargetMarker";
            markerObject.transform.SetParent(transform, false);
            markerObject.transform.localScale = Vector3.one * markerScale;

            var renderer = markerObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(1f, 0f, 1f, 1f);
            }

            var collider = markerObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            marker = markerObject.transform;
        }
        else
        {
            marker.localScale = Vector3.one * markerScale;
        }
    }

    private void Start()
    {
        if (referenceFrame == null)
        {
            var baseLink = GameObject.Find("base_link") ?? GameObject.Find("Base") ?? GameObject.Find("UR5e");
            if (baseLink != null)
            {
                referenceFrame = baseLink.transform;
                Debug.Log($"[HandTargetPoseVisualizer] Using reference frame: {referenceFrame.name}");
            }
        }

        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<PoseMsg>(topicName, OnPoseReceived);
        Debug.Log($"[HandTargetPoseVisualizer] Subscribed to {topicName}");
    }

    private void OnPoseReceived(PoseMsg msg)
    {
        var rosPos = new Vector3((float)msg.position.x, (float)msg.position.y, (float)msg.position.z);
        var unityPos = convertFromRosToUnity
            ? new Vector3(-rosPos.y, rosPos.z, rosPos.x)
            : rosPos;

        lock (poseLock)
        {
            latestUnityPosition = unityPos;
            hasPose = true;
        }
    }

    private void Update()
    {
        if (!hasPose || marker == null)
        {
            return;
        }

        Vector3 target;
        lock (poseLock)
        {
            target = latestUnityPosition;
        }

        if (referenceFrame != null)
        {
            marker.position = referenceFrame.TransformPoint(target);
        }
        else
        {
            marker.position = target;
        }
    }
}
