using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;
using RosMessageTypes.Sensor;
using RosMessageTypes.Trajectory;

public class Ur5eTrajectorySubscriber : MonoBehaviour
{
    [Header("Part4: JointState sync (recommended)")]
    public bool useJointStates = true;
    public string jointStatesTopic = "/joint_states";
    [Tooltip("Optional ROS joint prefix for multi-arm scenes, e.g. 'left_' or 'right_'.")]
    public string rosJointNamePrefix = "";
    [Tooltip("If true, also keep subscribing to trajectory topic while JointState sync is enabled.")]
    public bool subscribeTrajectoryWhileJointStatesEnabled = false;

    [Header("Legacy trajectory input")]
    [Tooltip("ROS topic publishing trajectory_msgs/JointTrajectory")]
    public string topicName = "/ur5e_joint_trajectory";

    [Header("Streaming")]
    [Tooltip("If true, accept 1-point JointTrajectory messages and treat them as streaming setpoints.")]
    public bool acceptSinglePointTrajectories = true;

    [Header("Assign 6 UR5e ArticulationBodies OR auto-find by name")]
    public ArticulationBody[] joints = new ArticulationBody[6];
    [Tooltip("If true, auto-fill joints[] by link name substrings")]
    public bool autoFillByName = true;

    [Header("Gripper visuals from /joint_states")]
    public bool visualizeGripperFromJointState = true;
    [Tooltip("If enabled, drive imported gripper ArticulationBodies directly. Disable for pure visual-only gripper motion.")]
    public bool useGripperArticulationBodies = false;
    [Tooltip("Legacy fallback gripper joint name. Prefer left/right names below for dual-finger visual sync.")]
    public string gripperJointName = "robotiq_hande_left_finger_joint";
    public string leftGripperJointName = "robotiq_hande_left_finger_joint";
    public string rightGripperJointName = "robotiq_hande_right_finger_joint";
    public bool autoFindGripperFingers = true;
    public Transform leftFinger;
    public Transform rightFinger;
    public string leftFingerNameContains = "HandE_LeftFinger";
    public string rightFingerNameContains = "HandE_RightFinger";
    [Tooltip("If true, visual finger travel uses the absolute gripper joint value instead of treating the first received value as zero.")]
    public bool useAbsoluteGripperJointForVisuals = true;
    [Tooltip("Joint value (meters) for fully closed Hand-E in this setup.")]
    public float gripperClosedPositionMeters = 0.0f;
    [Tooltip("Joint value (meters) for fully open Hand-E in this setup.")]
    public float gripperOpenPositionMeters = 0.025f;
    [Tooltip("Travel magnitude (meters) to apply when gripper goes from closed(0) to open(max).")]
    public float leftFingerTravelMeters = 0.025f;
    public float rightFingerTravelMeters = 0.025f;
    [Tooltip("Local travel axis for left/right finger motion (UR Hand-E defaults to local X).")]
    public Vector3 leftFingerLocalAxis = Vector3.right;
    public Vector3 rightFingerLocalAxis = Vector3.right;
    [Tooltip("Direction sign (+1 or -1) for left/right finger local-axis motion.")]
    public int leftFingerDirectionSign = 1;
    public int rightFingerDirectionSign = -1;
    [Tooltip("Constant local offset applied to the left finger visual baseline before travel motion.")]
    public Vector3 leftFingerVisualOffset = Vector3.zero;
    [Tooltip("Constant local offset applied to the right finger visual baseline before travel motion.")]
    public Vector3 rightFingerVisualOffset = Vector3.zero;

    [Header("Gripper visual diagnostics")]
    [Tooltip("Print periodic gripper joint/visual positions to Android logcat for Unity-vs-Gazebo comparisons.")]
    public bool debugGripperVisualSync = true;
    public float gripperVisualDebugLogPeriodSec = 2.0f;

    [Header("Playback")]
    [Tooltip("If a new trajectory arrives, restart playback from t=0")]
    public bool restartOnNewTrajectory = true;
    [Tooltip("Clamp incoming targets to Unity joint limits if present")]
    public bool clampToLimits = true;

    [Header("Drive initialization")]
    [Tooltip("Initialize articulation drives on startup so motion works without URDF Controller component")]
    public bool initializeDrivesOnStart = true;
    public float driveStiffness = 10000f;
    public float driveDamping = 100f;
    public float driveForceLimit = 1000f;
    public float jointFriction = 10f;
    public float jointAngularDamping = 10f;

    [Header("Visualizer-only physics")]
    [Tooltip("Disable gravity on UR articulation joints so Unity only mirrors ROS state.")]
    public bool disableGravityOnJoints = true;
    [Tooltip("Disable robot colliders at runtime so Unity does not resolve contacts that Gazebo already simulates.")]
    public bool disableRobotCollidersForVisualization = true;
    [Tooltip("When gripper articulation driving is disabled, also disable the imported finger ArticulationBodies.")]
    public bool disableGripperFingerArticulationWhenVisualOnly = true;
    [Tooltip("Disable colliders under the gripper fingers at runtime for visual-only gripper motion.")]
    public bool disableGripperFingerCollidersForVisualization = true;

    private readonly string[] urJointNames =
    {
        "shoulder_pan_joint",
        "shoulder_lift_joint",
        "elbow_joint",
        "wrist_1_joint",
        "wrist_2_joint",
        "wrist_3_joint"
    };

    private ROSConnection ros;

    // Trajectory buffer (legacy mode)
    private JointTrajectoryMsg currentTraj;
    private double trajStartWallTime;
    private bool playing;
    private int[] trajIndexToArtIndex;

    // JointState buffer (Part4 mode)
    private readonly object jointStateLock = new object();
    private readonly double[] latestJointStateRad = new double[6];
    private readonly bool[] haveJointStateRad = new bool[6];
    private bool haveAnyJointState;
    private bool haveGripperJointState;
    private bool haveLeftGripperJointState;
    private bool haveRightGripperJointState;
    private float latestGripperJointMeters;
    private float latestLeftGripperJointMeters;
    private float latestRightGripperJointMeters;
    private float nextJointStateLogTime;

    private bool gripperBaseCaptured;
    private Vector3 leftFingerBaseLocalPos;
    private Vector3 rightFingerBaseLocalPos;
    private ArticulationBody leftFingerJointBody;
    private ArticulationBody rightFingerJointBody;
    private bool gripperVisualReferenceCaptured;
    private float gripperVisualReferenceMeters;
    private float nextGripperVisualDebugLogTime;

    public bool HasLatestGripperJointState
    {
        get
        {
            lock (jointStateLock)
            {
                return haveGripperJointState || haveLeftGripperJointState || haveRightGripperJointState;
            }
        }
    }

    public float LatestGripperJointMeters
    {
        get
        {
            lock (jointStateLock)
            {
                if (haveLeftGripperJointState)
                    return latestLeftGripperJointMeters;
                return latestGripperJointMeters;
            }
        }
    }

    public Transform LeftFingerTransform => leftFinger;
    public Transform RightFingerTransform => rightFinger;

    private void Awake()
    {
        if (autoFillByName)
            TryAutofillJoints();

        if (autoFindGripperFingers)
            TryAutofillGripperFingers();

        if (useGripperArticulationBodies)
            TryBindGripperJointBodies();

        Debug.Log("[Ur5eTrajectorySubscriber] Final joints[]:");
        for (int i = 0; i < joints.Length; i++)
            Debug.Log($"  {i}: {(joints[i] ? joints[i].name : "NULL")}");
    }

    private void Start()
    {
        if (initializeDrivesOnStart)
            InitializeJointDrives();
        if (disableGravityOnJoints)
            DisableJointGravity();
        ApplyVisualizationOnlyPhysicsSettings();
        if (useGripperArticulationBodies)
            InitializeGripperJointDrives();

        CaptureGripperBaseIfNeeded();

        ros = ROSConnection.GetOrCreateInstance();

        if (useJointStates)
        {
            ros.Subscribe<JointStateMsg>(jointStatesTopic, OnJointStateReceived);
            Debug.Log("[Ur5eTrajectorySubscriber] Subscribed (joint_states): " + jointStatesTopic);
        }

        if (!useJointStates || subscribeTrajectoryWhileJointStatesEnabled)
        {
            ros.Subscribe<JointTrajectoryMsg>(topicName, OnTrajectoryReceived);
            Debug.Log("[Ur5eTrajectorySubscriber] Subscribed (trajectory): " + topicName);
        }

        Debug.Log(
            $"[Ur5eTrajectorySubscriber] Mode: useJointStates={useJointStates}, " +
            $"subscribeTrajectoryWhileJointStatesEnabled={subscribeTrajectoryWhileJointStatesEnabled}"
        );
    }

    private void InitializeJointDrives()
    {
        if (joints == null || joints.Length == 0)
        {
            Debug.LogWarning("[Ur5eTrajectorySubscriber] initializeDrivesOnStart=true but joints[] is empty.");
            return;
        }

        int configured = 0;
        for (int i = 0; i < joints.Length; i++)
        {
            var joint = joints[i];
            if (joint == null)
                continue;

            joint.jointFriction = jointFriction;
            joint.angularDamping = jointAngularDamping;

            var drive = joint.xDrive;
            drive.stiffness = driveStiffness;
            drive.damping = driveDamping;
            drive.forceLimit = driveForceLimit;
            joint.xDrive = drive;
            configured++;
        }

        Debug.Log(
            $"[Ur5eTrajectorySubscriber] Initialized drives on {configured} joints " +
            $"(stiffness={driveStiffness}, damping={driveDamping}, forceLimit={driveForceLimit})."
        );
    }

    private void DisableJointGravity()
    {
        if (joints == null || joints.Length == 0)
            return;

        int changed = 0;
        for (int i = 0; i < joints.Length; i++)
        {
            var joint = joints[i];
            if (joint == null)
                continue;
            if (joint.useGravity)
            {
                joint.useGravity = false;
                changed++;
            }
        }

        if (changed > 0)
            Debug.Log($"[Ur5eTrajectorySubscriber] Disabled gravity on {changed} articulation joints (visualizer mode).");
    }

    private void ApplyVisualizationOnlyPhysicsSettings()
    {
        int disabledRobotColliders = 0;
        if (disableRobotCollidersForVisualization)
            disabledRobotColliders = DisableEnabledCollidersUnder(transform);

        int disabledFingerColliders = 0;
        if (disableGripperFingerCollidersForVisualization && !disableRobotCollidersForVisualization)
        {
            disabledFingerColliders += DisableEnabledCollidersUnder(leftFinger);
            disabledFingerColliders += DisableEnabledCollidersUnder(rightFinger);
        }

        int disabledFingerArticulations = 0;
        if (!useGripperArticulationBodies && disableGripperFingerArticulationWhenVisualOnly)
        {
            disabledFingerArticulations += DisableArticulationBodyByName(leftFingerNameContains);
            disabledFingerArticulations += DisableArticulationBodyByName(rightFingerNameContains);
        }

        if (disabledRobotColliders > 0 || disabledFingerColliders > 0 || disabledFingerArticulations > 0)
        {
            Debug.Log(
                $"[Ur5eTrajectorySubscriber] Visualization-only physics cleanup: " +
                $"robotColliders={disabledRobotColliders}, fingerColliders={disabledFingerColliders}, " +
                $"fingerArticulationBodies={disabledFingerArticulations}."
            );
        }
    }

    private static int DisableEnabledCollidersUnder(Transform root)
    {
        if (root == null)
            return 0;

        int disabled = 0;
        Collider[] colliders = root.GetComponentsInChildren<Collider>(includeInactive: true);
        foreach (Collider collider in colliders)
        {
            if (collider == null || !collider.enabled)
                continue;

            collider.enabled = false;
            disabled++;
        }
        return disabled;
    }

    private int DisableArticulationBodyByName(string nameContains)
    {
        ArticulationBody body = FindArticulationBodyByNameContains(nameContains);
        if (body == null || !body.enabled)
            return 0;

        body.enabled = false;
        return 1;
    }

    private void TryAutofillJoints()
    {
        string[] desiredLinkContains =
        {
            "shoulder_link",
            "upper_arm_link",
            "forearm_link",
            "wrist_1_link",
            "wrist_2_link",
            "wrist_3_link"
        };

        var allBodies = GetComponentsInChildren<ArticulationBody>(includeInactive: true);
        var ordered = new List<ArticulationBody>();

        foreach (var key in desiredLinkContains)
        {
            var match = allBodies.FirstOrDefault(
                ab => ab != null && ab.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0
            );
            if (match == null)
                Debug.LogWarning($"[Ur5eTrajectorySubscriber] Missing ArticulationBody name contains '{key}'");
            ordered.Add(match);
        }

        joints = ordered.ToArray();
    }

    private void TryAutofillGripperFingers()
    {
        if (leftFinger == null)
            leftFinger = FindTransformByNameContains(leftFingerNameContains);
        if (rightFinger == null)
            rightFinger = FindTransformByNameContains(rightFingerNameContains);

        if (leftFinger == null)
            Debug.LogWarning($"[Ur5eTrajectorySubscriber] Missing left finger transform '{leftFingerNameContains}'");
        if (rightFinger == null)
            Debug.LogWarning($"[Ur5eTrajectorySubscriber] Missing right finger transform '{rightFingerNameContains}'");
    }

    private Transform FindTransformByNameContains(string needle)
    {
        if (string.IsNullOrWhiteSpace(needle))
            return null;

        var local = GetComponentsInChildren<Transform>(includeInactive: true);
        var localMatch = local.FirstOrDefault(
            t => t != null && t.name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
        );
        if (localMatch != null)
            return localMatch;

        var global = FindSceneObjects<Transform>();
        return global.FirstOrDefault(
            t => t != null && t.name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
        );
    }

    private void TryBindGripperJointBodies()
    {
        leftFingerJointBody = leftFinger != null ? leftFinger.GetComponent<ArticulationBody>() : null;
        rightFingerJointBody = rightFinger != null ? rightFinger.GetComponent<ArticulationBody>() : null;

        if (leftFingerJointBody == null)
            leftFingerJointBody = FindArticulationBodyByNameContains(leftFingerNameContains);
        if (rightFingerJointBody == null)
            rightFingerJointBody = FindArticulationBodyByNameContains(rightFingerNameContains);

        if (leftFingerJointBody != null || rightFingerJointBody != null)
        {
            Debug.Log(
                $"[Ur5eTrajectorySubscriber] Gripper articulation bodies: " +
                $"left={(leftFingerJointBody ? leftFingerJointBody.name : "NULL")}, " +
                $"right={(rightFingerJointBody ? rightFingerJointBody.name : "NULL")}"
            );
        }
    }

    private ArticulationBody FindArticulationBodyByNameContains(string needle)
    {
        if (string.IsNullOrWhiteSpace(needle))
            return null;

        var local = GetComponentsInChildren<ArticulationBody>(includeInactive: true);
        var localMatch = local.FirstOrDefault(
            ab => ab != null && ab.name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
        );
        if (localMatch != null)
            return localMatch;

        var global = FindSceneObjects<ArticulationBody>();
        return global.FirstOrDefault(
            ab => ab != null && ab.name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
        );
    }

    private static T[] FindSceneObjects<T>() where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return FindObjectsOfType<T>(true);
#endif
    }

    private void CaptureGripperBaseIfNeeded()
    {
        if (gripperBaseCaptured)
            return;

        if (leftFinger != null)
            leftFingerBaseLocalPos = leftFinger.localPosition;
        if (rightFinger != null)
            rightFingerBaseLocalPos = rightFinger.localPosition;
        gripperBaseCaptured = (leftFinger != null || rightFinger != null);
    }

    private void InitializeGripperJointDrives()
    {
        TryBindGripperJointBodies();

        int configured = 0;
        ConfigureGripperJointDrive(leftFingerJointBody, ref configured);
        ConfigureGripperJointDrive(rightFingerJointBody, ref configured);

        if (configured > 0)
        {
            Debug.Log(
                $"[Ur5eTrajectorySubscriber] Initialized {configured} gripper articulation joints " +
                $"for direct JointState mirroring."
            );
        }
    }

    private void ConfigureGripperJointDrive(ArticulationBody joint, ref int configured)
    {
        if (joint == null)
            return;

        joint.jointFriction = jointFriction;
        joint.angularDamping = jointAngularDamping;
        if (disableGravityOnJoints)
            joint.useGravity = false;

        var drive = joint.xDrive;
        drive.stiffness = driveStiffness;
        drive.damping = driveDamping;
        drive.forceLimit = Mathf.Max(drive.forceLimit, driveForceLimit);
        joint.xDrive = drive;
        configured++;
    }

    private void OnJointStateReceived(JointStateMsg msg)
    {
        if (msg == null || msg.name == null || msg.position == null)
            return;

        int n = Math.Min(msg.name.Length, msg.position.Length);
        if (n <= 0)
            return;

        bool touchedAny = false;
        float gripperVal = 0.0f;
        bool touchedGripper = false;
        float leftGripperVal = 0.0f;
        bool touchedLeftGripper = false;
        float rightGripperVal = 0.0f;
        bool touchedRightGripper = false;

        lock (jointStateLock)
        {
            for (int i = 0; i < n; i++)
            {
                string rosJointName = msg.name[i];
                double value = msg.position[i];

                int artIdx = GetArticulationIndexForRosJoint(rosJointName);
                if (artIdx >= 0 && artIdx < joints.Length)
                {
                    latestJointStateRad[artIdx] = value;
                    haveJointStateRad[artIdx] = true;
                    touchedAny = true;
                }

                if (IsRosJointNameMatch(rosJointName, gripperJointName))
                {
                    gripperVal = (float)value;
                    touchedGripper = true;
                }

                if (IsRosJointNameMatch(rosJointName, leftGripperJointName))
                {
                    leftGripperVal = (float)value;
                    touchedLeftGripper = true;
                }

                if (IsRosJointNameMatch(rosJointName, rightGripperJointName))
                {
                    rightGripperVal = (float)value;
                    touchedRightGripper = true;
                }
            }

            if (touchedAny)
                haveAnyJointState = true;
            if (touchedGripper)
            {
                latestGripperJointMeters = gripperVal;
                haveGripperJointState = true;
            }
            if (touchedLeftGripper)
            {
                latestLeftGripperJointMeters = leftGripperVal;
                haveLeftGripperJointState = true;
                latestGripperJointMeters = leftGripperVal;
                haveGripperJointState = true;
            }
            if (touchedRightGripper)
            {
                latestRightGripperJointMeters = rightGripperVal;
                haveRightGripperJointState = true;
                haveGripperJointState = true;
            }
        }

        if (Time.realtimeSinceStartup >= nextJointStateLogTime)
        {
            nextJointStateLogTime = Time.realtimeSinceStartup + 2.0f;
            Debug.Log(
                $"[Ur5eTrajectorySubscriber] JointState update: arm={touchedAny}, " +
                $"gripper={touchedGripper || touchedLeftGripper || touchedRightGripper}, " +
                $"leftGripper={touchedLeftGripper}, rightGripper={touchedRightGripper}, n={n}"
            );
        }
    }

    private int GetArticulationIndexForRosJoint(string rosJointName)
    {
        if (string.IsNullOrEmpty(rosJointName))
            return -1;

        for (int i = 0; i < urJointNames.Length; i++)
        {
            if (IsRosJointNameMatch(rosJointName, urJointNames[i]))
                return i;
        }
        return -1;
    }

    private bool IsRosJointNameMatch(string rosJointName, string baseJointName)
    {
        if (string.IsNullOrEmpty(rosJointName) || string.IsNullOrEmpty(baseJointName))
            return false;

        if (rosJointName == baseJointName || rosJointName.EndsWith("/" + baseJointName, StringComparison.Ordinal))
            return true;

        string prefix = string.IsNullOrWhiteSpace(rosJointNamePrefix) ? "" : rosJointNamePrefix.Trim();
        if (string.IsNullOrEmpty(prefix))
            return false;

        string prefixed = prefix + baseJointName;
        return rosJointName == prefixed || rosJointName.EndsWith("/" + prefixed, StringComparison.Ordinal);
    }

    private void OnTrajectoryReceived(JointTrajectoryMsg msg)
    {
        if (msg == null || msg.points == null || msg.points.Length == 0)
        {
            Debug.LogWarning("[Ur5eTrajectorySubscriber] Received empty/short trajectory.");
            return;
        }

        if (!acceptSinglePointTrajectories && msg.points.Length < 2)
        {
            Debug.LogWarning("[Ur5eTrajectorySubscriber] Received 1-point trajectory but acceptSinglePointTrajectories=false.");
            return;
        }

        currentTraj = msg;
        trajIndexToArtIndex = BuildIndexMap(currentTraj.joint_names);

        if (trajIndexToArtIndex == null)
        {
            Debug.LogError("[Ur5eTrajectorySubscriber] Could not build joint mapping. Not playing trajectory.");
            playing = false;
            return;
        }

        if (restartOnNewTrajectory || !playing)
        {
            trajStartWallTime = Time.timeAsDouble;
            playing = true;
        }

        Debug.Log(
            $"[Ur5eTrajectorySubscriber] Received trajectory: joints={msg.joint_names.Length}, " +
            $"points={msg.points.Length}, playing={playing}"
        );
    }

    private int[] BuildIndexMap(string[] msgJointNames)
    {
        if (msgJointNames == null || msgJointNames.Length == 0)
        {
            if (joints == null || joints.Length < 6 || joints.Any(j => j == null))
            {
                Debug.LogError("[Ur5eTrajectorySubscriber] joints[] not set correctly (need 6 non-null).");
                return null;
            }
            return new[] { 0, 1, 2, 3, 4, 5 };
        }

        var expected = new Dictionary<string, int>();
        for (int i = 0; i < urJointNames.Length; i++)
            expected[urJointNames[i]] = i;

        int[] map = new int[msgJointNames.Length];
        for (int i = 0; i < msgJointNames.Length; i++)
        {
            if (!expected.TryGetValue(msgJointNames[i], out int artIdx))
            {
                Debug.LogError($"[Ur5eTrajectorySubscriber] Unknown joint name in trajectory: {msgJointNames[i]}");
                return null;
            }
            map[i] = artIdx;
        }

        if (joints == null || joints.Length < 6 || joints.Any(j => j == null))
        {
            Debug.LogError("[Ur5eTrajectorySubscriber] joints[] not set correctly (need 6 non-null).");
            return null;
        }

        return map;
    }

    private void FixedUpdate()
    {
        bool appliedJointStates = false;
        if (useJointStates)
            appliedJointStates = ApplyJointStateBuffer();

        if (useJointStates && (appliedJointStates || !subscribeTrajectoryWhileJointStatesEnabled))
            return;

        if (!playing || currentTraj == null || currentTraj.points == null || currentTraj.points.Length == 0)
            return;

        if (currentTraj.points.Length == 1)
        {
            ApplyPoint(currentTraj.points[0]);
            return;
        }

        double t = Time.timeAsDouble - trajStartWallTime;
        int seg = FindSegment(currentTraj.points, t);

        if (seg >= currentTraj.points.Length - 1)
        {
            ApplyPoint(currentTraj.points[currentTraj.points.Length - 1]);
            playing = false;
            return;
        }

        var p0 = currentTraj.points[seg];
        var p1 = currentTraj.points[seg + 1];
        double t0 = TimeFromStartSeconds(p0);
        double t1 = TimeFromStartSeconds(p1);

        double alpha = 0.0;
        if (t1 > t0)
            alpha = Mathf.Clamp01((float)((t - t0) / (t1 - t0)));

        ApplyInterpolated(p0, p1, alpha);
    }

    private bool ApplyJointStateBuffer()
    {
        if (!haveAnyJointState && !haveGripperJointState && !haveLeftGripperJointState && !haveRightGripperJointState)
            return false;

        double[] armVals = new double[6];
        bool[] armHas = new bool[6];
        bool hasArm;
        bool hasGripper;
        float gripperMeters;
        bool hasLeftGripper;
        bool hasRightGripper;
        float leftGripperMeters;
        float rightGripperMeters;

        lock (jointStateLock)
        {
            hasArm = haveAnyJointState;
            hasGripper = haveGripperJointState;
            gripperMeters = latestGripperJointMeters;
            hasLeftGripper = haveLeftGripperJointState;
            hasRightGripper = haveRightGripperJointState;
            leftGripperMeters = latestLeftGripperJointMeters;
            rightGripperMeters = latestRightGripperJointMeters;
            Array.Copy(latestJointStateRad, armVals, latestJointStateRad.Length);
            Array.Copy(haveJointStateRad, armHas, haveJointStateRad.Length);
        }

        if (hasArm)
        {
            for (int i = 0; i < Math.Min(joints.Length, armVals.Length); i++)
            {
                if (armHas[i])
                    SetJointTargetRad(joints[i], armVals[i]);
            }
        }

        if (hasLeftGripper || hasRightGripper)
            ApplyGripperFromJointStates(leftGripperMeters, hasLeftGripper, rightGripperMeters, hasRightGripper, gripperMeters);
        else if (hasGripper)
            ApplyGripperFromJointState(gripperMeters);

        return hasArm || hasGripper || hasLeftGripper || hasRightGripper;
    }

    private void ApplyGripperFromJointState(float jointMeters)
    {
        ApplyGripperFromJointStates(jointMeters, true, jointMeters, true, jointMeters);
    }

    private void ApplyGripperFromJointStates(
        float leftJointMeters,
        bool hasLeftJointMeters,
        float rightJointMeters,
        bool hasRightJointMeters,
        float fallbackJointMeters
    )
    {
        if (!visualizeGripperFromJointState)
            return;

        if (!hasLeftJointMeters)
            leftJointMeters = fallbackJointMeters;
        if (!hasRightJointMeters)
            rightJointMeters = fallbackJointMeters;

        if (useGripperArticulationBodies)
        {
            bool droveArticulation = false;
            if (leftFingerJointBody != null)
            {
                SetPrismaticTargetMeters(leftFingerJointBody, leftJointMeters);
                droveArticulation = true;
            }

            if (rightFingerJointBody != null)
            {
                SetPrismaticTargetMeters(rightFingerJointBody, rightJointMeters);
                droveArticulation = true;
            }

            if (droveArticulation)
                return;
        }

        CaptureGripperBaseIfNeeded();
        if (!gripperBaseCaptured)
            return;

        float leftNormalized;
        float rightNormalized;
        if (useAbsoluteGripperJointForVisuals)
        {
            float range = Mathf.Max(1e-5f, gripperOpenPositionMeters - gripperClosedPositionMeters);
            leftNormalized = Mathf.Clamp01((leftJointMeters - gripperClosedPositionMeters) / range);
            rightNormalized = Mathf.Clamp01((rightJointMeters - gripperClosedPositionMeters) / range);
        }
        else
        {
            float open = Mathf.Max(1e-5f, gripperOpenPositionMeters);
            if (!gripperVisualReferenceCaptured)
            {
                gripperVisualReferenceMeters = fallbackJointMeters;
                gripperVisualReferenceCaptured = true;
            }

            leftNormalized = Mathf.Clamp((leftJointMeters - gripperVisualReferenceMeters) / open, -1f, 1f);
            rightNormalized = Mathf.Clamp((rightJointMeters - gripperVisualReferenceMeters) / open, -1f, 1f);
        }

        Vector3 leftAxis = leftFingerLocalAxis.sqrMagnitude > 1e-8f ? leftFingerLocalAxis.normalized : Vector3.right;
        Vector3 rightAxis = rightFingerLocalAxis.sqrMagnitude > 1e-8f ? rightFingerLocalAxis.normalized : Vector3.right;

        if (leftFinger != null)
        {
            Vector3 p = leftFingerBaseLocalPos + leftFingerVisualOffset;
            p += leftAxis * (Mathf.Sign(leftFingerDirectionSign) * leftFingerTravelMeters * leftNormalized);
            leftFinger.localPosition = p;
        }

        if (rightFinger != null)
        {
            Vector3 p = rightFingerBaseLocalPos + rightFingerVisualOffset;
            p += rightAxis * (Mathf.Sign(rightFingerDirectionSign) * rightFingerTravelMeters * rightNormalized);
            rightFinger.localPosition = p;
        }

        if (debugGripperVisualSync && Time.realtimeSinceStartup >= nextGripperVisualDebugLogTime)
        {
            nextGripperVisualDebugLogTime = Time.realtimeSinceStartup + Mathf.Max(0.2f, gripperVisualDebugLogPeriodSec);
            Debug.Log(
                "[Ur5eTrajectorySubscriber] GripperVisualSync " +
                $"prefix={rosJointNamePrefix}, " +
                $"jointL={leftJointMeters:F5}, jointR={rightJointMeters:F5}, " +
                $"normL={leftNormalized:F3}, normR={rightNormalized:F3}, " +
                $"leftLocal={FormatVector3(leftFinger != null ? leftFinger.localPosition : Vector3.zero)}, " +
                $"rightLocal={FormatVector3(rightFinger != null ? rightFinger.localPosition : Vector3.zero)}, " +
                $"leftBase={FormatVector3(leftFingerBaseLocalPos)}, rightBase={FormatVector3(rightFingerBaseLocalPos)}"
            );
        }
    }

    private static string FormatVector3(Vector3 value)
    {
        return $"({value.x:F4},{value.y:F4},{value.z:F4})";
    }

    private void SetPrismaticTargetMeters(ArticulationBody joint, float meters)
    {
        if (joint == null)
            return;

        var drive = joint.xDrive;
        float target = meters;
        if (drive.lowerLimit < drive.upperLimit)
            target = Mathf.Clamp(target, drive.lowerLimit, drive.upperLimit);

        drive.target = target;
        joint.xDrive = drive;
    }

    private int FindSegment(JointTrajectoryPointMsg[] points, double t)
    {
        for (int i = 0; i < points.Length - 1; i++)
        {
            double t0 = TimeFromStartSeconds(points[i]);
            double t1 = TimeFromStartSeconds(points[i + 1]);
            if (t >= t0 && t <= t1)
                return i;
        }
        return points.Length - 1;
    }

    private double TimeFromStartSeconds(JointTrajectoryPointMsg p)
    {
        return (double)p.time_from_start.sec + 1e-9 * (double)p.time_from_start.nanosec;
    }

    private void ApplyPoint(JointTrajectoryPointMsg p)
    {
        if (p.positions == null || trajIndexToArtIndex == null)
            return;

        int n = Math.Min(p.positions.Length, trajIndexToArtIndex.Length);
        for (int i = 0; i < n; i++)
        {
            int artIdx = trajIndexToArtIndex[i];
            SetJointTargetRad(joints[artIdx], p.positions[i]);
        }
    }

    private void ApplyInterpolated(JointTrajectoryPointMsg p0, JointTrajectoryPointMsg p1, double alpha)
    {
        if (p0.positions == null || p1.positions == null || trajIndexToArtIndex == null)
            return;

        int n = Math.Min(Math.Min(p0.positions.Length, p1.positions.Length), trajIndexToArtIndex.Length);
        for (int i = 0; i < n; i++)
        {
            double q0 = p0.positions[i];
            double q1 = p1.positions[i];
            double q = q0 + (q1 - q0) * alpha;
            int artIdx = trajIndexToArtIndex[i];
            SetJointTargetRad(joints[artIdx], q);
        }
    }

    private void SetJointTargetRad(ArticulationBody joint, double angleRad)
    {
        if (joint == null)
            return;

        var drive = joint.xDrive;
        float targetDeg = (float)(angleRad * Mathf.Rad2Deg);
        if (clampToLimits && drive.lowerLimit < drive.upperLimit)
            targetDeg = Mathf.Clamp(targetDeg, drive.lowerLimit, drive.upperLimit);

        drive.target = targetDeg;
        joint.xDrive = drive;
    }
}
