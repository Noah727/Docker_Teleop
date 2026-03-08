using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class HandTrackingDebugger : MonoBehaviour
{
    private InputDevice rightHand;
    private InputDevice leftHand;

    public Transform robotTransform; // Add reference to robot

    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 40;
        style.normal.textColor = Color.white;

        GUILayout.BeginArea(new Rect(50, 50, Screen.width, Screen.height));
        GUILayout.Label("Hand Tracking Debugger", style);

        // Display robot transform position
        if (robotTransform != null)
        {
            GUILayout.Label($"Robot Pos: {robotTransform.position}", style);
        }
        else
        {
            GUILayout.Label("Robot Transform: Not Assigned", style);
        }

        GetDevices();

        if (rightHand.isValid)
        {
            GUILayout.Label("Right Hand Found", style);
            if (rightHand.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos))
            {
                GUILayout.Label($"R Pos: {pos}", style);
            }
            else
            {
                 GUILayout.Label("R Pos: No Data", style);
            }
        }
        else
        {
            GUILayout.Label("Searching for Right Hand...", style);
        }

        if (leftHand.isValid)
        {
            GUILayout.Label("Left Hand Found", style);
            if (leftHand.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos))
            {
                GUILayout.Label($"L Pos: {pos}", style);
            }
        }
        
        GUILayout.EndArea();
    }

    void GetDevices()
    {
        if (!rightHand.isValid)
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, devices);
            if (devices.Count > 0) rightHand = devices[0];
        }
        if (!leftHand.isValid)
        {
             var devices = new List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, devices);
            if (devices.Count > 0) leftHand = devices[0];
        }
    }
}
