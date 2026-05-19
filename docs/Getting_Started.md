# Getting Started

This is the day-to-day guide for running the system after the repository has been cloned and Unity has been opened once. For full replication/setup from a fresh computer, start with `docs/System_Setup.md`.

## What This Runs

- Part 1: Quest/Unity input -> `/received_pose_states`.
- Part 2: mapping -> `/target_twist_states`.
- Part 3: MoveIt Servo + gripper execution in Gazebo.
- Part 4: ROS/Gazebo state sync back to Unity.

Current defaults:

| Item | Value |
| --- | --- |
| Container | `motion_planner_10` |
| Host Quest TCP port | `127.0.0.1:5026` |
| Container Quest TCP listener | `5005` |
| Host ROS-TCP port | `127.0.0.1:10001` |
| Container ROS-TCP endpoint | `10000` |
| Unity scene | `Assets/Scenes/Ur5e_Working 1.unity` |

## Fast Start

Use this when the system was recently built and is healthy:

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh bringup_wired
./scripts/backend10_lifecycle.sh status
```

Then build/run the Unity app on Quest or launch the already-installed app.

## Step-By-Step Start With Checkpoints

Use this when validating a new machine or debugging.

### 1. Set Variables

```bash
cd ros_backend1.0
CONTAINER=motion_planner_10
ROS_ENV='source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash'
```

### 2. Start/Build Container

```bash
./scripts/backend10_lifecycle.sh restart_container
./scripts/backend10_lifecycle.sh build_ws
./scripts/backend10_lifecycle.sh build_receiver
```

Checkpoint:

```bash
docker ps --filter name=motion_planner_10
docker port motion_planner_10
```

Expected mappings:

- `10000/tcp -> 127.0.0.1:10001`
- `5005/tcp -> 127.0.0.1:5026`

### 3. Enable Wired Quest USB Tunnel

Connect Quest by USB and accept the USB debugging prompt.

```bash
./scripts/backend10_lifecycle.sh wired_on
./scripts/backend10_lifecycle.sh wired_status
```

Expected:

- `adb devices` shows the headset as `device`.
- `adb reverse --list` includes `tcp:5026 tcp:5026`.
- `adb reverse --list` includes `tcp:10001 tcp:10001`.

### 4. Start Gazebo Simulation

```bash
./scripts/backend10_lifecycle.sh start_sim
```

Checkpoint:

```bash
docker exec "$CONTAINER" bash -lc "tail -n 80 /tmp/run_tabletop_sim.log"
```

### 5. Start Servo

```bash
./scripts/backend10_lifecycle.sh start_servo
```

Checkpoint:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 control list_controllers"
docker exec "$CONTAINER" bash -lc "tail -n 80 /tmp/servo_gz.log"
```

### 6. Start Receiver

```bash
./scripts/backend10_lifecycle.sh start_receiver
```

Check receiver logs:

```bash
docker exec "$CONTAINER" bash -lc "tail -f /tmp/qcr.log"
```

Check received controller state:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /received_pose_states"
```

Healthy signs:

- `/tmp/qcr.log` shows `TCP client connected`.
- `/tmp/qcr.log` shows `RX xx.x Hz ... stale=False`.
- `/received_pose_states` has `source: quest_right_controller`.

### 7. Start Mapper And Command Nodes

```bash
./scripts/backend10_lifecycle.sh start_part23
```

Check logs/topics:

```bash
docker exec "$CONTAINER" bash -lc "tail -f /tmp/part2_mapper.log"
docker exec "$CONTAINER" bash -lc "tail -f /tmp/part3_reset_manager.log"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /target_twist_states"
```

### 8. Start Unity Sync

```bash
./scripts/backend10_lifecycle.sh start_part4
```

Checkpoint:

```bash
docker exec "$CONTAINER" bash -lc "tail -f /tmp/part4_tcp_endpoint.log"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic list | rg /unity_sync/"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /unity_sync/Sync_RedCube_pose --once"
```

### 9. Final Status

```bash
./scripts/backend10_lifecycle.sh status
```

## Unity/Quest Runtime Checklist

Unity sender target:

```text
targetIP = 127.0.0.1
targetPort = 5026
```

Unity ROS settings:

```text
ROS IP Address = 127.0.0.1
ROS Port = 10001
```

Why this works:

- Quest connects to its own loopback `127.0.0.1:5026`.
- `adb reverse` carries that stream over USB to the host `127.0.0.1:5026`.
- Docker forwards that host port into container port `5005`.
- Unity ROS-TCP also connects to Quest loopback `127.0.0.1:10001`.
- `adb reverse` carries that stream to host `127.0.0.1:10001`.
- Docker forwards that into container port `10000`.

Unity scene expectations:

- Hand/controller sender enabled.
- Unity robot visualization subscribers enabled.
- Unity object sync manager enabled.
- If using noVNC Gazebo view: `http://localhost:6080/vnc.html`.

## Headset Controls

### Right Controller

- `Grip hold`: engage robot teleop. Robot follows your hand only while this is held.
- `Trigger tap`: toggle gripper open / close.
- `A hold`: rotation mode for hand-pose control.
- `B tap`: reset robot and table objects.
- `Thumbstick press`: clutch / pause hand following. Hold to move your hand without moving the robot; release to reset the hand reference at the new hand position.

### Left Controller

- `X tap`: start / stop wrist-camera recording.
- `Y tap`: switch between hand-pose mode and thumbstick/gamepad mode.

### Thumbstick / Gamepad Mode

- `Left stick Y`: forward / back.
- `Left stick X`: left / right.
- `Left trigger`: move up.
- `Left grip`: move down.
- `Right stick Y`: rotate around robot angular Y.
- `Right stick X`: rotate around robot angular Z.

### Floating Control Panel

- Release `Right Grip` so teleop is not engaged.
- Point the left controller at the floating panel.
- Hold `Left Trigger` and move the controller to drag the panel.
- Release `Left Trigger` to drop the panel.

### In-Scene Table Board

The Unity scene has a `TeleopInstructionBoard` component on `NetworkSender`.

It creates a scene object named `Teleop_Button_Instructions` near the table. If you want to move it manually, open the Unity hierarchy and edit `Teleop_Button_Instructions`.

If the board does not appear, select `NetworkSender`, find `Teleop Instruction Board`, and enable `Rebuild Instruction Board From Settings`.

## Optional Keyboard Controller

Use this to drive the arm from the host keyboard instead of the headset controller:

```bash
./scripts/backend10_lifecycle.sh keyboard
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

Important behavior:

- `keyboard` publishes directly to `/servo_node/delta_twist_cmds`.
- `keyboard` stops `target_twist_to_servo_cmd` first so keyboard and headset commands do not fight.
- After quitting keyboard mode, run `./scripts/backend10_lifecycle.sh start_part23` to restore headset teleop.

## Live Camera Checks

Gazebo gripper camera topic:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /gripper_camera/camera_info --once"
```

Live image view:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 run rqt_image_view rqt_image_view /gripper_camera/image_raw"
```

Camera pose:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 run tf2_ros tf2_echo tool0 robotiq_hande_camera_link"
```

## Recovery Ladder

Use the smallest recovery first.

### Level 1: Node Restart

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh stop_nodes
./scripts/backend10_lifecycle.sh start_receiver
./scripts/backend10_lifecycle.sh start_part23
./scripts/backend10_lifecycle.sh start_part4
./scripts/backend10_lifecycle.sh status
```

### Level 2: Rebuild Workspace

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh build_ws
./scripts/backend10_lifecycle.sh bringup_all
```

### Level 3: Clean Container Restart

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh restart_container
./scripts/backend10_lifecycle.sh build_ws
./scripts/backend10_lifecycle.sh bringup_all
```

### Level 4: Docker/USB Reset

Use this if port forwarding or USB forwarding is broken:

- Reconnect the USB cable.
- Rerun `./scripts/backend10_lifecycle.sh wired_on`.
- Restart Docker Desktop if needed.
- Run the Level 3 sequence again.

### Level 5: TCP Port Bump

Temporary workaround if the host port is stuck:

```bash
cd ros_backend1.0
./scripts/bump_tcp_port.sh
# or
./scripts/bump_tcp_port.sh 5027
```

Then update Unity `HandPoseSender.targetPort`, rebuild the Quest app, keep `targetIP = 127.0.0.1`, and rerun:

```bash
./scripts/backend10_lifecycle.sh wired_on
```

## RX 0.0 Decision Path

1. Check status:

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh status
```

2. Check host ingress on TCP `5026`:

```bash
sudo tcpdump -ni any 'tcp port 5026'
```

3. Interpret:

- No packets on host: Unity/Quest sender path issue.
- Packets on host but none in container: Docker TCP forwarding issue.
- Packets in container but `tracked=false`: sender payload/tracking state issue.

## Tuning Locations

Main tuning file:

```text
ros_backend1.0/src/teleop_bridge/config/teleop_tuning.yaml
```

Important parameters:

- `target_twist_reset_manager.ros__parameters.home_joint_positions`.
- `target_twist_reset_manager.ros__parameters.scene_reset_lift_z`.
- `keyboard_servo_cmd.ros__parameters.linear_speed_xyz`.
- `keyboard_servo_cmd.ros__parameters.linear_sign_xyz`.
- `keyboard_servo_cmd.ros__parameters.angular_speed_xyz`.
- `keyboard_servo_cmd.ros__parameters.angular_sign_xyz`.
- `keyboard_servo_cmd.ros__parameters.key_timeout_sec`.

Scene-object sync topics:

- `/unity_sync/Sync_RedCube_pose`.
- `/unity_sync/Sync_GreenCube_pose`.
- `/unity_sync/Sync_RedCylinder_pose`.
- `/unity_sync/Sync_GreenCylinder_pose`.
- `/unity_sync/Sync_Plate_A_pose`.
- `/unity_sync/Sync_Plate_B_pose`.

After editing tuning:

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh start_part23
./scripts/backend10_lifecycle.sh status
```

If you changed Python code:

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh build_ws
./scripts/backend10_lifecycle.sh start_part23
./scripts/backend10_lifecycle.sh status
```

## Shutdown

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh safe_down
```
