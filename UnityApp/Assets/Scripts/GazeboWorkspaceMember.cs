using UnityEngine;

/// <summary>
/// Marks an object as part of the movable Gazebo/MR workspace.
/// WorkspaceDragController reparents these members under GazeboWorkspace and
/// captures their starting poses during drag, so replica scenes move as one unit.
/// </summary>
public class GazeboWorkspaceMember : MonoBehaviour
{
    [Tooltip("Keep the current world pose when the object is automatically parented to the workspace root.")]
    public bool keepWorldPoseWhenParented = true;
}
