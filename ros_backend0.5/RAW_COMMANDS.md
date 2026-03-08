# ros_backend0.5 Raw Commands Runbook

Last updated: March 5, 2026

This backend intentionally does not use `part1_receiver_ctl.sh`, `part23_ctl.sh`, or `part4_sync_ctl.sh`.
Use raw `docker exec ... ros2 ...` commands only.

## 0) Container identity and assumptions

- Container name: `motion_planner_05`
- Compose project: `ros_backend05` (from `.env`)
- UDP host port default: `5016` (from `.env`)
- Container UDP listener: `5005`

Note: This stack is intended to run one-at-a-time (not in parallel with `ros_backend0.4`).

## 1) Build and start container

```bash
cd /Users/noahli/ros_unity_project/ros_backend0.5

# Optional: stop old backend if it is running
# docker stop motion_planner_04 2>/dev/null || true

# Start 0.5
DOCKER_DEFAULT_PLATFORM=linux/arm64 docker compose up -d --build

docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}' | grep motion_planner_05
docker port motion_planner_05
```

Expected UDP mapping:

- `5005/udp -> 0.0.0.0:5016`

## 2) Build ROS workspace in container

```bash
CONTAINER=motion_planner_05
docker exec "${CONTAINER}" bash -lc "source /opt/ros/humble/setup.bash && cd /home/noah/ws_moveit && colcon build --packages-select robotiq_hande_description ur_hande_description ur_moveit_config servo_test_config teleop_bridge_msgs teleop_bridge ROS-TCP-Endpoint"
```

Quick package sanity:

```bash
docker exec "${CONTAINER}" bash -lc "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && ros2 pkg list | grep -E 'teleop_bridge$|teleop_bridge_msgs$|servo_test_config$|ros_tcp_endpoint$'"
```

## 3) Part 1: receiver (Quest -> /received_pose_states)

### Start

```bash
CONTAINER=motion_planner_05
docker exec "${CONTAINER}" bash -lc "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && nohup /opt/ros/humble/bin/ros2 run teleop_bridge quest_controller_receiver >/tmp/qcr.log 2>&1 < /dev/null &"
```

### Stop

```bash
CONTAINER=motion_planner_05
docker exec "${CONTAINER}" bash -lc "pkill -f '[q]uest_controller_receiver' || true"
```

### Status / logs

```bash
CONTAINER=motion_planner_05
docker exec "${CONTAINER}" bash -lc "pgrep -fa quest_controller_receiver || true"
docker exec "${CONTAINER}" bash -lc "netstat -anup 2>/dev/null | grep ':5005 ' || true"
docker exec "${CONTAINER}" bash -lc "tail -n 80 /tmp/qcr.log"
```

### Topic checks

```bash
CONTAINER=motion_planner_05
docker exec "${CONTAINER}" bash -lc "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && ros2 topic hz /received_pose_states"
docker exec "${CONTAINER}" bash -lc "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && ros2 topic echo /received_pose_states --once"
```

## 4) Part 2 + Part 3 bridges (mapper/servo/gripper/reset)

### Start all

```bash
CONTAINER=motion_planner_05
docker exec "${CONTAINER}" bash -lc "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && \
pkill -f '[/]teleop_bridge/received_pose_to_target_twist' || true; \
pkill -f '[/]teleop_bridge/target_twist_to_servo_cmd' || true; \
pkill -f '[/]teleop_bridge/target_twist_to_gripper_cmd' || true; \
pkill -f '[/]teleop_bridge/target_twist_reset_manager' || true; \
nohup /opt/ros/humble/bin/ros2 run teleop_bridge received_pose_to_target_twist >/tmp/part2_mapper.log 2>&1 < /dev/null & \
nohup /opt/ros/humble/bin/ros2 run teleop_bridge target_twist_to_servo_cmd >/tmp/part3_target_to_servo.log 2>&1 < /dev/null & \
nohup /opt/ros/humble/bin/ros2 run teleop_bridge target_twist_to_gripper_cmd >/tmp/part3_target_to_gripper.log 2>&1 < /dev/null & \
nohup /opt/ros/humble/bin/ros2 run teleop_bridge target_twist_reset_manager >/tmp/part3_reset_manager.log 2>&1 < /dev/null &"
```

### Stop all

```bash
CONTAINER=motion_planner_05
docker exec "${CONTAINER}" bash -lc "pkill -f '[r]eceived_pose_to_target_twist|[t]arget_twist_to_servo_cmd|[t]arget_twist_to_gripper_cmd|[t]arget_twist_reset_manager' || true"
```

### Status / logs

```bash
CONTAINER=motion_planner_05
docker exec "${CONTAINER}" bash -lc "pgrep -fa 'received_pose_to_target_twist|target_twist_to_servo_cmd|target_twist_to_gripper_cmd|target_twist_reset_manager' || true"
docker exec "${CONTAINER}" bash -lc "for f in /tmp/part2_mapper.log /tmp/part3_target_to_servo.log /tmp/part3_target_to_gripper.log /tmp/part3_reset_manager.log; do echo '===== ' \$f; [ -f \$f ] && tail -n 80 \$f || echo missing; done"
```

### Topic checks

```bash
CONTAINER=motion_planner_05
docker exec "${CONTAINER}" bash -lc "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && ros2 topic echo /target_twist_states --once"
docker exec "${CONTAINER}" bash -lc "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && ros2 topic echo /joint_group_velocity_controller/commands --once"
docker exec "${CONTAINER}" bash -lc "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && ros2 topic echo /hande_position_controller/commands --once"
```

## 5) Simulation + Servo launch

### Start tabletop simulation

```bash
CONTAINER=motion_planner_05
docker exec "${CONTAINER}" bash -lc "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && SIM_HEADLESS=1 /home/noah/ws_moveit/simulation/launch/run_tabletop_sim.sh >/tmp/run_tabletop_sim.log 2>&1 < /dev/null &"
```

### Start MoveIt Servo launch

```bash
CONTAINER=motion_planner_05
docker exec "${CONTAINER}" bash -lc "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && nohup /opt/ros/humble/bin/ros2 launch servo_test_config servo_gz.launch.py >/tmp/servo_gz.log 2>&1 < /dev/null &"
```

### Stop simulation + servo

```bash
CONTAINER=motion_planner_05
docker exec "${CONTAINER}" bash -lc "pkill -f '[r]un_tabletop_sim.sh|[s]ervo_gz.launch.py' || true"
```

### Health checks

```bash
CONTAINER=motion_planner_05
docker exec "${CONTAINER}" bash -lc "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && ros2 control list_controllers"
docker exec "${CONTAINER}" bash -lc "source /opt/ros/humble/setup.bash && /home/noah/ws_moveit/simulation/launch/check_tabletop_rtf.sh"
```

## 6) Part 4 sync (ROS-TCP + cube pose publisher)

### Start

```bash
CONTAINER=motion_planner_05
docker exec "${CONTAINER}" bash -lc "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && \
self=\$\$; \
for p in \$(pgrep -f 'cube_pose_sync_publisher|ros_tcp_endpoint.*endpoint.py|default_server_endpoint' || true); do [ \"\$p\" = \"\$self\" ] && continue; kill -2 \"\$p\" 2>/dev/null || true; done; \
sleep 0.6; \
for p in \$(pgrep -f 'cube_pose_sync_publisher|ros_tcp_endpoint.*endpoint.py|default_server_endpoint' || true); do [ \"\$p\" = \"\$self\" ] && continue; kill \"\$p\" 2>/dev/null || true; done; \
nohup /opt/ros/humble/bin/ros2 launch ros_tcp_endpoint endpoint.py >/tmp/part4_tcp_endpoint.log 2>&1 < /dev/null & \
nohup /opt/ros/humble/bin/ros2 run teleop_bridge cube_pose_sync_publisher >/tmp/part4_cube_pose.log 2>&1 < /dev/null &"
```

### Stop

```bash
CONTAINER=motion_planner_05
docker exec "${CONTAINER}" bash -lc "self=\$\$; for p in \$(pgrep -f 'cube_pose_sync_publisher|ros_tcp_endpoint.*endpoint.py|default_server_endpoint' || true); do [ \"\$p\" = \"\$self\" ] && continue; kill -2 \"\$p\" 2>/dev/null || true; done; sleep 0.6; for p in \$(pgrep -f 'cube_pose_sync_publisher|ros_tcp_endpoint.*endpoint.py|default_server_endpoint' || true); do [ \"\$p\" = \"\$self\" ] && continue; kill \"\$p\" 2>/dev/null || true; done"
```

### Status / topic checks

```bash
CONTAINER=motion_planner_05
docker exec "${CONTAINER}" bash -lc "pgrep -fa 'cube_pose_sync_publisher|ros_tcp_endpoint.*/endpoint.py|default_server_endpoint' || true"
docker exec "${CONTAINER}" bash -lc "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && ros2 topic echo /unity_sync/target_cube_pose --once"
docker exec "${CONTAINER}" bash -lc "tail -n 80 /tmp/part4_tcp_endpoint.log /tmp/part4_cube_pose.log"
```

## 7) End-to-end bringup order (raw)

```bash
# 1. compose up
# 2. colcon build
# 3. start simulation
# 4. start servo launch
# 5. start Part 1 receiver
# 6. start Part 2+3 bridges
# 7. start Part 4 sync
```

## 8) UDP forwarding diagnostics (port 5016)

### Host sees Quest packets?

```bash
sudo tcpdump -ni any 'udp port 5016'
```

### Container receives published UDP?

Stop receiver first so raw listener can bind `:5005`:

```bash
CONTAINER=motion_planner_05
docker exec "${CONTAINER}" bash -lc "pkill -f '[q]uest_controller_receiver' || true"
docker exec "${CONTAINER}" bash -lc "python3 - <<'PY'
import socket,time
s=socket.socket(socket.AF_INET,socket.SOCK_DGRAM)
s.bind(('0.0.0.0',5005))
s.settimeout(0.5)
print('LISTENING_5005')
end=time.time()+8
count=0
src={}
while time.time()<end:
    try:
        _,a=s.recvfrom(4096)
        count+=1
        src[a[0]]=src.get(a[0],0)+1
    except socket.timeout:
        pass
print('packets_in_container_5005 =',count)
print('sources =',src)
PY"
```

If host shows packets but container sees zero, Docker Desktop UDP forwarding is broken; restart Docker Desktop and recreate compose.

## 9) Quick teardown

```bash
CONTAINER=motion_planner_05
docker exec "${CONTAINER}" bash -lc "pkill -f '[q]uest_controller_receiver|[r]eceived_pose_to_target_twist|[t]arget_twist_to_servo_cmd|[t]arget_twist_to_gripper_cmd|[t]arget_twist_reset_manager|[c]ube_pose_sync_publisher|[r]os_tcp_endpoint.*/endpoint.py|default_server_endpoint|[r]un_tabletop_sim.sh|[s]ervo_gz.launch.py' || true"

cd /Users/noahli/ros_unity_project/ros_backend0.5
docker compose down --remove-orphans
```
