# Getting Started

This is the day-to-day run guide for the current dual-arm MR teleoperation system. For full machine replication from scratch, start with `docs/System_Setup.md` first.

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

Use this when the backend has already been built and the Quest app is installed or ready to install:

```bash
cd ros_backend1.1
cp .env.example .env 2>/dev/null || true
./scripts/backend11_lifecycle.sh mode_wired
./scripts/backend11_lifecycle.sh bringup_dual
./scripts/backend11_lifecycle.sh status
```

`bringup_dual` starts the container, applies wired ADB reverse tunnels if the Quest is connected, generates the Gazebo world from task profiles, starts dual Gazebo, starts dual MoveIt Servo, starts the Quest TCP receiver, starts both arm control pipelines, and starts Unity synchronization.

## Install Prebuilt Quest App

Use this when a prebuilt APK exists locally under `UnityApp/App_Build/`. APK files are ignored by Git, so another computer may need to build the app in Unity first.

1. Connect the Quest 3 by USB.
2. Accept the USB debugging prompt in the headset.
3. Confirm ADB sees the headset:

```bash
adb devices
```

Expected: the Quest appears as `device`, not `unauthorized`.

4. Install the newest local APK, for example:

```bash
adb install -r -d 'UnityApp/App_Build/R&U_7.0.1.apk'
```

If the APK name is different, list available builds first:

```bash
ls UnityApp/App_Build/*.apk
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

The old in-headset control-mode switch is disabled by default. Optional keyboard/thumbstick/gamepad control modes are activated from terminal commands instead.

## Optional Controllers

Optional controllers are right-arm overrides by default. The left Quest controller and left robot arm continue normally.

### Thumbstick / Gamepad Mode

This keeps the Quest app running, but tells only the right-arm backend mapper to ignore right-hand pose following and use the gamepad/thumbstick fields instead.

Enable right-arm thumbstick mode:

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
- `Left trigger`: move up.
- `Left grip`: move down.
- `Right stick Y`: angular Y.
- `Right stick X`: angular Z.

Backend parameters live in:

```text
ros_backend1.1/src/teleop_bridge/config/teleop_tuning_dual_right.yaml
```

Main tuning keys:

- `gamepad_deadband`
- `gamepad_linear_speed_xyz`
- `gamepad_linear_sign_xyz`
- `gamepad_angular_speed_xyz`
- `gamepad_angular_sign_xyz`

### Keyboard Override

This starts an interactive terminal keyboard controller for the right arm only. It temporarily stops the right-arm target-to-Servo bridge so right Quest controller pose packets still arrive but do not command the right robot arm. The left arm pipeline stays active.

Start right-arm keyboard override:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh right_keyboard
```

Key map:

- `W/S`: robot `+/-X`.
- `A/D`: robot `+/-Y`.
- `Q/E`: robot `+/-Z`.
- `U/J`: roll, angular `+/-X`.
- `I/K`: pitch, angular `+/-Y`.
- `O/L`: yaw, angular `+/-Z`.
- `Space`: stop immediately.
- `X` or `Ctrl-C`: quit.

After quitting, restore right-arm headset control:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh right_keyboard_off
```

### SpaceMouse

Placeholder. SpaceMouse support has not been implemented yet.

Intended future behavior:

```bash
cd ros_backend1.1
# TODO: ./scripts/backend11_lifecycle.sh right_spacemouse
```

## Step-By-Step Bringup With Checkpoints

Use this when validating a new machine or debugging.

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
- `allow_unity_control_mode_switch`: default `false`; keep false unless intentionally restoring old UI mode switching.
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
