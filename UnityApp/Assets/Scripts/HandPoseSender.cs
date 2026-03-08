using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class HandPoseSender : MonoBehaviour
{
    [Header("Network Settings")]
    [Tooltip("The IP address to send data to. Use 255.255.255.255 for broadcast.")]
    public string targetIP = "255.255.255.255";
    public int targetPort = 5005;

    [Header("Hand References")]
    public Transform leftHandTransform;
    public Transform rightHandTransform;
    public OVRHand leftOVRHand;
    public OVRHand rightOVRHand;

    [Header("Controller References")]
    public bool preferControllers = true;
    public Transform leftControllerTransform;
    public Transform rightControllerTransform;

    private UdpClient udpClient;
    private IPEndPoint remoteEndPoint;
    private float nextResolveTime;
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
        public bool rotate_enable;
        public bool close_enable;
        public bool open_enable;
        public bool reset_enable;
        public float grip_value;
        public float trigger_value;
        public string source;
        public Quaternion right_controller_rot;
    }

    [Header("Controller Input")]
    [Range(0.05f, 0.95f)]
    public float analogPressThreshold = 0.55f;

    void Start()
    {
        udpClient = new UdpClient();
        remoteEndPoint = new IPEndPoint(IPAddress.Parse(targetIP), targetPort);
        Debug.Log($"[HandPoseSender] Sending to {targetIP}:{targetPort}");
        ResolveHandTransforms(forceLog: true);
    }

    void Update()
    {
        if ((leftHandTransform == null || rightHandTransform == null) && Time.time >= nextResolveTime)
        {
            ResolveHandTransforms(forceLog: true);
            nextResolveTime = Time.time + 1.0f;
        }

        Packet packet = new Packet();
        packet.timestamp = Time.time;
        
        packet.left_hand = GetLeftInputData();
        packet.right_hand = GetRightInputData();
        packet.controls = GetControlsData(packet.right_hand);

        string json = JsonUtility.ToJson(packet);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        try
        {
            udpClient.Send(bytes, bytes.Length, remoteEndPoint);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HandPoseSender] Error: {e.Message}");
        }
    }

    ControlsData GetControlsData(HandData rightHand)
    {
        ControlsData controls = new ControlsData();

        controls.rotate_held = OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.RTouch);
        controls.reset_held = OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.RTouch);

        // For RTouch, use Primary* virtual axes. Secondary* maps to None on single-hand controller queries.
        float closeValue = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch);
        float openValue = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
        controls.grip_value = closeValue;
        controls.trigger_value = openValue;
        controls.close_held = closeValue >= analogPressThreshold;
        controls.open_held = openValue >= analogPressThreshold;
        controls.rotate_enable = controls.rotate_held;
        controls.close_enable = controls.close_held;
        controls.open_enable = controls.open_held;
        controls.reset_enable = controls.reset_held;
        controls.source = "quest_right_controller";

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
        if (leftController != null) leftControllerTransform = leftController.transform;
        if (rightController != null) rightControllerTransform = rightController.transform;

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
                $"leftController={leftControllerPath}, rightController={rightControllerPath}, preferControllers={preferControllers}"
            );
        }
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
        if (udpClient != null) udpClient.Close();
    }
}
