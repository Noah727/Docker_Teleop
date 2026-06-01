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

    [Header("Task profile")]
    [Tooltip("Prefer the generated task profile created from ros_backend1.1/profiles instead of the fallback bindings below.")]
    public bool loadBindingsFromGeneratedTaskProfile = true;
    public string taskProfileResourcePath = "TaskProfiles/active_task";

    [Header("Workspace placement")]
    [Tooltip("When present, incoming Gazebo-world poses are placed relative to this movable Unity workspace root.")]
    public Transform workspaceRoot;
    public string workspaceRootName = "GazeboWorkspace";
    public bool applyWorkspaceTransform = true;
    [Tooltip("Small Unity-local correction applied to every synced object after ROS/Gazebo pose conversion. Keep zero unless calibrating a measured visual offset.")]
    public Vector3 poseOffsetLocal = Vector3.zero;

    [Header("Visual smoothing")]
    [Tooltip("Interpolate synchronized object visuals toward the latest Gazebo pose. Keep off when visual contact alignment matters more than smoothing.")]
    public bool smoothPoseUpdates = false;
    [Tooltip("Higher values follow the incoming Gazebo position more tightly when smoothing is enabled.")]
    public float positionFollowSharpness = 90f;
    [Tooltip("Higher values follow the incoming Gazebo orientation more tightly when smoothing is enabled.")]
    public float rotationFollowSharpness = 90f;
    [Tooltip("If the received pose jumps farther than this, snap immediately instead of smoothing across the scene.")]
    public float snapDistanceMeters = 0.15f;

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
    private readonly HashSet<string> firstPoseAppliedByTopic = new HashSet<string>();
    private readonly object poseLock = new object();
    private ROSConnection ros;

#pragma warning disable 0649
    [Serializable]
    private class UnityTaskProfile
    {
        public UnityTaskObject[] objects;
    }

    [Serializable]
    private class UnityTaskObject
    {
        public string id;
    }
#pragma warning restore 0649

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

        if (loadBindingsFromGeneratedTaskProfile)
            LoadBindingsFromGeneratedTaskProfile();

        if (autoPopulateDefaultBindings)
            TryBindTargetsByName();

        if (disableTargetCollidersForVisualization)
            DisableBoundTargetColliders();

        ResolveWorkspaceRoot();
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

    private void LoadBindingsFromGeneratedTaskProfile()
    {
        if (string.IsNullOrWhiteSpace(taskProfileResourcePath))
            return;

        TextAsset asset = Resources.Load<TextAsset>(taskProfileResourcePath);
        if (asset == null)
        {
            Debug.Log($"[SceneObjectPoseSyncManager] Generated task profile not found at Resources/{taskProfileResourcePath}; using Inspector bindings.");
            return;
        }

        UnityTaskProfile profile;
        try
        {
            profile = JsonUtility.FromJson<UnityTaskProfile>(asset.text);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SceneObjectPoseSyncManager] Could not parse generated task profile {taskProfileResourcePath}: {ex.Message}");
            return;
        }

        if (profile == null || profile.objects == null || profile.objects.Length == 0)
        {
            Debug.LogWarning($"[SceneObjectPoseSyncManager] Generated task profile {taskProfileResourcePath} has no objects; using Inspector bindings.");
            return;
        }

        List<SyncBinding> generatedBindings = new List<SyncBinding>();
        HashSet<string> seenObjectNames = new HashSet<string>();
        foreach (UnityTaskObject taskObject in profile.objects)
        {
            string objectName = taskObject?.id;
            if (string.IsNullOrWhiteSpace(objectName) || !seenObjectNames.Add(objectName))
                continue;

            SyncBinding existing = FindBindingByObjectName(objectName);
            SyncBinding binding = existing ?? new SyncBinding();
            binding.objectName = objectName;
            if (string.IsNullOrWhiteSpace(binding.topicName))
                binding.topicName = $"/unity_sync/{objectName}_pose";
            else if (existing == null)
                binding.topicName = $"/unity_sync/{objectName}_pose";

            generatedBindings.Add(binding);
        }

        if (generatedBindings.Count == 0)
        {
            Debug.LogWarning($"[SceneObjectPoseSyncManager] Generated task profile {taskProfileResourcePath} produced no valid bindings; using Inspector bindings.");
            return;
        }

        bindings = generatedBindings.ToArray();
        Debug.Log($"[SceneObjectPoseSyncManager] Loaded {bindings.Length} sync bindings from Resources/{taskProfileResourcePath}.");
    }

    private SyncBinding FindBindingByObjectName(string objectName)
    {
        if (bindings == null)
            return null;

        foreach (SyncBinding binding in bindings)
        {
            if (binding != null && string.Equals(binding.objectName, objectName, StringComparison.Ordinal))
                return binding;
        }

        return null;
    }

    private void TryBindTargetsByName()
    {
        foreach (var binding in bindings)
        {
            if (binding == null || binding.target != null || string.IsNullOrWhiteSpace(binding.objectName))
                continue;

            binding.target = FindPreferredSyncTarget(binding.objectName);
        }
    }

    private Transform FindPreferredSyncTarget(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        ResolveWorkspaceRoot();

        Transform best = null;
        if (workspaceRoot != null)
        {
            Transform[] workspaceChildren = workspaceRoot.GetComponentsInChildren<Transform>(includeInactive: true);
            foreach (Transform candidate in workspaceChildren)
            {
                if (candidate == null || !string.Equals(candidate.name, objectName, StringComparison.Ordinal))
                    continue;

                if (IsUnderNamedParent(candidate, "GameObjects_sync"))
                    return candidate;

                if (best == null)
                    best = candidate;
            }
        }

        if (best != null)
            return best;

        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate != null && string.Equals(candidate.name, objectName, StringComparison.Ordinal))
                return candidate;
        }

        return null;
    }

    private static bool IsUnderNamedParent(Transform transform, string parentName)
    {
        Transform current = transform != null ? transform.parent : null;
        while (current != null)
        {
            if (string.Equals(current.name, parentName, StringComparison.Ordinal))
                return true;
            current = current.parent;
        }
        return false;
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
        if (applyWorkspaceTransform && workspaceRoot == null)
            ResolveWorkspaceRoot();

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

            Vector3 localPosition = pose.position + poseOffsetLocal;
            Vector3 desiredPosition;
            Quaternion desiredRotation = target.rotation;
            bool applyRotation = applyOrientationByTopic.TryGetValue(pair.Key, out bool applyRot) && applyRot;

            if (applyWorkspaceTransform && workspaceRoot != null)
            {
                desiredPosition = workspaceRoot.TransformPoint(localPosition);
                if (applyRotation)
                    desiredRotation = workspaceRoot.rotation * pose.rotation;
            }
            else if (referenceFrame != null)
            {
                desiredPosition = referenceFrame.TransformPoint(localPosition);
                if (applyRotation)
                    desiredRotation = referenceFrame.rotation * pose.rotation;
            }
            else
            {
                desiredPosition = localPosition;
                if (applyRotation)
                    desiredRotation = pose.rotation;
            }

            ApplyVisualPose(pair.Key, target, desiredPosition, desiredRotation, applyRotation);
        }
    }

    private void ApplyVisualPose(string topic, Transform target, Vector3 desiredPosition, Quaternion desiredRotation, bool applyRotation)
    {
        bool firstPose = !firstPoseAppliedByTopic.Contains(topic);
        if (firstPose)
            firstPoseAppliedByTopic.Add(topic);

        float distance = Vector3.Distance(target.position, desiredPosition);
        bool shouldSnap = !smoothPoseUpdates || firstPose || distance > snapDistanceMeters || Time.deltaTime <= 0f;
        if (shouldSnap)
        {
            target.position = desiredPosition;
            if (applyRotation)
                target.rotation = desiredRotation;
            return;
        }

        float positionSharpness = Mathf.Max(0.01f, positionFollowSharpness);
        float rotationSharpness = Mathf.Max(0.01f, rotationFollowSharpness);
        float positionAlpha = 1f - Mathf.Exp(-positionSharpness * Time.deltaTime);
        float rotationAlpha = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);

        target.position = Vector3.Lerp(target.position, desiredPosition, positionAlpha);
        if (applyRotation)
            target.rotation = Quaternion.Slerp(target.rotation, desiredRotation, rotationAlpha);
    }

    private void ResolveWorkspaceRoot()
    {
        if (!applyWorkspaceTransform || workspaceRoot != null || string.IsNullOrWhiteSpace(workspaceRootName))
            return;

        GameObject go = GameObject.Find(workspaceRootName);
        if (go != null)
            workspaceRoot = go.transform;
    }
}
