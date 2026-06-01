# Dual-Arm Getting Started

Backend folder: `ros_backend1.1`
Lifecycle script: `./scripts/backend11_lifecycle.sh`
Docker container: `motion_planner_11`

This file is the practical command sheet for running, restarting, and debugging the dual-arm backend.

## Quick Start

Use this for the normal dual-arm MR/Quest workflow:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh bringup_dual
```

`bringup_dual` starts the container, applies wired ADB reverse tunnels if wired mode is enabled and the Quest is connected, builds the ROS workspace if needed, starts Gazebo, starts dual Servo, starts the Quest TCP receiver, starts the dual teleop mapping pipeline, starts Unity synchronization, and prints status.

If you want to run each step manually:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh up_container
./scripts/backend11_lifecycle.sh build_ws
./scripts/backend11_lifecycle.sh start_dual_sim
./scripts/backend11_lifecycle.sh start_dual_servo
./scripts/backend11_lifecycle.sh start_receiver
./scripts/backend11_lifecycle.sh start_dual_part23
./scripts/backend11_lifecycle.sh start_dual_part4
./scripts/backend11_lifecycle.sh status
```

## Restart Gazebo After Closing The Window

If you close the Gazebo GUI window but the Docker container is still running, restart the dual-arm simulation with:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh start_dual_sim
sleep 2
./scripts/backend11_lifecycle.sh start_dual_servo
./scripts/backend11_lifecycle.sh start_dual_part23
./scripts/backend11_lifecycle.sh start_dual_part4
./scripts/backend11_lifecycle.sh status
```

Why not only `start_dual_sim`? It does reopen Gazebo and respawn the robots, but Servo and sync are connected to the simulated controllers/entities. Restarting `start_dual_servo`, `start_dual_part23`, and `start_dual_part4` is the safer path after a Gazebo respawn.

If the container was stopped or the backend feels messy, use a full clean restart:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh safe_down
./scripts/backend11_lifecycle.sh bringup_dual
```

## Full Lifecycle Command Reference

Every command uses the same format:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh <command>
```

### Network Mode

| Command | What It Does |
| --- | --- |
| `mode_wired` | Sets `.env` so Docker ports bind to `127.0.0.1`. Use this for USB/ADB wired Quest testing. Restart the container after changing this. |
| `mode_wireless` | Sets `.env` so Docker ports bind to `0.0.0.0`. Use this for LAN/Wi-Fi Quest testing. Restart the container after changing this. |
| `mode_status` | Prints the configured bind addresses and live Docker port mappings. |
| `wired_on` | Applies ADB reverse tunnels for the Quest hand-control TCP port and ROS-TCP port. |
| `wired_off` | Removes the ADB reverse tunnels. |
| `wired_status` | Prints ADB device status, active reverse tunnels, and Docker port mappings. |

Wired mode mapping:

```text
Quest 127.0.0.1:5026  -> host 127.0.0.1:5026  -> container 5005
Quest 127.0.0.1:10001 -> host 127.0.0.1:10001 -> container 10000
```

In wired mode, Unity should use:

```text
HandPoseSender target IP: 127.0.0.1
HandPoseSender target port: 5026
ROS Settings IP Address: 127.0.0.1
ROS Settings Port: 10001
```

### Container And Build

| Command | What It Does |
| --- | --- |
| `safe_down` | Stops teleop/simulation processes, runs `docker compose down --remove-orphans`, and removes stale `motion_planner_11` containers. Use this before a clean restart. |
| `up_container` | Starts the Docker compose stack without rebuilding the image. |
| `up_container_build` | Rebuilds and starts the Docker compose stack. Use this after Dockerfile/dependency changes. |
| `restart_container` | Runs `safe_down`, then `up_container`. |
| `build_ws` | Builds all required ROS workspace packages inside the container. |
| `build_receiver` | Builds only the Quest TCP receiver package. |
| `stop_nodes` | Stops ROS teleop, simulation launcher, Servo, sync, bridge, and receiver processes inside the running container. It does not stop the container. |

### Combined Bring-Up

| Command | What It Does |
| --- | --- |
| `bringup_dual` | Primary dual-arm command. Starts container, optional wired tunnels, dual Gazebo, dual Servo, receiver, dual teleop, and dual sync. |
| `bringup_all` | Legacy single-arm full bring-up. |
| `bringup_wired` | Legacy single-arm full bring-up with wired ADB reverse tunnels. |

For the current dual-arm MR app, prefer `bringup_dual`.

### Simulation And Servo

| Command | What It Does |
| --- | --- |
| `start_dual_sim` | Starts the dual-arm Gazebo scene with GUI and respawns both UR5e Hand-E robots. |
| `start_dual_servo` | Starts the dual-arm MoveIt Servo launch. |
| `start_sim` | Starts the legacy single-arm Gazebo tabletop scene. |
| `start_servo` | Starts the legacy single-arm Servo launch. |

The dual Gazebo world file is:

```text
simulation/worlds/ur_hande_dual_arm_tabletop.sdf
```

The dual Gazebo launch helper is:

```text
simulation/launch/run_dual_arm_tabletop_sim.sh
```

### Runtime Nodes

| Command | What It Does |
| --- | --- |
| `start_receiver` | Starts `quest_controller_receiver`, the TCP receiver that reads controller/hand data from the Quest app. |
| `start_dual_part23` | Starts both left and right teleop mapping pipelines, Servo command bridges, gripper command bridges, and reset managers. |
| `start_dual_part4` | Starts ROS-TCP endpoint and Gazebo-to-Unity object pose synchronization for the dual-arm world. |
| `start_part23` | Starts the legacy single-arm teleop mapping pipeline. |
| `start_part4` | Starts legacy single-arm Unity sync and ROS-TCP endpoint. |
| `keyboard` | Starts the interactive keyboard controller. This temporarily disables headset-to-Servo output. After quitting, run `start_dual_part23` to restore the dual-arm headset pipeline. |
| `status` | Prints Docker status, live backend processes, TCP listeners, and recent log tails. |

## Part Meaning

`receiver` reads TCP controller data from the Quest app.

`part23` maps controller poses/buttons into target twist/gripper/reset messages, then bridges those commands into Servo and Gazebo controllers.

`part4` publishes Gazebo object poses to Unity through ROS-TCP so Unity visual objects can follow simulation objects.

`sim` starts Gazebo and spawns the robot/world.

`servo` starts MoveIt Servo so velocity commands can move the robot arms.

## Topic Layout

| Purpose | Topic |
| --- | --- |
| Quest receiver compatibility stream | `/received_pose_states` |
| Left arm input stream | `/left_arm/received_pose_states` |
| Right arm input stream | `/right_arm/received_pose_states` |
| Left mapper output | `/left_arm/target_twist_states` |
| Right mapper output | `/right_arm/target_twist_states` |
| Left Servo command | `/left_arm/servo_node/delta_twist_cmds` |
| Right Servo command | `/right_arm/servo_node/delta_twist_cmds` |
| Left Gazebo arm command | `/left_joint_group_velocity_controller/commands` |
| Right Gazebo arm command | `/right_joint_group_velocity_controller/commands` |
| Left gripper command | `/left_hande_position_controller/commands` |
| Right gripper command | `/right_hande_position_controller/commands` |

## Important Logs

All logs below are inside the Docker container.

| Log | Meaning |
| --- | --- |
| `/tmp/run_dual_arm_tabletop_sim.log` | Main dual-arm simulation launcher log. |
| `/tmp/gz_dual_arm_tabletop.log` | Gazebo GUI/server log for the dual-arm world. |
| `/tmp/rsp_left_dual_arm.log` | Left robot state publisher log. |
| `/tmp/rsp_right_dual_arm.log` | Right robot state publisher log. |
| `/tmp/servo_dual_gz.log` | Dual MoveIt Servo launch log. Check this first if arms do not move. |
| `/tmp/qcr.log` | Quest TCP receiver log. Check this if Unity buttons/poses are not arriving. |
| `/tmp/dual_left_mapper.log` | Left controller-to-target mapper log. |
| `/tmp/dual_right_mapper.log` | Right controller-to-target mapper log. |
| `/tmp/dual_left_target_to_servo.log` | Left target-to-Servo bridge log. |
| `/tmp/dual_right_target_to_servo.log` | Right target-to-Servo bridge log. |
| `/tmp/dual_left_target_to_gripper.log` | Left gripper command bridge log. |
| `/tmp/dual_right_target_to_gripper.log` | Right gripper command bridge log. |
| `/tmp/dual_part4_tcp_endpoint.log` | ROS-TCP endpoint log for Unity sync. |
| `/tmp/dual_part4_gz_tf_bridge.log` | Gazebo dynamic pose bridge log. |
| `/tmp/dual_part4_cube_pose.log` | Object pose sync publisher log. |

To inspect a log:

```bash
docker exec motion_planner_11 bash -lc 'tail -n 80 /tmp/servo_dual_gz.log'
```

## Troubleshooting

If Gazebo opens but the dual robots are missing:

```bash
docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
docker exec motion_planner_11 bash -lc 'source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && ign model --list'
```

Expected model names:

```text
left_ur5e_hande
right_ur5e_hande
```

If ROS packages are not found inside the container:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh build_ws
```

If the Quest cannot connect in wired mode:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh wired_status
./scripts/backend11_lifecycle.sh wired_on
```

If the robot fingers work but the robot arms do not move:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh status
docker exec motion_planner_11 bash -lc 'tail -n 80 /tmp/qcr.log'
docker exec motion_planner_11 bash -lc 'tail -n 80 /tmp/dual_left_mapper.log'
docker exec motion_planner_11 bash -lc 'tail -n 80 /tmp/dual_right_mapper.log'
docker exec motion_planner_11 bash -lc 'tail -n 80 /tmp/servo_dual_gz.log'
```

## Unity Pairing

The matching Unity scene is:

```text
UnityApp/Assets/Scenes/GazeboReplica_DualArm_MR.unity
```

The Unity scene should visually replicate the Gazebo workspace and subscribe to the dual-arm object synchronization stream from `start_dual_part4`.
