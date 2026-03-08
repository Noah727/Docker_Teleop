using UnityEngine;

public class VRCameraFix : MonoBehaviour
{
    public Transform targetToLookAt; // Optional: Assign the Robot or Table here
    
    // Start is called before the first frame update
    System.Collections.IEnumerator Start()
    {
        Debug.Log($"[VRCameraFix] Start Position: {transform.position}");
        Debug.Log($"[VRCameraFix] Start Rotation: {transform.rotation.eulerAngles}");
        
        // Wait a moment for OVR tracking to initialize
        yield return new WaitForSeconds(1.0f);
        
        Debug.Log("[VRCameraFix] Attempting auto-recenter after delay...");
        Recenter();
    }

    // Handle the "putting headset back on" case programmatically
    void OnApplicationPause(bool pauseStatus)
    {
        // pauseStatus = false means we are RESUMING (putting headset back on/waking up)
        if (!pauseStatus)
        {
            Debug.Log("[VRCameraFix] Application Resumed. Forcing Recenter.");
            Recenter();
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Check for "A" button on Quest controller or "R" key on keyboard
        if (OVRInput.GetDown(OVRInput.Button.One) || Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("[VRCameraFix] Recenter Request Triggered via Input");
            Recenter();
        }

        // Debug log every few seconds if position is weird (e.g. very far away)
        if (Time.frameCount % 300 == 0)
        {
             // Log less frequently to avoid spam, but keep monitoring
        }
    }

    public void Recenter()
    {
        if (OVRManager.display != null)
        {
            Debug.Log("[VRCameraFix] Calling OVRManager.display.RecenterPose()");
            OVRManager.display.RecenterPose();
        }
        else
        {
             Debug.LogError("[VRCameraFix] OVRManager.display is null!");
        }
    }
}
