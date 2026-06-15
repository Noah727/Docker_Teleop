# Getting Started

This document is the operational run guide for the current dual-arm mixed-reality teleoperation system. It assumes the repository, Unity project, Docker backend, and Quest development environment have already been set up. For first-time machine setup, use [System_Setup.md](System_Setup.md).

## Current Defaults

| Item | Value |
| --- | --- |
| Backend folder | `ros_backend1.1` |
| Lifecycle script | `ros_backend1.1/scripts/backend11_lifecycle.sh` |
| Docker container | `motion_planner_11` |
| Unity scene | `UnityApp/Assets/Scenes/GazeboReplica_DualArm_MR.unity` |
| Host Quest TCP port | `127.0.0.1:5026` |
| Container Quest TCP listener | `5005` |
| Host ROS-TCP port | `127.0.0.1:10001` |
| Container ROS-TCP endpoint | `10000` |
| Preferred connection | Wired USB through `adb reverse` |

## Fast Start

Use this sequence when the backend has already been built and the Quest app is installed or ready to install:

```bash
cd ros_backend1.1
cp .env.example .env 2>/dev/null || true
./scripts/backend11_lifecycle.sh mode_wired
./scripts/backend11_lifecycle.sh bringup_dual
./scripts/backend11_lifecycle.sh status
```

`bringup_dual` starts the container, applies wired ADB reverse tunnels if the Quest is connected, generates the Gazebo world from task profiles, starts dual Gazebo, starts dual MoveIt Servo, starts the Quest TCP receiver, starts both arm control pipelines, and starts Unity synchronization.

## Install Published Quest App

The current prebuilt Quest APK is published as a GitHub Release asset:

- Release: [Unity Quest App 7.0.7](https://github.com/su-idr-lab/ros_unity_project/releases/tag/unity-app-7.0.7)
- APK asset: [`R.U_7.0.7.apk`](https://github.com/su-idr-lab/ros_unity_project/releases/download/unity-app-7.0.7/R.U_7.0.7.apk)

Download it with GitHub CLI:

```bash
mkdir -p UnityApp/App_Build
gh release download unity-app-7.0.7 \
  --repo su-idr-lab/ros_unity_project \
  --pattern 'R.U_7.0.7.apk' \
  --dir UnityApp/App_Build
```

1. Connect the Quest 3 by USB.
2. Accept the USB debugging prompt in the headset.
3. Confirm ADB sees the headset:

```bash
adb devices
```

Expected: the Quest appears as `device`, not `unauthorized`.

4. Install the APK:

```bash
adb install -r -d 'UnityApp/App_Build/R.U_7.0.7.apk'
```

5. Launch the installed app from the headset app library. Current package ID is:

```text
com.noahli.ROSUNITY
```

Useful install checks:

```bash
adb shell pm list packages | grep -i noahli
adb logcat -d "Unity:I" "*:S" | tail -n 80
```

APK files are release artifacts rather than normal Git-tracked files. Build from source only when changing Unity code, changing Unity project settings, or producing a new public release build.

## Build Quest App From Source

Use this path when the published APK is not sufficient or a new Unity build is required.

### Unity Environment

Required Unity setup:

| Item | Value |
| --- | --- |
| Unity version | `6000.2.10f1` |
| Project folder | `UnityApp` |
| Active scene | `Assets/Scenes/GazeboReplica_DualArm_MR.unity` |
| Target platform | Android / Quest |
| Package ID | `com.noahli.ROSUNITY` |

Install these Unity modules through Unity Hub:

- Android Build Support.
- Android SDK & NDK Tools.
- OpenJDK.

### Open The Project

1. Open Unity Hub.
2. Select `Add project from disk`.
3. Choose the repository's `UnityApp` folder.
4. Open with Unity `6000.2.10f1`.
5. Let Unity restore packages and regenerate the local `Library/` folder.
6. Open the active scene:

```text
Assets/Scenes/GazeboReplica_DualArm_MR.unity
```

### Confirm Connection Settings

For wired Quest operation, the Unity build should use:

| Unity Setting | Value |
| --- | --- |
| Hand/controller TCP target IP | `127.0.0.1` |
| Hand/controller TCP target port | `5026` |
| ROS Settings IP Address | `127.0.0.1` |
| ROS Settings Port | `10001` |

The backend `adb reverse` tunnels map these headset-local addresses back to the host and Docker container.

### Build And Install

1. Connect the Quest 3 by USB.
2. Accept the USB debugging prompt in the headset.
3. Confirm ADB sees the headset:

```bash
adb devices
```

4. In Unity, open:

```text
File > Build Profiles
```

5. Select the Android/Quest build profile.
6. Confirm that `GazeboReplica_DualArm_MR.unity` is included in the build scenes.
7. Use `Build And Run`, or build an APK into:

```text
UnityApp/App_Build/
```

If an APK is built locally, it can also be installed from the terminal:

```bash
adb install -r -d 'UnityApp/App_Build/<app-build>.apk'
```

Local APKs, Unity `Library/`, Unity `Temp/`, and generated build folders are not tracked by Git.

## Unity/Quest Connection Settings

In wired mode, the Quest app connects to its own loopback address. `adb reverse` tunnels those ports back to the host and Docker forwards them into the container.

| Unity Setting | Value |
| --- | --- |
| Hand/controller TCP target IP | `127.0.0.1` |
| Hand/controller TCP target port | `5026` |
| ROS Settings IP Address | `127.0.0.1` |
| ROS Settings Port | `10001` |

Check wired tunnels:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh wired_status
adb reverse --list
```

Expected tunnels:

```text
tcp:5026 tcp:5026
tcp:10001 tcp:10001
```

## Headset Controls

Each physical controller drives one robot arm by default.

### Right Controller / Right Arm

- `Grip hold`: engage right-arm teleop.
- `Trigger tap`: toggle right gripper open/close.
- `A hold`: right-arm rotation mode.
- `B tap`: toggle right-arm attachment mode.
- `Right thumbstick press`: clutch/pause hand following; release to recenter the hand reference.

### Left Controller / Left Arm

- `Grip hold`: engage left-arm teleop.
- `Trigger tap`: toggle left gripper open/close.
- `X hold`: left-arm rotation mode.
- `Y tap`: toggle left-arm attachment mode.

### Control Panel

- `Swap Hands`: swap physical controller-to-arm assignment.
- Reset buttons: objects, left arm, right arm, both arms, all, workspace viewpoint.
- Camera page: left wrist camera, right wrist camera, floating camera preview.
- Recording controls: start/stop wrist-camera recording.
- Haptics page: haptic output and gain controls.

The legacy in-headset control-mode switch is disabled by default. Optional keyboard/thumbstick/gamepad control modes are activated from terminal commands instead.

## Optional Controllers

Optional controllers are right-arm overrides by default. The left Quest controller and left robot arm continue normally.

### Thumbstick / Gamepad Mode

This mode keeps the Quest app running while the right-arm backend mapper ignores right-hand pose following and uses the gamepad/thumbstick fields instead.

Enable right-arm thumbstick/gamepad mode:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh right_gamepad_on
```

Disable it and restore right-arm hand-pose mode:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh right_gamepad_off
```

Gamepad mapping:

- `Left stick Y`: forward/back.
- `Left stick X`: left/right.
- `Right stick Y`: up/down.
- `Right trigger`: toggle right gripper open/close.
- Hold right `A` or left `X`: enter rotation layer.
- `Left stick X` while holding `A` or `X`: roll.
- `Right stick Y` while holding `A` or `X`: pitch.
- `Right stick X` while holding `A` or `X`: yaw.
- Default linear speed is `gamepad_linear_speed_xyz=[0.30,0.30,0.30]`.
- Default angular speed is `gamepad_angular_speed_xyz=[0.55,0.70,0.70]`.

Backend parameters live in:

```text
ros_backend1.1/src/teleop_bridge/config/teleop_tuning_dual_right.yaml
```

Main tuning keys:

- `gamepad_deadband`
- `gamepad_linear_speed_xyz`
- `gamepad_linear_sign_xyz`
- `gamepad_rotation_mode`
- `gamepad_angular_speed_xyz`
- `gamepad_angular_sign_xyz`

### Keyboard Override

This starts an interactive terminal keyboard controller for the right arm only. It temporarily stops the right-arm hand-pose mapper, then publishes into the normal right-arm target-twist stream. The right Servo bridge and coupled gripper controller stay active, so the keyboard path uses the same downstream control pipeline as normal headset teleop. The left arm pipeline stays active.

Start right-arm keyboard override:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh right_keyboard
```

Key map:

- `W/S`: robot `+/-X`.
- `A/D`: robot `+/-Y`.
- `Q/E`: robot `+/-Z`.
- `U/J`: roll `+/-`.
- `I/K`: pitch `+/-`.
- `O/L`: yaw `+/-`.
- `G`: toggle right gripper close/open.
- `Space`: stop immediately.
- `X` or `Ctrl-C`: quit.

After quitting, restore right-arm headset control:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh right_keyboard_off
```

### SpaceMouse

SpaceMouse is implemented as a right-arm optional input. It publishes into the normal right-arm target-twist stream and uses the left-side SpaceMouse button as a gripper open/close toggle.

Install the official driver first:

- macOS: install 3DxWare 10 for macOS from the 3Dconnexion driver page: <https://3dconnexion.com/us/drivers/>.
- Ubuntu/Linux: install the Linux 3DxWare driver or make sure the SpaceMouse HID device is visible under `/dev/hidraw*`.

#### macOS Docker Desktop Path

Docker Desktop on macOS does not expose the SpaceMouse HID device directly to the Linux container. Use the macOS host bridge:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh right_spacemouse_host_bridge
```

Install the host dependency once:

```bash
cd /Users/noahli/ros_unity_project
python3 -m venv ros_backend1.1/.venv_spacemouse_host
ros_backend1.1/.venv_spacemouse_host/bin/python -m pip install hidapi
```

Detect the SpaceMouse interfaces:

```bash
ros_backend1.1/.venv_spacemouse_host/bin/python \
  ros_backend1.1/src/teleop_bridge/teleop_bridge/optional_inputs/mac_spacemouse_host_bridge.py \
  --detect-only
```

Run the host bridge. On this Mac, `MATCH[1]` is the motion interface:

```bash
ros_backend1.1/.venv_spacemouse_host/bin/python \
  ros_backend1.1/src/teleop_bridge/teleop_bridge/optional_inputs/mac_spacemouse_host_bridge.py \
  --host 127.0.0.1 \
  --port 5036 \
  --device-index 1 \
  --linear-sign-xyz -1.0,-1.0,-1.0 \
  --angular-speed-xyz 1.4,1.4,1.4
```

Restore right-arm headset control:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh right_spacemouse_host_bridge_off
```

#### Native Linux Path

On native Linux, if the SpaceMouse appears inside the container under `/dev/hidraw*`, the direct ROS node can be used:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh right_spacemouse
./scripts/backend11_lifecycle.sh right_spacemouse_off
```

Default SpaceMouse mapping:

- Cap translation: right-arm XYZ velocity.
- Cap twist/rotation: right-arm roll/pitch/yaw angular velocity.
- Left SpaceMouse button: toggle right gripper close/open.
- The default translation sign is `linear_sign_xyz=[-1.0,-1.0,-1.0]`, which keeps the corrected up/down direction and restores the original X/Y feel.
- The default angular speed is `angular_speed_xyz=[1.4,1.4,1.4]`.
- If any rotation axis feels reversed, flip that entry in `angular_sign_xyz`.

SpaceMouse scripts live in:

```text
ros_backend1.1/src/teleop_bridge/teleop_bridge/optional_inputs/
```

Current macOS test result: the synthetic TCP path published `/right_arm/target_twist_states` at about 60 Hz, `MATCH[1]` produced physical cap motion, and the right Servo/gripper bridges received the SpaceMouse stream.

The official 3Dconnexion SDK is available through the 3Dconnexion Software Developer Program (<https://3dconnexion.com/us/software-developer-program/>). This project currently uses direct HID input so the SpaceMouse path can run as a normal ROS node.

### Optional Input Script Locations

The optional input scripts are grouped here:

```text
ros_backend1.1/src/teleop_bridge/teleop_bridge/optional_inputs/
```

Gamepad/thumbstick mode is implemented inside `teleop_bridge/mapping/hand_pose_mapper.py` because it uses thumbstick and trigger fields already sent by the Quest app.

## Step-By-Step Bringup With Checkpoints

Use this sequence when validating a new machine or debugging the runtime stack.

### 1. Set Variables

```bash
cd ros_backend1.1
CONTAINER=motion_planner_11
ROS_ENV='source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash'
```

### 2. Start Or Build Container

```bash
./scripts/backend11_lifecycle.sh up_container
./scripts/backend11_lifecycle.sh build_ws
./scripts/backend11_lifecycle.sh build_receiver
```

Checkpoint:

```bash
docker ps --filter name=motion_planner_11
docker port motion_planner_11
```

Expected wired mappings:

- `10000/tcp -> 127.0.0.1:10001`
- `5005/tcp -> 127.0.0.1:5026`

### 3. Enable Wired Quest USB Tunnel

```bash
./scripts/backend11_lifecycle.sh wired_on
./scripts/backend11_lifecycle.sh wired_status
```

### 4. Start Gazebo Dual-Arm Simulation

```bash
./scripts/backend11_lifecycle.sh start_dual_sim
```

Checkpoint:

```bash
docker exec "$CONTAINER" bash -lc "tail -n 80 /tmp/run_dual_arm_tabletop_sim.log"
```

### 5. Start Dual Servo

```bash
./scripts/backend11_lifecycle.sh start_dual_servo
```

Checkpoint:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 control list_controllers"
docker exec "$CONTAINER" bash -lc "tail -n 80 /tmp/servo_dual_gz.log"
```

### 6. Start Quest TCP Receiver

```bash
./scripts/backend11_lifecycle.sh start_receiver
```

Checkpoint:

```bash
docker exec "$CONTAINER" bash -lc "tail -f /tmp/qcr.log"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /left_arm/received_pose_states --once"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /right_arm/received_pose_states --once"
```

Healthy signs:

- `/tmp/qcr.log` shows `TCP client connected`.
- `/tmp/qcr.log` shows `RX xx.x Hz`.
- Left and right received topics publish when the Quest app is running.

### 7. Start Dual Mapping And Command Nodes

```bash
./scripts/backend11_lifecycle.sh start_dual_part23
```

Checkpoint:

```bash
docker exec "$CONTAINER" bash -lc "tail -f /tmp/dual_left_mapper.log"
docker exec "$CONTAINER" bash -lc "tail -f /tmp/dual_right_mapper.log"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /right_arm/target_twist_states --once"
```

### 8. Start Unity Sync

```bash
./scripts/backend11_lifecycle.sh start_dual_part4
```

Checkpoint:

```bash
docker exec "$CONTAINER" bash -lc "tail -f /tmp/dual_part4_tcp_endpoint.log"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic list | grep /unity_sync/"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /unity_sync/Sync_RedCube_pose --once"
```

### 9. Final Status

```bash
./scripts/backend11_lifecycle.sh status
```

## Restart Gazebo After Closing The Window

If Gazebo was closed but the container is still running:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh start_dual_sim
sleep 2
./scripts/backend11_lifecycle.sh start_dual_servo
./scripts/backend11_lifecycle.sh start_dual_part23
./scripts/backend11_lifecycle.sh start_dual_part4
./scripts/backend11_lifecycle.sh status
```

## Live Camera Checks

The current workflow uses Unity-side wrist cameras for preview and recording. The Gazebo gripper camera is intentionally disabled, so `/gripper_camera/image_raw` and `/gripper_camera/camera_info` are not expected to appear.

Unity camera scripts:

```text
UnityApp/Assets/Scripts/GripperCameraRecorder.cs
UnityApp/Assets/Scripts/FloatingSceneCameraController.cs
```

Recordings on Quest are saved under:

```text
/storage/emulated/0/Android/data/com.noahli.ROSUNITY/files/GripperCameraRecordings
```

Pull recordings:

```bash
adb pull "/storage/emulated/0/Android/data/com.noahli.ROSUNITY/files/GripperCameraRecordings" ./GripperCameraRecordings
```

## Recovery Ladder

Use the smallest recovery first.

### Level 1: Restart Runtime Nodes

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh stop_nodes
./scripts/backend11_lifecycle.sh start_receiver
./scripts/backend11_lifecycle.sh start_dual_part23
./scripts/backend11_lifecycle.sh start_dual_part4
./scripts/backend11_lifecycle.sh status
```

### Level 2: Restart Dual Simulation Stack

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh start_dual_sim
sleep 2
./scripts/backend11_lifecycle.sh start_dual_servo
./scripts/backend11_lifecycle.sh start_dual_part23
./scripts/backend11_lifecycle.sh start_dual_part4
```

### Level 3: Clean Container Restart

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh safe_down
./scripts/backend11_lifecycle.sh bringup_dual
```

### Level 4: Docker/USB Reset

Use this if port forwarding or USB forwarding is broken:

- Reconnect the USB cable.
- Rerun `./scripts/backend11_lifecycle.sh wired_on`.
- Restart Docker Desktop if needed.
- Run the Level 3 sequence again.

## Tuning Locations

Dual-arm tuning files:

```text
ros_backend1.1/src/teleop_bridge/config/teleop_tuning_dual_left.yaml
ros_backend1.1/src/teleop_bridge/config/teleop_tuning_dual_right.yaml
```

Important parameters:

- `kp_linear`, `max_linear_speed`: hand-position responsiveness.
- `kp_angular`, `max_angular_speed`: rotation responsiveness.
- `map_axes`, `map_signs`: position mapping axes/signs.
- `rot_map_axes`, `rot_map_signs`: rotation mapping axes/signs.
- `control_mode`: `hand_pose` or `gamepad`, normally changed by terminal commands.
- `allow_unity_control_mode_switch`: default `false`; keep false unless intentionally restoring legacy UI mode switching.
- `gamepad_*`: optional thumbstick/gamepad control tuning.
- `home_joint_positions`: robot reset/home pose.

After editing tuning or Python code:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh build_ws
./scripts/backend11_lifecycle.sh start_dual_part23
./scripts/backend11_lifecycle.sh status
```

## Shutdown

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh safe_down
```
