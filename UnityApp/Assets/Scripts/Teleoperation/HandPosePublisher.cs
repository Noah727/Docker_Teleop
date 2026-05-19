using UnityEngine;
using System.Collections;

namespace Teleoperation
{
    public class HandPosePublisher : MonoBehaviour
    {
        [Header("Hand Tracking Settings")]
        [Tooltip("Assign the OVRHand component here (drag the object)")]
        public OVRHand linkedHand; 
        
        public bool debugLog = true;

        void Start()
        {
            if (linkedHand == null)
            {
                // Auto-discovery for convenience
                var handObj = GameObject.Find("RightHand") ?? GameObject.Find("LeftHand");
                if (handObj != null) linkedHand = handObj.GetComponent<OVRHand>();
            }
        }

        void Update()
        {
            if (linkedHand == null) return;
            
            if (linkedHand.IsTracked)
            {
                if (debugLog) Debug.Log("Hand is tracked!");
                
                // Example of reading pointer pose
                // Pose pointerPose = linkedHand.PointerPose;
                // transform.position = pointerPose.position;
                // transform.rotation = pointerPose.rotation;
            }
        }
    }
}
