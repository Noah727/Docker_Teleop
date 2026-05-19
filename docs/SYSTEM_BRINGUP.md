Helper scripts now live in `scripts/`.

See also:
- `docs/WIRED_WIRELESS_SETUP.md` for full wired and wireless setup instructions.
- `docs/WORLD_FRAME_MAPPING.md` for the 1.0 world-delta hand-to-EE mapping behavior.

# System Bringup (ros_backend1.0)

Last updated: April 6, 2026

Scope:
- Part 1: Quest/Unity input -> `/received_pose_states`
- Part 2: Mapping -> `/target_twist_states`
- Part 3: Servo + gripper execution in Gazebo
- Part 4: ROS/Gazebo state sync back to Unity

Current network defaults:
- Container TCP listener: `5005`
- ROS-TCP endpoint in container: `10000`
- Host TCP publish port: `127.0.0.1:5026`
- Quest wired target: `127.0.0.1:5026` through `adb reverse`
- Quest ROS-TCP target: `127.0.0.1:10001` through `adb reverse`
- Container name: `motion_planner_10`
- Compose sets `AUTO_COLCON_BUILD=0` (workspace build is manual via lifecycle script)

---

CONTAINER=motion_planner_10
ROS_ENV='source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash'

## 1) Fast Bringup (When System Is Healthy)

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh bringup_wired
./scripts/backend10_lifecycle.sh status
```

---

## 2) Step-by-Step Bringup With Checkpoints (Recommended)

Use this when validating each stage or debugging.

### Setup variables

```bash
cd ros_backend1.0
CONTAINER=motion_planner_10
ROS_ENV='source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash'
```

### Step A: Restart container and build

```bash
./scripts/backend10_lifecycle.sh restart_container
./scripts/backend10_lifecycle.sh build_ws
./scripts/backend10_lifecycle.sh build_receiver
```

Checkpoint A:

```bash
docker ps --filter name=motion_planner_10
docker port motion_planner_10
```

Expected mapping:
- `10000/tcp -> 127.0.0.1:10001`
- `5005/tcp -> 127.0.0.1:5026`

### Step A1: Enable the wired USB TCP tunnel

Connect the Quest by USB and accept the headset's USB debugging prompt once.

```bash
./scripts/backend10_lifecycle.sh wired_on
./scripts/backend10_lifecycle.sh wired_status
```

Expected result:
- `adb devices` shows the headset as `device`
- `adb reverse --list` shows `tcp:5026 tcp:5026`
- `adb reverse --list` shows `tcp:10001 tcp:10001`

### Step B: Start simulation

```bash
./scripts/backend10_lifecycle.sh start_sim
```

Checkpoint B:

```bash
docker exec "$CONTAINER" bash -lc "tail -n 80 /tmp/run_tabletop_sim.log"
```

### Step C: Start servo

```bash
./scripts/backend10_lifecycle.sh start_servo
```

Checkpoint C:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 control list_controllers"
docker exec "$CONTAINER" bash -lc "tail -n 80 /tmp/servo_gz.log"
```

### Optional: Keyboard-only controller

Use this when you want to drive the arm from the host keyboard instead of the headset controller.

```bash
./scripts/backend10_lifecycle.sh keyboard
```

Key map:
- `W/S`: robot `+/-X`
- `A/D`: robot `+/-Y`
- `Q/E`: robot `+/-Z`
- `U/J`: roll, angular `+/-X`
- `I/K`: pitch, angular `+/-Y`
- `O/L`: yaw, angular `+/-Z`
- `Space`: stop immediately
- `X` or `Ctrl-C`: quit

Important behavior:
- `keyboard` publishes directly to `/servo_node/delta_twist_cmds`.
- `keyboard` stops `target_twist_to_servo_cmd` first so keyboard and headset commands do not fight each other.
- After quitting keyboard mode, run `./scripts/backend10_lifecycle.sh start_part23` to restore headset/controller teleop.

### Step D: Start receiver (Part 1)

```bash
./scripts/backend10_lifecycle.sh start_receiver
```

Checkpoint D (live watch):

```bash
# Terminal 1: receiver runtime log (should show TCP client connected + RX > 0)
docker exec "$CONTAINER" bash -lc "tail -f /tmp/qcr.log"

# Terminal 2: live topic stream
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /received_pose_states"
```

Healthy signs:
- `/tmp/qcr.log` shows `TCP client connected`.
- `/tmp/qcr.log` shows `RX xx.x Hz ... stale=False`.
- `/received_pose_states` has `source: quest_right_controller`.

### Step E: Start Part 2 + Part 3 nodes

```bash
./scripts/backend10_lifecycle.sh start_part23
```

Checkpoint E (live watch):

```bash
# Terminal 1
docker exec "$CONTAINER" bash -lc "tail -f /tmp/part2_mapper.log"

# Terminal 2
docker exec "$CONTAINER" bash -lc "tail -f /tmp/part3_reset_manager.log"

# Terminal 3
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /target_twist_states"
```

### Step F: Start Part 4 sync

```bash
./scripts/backend10_lifecycle.sh start_part4
```

Checkpoint F:

```bash
docker exec "$CONTAINER" bash -lc "tail -f /tmp/part4_tcp_endpoint.log"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic list | rg /unity_sync/"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /unity_sync/Sync_RedCube_pose --once"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /gripper_camera/camera_info --once"
```

Live gripper camera view:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 run rqt_image_view rqt_image_view /gripper_camera/image_raw"
```

Live gripper camera pose / angle:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 run tf2_ros tf2_echo tool0 robotiq_hande_camera_link"
```

### Step G: Final status snapshot

```bash
./scripts/backend10_lifecycle.sh status
```

---

## 3) Unity/Quest Runtime Checklist

Unity sender target:
- `targetIP = 127.0.0.1`
- `targetPort = 5026`

Unity ROS Settings:
- `ROS IP Address = 127.0.0.1`
- `ROS Port = 10001`

Why this works:
- the Quest app connects to its own loopback `127.0.0.1:5026`
- `adb reverse` carries that TCP stream over USB to the Mac's loopback `127.0.0.1:5026`
- Docker publishes that host port into the container on `5005`
- the Unity ROS connector also connects to Quest loopback `127.0.0.1:10001`
- `adb reverse` carries that ROS-TCP stream to the Mac's loopback `127.0.0.1:10001`
- Docker publishes that host port into the container on `10000`

Unity scene expectations:
- Hand/controller sender enabled
- Unity robot visualization subscribers enabled
- If using noVNC Gazebo view: `http://localhost:6080/vnc.html`

---

## 4) Recovery Ladder

Use smallest recovery first.

### Level 1: Node-only restart

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh stop_nodes
./scripts/backend10_lifecycle.sh start_receiver
./scripts/backend10_lifecycle.sh start_part23
./scripts/backend10_lifecycle.sh start_part4
./scripts/backend10_lifecycle.sh status
```

### Level 2: Rebuild workspace + bringup

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh build_ws
./scripts/backend10_lifecycle.sh bringup_all
```

### Level 3: Clean container restart

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh restart_container
./scripts/backend10_lifecycle.sh build_ws
./scripts/backend10_lifecycle.sh bringup_all
```

### Level 4: Docker-level reset (only if port forwarding is broken)

Symptoms:
- `adb devices` shows no Quest
- or `adb reverse --list` is missing `tcp:5026 tcp:5026`
- or `adb reverse --list` is missing `tcp:10001 tcp:10001`
- or container listener on `5005` sees zero.

Then:
- Reconnect the USB cable
- Re-run `./scripts/backend10_lifecycle.sh wired_on`
- Restart Docker Desktop if needed
- Run Level 3 sequence again

### Level 5: Quick TCP Port Bump (temporary workaround)

```bash
cd ros_backend1.0
./scripts/bump_tcp_port.sh
# or
./scripts/bump_tcp_port.sh 5027
```

Then update Unity `HandPoseSender.targetPort` to the same value and rebuild the Quest app.
Keep `targetIP = 127.0.0.1`, then re-run `./scripts/backend10_lifecycle.sh wired_on`.

---

## 5) Part 1 RX 0.0 Decision Path

1. Check status:

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh status
```

2. Check host ingress on TCP 5026:

```bash
sudo tcpdump -ni any 'tcp port 5026'
```

3. Interpret:
- No packets on host -> Unity/Quest sender path issue (app foreground, wrong IP/port).
- Packets on host but none in container -> Docker TCP forwarding issue.
- Packets in container but `tracked=false` -> sender payload/tracking state issue.

---

## 6) Tuning + Initial Reset Pose

Main tuning file:
- `ros_backend1.0/src/teleop_bridge/config/teleop_tuning.yaml`
- `start_part23` loads this file directly via `--ros-args --params-file`
- `keyboard` also loads this file directly via `--ros-args --params-file`

Reset/home initial joint pose:
- `target_twist_reset_manager.ros__parameters.home_joint_positions`

Scene-object reset lift:
- `target_twist_reset_manager.ros__parameters.scene_reset_lift_z`
- Default `0.01` means dynamic table objects reset 1 cm above their SDF setup pose, then physics settles them onto the table.

Keyboard controller tuning:
- `keyboard_servo_cmd.ros__parameters.linear_speed_xyz`
- `keyboard_servo_cmd.ros__parameters.linear_sign_xyz`
- `keyboard_servo_cmd.ros__parameters.angular_speed_xyz`
- `keyboard_servo_cmd.ros__parameters.angular_sign_xyz`
- `keyboard_servo_cmd.ros__parameters.key_timeout_sec`

Scene-object sync:
- Part 4 publishes `/unity_sync/Sync_RedCube_pose`
- Part 4 publishes `/unity_sync/Sync_GreenCube_pose`
- Part 4 publishes `/unity_sync/Sync_RedCylinder_pose`
- Part 4 publishes `/unity_sync/Sync_GreenCylinder_pose`
- Part 4 publishes `/unity_sync/Sync_Plate_A_pose`
- Part 4 publishes `/unity_sync/Sync_Plate_B_pose`

After editing tuning:

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh start_part23
./scripts/backend10_lifecycle.sh status
```

If you changed Python code in `teleop_bridge`:

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh build_ws
./scripts/backend10_lifecycle.sh start_part23
./scripts/backend10_lifecycle.sh status
```

---

## 7) Shutdown

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh safe_down
```
