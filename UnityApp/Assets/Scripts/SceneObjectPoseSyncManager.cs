using System;
using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;
using RosMessageTypes.Geometry;

public class SceneObjectPoseSyncManager : MonoBehaviour
{
    [Serializable]
    public class SyncBinding
    {
        public string objectName;
        public string topicName;
        public Transform target;
        public bool applyOrientation = true;
    }

    [Header("ROS")]
    public Transform referenceFrame;
    public bool convertFromRosFluToUnity = true;
    public bool autoPopulateDefaultBindings = true;
    [Tooltip("Disable colliders on synchronized Unity visuals so Gazebo remains the only physics authority.")]
    public bool disableTargetCollidersForVisualization = true;

    [Header("Bindings")]
    public SyncBinding[] bindings =
    {
        new SyncBinding { objectName = "Sync_RedCube", topicName = "/unity_sync/Sync_RedCube_pose", applyOrientation = true },
        new SyncBinding { objectName = "Sync_GreenCube", topicName = "/unity_sync/Sync_GreenCube_pose", applyOrientation = true },
        new SyncBinding { objectName = "Sync_RedCylinder", topicName = "/unity_sync/Sync_RedCylinder_pose", applyOrientation = true },
        new SyncBinding { objectName = "Sync_GreenCylinder", topicName = "/unity_sync/Sync_GreenCylinder_pose", applyOrientation = true },
        new SyncBinding { objectName = "Sync_Plate_A", topicName = "/unity_sync/Sync_Plate_A_pose", applyOrientation = true },
        new SyncBinding { objectName = "Sync_Plate_B", topicName = "/unity_sync/Sync_Plate_B_pose", applyOrientation = true },
    };

    private readonly Dictionary<string, Transform> targetByTopic = new Dictionary<string, Transform>();
    private readonly Dictionary<string, bool> applyOrientationByTopic = new Dictionary<string, bool>();
    private readonly Dictionary<string, PoseState> latestPoseByTopic = new Dictionary<string, PoseState>();
    private readonly object poseLock = new object();
    private ROSConnection ros;

    private struct PoseState
    {
        public bool hasPose;
        public Vector3 position;
        public Quaternion rotation;
    }

    private void Awake()
    {
        if (referenceFrame == null)
        {
            var go = GameObject.Find("base_link") ?? GameObject.Find("UR5e") ?? GameObject.Find("ur5e");
            if (go != null)
                referenceFrame = go.transform;
        }

        if (autoPopulateDefaultBindings)
            TryBindTargetsByName();

        if (disableTargetCollidersForVisualization)
            DisableBoundTargetColliders();
    }

    private void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();

        foreach (var binding in bindings)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.topicName))
                continue;

            if (binding.target == null)
            {
                Debug.LogWarning($"[SceneObjectPoseSyncManager] No target bound for {binding.objectName} ({binding.topicName}).");
                continue;
            }

            string topic = binding.topicName;
            targetByTopic[topic] = binding.target;
            applyOrientationByTopic[topic] = binding.applyOrientation;
            latestPoseByTopic[topic] = default;
            ros.Subscribe<PoseStampedMsg>(topic, msg => OnPoseReceived(topic, msg));
            Debug.Log($"[SceneObjectPoseSyncManager] Subscribed {binding.objectName} <- {topic}");
        }
    }

    private void TryBindTargetsByName()
    {
        foreach (var binding in bindings)
        {
            if (binding == null || binding.target != null || string.IsNullOrWhiteSpace(binding.objectName))
                continue;

            var go = GameObject.Find(binding.objectName);
            if (go != null)
                binding.target = go.transform;
        }
    }

    private void DisableBoundTargetColliders()
    {
        int disabled = 0;
        foreach (var binding in bindings)
        {
            if (binding == null || binding.target == null)
                continue;

            Collider[] colliders = binding.target.GetComponentsInChildren<Collider>(includeInactive: true);
            foreach (Collider collider in colliders)
            {
                if (collider == null || !collider.enabled)
                    continue;

                collider.enabled = false;
                disabled++;
            }
        }

        if (disabled > 0)
            Debug.Log($"[SceneObjectPoseSyncManager] Disabled {disabled} synchronized-object colliders for visualization-only mode.");
    }

    private void OnPoseReceived(string topic, PoseStampedMsg msg)
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
            p = new Vector3((float)msg.pose.position.x, (float)msg.pose.position.y, (float)msg.pose.position.z);
            q = new Quaternion(
                (float)msg.pose.orientation.x,
                (float)msg.pose.orientation.y,
                (float)msg.pose.orientation.z,
                (float)msg.pose.orientation.w
            );
        }

        lock (poseLock)
        {
            latestPoseByTopic[topic] = new PoseState
            {
                hasPose = true,
                position = p,
                rotation = q,
            };
        }
    }

    private void Update()
    {
        foreach (var pair in targetByTopic)
        {
            PoseState pose;
            lock (poseLock)
            {
                if (!latestPoseByTopic.TryGetValue(pair.Key, out pose) || !pose.hasPose)
                    continue;
            }

            Transform target = pair.Value;
            if (target == null)
                continue;

            if (referenceFrame != null)
            {
                target.position = referenceFrame.TransformPoint(pose.position);
                if (applyOrientationByTopic.TryGetValue(pair.Key, out bool applyRot) && applyRot)
                    target.rotation = referenceFrame.rotation * pose.rotation;
            }
            else
            {
                target.position = pose.position;
                if (applyOrientationByTopic.TryGetValue(pair.Key, out bool applyRot) && applyRot)
                    target.rotation = pose.rotation;
            }
        }
    }
}
