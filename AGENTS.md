# Agent Handoff Context

Last updated: 2026-06-12

This file is for future coding agents taking over this project. Read this before changing code. It captures the practical context that is easy to lose between chats.

## Project Goal

This repo is a Quest 3 MR teleoperation platform for Gazebo-simulated robot manipulation.

Current system:

- Unity/Quest 3 provides MR passthrough UI, controller input, robot/object visualization, workspace dragging, control panel UI, and camera recording/preview.
- Dockerized ROS 2 Humble backend provides Gazebo simulation, MoveIt Servo, robot/gripper control, object synchronization, reset logic, and haptic/contact messages.
- Gazebo is the physics authority. Unity should be visualization and UI/control only, not a competing physics simulation.

Long-term direction:

- Treat the app/backend as a platform for multiple robot arms and task scenes.
- Support dual-arm operation as the main architecture, not as a later bolt-on.
- Make robots, workspaces, tasks, and control mappings configurable via profiles instead of hard-coded scene edits.

## Current Canonical Version

Use this unless the user says otherwise:

```text
Backend: ros_backend1.1
Container: motion_planner_11
Unity active scene: UnityApp/Assets/Scenes/GazeboReplica_DualArm_MR.unity
Single-arm backup scene: UnityApp/Assets/Scenes/GazeboReplica_MR.unity
Lifecycle script: ros_backend1.1/scripts/backend11_lifecycle.sh
Active scene profile pointer: ros_backend1.1/profiles/active_scene_profile.txt
Generated Gazebo world: ros_backend1.1/simulation/worlds/ur_hande_dual_arm_tabletop.sdf
Generated Unity task profile: UnityApp/Assets/Resources/TaskProfiles/active_task.json
```

Important: some top-level docs may still contain stale `ros_backend1.0`, `motion_planner_10`, or old scene references. Prefer `ros_backend1.1` and the files listed above.

There may be user edits in the Unity scene. Always run `git status --short` before editing and never revert user changes unless explicitly asked.

The current default simulation mode is headless Gazebo. Use headed mode only when the user explicitly wants to inspect Gazebo through noVNC:

```bash
SIM_HEADLESS=0 ./scripts/backend11_lifecycle.sh start_dual_sim
```

## Git Remotes

```text
origin        git@github.com:su-idr-lab/ros_unity_project.git
docker_teleop git@github.com:Noah727/Docker_Teleop.git
```

Use normal feature-branch discipline if doing larger work. Do not commit captures, videos, Unity `Library/`, temp files, or generated caches.

## Thesis / Evaluation Material State

The thesis/evaluation material is now organized around a curated/raw split:

```text
TODO.md
complete_material/
thesis_eval_raw/06_11/
thesis_eval_raw/Ubuntu_data/
thesis_eval_raw/Windows_data/
```

Read these first before touching evaluation scripts or thesis evidence:

```text
TODO.md
complete_material/README.md
complete_material/material_verification_06_11.md
```

Current accepted material in `complete_material/`:

- Setup snapshot and backend status for the Mac development machine.
- Mac runtime performance comparison: plain headless backend, noVNC-only headless backend, and noVNC + headed Gazebo.
- Task profile/SDF/Unity JSON consistency and saved-scene smoke checks.
- Mac portability snapshot.
- Demo source video, v2 topic-level clips, screenshots, contact sheet, checksums, and draft captions.
- Headset backend latency traces, with caveat: ROS/container arrival-time latency after receiver publication, not optical end-to-end display latency.
- RedCube MR sync/visual alignment, with caveat: RedCube object sync is usable; old EE absolute alignment rows are not usable because they compare mixed reference points.
- Wrist-camera recording file verification, with caveat: confirms recording and estimates capture rate, but does not measure Unity FPS/sync latency.
- No-headset backend scripted checks, with caveat: synthetic receiver is debug/supporting evidence, not the primary controller evaluation.

Still-needed thesis material is tracked in `TODO.md`, not by memory:

- Human-reviewed captions and final 5-8 second README teaser clips.
- Cable insertion trial rows.
- Pick/place trial rows and optional dual-arm handoff rows.
- Valid recording-on FPS/sync-latency trace after deploying an app that publishes `/unity_eval/recording_state` and `/unity_eval/fps_sample`.
- Linux/Ubuntu performance and portability data.
- Optional Windows/WSL portability data.
- Optional new EE visual alignment trace if quantitative EE visual alignment is needed.

Do not present these as passing thesis results unless fixed and rerun:

- Current gripper timing/symmetry script output; it did not exercise the coupled gripper path.
- Current reset reliability stress script output; it reported object-pose failures despite the user-facing reset button working during headset testing.
- Old EE absolute alignment rows from MR sync traces.
- Recording FPS traces that ended `max_wait_elapsed_without_recording`.

## Backend Bringup

From repo root:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh bringup_dual
./scripts/backend11_lifecycle.sh status
```

Useful lifecycle commands:

```bash
./scripts/backend11_lifecycle.sh safe_down
./scripts/backend11_lifecycle.sh up_container
./scripts/backend11_lifecycle.sh up_container_build
./scripts/backend11_lifecycle.sh build_ws
./scripts/backend11_lifecycle.sh build_receiver
./scripts/backend11_lifecycle.sh wired_on
./scripts/backend11_lifecycle.sh wired_status
./scripts/backend11_lifecycle.sh generate_dual_world
./scripts/backend11_lifecycle.sh start_dual_sim
./scripts/backend11_lifecycle.sh start_dual_servo
./scripts/backend11_lifecycle.sh start_receiver
./scripts/backend11_lifecycle.sh start_dual_part23
./scripts/backend11_lifecycle.sh start_dual_part4
./scripts/backend11_lifecycle.sh list_tasks
./scripts/backend11_lifecycle.sh select_task pick_place_basic
./scripts/backend11_lifecycle.sh select_task rubik_2x2
./scripts/backend11_lifecycle.sh select_task cable_insertion
./scripts/backend11_lifecycle.sh debug_self_collision
./scripts/backend11_lifecycle.sh status
```

`bringup_dual` performs the normal sequence:

1. Start container.
2. Enable wired ADB reverse if the backend is in wired mode and the Quest is connected.
3. Build/check workspace.
4. Generate the dual-arm Gazebo world from profiles.
5. Start Gazebo dual-arm simulation, headless by default.
6. Start dual MoveIt Servo.
7. Start Quest TCP receiver.
8. Start dual Part 2/3 mapping/control nodes.
9. Start Part 4 ROS-TCP/object sync/haptics/task-manager services.

Part 4 currently starts:

- `ros_tcp_endpoint` for Unity ROS-TCP.
- Gazebo dynamic pose bridge.
- `runtime_task_manager`.
- `task_pose_sync_publisher`.
- Optional `rubik_task_controller` only when a Rubik task is active.
- `contact_haptic_publisher` when `ENABLE_HAPTICS=1`.

## Wired Quest Connection

Preferred local development mode is wired USB with `adb reverse`.

Unity/Quest sends controller data over TCP to host port `5026`, which maps to container port `5005`.
Unity ROS-TCP connects to host port `10001`, which maps to container port `10000`.

In wired mode, Unity should use:

```text
Hand/controller TCP target IP: 127.0.0.1
Hand/controller TCP target port: 5026
ROS Settings IP: 127.0.0.1
ROS Settings port: 10001
```

`adb reverse` makes Quest `127.0.0.1:<port>` tunnel back to the Mac host. It does not identify a physical network port; it is an ADB-managed USB tunnel.

Check wired status:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh wired_status
adb devices
adb reverse --list
```

## Backend Data Flow

High-level control loop:

```text
Quest Unity controller pose
  -> TCP receiver: receiver/quest_controller_receiver
  -> /left_arm/received_pose_states and /right_arm/received_pose_states
  -> teleop_bridge/mapping/hand_pose_mapper
  -> /left_arm/target_twist_states and /right_arm/target_twist_states
  -> teleop_bridge/servo_bridge/servo_command_bridge
  -> MoveIt Servo delta_twist_cmds
  -> Gazebo controllers
```

Gripper loop:

```text
Quest trigger state
  -> TargetTwistStates gripper fields
  -> teleop_bridge/gripper_control/coupled_gripper_controller
  -> /left_hande_position_controller/commands and /right_hande_position_controller/commands
  -> Gazebo Hand-E finger joints
```

Sync loop:

```text
Gazebo dynamic poses/joint states
  -> ROS/Gazebo bridge and task_pose_sync_publisher
  -> /unity_sync/<object>_pose
  -> ROS-TCP Endpoint
  -> Unity SceneObjectPoseSyncManager/GazeboPoseStampedSubscriber
```

Runtime task layout loop:

```text
runtime_task_manager
  -> /task_manager/status
  -> /task_manager/active_task_manifest
  -> Unity SceneObjectPoseSyncManager
  -> GazeboReplicaDualArmSceneBuilder rebuilds only GazeboWorkspace/TaskGroups/TaskGroup_Main
```

## Current Controller Behavior

The profile summary is in:

```text
ros_backend1.1/profiles/controls/quest_dual_arm.yaml
```

Current intended controls:

- Left grip hold: engage left arm teleop.
- Right grip hold: engage right arm teleop.
- Left trigger tap: toggle left gripper open/close.
- Right trigger tap: toggle right gripper open/close.
- Left X hold: left arm rotation mode.
- Right A hold: right arm rotation mode.
- Left Y tap: left arm attachment mode toggle.
- Right B tap: right arm attachment mode toggle.
- Control panel `Swap Hands` button: swap which physical controller drives each robot arm.
- Central control panel owns non-motion controls such as reset, mode status, recording, camera view selection, task selection, and task status.

Grip should engage per arm independently. Avoid fallback logic that engages both arms from one combined grip axis unless there is no alternative and it is explicitly documented.

## Teleop Tuning Files

Single-arm/default tuning:

```text
ros_backend1.1/src/teleop_bridge/config/teleop_tuning.yaml
```

Dual-arm tuning:

```text
ros_backend1.1/src/teleop_bridge/config/teleop_tuning_dual_left.yaml
ros_backend1.1/src/teleop_bridge/config/teleop_tuning_dual_right.yaml
```

Important parameters:

- `map_axes`, `map_signs`: position mapping from Unity/controller world motion to robot base-frame motion.
- `rot_map_axes`, `rot_map_signs`, `rot_scale_xyz`: delta rotation mapping.
- `kp_linear`, `max_linear_speed`: EE positional chasing speed.
- `kp_angular`, `max_angular_speed`: normal rotation control speed.
- `attachment_*`: attachment-mode base transform, tool positional offset, tool rotation offset, and angular speed.
- `gamepad_*`: thumbstick/gamepad mode parameters.

Current dual-arm position mapping was fixed so hand forward/back, left/right, and up/down feel consistent in MR even when the workspace is moved/rotated. If it feels swapped again, inspect `map_axes` and `map_signs` first.

Current speed-tuning values are intentionally boosted for responsiveness:

```yaml
kp_linear: 6.0
max_linear_speed: 0.85
max_angular_speed: 6.0
attachment_max_angular_speed: 3.5
```

Current attachment defaults use absolute position and rotation:

```yaml
attachment_use_absolute_position: true
attachment_enable_rotation: true
attachment_use_absolute_rotation: true
attachment_tool_position_offset_xyz: [0.0, 0.0, 0.0]
attachment_tool_rotation_offset_xyzw: [0.5, 0.5, 0.5, 0.5]
```

The tool rotation offset is the rotation from controller/ray orientation to gripper EE orientation. It is easy for this to appear 90 or 180 degrees off because controller, Unity, robot base, and tool frames do not share the same forward/up convention.

## Gripper State

The coupled gripper controller should be the default for dual bringup.

Relevant files:

```text
ros_backend1.1/scripts/backend11_lifecycle.sh
ros_backend1.1/src/teleop_bridge/teleop_bridge/gripper_control/coupled_gripper_controller.py
ros_backend1.1/src/teleop_bridge/teleop_bridge/gripper_control/simple_gripper_command_bridge.py
ros_backend1.1/Readme/Coupled_Gripper_Controller.md
```

In `backend11_lifecycle.sh`, `start_dual_part23` chooses:

```bash
DUAL_GRIPPER_CONTROLLER=coupled
```

unless overridden with `DUAL_GRIPPER_CONTROLLER=position`.

Why coupled exists:

- The Gazebo Hand-E has two independent prismatic finger joints.
- The real Hand-E behaves like one coupled aperture mechanism.
- Independent finger targets caused asymmetric finger rail drift and one-finger-only behavior during contact.
- The coupled controller publishes one shared aperture target to both fingers and clamps behavior near contact.

If the gripper is too slow, tune the controller parameters in the dual tuning YAML files, especially `speed_m_per_s`, `close_speed_m_per_s`, and `open_speed_m_per_s` under the coupled gripper node sections.

Current coupled gripper speed defaults are:

```yaml
close_speed_m_per_s: 0.40
open_speed_m_per_s: 0.22
```

URDF/Xacro sources:

```text
ros_backend1.1/src/ur_hande_description/urdf/ur_hande.urdf.xacro
ros_backend1.1/src/robotiq_hande_description/urdf/robotiq_hande_gripper.xacro
ros_backend1.1/src/robotiq_hande_description/urdf/robotiq_hande_gripper.ros2_control.xacro
```

## Task Profile System

The backend now separates reusable workspace, robots, controls, and tasks.

Profiles live in:

```text
ros_backend1.1/profiles/
  workspaces/dual_arm_tabletop/workspace.yaml
  robots/ur5e_hande_dual/robot.yaml
  controls/quest_dual_arm.yaml
  tasks/pick_place_basic/task.yaml
  tasks/rubik_2x2/task.yaml
  tasks/cable_insertion/task.yaml
  scenes/dual_arm_tabletop/scene.yaml
  scenes/dual_arm_rubik_2x2/scene.yaml
  scenes/dual_arm_cable_insertion/scene.yaml
```

The current default `pick_place_basic` task is no longer only pick-and-place. It is the main combined evaluation/task profile and includes:

- A square task platform, `Sync_TaskPlatform`, size `[0.462, 0.100, 0.462]`.
- Four pick/place objects: red cube, green cube, red cylinder, green cylinder.
- Two blue plates.
- A movable five-port insertion box, `Sync_CableReceiverBox`, with upward-facing ports.
- A fixed five-port insertion box, `Sync_CableReceiverBox_Fixed`, with upward-facing ports.
- Five insertion port sizes: loose 2.0 mm/side, loose 1.5 mm/side, default 1.0 mm/side, tight 0.5 mm/side, tight 0.2 mm/side.
- Two cable rods: `Sync_CableRod` lying down like the original cable, and `Sync_CableRod_B` standing up.

Important current cable poses in the task profile:

```yaml
Sync_CableRod:
  local_position_xyz: [-0.055, 0.1045, 0.135]
  local_euler_xyz: [0.0, 0.0, 0.0]
Sync_CableRod_B:
  local_position_xyz: [0.055, 0.154, 0.135]
  local_euler_xyz: [-90.0, 0.0, 0.0]
```

Current robot spawn/home values are in:

```text
ros_backend1.1/profiles/robots/ur5e_hande_dual/robot.yaml
```

As of this handoff both arms use:

```yaml
home_joint_positions: [0.0, -1.57, 1.57, 0.0, 1.57, 0.0]
left spawn_pose_xyz_rpy: [0.0, 0.60, 0.0, 0.0, 0.0, 0.0]
right spawn_pose_xyz_rpy: [0.0, -0.60, 0.0, 0.0, 0.0, 0.0]
```

A scene profile chooses the active workspace, robot profile, control profile, and task profile:

```yaml
workspace_profile: ../../workspaces/dual_arm_tabletop/workspace.yaml
robot_profile: ../../robots/ur5e_hande_dual/robot.yaml
control_profile: ../../controls/quest_dual_arm.yaml
task_profile: ../../tasks/pick_place_basic/task.yaml
active_task_group: TaskGroup_Main
workspace_root_name: GazeboWorkspace
```

Generate the Gazebo world and Unity active task JSON:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh generate_dual_world
```

Generate a different task scene without editing the default scene profile:

```bash
DUAL_SCENE_PROFILE=profiles/scenes/dual_arm_cable_insertion/scene.yaml ./scripts/backend11_lifecycle.sh generate_dual_world
```

Generated outputs:

```text
ros_backend1.1/simulation/worlds/ur_hande_dual_arm_tabletop.sdf
UnityApp/Assets/Resources/TaskProfiles/active_task.json
```

The world generator is:

```text
ros_backend1.1/simulation/tools/generate_dual_arm_world_from_profiles.py
```

Detailed docs:

```text
ros_backend1.1/Readme/Task_Profile_Workflow.md
ros_backend1.1/Readme/Object_Position_Tuning.md
```

## How Task Switching Works Today

Current task switching has two paths.

Stable pre-launch/lifecycle path:

1. Edit `profiles/scenes/dual_arm_tabletop/scene.yaml` to point to a different `task_profile`, or pass `DUAL_SCENE_PROFILE=...`.
2. Run `generate_dual_world` or `bringup_dual`.
3. Gazebo starts with the generated SDF.
4. Unity receives `active_task.json` and can rebuild the scene visuals from the same profile.
5. In the Unity editor, run `Tools > Gazebo Replica > Rebuild Dual Arm Workspace In Active Scene` if the scene needs to be rebuilt after profile changes.

Lifecycle shortcut:

```bash
./scripts/backend11_lifecycle.sh list_tasks
./scripts/backend11_lifecycle.sh select_task pick_place_basic
./scripts/backend11_lifecycle.sh select_task rubik_2x2
./scripts/backend11_lifecycle.sh select_task cable_insertion
```

`select_task` updates `profiles/active_scene_profile.txt`, regenerates the world/profile, and restarts Gazebo, Servo, Part 2/3, and Part 4 when the container is running.

Runtime task-manager path:

- Backend node: `ros_backend1.1/src/teleop_bridge/teleop_bridge/task_sync/runtime_task_manager.py`
- Select topic: `/task_manager/select_task`
- Status topic: `/task_manager/status`
- Manifest topic: `/task_manager/active_task_manifest`
- Services: `/task_manager/list_tasks`, `/task_manager/current_task`, `/task_manager/regenerate_world`

Unity has a Task Switch page on the central panel that publishes task names and listens for active manifests. This is the intended long-term UX, but treat live hot-swap as newer/experimental compared with the lifecycle restart path.

Because the current `pick_place_basic` profile already includes the cable insertion objects, the user often does not need to switch tasks for normal demos/evaluation.

## Best Long-Term Task Switching Solution

Recommended target architecture, partially implemented:

- Keep `task.yaml` as the source of truth for each task.
- Use the ROS `task_manager` node with services/topics such as:
  - `/task_manager/list_tasks`
  - `/task_manager/select_task`
  - `/task_manager/reset_task`
  - `/task_manager/current_task`
- The task manager should load task profiles, spawn/delete/reset Gazebo task models, and publish the active task layout.
- Unity should have a `TaskSelection` page on the central control panel that calls those services through ROS-TCP.
- Unity should rebuild only the task group visuals under `GazeboWorkspace/TaskGroups/TaskGroup_Main`, not the whole robot/workspace.
- Synchronization should follow the active task object list published by the backend, so Unity does not keep stale objects from previous tasks.

This avoids creating separate Unity scenes for every task. Different scenes should only be used when the overall workspace or robot arrangement changes significantly. For normal task changes, switch task profiles at runtime.

Remaining work:

- Harden live Gazebo model delete/spawn for every task type.
- Keep Unity stale-object cleanup strict when task manifests change.
- Add a reset service that uses the active task manifest rather than hard-coded object names.
- Decide whether final demos should use runtime task switching or the simpler combined `pick_place_basic` profile.

## Unity Architecture Notes

Important Unity scripts:

```text
UnityApp/Assets/Scripts/HandPoseSender.cs
UnityApp/Assets/Scripts/GazeboReplicaDualArmSceneBuilder.cs
UnityApp/Assets/Scripts/SceneObjectPoseSyncManager.cs
UnityApp/Assets/Scripts/GazeboPoseStampedSubscriber.cs
UnityApp/Assets/Scripts/WorkspaceDragController.cs
UnityApp/Assets/Scripts/MRCentralControlPanel.cs
UnityApp/Assets/Scripts/ControllerRayVisual.cs
UnityApp/Assets/Scripts/GripperCameraRecorder.cs
UnityApp/Assets/Scripts/FloatingSceneCameraController.cs
UnityApp/Assets/Scripts/QuestHapticFeedbackController.cs
UnityApp/Assets/Scripts/RobotFirstPersonTestMode.cs
UnityApp/Assets/Scripts/RobotViewpointController.cs
```

Unity scene structure should keep the whole simulated workspace under one root:

```text
GazeboWorkspace
  Gazebo_Table
  robots/robot visuals
  TaskGroups
    TaskGroup_Main
      GameObjects_sync
        Sync_* objects
```

Workspace dragging/rotation must move `GazeboWorkspace`, not only the table mesh. If robot or task objects stay behind when dragging, check parenting and `GazeboWorkspaceMember` usage.

Unity should not use physics to move synchronized task objects. If objects jitter in MR while Gazebo is stable, check for:

- Rigidbody/collider interactions on synced visual objects.
- Multiple duplicate visual objects at the same name/pose.
- Old task objects left outside `GazeboWorkspace/TaskGroups/TaskGroup_Main`.
- Sync rates/protocol mismatch between robot and object visualizers.

After regenerating task profiles or worlds, synchronize the Unity editor hierarchy from the generated profile:

```text
Tools > Gazebo Replica > Rebuild Dual Arm Workspace In Active Scene
```

When Unity MCP is available to Codex, this menu can be triggered with `execute_menu_item`. If MCP tools are not visible, ask the user to restart/refresh the MCP server and rerun tool discovery. Avoid manually editing Unity scene YAML for generated task objects unless there is no alternative.

Recent expected Unity task hierarchy:

```text
GazeboWorkspace
  TaskGroups
    TaskGroup_Main
      GameObjects_sync
        Sync_RedCube
        Sync_GreenCube
        Sync_RedCylinder
        Sync_GreenCylinder
        Sync_Plate_A
        Sync_Plate_B
        Sync_TaskPlatform
        Sync_CableReceiverBox
        Sync_CableReceiverBox_Fixed
        Sync_CableRod
        Sync_CableRod_B
```

## Unity Control Panel

The central panel is intended to replace scattered instruction/debug/camera windows.

Current goals:

- Editor-created, not runtime-only invisible hierarchy.
- Position editable in Unity editor.
- Default RectTransform target requested by user: position `(0.0, 1.7, 1.0)`, rotation `(0, 0, 0)`.
- Header/title remains fixed.
- Page buttons at the bottom.
- Action buttons near the top of each page.
- Current preferred layout is reduced fixed content, not complex scrolling. The code disables stale `ScrollRect` children and uses `FixedContent` under `ContentScrollArea`.
- Drag handle should look like a semi-transparent Meta/Vision-Pro-style rounded dash below the panel.
- Resize handles should be small rounded L-shaped semi-transparent corner notches outside the panel and appear on ray hover.
- Panel should face or tilt toward the headset after drag release, without forcing awkward vertical-only behavior.
- Controls page should keep reset actions simple: left arm reset, right arm reset, object reset, overhead/workspace reset, plus `Swap Hands`.
- Camera page includes Left Wrist, Right Wrist, and Floating camera selection.
- Task page publishes task selections to `/task_manager/select_task`.

If the editor still shows old `teleop_button_instructions` or old debug panels, those are likely stale objects and should be removed once the new panel is confirmed stable.

## Cameras And Recording

Unity-side cameras matter more than Gazebo camera for current recording/UI work.

The Gazebo gripper camera is intentionally disabled/commented out in:

```text
ros_backend1.1/src/ur_hande_description/urdf/ur_hande.urdf.xacro
```

Keep the commented placeholder for later, but do not assume `/gripper_camera/*` topics exist in the current default system.

Current Unity camera goals:

- Wrist/gripper camera for recording.
- Movable floating camera with ray interaction and 3-axis rotation rings.
- Camera visual cone should be semi-transparent light blue, small enough not to dominate the scene or appear in its own camera view.
- Camera page in the control panel supports Left Wrist, Right Wrist, and Floating camera selection.
- Floating camera now has its own runtime `GripperCameraRecorder` via `FloatingSceneCameraController`; record/capture should work for the selected floating camera through the central panel.
- `RecordingPerformanceTraceLogger` publishes recording/FPS evaluation topics when present in the deployed app:
  - `/unity_eval/recording_state`
  - `/unity_eval/fps_sample`
- If recording-FPS tests see no messages on those topics, rebuild/redeploy the Quest app before rerunning `16_recording_fps_sync_latency_trace.py`.

Quest recordings/captures should not be committed. Recent retrieved Quest videos are under ignored local capture folders such as:

```text
.quest_screenshots/QuestVideos/
```

## Haptics State

Current backend Part 4 starts:

```text
teleop_bridge/haptic_feedback/contact_haptic_publisher
```

Unity side includes:

```text
UnityApp/Assets/Scripts/QuestHapticFeedbackController.cs
```

Preferred haptic source is Gazebo contact/proximity state, not EE target-vs-actual gap. The EE-gap method caused misleading vibration when the control loop could not move or when attachment targets were far away.

Current/default haptic behavior:

- Distinct short double pulse when both gripper fingers contact/pinch the object, including empty-close/finger contact when enabled.
- Continuous contact/proximity amplitudes are currently set to `0.0` by lifecycle defaults to avoid constant distracting vibration.
- Servo collision-gap haptics are gated by Servo collision status plus target/actual gap thresholds, not by raw EE error alone.
- Keep haptics independent from the control loop. Haptics should never block or throttle teleop messages.

Lifecycle defaults in `start_dual_part4` include:

```text
DUAL_HAPTIC_RATE_HZ=60.0
pulse_amplitude=0.55
continuous_contact_amplitude=0.0
proximity_amplitude=0.0
collision_gap_amplitude=0.35
collision_gap_activation_threshold_m=0.08
collision_gap_release_threshold_m=0.045
collision_gap_hold_sec=0.25
```

For performance testing, set:

```bash
ENABLE_HAPTICS=0 ./scripts/backend11_lifecycle.sh start_dual_part4
DUAL_HAPTIC_ENABLE_SERVO_GAP=false ./scripts/backend11_lifecycle.sh start_dual_part4
```

## Debugging Checklist

Start with:

```bash
git status --short
cd ros_backend1.1
./scripts/backend11_lifecycle.sh status
```

If Quest input is not reaching ROS:

```bash
adb devices
adb reverse --list
./scripts/backend11_lifecycle.sh wired_status
docker exec motion_planner_11 bash -lc 'tail -n 80 /tmp/qcr.log'
```

If control loop is not moving Gazebo:

```bash
CONTAINER=motion_planner_11
ROS_ENV='source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash'
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic hz /left_arm/received_pose_states"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic hz /right_arm/received_pose_states"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /left_arm/target_twist_states --once"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /right_arm/target_twist_states --once"
docker exec "$CONTAINER" bash -lc 'tail -n 80 /tmp/dual_left_mapper.log'
docker exec "$CONTAINER" bash -lc 'tail -n 80 /tmp/dual_right_mapper.log'
docker exec "$CONTAINER" bash -lc 'tail -n 80 /tmp/servo_dual_gz.log'
```

If Gazebo moves but Unity/MR does not sync:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic list | grep /unity_sync"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /unity_sync/Sync_RedCube_pose --once"
docker exec "$CONTAINER" bash -lc 'tail -n 80 /tmp/dual_part4_tcp_endpoint.log'
docker exec "$CONTAINER" bash -lc 'tail -n 80 /tmp/dual_part4_cube_pose.log'
docker exec "$CONTAINER" bash -lc 'tail -n 80 /tmp/dual_part4_task_manager.log'
```

If Unity app behavior is unclear on Quest:

```bash
adb logcat -d "Unity:I" "*:S" | rg -n "HandPoseSender|NetworkSender|MRCentralControlPanel|WorkspaceDrag|Haptic|MREvaluationTracePublisher|RecordingPerformanceTraceLogger|Error|Exception" -m 160
```

If Unity MCP is available:

```text
Use tool_search for Unity MCP tools.
Check active scene with manage_scene(action="get_active").
Read console with read_console.
Rebuild generated workspace with execute_menu_item("Tools/Gazebo Replica/Rebuild Dual Arm Workspace In Active Scene").
Save with manage_scene(action="save") only after checking the hierarchy/console.
```

If Servo slows heavily or reports collision issues:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh debug_self_collision
docker exec motion_planner_11 bash -lc 'tail -n 120 /tmp/servo_dual_gz.log'
```

If headset input is inconvenient, use the fake hand/debug tools instead of asking the user to wear the headset for every Servo test:

```text
ros_backend1.1/src/teleop_bridge/teleop_bridge/test_tools/debug_hand_generator.py
ros_backend1.1/src/teleop_bridge/teleop_bridge/servo_response_sampler.py
```

Current evaluation scripts live under:

```text
ros_backend1.1/scripts/test_tools/eval_scripts/
ros_backend1.1/scripts/test_tools/performance_test_scripts/
```

Most important current scripts:

```text
13_dynamic_novnc_headed_performance_test.py
14_headset_backend_latency_trace.py
15_mr_sync_visual_latency_trace.py
16_recording_fps_sync_latency_trace.py
run_dynamic_backend_performance_linux.sh
run_dynamic_backend_performance_windows.ps1
```

Unity-side evaluation publishers:

```text
UnityApp/Assets/Scripts/MREvaluationTracePublisher.cs
UnityApp/Assets/Scripts/RecordingPerformanceTraceLogger.cs
UnityApp/Assets/Scripts/QuestMRFeatureBootstrap.cs
```

If the Unity editor cannot build scripts from command line because `project.assets.json` is missing, this usually means the generated Unity `Temp/obj/.../project.assets.json` file is absent outside the editor. It usually does not affect manual editor builds.

## Common Pitfalls

- Do not treat Unity as the physics source. Gazebo owns physics and object poses.
- Do not fix sync mismatch by manually moving only Unity visuals unless the Gazebo profile is also updated.
- Do not leave duplicate task object visuals in the Unity scene; stale visuals look like sync offsets.
- Do not leave old generated task objects outside `GazeboWorkspace/TaskGroups/TaskGroup_Main/GameObjects_sync`.
- Do not edit archived backends unless the user explicitly asks. Work in `ros_backend1.1` now.
- Do not assume root docs are fully current; cross-check with `ros_backend1.1/Readme/` and lifecycle scripts.
- Do not use destructive git commands like `git reset --hard` or `git checkout --` without explicit user approval.
- Do not revert local Unity scene changes unless explicitly asked.
- Do not commit local Quest videos/screenshots, Unity caches, or thesis/private dev notes unless the user explicitly asks.

## Current Open/Recent Issues To Be Aware Of

These are areas the user has recently been working on:

- Coupled gripper is now preferred/default, with matching open/close speed, but contact stability may still need tuning.
- Unity object-vs-gripper visual offsets should be debugged by comparing live Gazebo poses/joint states against Unity visual transforms/logs, not just eyeballing screenshots.
- Control panel UI is still being refined; scroll/drag/resize and page layout have been recurring pain points.
- Workspace drag rings/rectangle should appear when controller ray is visible and support ray hit markers.
- Movable camera should have ray hit marker and 3-axis rotation rings.
- Current default task combines pick/place and cable insertion objects on one platform.
- Runtime task selection exists, but use the combined default task for simpler demos unless specifically testing task switching.
- Rubik 2x2 is currently a backend kinematic mechanism, not a passive physical cube twistable purely by gripper contact.
- Self-collision checking can make Servo crawl if thresholds/SRDF collision matrix are too conservative. Prefer using the inspector and SRDF/collision-matrix fixes over disabling all collision checking.
- Headless Gazebo is preferred for performance/RTF; use headed/noVNC only for visual debugging.

## Where To Put New Documentation

- Stable user docs: `docs/`
- Backend-specific docs: `ros_backend1.1/Readme/`
- Development/debug notes: `dev_notes/`
- Agent handoff/context: this file, `AGENTS.md`

If you add a long-running architectural decision, put the stable version in `docs/Technical_Details.md` and the rough working notes in `dev_notes/`.
