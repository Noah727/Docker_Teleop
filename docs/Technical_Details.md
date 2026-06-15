# Technical Details

This document describes the current architecture, runtime data flow, configuration files, and extension points for the mixed-reality dual-arm teleoperation platform. It is written against the maintained `ros_backend1.1` backend and `GazeboReplica_DualArm_MR.unity` Unity scene.

For installation and replication, use [System_Setup.md](System_Setup.md). For day-to-day commands, use [Getting_Started.md](Getting_Started.md).

## Canonical Runtime

| Component | Current Value |
| --- | --- |
| Backend | `ros_backend1.1` |
| Container | `motion_planner_11` |
| Lifecycle script | `ros_backend1.1/scripts/backend11_lifecycle.sh` |
| Unity active scene | `UnityApp/Assets/Scenes/GazeboReplica_DualArm_MR.unity` |
| Single-arm backup scene | `UnityApp/Assets/Scenes/GazeboReplica_MR.unity` |
| Active scene profile pointer | `ros_backend1.1/profiles/active_scene_profile.txt` |
| Generated Gazebo world | `ros_backend1.1/simulation/worlds/ur_hande_dual_arm_tabletop.sdf` |
| Generated Unity task profile | `UnityApp/Assets/Resources/TaskProfiles/active_task.json` |
| Preferred simulation mode | Headless Gazebo |
| Preferred Quest link | USB wired mode through `adb reverse` |

## Architecture Summary

The system is split into a headset-side Unity frontend and a Dockerized ROS/Gazebo backend.

Unity responsibilities:

- Read Quest controller poses and buttons.
- Send controller state to the backend over TCP.
- Display Quest passthrough, robot visuals, synchronized task objects, control panels, and camera previews.
- Let the operator move and rotate the mixed-reality workspace.
- Record wrist-camera and floating-camera image data from the Unity side.

ROS/Gazebo responsibilities:

- Run the dual UR5e + Robotiq Hand-E simulation.
- Receive and decode Quest controller packets.
- Map controller motion to robot target twist commands.
- Run MoveIt Servo and gripper controllers.
- Publish joint state, task-object poses, haptic/contact events, and task manifests.
- Provide reset, task layout, and synchronization services.

Gazebo is the physics authority. Unity synchronized task objects are visual followers and should not run independent physics for robot/task state.

## Runtime Data Flow

Controller motion:

```text
Quest Unity controller pose/buttons
  -> TCP receiver: receiver/quest_controller_receiver
  -> /left_arm/received_pose_states
  -> /right_arm/received_pose_states
  -> teleop_bridge/mapping/hand_pose_mapper
  -> /left_arm/target_twist_states
  -> /right_arm/target_twist_states
  -> teleop_bridge/servo_bridge/servo_command_bridge
  -> MoveIt Servo delta_twist_cmds
  -> Gazebo controllers
```

Gripper command:

```text
Quest trigger state
  -> TargetTwistStates gripper fields
  -> teleop_bridge/gripper_control/coupled_gripper_controller
  -> /left_hande_position_controller/commands
  -> /right_hande_position_controller/commands
  -> Gazebo Hand-E finger joints
```

Unity synchronization:

```text
Gazebo model poses and /joint_states
  -> task_pose_sync_publisher and ROS/Gazebo bridge
  -> /unity_sync/<object>_pose
  -> ros_tcp_endpoint
  -> Unity SceneObjectPoseSyncManager / GazeboPoseStampedSubscriber
```

Task layout:

```text
runtime_task_manager
  -> /task_manager/status
  -> /task_manager/active_task_manifest
  -> Unity task-profile scene builder
  -> GazeboWorkspace/TaskGroups/TaskGroup_Main
```

Haptics:

```text
Gazebo contact/proximity/collision state
  -> contact_haptic_publisher
  -> ROS-TCP Endpoint
  -> Unity QuestHapticFeedbackController
  -> Quest controller vibration
```

## Network Model

Wired mode is the maintained default. In wired mode, the Quest app connects to `127.0.0.1` inside the headset. ADB reverse tunnels forward those headset-local connections to the host, and Docker forwards them into the container.

| Channel | Quest Target | Host Port | Container Port | Purpose |
| --- | --- | --- | --- | --- |
| Controller TCP | `127.0.0.1:5026` | `127.0.0.1:5026` | `5005` | Quest controller pose/buttons into ROS |
| ROS-TCP | `127.0.0.1:10001` | `127.0.0.1:10001` | `10000` | ROS topics/services between backend and Unity |

Verification:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh wired_status
adb reverse --list
docker port motion_planner_11
```

Expected ADB reverse entries:

```text
tcp:5026 tcp:5026
tcp:10001 tcp:10001
```

Wireless mode can be used for untethered experiments, but it requires stable LAN routing, host firewall access on ports `5026` and `10001`, and Unity connection settings pointed at the host LAN IP. Wired mode should be used for repeatable evaluation unless wireless operation is the item being tested.

## Backend Lifecycle

Normal dual-arm bringup:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh bringup_dual
./scripts/backend11_lifecycle.sh status
```

Clean restart:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh safe_down
./scripts/backend11_lifecycle.sh bringup_dual
```

Stepwise bringup:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh up_container
./scripts/backend11_lifecycle.sh wired_on
./scripts/backend11_lifecycle.sh build_ws
./scripts/backend11_lifecycle.sh generate_dual_world
./scripts/backend11_lifecycle.sh start_dual_sim
./scripts/backend11_lifecycle.sh start_dual_servo
./scripts/backend11_lifecycle.sh start_receiver
./scripts/backend11_lifecycle.sh start_dual_part23
./scripts/backend11_lifecycle.sh start_dual_part4
./scripts/backend11_lifecycle.sh status
```

Headed Gazebo/noVNC should be reserved for visual simulator debugging:

```bash
cd ros_backend1.1
SIM_HEADLESS=0 ./scripts/backend11_lifecycle.sh start_dual_sim
```

## Main Runtime Nodes

| Node | Role |
| --- | --- |
| `quest_controller_receiver` | TCP receiver for Quest controller packets. |
| `hand_pose_mapper` | Maps controller deltas, rotation mode, attachment mode, gripper intent, and reset intent into target twist state. |
| `servo_command_bridge` | Converts target twist state into MoveIt Servo commands. |
| `coupled_gripper_controller` | Publishes coupled Hand-E finger commands from gripper intent. |
| `reset_manager` | Handles arm home motion, object reset, and Servo restart support. |
| `task_pose_sync_publisher` | Publishes Gazebo task-object poses for Unity. |
| `runtime_task_manager` | Publishes task manifests and handles task selection/reset services. |
| `contact_haptic_publisher` | Publishes haptic events from contact/proximity/collision state. |
| `ros_tcp_endpoint` | Bridges ROS topics/services to Unity ROS-TCP Connector. |

## Unity Scene Architecture

The active scene keeps the simulated workspace under a single root:

```text
GazeboWorkspace
  Gazebo_Table
  robots / robot visuals
  TaskGroups
    TaskGroup_Main
      GameObjects_sync
        Sync_* objects
```

Workspace movement should transform `GazeboWorkspace`, not individual robot or task visuals. Task objects synchronized from Gazebo should remain visual-only in Unity.

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
UnityApp/Assets/Scripts/RobotViewpointController.cs
```

When a task profile or generated world changes, synchronize the Unity editor hierarchy with the generated task profile:

```text
Tools > Gazebo Replica > Rebuild Dual Arm Workspace In Active Scene
```

## Control Model

Default controller assignment:

| Controller Input | Left Arm | Right Arm |
| --- | --- | --- |
| Grip hold | Engage left teleop | Engage right teleop |
| Trigger tap | Toggle left gripper | Toggle right gripper |
| `X` / `A` hold | Left rotation mode | Right rotation mode |
| `Y` / `B` tap | Left attachment mode | Right attachment mode |

The central control panel owns shared operations such as reset, camera selection, recording, task status, debug status, haptic controls, and `Swap Hands`.

Grip engagement is per arm. A combined single-grip fallback should be avoided unless explicitly documented for a temporary debug path.

## Motion Mapping And Tuning

Dual-arm tuning files:

```text
ros_backend1.1/src/teleop_bridge/config/teleop_tuning_dual_left.yaml
ros_backend1.1/src/teleop_bridge/config/teleop_tuning_dual_right.yaml
```

Core mapping parameters:

| Parameter | Purpose |
| --- | --- |
| `map_axes`, `map_signs` | Maps Unity/controller displacement into robot base-frame motion. |
| `rot_map_axes`, `rot_map_signs` | Maps controller delta rotation into tool rotation. |
| `scale_xyz` | Linear hand-motion gain. |
| `kp_linear`, `max_linear_speed` | End-effector positional chasing responsiveness. |
| `kp_angular`, `max_angular_speed` | End-effector rotational responsiveness. |
| `attachment_*` | Attachment-mode frame and tool-offset calibration. |
| `gamepad_*` | Optional thumbstick/gamepad control parameters. |

The current dual-arm configuration uses boosted responsiveness for headset operation:

```yaml
kp_linear: 6.0
max_linear_speed: 0.85
max_angular_speed: 6.0
attachment_max_angular_speed: 3.5
```

After editing tuning files or Python source:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh build_ws
./scripts/backend11_lifecycle.sh start_dual_part23
./scripts/backend11_lifecycle.sh status
```

## Gripper Control

The default dual-arm gripper path uses the coupled gripper controller:

```text
ros_backend1.1/src/teleop_bridge/teleop_bridge/gripper_control/coupled_gripper_controller.py
```

The coupled controller publishes one shared aperture target to both Hand-E fingers. This better matches the real Hand-E mechanism and reduces asymmetric one-finger behavior during contact.

The lifecycle default is:

```bash
DUAL_GRIPPER_CONTROLLER=coupled
```

Main gripper tuning values live in the dual tuning YAML files. Important fields include:

```yaml
speed_m_per_s
close_speed_m_per_s
open_speed_m_per_s
```

## Task Profile System

Profiles separate workspace, robot, control, task, and scene configuration:

```text
ros_backend1.1/profiles/
  workspaces/dual_arm_tabletop/workspace.yaml
  robots/ur5e_hande_dual/robot.yaml
  controls/quest_dual_arm.yaml
  tasks/pick_place_basic/task.yaml
  tasks/cable_insertion/task.yaml
  scenes/dual_arm_tabletop/scene.yaml
  scenes/dual_arm_cable_insertion/scene.yaml
```

Generate the active Gazebo world and Unity task profile:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh generate_dual_world
```

List or select task profiles:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh list_tasks
./scripts/backend11_lifecycle.sh select_task pick_place_basic
./scripts/backend11_lifecycle.sh select_task cable_insertion
```

The current default `pick_place_basic` profile includes the main combined evaluation objects: cubes, cylinders, plates, task platform, cable rods, and insertion boxes. This allows most demos and evaluations to run without changing task profiles.

Runtime task switching exists through `runtime_task_manager`, but the lifecycle `select_task` path is the more stable path for repeatable setup.

## Cameras And Recording

The maintained camera path is Unity-side, not Gazebo-side.

Unity camera scripts:

```text
UnityApp/Assets/Scripts/GripperCameraRecorder.cs
UnityApp/Assets/Scripts/FloatingSceneCameraController.cs
```

Current preview sources:

- Left wrist camera.
- Right wrist camera.
- Floating scene camera.

Quest recording path:

```text
/storage/emulated/0/Android/data/com.noahli.ROSUNITY/files/GripperCameraRecordings
```

Pull recordings:

```bash
adb pull "/storage/emulated/0/Android/data/com.noahli.ROSUNITY/files/GripperCameraRecordings" ./GripperCameraRecordings
```

The Gazebo gripper camera is disabled in the default robot description. `/gripper_camera/image_raw` and `/gripper_camera/camera_info` are not expected runtime topics.

## Haptics

The current haptic path is contact-oriented. Backend Part 4 can start:

```text
teleop_bridge/haptic_feedback/contact_haptic_publisher
```

Unity receives haptic events through:

```text
UnityApp/Assets/Scripts/QuestHapticFeedbackController.cs
```

Default behavior:

- A short pulse can indicate finger/object pinch or contact.
- Continuous contact/proximity amplitudes are disabled by default to avoid distracting vibration.
- Servo collision-gap haptics are gated by collision status and target/actual gap thresholds.
- Haptic logic must not block the teleoperation control loop.

For performance testing, haptics can be disabled when starting Part 4:

```bash
cd ros_backend1.1
ENABLE_HAPTICS=0 ./scripts/backend11_lifecycle.sh start_dual_part4
```

## Evaluation And Local Data Policy

Raw recordings, trial rows, trace logs, and large evaluation outputs are local-only research artifacts. They should not be committed to the public repository.

Typical local output roots:

```text
thesis_eval_raw/
complete_material/
GripperCameraRecordings/
```

Curated public-facing results should be converted into lightweight plots, tables, captions, preview images, or hosted video links before being referenced from the repository.

## Troubleshooting Reference

Backend status:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh status
```

Quest input path:

```bash
adb devices
adb reverse --list
cd ros_backend1.1
./scripts/backend11_lifecycle.sh wired_status
docker exec motion_planner_11 bash -lc 'tail -n 80 /tmp/qcr.log'
```

Control-loop topics:

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

Unity sync path:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic list | grep /unity_sync"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /unity_sync/Sync_RedCube_pose --once"
docker exec "$CONTAINER" bash -lc 'tail -n 80 /tmp/dual_part4_tcp_endpoint.log'
docker exec "$CONTAINER" bash -lc 'tail -n 80 /tmp/dual_part4_task_manager.log'
```

Quest Unity logs:

```bash
adb logcat -d "Unity:I" "*:S" | rg -n "HandPoseSender|NetworkSender|MRCentralControlPanel|WorkspaceDrag|Haptic|RecordingPerformanceTraceLogger|Error|Exception" -m 160
```

## Extension Checklist

Adding a task:

1. Define task objects in a task profile under `ros_backend1.1/profiles/tasks/`.
2. Reference the task profile from a scene profile under `ros_backend1.1/profiles/scenes/`.
3. Run `generate_dual_world`.
4. Rebuild the Unity workspace from the generated active task profile.
5. Verify Gazebo poses, `/unity_sync/*` topics, and Unity visual hierarchy.

Adding a robot:

1. Add or replace the robot description, meshes, inertial/collision data, and ros2_control configuration.
2. Add or update MoveIt configuration and Servo command topics.
3. Define base/tool frames and home positions in a robot profile.
4. Update Unity robot visualization and joint-name mapping.
5. Retune controller-to-robot mapping parameters.
6. Validate `/joint_states`, TF, Servo commands, gripper commands, and Unity visual sync before headset operation.

Adding a gripper:

1. Define open/close command semantics.
2. Identify actual gripper joint/state feedback.
3. Implement or adapt a gripper command controller.
4. Update Unity finger visualization.
5. Prefer real contact state for haptics when available.
