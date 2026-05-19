using System;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;
using RosMessageTypes.Geometry;

public class GazeboPoseStampedSubscriber : MonoBehaviour
{
    [Header("ROS")]
    public string topicName = "/unity_sync/target_cube_pose";

    [Header("Target")]
    [Tooltip("Recommended: assign an empty anchor here and put your custom Unity visual as a child.")]
    public Transform target;
    public bool autoCreateTargetIfMissing = true;
    public float targetScale = 0.06f;
    [Tooltip("Leave off if your assigned target is an empty sync anchor with a child visual that has its own scale.")]
    public bool applyTargetScaleToAssignedTarget = false;

    [Header("Frame alignment")]
    [Tooltip("Apply incoming pose relative to this frame (typically base_link / UR5e root).")]
    public Transform referenceFrame;
    public bool convertFromRosFluToUnity = true;
    public bool applyOrientation = true;

    [Header("Preview / Authoring")]
    [Tooltip("Applies a preview pose before the first ROS message arrives so you can lay out visuals without Gazebo/sync running.")]
    public bool previewWithoutRos = false;
    [Tooltip("If true, preview pose is interpreted in referenceFrame local space. Otherwise it is applied in world space.")]
    public bool previewRelativeToReferenceFrame = true;
    public Vector3 previewPosition = new Vector3(0.60f, 0.25f, 0.02f);
    public Vector3 previewEulerDegrees = Vector3.zero;

    private ROSConnection ros;
    private readonly object poseLock = new object();
    private bool hasPose;
    private Vector3 latestPosition;
    private Quaternion latestRotation = Quaternion.identity;
    private bool previewApplied;

    private void Awake()
    {
        if (target == null && autoCreateTargetIfMissing)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "TargetCubeSyncMarker";
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * targetScale;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.12f, 0.75f, 0.95f, 1.0f);

            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            target = go.transform;
        }
        else if (target != null && applyTargetScaleToAssignedTarget)
        {
            target.localScale = Vector3.one * targetScale;
        }
    }

    private void Start()
    {
        if (referenceFrame == null)
        {
            var go = GameObject.Find("base_link") ?? GameObject.Find("UR5e");
            if (go != null)
            {
                referenceFrame = go.transform;
                Debug.Log($"[GazeboPoseStampedSubscriber] Using reference frame: {referenceFrame.name}");
            }
        }

        ApplyPreviewPoseIfNeeded();

        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<PoseStampedMsg>(topicName, OnPoseReceived);
        Debug.Log($"[GazeboPoseStampedSubscriber] Subscribed to {topicName}");
    }

    private void OnPoseReceived(PoseStampedMsg msg)
    {
        if (msg == null || msg.pose == null)
            return;

        Vector3 p;
        Quaternion q;

        if (convertFromRosFluToUnity)
        {
            p = msg.pose.position.From<FLU>();
            q = msg.pose.orientation.From<FLU>();
        }
        else
        {
            p = new Vector3(
                (float)msg.pose.position.x,
                (float)msg.pose.position.y,
                (float)msg.pose.position.z
            );
            q = new Quaternion(
                (float)msg.pose.orientation.x,
                (float)msg.pose.orientation.y,
                (float)msg.pose.orientation.z,
                (float)msg.pose.orientation.w
            );
        }

        lock (poseLock)
        {
            latestPosition = p;
            latestRotation = q;
            hasPose = true;
        }
    }

    private void Update()
    {
        if (target == null)
            return;

        if (!hasPose)
        {
            ApplyPreviewPoseIfNeeded();
            return;
        }

        Vector3 p;
        Quaternion q;
        lock (poseLock)
        {
            p = latestPosition;
            q = latestRotation;
        }

        if (referenceFrame != null)
        {
            target.position = referenceFrame.TransformPoint(p);
            if (applyOrientation)
                target.rotation = referenceFrame.rotation * q;
        }
        else
        {
            target.position = p;
            if (applyOrientation)
                target.rotation = q;
        }
    }

    private void ApplyPreviewPoseIfNeeded()
    {
        if (!previewWithoutRos || previewApplied || target == null)
            return;

        Quaternion previewRotation = Quaternion.Euler(previewEulerDegrees);

        if (previewRelativeToReferenceFrame && referenceFrame != null)
        {
            target.position = referenceFrame.TransformPoint(previewPosition);
            if (applyOrientation)
                target.rotation = referenceFrame.rotation * previewRotation;
        }
        else
        {
            target.position = previewPosition;
            if (applyOrientation)
                target.rotation = previewRotation;
        }

        previewApplied = true;
    }
}
