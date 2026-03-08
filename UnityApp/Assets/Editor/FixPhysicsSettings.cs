#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class FixPhysicsSettings : MonoBehaviour
{
    [MenuItem("Tools/Fix Physics Settings")]
    public static void ApplyFix()
    {
        // Increase solver iterations for stable ArticulationBody simulation
        Physics.defaultSolverIterations = 30;
        Physics.defaultSolverVelocityIterations = 30;
        
        // Ensure a good fixed timestep (default 0.02 is usually fine, but 0.01 can be smoother for robots)
        // Time.fixedDeltaTime = 0.01f; 

        // Optional: Disable auto-sync transforms if performance is an issue, but usually not needed for stability.
        // Physics.autoSyncTransforms = false;

        Debug.Log($"Physics Settings Updated: Iterations set to {Physics.defaultSolverIterations}");
    }
}
#endif
