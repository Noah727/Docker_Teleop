# Technical Details

This file collects the deeper project notes that are useful after the basic setup and run path are working. Start with `docs/System_Setup.md` for replication and `docs/Getting_Started.md` for daily operation.

The sections below consolidate the previous separate docs for architecture, Unity build details, wired/wireless networking, world-frame mapping, tuning, camera/recording, troubleshooting, and GitHub setup.

---


## Architecture

# Architecture

## Data Flow

```text
Meta Quest / Unity
  HandPoseSender.cs
    TCP JSON packets
      |
      v
ROS backend container
  quest_controller_receiver
    /received_pose_states
      |
      v
  received_pose_to_target_twist
    /target_twist_states
      |
      +--> target_twist_to_servo_cmd --> MoveIt Servo --> Gazebo robot arm
      |
      +--> target_twist_to_gripper_cmd --> Hand-E position controller
      |
      +--> target_twist_reset_manager --> reset / home / scene object reset

Gazebo / ROS state
  /joint_states, /tf, object poses, camera topics
      |
      v
Unity ROS-TCP-Connector
  Ur5eTrajectorySubscriber.cs
  SceneObjectPoseSyncManager.cs
  GazeboPoseStampedSubscriber.cs
```

## Unity Responsibilities

- Read headset/controller pose and controller buttons.
- Send controller state to ROS over TCP.
- Visualize robot joint states from ROS.
- Visualize synced table objects from Gazebo poses.
- Provide a wrist-camera preview and frame recording interface.
- Provide user-facing control panel and scene decorations.

Unity should be treated mostly as a visualizer and UI layer. Robot/object physics should come from Gazebo.

## ROS Backend Responsibilities

- Run Gazebo simulation.
- Run UR5e + Hand-E robot description and controllers.
- Receive Unity TCP controller data.
- Map controller input to target twist commands.
- Run MoveIt Servo and gripper command bridges.
- Publish Gazebo object poses back to Unity.
- Reset robot and scene objects.

## Main Runtime Nodes

- `quest_controller_receiver`: TCP JSON receiver from Unity.
- `received_pose_to_target_twist`: hand/gamepad mapping and mode logic.
- `target_twist_to_servo_cmd`: converts target twist state into servo commands.
- `target_twist_to_gripper_cmd`: converts gripper intent into Hand-E position commands.
- `target_twist_reset_manager`: B-button reset, home motion, object reset.
- `ros_tcp_endpoint`: Unity ROS-TCP bridge server.

## Network Ports

- Unity controller TCP to backend: host `127.0.0.1:5026` in wired mode.
- Unity ROS-TCP-Connector to backend: host `127.0.0.1:10001` in wired mode.
- Container controller listener: `5005`.
- Container ROS-TCP endpoint: `10000`.


---

## Unity Quest Build

# Unity Quest 3 Rebuild Guide

This guide explains how another developer can clone the repository, open the Unity project, rebuild the Quest 3 app, and run it against the ROS backend.

## Repository Size Notes

The full local `UnityApp/` folder can become several GB after Unity opens it because Unity creates generated folders such as `Library/`, `Temp/`, and local build outputs.

The pushed Git-tracked Unity content is much smaller:

```text
Tracked UnityApp files: about 94 MB
UnityApp Git LFS files: about 91 MB
```

Do not commit generated Unity folders or APK builds. They are ignored by `.gitignore`.

## Requirements

- Unity Hub.
- Unity `6000.2.10f1`.
- Unity Android Build Support module.
- Unity Android SDK & NDK Tools module.
- Unity OpenJDK module.
- Meta Quest 3 with Developer Mode enabled.
- `adb` available on the host machine.
- Git LFS.
- Docker backend from `ros_backend1.0/`.

## Clone The Project

Use SSH if you have GitHub SSH access:

```bash
git lfs install
git clone git@github.com:su-idr-lab/ros_unity_project.git
cd ros_unity_project
git lfs pull
```

If Git LFS is not installed or `git lfs pull` is skipped, some meshes/textures may appear as tiny pointer files and Unity imports will be broken.

## Open In Unity

1. Open Unity Hub.
2. Select `Add project from disk`.
3. Choose:

```text
ros_unity_project/UnityApp
```

4. Open with Unity `6000.2.10f1`.
5. Let Unity restore packages and rebuild the local `Library/` folder.

The first import can take a while. This is normal and should not be committed.

## Important Project Settings

Current Unity project identity:

```text
Company: NoahLi
Product: HandTrackingUnity
Android package ID: com.noahli.handtrackingunity
```

Current active build scene:

```text
Assets/Scenes/Ur5e_Working 1.unity
```

Current build profile asset:

```text
Assets/Settings/Build Profiles/Quest3_4.2.asset
```

## Build For Quest 3

1. Connect the Quest 3 by USB.
2. Accept the headset USB debugging prompt.
3. Confirm the device is visible:

```bash
adb devices
```

Expected output should include a connected device, not `unauthorized`.

4. In Unity, open:

```text
File > Build Profiles
```

5. Select Android / Quest build profile.
6. Confirm the active scene includes:

```text
Assets/Scenes/Ur5e_Working 1.unity
```

7. Use `Build And Run` to install directly to the Quest.

## Backend For Wired Quest Mode

For the usual wired development mode, start the backend before running the app:

```bash
cd ros_backend1.0
cp .env.example .env
./scripts/backend10_lifecycle.sh bringup_wired
```

Wired mode uses `adb reverse`, so the Quest app connects to its own loopback address:

```text
Quest app target: 127.0.0.1:5026
Unity ROS-TCP target: 127.0.0.1:10001
```

The USB tunnel forwards those connections back to the host backend.

## Runtime Verification

Use logs when testing a deployed Quest build:

```bash
adb logcat -c
adb logcat Unity:I '*:S'
```

Useful one-shot capture after reproducing a bug:

```bash
adb logcat -d Unity:I '*:S' > quest_unity_log.txt
```

Check the backend separately:

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh status
```

## Recording Data Path On Quest

The app package ID is:

```text
com.noahli.handtrackingunity
```

Wrist-camera recordings are expected under:

```text
/storage/emulated/0/Android/data/com.noahli.handtrackingunity/files/GripperCameraRecordings
```

Pull recordings to the host:

```bash
adb pull "/storage/emulated/0/Android/data/com.noahli.handtrackingunity/files/GripperCameraRecordings" ./GripperCameraRecordings
```

## Common Problems

- Missing robot meshes or textures: run `git lfs pull`.
- Unity opens with the wrong version: install/use Unity `6000.2.10f1`.
- Android build unavailable: install Android Build Support, SDK/NDK Tools, and OpenJDK through Unity Hub.
- `adb devices` shows `unauthorized`: accept the USB debugging prompt inside the headset.
- App cannot connect in wired mode: restart `bringup_wired` and confirm the Quest is connected by USB.
- Robot does not move: confirm backend status, Quest logs, and controller state in the in-app panel.
- Recording path looks empty: make sure the currently installed app uses package ID `com.noahli.handtrackingunity`.

## Development Workflow

Keep `main` stable. Use feature branches for app changes:

```bash
git checkout -b feature/my-unity-change
```

After testing:

```bash
git add UnityApp docs
git commit -m "Describe Unity change"
git push -u origin feature/my-unity-change
```

Merge back to `main` only after the Quest build is tested.


---

## Wired And Wireless Setup

# Wired / Wireless Setup (ros_backend1.0)

This guide explains both connection modes for the Quest/Unity app.

There are 2 TCP channels in this project:

1. Hand pose stream
- Unity `HandPoseSender`
- Quest/Unity -> backend receiver
- default port: `5026` on host -> `5005` in container

2. Unity ROS-TCP connector
- Unity `ROSConnection`
- Unity subscribers/publishers for joint states, cube sync, TF, etc.
- default port: `10001` on host -> `10000` in container

## Quick recommendation

- Use `wired` when you want the easiest and most stable Quest connection.
- Use `wireless` when you want the headset untethered.

## Ports used

- Hand TCP: host `5026` -> container `5005`
- ROS-TCP: host `10001` -> container `10000`

## Backend mode commands

From `ros_backend1.0`:

```bash
./scripts/backend10_lifecycle.sh mode_wired
./scripts/backend10_lifecycle.sh mode_wireless
./scripts/backend10_lifecycle.sh mode_status
```

After changing mode, restart the container so Docker applies the new port bindings:

```bash
./scripts/backend10_lifecycle.sh restart_container
```

## Wired mode

### How it works

Wired mode uses `adb reverse` over USB.

Path for hand data:

`Quest 127.0.0.1:5026`
-> `adb reverse`
-> `host 127.0.0.1:5026`
-> `Docker`
-> `container 5005`

Path for Unity ROS-TCP:

`Quest 127.0.0.1:10001`
-> `adb reverse`
-> `host 127.0.0.1:10001`
-> `Docker`
-> `container 10000`

### One-time host setup

Install `adb`.

Ubuntu:

```bash
sudo apt update
sudo apt install -y adb
```

macOS with Homebrew:

```bash
brew install android-platform-tools
```

Verify:

```bash
which adb
adb version
```

### Quest setup

- enable Developer Mode
- connect headset by USB
- accept USB debugging prompt in headset

Verify:

```bash
adb devices
```

Expected:

```text
List of devices attached
<serial>    device
```

### Switch backend to wired mode

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh mode_wired
./scripts/backend10_lifecycle.sh restart_container
```

### Enable the USB tunnels

```bash
./scripts/backend10_lifecycle.sh wired_on
./scripts/backend10_lifecycle.sh wired_status
```

Expected `adb reverse --list` entries:

```text
tcp:5026 tcp:5026
tcp:10001 tcp:10001
```

### Unity settings for wired mode

`HandPoseSender`:
- `targetIP = 127.0.0.1`
- `targetPort = 5026`

`ROS Settings` / `ROSConnection`:
- `ROS IP Address = 127.0.0.1`
- `ROS Port = 10001`

Current defaults in the project already match wired mode:
- `UnityApp/Assets/Scripts/HandPoseSender.cs`
- `UnityApp/Assets/Resources/ROSConnectionPrefab.prefab`

Rebuild and deploy the Quest app after changing connection settings.

### Bringup for wired mode

Fast path:

```bash
./scripts/backend10_lifecycle.sh bringup_wired
```

Or step by step:

```bash
./scripts/backend10_lifecycle.sh up_container
./scripts/backend10_lifecycle.sh wired_on
./scripts/backend10_lifecycle.sh build_ws
./scripts/backend10_lifecycle.sh start_sim
./scripts/backend10_lifecycle.sh start_servo
./scripts/backend10_lifecycle.sh start_receiver
./scripts/backend10_lifecycle.sh start_part23
./scripts/backend10_lifecycle.sh start_part4
```

## Wireless mode

### How it works

Wireless mode uses your LAN/Wi-Fi network.

Path for hand data:

`Quest <HostLANIP>:5026`
-> `host LAN interface`
-> `Docker`
-> `container 5005`

Path for Unity ROS-TCP:

`Quest <HostLANIP>:10001`
-> `host LAN interface`
-> `Docker`
-> `container 10000`

### Switch backend to wireless mode

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh mode_wireless
./scripts/backend10_lifecycle.sh restart_container
./scripts/backend10_lifecycle.sh wired_off
```

The `wired_off` step removes any old USB reverse rules so they do not confuse testing.

### Find the host LAN IP

macOS:

```bash
ipconfig getifaddr en0
```

Ubuntu:

```bash
hostname -I
```

Use the LAN IP address that the Quest can reach, for example `192.168.1.199`.

### Unity settings for wireless mode

`HandPoseSender`:
- `targetIP = <HostLANIP>`
- `targetPort = 5026`

`ROS Settings` / `ROSConnection`:
- `ROS IP Address = <HostLANIP>`
- `ROS Port = 10001`

### Notes for wireless mode

- Quest and host must be on the same network
- host firewall must allow inbound TCP on `5026` and `10001`
- IP can change when you move networks

### Bringup for wireless mode

```bash
./scripts/backend10_lifecycle.sh bringup_all
```

## How to switch between modes

### Wired -> Wireless

1. Switch backend bind mode:

```bash
./scripts/backend10_lifecycle.sh mode_wireless
./scripts/backend10_lifecycle.sh restart_container
```

2. Remove USB tunnels:

```bash
./scripts/backend10_lifecycle.sh wired_off
```

3. In Unity / Quest app:
- change `HandPoseSender.targetIP` to `<HostLANIP>`
- change `ROS IP Address` to `<HostLANIP>`
- keep ports `5026` and `10001`
- rebuild and redeploy app

### Wireless -> Wired

1. Switch backend bind mode:

```bash
./scripts/backend10_lifecycle.sh mode_wired
./scripts/backend10_lifecycle.sh restart_container
```

2. Connect Quest by USB and enable reverse tunnels:

```bash
./scripts/backend10_lifecycle.sh wired_on
./scripts/backend10_lifecycle.sh wired_status
```

3. In Unity / Quest app:
- set `HandPoseSender.targetIP = 127.0.0.1`
- set `ROS IP Address = 127.0.0.1`
- keep ports `5026` and `10001`
- rebuild and redeploy app

## Verification checklist

### Verify backend mode

```bash
./scripts/backend10_lifecycle.sh mode_status
```

### Verify hand receiver

```bash
docker exec motion_planner_10 bash -lc "tail -f /tmp/qcr.log"
```

### Verify hand topic

```bash
docker exec motion_planner_10 bash -lc "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && ros2 topic echo /received_pose_states"
```

### Verify ROS-TCP bridge port

```bash
docker port motion_planner_10
```

### Verify wired ADB state

```bash
adb devices
adb reverse --list
```

## Common issues

### `adb devices` shows nothing

- check USB cable
- check Quest Developer Mode
- accept USB debugging prompt
- on Ubuntu, you may need udev rules

Useful commands:

```bash
adb kill-server
adb start-server
adb devices
```

Ubuntu helper package:

```bash
sudo apt install -y android-sdk-platform-tools-common
sudo udevadm control --reload-rules
sudo udevadm trigger
```

### Wireless mode connects unreliably

Typical causes:
- wrong host LAN IP
- Quest on different Wi-Fi/VLAN
- host firewall blocking `5026` or `10001`

### Wired mode hand data works but ROS sync does not

Check that both reverse rules exist:

```bash
adb reverse --list
```

You need both:
- `tcp:5026 tcp:5026`
- `tcp:10001 tcp:10001`


---

## World Frame Mapping

# World-Frame Hand Mapping (ros_backend1.0)

## Goal

`ros_backend1.0` changes hand-pose teleop from absolute pose mapping to world-delta mapping.

Old `0.8` behavior:

```text
controller pose relative to headset -> axis map/scale/offset -> absolute EE target in base_link
```

New `1.0` default behavior:

```text
controller pose in Unity world -> mapped controller displacement -> EE displacement in base_link
```

This makes the robot move in the same direction as the controller motion, as long as the Unity world/table axes are mapped correctly to the robot base axes.

## Important Unity Setting

In the active Unity scene `Ur5e_Working 1`, `HandPoseSender.sendRelativeToHeadset` should be disabled.

The scene file currently has:

```yaml
sendRelativeToHeadset: 0
```

That means `HandPoseSender` sends the controller pose in Unity world coordinates instead of headset-local coordinates.

If this is accidentally enabled, the backend will receive headset-relative motion again, and moving your head can change the controller frame. That is exactly what `0.9` is trying to avoid.

## Runtime Formula

When hand-pose mode starts and valid TF is available, the mapper captures:

```text
hand_ref = mapped current controller world position
EE_ref   = current tool0 position in base_link
```

Then every frame:

```text
hand_delta = mapped_current_hand - hand_ref
EE_target  = EE_ref + hand_delta * scale_xyz + offset_xyz
EE_target  = clamp(EE_target, min_xyz, max_xyz)
twist      = kp_linear * (EE_target - current_EE)
```

The node still publishes velocity, not direct joint positions. MoveIt Servo receives a twist command and moves the arm toward that target.

## Why Delta Instead Of Absolute

Absolute mapping only works cleanly if Unity world coordinates are numerically calibrated into the robot workspace. That requires a carefully tuned offset and can jump when tracking starts.

World-delta mapping is more practical for headset control:

- It does not require the controller's absolute Unity position to equal a robot workspace coordinate.
- It captures the robot's current EE position when control starts.
- It maps hand movement direction to robot movement direction.
- `scale_xyz` becomes an intuitive motion gain.

## Parameters

Config file:

```bash
ros_backend1.0/src/teleop_bridge/config/teleop_tuning.yaml
```

Default mode:

```yaml
position_mapping_mode: world_delta
```

Useful live commands:

```bash
cd ros_backend1.0
CONTAINER=motion_planner_10
ROS_ENV='source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash'

docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist position_mapping_mode"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist position_mapping_mode world_delta"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist position_mapping_mode absolute"
```

Tune direction mapping:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist map_axes \"['z','x','y']\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist map_signs \"[1.0,-1.0,1.0]\""
```

Tune movement scale:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist scale_xyz \"[1.0,1.0,1.0]\""
```

For world-delta mode, keep offset zero unless you intentionally want a constant target bias:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist offset_xyz \"[0.0,0.0,0.0]\""
```

## When The Reference Resets

The world-delta reference is recaptured when:

- tracking is lost and regained
- teleop is engaged with right grip
- you switch between hand-pose mode and gamepad mode with left `Y`
- reset mode is toggled with right `B`; while reset is active, the pending hand reference keeps refreshing from the current tracked hand pose
- the right thumbstick is held and released; arm motion pauses while held, then the release pose becomes the new hand reference without homing the robot or resetting scene objects
- mapper frame/mapping/scale/offset parameters are changed live
- the node restarts

This avoids sudden jumps after mode changes.

## Rotation

Rotation control remains delta-based. Holding right-controller `A` captures the current controller orientation and current EE orientation, then maps controller delta rotation into EE delta rotation using:

```yaml
rot_map_axes
rot_map_signs
rot_scale_xyz
```

So position and rotation now have separate tuning parameters, but both are still delta-style controls.


---

## Mapping Tuning

# Mapping + Reset Tuning Commands (ros_backend1.0)

This file contains the commands to tune mapping and reset behavior.

## 0) Setup

```bash
cd ros_backend1.0
CONTAINER=motion_planner_10
ROS_ENV='source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash'
```

## 1) Ensure nodes are up

```bash
./scripts/backend10_lifecycle.sh status
```

If mapper/reset nodes are missing:

```bash
./scripts/backend10_lifecycle.sh start_part23
```

## 2) Read current mapper/reset parameters

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist map_axes"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist map_signs"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist position_mapping_mode"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist rot_map_axes"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist rot_map_signs"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist rot_scale_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist control_mode"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist start_in_gamepad_mode"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist gamepad_deadband"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist gamepad_linear_speed_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist gamepad_linear_sign_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist gamepad_angular_speed_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist gamepad_angular_sign_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /keyboard_servo_cmd linear_speed_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /keyboard_servo_cmd linear_sign_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /keyboard_servo_cmd angular_speed_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /keyboard_servo_cmd angular_sign_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /keyboard_servo_cmd key_timeout_sec"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist scale_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist offset_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist min_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist max_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist kp_linear"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist kp_angular"

docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /target_twist_reset_manager home_joint_positions"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /target_twist_reset_manager kick_velocities"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /target_twist_reset_manager kick_duration_sec"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /target_twist_reset_manager scene_reset_lift_z"
```

## 3) Live mapper tuning (no restart)

Position mapping mode:

```bash
# New 1.0 behavior: hand/controller movement is interpreted as a world-frame displacement.
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist position_mapping_mode world_delta"

# Legacy 0.8 behavior: incoming hand/controller position maps directly to an absolute robot target.
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist position_mapping_mode absolute"
```

World-delta mode requires Unity to send world pose:

- In `Ur5e_Working 1`, `HandPoseSender.sendRelativeToHeadset` should be unchecked/false.
- When world-delta starts, the mapper captures the current controller pose and current `tool0` pose.
- The target is then `EE_ref + mapped(controller_world - controller_ref) * scale_xyz + offset_xyz`.
- Pressing right-controller `B` requests a recenter. While reset is active, the pending hand reference follows the current tracked hand pose, so the hand's current position becomes the new zero point when hand-pose control resumes.
- Holding the right thumbstick acts like a clutch: arm motion is paused while held, and the hand pose at release becomes the new position reference. This does not toggle robot/object reset.
- Keep `offset_xyz` at `[0.0,0.0,0.0]` unless you intentionally want a constant bias.

Axis map/sign:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist map_axes \"['z','x','y']\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist map_signs \"[1.0,-1.0,1.0]\""
```

Rotation axis map/sign/scale:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist rot_map_axes \"['z','x','y']\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist rot_map_signs \"[1.0,-1.0,1.0]\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist rot_scale_xyz \"[1.0,1.0,1.0]\""
```

Control mode:

```bash
# Hand/controller pose drives EE position.
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist control_mode hand_pose"

# Left stick + left trigger/grip drives EE position.
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist control_mode gamepad"
```

Gamepad mode defaults/tuning:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist start_in_gamepad_mode false"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist gamepad_deadband 0.15"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist gamepad_linear_speed_xyz \"[0.20,0.20,0.20]\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist gamepad_linear_sign_xyz \"[1.0,-1.0,1.0]\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist gamepad_angular_speed_xyz \"[0.0,0.75,0.75]\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist gamepad_angular_sign_xyz \"[1.0,1.0,-1.0]\""
```

Control-mode notes:

- Left controller `Y` toggles between `hand_pose` and `gamepad` mode.
- In `gamepad` mode:
  - left stick `Y` drives robot `+/-X` (forward/back)
  - left stick `X` drives robot `-/+Y` (right/left with the default sign setting)
  - left trigger drives robot `+Z` (up)
  - left grip drives robot `-Z` (down)
  - right stick `Y` drives robot angular `Y`
  - right stick `X` drives robot angular `Z`
- Right controller `A` still enables rotate mode.
- If `A` is held in gamepad mode, the existing controller-orientation delta rotation takes priority over right-stick rotation.
- Right controller `B` still triggers reset.
- Hold right grip to engage teleop. The arm only follows hand/gamepad motion while right grip is held; releasing right grip pauses arm motion so you can move your hand freely.
- Hold right thumbstick to pause arm motion and move your hand freely; release it to recenter the hand reference at the current hand pose. This does not home the robot or reset objects.
- Right trigger toggles the gripper command. First press closes, next press opens, then alternates.

After changing controller fields such as right-thumbstick recenter or right-grip teleop engage, rebuild the backend once so the ROS message definition is regenerated:

```bash
./scripts/backend10_lifecycle.sh up_container
./scripts/backend10_lifecycle.sh build_ws
```

Keyboard controller tuning:

```bash
# Start keyboard mode from a real terminal first:
./scripts/backend10_lifecycle.sh keyboard

# Then, from another terminal:
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /keyboard_servo_cmd linear_speed_xyz \"[0.15,0.15,0.15]\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /keyboard_servo_cmd linear_sign_xyz \"[1.0,1.0,1.0]\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /keyboard_servo_cmd angular_speed_xyz \"[0.50,0.50,0.50]\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /keyboard_servo_cmd angular_sign_xyz \"[1.0,1.0,1.0]\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /keyboard_servo_cmd key_timeout_sec 0.25"
```

Keyboard notes:
- `W/S`: robot `+/-X`
- `A/D`: robot `+/-Y`
- `Q/E`: robot `+/-Z`
- `U/J`: angular `+/-X`
- `I/K`: angular `+/-Y`
- `O/L`: angular `+/-Z`
- `Space`: stop immediately
- `X` or `Ctrl-C`: quit keyboard mode
- `keyboard` publishes directly to `/servo_node/delta_twist_cmds`, so the lifecycle command stops `target_twist_to_servo_cmd` first to avoid fighting the headset path.
- After quitting keyboard mode, run `./scripts/backend10_lifecycle.sh start_part23` to restore headset control.
- If holding a key feels pulsed, increase `key_timeout_sec` slightly, for example `0.35`.

Scale/offset/workspace:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist scale_xyz \"[1.0,1.0,1.0]\""

docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist offset_xyz \"[0.0,0.0,0.0]\""

docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist min_xyz \"[-1.0,-1.0,-1.0]\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist max_xyz \"[1.0,1.0,1.0]\""
```

Gains/limits/deadbands:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist kp_linear 2.0"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist max_linear_speed 0.30"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist linear_deadband 0.005"

docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist kp_angular 4.0"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist max_angular_speed 1.50"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist angular_deadband 0.02"
```

## 4) Live reset tuning (no restart)

These apply to the next reset cycle. If a reset is currently running, the node rejects param changes; toggle reset OFF and retry.

Home and kick:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /target_twist_reset_manager home_joint_positions \"[-1.5,-1.5,1.5,0.0,0.0,0.0]\""

docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /target_twist_reset_manager kick_velocities \"[0.35,-0.18,-0.28,0.18,-0.28,0.0]\""

docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /target_twist_reset_manager kick_duration_sec 1.5"
```

Other reset knobs:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /target_twist_reset_manager home_kp 1.2"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /target_twist_reset_manager home_max_vel 0.60"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /target_twist_reset_manager home_tolerance 0.03"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /target_twist_reset_manager home_timeout_sec 8.0"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /target_twist_reset_manager min_reset_interval_sec 2.5"

# Dynamic scene objects reset to setup_z + scene_reset_lift_z.
# Default is 0.01 m, so objects are placed 1 cm above the table and settle down.
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /target_twist_reset_manager scene_reset_lift_z 0.01"
```

## 5) Verify while tuning

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /target_twist_states"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && tail -f /tmp/part2_mapper.log"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && tail -f /tmp/part3_reset_manager.log"
```

## 6) Live scene-object position/orientation modifier

In `1.0`, the legacy single-cube reset path is disabled.  
For live scene-object adjustment, move the actual Gazebo model directly.

```bash
docker exec "$CONTAINER" bash -lc "ign service -s /world/ur_hande_tabletop/set_pose --reqtype ignition.msgs.Pose --reptype ignition.msgs.Boolean --timeout 2000 --req 'name: \"Sync_RedCube\" position: {x: 0.50538 y: 0.45121 z: 0.020} orientation: {x: 0 y: 0 z: 0 w: 1}'"
```

Check the mirrored Unity sync topic:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /unity_sync/Sync_RedCube_pose --once"
```

## 7) Persist tuning to file

Live `ros2 param set` is runtime-only. To keep values after restart, update:

`src/teleop_bridge/config/teleop_tuning.yaml`

Then restart Part2/Part3 nodes:

```bash
./scripts/backend10_lifecycle.sh start_part23
```

If you modified Python source code (node logic), rebuild workspace and restart Part2/Part3:

```bash
./scripts/backend10_lifecycle.sh build_ws
./scripts/backend10_lifecycle.sh start_part23
```

If you modified cube size/mass in world SDF, restart simulation:

```bash
./scripts/backend10_lifecycle.sh start_sim
```

## 8) Live spawn/remove another debug object

Spawn another debug cube now (example name `debug_cube_1`):

```bash
docker exec "$CONTAINER" bash -lc "cat > /tmp/debug_cube_1.sdf <<'EOF'
<?xml version='1.0'?>
<sdf version='1.8'>
  <model name='debug_cube_1'>
    <static>false</static>
    <link name='cube_link'>
      <inertial><mass>0.08</mass><inertia><ixx>0.00003</ixx><iyy>0.00003</iyy><izz>0.00003</izz><ixy>0</ixy><ixz>0</ixz><iyz>0</iyz></inertia></inertial>
      <collision name='collision'><geometry><box><size>0.045 0.045 0.045</size></box></geometry></collision>
      <visual name='visual'><geometry><box><size>0.045 0.045 0.045</size></box></geometry></visual>
    </link>
  </model>
</sdf>
EOF
source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && ros2 run ros_gz_sim create -world ur_hande_tabletop -name debug_cube_1 -file /tmp/debug_cube_1.sdf -x 0.62 -y 0.05 -z 0.0225"
```

Move it live:

```bash
docker exec "$CONTAINER" bash -lc "ign service -s /world/ur_hande_tabletop/set_pose --reqtype ignition.msgs.Pose --reptype ignition.msgs.Boolean --timeout 2000 --req 'name: \"debug_cube_1\" position: {x: 0.62 y: 0.05 z: 0.023} orientation: {x: 0 y: 0 z: 0 w: 1}'"
```

Remove it:

```bash
docker exec "$CONTAINER" bash -lc "ign service -s /world/ur_hande_tabletop/remove --reqtype ignition.msgs.Entity --reptype ignition.msgs.Boolean --timeout 2000 --req 'name: \"debug_cube_1\" type: MODEL'"
```

## 9) Live resize + friction tune a synced scene object

Gazebo does not expose a direct \"resize collision geometry\" service.  
Use remove + respawn for true physics size/friction changes (live, no container restart).

Example: respawn `Sync_RedCube` with a custom size/friction while keeping the same synced object name.

```bash
docker exec "$CONTAINER" bash -lc "cat > /tmp/sync_red_cube_live.sdf <<'EOF'
<?xml version='1.0'?>
<sdf version='1.8'>
  <model name='Sync_RedCube'>
    <static>false</static>
    <link name='body_link'>
      <inertial>
        <mass>0.08</mass>
        <inertia>
          <ixx>0.00003</ixx><iyy>0.00003</iyy><izz>0.00003</izz>
          <ixy>0</ixy><ixz>0</ixz><iyz>0</iyz>
        </inertia>
      </inertial>
      <collision name='cube_collision'>
        <geometry><box><size>0.040 0.040 0.040</size></box></geometry>
        <surface>
          <friction><ode><mu>1.4</mu><mu2>1.4</mu2></ode></friction>
          <contact><ode><kp>200000</kp><kd>30</kd><max_vel>0.1</max_vel><min_depth>0.001</min_depth></ode></contact>
        </surface>
      </collision>
      <visual name='cube_visual'>
        <geometry><box><size>0.040 0.040 0.040</size></box></geometry>
        <material>
          <ambient>0.85 0.12 0.12 1</ambient>
          <diffuse>0.85 0.12 0.12 1</diffuse>
          <specular>0.1 0.1 0.1 1</specular>
        </material>
      </visual>
    </link>
  </model>
</sdf>
EOF
ign service -s /world/ur_hande_tabletop/remove --reqtype ignition.msgs.Entity --reptype ignition.msgs.Boolean --timeout 2000 --req 'name: \"Sync_RedCube\" type: MODEL' || true
source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash
ros2 run ros_gz_sim create -world ur_hande_tabletop -name Sync_RedCube -file /tmp/sync_red_cube_live.sdf -x 0.50538 -y 0.45121 -z 0.020"
```

Verify synced pose after respawn:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /unity_sync/Sync_RedCube_pose --once"
```


---

## Gripper Camera

# Gripper / Wrist Camera

There are two camera-related paths in this project.

## Unity Wrist Camera

Unity has a scene camera named `GripperDataCamera` attached near the gripper. It is used for headset-side preview and data recording.

Main script:

```text
UnityApp/Assets/Scripts/GripperCameraRecorder.cs
```

Scene object:

```text
UnityApp/Assets/Scenes/Ur5e_Working 1.unity > GripperDataCamera
```

Important fields:

- `Width` / `Height`: recording and preview RenderTexture size.
- `Capture Every N Frames`: frame-save interval while recording.
- `Assign Runtime Render Texture`: should stay enabled.
- `Force Render Before Capture`: usually disabled for performance.
- `Create Floating Panel`: creates the camera preview/control panel.

## Recording Controls

- Left controller `X`: start / stop recording.
- Floating panel `Record` button: start / stop recording.
- Floating panel `Capture Frame`: save one frame.

See the `RECORDING` section later in this file.

## Gazebo Gripper Camera

The Gazebo camera, when enabled in the robot description, publishes ROS topics such as:

```bash
/gripper_camera/image_raw
/gripper_camera/camera_info
```

Inspect topics:

```bash
cd ros_backend1.0
CONTAINER=motion_planner_10
ROS_ENV='source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash'
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic list | grep gripper_camera"
```

Check image rate:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic hz /gripper_camera/image_raw"
```

If `/gripper_camera/camera_info` is missing, inspect the Gazebo camera plugin and bridge configuration in `ros_backend1.0/simulation` and robot xacro files.


---

## Recording

# Headset Data Recording

This file documents how to record images from the Unity gripper-mounted camera while running the app in the headset.

## 1) What Is Being Recorded

The Unity scene has a camera attached to the gripper:

```text
Ur5e_Working 1.unity
UR5e/.../robotiq_hande_link/GripperDataCamera
```

The recording script is:

```text
UnityApp/Assets/Scripts/GripperCameraRecorder.cs
```

It records PNG frames from `GripperDataCamera`.

The floating preview panel and UI buttons are excluded from the gripper camera recording, so they should not appear in the saved frames.

The yellow camera marker is controlled by `GripperCameraRecorder`:

- `showSceneMarker`: draws the editor Gizmo marker.
- `createRuntimeSceneMarker`: creates a real editor/play-mode child object named `GripperDataCamera_VisibleMarker` under `GripperDataCamera`.
- `rebuildSceneMarkerFromSettings`: one-shot checkbox that deletes/recreates the marker visuals from the numeric settings below.
- `runtimeSceneMarkerLayer`: defaults to layer `0` (`Default`) so it is visible in Scene view and headset/Game view.
- `markerBoxSize`, `markerForwardLength`, `markerFrustumHalfSize`, and `markerLineWidth` control its size. `markerForwardLength` is the distance from the camera body to the frustum plane.

After the marker exists, you can manually edit `GripperDataCamera_VisibleMarker`, `CameraBody`, and `Frustum` in the hierarchy. Those manual edits are preserved across script reloads/builds unless you enable `Rebuild Scene Marker From Settings`.

The marker is temporarily hidden only while a frame is captured, so it should not appear in the saved PNG images.

If the marker does not appear immediately after Unity recompiles, select `GripperDataCamera` and toggle `Create Runtime Scene Marker` off/on, or reopen the scene. Unity should then create `GripperDataCamera_VisibleMarker` as a normal child object in the hierarchy.

## 2) Headset Controls

When running inside the Quest headset:

- Left controller `X`: start/stop recording
- Floating control panel: shows live gripper camera preview and `IDLE` / `REC` status
- Floating panel `Record` / `Stop` button: also toggles recording if XR UI pointer interaction is working
- Floating panel `Capture Frame` button: captures one frame if XR UI pointer interaction is working
- Floating panel drag: release right grip so teleop is disengaged, point the left controller at the panel, hold left trigger, move the panel, then release left trigger

In the Unity Editor:

- Keyboard `R`: start/stop recording
- Keyboard `P`: capture one frame

## 3) Recommended Recording Flow

1. Start the ROS/Gazebo backend as usual.

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh status
```

2. Build or run the Unity app on the Quest.

3. Put on the headset.

4. Confirm the floating panel appears on the left side of the view.

5. Optional: release right grip, point the left controller at the panel, hold left trigger, drag the panel to a comfortable place, then release left trigger.

6. Press left controller `X`.

7. Confirm the floating panel status changes from `IDLE` to `REC`.

8. Perform the robot/task motion.

9. Press left controller `X` again to stop.

10. Confirm the floating panel status returns to `IDLE`.

## 4) Where Files Are Saved

The script saves frames under Unity's `Application.persistentDataPath`.

For the Quest/Android build, expected path:

```text
/storage/emulated/0/Android/data/com.noahli.handtrackingunity/files/GripperCameraRecordings
```

Inside that folder:

- Normal recordings are saved in timestamp folders, for example:

```text
20260416_143015/
```

- Single-frame captures are saved in:

```text
single_frame/
```

Each image filename looks like:

```text
gripper_camera_YYYYMMDD_HHMMSS_mmm_FRAME.png
```

Example:

```text
gripper_camera_20260416_143015_281_00012345.png
```

## 5) Pull Data From Quest To Mac

Connect the Quest to the Mac with USB.

Check ADB sees the headset:

```bash
adb devices
```

Expected:

```text
List of devices attached
<device_id>    device
```

Pull the recording folder:

```bash
cd /path/to/robot-teleop-project
adb pull "/storage/emulated/0/Android/data/com.noahli.handtrackingunity/files/GripperCameraRecordings" ./GripperCameraRecordings
```

Open the pulled folder:

```bash
open ./GripperCameraRecordings
```

## 6) Check Files Before Pulling

List recording folders on the Quest:

```bash
adb shell ls -lah "/storage/emulated/0/Android/data/com.noahli.handtrackingunity/files/GripperCameraRecordings"
```

List files inside one session:

```bash
adb shell ls -lah "/storage/emulated/0/Android/data/com.noahli.handtrackingunity/files/GripperCameraRecordings/20260416_143015"
```

Replace `20260416_143015` with the actual session folder name.

## 7) If Left X Does Not Toggle Recording

Use these checks:

1. Confirm the app is running in the headset, not only in the Unity Editor.

2. Confirm the left controller is awake and tracked.

3. Select `GripperDataCamera` in Unity and check:

```text
GripperCameraRecorder -> Toggle Recording With Left X = true
GripperCameraRecorder -> Debug Left X Input = true
```

4. If you want recording to start automatically, enable:

```text
GripperCameraRecorder -> Record On Play = true
```

This starts recording as soon as the scene starts.

5. Check whether the headset is reporting the X press:

```bash
adb logcat -d Unity:I '*:S' | grep 'Left X toggle detected'
```

If the X button is detected, the log should include:

```text
[GripperCameraRecorder] Left X toggle detected. ...
```

The log also reports which OVRInput mapping fired, for example `RawButton.X/Any=True`.

If logs show this error:

```text
InvalidOperationException: You are trying to read Input using the UnityEngine.Input class
```

then the headset is running an old build of the recorder. Rebuild/reinstall the Unity app; the current recorder avoids legacy `UnityEngine.Input` on Quest and uses `OVRInput` for left-controller `X`.

## 8) If No Files Appear

Check the Unity log message. The recorder prints the exact root folder at scene start:

```text
[GripperCameraRecorder] Recording root: ...
```

If running on Quest, inspect logs:

```bash
adb logcat -d Unity:I '*:S' | grep GripperCameraRecorder
```

If recording started correctly, logs should include:

```text
[GripperCameraRecorder] Started recording to: ...
```

If recording stopped correctly, logs should include:

```text
[GripperCameraRecorder] Stopped recording. Last session: ...
```

## 9) Recording Rate And Resolution

Current default settings on `GripperDataCamera`:

```text
width: 1280
height: 720
captureEveryNFrames: 5
```

At 60 FPS, `captureEveryNFrames = 5` records about 12 images per second.

To record more frames:

```text
captureEveryNFrames = 1
```

To reduce load:

```text
captureEveryNFrames = 10
```

If the headset performance drops, increase `captureEveryNFrames` or reduce resolution.

## 10) Unity Editor Test Path

When testing in Unity Editor on Mac, files are saved here:

```bash
~/Library/Application Support/DefaultCompany/My project/GripperCameraRecordings
```

Open it:

```bash
open "$HOME/Library/Application Support/DefaultCompany/My project/GripperCameraRecordings"
```

## 11) Relevant Unity Files

Scene:

```text
UnityApp/Assets/Scenes/Ur5e_Working 1.unity
```

Recorder script:

```text
UnityApp/Assets/Scripts/GripperCameraRecorder.cs
```

Camera object:

```text
GripperDataCamera
```

Important inspector fields:

- `Record On Play`
- `Toggle Recording With Left X`
- `Capture Every N Frames`
- `Width`
- `Height`
- `Create Floating Panel`
- `Rebuild Floating Panel From Settings`
- `Floating Panel Fixed In Scene`
- `Floating Panel World Position`
- `Floating Panel World Euler`
- `Floating Panel Face Main Camera On Create`
- `Floating Panel Local Position`
- `Preview Size`
- `Enable Floating Panel Controller Drag`
- `Require Teleop Disengaged For Panel Drag`
- `Floating Panel Drag Controller`
- `Floating Panel Drag Button`
- `Floating Panel Drag Button Threshold`
- `Floating Panel Drag Ray Max Distance`
- `Right Grip Teleop Drag Block Threshold`
- `Persist Floating Panel Dragged Pose`

`GripperCameraFloatingPanel` is an editor-created scene object. It is now treated as the central headset control panel, not only a camera preview. After Unity recompiles, it should appear in the hierarchy and can be moved, rotated, scaled, or restyled directly. Manual edits are preserved unless `Rebuild Floating Panel From Settings` is enabled.

When `Floating Panel Fixed In Scene` is enabled, the recording window is placed at the world position/euler fields during creation/rebuild and stays fixed in the room. `Floating Panel Face Main Camera On Create` aims the panel at the main camera once when the panel is created/rebuilt, then leaves it fixed. When fixed mode is disabled, the window uses the older headset-following behavior and `Floating Panel Local Position` is relative to the headset/Main Camera.

## 12) Dragging The Floating Control Panel In The Headset

Default drag settings:

```text
Enable Floating Panel Controller Drag = true
Require Teleop Disengaged For Panel Drag = true
Floating Panel Drag Controller = Left
Floating Panel Drag Button = Trigger
Floating Panel Drag Button Threshold = 0.55
Floating Panel Drag Ray Max Distance = 3
Right Grip Teleop Drag Block Threshold = 0.55
Persist Floating Panel Dragged Pose = true
```

Headset workflow:

1. Release the right grip so robot teleop is disengaged.
2. Point the left controller ray toward `GripperCameraFloatingPanel`.
3. Hold the left trigger.
4. Move the left controller to reposition the panel.
5. Release the left trigger to drop the panel.

When `Persist Floating Panel Dragged Pose` is enabled, the dropped world pose is saved with Unity `PlayerPrefs` on the headset. The next app run loads that saved panel pose.

If you want a different drag control, change:

```text
Floating Panel Drag Controller = Left or Right
Floating Panel Drag Button = Trigger, Grip, or Thumbstick
```

Keep `Require Teleop Disengaged For Panel Drag` enabled for normal use. It prevents the panel from being dragged while right grip is held for robot teleop. `Right Grip Teleop Drag Block Threshold` should normally match `HandPoseSender -> Analog Press Threshold`.


---

## Troubleshooting

# Troubleshooting

## Robot Does Not Move

Check backend state:

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh status
```

Check mapper output:

```bash
CONTAINER=motion_planner_10
ROS_ENV='source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash'
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /target_twist_states --once"
```

Common causes:

- Right grip is not held. Arm motion is gated by right grip.
- B reset is active. In `1.0`, reset should auto-release after `reset_hold_sec`.
- Part 2/3 nodes are stale. Restart them:

```bash
./scripts/backend10_lifecycle.sh start_part23
```

## Y Button Seems To Break Motion

`Y` toggles hand-pose mode vs thumbstick/gamepad mode.

- In `hand_pose` mode, right hand/controller motion moves the robot while right grip is held.
- In `gamepad` mode, the robot ignores hand-position motion and uses sticks/triggers instead.
- Press `Y` again to return to hand-pose mode.

Live check:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist control_mode"
```

## Floating Control Panel Has No Camera Preview

Check that `GripperCameraRecorder` is enabled on `GripperDataCamera` in Unity.

The preview depends on a runtime RenderTexture. If the panel exists but is blank:

- Stop Play mode.
- Select `GripperDataCamera`.
- Confirm `Gripper Camera Recorder` is enabled.
- Confirm `Assign Runtime Render Texture` is enabled.
- Press Play again.

## Recording Causes Lag

Recording currently uses GPU readback + image encoding + synchronous file writes.

Reduce load by changing `GripperDataCamera > GripperCameraRecorder`:

- Increase `Capture Every N Frames` from `5` to `10` or `15`.
- Reduce `Width/Height` from `1280x720` to `640x360`.
- Keep `Force Render Before Capture` disabled unless needed.

Future improvement: async GPU readback and JPEG/video encoding.

## Gripper Visual Moves Opposite Direction

Unity uses visual-only gripper finger motion from `/joint_states`.

Current intended settings in `Ur5e_Working 1`:

```yaml
useAbsoluteGripperJointForVisuals: 1
gripperClosedPositionMeters: 0
gripperOpenPositionMeters: 0.025
leftFingerLocalAxis: {x: 0, y: 0, z: 1}
rightFingerLocalAxis: {x: 0, y: 0, z: 1}
leftFingerDirectionSign: 1
rightFingerDirectionSign: -1
```

If a reimported robot has different mesh orientation, only adjust:

- `leftFingerLocalAxis`
- `rightFingerLocalAxis`
- `leftFingerDirectionSign`
- `rightFingerDirectionSign`

Do not re-enable gripper ArticulationBodies unless Unity physics is intentionally being used.

## Docker On macOS Uses CPU Heavily

Gazebo camera rendering inside Docker on macOS generally does not get native GPU acceleration like a Linux workstation. Camera rendering and PNG recording can lower real-time factor.

Useful mitigations:

- Lower camera resolution.
- Record fewer frames.
- Run headless when not visually debugging Gazebo.
- Use a Linux workstation with proper GPU passthrough for heavier simulation work.


---

## GitHub Setup

# GitHub Setup

These steps create a clean Git repository from the current project folder.

## 1) Install Git LFS

Large Unity/mesh/data files are configured in `.gitattributes` for Git LFS.

```bash
git lfs install
```

## 2) Initialize The Repository

From the project root:

```bash
git init
git status
```

## 3) Add Files

The root `.gitignore` excludes generated Unity files, old archives, recordings, local `.env`, and old backend versions.

```bash
git add README.md docs .gitignore .gitattributes UnityApp ros_backend1.0
git status
```

Before committing, make sure these are not staged:

```text
UnityApp/Library/
UnityApp/Temp/
UnityApp/Builds/
UnityApp/*.csproj
UnityApp/*.sln
ros_backend1.0/build/
ros_backend1.0/install/
ros_backend1.0/log/
ros_backend1.0/.env
GripperCameraRecordings/
Archive/
Ros_archive/
ros_backend0.9/
```

## 4) Commit

```bash
git commit -m "Initial UR5e Hand-E VR teleop project"
```

## 5) Create GitHub Repo And Push

Create an empty GitHub repo, then connect it:

```bash
git branch -M main
git remote add origin git@github.com:<your-user>/<your-repo>.git
git push -u origin main
```

If you use HTTPS instead of SSH:

```bash
git remote add origin https://github.com/<your-user>/<your-repo>.git
git push -u origin main
```

## 6) Clone On Another Computer

```bash
git clone git@github.com:<your-user>/<your-repo>.git
cd <your-repo>
git lfs pull
cd ros_backend1.0
cp .env.example .env
./scripts/backend10_lifecycle.sh up_container
./scripts/backend10_lifecycle.sh build_ws
```


---
