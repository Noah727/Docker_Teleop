# ros_backend0.4 — In-Container Runbook (Current Setup)

This is a companion guide to `README.md` with commands intended to be run **inside** the container shell.

Use this when you prefer manual multi-terminal bring-up and want to avoid host-side `docker exec` command confusion.

## noVNC GUI Access (Password + How To Use)

- noVNC URL: `http://localhost:6080/vnc.html`
- If accessing from another device on the same network: `http://<MAC_LAN_IP>:6080/vnc.html`
- noVNC password: none (leave it blank and click Connect)
- Why: `start_desktop.sh` starts `x11vnc` with `-nopw`

What you see:
- This is the XFCE desktop running inside the container.
- Open a GUI terminal there via Applications -> Terminal Emulator.

If you want a normal shell from host instead of GUI:
- `docker exec -it motion_planner_04 bash`

If you see the `noah` unlock screen:
- There is no usable unlock password. The account is locked (`passwd -S noah` shows `L`, `/etc/shadow` uses `!`).
- Recover by restarting container: `docker restart motion_planner_04`
- Optional lock-screen workaround: `docker exec motion_planner_04 bash -lc 'pkill -f xfce4-screensaver || true; pkill -f light-locker || true'`

---

## Recommended Gazebo World (Tabletop + Cube, Empty-Style)

Use the custom world at:
- `/home/noah/ws_moveit/simulation/worlds/ur_hande_tabletop.sdf`

World layout:
- Table (`1.6 x 1.2 x 0.8 m`) centered at `x=0, y=0`, top surface at `z=0`
- Target cube (`0.06 x 0.06 x 0.06 m`) at `x=0.45, y=0.20, z=0.03`
- UR5e + Hand-E spawns fixed at `x=0, y=0, z=0` (on table top)

Inside container, start sim + spawn robot:
- `/home/noah/ws_moveit/simulation/launch/run_tabletop_sim.sh`

Check sim speed (RTF now and after 15s):
- `/home/noah/ws_moveit/simulation/launch/check_tabletop_rtf.sh`

Notes:
- `run_tabletop_sim.sh` clears old Gazebo/ROS test processes first.
- `SIM_HEADLESS=1` starts headless Gazebo; `SIM_HEADLESS=0` starts Gazebo GUI (visible in noVNC).
- `run_tabletop_sim.sh` now auto-spawns `joint_state_broadcaster`, `joint_group_velocity_controller`, and `hande_position_controller`.
- Gazebo log: `/tmp/gz_tabletop.log`
- RSP log: `/tmp/rsp_tabletop.log`

---

## Recommended Bring-Up (Gazebo + Servo + Teleop + Gripper)

This is the current recommended path for UR5e + Hand-E in Gazebo with Quest teleoperation.

### A) Start container + noVNC (host)

- `cd ros_backend0.4`
- `docker compose up -d --build`
- `docker restart motion_planner_04`
- Open noVNC: `http://localhost:6080/vnc.html` (password: blank)

### B) Enter container and source env (all tabs)

- `docker exec -it motion_planner_04 bash`
- `source /opt/ros/humble/setup.bash`
- `source /home/noah/ws_moveit/install/setup.bash`

If needed, build updated packages once:
- `cd /home/noah/ws_moveit`
- `colcon build --symlink-install --packages-select teleop_bridge servo_test_config ur_hande_description robotiq_hande_description`
- `source /home/noah/ws_moveit/install/setup.bash`

### C) Start simulation world

GUI in noVNC:
- `SIM_HEADLESS=0 /home/noah/ws_moveit/simulation/launch/run_tabletop_sim.sh`

Headless benchmark mode:
- `SIM_HEADLESS=1 /home/noah/ws_moveit/simulation/launch/run_tabletop_sim.sh`
- `/home/noah/ws_moveit/simulation/launch/check_tabletop_rtf.sh`

Target:
- RTF around `1.0` (acceptable `>= 0.95` in headless mode).

### D) Start Servo + teleop nodes

Terminal 1 (Servo node for Gazebo velocity controller):
- `ros2 launch servo_test_config servo_gz.launch.py`

Terminal 2 (Quest UDP -> pose + angular + gripper hold commands):
- `ros2 run teleop_bridge hand_to_servo --ros-args -p scale_xyz:="[1.0,1.0,1.0]" -p offset_xyz:="[0.0,0.0,0.0]" -p min_xyz:="[0.15,-0.50,0.05]" -p max_xyz:="[0.75,0.50,0.70]"`

Terminal 3 (Pose + angular -> MoveIt Servo twist):
- `ros2 run teleop_bridge pose_to_servo_twist --ros-args -p input_pose_topic:=/servo_node/target_pose`

Terminal 4 (Hold-to-move gripper position ramp):
- `ros2 run teleop_bridge gripper_hold_to_position`

### E) Verify controllers and command flow

Controllers active:
- `ros2 control list_controllers`
- `ros2 control list_hardware_interfaces`

Expected:
- `joint_group_velocity_controller` active
- `hande_position_controller` active
- gripper position interface claimed

Servo and twist flow:
- `ros2 topic echo /servo_node/delta_twist_cmds --once`
- `ros2 topic echo /servo_node/status --once`

Gripper flow:
- `ros2 topic echo /hande_position_controller/commands`
- `ros2 topic echo /joint_states`

Quest button mapping:
- `A` held: rotation input enabled (angular twist)
- Right grip held: close gripper continuously
- Right trigger held: open gripper continuously
- Release close/open: gripper stops at current opening

---

## 0) Start container (host) and open shells

On macOS host (outside container):
- `cd ros_backend0.4`
- `docker compose up -d --build`
- `docker restart motion_planner_04`

Open 5 terminal tabs and enter the same container in each:
- `docker exec -it motion_planner_04 bash`

In every container tab, source the environment once:
- `source /opt/ros/humble/setup.bash`
- `source /home/noah/ws_moveit/install/setup.bash`

If overlay is missing, build once in any container tab:
- `cd /home/noah/ws_moveit`
- `colcon build --symlink-install --packages-select teleop_bridge servo_test_config ur_hande_description robotiq_hande_description`
- `source /home/noah/ws_moveit/install/setup.bash`

---

## 1) Terminal A — Servo stack

Run:
- `ros2 launch servo_test_config servo_test.launch.py use_fake_hardware:=true headless_mode:=true`

Expected:
- `/servo_node` and `/controller_manager` appear in `ros2 node list`.

---

## 2) Terminal B — Unity TCP endpoint

Run:
- `ros2 launch ros_tcp_endpoint endpoint.py`

Unity side:
- ROSConnection host = your Mac IP
- ROSConnection port = `10001`
- Subscriber topic = `/joint_trajectory_controller/joint_trajectory`

---

## 3) Terminal C — Recovery + Servo services

Run once after A/B are up:
- `ros2 topic pub --once /joint_trajectory_controller/joint_trajectory trajectory_msgs/msg/JointTrajectory "{joint_names: [shoulder_pan_joint, shoulder_lift_joint, elbow_joint, wrist_1_joint, wrist_2_joint, wrist_3_joint], points: [{positions: [0.35, -1.0, 1.7, -1.2, -1.4, 0.2], time_from_start: {sec: 3, nanosec: 0}}]}"`

Then:
- `ros2 service call /servo_node/start_servo std_srvs/srv/Trigger "{}" && ros2 service call /servo_node/reset_servo_status std_srvs/srv/Empty "{}"`
- `ros2 service call /servo_node/reset_servo_status std_srvs/srv/Empty "{}"`

What these do:
- `start_servo`: enables Servo command processing.
- `reset_servo_status`: clears latched halt/error state.

---

## 4) Terminal D — Hand UDP to target pose

Run:
- `ros2 run teleop_bridge hand_to_servo --ros-args -p scale_xyz:="[1.0,1.0,1.0]" -p offset_xyz:="[0.0,0.0,0.0]" -p min_xyz:="[0.15,-0.50,0.05]" -p max_xyz:="[0.75,0.50,0.70]"`

Quick rebuild + restart (inside container, copy/paste):
- `cd /home/noah/ws_moveit && colcon build --symlink-install --packages-select teleop_bridge && source /home/noah/ws_moveit/install/setup.bash && pkill -f "ros2 run teleop_bridge hand_to_servo" || true && ros2 run teleop_bridge hand_to_servo --ros-args -p scale_xyz:="[1.0,1.0,1.0]" -p offset_xyz:="[0.0,0.0,0.0]" -p min_xyz:="[0.15,-0.50,0.05]" -p max_xyz:="[0.75,0.50,0.70]"`

Current mapping in node:
- Unity `(x,y,z)` -> ROS `(-x, y, z)` (default)
- Then: `scaled = mapped * scale_xyz + offset_xyz`
- Then: clipped by `min_xyz/max_xyz`

Preset mapping commands (quick switch):
- Default (`rx=-ux, ry=uy, rz=uz`):
	- `ros2 run teleop_bridge hand_to_servo --ros-args -p map_axes:='["x","y","z"]' -p map_signs:="[-1,1,1]" -p scale_xyz:="[1.0,1.0,1.0]" -p offset_xyz:="[0.0,0.0,0.0]" -p min_xyz:="[0.15,-0.50,0.05]" -p max_xyz:="[0.75,0.50,0.70]"`
- Unity-forward to robot-x style (`rx=uz, ry=-ux, rz=uy`):
	- `ros2 run teleop_bridge hand_to_servo --ros-args -p map_axes:='["z","x","y"]' -p map_signs:="[1,-1,1]" -p scale_xyz:="[1.0,1.0,1.0]" -p offset_xyz:="[0.0,0.0,0.0]" -p min_xyz:="[0.15,-0.50,0.05]" -p max_xyz:="[0.75,0.50,0.70]"`
- Identity (`rx=ux, ry=uy, rz=uz`):
	- `ros2 run teleop_bridge hand_to_servo --ros-args -p map_axes:='["x","y","z"]' -p map_signs:="[1,1,1]" -p scale_xyz:="[1.0,1.0,1.0]" -p offset_xyz:="[0.0,0.0,0.0]" -p min_xyz:="[0.15,-0.50,0.05]" -p max_xyz:="[0.75,0.50,0.70]"`

---
ros2 run teleop_bridge hand_to_servo --ros-args -p map_axes:='["z","x","y"]' -p scale_xyz:="[1.0,1.0,1.0]" -p offset_xyz:="[-0.5,0.5,-2.0]" -p min_xyz:="[-2.0,-2.0,-2.0]" -p max_xyz:="[2.00,2.00,2.00]"

ros2 run teleop_bridge hand_to_servo --ros-args -p map_axes:='["x","z","y"]' -p scale_xyz:="[1.0,1.0,1.0]" -p offset_xyz:="[0.0,-1.0,0.0]" -p min_xyz:="[-1.0,-1.0,-1.0]" -p max_xyz:="[1.00,1.00,1.00]"


## 5) Terminal E — Pose to Servo twist

Run:
- `ros2 run teleop_bridge pose_to_servo_twist --ros-args -p input_pose_topic:=/servo_node/target_pose`

---

## 6) Optional monitor tab

Run these checks while A–E are active:
- `ros2 node list`
- `ros2 topic echo /servo_node/target_pose --once --no-lost-messages`
- `ros2 topic echo /servo_node/delta_twist_cmds --once --no-lost-messages`
- `ros2 topic echo /joint_trajectory_controller/joint_trajectory --once --no-lost-messages`
- `ros2 topic echo /servo_node/status --once --no-lost-messages`

Healthy flow:
- hand pose updates -> `/servo_node/target_pose`
- converter outputs -> `/servo_node/delta_twist_cmds`
- Servo outputs changing trajectory -> `/joint_trajectory_controller/joint_trajectory`
- Unity UR5e animates from trajectory topic

---

## Common issues

### `bash: docker: command not found`
You are already **inside** the container. Do not run `docker exec ...` there. Run ROS commands directly.

### `Node not found` for `/hand_to_servo`
The node is not currently running in any tab. Start Terminal D command first.

### Robot only moves in a plane
Usually due to mapping limits/clamping. Temporarily widen bounds to test:
- `-p min_xyz:="[-2,-2,-2]" -p max_xyz:="[2,2,2]"`

### Purple target jumps between hand and a fixed point
Often tracking dropouts/default hand sample. Confirm Unity sender hand refs are valid and tracked, then check `/servo_node/target_pose` continuity.

### Input system warning from URDF `Controller`
If using new Unity Input System only, URDF keyboard `Controller` may throw legacy input warnings. Prefer ROS-driven subscribers and keep drive initialization in `Ur5eTrajectorySubscriber`.

---

## Stop all ROS processes (inside container)

- `pkill -f "ros2 launch servo_test_config servo_test.launch.py"`
- `pkill -f "ros2 launch ros_tcp_endpoint endpoint.py"`
- `pkill -f "ros2 run teleop_bridge hand_to_servo"`
- `pkill -f "ros2 run teleop_bridge pose_to_servo_twist"`

Use with caution if other ROS jobs are running in the same container.
