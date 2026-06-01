using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class HandPoseSender : MonoBehaviour
{
    [Header("Network Settings")]
    [Tooltip("The host IP to stream hand data to over TCP. For wired Quest USB mode with adb reverse, use 127.0.0.1.")]
    public string targetIP = "127.0.0.1";
    public int targetPort = 5026;

    [Header("TCP Settings")]
    [Tooltip("Reconnect interval (seconds) when disconnected.")]
    public float reconnectIntervalSec = 1.0f;
    [Tooltip("TCP connect timeout in milliseconds.")]
    public int connectTimeoutMs = 300;

    [Header("Hand References")]
    public Transform leftHandTransform;
    public Transform rightHandTransform;
    public OVRHand leftOVRHand;
    public OVRHand rightOVRHand;

    [Header("Controller References")]
    public bool preferControllers = true;
    public Transform leftControllerTransform;
    public Transform rightControllerTransform;

    private TcpClient tcpClient;
    private NetworkStream tcpStream;
    private float nextResolveTime;
    private float nextReconnectTime;
    private bool rightTriggerWasHeld;
    private bool modeSwitchWasHeld;
    private int gripperToggleCommand;
    private int leftGripperToggleCommand;
    private int rightGripperToggleCommand;
    private bool gamepadModeActive;
    private bool leftAttachmentModeActive;
    private bool rightAttachmentModeActive;
    private bool leftTriggerWasHeld;
    private bool leftAttachmentToggleWasHeld;
    private bool rightAttachmentToggleWasHeld;
    private float modeSwitchPulseUntilTime;
    private float resetPulseUntilTime;
    private float resetRobotPulseUntilTime;
    private float resetScenePulseUntilTime;
    private ControlsData latestControls;
    private bool latestRightInputTracked;
    private int sentPacketCount;
    private float lastStatusLogTime;
    public bool preferOVRHands = true;

    public ControlsData LatestControls => latestControls;
    public bool IsTeleopHeld => latestControls != null && latestControls.teleop_held;
    public bool IsLeftTeleopHeld => latestControls != null && latestControls.left_teleop_enable;
    public bool IsRightTeleopHeld => latestControls != null && latestControls.right_teleop_enable;
    public bool IsLeftAttachmentModeActive => latestControls != null && latestControls.left_attachment_mode;
    public bool IsRightAttachmentModeActive => latestControls != null && latestControls.right_attachment_mode;
    public bool IsGripperClosing => latestControls != null && latestControls.close_held;
    public bool IsGripperOpening => latestControls != null && latestControls.open_held;
    public bool IsTcpConnected => tcpClient != null && tcpClient.Connected && tcpStream != null;
    public bool IsRightInputTracked => latestRightInputTracked;
    public bool IsGamepadModeActive => gamepadModeActive;
    public string ControlModeLabel => gamepadModeActive ? "gamepad" : "hand_pose";
    public string LastStatus { get; private set; } = "not started";

    [System.Serializable]
    public class HandData
    {
        public bool isTracked;
        public Vector3 pos;
        public Quaternion rot;
    }

    [System.Serializable]
    public class Packet
    {
        public float timestamp;
        public HandData left_hand;
        public HandData right_hand;
        public ControlsData controls;
    }

    [System.Serializable]
    public class ControlsData
    {
        public bool rotate_held;
        public bool close_held;
        public bool open_held;
        public bool reset_held;
        public bool recenter_held;
        public bool mode_switch_held;
        public bool teleop_held;
        public bool rotate_enable;
        public bool close_enable;
        public bool open_enable;
        public bool reset_enable;
        public bool reset_robot_enable;
        public bool reset_scene_enable;
        public bool recenter_enable;
        public bool left_recenter_held;
        public bool left_recenter_enable;
        public bool mode_switch_enable;
        public bool teleop_enable;
        public bool gamepad_mode;
        public bool left_rotate_enable;
        public bool right_rotate_enable;
        public bool left_teleop_enable;
        public bool right_teleop_enable;
        public bool left_close_enable;
        public bool left_open_enable;
        public bool right_close_enable;
        public bool right_open_enable;
        public bool left_attachment_mode;
        public bool right_attachment_mode;
        public bool left_attachment_toggle_enable;
        public bool right_attachment_toggle_enable;
        public bool attachment_adjustment_mode;
        public Vector3 left_attachment_position_offset;
        public Quaternion left_attachment_rotation_offset;
        public Vector3 right_attachment_position_offset;
        public Quaternion right_attachment_rotation_offset;
        public float grip_value;
        public float trigger_value;
        public float left_grip_value;
        public float left_trigger_value;
        public float left_thumbstick_x;
        public float left_thumbstick_y;
        public float right_thumbstick_x;
        public float right_thumbstick_y;
        public bool workspace_pose_valid;
        public Vector3 workspace_pos;
        public Quaternion workspace_rot;
        public string mapping_mode;
        public string source;
        public string control_mode;
        public Quaternion right_controller_rot;
    }

    [Header("Controller Input")]
    [Range(0.05f, 0.95f)]
    public float analogPressThreshold = 0.55f;
    [Tooltip("When enabled, right trigger toggles the gripper only while right grip teleop is held. When teleop is released, other scripts may use the trigger for UI/workspace dragging.")]
    public bool gripperTriggerRequiresTeleopHeld = true;
    [Tooltip("Seconds between runtime status logs. Set to 0 to disable periodic logs.")]
    public float statusLogIntervalSec = 2.0f;
    [Tooltip("How long to keep the Y-mode-switch pulse true so the ROS receiver publish loop cannot miss it.")]
    public float modeSwitchPulseSec = 0.18f;
    [Tooltip("How long to keep panel-requested reset true so the ROS receiver publish loop cannot miss it.")]
    public float resetPulseSec = 0.25f;

    [Header("Attachment Calibration")]
    [Tooltip("Legacy panel flag. Hold left X / right A during attachment mode to freeze the arm and capture the hand-to-tool offset on release.")]
    public bool attachmentAdjustmentModeActive = false;
    [Tooltip("Position offset in Unity workspace axes, applied to the left arm in attachment mode.")]
    public Vector3 leftAttachmentPositionOffset = Vector3.zero;
    [Tooltip("Position offset in Unity workspace axes, applied to the right arm in attachment mode.")]
    public Vector3 rightAttachmentPositionOffset = Vector3.zero;
    [Tooltip("Euler display/edit value for the left attachment rotation offset.")]
    public Vector3 leftAttachmentRotationOffsetEuler = Vector3.zero;
    [Tooltip("Euler display/edit value for the right attachment rotation offset.")]
    public Vector3 rightAttachmentRotationOffsetEuler = Vector3.zero;
    public bool persistAttachmentOffsets = true;
    [Tooltip("Scene root for resolving the left attachment tool transform.")]
    public string leftRobotRootName = "left_ur5e";
    [Tooltip("Scene root for resolving the right attachment tool transform.")]
    public string rightRobotRootName = "right_ur5e";
    [Tooltip("Child transform name used as the attachment/tool pose reference. This should match the backend ee_frame tool0, not the visible gripper tip.")]
    public string attachmentToolTransformName = "tool0";
    [Tooltip("Fallback child transform if the tool0 reference is missing.")]
    public string attachmentToolFallbackTransformName = "robotiq_hande_end";
    [Tooltip("Optional explicit left tool transform. Leave empty to auto-resolve by name.")]
    public Transform leftAttachmentToolTransform;
    [Tooltip("Optional explicit right tool transform. Leave empty to auto-resolve by name.")]
    public Transform rightAttachmentToolTransform;
    [Tooltip("Must match the backend attachment_tool_rotation_offset_xyzw default. Used only so hold-to-calibrate records an offset relative to the backend default.")]
    public Vector4 backendAttachmentToolRotationOffsetXyzw = new Vector4(0.5f, 0.5f, 0.5f, 0.5f);
    [Tooltip("Keep the selected arm paused briefly after saving an attachment offset so ROS receives the new offset before motion resumes.")]
    public float attachmentCalibrationSettleSec = 0.12f;

    private const string AttachmentPrefsPrefix = "HandPoseSender.AttachmentOffset.";
    private Quaternion leftAttachmentRotationOffset = Quaternion.identity;
    private Quaternion rightAttachmentRotationOffset = Quaternion.identity;
    private bool leftAttachmentAdjustWasHeld;
    private bool rightAttachmentAdjustWasHeld;
    private bool leftAttachmentCalibrationActive;
    private bool rightAttachmentCalibrationActive;
    private float leftAttachmentCalibrationHoldUntilTime;
    private float rightAttachmentCalibrationHoldUntilTime;

    [Header("Pose Frame")]
    [Tooltip("If enabled, sent hand/controller pose is expressed in headset local frame.")]
    public bool sendRelativeToHeadset = false;
    [Tooltip("Optional explicit headset transform. If empty, script auto-resolves CenterEyeAnchor/Main Camera.")]
    public Transform headsetTransform;
    [Tooltip("If enabled, sent hand/controller pose is expressed in this frame's local coordinates. Use GazeboWorkspace for movable MR workspaces.")]
    public bool sendRelativeToControlFrame = false;
    [Tooltip("Optional explicit control frame. In GazeboReplica_MR this should be GazeboWorkspace.")]
    public Transform controlFrameTransform;
    public string controlFrameName = "GazeboWorkspace";
    [Tooltip("When enabled, include the movable workspace pose in the TCP packet. Keep hand/controller poses in Unity world space so ROS can map them against the current workspace transform.")]
    public bool includeWorkspacePoseInControls = false;
    [Tooltip("Mapping label sent to ROS. unity_world_delta means controller deltas are measured in headset/world space and converted into the workspace/simulation frame on the ROS side.")]
    public string mappingMode = "world_delta";

    void Start()
    {
        LoadAttachmentOffsets();
        ResolveHandTransforms(forceLog: true);
        ResolveControlFrame(forceLog: true);
        ConnectTcp(forceLog: true);
    }

    void Update()
    {
        if ((leftHandTransform == null || rightHandTransform == null) && Time.time >= nextResolveTime)
        {
            ResolveHandTransforms(forceLog: true);
            nextResolveTime = Time.time + 1.0f;
        }

        if (sendRelativeToControlFrame && controlFrameTransform == null && Time.time >= nextResolveTime)
        {
            ResolveControlFrame(forceLog: true);
            nextResolveTime = Time.time + 1.0f;
        }

        if (!EnsureConnected())
        {
            UpdateLastStatus(false, false);
            return;
        }

        Packet packet = new Packet();
        packet.timestamp = Time.time;

        packet.left_hand = GetLeftInputData();
        packet.right_hand = GetRightInputData();
        latestRightInputTracked = packet.right_hand != null && packet.right_hand.isTracked;
        packet.controls = GetControlsData(packet.left_hand, packet.right_hand);
        latestControls = packet.controls;

        string json = JsonUtility.ToJson(packet);
        SendJsonPacket(json);
        UpdateLastStatus(true, latestRightInputTracked);
    }

    bool EnsureConnected()
    {
        if (tcpClient != null && tcpClient.Connected && tcpStream != null)
        {
            return true;
        }

        if (Time.time < nextReconnectTime)
        {
            return false;
        }

        ConnectTcp(forceLog: false);
        return tcpClient != null && tcpClient.Connected && tcpStream != null;
    }

    void ConnectTcp(bool forceLog)
    {
        CloseTcpClient();

        if (targetIP == "255.255.255.255")
        {
            Debug.LogError("[HandPoseSender] TCP does not support broadcast targetIP=255.255.255.255. Use 127.0.0.1 for wired USB mode or a host LAN IP for wireless mode.");
            nextReconnectTime = Time.time + Mathf.Max(0.2f, reconnectIntervalSec);
            return;
        }

        try
        {
            TcpClient client = new TcpClient();
            client.NoDelay = true;

            IAsyncResult asyncResult = client.BeginConnect(targetIP, targetPort, null, null);
            bool connected = asyncResult.AsyncWaitHandle.WaitOne(Mathf.Max(50, connectTimeoutMs));
            if (!connected)
            {
                client.Close();
                throw new TimeoutException($"TCP connect timeout to {targetIP}:{targetPort}");
            }
            client.EndConnect(asyncResult);

            tcpClient = client;
            tcpStream = client.GetStream();

            if (forceLog)
            {
                Debug.Log($"[HandPoseSender] TCP connected to {targetIP}:{targetPort}");
            }
        }
        catch (Exception e)
        {
            if (forceLog)
            {
                Debug.LogWarning($"[HandPoseSender] TCP connect failed: {e.Message}");
            }
            CloseTcpClient();
            nextReconnectTime = Time.time + Mathf.Max(0.2f, reconnectIntervalSec);
        }
    }

    void SendJsonPacket(string json)
    {
        if (tcpStream == null)
        {
            return;
        }

        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json + "\n");
            tcpStream.Write(bytes, 0, bytes.Length);
            sentPacketCount++;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[HandPoseSender] TCP send failed: {e.Message}");
            CloseTcpClient();
            nextReconnectTime = Time.time + Mathf.Max(0.2f, reconnectIntervalSec);
        }
    }

    void CloseTcpClient()
    {
        if (tcpStream != null)
        {
            try
            {
                tcpStream.Close();
            }
            catch (Exception)
            {
            }
            tcpStream = null;
        }

        if (tcpClient != null)
        {
            try
            {
                tcpClient.Close();
            }
            catch (Exception)
            {
            }
            tcpClient = null;
        }
    }

    ControlsData GetControlsData(HandData leftHand, HandData rightHand)
    {
        ControlsData controls = new ControlsData();

        bool leftRotateHeld = IsLeftXHeld();
        bool rightRotateHeld = OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.RTouch) ||
                               OVRInput.Get(OVRInput.RawButton.A, OVRInput.Controller.RTouch) ||
                               OVRInput.Get(OVRInput.RawButton.A);
        bool rightAttachmentToggleHeld = OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.RTouch) ||
                                         OVRInput.Get(OVRInput.RawButton.B, OVRInput.Controller.RTouch) ||
                                         OVRInput.Get(OVRInput.RawButton.B);

        controls.rotate_held = rightRotateHeld;
        controls.reset_held = false;
        controls.recenter_held =
            OVRInput.Get(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch) ||
            OVRInput.Get(OVRInput.Button.SecondaryThumbstick, OVRInput.Controller.Touch) ||
            OVRInput.Get(OVRInput.RawButton.RThumbstick);
        controls.mode_switch_held = IsLeftYHeld();
        bool leftAttachmentToggleDown = controls.mode_switch_held && !leftAttachmentToggleWasHeld;
        if (leftAttachmentToggleDown)
            leftAttachmentModeActive = !leftAttachmentModeActive;
        leftAttachmentToggleWasHeld = controls.mode_switch_held;

        bool rightAttachmentToggleDown = rightAttachmentToggleHeld && !rightAttachmentToggleWasHeld;
        if (rightAttachmentToggleDown)
            rightAttachmentModeActive = !rightAttachmentModeActive;
        rightAttachmentToggleWasHeld = rightAttachmentToggleHeld;

        bool modeSwitchDown = controls.mode_switch_held && !modeSwitchWasHeld;
        if (modeSwitchDown && !leftAttachmentToggleDown)
            ToggleControlMode();
        modeSwitchWasHeld = controls.mode_switch_held;

        // Read both direct controller axes and combined Touch fallbacks. Some Quest/OpenXR
        // profiles expose right-hand controls as Secondary* on the combined Touch mapping.
        float rightGripValue = Mathf.Max(
            OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch),
            OVRInput.Get(OVRInput.Axis1D.SecondaryHandTrigger, OVRInput.Controller.Touch));
        float rightTriggerValue = Mathf.Max(
            OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch),
            OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger, OVRInput.Controller.Touch));
        float leftGripValue = Mathf.Max(
            OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch),
            OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.Touch));
        float leftTriggerValue = Mathf.Max(
            OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch),
            OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.Touch));
        Vector2 leftStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
        Vector2 rightStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);

        // Fallback for profiles that expose both controllers through the combined Touch mapping.
        if (leftStick.sqrMagnitude < 0.0001f)
        {
            leftStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.Touch);
        }
        if (rightStick.sqrMagnitude < 0.0001f)
        {
            rightStick = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick, OVRInput.Controller.Touch);
        }
        bool rightTriggerHeld = rightTriggerValue >= analogPressThreshold;
        bool teleopHeld = rightGripValue >= analogPressThreshold;
        bool leftTriggerHeld = leftTriggerValue >= analogPressThreshold;
        bool leftTeleopHeld = leftGripValue >= analogPressThreshold;
        bool leftAttachmentCalibrating = UpdateAttachmentOffsetCalibration(true, leftRotateHeld, leftHand);
        bool rightAttachmentCalibrating = UpdateAttachmentOffsetCalibration(false, rightRotateHeld, rightHand);
        bool leftAttachmentPaused = leftAttachmentCalibrating || Time.time <= leftAttachmentCalibrationHoldUntilTime;
        bool rightAttachmentPaused = rightAttachmentCalibrating || Time.time <= rightAttachmentCalibrationHoldUntilTime;
        if (leftTriggerHeld && !leftTriggerWasHeld && (!gripperTriggerRequiresTeleopHeld || leftTeleopHeld))
            leftGripperToggleCommand = leftGripperToggleCommand <= 0 ? 1 : -1;
        if (rightTriggerHeld && !rightTriggerWasHeld && (!gripperTriggerRequiresTeleopHeld || teleopHeld))
            rightGripperToggleCommand = rightGripperToggleCommand <= 0 ? 1 : -1;
        gripperToggleCommand = rightGripperToggleCommand;
        leftTriggerWasHeld = leftTriggerHeld;
        rightTriggerWasHeld = rightTriggerHeld;

        controls.grip_value = rightGripValue;
        controls.trigger_value = rightTriggerValue;
        controls.left_grip_value = leftGripValue;
        controls.left_trigger_value = leftTriggerValue;
        controls.close_held = gripperToggleCommand > 0;
        controls.open_held = gripperToggleCommand < 0;
        controls.teleop_held = teleopHeld;
        controls.rotate_enable = rightAttachmentPaused ? false : controls.rotate_held;
        controls.close_enable = controls.close_held;
        controls.open_enable = controls.open_held;
        bool resetRobotEnable = resetRobotPulseUntilTime > 0f && Time.time <= resetRobotPulseUntilTime;
        bool resetSceneEnable = resetScenePulseUntilTime > 0f && Time.time <= resetScenePulseUntilTime;
        controls.reset_robot_enable = resetRobotEnable;
        controls.reset_scene_enable = resetSceneEnable;
        controls.reset_enable = controls.reset_held || (resetPulseUntilTime > 0f && Time.time <= resetPulseUntilTime) || (resetRobotEnable && resetSceneEnable);
        controls.recenter_enable = controls.recenter_held || rightAttachmentPaused;
        controls.left_recenter_held = leftAttachmentPaused;
        controls.left_recenter_enable = leftAttachmentPaused;
        controls.mode_switch_enable = Time.time <= modeSwitchPulseUntilTime;
        controls.teleop_enable = controls.teleop_held;
        controls.gamepad_mode = gamepadModeActive;
        controls.left_rotate_enable = leftAttachmentPaused ? false : leftRotateHeld;
        controls.right_rotate_enable = rightAttachmentPaused ? false : rightRotateHeld;
        controls.left_teleop_enable = leftTeleopHeld;
        controls.right_teleop_enable = teleopHeld;
        controls.left_close_enable = leftGripperToggleCommand > 0;
        controls.left_open_enable = leftGripperToggleCommand < 0;
        controls.right_close_enable = rightGripperToggleCommand > 0;
        controls.right_open_enable = rightGripperToggleCommand < 0;
        controls.left_attachment_mode = leftAttachmentModeActive;
        controls.right_attachment_mode = rightAttachmentModeActive;
        controls.left_attachment_toggle_enable = leftAttachmentToggleDown;
        controls.right_attachment_toggle_enable = rightAttachmentToggleDown;
        controls.attachment_adjustment_mode = attachmentAdjustmentModeActive || leftAttachmentPaused || rightAttachmentPaused;
        controls.left_attachment_position_offset = leftAttachmentPositionOffset;
        controls.left_attachment_rotation_offset = leftAttachmentRotationOffset;
        controls.right_attachment_position_offset = rightAttachmentPositionOffset;
        controls.right_attachment_rotation_offset = rightAttachmentRotationOffset;
        controls.left_thumbstick_x = leftStick.x;
        controls.left_thumbstick_y = leftStick.y;
        controls.right_thumbstick_x = rightStick.x;
        controls.right_thumbstick_y = rightStick.y;
        controls.source = "quest_dual_controller";
        controls.control_mode = ControlModeLabel;
        controls.mapping_mode = mappingMode;

        if (includeWorkspacePoseInControls)
        {
            if (controlFrameTransform == null)
                ResolveControlFrame(forceLog: false);

            if (controlFrameTransform != null)
            {
                controls.workspace_pose_valid = true;
                controls.workspace_pos = controlFrameTransform.position;
                controls.workspace_rot = controlFrameTransform.rotation;
            }
            else
            {
                controls.workspace_pose_valid = false;
                controls.workspace_pos = Vector3.zero;
                controls.workspace_rot = Quaternion.identity;
            }
        }
        else
        {
            controls.workspace_pose_valid = false;
            controls.workspace_pos = Vector3.zero;
            controls.workspace_rot = Quaternion.identity;
        }

        if (rightHand != null && rightHand.isTracked)
        {
            controls.right_controller_rot = rightHand.rot;
        }
        else
        {
            controls.right_controller_rot = Quaternion.identity;
        }

        return controls;
    }

    private bool UpdateAttachmentOffsetCalibration(bool leftArm, bool rotateHeld, HandData hand)
    {
        bool armAttachmentActive = leftArm ? leftAttachmentModeActive : rightAttachmentModeActive;
        bool calibrating = armAttachmentActive && rotateHeld && hand != null && hand.isTracked;

        if (leftArm)
        {
            UpdateAttachmentOffsetCalibrationForArm(
                true,
                calibrating,
                hand,
                ref leftAttachmentAdjustWasHeld,
                ref leftAttachmentCalibrationActive);
        }
        else
        {
            UpdateAttachmentOffsetCalibrationForArm(
                false,
                calibrating,
                hand,
                ref rightAttachmentAdjustWasHeld,
                ref rightAttachmentCalibrationActive);
        }

        return calibrating;
    }

    private void UpdateAttachmentOffsetCalibrationForArm(
        bool leftArm,
        bool calibrating,
        HandData hand,
        ref bool wasHeld,
        ref bool activeFlag)
    {
        activeFlag = calibrating;
        if (calibrating)
        {
            wasHeld = true;
            return;
        }

        if (!wasHeld)
            return;

        wasHeld = false;
        activeFlag = false;
        if (TryCaptureAttachmentOffsetFromCurrentPose(leftArm, hand))
        {
            if (leftArm)
                leftAttachmentCalibrationHoldUntilTime = Time.time + Mathf.Max(0.02f, attachmentCalibrationSettleSec);
            else
                rightAttachmentCalibrationHoldUntilTime = Time.time + Mathf.Max(0.02f, attachmentCalibrationSettleSec);
            SaveAttachmentOffsets();
            Debug.Log($"[HandPoseSender] Captured {(leftArm ? "left" : "right")} attachment offset: {AttachmentOffsetStatus}");
        }
        else
        {
            Debug.LogWarning($"[HandPoseSender] Could not capture {(leftArm ? "left" : "right")} attachment offset. Check controller tracking and {attachmentToolTransformName} in the scene.");
        }
    }

    private bool TryCaptureAttachmentOffsetFromCurrentPose(bool leftArm, HandData hand)
    {
        if (hand == null || !hand.isTracked)
            return false;
        if (!TryGetInputWorldPose(leftArm, out Vector3 handWorldPos, out Quaternion handWorldRot))
            return false;

        Transform tool = ResolveAttachmentToolTransform(leftArm);
        if (tool == null)
            return false;

        Vector3 worldOffset = tool.position - handWorldPos;
        Vector3 workspaceOffset = controlFrameTransform != null
            ? controlFrameTransform.InverseTransformVector(worldOffset)
            : worldOffset;

        Quaternion backendDefault = BackendAttachmentToolRotationOffset();
        Quaternion toolGazeboRot = UnityWorldRotToGazeboWorld(tool.rotation);
        Quaternion desiredInputGazeboRot = NormalizeQuaternion(toolGazeboRot * Quaternion.Inverse(backendDefault));
        Quaternion desiredInputUnityWorldRot = GazeboWorldRotToUnityWorld(desiredInputGazeboRot);
        Quaternion runtimeOffset = NormalizeQuaternion(Quaternion.Inverse(handWorldRot) * desiredInputUnityWorldRot);

        if (leftArm)
        {
            leftAttachmentPositionOffset = workspaceOffset;
            leftAttachmentRotationOffset = runtimeOffset;
        }
        else
        {
            rightAttachmentPositionOffset = workspaceOffset;
            rightAttachmentRotationOffset = runtimeOffset;
        }

        SyncAttachmentEulerFromQuaternions();
        return true;
    }

    private bool TryGetInputWorldPose(bool leftArm, out Vector3 pos, out Quaternion rot)
    {
        Transform controller = leftArm ? leftControllerTransform : rightControllerTransform;
        if (controller != null && controller.gameObject.activeInHierarchy)
        {
            pos = controller.position;
            rot = controller.rotation;
            return true;
        }

        Transform hand = leftArm ? leftHandTransform : rightHandTransform;
        if (hand != null && hand.gameObject.activeInHierarchy)
        {
            pos = hand.position;
            rot = hand.rotation;
            return true;
        }

        pos = Vector3.zero;
        rot = Quaternion.identity;
        return false;
    }

    private Transform ResolveAttachmentToolTransform(bool leftArm)
    {
        Transform cached = leftArm ? leftAttachmentToolTransform : rightAttachmentToolTransform;
        if (cached != null && cached.gameObject.activeInHierarchy)
            return cached;

        string rootName = leftArm ? leftRobotRootName : rightRobotRootName;
        GameObject root = !string.IsNullOrWhiteSpace(rootName) ? GameObject.Find(rootName) : null;
        Transform found = root != null
            ? FindChildRecursive(root.transform, attachmentToolTransformName)
            : null;
        if (found == null && root != null && !string.IsNullOrWhiteSpace(attachmentToolFallbackTransformName))
            found = FindChildRecursive(root.transform, attachmentToolFallbackTransformName);

        if (found == null && !string.IsNullOrWhiteSpace(attachmentToolTransformName))
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || !go.scene.IsValid())
                    continue;
                bool nameMatches = go.name == attachmentToolTransformName ||
                    (!string.IsNullOrWhiteSpace(attachmentToolFallbackTransformName) && go.name == attachmentToolFallbackTransformName);
                if (!nameMatches)
                    continue;
                if (!go.activeInHierarchy)
                    continue;
                found = go.transform;
                break;
            }
        }

        if (leftArm)
            leftAttachmentToolTransform = found;
        else
            rightAttachmentToolTransform = found;
        return found;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;
        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private Quaternion BackendAttachmentToolRotationOffset()
    {
        return NormalizeQuaternion(new Quaternion(
            backendAttachmentToolRotationOffsetXyzw.x,
            backendAttachmentToolRotationOffsetXyzw.y,
            backendAttachmentToolRotationOffsetXyzw.z,
            backendAttachmentToolRotationOffsetXyzw.w));
    }

    private Quaternion UnityWorldRotToGazeboWorld(Quaternion unityWorldRot)
    {
        Quaternion unityWorkspaceRot = controlFrameTransform != null
            ? Quaternion.Inverse(controlFrameTransform.rotation) * unityWorldRot
            : unityWorldRot;
        return UnityWorkspaceRotToGazeboWorld(unityWorkspaceRot);
    }

    private Quaternion GazeboWorldRotToUnityWorld(Quaternion gazeboWorldRot)
    {
        Quaternion unityWorkspaceRot = GazeboWorldRotToUnityWorkspace(gazeboWorldRot);
        return controlFrameTransform != null
            ? NormalizeQuaternion(controlFrameTransform.rotation * unityWorkspaceRot)
            : unityWorkspaceRot;
    }

    private static Quaternion UnityWorkspaceRotToGazeboWorld(Quaternion unityWorkspaceRot)
    {
        Matrix4x4 basis = GazeboToUnityBasis();
        Matrix4x4 unity = Matrix4x4.Rotate(unityWorkspaceRot);
        Matrix4x4 gazebo = basis.transpose * unity * basis;
        return NormalizeQuaternion(gazebo.rotation);
    }

    private static Quaternion GazeboWorldRotToUnityWorkspace(Quaternion gazeboWorldRot)
    {
        Matrix4x4 basis = GazeboToUnityBasis();
        Matrix4x4 gazebo = Matrix4x4.Rotate(gazeboWorldRot);
        Matrix4x4 unity = basis * gazebo * basis.transpose;
        return NormalizeQuaternion(unity.rotation);
    }

    private static Matrix4x4 GazeboToUnityBasis()
    {
        Matrix4x4 basis = Matrix4x4.identity;
        basis.m00 = 0f;  basis.m01 = -1f; basis.m02 = 0f;
        basis.m10 = 0f;  basis.m11 = 0f;  basis.m12 = 1f;
        basis.m20 = 1f;  basis.m21 = 0f;  basis.m22 = 0f;
        return basis;
    }

    public void ToggleAttachmentAdjustmentMode()
    {
        SetAttachmentAdjustmentMode(!attachmentAdjustmentModeActive);
    }

    public void SetAttachmentAdjustmentMode(bool active)
    {
        attachmentAdjustmentModeActive = active;
        if (!active)
        {
            leftAttachmentAdjustWasHeld = false;
            rightAttachmentAdjustWasHeld = false;
            leftAttachmentCalibrationActive = false;
            rightAttachmentCalibrationActive = false;
            leftAttachmentCalibrationHoldUntilTime = 0f;
            rightAttachmentCalibrationHoldUntilTime = 0f;
            SaveAttachmentOffsets();
        }
    }

    public void ResetLeftAttachmentOffset()
    {
        leftAttachmentPositionOffset = Vector3.zero;
        leftAttachmentRotationOffset = Quaternion.identity;
        leftAttachmentRotationOffsetEuler = Vector3.zero;
        leftAttachmentAdjustWasHeld = false;
        leftAttachmentCalibrationActive = false;
        leftAttachmentCalibrationHoldUntilTime = 0f;
        SaveAttachmentOffsets();
    }

    public void ResetRightAttachmentOffset()
    {
        rightAttachmentPositionOffset = Vector3.zero;
        rightAttachmentRotationOffset = Quaternion.identity;
        rightAttachmentRotationOffsetEuler = Vector3.zero;
        rightAttachmentAdjustWasHeld = false;
        rightAttachmentCalibrationActive = false;
        rightAttachmentCalibrationHoldUntilTime = 0f;
        SaveAttachmentOffsets();
    }

    public void ResetAttachmentOffsets()
    {
        ResetLeftAttachmentOffset();
        ResetRightAttachmentOffset();
    }

    public void SetLeftAttachmentOffset(Vector3 positionOffset, Vector3 eulerOffset)
    {
        leftAttachmentPositionOffset = positionOffset;
        leftAttachmentRotationOffsetEuler = eulerOffset;
        leftAttachmentRotationOffset = NormalizeQuaternion(Quaternion.Euler(eulerOffset));
        SaveAttachmentOffsets();
    }

    public void SetRightAttachmentOffset(Vector3 positionOffset, Vector3 eulerOffset)
    {
        rightAttachmentPositionOffset = positionOffset;
        rightAttachmentRotationOffsetEuler = eulerOffset;
        rightAttachmentRotationOffset = NormalizeQuaternion(Quaternion.Euler(eulerOffset));
        SaveAttachmentOffsets();
    }

    public string AttachmentOffsetStatus =>
        $"calib L={(leftAttachmentCalibrationActive ? "held" : "idle")} R={(rightAttachmentCalibrationActive ? "held" : "idle")} " +
        $"L pos={FormatVector(leftAttachmentPositionOffset)} rot={FormatVector(leftAttachmentRotationOffsetEuler)} " +
        $"R pos={FormatVector(rightAttachmentPositionOffset)} rot={FormatVector(rightAttachmentRotationOffsetEuler)}";

    private void LoadAttachmentOffsets()
    {
        if (persistAttachmentOffsets)
        {
            leftAttachmentPositionOffset = LoadVector3("LeftPos", leftAttachmentPositionOffset);
            rightAttachmentPositionOffset = LoadVector3("RightPos", rightAttachmentPositionOffset);
            leftAttachmentRotationOffsetEuler = LoadVector3("LeftRotEuler", leftAttachmentRotationOffsetEuler);
            rightAttachmentRotationOffsetEuler = LoadVector3("RightRotEuler", rightAttachmentRotationOffsetEuler);
        }

        leftAttachmentRotationOffset = NormalizeQuaternion(Quaternion.Euler(leftAttachmentRotationOffsetEuler));
        rightAttachmentRotationOffset = NormalizeQuaternion(Quaternion.Euler(rightAttachmentRotationOffsetEuler));
    }

    private void SaveAttachmentOffsets()
    {
        if (!persistAttachmentOffsets)
            return;

        SaveVector3("LeftPos", leftAttachmentPositionOffset);
        SaveVector3("RightPos", rightAttachmentPositionOffset);
        SaveVector3("LeftRotEuler", leftAttachmentRotationOffsetEuler);
        SaveVector3("RightRotEuler", rightAttachmentRotationOffsetEuler);
        PlayerPrefs.Save();
    }

    private void SyncAttachmentEulerFromQuaternions()
    {
        leftAttachmentRotationOffsetEuler = NormalizeEuler(leftAttachmentRotationOffset.eulerAngles);
        rightAttachmentRotationOffsetEuler = NormalizeEuler(rightAttachmentRotationOffset.eulerAngles);
    }

    private static Vector3 NormalizeEuler(Vector3 euler)
    {
        return new Vector3(NormalizeAngle(euler.x), NormalizeAngle(euler.y), NormalizeAngle(euler.z));
    }

    private static float NormalizeAngle(float degrees)
    {
        degrees = Mathf.Repeat(degrees + 180f, 360f) - 180f;
        return Mathf.Abs(degrees) < 0.0001f ? 0f : degrees;
    }

    private static Quaternion NormalizeQuaternion(Quaternion q)
    {
        float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        if (mag < 0.000001f)
            return Quaternion.identity;
        float inv = 1f / mag;
        return new Quaternion(q.x * inv, q.y * inv, q.z * inv, q.w * inv);
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:F3},{value.y:F3},{value.z:F3})";
    }

    private static Vector3 LoadVector3(string key, Vector3 fallback)
    {
        string prefix = AttachmentPrefsPrefix + key + ".";
        return new Vector3(
            PlayerPrefs.GetFloat(prefix + "x", fallback.x),
            PlayerPrefs.GetFloat(prefix + "y", fallback.y),
            PlayerPrefs.GetFloat(prefix + "z", fallback.z));
    }

    private static void SaveVector3(string key, Vector3 value)
    {
        string prefix = AttachmentPrefsPrefix + key + ".";
        PlayerPrefs.SetFloat(prefix + "x", value.x);
        PlayerPrefs.SetFloat(prefix + "y", value.y);
        PlayerPrefs.SetFloat(prefix + "z", value.z);
    }

    private void UpdateLastStatus(bool connected, bool rightTracked)
    {
        string controlsText = latestControls != null
            ? $"mode={ControlModeLabel}, R teleop={latestControls.right_teleop_enable}, R grip={latestControls.grip_value:F2}, R trigger={latestControls.trigger_value:F2}, R close/open=({latestControls.right_close_enable},{latestControls.right_open_enable}), L teleop={latestControls.left_teleop_enable}, L grip={latestControls.left_grip_value:F2}, L trigger={latestControls.left_trigger_value:F2}, L close/open=({latestControls.left_close_enable},{latestControls.left_open_enable}), yHeld={latestControls.mode_switch_held}, yPulse={latestControls.mode_switch_enable}"
            : "controls=NULL";

        LastStatus = $"tcp={(connected ? "ON" : "OFF")} {targetIP}:{targetPort}, rightTracked={rightTracked}, sent={sentPacketCount}, {controlsText}";

        if (statusLogIntervalSec > 0f && Time.unscaledTime - lastStatusLogTime >= statusLogIntervalSec)
        {
            lastStatusLogTime = Time.unscaledTime;
            Debug.Log($"[HandPoseSender] {LastStatus}");
        }
    }

    public void ToggleControlMode()
    {
        gamepadModeActive = !gamepadModeActive;
        modeSwitchPulseUntilTime = Time.time + Mathf.Max(0.05f, modeSwitchPulseSec);
    }

    public void RequestReset()
    {
        RequestResetAll();
    }

    public void RequestResetObjects()
    {
        resetScenePulseUntilTime = Time.time + Mathf.Max(0.05f, resetPulseSec);
    }

    public void RequestResetRobots()
    {
        resetRobotPulseUntilTime = Time.time + Mathf.Max(0.05f, resetPulseSec);
    }

    public void RequestResetAll()
    {
        resetPulseUntilTime = Time.time + Mathf.Max(0.05f, resetPulseSec);
        resetRobotPulseUntilTime = resetPulseUntilTime;
        resetScenePulseUntilTime = resetPulseUntilTime;
    }

    private bool IsLeftYHeld()
    {
        // Quest Touch Y is exposed differently depending on whether Unity reports
        // a specific left controller, the combined Touch profile, or raw buttons.
        return
            OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.LTouch) ||
            OVRInput.Get(OVRInput.Button.Four, OVRInput.Controller.LTouch) ||
            OVRInput.Get(OVRInput.Button.Four, OVRInput.Controller.Touch) ||
            OVRInput.Get(OVRInput.RawButton.Y, OVRInput.Controller.LTouch) ||
            OVRInput.Get(OVRInput.RawButton.Y, OVRInput.Controller.Touch) ||
            OVRInput.Get(OVRInput.RawButton.Y);
    }

    private bool IsLeftXHeld()
    {
        return
            OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.LTouch) ||
            OVRInput.Get(OVRInput.Button.Three, OVRInput.Controller.LTouch) ||
            OVRInput.Get(OVRInput.Button.Three, OVRInput.Controller.Touch) ||
            OVRInput.Get(OVRInput.RawButton.X, OVRInput.Controller.LTouch) ||
            OVRInput.Get(OVRInput.RawButton.X, OVRInput.Controller.Touch) ||
            OVRInput.Get(OVRInput.RawButton.X);
    }

    void ResolveHandTransforms(bool forceLog = false)
    {
        var leftController = GameObject.Find("OVRCameraRig/TrackingSpace/LeftControllerAnchor") ?? GameObject.Find("LeftControllerAnchor");
        var rightController = GameObject.Find("OVRCameraRig/TrackingSpace/RightControllerAnchor") ?? GameObject.Find("RightControllerAnchor");
        var headset = GameObject.Find("OVRCameraRig/TrackingSpace/CenterEyeAnchor") ?? GameObject.Find("CenterEyeAnchor");

        if (leftController != null) leftControllerTransform = leftController.transform;
        if (rightController != null) rightControllerTransform = rightController.transform;
        if (headset != null) headsetTransform = headset.transform;
        else if (headsetTransform == null && Camera.main != null) headsetTransform = Camera.main.transform;

        // Preferred path: OVRHands rig (best for OpenXR finger articulation)
        var leftOvrHands = GameObject.Find("OVRHands/LeftHand");
        var rightOvrHands = GameObject.Find("OVRHands/RightHand");

        if (preferOVRHands && leftOvrHands != null)
        {
            leftHandTransform = leftOvrHands.transform;
        }
        else if (leftHandTransform == null)
        {
            if (leftOvrHands != null) leftHandTransform = leftOvrHands.transform;
        }

        if (preferOVRHands && rightOvrHands != null)
        {
            rightHandTransform = rightOvrHands.transform;
        }
        else if (rightHandTransform == null)
        {
            if (rightOvrHands != null) rightHandTransform = rightOvrHands.transform;
        }

        // Fallback path: OVRCameraRig anchor-based hands
        if (leftHandTransform == null)
        {
            var left = GameObject.Find("OVRCameraRig/TrackingSpace/LeftHandAnchor/LeftHand") ?? GameObject.Find("LeftHand");
            if (left != null) leftHandTransform = left.transform;
        }

        if (rightHandTransform == null)
        {
            var right = GameObject.Find("OVRCameraRig/TrackingSpace/RightHandAnchor/RightHand") ?? GameObject.Find("RightHand");
            if (right != null) rightHandTransform = right.transform;
        }

        if (forceLog)
        {
            var leftPath = leftHandTransform != null ? leftHandTransform.name : "NULL";
            var rightPath = rightHandTransform != null ? rightHandTransform.name : "NULL";
            var leftControllerPath = leftControllerTransform != null ? leftControllerTransform.name : "NULL";
            var rightControllerPath = rightControllerTransform != null ? rightControllerTransform.name : "NULL";
            leftOVRHand = leftHandTransform != null ? leftHandTransform.GetComponent<OVRHand>() : null;
            rightOVRHand = rightHandTransform != null ? rightHandTransform.GetComponent<OVRHand>() : null;
            Debug.Log(
                $"[HandPoseSender] Refs resolved: " +
                $"leftHand={leftPath} (ovr={leftOVRHand != null}), rightHand={rightPath} (ovr={rightOVRHand != null}), " +
                $"leftController={leftControllerPath}, rightController={rightControllerPath}, " +
                $"headset={(headsetTransform != null ? headsetTransform.name : "NULL")}, " +
                $"controlFrame={(controlFrameTransform != null ? controlFrameTransform.name : "NULL")}, " +
                $"preferControllers={preferControllers}, sendRelativeToHeadset={sendRelativeToHeadset}, " +
                $"sendRelativeToControlFrame={sendRelativeToControlFrame}, " +
                $"includeWorkspacePoseInControls={includeWorkspacePoseInControls}, mappingMode={mappingMode}"
            );
        }
    }

    void ResolveControlFrame(bool forceLog = false)
    {
        if (!string.IsNullOrWhiteSpace(controlFrameName))
        {
            GameObject frame = GameObject.Find(controlFrameName);
            if (frame != null)
                controlFrameTransform = frame.transform;
        }

        if (forceLog)
        {
            Debug.Log(
                $"[HandPoseSender] Control frame resolved: " +
                $"{(controlFrameTransform != null ? controlFrameTransform.name : "NULL")}, " +
                $"sendRelativeToControlFrame={sendRelativeToControlFrame}, " +
                $"includeWorkspacePoseInControls={includeWorkspacePoseInControls}"
            );
        }
    }

    bool TryGetHeadsetPose(out Vector3 pos, out Quaternion rot)
    {
        if (headsetTransform == null)
        {
            var headset = GameObject.Find("OVRCameraRig/TrackingSpace/CenterEyeAnchor") ?? GameObject.Find("CenterEyeAnchor");
            if (headset != null) headsetTransform = headset.transform;
            else if (Camera.main != null) headsetTransform = Camera.main.transform;
        }

        if (headsetTransform != null && headsetTransform.gameObject.activeInHierarchy)
        {
            pos = headsetTransform.position;
            rot = headsetTransform.rotation;
            return true;
        }

        pos = Vector3.zero;
        rot = Quaternion.identity;
        return false;
    }

    void ConvertToHeadRelative(ref Vector3 worldPos, ref Quaternion worldRot)
    {
        if (!sendRelativeToHeadset) return;
        if (!TryGetHeadsetPose(out Vector3 headPos, out Quaternion headRot)) return;

        Quaternion invHead = Quaternion.Inverse(headRot);
        worldPos = invHead * (worldPos - headPos);
        worldRot = invHead * worldRot;
    }

    void ConvertToControlFrame(ref Vector3 worldPos, ref Quaternion worldRot)
    {
        if (!sendRelativeToControlFrame) return;
        if (controlFrameTransform == null)
            ResolveControlFrame(forceLog: false);
        if (controlFrameTransform == null) return;

        worldPos = controlFrameTransform.InverseTransformPoint(worldPos);
        worldRot = Quaternion.Inverse(controlFrameTransform.rotation) * worldRot;
    }

    void ConvertPoseFrame(ref Vector3 worldPos, ref Quaternion worldRot)
    {
        if (sendRelativeToControlFrame)
        {
            ConvertToControlFrame(ref worldPos, ref worldRot);
            return;
        }

        ConvertToHeadRelative(ref worldPos, ref worldRot);
    }

    bool IsControllerConnected(OVRInput.Controller controller)
    {
        var connected = OVRInput.GetConnectedControllers();
        return (connected & controller) != OVRInput.Controller.None;
    }

    HandData GetRightInputData()
    {
        if (preferControllers && rightControllerTransform != null && rightControllerTransform.gameObject.activeInHierarchy)
        {
            HandData data = new HandData();
            data.isTracked = IsControllerConnected(OVRInput.Controller.RTouch);
            if (data.isTracked)
            {
                data.pos = rightControllerTransform.position;
                data.rot = rightControllerTransform.rotation;
                ConvertPoseFrame(ref data.pos, ref data.rot);
            }
            return data;
        }
        return GetHandDataFromTransform(rightHandTransform, rightOVRHand);
    }

    HandData GetLeftInputData()
    {
        if (preferControllers && leftControllerTransform != null && leftControllerTransform.gameObject.activeInHierarchy)
        {
            HandData data = new HandData();
            data.isTracked = IsControllerConnected(OVRInput.Controller.LTouch);
            if (data.isTracked)
            {
                data.pos = leftControllerTransform.position;
                data.rot = leftControllerTransform.rotation;
                ConvertPoseFrame(ref data.pos, ref data.rot);
            }
            return data;
        }
        return GetHandDataFromTransform(leftHandTransform, leftOVRHand);
    }

    HandData GetHandDataFromTransform(Transform t, OVRHand ovrHand)
    {
        HandData data = new HandData();
        if (t != null && t.gameObject.activeInHierarchy)
        {
            bool tracked = (ovrHand == null) ? true : ovrHand.IsTracked;
            data.isTracked = tracked;
            if (tracked)
            {
                data.pos = t.position;
                data.rot = t.rotation;
                ConvertPoseFrame(ref data.pos, ref data.rot);
            }
        }
        else
        {
            data.isTracked = false;
        }
        return data;
    }

    void OnDestroy()
    {
        CloseTcpClient();
    }
}
