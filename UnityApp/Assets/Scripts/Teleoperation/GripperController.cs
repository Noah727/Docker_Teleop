using UnityEngine;

namespace Teleoperation
{
    public class GripperController : MonoBehaviour
    {
        [Header("Input Settings")]
        public OVRHand linkedHand;
        public OVRHand.HandFinger fingerToPinch = OVRHand.HandFinger.Index;

        [Header("Gripper Simulation")]
        public Transform gripperJaws; 
        
        [Range(0f, 1f)]
        public float openWidth = 0.1f;
        [Range(0f, 1f)]
        public float closedWidth = 0.0f;

        void Start()
        {
            if (linkedHand == null)
            {
                var handObj = GameObject.Find("RightHand") ?? GameObject.Find("LeftHand");
                if (handObj != null) linkedHand = handObj.GetComponent<OVRHand>();
            }

            if (gripperJaws == null)
            {
                var viz = GameObject.Find("GripperVisualizer");
                if (viz != null) gripperJaws = viz.transform;
            }
        }

        void Update()
        {
            if (linkedHand == null) return;

            // Get pinch strength (0 to 1)
            float pinchStrength = linkedHand.GetFingerPinchStrength(fingerToPinch);
            
            // Map 0..1 (open..pinch) to openWidth..closedWidth
            float targetWidth = Mathf.Lerp(openWidth, closedWidth, pinchStrength);

            if (gripperJaws != null)
            {
                // Simple visualization: scale or move jaws. 
                // Assuming X-axis scaling for demo purposes, or just logging.
                // In a real robot, this would send a command.
                // gripperJaws.localScale = new Vector3(targetWidth, 1, 1);
            }
        }
    }
}
