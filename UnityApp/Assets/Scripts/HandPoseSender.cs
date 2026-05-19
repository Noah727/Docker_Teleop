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
    private int gripperToggleCommand;
    public bool preferOVRHands = true;

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
        public bool recenter_enable;
        public bool mode_switch_enable;
        public bool teleop_enable;
        public float grip_value;
        public float trigger_value;
        public float left_grip_value;
        public float left_trigger_value;
        public float left_thumbstick_x;
        public float left_thumbstick_y;
        public float right_thumbstick_x;
        public float right_thumbstick_y;
        public string source;
        public Quaternion right_controller_rot;
    }

    [Header("Controller Input")]
    [Range(0.05f, 0.95f)]
    public float analogPressThreshold = 0.55f;

    [Header("Pose Frame")]
    [Tooltip("If enabled, sent hand/controller pose is expressed in headset local frame.")]
    public bool sendRelativeToHeadset = false;
    [Tooltip("Optional explicit headset transform. If empty, script auto-resolves CenterEyeAnchor/Main Camera.")]
    public Transform headsetTransform;

    void Start()
    {
        ResolveHandTransforms(forceLog: true);
        ConnectTcp(forceLog: true);
    }

    void Update()
    {
        if ((leftHandTransform == null || rightHandTransform == null) && Time.time >= nextResolveTime)
        {
            ResolveHandTransforms(forceLog: true);
            nextResolveTime = Time.time + 1.0f;
        }

        if (!EnsureConnected())
        {
            return;
        }

        Packet packet = new Packet();
        packet.timestamp = Time.time;

        packet.left_hand = GetLeftInputData();
        packet.right_hand = GetRightInputData();
        packet.controls = GetControlsData(packet.right_hand);

        string json = JsonUtility.ToJson(packet);
        SendJsonPacket(json);
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

    ControlsData GetControlsData(HandData rightHand)
    {
        ControlsData controls = new ControlsData();

        controls.rotate_held = OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.RTouch);
        controls.reset_held = OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.RTouch);
        controls.recenter_held =
            OVRInput.Get(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch) ||
            OVRInput.Get(OVRInput.Button.SecondaryThumbstick, OVRInput.Controller.Touch) ||
            OVRInput.Get(OVRInput.RawButton.RThumbstick);
        // When querying a specific Touch controller, OVRInput remaps the face buttons:
        // LTouch: Button.One=X, Button.Two=Y. RTouch: Button.One=A, Button.Two=B.
        // The combined Touch profile maps Button.Four=Y, so keep it as a fallback.
        controls.mode_switch_held =
            OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.LTouch) ||
            OVRInput.Get(OVRInput.Button.Four, OVRInput.Controller.Touch);

        // For RTouch, use Primary* virtual axes. Secondary* maps to None on single-hand controller queries.
        float rightGripValue = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch);
        float rightTriggerValue = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
        float leftGripValue = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch);
        float leftTriggerValue = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
        Vector2 leftStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
        Vector2 rightStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);

        // Fallback for profiles that expose both controllers through the combined Touch mapping.
        if (leftGripValue < 0.0001f)
        {
            leftGripValue = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.Touch);
        }
        if (leftTriggerValue < 0.0001f)
        {
            leftTriggerValue = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.Touch);
        }
        if (leftStick.sqrMagnitude < 0.0001f)
        {
            leftStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.Touch);
        }
        if (rightStick.sqrMagnitude < 0.0001f)
        {
            rightStick = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick, OVRInput.Controller.Touch);
        }
        bool rightTriggerHeld = rightTriggerValue >= analogPressThreshold;
        if (rightTriggerHeld && !rightTriggerWasHeld)
            gripperToggleCommand = gripperToggleCommand <= 0 ? 1 : -1;
        rightTriggerWasHeld = rightTriggerHeld;

        controls.grip_value = rightGripValue;
        controls.trigger_value = rightTriggerValue;
        controls.left_grip_value = leftGripValue;
        controls.left_trigger_value = leftTriggerValue;
        controls.close_held = gripperToggleCommand > 0;
        controls.open_held = gripperToggleCommand < 0;
        controls.teleop_held = rightGripValue >= analogPressThreshold;
        controls.rotate_enable = controls.rotate_held;
        controls.close_enable = controls.close_held;
        controls.open_enable = controls.open_held;
        controls.reset_enable = controls.reset_held;
        controls.recenter_enable = controls.recenter_held;
        controls.mode_switch_enable = controls.mode_switch_held;
        controls.teleop_enable = controls.teleop_held;
        controls.left_thumbstick_x = leftStick.x;
        controls.left_thumbstick_y = leftStick.y;
        controls.right_thumbstick_x = rightStick.x;
        controls.right_thumbstick_y = rightStick.y;
        controls.source = "quest_dual_controller";

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
                $"preferControllers={preferControllers}, sendRelativeToHeadset={sendRelativeToHeadset}"
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
                ConvertToHeadRelative(ref data.pos, ref data.rot);
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
                ConvertToHeadRelative(ref data.pos, ref data.rot);
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
                ConvertToHeadRelative(ref data.pos, ref data.rot);
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
