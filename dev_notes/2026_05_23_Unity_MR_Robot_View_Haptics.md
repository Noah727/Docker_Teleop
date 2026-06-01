# 2026-05-23 Unity MR, Robot View, Haptics

## Goal

Improve the Quest Unity app with four features:

1. Mixed-reality passthrough so the user sees the real environment with the virtual robot/table overlaid.
2. A second robot viewpoint directly above the robot base, with a controller/hand proxy shown in that view.
3. A documented protocol for switching from UR5e to another robot arm.
4. Haptic feedback for blocked EE motion and object pinch confirmation.

## Implementation

Added Unity runtime feature scripts:

```text
UnityApp/Assets/Scripts/QuestMRFeatureBootstrap.cs
UnityApp/Assets/Scripts/QuestPassthroughController.cs
UnityApp/Assets/Scripts/RobotViewpointController.cs
UnityApp/Assets/Scripts/QuestHapticFeedbackController.cs
UnityApp/Assets/Scripts/TeleopRuntimeDebugPanel.cs
UnityApp/Assets/Editor/QuestMRFeatureSetup.cs
```

Updated existing Unity scripts:

```text
UnityApp/Assets/Scripts/HandPoseSender.cs
UnityApp/Assets/Ur5eTrajectorySubscriber.cs
```

Updated Meta project config:

```text
UnityApp/Assets/Oculus/OculusProjectConfig.asset
```

Added backend pose publication for haptics/debug:

```text
ros_backend1.0/src/teleop_bridge/teleop_bridge/received_pose_to_target_twist.py
```

## Behavior

`QuestMRFeatureBootstrap` auto-creates a `QuestMRFeatures` runtime object after scene load. That object adds the MR passthrough controller, robot-view camera, haptic controller, and runtime debug panel if they are not already in the scene.

`QuestPassthroughController` enables `OVRManager.isInsightPassthroughEnabled`, creates an `OVRPassthroughLayer` underlay, sets headset cameras to transparent/solid clear, and hides virtual room/decor objects by name. It keeps `Desk`, `ur5e`, `Sync_*`, panels, and instruction boards visible.

`RobotViewpointController` creates `RobotBaseViewCamera`, renders it to `RobotBaseView_RT`, and displays it on `RobotBaseViewWindow`. It also creates `RobotView_RightControllerProxy`, which follows the right controller pose so the robot-view camera can see the user's controller/hand proxy.

`QuestHapticFeedbackController` subscribes to:

```text
/teleop/target_ee_pose
/teleop/actual_ee_pose
```

This original EE-gap/stall heuristic is currently disabled for teleop isolation. Future haptic feedback should be driven by Gazebo contact state instead.

`TeleopRuntimeDebugPanel` creates a small world-space debug panel showing teleop, gripper, MR, haptic, and robot-view status.

## Known Limitations

The pinch/contact vibration is currently a heuristic. It is not a true Gazebo contact signal. A better future implementation is to add Gazebo contact sensors/plugins on the gripper fingers and publish a standard contact topic back to Unity.

The robot-view camera is a preview panel, not a full headset camera takeover. This is safer for testing because it does not disorient the user or break MR passthrough. If we later want full first-person robot embodiment, add an explicit toggle and recenter flow.

The virtual-room hiding is name-based. If room/decor object names change, update `QuestPassthroughController.hideObjectNameContains` and `keepObjectNameContains`.

## 2026-05-23 Follow-Up: Gazebo Replica MR Workspace

Added a fresh build scene:

```text
UnityApp/Assets/Scenes/GazeboReplica_MR.unity
```

This scene is based on the working robot scene so it keeps the imported UR5e/Hand-E hierarchy and ROS wiring, but it now uses `GazeboReplicaSceneBuilder` to rebuild the visible workspace as a Gazebo-style setup:

```text
GazeboWorkspace
Gazebo_Table
WorkspaceDragHandle_Ring
GameObjects_sync/Sync_*
ur5e
```

The builder hides the old room/decor/desk and creates a 2 m x 2 m x 0.8 m gray Gazebo table matching `ros_backend1.0/simulation/worlds/ur_hande_tabletop.sdf`. The top surface aligns with the robot base height. The synced cube/cylinder/plate visuals are placed using the same SDF object poses, converted into Unity coordinates.

`WorkspaceDragController` now moves one workspace root instead of separate `Desk` and `ur5e` roots. It only starts dragging when the right-controller ray hits an object whose name contains `WorkspaceDragHandle`, so the blue table skirt/ring is the intentional grab target. While dragging, the user can translate horizontally, move up/down, and yaw-rotate the whole workspace; pitch and roll stay locked to keep the table level with the room floor.

The Unity build settings now enable `GazeboReplica_MR.unity` and disable the older `Ur5e_Working 1.unity` scene for builds.

The panels/windows now have small `DragHandle` notches:

```text
Teleop_Button_Instructions
Teleop_Runtime_DebugPanel
GripperCameraFloatingPanel
RobotBaseViewWindow
```

The shared `VRDraggableWindow` helper lets the user drag these panels with the left-controller trigger while right-grip teleop is released.

Control-loop debug result:

```text
/tmp/qcr.log: RX 0.0 Hz from none
```

A direct Quest-side ADB test packet to `127.0.0.1:5026` did connect to the ROS receiver, so the wired ADB reverse tunnel and backend listener are healthy. The failure is on the Unity app side: the built app was not maintaining a `HandPoseSender` TCP stream. `QuestMRFeatureBootstrap` now enables an existing `HandPoseSender` if found, or creates a fallback `NetworkSender` with `targetIP=127.0.0.1`, `targetPort=5026`, `sendRelativeToHeadset=false`, and `preferControllers=true`.

## 2026-05-23 Follow-Up: Haptics Disabled And Ray Aiming

The EE-gap haptic path is disabled for isolation:

```text
QuestMRFeatureBootstrap.enableHapticsFeature = false
QuestHapticFeedbackController.enableEeGapHaptics = false
QuestHapticFeedbackController.enablePinchContactPulse = false
```

This returns the teleop path to the non-haptic control loop while we debug packet flow.

Added:

```text
UnityApp/Assets/Scripts/ControllerRayVisual.cs
```

This draws controller aim rays in MR. The line changes color and shows a small hit dot when it points at an interactable target:

```text
WorkspaceDragHandle*
DragHandle
ToggleRecordingButton
CaptureFrameButton
```

The right-hand ray is hidden while right-grip teleop is held so robot-control mode stays visually clean. When right grip is released, the right ray can aim at the blue workspace skirt/ring. The left ray can aim at panel/window drag notches.

Backend receiver diagnostics were added to `quest_controller_receiver.py`:

```text
bytes=<window bytes>
decode_errors=<window JSON errors>
client_idle_disconnect_sec=1.5
```

The receiver now drops an idle TCP client if it stays connected but sends no bytes. This prevents Unity from sitting on a stale TCP connection while ROS keeps publishing neutral states.

Controlled packet test after rebuild:

```text
RX 4.5 Hz from 127.0.0.1, bytes=1746, decode_errors=0, stale=False, tracked=True, teleop=True
received_pose_to_target_twist: tracked=True, tf_ok=True, teleop=True, lin=(...)
```

So the backend receiver and mapper can parse valid packets. If the headset app still fails, inspect `/tmp/qcr.log`: `bytes=0` means no bytes reached ROS, while `bytes>0 decode_errors>0` means Unity sent malformed JSON.

Next haptics design should use Gazebo contact state, not target-vs-actual EE gap. Preferred architecture:

```text
Gazebo finger/object contact sensor or contact bridge
-> ROS contact topic, e.g. /gripper_contact_state
-> Unity subscriber
-> short controller pulse only when Gazebo reports contact
```

Do not re-enable EE-gap haptics unless explicitly testing target-error feedback.

## Test Plan

1. Rebuild the ROS workspace because `received_pose_to_target_twist.py` changed.
2. Start backend and confirm pose topics:

```bash
ros2 topic echo /teleop/target_ee_pose --once
ros2 topic echo /teleop/actual_ee_pose --once
```

3. Build and run Unity on Quest 3.
4. Confirm passthrough appears and virtual room walls/floor/decor are hidden.
5. Confirm table, robot, synced task objects, instruction board, control panel, robot-view window, and runtime debug panel remain visible.
6. Confirm no continuous EE-gap haptic vibration occurs while testing teleop.
7. Confirm controller rays appear in MR and highlight the workspace skirt/window drag handles.
8. Check `/tmp/qcr.log` for `bytes`, `decode_errors`, `tracked`, and `teleop`.
9. Check `adb logcat Unity:I '*:S'` for errors from the new scripts.

## 2026-05-23 Follow-Up: Workspace Drag, Rays, And Servo Timing Fix

Unity workspace placement was tightened so synced Gazebo object poses are applied through the movable `GazeboWorkspace` frame instead of staying fixed in world space:

```text
UnityApp/Assets/Scripts/GazeboPoseStampedSubscriber.cs
UnityApp/Assets/Scripts/GazeboReplicaSceneBuilder.cs
UnityApp/Assets/Scripts/WorkspaceDragController.cs
```

`GazeboPoseStampedSubscriber` now has `workspaceRootName=GazeboWorkspace` and `applyWorkspaceTransform=true`. This means ROS/Gazebo object poses are treated as Gazebo-world-local poses, then transformed into the user's MR room through the dragged workspace root.

`WorkspaceDragController` also keeps `ur5e` and `GameObjects_sync` parented under `GazeboWorkspace`, so dragging the blue workspace skirt is intended to move the operational scene as one unit.

Controller rays were made less intrusive:

```text
ControllerRayVisual.showOnlyOnInteractionIntent = true
ControllerRayVisual.indexTriggerIntentThreshold = 0.10
ControllerRayVisual.handTriggerIntentThreshold = 0.20
```

The rays are now translucent and only appear while the user is starting an interaction gesture. The right ray still hides while right-grip teleop is held.

Backend arm-control diagnosis:

1. Quest TCP packets were reaching ROS correctly during the active test.
2. `received_pose_to_target_twist` produced nonzero twist commands.
3. `target_twist_to_servo_cmd` published to `/servo_node/delta_twist_cmds`.
4. MoveIt Servo initially emitted zero joint velocities because `/clock` had no publisher while Servo was launched with `use_sim_time=true`.
5. After adding `/clock`, Servo processed commands but returned status `2`, which is `HALT_FOR_SINGULARITY`.
6. The startup/reset pose was still wrist-singular because `wrist_2_joint` was near zero.

Backend fixes:

```text
ros_backend1.0/simulation/launch/run_tabletop_sim.sh
ros_backend1.0/src/servo_test_config/launch/servo_gz.launch.py
ros_backend1.0/src/ur_hande_description/config/initial_positions.yaml
ros_backend1.0/src/teleop_bridge/config/teleop_tuning.yaml
ros_backend1.0/scripts/backend10_lifecycle.sh
```

`run_tabletop_sim.sh` now starts a Gazebo clock bridge:

```bash
ros2 run ros_gz_bridge parameter_bridge '/clock@rosgraph_msgs/msg/Clock[ignition.msgs.Clock'
```

Teleop bridge nodes now use sim time so command stamps match Servo/Gazebo time:

```yaml
use_sim_time: true
```

Startup and reset home now use the MoveIt `test_configuration` pose to avoid the wrist singularity:

```text
shoulder_pan_joint: 1.54
shoulder_lift_joint: -1.62
elbow_joint: 1.4
wrist_1_joint: -1.2
wrist_2_joint: -1.6
wrist_3_joint: -0.11
```

Validation result after restart:

```text
/servo_node/status: data: 0
/joint_group_velocity_controller/commands: nonzero 6-joint velocity vector
/joint_states: arm joints changed after direct sim-time-stamped Servo twist
```

This confirms the arm control path is alive again. If the headset app does not immediately reconnect after backend restart, restart the app or check `adb reverse --list` for both `tcp:5026` and `tcp:10001`.
