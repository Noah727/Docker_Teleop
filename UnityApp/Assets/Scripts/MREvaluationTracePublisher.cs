using System;
using System.Collections.Generic;
using RosMessageTypes.BuiltinInterfaces;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;

/// <summary>
/// Publishes the displayed Unity/MR visual poses used by evaluation scripts.
/// Poses are expressed in GazeboWorkspace-local coordinates and converted to
/// ROS FLU so they can be compared directly with /unity_sync and EE pose topics.
/// </summary>
public class MREvaluationTracePublisher : MonoBehaviour
{
    [Serializable]
    public class PoseTraceTarget
    {
        public string label;
        public string topicName;
        public string objectName;
        public string rootName;
        public string childName;
        public Transform target;
    }

    [Header("Publishing")]
    public bool publishVisualTrace = true;
    [Min(1f)]
    public float publishRateHz = 60f;
    public string frameId = "world";

    [Header("Workspace")]
    public string workspaceRootName = "GazeboWorkspace";
    public Transform workspaceRoot;

    [Header("Targets")]
    public PoseTraceTarget[] targets =
    {
        new PoseTraceTarget
        {
            label = "red_cube_visual",
            topicName = "/unity_eval/Sync_RedCube_visual_pose",
            objectName = "Sync_RedCube"
        },
        new PoseTraceTarget
        {
            label = "left_tool0_visual",
            topicName = "/unity_eval/left_tool0_visual_pose",
            rootName = "left_ur5e",
            childName = "tool0"
        },
        new PoseTraceTarget
        {
            label = "left_wrist_3_visual",
            topicName = "/unity_eval/left_wrist_3_visual_pose",
            rootName = "left_ur5e",
            childName = "wrist_3_link"
        },
        new PoseTraceTarget
        {
            label = "left_hande_end_visual",
            topicName = "/unity_eval/left_hande_end_visual_pose",
            rootName = "left_ur5e",
            childName = "robotiq_hande_end"
        },
        new PoseTraceTarget
        {
            label = "right_tool0_visual",
            topicName = "/unity_eval/right_tool0_visual_pose",
            rootName = "right_ur5e",
            childName = "tool0"
        },
        new PoseTraceTarget
        {
            label = "right_wrist_3_visual",
            topicName = "/unity_eval/right_wrist_3_visual_pose",
            rootName = "right_ur5e",
            childName = "wrist_3_link"
        },
        new PoseTraceTarget
        {
            label = "right_hande_end_visual",
            topicName = "/unity_eval/right_hande_end_visual_pose",
            rootName = "right_ur5e",
            childName = "robotiq_hande_end"
        },
    };

    [Header("Diagnostics")]
    public float warningLogPeriodSec = 5f;

    private ROSConnection ros;
    private readonly HashSet<string> registeredTopics = new HashSet<string>();
    private float nextPublishTime;
    private float nextResolveTime;
    private float nextWarningTime;

    private void Start()
    {
        ResolveWorkspaceRoot();
        ResolveTargets(force: true);
        ros = ROSConnection.GetOrCreateInstance();
        RegisterPublishers();
    }

    private void Update()
    {
        if (!publishVisualTrace)
            return;

        if (workspaceRoot == null || Time.unscaledTime >= nextResolveTime)
        {
            ResolveWorkspaceRoot();
            ResolveTargets(force: false);
            nextResolveTime = Time.unscaledTime + 1f;
        }

        float period = 1f / Mathf.Max(1f, publishRateHz);
        if (Time.unscaledTime < nextPublishTime)
            return;

        nextPublishTime = Time.unscaledTime + period;
        RegisterPublishers();
        PublishTargets();
    }

    private void RegisterPublishers()
    {
        if (ros == null || targets == null)
            return;

        foreach (PoseTraceTarget target in targets)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.topicName))
                continue;

            if (registeredTopics.Add(target.topicName))
                ros.RegisterPublisher<PoseStampedMsg>(target.topicName);
        }
    }

    private void PublishTargets()
    {
        if (ros == null || workspaceRoot == null || targets == null)
            return;

        foreach (PoseTraceTarget target in targets)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.topicName))
                continue;

            if (target.target == null)
                ResolveTarget(target);

            if (target.target == null)
            {
                LogMissingTarget(target);
                continue;
            }

            ros.Publish(target.topicName, BuildPoseStamped(target.target));
        }
    }

    private PoseStampedMsg BuildPoseStamped(Transform target)
    {
        Vector3 localPosition = workspaceRoot.InverseTransformPoint(target.position);
        Quaternion localRotation = Quaternion.Inverse(workspaceRoot.rotation) * target.rotation;
        double t = Time.realtimeSinceStartupAsDouble;
        int sec = (int)Math.Floor(t);
        uint nanosec = (uint)Math.Floor((t - sec) * 1000000000.0);

        return new PoseStampedMsg
        {
            header = new HeaderMsg(new TimeMsg(sec, nanosec), frameId),
            pose = new PoseMsg
            {
                position = localPosition.To<FLU>(),
                orientation = localRotation.To<FLU>(),
            },
        };
    }

    private void ResolveWorkspaceRoot()
    {
        if (workspaceRoot != null || string.IsNullOrWhiteSpace(workspaceRootName))
            return;

        GameObject workspace = GameObject.Find(workspaceRootName);
        if (workspace != null)
            workspaceRoot = workspace.transform;
    }

    private void ResolveTargets(bool force)
    {
        if (targets == null)
            return;

        foreach (PoseTraceTarget target in targets)
        {
            if (target == null)
                continue;
            if (!force && target.target != null)
                continue;
            ResolveTarget(target);
        }
    }

    private void ResolveTarget(PoseTraceTarget target)
    {
        if (target == null)
            return;

        if (!string.IsNullOrWhiteSpace(target.objectName))
        {
            Transform found = FindByNameUnderWorkspace(target.objectName);
            if (found != null)
            {
                target.target = found;
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(target.rootName) && !string.IsNullOrWhiteSpace(target.childName))
        {
            GameObject root = GameObject.Find(target.rootName);
            if (root != null)
            {
                Transform found = FindChildRecursive(root.transform, target.childName);
                if (found != null)
                {
                    target.target = found;
                    return;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(target.childName))
        {
            GameObject fallback = GameObject.Find(target.childName);
            if (fallback != null)
                target.target = fallback.transform;
        }
    }

    private Transform FindByNameUnderWorkspace(string objectName)
    {
        if (workspaceRoot != null)
        {
            foreach (Transform candidate in workspaceRoot.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (candidate != null && string.Equals(candidate.name, objectName, StringComparison.Ordinal))
                    return candidate;
            }
        }

        GameObject fallback = GameObject.Find(objectName);
        return fallback != null ? fallback.transform : null;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(includeInactive: true))
        {
            if (child != null && string.Equals(child.name, childName, StringComparison.Ordinal))
                return child;
        }

        return null;
    }

    private void LogMissingTarget(PoseTraceTarget target)
    {
        if (Time.unscaledTime < nextWarningTime)
            return;

        nextWarningTime = Time.unscaledTime + Mathf.Max(1f, warningLogPeriodSec);
        string name = !string.IsNullOrWhiteSpace(target.objectName)
            ? target.objectName
            : $"{target.rootName}/{target.childName}";
        Debug.LogWarning($"[MREvaluationTracePublisher] Missing target for {target.topicName}: {name}");
    }
}
