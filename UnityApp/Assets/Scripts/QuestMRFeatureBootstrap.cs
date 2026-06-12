using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class QuestMRFeatureBootstrap : MonoBehaviour
{
    public bool enablePassthroughFeature = true;
    public bool enableRobotViewFeature = false;
    public bool enableHapticsFeature = true;
    public bool enableRuntimeDebugPanel = false;
    public bool enableCentralControlPanel = true;
    public bool enableWorkspaceDragFeature = true;
    public bool enableHandPoseSenderFallback = true;
    public bool enableControllerRayVisuals = true;
    public bool enableFloatingSceneCamera = true;
    public bool enableEvaluationTracePublisher = true;
    public bool enableRecordingPerformanceTraceLogger = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateBootstrapAfterSceneLoad()
    {
        if (FindAny<QuestMRFeatureBootstrap>() != null)
            return;

        GameObject root = GameObject.Find("QuestMRFeatures");
        if (root == null)
            root = new GameObject("QuestMRFeatures");
        root.AddComponent<QuestMRFeatureBootstrap>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        EnsureRequestedFeatures();
    }

    private void EnsureRequestedFeatures()
    {
        if (enableHandPoseSenderFallback)
            EnsureHandPoseSender();
        if (enablePassthroughFeature)
            EnsureComponent<QuestPassthroughController>();
        if (enableRobotViewFeature)
            EnsureComponent<RobotViewpointController>();
        if (enableHapticsFeature)
            ConfigureHaptics(EnsureComponent<QuestHapticFeedbackController>());
        else
            DisableExistingHaptics();
        if (enableWorkspaceDragFeature)
            EnsureComponent<WorkspaceDragController>();
        if (enableControllerRayVisuals)
            EnsureComponent<ControllerRayVisual>();
        if (enableFloatingSceneCamera)
            EnsureComponent<FloatingSceneCameraController>();
        if (enableEvaluationTracePublisher)
            ConfigureEvaluationTracePublisher(EnsureComponent<MREvaluationTracePublisher>());
        if (enableRecordingPerformanceTraceLogger)
            ConfigureRecordingPerformanceTraceLogger(EnsureComponent<RecordingPerformanceTraceLogger>());
        if (enableRuntimeDebugPanel)
            EnsureComponent<TeleopRuntimeDebugPanel>();
        if (enableCentralControlPanel)
            EnsureComponent<MRCentralControlPanel>();
        DisableLegacyPanels();
    }

    private static void ConfigureEvaluationTracePublisher(MREvaluationTracePublisher publisher)
    {
        if (publisher == null)
            return;

        publisher.enabled = true;
        publisher.publishVisualTrace = true;
        publisher.publishRateHz = 60f;
        publisher.workspaceRootName = "GazeboWorkspace";
        publisher.frameId = "world";
    }

    private static void ConfigureRecordingPerformanceTraceLogger(RecordingPerformanceTraceLogger logger)
    {
        if (logger == null)
            return;

        logger.enabled = true;
        logger.enableLogging = true;
        logger.writeLocalCsv = true;
        logger.publishRosTrace = true;
        logger.sampleRateHz = 10f;
        logger.postRecordingLogSeconds = 15f;
        logger.outputFolderName = "RecordingPerformanceLogs";
        logger.recordingStateTopic = "/unity_eval/recording_state";
        logger.fpsSampleTopic = "/unity_eval/fps_sample";
        logger.publishIdleHeartbeat = true;
        logger.idleHeartbeatHz = 1f;
        logger.autoDiscoverRecorders = true;
    }

    private void DisableExistingHaptics()
    {
        QuestHapticFeedbackController existing = FindAny<QuestHapticFeedbackController>();
        if (existing != null)
            existing.enabled = false;
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.Touch);
    }

    private static void ConfigureHaptics(QuestHapticFeedbackController haptics)
    {
        if (haptics == null)
            return;

        haptics.enabled = true;
        haptics.enableRosContactHaptics = true;
        haptics.enableEeGapHaptics = false;
        haptics.requireRosContactForGapHaptics = true;
        haptics.enablePinchContactPulse = false;
        haptics.defaultOutputGain = 0.35f;
        haptics.outputGain = 0.35f;
        haptics.gapActivationThresholdMeters = 0.08f;
        haptics.gapReleaseThresholdMeters = 0.045f;
        haptics.gapActivationHoldSec = 0.35f;
        haptics.leftContactAmplitudeTopic = "/left_arm/haptics/contact_amplitude";
        haptics.rightContactAmplitudeTopic = "/right_arm/haptics/contact_amplitude";
        haptics.leftTargetEePoseTopic = "/left_arm/teleop/target_ee_pose";
        haptics.leftActualEePoseTopic = "/left_arm/teleop/actual_ee_pose";
        haptics.rightTargetEePoseTopic = "/right_arm/teleop/target_ee_pose";
        haptics.rightActualEePoseTopic = "/right_arm/teleop/actual_ee_pose";
    }

    private void EnsureHandPoseSender()
    {
        HandPoseSender existing = FindAny<HandPoseSender>();
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            existing.enabled = true;
            existing.targetIP = "127.0.0.1";
            existing.targetPort = 5026;
            existing.sendRelativeToHeadset = false;
            existing.sendRelativeToControlFrame = false;
            existing.controlFrameName = "GazeboWorkspace";
            existing.includeWorkspacePoseInControls = true;
            existing.mappingMode = "unity_world_delta";
            existing.preferControllers = true;
            existing.attachmentToolTransformName = "tool0";
            existing.attachmentToolFallbackTransformName = "robotiq_hande_end";
            return;
        }

        GameObject senderRoot = GameObject.Find("NetworkSender");
        if (senderRoot == null)
            senderRoot = new GameObject("NetworkSender");

        HandPoseSender sender = senderRoot.AddComponent<HandPoseSender>();
        sender.targetIP = "127.0.0.1";
        sender.targetPort = 5026;
        sender.sendRelativeToHeadset = false;
        sender.sendRelativeToControlFrame = false;
        sender.controlFrameName = "GazeboWorkspace";
        sender.includeWorkspacePoseInControls = true;
        sender.mappingMode = "unity_world_delta";
        sender.preferControllers = true;
        sender.attachmentToolTransformName = "tool0";
        sender.attachmentToolFallbackTransformName = "robotiq_hande_end";
        Debug.Log("[QuestMRFeatureBootstrap] Created fallback HandPoseSender on NetworkSender.");
    }

    private void DisableLegacyPanels()
    {
        if (!enableCentralControlPanel)
            return;

        TeleopRuntimeDebugPanel debugPanel = FindAny<TeleopRuntimeDebugPanel>();
        if (debugPanel != null)
        {
            debugPanel.createDebugPanel = false;
            debugPanel.enabled = false;
        }

        RobotViewpointController robotView = FindAny<RobotViewpointController>();
        if (robotView != null)
        {
            robotView.createRobotViewScreen = false;
            if (!enableRobotViewFeature)
                robotView.enabled = false;
        }

        foreach (TeleopInstructionBoard board in FindAll<TeleopInstructionBoard>())
        {
            if (board != null)
            {
                board.createInstructionBoard = false;
                board.enabled = false;
            }
        }

        foreach (GripperCameraRecorder recorder in FindAll<GripperCameraRecorder>())
        {
            if (recorder != null)
                recorder.createFloatingPanel = false;
        }
    }

    private T EnsureComponent<T>() where T : Component
    {
        T existing = FindAny<T>();
        if (existing != null)
            return existing;

        return gameObject.AddComponent<T>();
    }

    private static T FindAny<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
        return Object.FindObjectOfType<T>(true);
#endif
    }

    private static T[] FindAll<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<T>(true);
#endif
    }
}
