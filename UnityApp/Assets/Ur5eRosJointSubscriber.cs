using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std; // Float64MultiArrayMsg

public class Ur5eRosJointSubscriber : MonoBehaviour
{
    [Header("ROS topic that sends 6 joint angles (radians)")]
    public string topicName = "/ur5e_joint_targets";

    // Ordered: shoulder, upper arm, forearm, wrist1, wrist2, wrist3
    [Header("Auto-filled by name on Awake (shoulder_link, upper_arm_link, etc.)")]
    public ArticulationBody[] joints = new ArticulationBody[6];

    private ROSConnection ros;
    private double[] latestTargetsRad;
    private bool haveTargets = false;

    void Awake()
    {
        // If user hasn't manually filled joints[], auto-detect by name
        bool needAutofill = joints == null || joints.Length == 0 || joints.Any(j => j == null);
        if (needAutofill)
        {
            var allBodies = GetComponentsInChildren<ArticulationBody>(includeInactive: true);

            string[] desiredNames =
            {
                "shoulder_link",
                "upper_arm_link",
                "forearm_link",
                "wrist_1_link",
                "wrist_2_link",
                "wrist_3_link"
            };

            List<ArticulationBody> ordered = new List<ArticulationBody>();

            foreach (string name in desiredNames)
            {
                var match = allBodies.FirstOrDefault(ab => ab.name.Contains(name));
                if (match != null)
                {
                    ordered.Add(match);
                }
                else
                {
                    Debug.LogWarning("[Ur5eRosJointSubscriber] Could not find joint with name containing '" + name + "'.");
                }
            }

            joints = ordered.ToArray();
        }

        Debug.Log("[Ur5eRosJointSubscriber] Final joint list (size " + joints.Length + "):");
        for (int i = 0; i < joints.Length; i++)
        {
            string jointName = (joints[i] != null) ? joints[i].name : "NULL";
            Debug.Log("  Joint " + i + ": " + jointName);
        }
    }

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<Float64MultiArrayMsg>(topicName, OnJointTargetsReceived);

        Debug.Log("[Ur5eRosJointSubscriber] Subscribed to " + topicName);
    }

    void OnJointTargetsReceived(Float64MultiArrayMsg msg)
    {
        if (msg.data == null || msg.data.Length == 0)
            return;

        int n = Mathf.Min(joints.Length, msg.data.Length);

        if (latestTargetsRad == null || latestTargetsRad.Length != n)
            latestTargetsRad = new double[n];

        for (int i = 0; i < n; i++)
            latestTargetsRad[i] = msg.data[i]; // radians

        haveTargets = true;

        Debug.Log("[Ur5eRosJointSubscriber] Received targets (rad): " + string.Join(", ", msg.data));
    }

    void FixedUpdate()
    {
        if (!haveTargets || latestTargetsRad == null || joints == null)
            return;

        int n = Mathf.Min(joints.Length, latestTargetsRad.Length);

        for (int i = 0; i < n; i++)
        {
            ApplyTarget(joints[i], latestTargetsRad[i]);
        }
    }

    void ApplyTarget(ArticulationBody joint, double angleRad)
    {
        if (joint == null)
            return;

        var drive = joint.xDrive;

        // Convert radians (ROS) -> degrees (Unity)
        float targetDeg = (float)(angleRad * Mathf.Rad2Deg);

        // Respect joint limits if they exist
        if (drive.lowerLimit < drive.upperLimit)
        {
            targetDeg = Mathf.Clamp(targetDeg, drive.lowerLimit, drive.upperLimit);
        }

        drive.target = targetDeg;
        joint.xDrive = drive;
    }
}