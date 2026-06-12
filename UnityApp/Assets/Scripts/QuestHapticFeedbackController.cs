using System.Collections.Generic;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;

[DefaultExecutionOrder(200)]
public class QuestHapticFeedbackController : MonoBehaviour
{
    public static QuestHapticFeedbackController ActiveInstance { get; private set; }

    [Header("References")]
    public HandPoseSender handPoseSender;
    public Ur5eTrajectorySubscriber robotSubscriber;

    [Header("EE Error Haptics")]
    public bool enableEeGapHaptics = false;
    public string leftTargetEePoseTopic = "/left_arm/teleop/target_ee_pose";
    public string leftActualEePoseTopic = "/left_arm/teleop/actual_ee_pose";
    public string rightTargetEePoseTopic = "/right_arm/teleop/target_ee_pose";
    public string rightActualEePoseTopic = "/right_arm/teleop/actual_ee_pose";
    public string targetEePoseTopic = "/teleop/target_ee_pose";
    public string actualEePoseTopic = "/teleop/actual_ee_pose";
    public float poseFreshTimeoutSec = 0.5f;
    public float gapDeadbandMeters = 0.025f;
    public float gapActivationThresholdMeters = 0.08f;
    public float gapReleaseThresholdMeters = 0.045f;
    public float gapActivationHoldSec = 0.35f;
    public float gapMaxMeters = 0.16f;
    [Range(0.0f, 1.0f)] public float maxGapAmplitude = 0.65f;
    [Range(0.0f, 1.0f)] public float gapFrequency = 0.55f;
    [Tooltip("When enabled, EE-gap vibration is only allowed while ROS/Gazebo contact haptics are also reporting contact. Keep enabled so normal servo lag does not vibrate.")]
    public bool requireRosContactForGapHaptics = true;

    [Header("Pinch Confirmation Haptics")]
    public bool enablePinchContactPulse = false;
    public bool requireObjectNearForPinchPulse = true;
    public string[] syncedObjectNames =
    {
        "Sync_RedCube", "Sync_GreenCube", "Sync_RedCylinder", "Sync_GreenCylinder",
        "Sync_CableReceiverBox", "Sync_CableReceiverBox_Fixed", "Sync_CableRod", "Sync_CableRod_B",
        "Sync_Rubik2x2"
    };
    public float objectNearRadiusMeters = 0.08f;
    public float gripperContactMinGapMeters = 0.003f;
    public float gripperOpenRearmMeters = 0.018f;
    public float gripperStallVelocityMetersPerSec = 0.002f;
    public float gripperStallConfirmSec = 0.18f;
    [Range(0.0f, 1.0f)] public float pinchPulseAmplitude = 0.85f;
    [Range(0.0f, 1.0f)] public float pinchPulseFrequency = 0.75f;
    public float pinchPulseOnSec = 0.075f;
    public float pinchPulseGapSec = 0.055f;

    [Header("ROS/Gazebo Contact Haptics")]
    public bool enableRosContactHaptics = true;
    public string leftContactAmplitudeTopic = "/left_arm/haptics/contact_amplitude";
    public string rightContactAmplitudeTopic = "/right_arm/haptics/contact_amplitude";
    public float contactFreshTimeoutSec = 0.40f;
    [Range(0.0f, 1.0f)] public float contactFrequency = 0.75f;

    [Header("Output")]
    public bool hapticOutputEnabled = true;
    [Range(0.0f, 2.0f)] public float defaultOutputGain = 0.35f;
    [Range(0.0f, 2.0f)] public float outputGain = 0.35f;
    public OVRInput.Controller hapticController = OVRInput.Controller.RTouch;
    public bool vibrateBothControllersForGap = false;

    public bool HasEePosePair { get; private set; }
    public float CurrentGapMeters { get; private set; }
    public float CurrentAmplitude { get; private set; }
    public bool PulseActive => pulseStage != 0;
    public string LastStatus { get; private set; } = "Not initialized";

    private ROSConnection ros;
    private bool subscribed;
    private float nextSubscribeAttemptTime;
    private float nextSubscribeWarnTime;
    private Vector3 targetEePosition;
    private Vector3 actualEePosition;
    private Vector3 leftTargetEePosition;
    private Vector3 leftActualEePosition;
    private Vector3 rightTargetEePosition;
    private Vector3 rightActualEePosition;
    private float lastTargetPoseTime = -999f;
    private float lastActualPoseTime = -999f;
    private float lastLeftTargetPoseTime = -999f;
    private float lastLeftActualPoseTime = -999f;
    private float lastRightTargetPoseTime = -999f;
    private float lastRightActualPoseTime = -999f;
    private float leftGapAboveThresholdSince = -1f;
    private float rightGapAboveThresholdSince = -1f;
    private bool leftGapHapticActive;
    private bool rightGapHapticActive;
    private float leftGapMeters;
    private float rightGapMeters;
    private readonly List<Transform> syncedObjectTransforms = new List<Transform>();
    private float nextObjectRefreshTime;
    private float lastGripperValue;
    private float lastGripperSampleTime = -1f;
    private float closingStallTime;
    private bool pinchPulseArmed = true;
    private int pulseStage;
    private float pulseStageEndTime;
    private float lastSentAmplitude = -1f;
    private bool rosHapticsSubscribed;
    private float leftRosContactAmplitude;
    private float rightRosContactAmplitude;
    private float lastLeftRosContactTime = -999f;
    private float lastRightRosContactTime = -999f;
    private float lastSentLeftAmplitude = -1f;
    private float lastSentRightAmplitude = -1f;

    private void Awake()
    {
        ActiveInstance = this;
    }

    private void Start()
    {
        if (!enableEeGapHaptics && !enablePinchContactPulse && !enableRosContactHaptics)
        {
            LastStatus = "haptics disabled";
            StopHaptics();
            enabled = false;
            return;
        }

        ResolveReferences();
        SubscribeToRosPoseTopics();
        SubscribeToRosContactTopics();
        RefreshSyncedObjectList();
    }

    private void Update()
    {
        ResolveReferences();
        SubscribeToRosPoseTopics();
        SubscribeToRosContactTopics();

        if (Time.unscaledTime >= nextObjectRefreshTime)
            RefreshSyncedObjectList();

        UpdatePinchPulseDetection();
        float legacyPulseAmplitude = UpdatePulseState();
        float leftGapAmplitude = ComputeGapAmplitude(left: true);
        float rightGapAmplitude = ComputeGapAmplitude(left: false);
        float legacyAmplitude = legacyPulseAmplitude;
        float legacyFrequency = pinchPulseFrequency;

        float leftContactAmplitude = ComputeFreshRosContactAmplitude(true);
        float rightContactAmplitude = ComputeFreshRosContactAmplitude(false);
        float leftOutput = Mathf.Max(leftContactAmplitude, leftGapAmplitude);
        float rightOutput = Mathf.Max(rightContactAmplitude, rightGapAmplitude);
        float leftFrequency = leftGapAmplitude > leftContactAmplitude ? gapFrequency : contactFrequency;
        float rightFrequency = rightGapAmplitude > rightContactAmplitude ? gapFrequency : contactFrequency;

        if (legacyAmplitude > 0f)
        {
            bool bothLegacy = vibrateBothControllersForGap && legacyPulseAmplitude <= 0f;
            if (bothLegacy || hapticController == OVRInput.Controller.Touch || hapticController == OVRInput.Controller.LTouch)
                leftOutput = Mathf.Max(leftOutput, legacyAmplitude);
            if (bothLegacy || hapticController == OVRInput.Controller.Touch || hapticController == OVRInput.Controller.RTouch)
                rightOutput = Mathf.Max(rightOutput, legacyAmplitude);
        }

        if (!hapticOutputEnabled)
        {
            leftOutput = 0f;
            rightOutput = 0f;
        }
        else
        {
            leftOutput = Mathf.Clamp01(leftOutput * Mathf.Max(0f, outputGain));
            rightOutput = Mathf.Clamp01(rightOutput * Mathf.Max(0f, outputGain));
        }

        SendControllerHaptics(OVRInput.Controller.LTouch, leftFrequency, leftOutput, ref lastSentLeftAmplitude);
        SendControllerHaptics(OVRInput.Controller.RTouch, rightFrequency, rightOutput, ref lastSentRightAmplitude);
        if (legacyAmplitude > 0f && leftOutput <= 0f && rightOutput <= 0f)
            SendHaptics(legacyFrequency, legacyAmplitude);
        else
            lastSentAmplitude = Mathf.Max(leftOutput, rightOutput);

        CurrentAmplitude = Mathf.Max(leftOutput, rightOutput);
        LastStatus =
            $"enabled={hapticOutputEnabled} gain={outputGain:F2} ros={enableRosContactHaptics} " +
            $"rosContact L={leftContactAmplitude:F2} R={rightContactAmplitude:F2} " +
            $"gap L={leftGapMeters:F3} R={rightGapMeters:F3} active L={leftGapHapticActive} R={rightGapHapticActive} " +
            $"amp={CurrentAmplitude:F2} poses={HasEePosePair} pulse={PulseActive}";
    }

    public void ToggleHapticOutput()
    {
        hapticOutputEnabled = !hapticOutputEnabled;
        if (!hapticOutputEnabled)
            StopHaptics();
    }

    public void ToggleRosContactHaptics()
    {
        enableRosContactHaptics = !enableRosContactHaptics;
        if (!enableRosContactHaptics)
            StopHaptics();
    }

    public void AdjustOutputGain(float delta)
    {
        outputGain = Mathf.Clamp(outputGain + delta, 0f, 2f);
        if (outputGain <= 0f)
            StopHaptics();
    }

    public void ResetOutputGain()
    {
        outputGain = Mathf.Clamp(defaultOutputGain, 0f, 2f);
    }

    private void ResolveReferences()
    {
        if (handPoseSender == null)
            handPoseSender = FindAny<HandPoseSender>();
        if (robotSubscriber == null)
            robotSubscriber = FindAny<Ur5eTrajectorySubscriber>();
    }

    private void SubscribeToRosPoseTopics()
    {
        if (!enableEeGapHaptics)
            return;
        if (subscribed)
            return;

        float now = Time.unscaledTime;
        if (now < nextSubscribeAttemptTime)
            return;
        nextSubscribeAttemptTime = now + 1.0f;

        try
        {
            ros = ROSConnection.GetOrCreateInstance();
            SubscribePoseTopic(leftTargetEePoseTopic, OnLeftTargetEePose);
            SubscribePoseTopic(leftActualEePoseTopic, OnLeftActualEePose);
            SubscribePoseTopic(rightTargetEePoseTopic, OnRightTargetEePose);
            SubscribePoseTopic(rightActualEePoseTopic, OnRightActualEePose);
            if (!string.IsNullOrWhiteSpace(targetEePoseTopic) && !string.IsNullOrWhiteSpace(actualEePoseTopic))
            {
                SubscribePoseTopic(targetEePoseTopic, OnTargetEePose);
                SubscribePoseTopic(actualEePoseTopic, OnActualEePose);
            }
            subscribed = true;
            Debug.Log("[QuestHapticFeedbackController] Subscribed to EE target/actual pose topics for haptic gap feedback.");
        }
        catch (System.Exception e)
        {
            if (now >= nextSubscribeWarnTime)
            {
                nextSubscribeWarnTime = now + 2.0f;
                Debug.LogWarning($"[QuestHapticFeedbackController] ROS pose subscription not ready: {e.Message}");
            }
        }
    }

    private void SubscribePoseTopic(string topicName, System.Action<PoseStampedMsg> callback)
    {
        if (string.IsNullOrWhiteSpace(topicName) || callback == null)
            return;
        ros.Subscribe<PoseStampedMsg>(topicName, callback);
    }

    private void SubscribeToRosContactTopics()
    {
        if (!enableRosContactHaptics || rosHapticsSubscribed)
            return;

        float now = Time.unscaledTime;
        if (now < nextSubscribeAttemptTime)
            return;
        nextSubscribeAttemptTime = now + 1.0f;

        try
        {
            ros = ROSConnection.GetOrCreateInstance();
            ros.Subscribe<Float32Msg>(leftContactAmplitudeTopic, OnLeftContactAmplitude);
            ros.Subscribe<Float32Msg>(rightContactAmplitudeTopic, OnRightContactAmplitude);
            rosHapticsSubscribed = true;
            Debug.Log($"[QuestHapticFeedbackController] Subscribed to {leftContactAmplitudeTopic} and {rightContactAmplitudeTopic}");
        }
        catch (System.Exception e)
        {
            if (now >= nextSubscribeWarnTime)
            {
                nextSubscribeWarnTime = now + 2.0f;
                Debug.LogWarning($"[QuestHapticFeedbackController] ROS haptic subscription not ready: {e.Message}");
            }
        }
    }

    private void OnLeftContactAmplitude(Float32Msg msg)
    {
        leftRosContactAmplitude = Mathf.Clamp01(msg.data);
        lastLeftRosContactTime = Time.unscaledTime;
    }

    private void OnRightContactAmplitude(Float32Msg msg)
    {
        rightRosContactAmplitude = Mathf.Clamp01(msg.data);
        lastRightRosContactTime = Time.unscaledTime;
    }

    private float ComputeFreshRosContactAmplitude(bool left)
    {
        if (!enableRosContactHaptics)
            return 0f;

        float lastTime = left ? lastLeftRosContactTime : lastRightRosContactTime;
        if (Time.unscaledTime - lastTime > contactFreshTimeoutSec)
            return 0f;

        return left ? leftRosContactAmplitude : rightRosContactAmplitude;
    }

    private void OnTargetEePose(PoseStampedMsg msg)
    {
        if (msg == null || msg.pose == null)
            return;

        targetEePosition = ToVector3(msg.pose.position);
        lastTargetPoseTime = Time.unscaledTime;
    }

    private void OnActualEePose(PoseStampedMsg msg)
    {
        if (msg == null || msg.pose == null)
            return;

        actualEePosition = ToVector3(msg.pose.position);
        lastActualPoseTime = Time.unscaledTime;
    }

    private void OnLeftTargetEePose(PoseStampedMsg msg)
    {
        if (msg == null || msg.pose == null)
            return;
        leftTargetEePosition = ToVector3(msg.pose.position);
        lastLeftTargetPoseTime = Time.unscaledTime;
    }

    private void OnLeftActualEePose(PoseStampedMsg msg)
    {
        if (msg == null || msg.pose == null)
            return;
        leftActualEePosition = ToVector3(msg.pose.position);
        lastLeftActualPoseTime = Time.unscaledTime;
    }

    private void OnRightTargetEePose(PoseStampedMsg msg)
    {
        if (msg == null || msg.pose == null)
            return;
        rightTargetEePosition = ToVector3(msg.pose.position);
        lastRightTargetPoseTime = Time.unscaledTime;
    }

    private void OnRightActualEePose(PoseStampedMsg msg)
    {
        if (msg == null || msg.pose == null)
            return;
        rightActualEePosition = ToVector3(msg.pose.position);
        lastRightActualPoseTime = Time.unscaledTime;
    }

    private float ComputeGapAmplitude(bool left)
    {
        if (!enableEeGapHaptics)
        {
            HasEePosePair = false;
            CurrentGapMeters = 0f;
            leftGapMeters = 0f;
            rightGapMeters = 0f;
            leftGapHapticActive = false;
            rightGapHapticActive = false;
            return 0f;
        }

        float now = Time.unscaledTime;
        bool fresh;
        bool teleopHeld;
        Vector3 target;
        Vector3 actual;
        if (left)
        {
            fresh = now - lastLeftTargetPoseTime <= poseFreshTimeoutSec && now - lastLeftActualPoseTime <= poseFreshTimeoutSec;
            teleopHeld = handPoseSender != null && handPoseSender.IsLeftTeleopHeld;
            target = leftTargetEePosition;
            actual = leftActualEePosition;
        }
        else
        {
            fresh = now - lastRightTargetPoseTime <= poseFreshTimeoutSec && now - lastRightActualPoseTime <= poseFreshTimeoutSec;
            teleopHeld = handPoseSender != null && handPoseSender.IsRightTeleopHeld;
            target = rightTargetEePosition;
            actual = rightActualEePosition;
        }

        bool legacyFresh = now - lastTargetPoseTime <= poseFreshTimeoutSec && now - lastActualPoseTime <= poseFreshTimeoutSec;
        HasEePosePair = fresh || legacyFresh;

        if (!fresh || !teleopHeld)
        {
            SetGapState(left, 0f, false, -1f);
            return 0f;
        }

        if (requireRosContactForGapHaptics && ComputeFreshRosContactAmplitude(left) <= 0f)
        {
            SetGapState(left, 0f, false, -1f);
            return 0f;
        }

        float gap = Vector3.Distance(target, actual);
        float activationThreshold = Mathf.Max(gapDeadbandMeters, gapActivationThresholdMeters);
        float releaseThreshold = Mathf.Clamp(gapReleaseThresholdMeters, gapDeadbandMeters, activationThreshold);
        bool active = left ? leftGapHapticActive : rightGapHapticActive;
        float aboveSince = left ? leftGapAboveThresholdSince : rightGapAboveThresholdSince;

        if (gap >= activationThreshold)
        {
            if (aboveSince < 0f)
                aboveSince = now;
            if (!active && now - aboveSince >= Mathf.Max(0f, gapActivationHoldSec))
                active = true;
        }
        else if (gap <= releaseThreshold)
        {
            active = false;
            aboveSince = -1f;
        }

        SetGapState(left, gap, active, aboveSince);
        CurrentGapMeters = Mathf.Max(leftGapMeters, rightGapMeters);

        if (!active)
            return 0f;

        float t = Mathf.InverseLerp(activationThreshold, Mathf.Max(activationThreshold + 0.001f, gapMaxMeters), gap);
        return Mathf.Clamp01(t) * maxGapAmplitude;
    }

    private void SetGapState(bool left, float gap, bool active, float aboveSince)
    {
        if (left)
        {
            leftGapMeters = gap;
            leftGapHapticActive = active;
            leftGapAboveThresholdSince = aboveSince;
        }
        else
        {
            rightGapMeters = gap;
            rightGapHapticActive = active;
            rightGapAboveThresholdSince = aboveSince;
        }
    }

    private void UpdatePinchPulseDetection()
    {
        if (!enablePinchContactPulse || robotSubscriber == null || !robotSubscriber.HasLatestGripperJointState)
            return;

        float now = Time.unscaledTime;
        float gripperValue = robotSubscriber.LatestGripperJointMeters;
        if (lastGripperSampleTime < 0f)
        {
            lastGripperSampleTime = now;
            lastGripperValue = gripperValue;
            return;
        }

        float dt = Mathf.Max(0.001f, now - lastGripperSampleTime);
        float speed = Mathf.Abs(gripperValue - lastGripperValue) / dt;
        bool closing = handPoseSender != null && handPoseSender.IsGripperClosing;
        bool opening = handPoseSender != null && handPoseSender.IsGripperOpening;

        if (opening || gripperValue >= gripperOpenRearmMeters)
        {
            pinchPulseArmed = true;
            closingStallTime = 0f;
        }

        if (closing && pinchPulseArmed)
        {
            bool stoppedBeforeClosed = speed <= gripperStallVelocityMetersPerSec && gripperValue >= gripperContactMinGapMeters;
            bool objectNear = !requireObjectNearForPinchPulse || IsAnySyncedObjectNearGrip();
            if (stoppedBeforeClosed && objectNear)
            {
                closingStallTime += dt;
                if (closingStallTime >= gripperStallConfirmSec)
                {
                    TriggerPinchPulse();
                    pinchPulseArmed = false;
                    closingStallTime = 0f;
                }
            }
            else
            {
                closingStallTime = 0f;
            }
        }
        else if (!closing)
        {
            closingStallTime = 0f;
        }

        lastGripperValue = gripperValue;
        lastGripperSampleTime = now;
    }

    public void TriggerPinchPulse()
    {
        pulseStage = 1;
        pulseStageEndTime = Time.unscaledTime + Mathf.Max(0.01f, pinchPulseOnSec);
        Debug.Log("[QuestHapticFeedbackController] Pinch/contact haptic pulse triggered.");
    }

    private float UpdatePulseState()
    {
        if (pulseStage == 0)
            return 0f;

        float now = Time.unscaledTime;
        if (now < pulseStageEndTime)
            return pulseStage == 1 || pulseStage == 3 ? pinchPulseAmplitude : 0f;

        if (pulseStage == 1)
        {
            pulseStage = 2;
            pulseStageEndTime = now + Mathf.Max(0.01f, pinchPulseGapSec);
            return 0f;
        }

        if (pulseStage == 2)
        {
            pulseStage = 3;
            pulseStageEndTime = now + Mathf.Max(0.01f, pinchPulseOnSec);
            return pinchPulseAmplitude;
        }

        pulseStage = 0;
        return 0f;
    }

    private bool IsAnySyncedObjectNearGrip()
    {
        Transform left = robotSubscriber != null ? robotSubscriber.LeftFingerTransform : null;
        Transform right = robotSubscriber != null ? robotSubscriber.RightFingerTransform : null;
        if (left == null || right == null)
            return false;

        Vector3 center = 0.5f * (left.position + right.position);
        float radiusSqr = objectNearRadiusMeters * objectNearRadiusMeters;
        foreach (Transform target in syncedObjectTransforms)
        {
            if (target != null && (target.position - center).sqrMagnitude <= radiusSqr)
                return true;
        }
        return false;
    }

    private void RefreshSyncedObjectList()
    {
        nextObjectRefreshTime = Time.unscaledTime + 1.0f;
        syncedObjectTransforms.Clear();
        if (syncedObjectNames == null)
            return;

        foreach (string objectName in syncedObjectNames)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                continue;
            GameObject go = GameObject.Find(objectName);
            if (go != null)
                syncedObjectTransforms.Add(go.transform);
        }
    }

    private void SendHaptics(float frequency, float amplitude)
    {
        amplitude = Mathf.Clamp01(amplitude);
        if (Mathf.Abs(amplitude - lastSentAmplitude) < 0.01f)
            return;

        OVRInput.Controller target = vibrateBothControllersForGap && pulseStage == 0
            ? OVRInput.Controller.Touch
            : hapticController;
        OVRInput.SetControllerVibration(Mathf.Clamp01(frequency), amplitude, target);
        lastSentAmplitude = amplitude;
    }

    private void SendControllerHaptics(
        OVRInput.Controller controller,
        float frequency,
        float amplitude,
        ref float lastSent
    )
    {
        amplitude = Mathf.Clamp01(amplitude);
        if (Mathf.Abs(amplitude - lastSent) < 0.01f)
            return;

        OVRInput.SetControllerVibration(Mathf.Clamp01(frequency), amplitude, controller);
        lastSent = amplitude;
    }

    private void StopHaptics()
    {
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.Touch);
        lastSentAmplitude = 0f;
        lastSentLeftAmplitude = 0f;
        lastSentRightAmplitude = 0f;
    }

    private static Vector3 ToVector3(PointMsg p)
    {
        return new Vector3((float)p.x, (float)p.y, (float)p.z);
    }

    private static T FindAny<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
        return Object.FindObjectOfType<T>(true);
#endif
    }

    private void OnDisable()
    {
        StopHaptics();
    }

    private void OnDestroy()
    {
        StopHaptics();
    }
}
