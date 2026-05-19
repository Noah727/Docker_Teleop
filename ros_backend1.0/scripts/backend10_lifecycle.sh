#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONTAINER="${CONTAINER:-motion_planner_10}"
RECEIVER_PACKAGE="receiver"
BUILD_PACKAGES="robotiq_hande_description ur_hande_description ur_moveit_config servo_test_config teleop_bridge_msgs teleop_bridge ros_tcp_endpoint receiver"
TELEOP_TUNING_FILE="/home/noah/ws_moveit/src/teleop_bridge/config/teleop_tuning.yaml"
ENV_FILE="${ROOT_DIR}/.env"

TELEOP_PATTERN="quest_controller_receiver|received_pose_to_target_twist|target_twist_to_servo_cmd|target_twist_to_gripper_cmd|target_twist_reset_manager|keyboard_servo_cmd|cube_pose_sync_publisher|ros_tcp_endpoint.*endpoint.py|default_server_endpoint|run_tabletop_sim.sh|servo_gz.launch.py|servo_node_main|joint_states_filter|ros_gz_bridge.*dynamic_pose/info|ros_gz_bridge.*gripper_camera/camera_info|ros_gz_image.*gripper_camera/image_raw"

dc() {
  (cd "${ROOT_DIR}" && docker compose "$@")
}

container_running() {
  docker ps --format '{{.Names}}' | grep -qx "${CONTAINER}"
}

require_running() {
  if ! container_running; then
    echo "[error] Container ${CONTAINER} is not running."
    echo "        Start it with: ./scripts/backend10_lifecycle.sh up_container"
    exit 1
  fi
}

dexec() {
  docker exec "${CONTAINER}" bash -lc "$1"
}

set_env_var() {
  local key="$1"
  local value="$2"
  local tmp_file
  tmp_file="$(mktemp)"
  awk -F= -v k="${key}" -v v="${value}" '
BEGIN { set = 0 }
$1 == k {
  print k "=" v
  set = 1
  next
}
{ print $0 }
END {
  if (set == 0) {
    print k "=" v
  }
}
' "${ENV_FILE}" > "${tmp_file}"
  mv "${tmp_file}" "${ENV_FILE}"
}

quest_tcp_host_port() {
  local port
  port="$(awk -F= '/^QUEST_TCP_HOST_PORT=/{print $2}' "${ENV_FILE}" | tail -n 1)"
  echo "${port:-5026}"
}

ros_tcp_host_port() {
  local port
  port="$(awk -F= '/^ROS_TCP_HOST_PORT=/{print $2}' "${ENV_FILE}" | tail -n 1)"
  echo "${port:-10001}"
}

quest_tcp_host_bind() {
  local bind
  bind="$(awk -F= '/^QUEST_TCP_HOST_BIND=/{print $2}' "${ENV_FILE}" | tail -n 1)"
  echo "${bind:-127.0.0.1}"
}

ros_tcp_host_bind() {
  local bind
  bind="$(awk -F= '/^ROS_TCP_HOST_BIND=/{print $2}' "${ENV_FILE}" | tail -n 1)"
  echo "${bind:-127.0.0.1}"
}

adb_device_connected() {
  command -v adb >/dev/null 2>&1 || return 1
  adb devices | awk 'NR > 1 && $2 == "device" { found = 1 } END { exit(found ? 0 : 1) }'
}

mode_wired() {
  set_env_var "ROS_TCP_HOST_BIND" "127.0.0.1"
  set_env_var "QUEST_TCP_HOST_BIND" "127.0.0.1"
  echo "[ok] Backend network mode set to WIRED in ${ENV_FILE}."
  echo "     ROS-TCP: 127.0.0.1:$(ros_tcp_host_port) -> container 10000"
  echo "     Hand TCP: 127.0.0.1:$(quest_tcp_host_port) -> container 5005"
  echo "     Restart the container to apply Docker port-binding changes."
}

mode_wireless() {
  set_env_var "ROS_TCP_HOST_BIND" "0.0.0.0"
  set_env_var "QUEST_TCP_HOST_BIND" "0.0.0.0"
  echo "[ok] Backend network mode set to WIRELESS in ${ENV_FILE}."
  echo "     ROS-TCP: 0.0.0.0:$(ros_tcp_host_port) -> container 10000"
  echo "     Hand TCP: 0.0.0.0:$(quest_tcp_host_port) -> container 5005"
  echo "     Restart the container to apply Docker port-binding changes."
  echo "     Unity/Quest should use your host LAN IP, not 127.0.0.1."
}

mode_status() {
  echo "--- network mode from ${ENV_FILE}"
  echo "ros tcp bind:   $(ros_tcp_host_bind):$(ros_tcp_host_port)"
  echo "quest tcp bind: $(quest_tcp_host_bind):$(quest_tcp_host_port)"

  if container_running; then
    echo "--- live docker port mapping"
    docker port "${CONTAINER}" || true
  else
    echo "--- live docker port mapping"
    echo "container not running"
  fi
}

workspace_ready() {
  if ! container_running; then
    return 1
  fi
  docker exec "${CONTAINER}" bash -lc "source /opt/ros/humble/setup.bash && \
    [ -f /home/noah/ws_moveit/install/setup.bash ] && \
    source /home/noah/ws_moveit/install/setup.bash && \
    ros2 pkg prefix teleop_bridge >/dev/null 2>&1 && \
    ros2 pkg prefix servo_test_config >/dev/null 2>&1"
}

build_ws() {
  require_running
  dexec "source /opt/ros/humble/setup.bash && cd /home/noah/ws_moveit && \
    colcon build --packages-select ${BUILD_PACKAGES}"
  echo "[ok] Built workspace packages: ${BUILD_PACKAGES}"
}

receiver_ready() {
  if ! container_running; then
    return 1
  fi
  docker exec "${CONTAINER}" bash -lc "source /opt/ros/humble/setup.bash && \
    [ -f /home/noah/ws_moveit/install/setup.bash ] && \
    source /home/noah/ws_moveit/install/setup.bash && \
    ros2 pkg prefix ${RECEIVER_PACKAGE} >/dev/null 2>&1"
}

build_receiver() {
  require_running
  dexec "source /opt/ros/humble/setup.bash && cd /home/noah/ws_moveit && \
    colcon build --packages-select ${RECEIVER_PACKAGE}"
  echo "[ok] Built receiver package: ${RECEIVER_PACKAGE}"
}

ensure_receiver_built() {
  if receiver_ready; then
    echo "[ok] Receiver package already built (${RECEIVER_PACKAGE})."
    return 0
  fi
  echo "[info] Receiver package not ready. Building now..."
  build_receiver
}

ensure_ws_built() {
  if workspace_ready; then
    echo "[ok] Workspace already built (teleop_bridge + servo_test_config found)."
    return 0
  fi
  echo "[info] Workspace not ready in container. Building now..."
  build_ws
}

stop_nodes() {
  if ! container_running; then
    echo "[info] ${CONTAINER} not running; skip node stop."
    return 0
  fi

  dexec "self=\$\$; \
    for p in \$(pgrep -f '${TELEOP_PATTERN}' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; \
      kill -2 \"\$p\" 2>/dev/null || true; \
    done; \
    sleep 1; \
    for p in \$(pgrep -f '${TELEOP_PATTERN}' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; \
      kill -9 \"\$p\" 2>/dev/null || true; \
    done"

  echo "[ok] Stopped teleop/simulation processes inside ${CONTAINER}."
}

safe_down() {
  stop_nodes
  dc down --remove-orphans || true

  while IFS= read -r cid; do
    [ -z "${cid}" ] && continue
    docker rm -f "${cid}" >/dev/null 2>&1 || true
  done < <(docker ps -a --format '{{.ID}} {{.Names}}' | awk -v name="${CONTAINER}" '$2 == name {print $1}')

  echo "[ok] Compose stack is down and stale containers are cleaned."
}

up_container() {
  dc up -d
  docker ps --filter "name=^/${CONTAINER}$" --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
  docker port "${CONTAINER}" || true
}

up_container_build() {
  dc up -d --build
  docker ps --filter "name=^/${CONTAINER}$" --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
  docker port "${CONTAINER}" || true
}

wired_on() {
  local quest_port ros_port
  quest_port="$(quest_tcp_host_port)"
  ros_port="$(ros_tcp_host_port)"

  if ! command -v adb >/dev/null 2>&1; then
    echo "[error] adb is not installed or not in PATH."
    exit 1
  fi

  if ! adb_device_connected; then
    echo "[error] No ADB device is connected."
    echo "        Connect the Quest by USB, allow USB debugging in the headset, then retry."
    exit 1
  fi

  adb reverse "tcp:${quest_port}" "tcp:${quest_port}"
  adb reverse "tcp:${ros_port}" "tcp:${ros_port}"
  echo "[ok] Wired TCP tunnels enabled:"
  echo "     HandPoseSender: Quest 127.0.0.1:${quest_port} -> Mac 127.0.0.1:${quest_port}"
  echo "     ROS-TCP:        Quest 127.0.0.1:${ros_port} -> Mac 127.0.0.1:${ros_port}"
  echo "     Unity HandPoseSender should use targetIP=127.0.0.1 targetPort=${quest_port}"
  echo "     Unity ROS Settings should use ROS IP Address=127.0.0.1 ROS Port=${ros_port}"
}

wired_off() {
  local quest_port ros_port
  quest_port="$(quest_tcp_host_port)"
  ros_port="$(ros_tcp_host_port)"

  if ! command -v adb >/dev/null 2>&1; then
    echo "[error] adb is not installed or not in PATH."
    exit 1
  fi

  adb reverse --remove "tcp:${quest_port}" || true
  adb reverse --remove "tcp:${ros_port}" || true
  echo "[ok] Wired TCP tunnels removed for tcp:${quest_port} and tcp:${ros_port}."
}

wired_status() {
  local quest_port quest_bind ros_port ros_bind
  quest_port="$(quest_tcp_host_port)"
  quest_bind="$(quest_tcp_host_bind)"
  ros_port="$(ros_tcp_host_port)"
  ros_bind="$(ros_tcp_host_bind)"

  echo "--- wired defaults"
  echo "quest hand host bind: ${quest_bind}"
  echo "quest hand host port: ${quest_port}"
  echo "quest hand target:    127.0.0.1:${quest_port}"
  echo "ros tcp host bind:    ${ros_bind}"
  echo "ros tcp host port:    ${ros_port}"
  echo "ros tcp target:       127.0.0.1:${ros_port}"

  if command -v adb >/dev/null 2>&1; then
    echo "--- adb devices"
    adb devices || true
    echo "--- adb reverse"
    adb reverse --list 2>/dev/null || true
  else
    echo "--- adb"
    echo "adb not found"
  fi

  echo "--- docker port"
  docker port "${CONTAINER}" 2>/dev/null || true
}

start_receiver() {
  require_running
  ensure_receiver_built
  dexec "source /opt/ros/humble/setup.bash && \
    source /home/noah/ws_moveit/install/setup.bash && \
    self=\$\$; \
    for p in \$(pgrep -f 'quest_controller_receiver' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill -2 \"\$p\" 2>/dev/null || true; \
    done; \
    sleep 0.4; \
    for p in \$(pgrep -f 'quest_controller_receiver' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill -9 \"\$p\" 2>/dev/null || true; \
    done; \
    nohup /opt/ros/humble/bin/ros2 run receiver quest_controller_receiver >/tmp/qcr.log 2>&1 < /dev/null &"
  echo "[ok] Started quest_controller_receiver (log: /tmp/qcr.log)."
}

start_part23() {
  require_running
  ensure_ws_built
  dexec "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && \
    self=\$\$; \
    for p in \$(pgrep -f 'received_pose_to_target_twist|target_twist_to_servo_cmd|target_twist_to_gripper_cmd|target_twist_reset_manager' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill -2 \"\$p\" 2>/dev/null || true; \
    done; \
    sleep 0.4; \
    for p in \$(pgrep -f 'received_pose_to_target_twist|target_twist_to_servo_cmd|target_twist_to_gripper_cmd|target_twist_reset_manager' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill -9 \"\$p\" 2>/dev/null || true; \
    done; \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge received_pose_to_target_twist --ros-args --params-file ${TELEOP_TUNING_FILE} >/tmp/part2_mapper.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge target_twist_to_servo_cmd --ros-args --params-file ${TELEOP_TUNING_FILE} >/tmp/part3_target_to_servo.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge target_twist_to_gripper_cmd --ros-args --params-file ${TELEOP_TUNING_FILE} >/tmp/part3_target_to_gripper.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge target_twist_reset_manager --ros-args --params-file ${TELEOP_TUNING_FILE} >/tmp/part3_reset_manager.log 2>&1 < /dev/null &"
  echo "[ok] Started Part2/Part3 mapper/bridges."
}

start_part4() {
  require_running
  ensure_ws_built
  dexec "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && \
    self=\$\$; \
    for p in \$(pgrep -f 'cube_pose_sync_publisher|ros_tcp_endpoint.*endpoint.py|default_server_endpoint|ros_gz_bridge.*dynamic_pose/info|ros_gz_bridge.*gripper_camera/camera_info|ros_gz_image.*gripper_camera/image_raw' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill -2 \"\$p\" 2>/dev/null || true; \
    done; \
    sleep 0.6; \
    for p in \$(pgrep -f 'cube_pose_sync_publisher|ros_tcp_endpoint.*endpoint.py|default_server_endpoint|ros_gz_bridge.*dynamic_pose/info|ros_gz_bridge.*gripper_camera/camera_info|ros_gz_image.*gripper_camera/image_raw' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill \"\$p\" 2>/dev/null || true; \
    done; \
    nohup /opt/ros/humble/bin/ros2 launch ros_tcp_endpoint endpoint.py >/tmp/part4_tcp_endpoint.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run ros_gz_bridge parameter_bridge '/world/ur_hande_tabletop/dynamic_pose/info@tf2_msgs/msg/TFMessage[ignition.msgs.Pose_V' >/tmp/part4_gz_tf_bridge.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run ros_gz_bridge parameter_bridge '/gripper_camera/camera_info@sensor_msgs/msg/CameraInfo[ignition.msgs.CameraInfo' >/tmp/part4_gz_camera_info_bridge.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run ros_gz_image image_bridge /gripper_camera/image_raw >/tmp/part4_gz_image_bridge.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge cube_pose_sync_publisher >/tmp/part4_cube_pose.log 2>&1 < /dev/null &"
  echo "[ok] Started Part4 sync services."
}

start_sim() {
  require_running
  ensure_ws_built
  dexec "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && \
    self=\$\$; \
    for p in \$(pgrep -f 'run_tabletop_sim.sh' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill -2 \"\$p\" 2>/dev/null || true; \
    done; \
    sleep 0.4; \
    for p in \$(pgrep -f 'run_tabletop_sim.sh' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill -9 \"\$p\" 2>/dev/null || true; \
    done; \
    nohup env SIM_HEADLESS=0 /home/noah/ws_moveit/simulation/launch/run_tabletop_sim.sh >/tmp/run_tabletop_sim.log 2>&1 < /dev/null &"
  echo "[ok] Started Gazebo tabletop simulation."
}

start_servo() {
  require_running
  ensure_ws_built
  dexec "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && \
    self=\$\$; \
    for p in \$(pgrep -f 'servo_gz.launch.py|servo_node_main|joint_states_filter' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill -2 \"\$p\" 2>/dev/null || true; \
    done; \
    sleep 0.4; \
    for p in \$(pgrep -f 'servo_gz.launch.py|servo_node_main|joint_states_filter' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill -9 \"\$p\" 2>/dev/null || true; \
    done; \
    nohup /opt/ros/humble/bin/ros2 launch servo_test_config servo_gz.launch.py >/tmp/servo_gz.log 2>&1 < /dev/null &"
  echo "[ok] Started servo launch."
}

keyboard() {
  require_running
  ensure_ws_built
  dexec "self=\$\$; \
    for p in \$(pgrep -f 'target_twist_to_servo_cmd|keyboard_servo_cmd' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill -2 \"\$p\" 2>/dev/null || true; \
    done; \
    sleep 0.4; \
    for p in \$(pgrep -f 'target_twist_to_servo_cmd|keyboard_servo_cmd' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill -9 \"\$p\" 2>/dev/null || true; \
    done"
  echo "[info] Starting interactive keyboard controller."
  echo "[info] This temporarily disables headset-to-Servo output. Run start_part23 after quitting to restore headset control."
  echo "[info] Press x or Ctrl-C inside the keyboard controller to quit."
  docker exec -it "${CONTAINER}" bash -lc "source /opt/ros/humble/setup.bash && \
    source /home/noah/ws_moveit/install/setup.bash && \
    ros2 run teleop_bridge keyboard_servo_cmd --ros-args --params-file ${TELEOP_TUNING_FILE}"
}

bringup_all() {
  up_container
  ensure_ws_built
  start_sim
  sleep 2
  start_servo
  sleep 2
  start_receiver
  start_part23
  start_part4
  status
}

bringup_wired() {
  up_container
  wired_on
  ensure_ws_built
  start_sim
  sleep 2
  start_servo
  sleep 2
  start_receiver
  start_part23
  start_part4
  status
}

status() {
  docker ps --filter "name=^/${CONTAINER}$" --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
  if container_running; then
    dexec "echo '--- processes'; pgrep -fa '${TELEOP_PATTERN}' || true"
    dexec "echo '--- tcp listen'; netstat -ant 2>/dev/null | grep LISTEN | grep ':5005 ' || true"
    dexec "echo '--- recent logs'; \
      for f in /tmp/qcr.log /tmp/part2_mapper.log /tmp/part3_target_to_servo.log /tmp/part3_target_to_gripper.log /tmp/part3_reset_manager.log /tmp/part4_tcp_endpoint.log /tmp/part4_gz_tf_bridge.log /tmp/part4_gz_camera_info_bridge.log /tmp/part4_gz_image_bridge.log /tmp/part4_cube_pose.log /tmp/run_tabletop_sim.log /tmp/servo_gz.log; do \
        echo '===== ' \$f; [ -f \$f ] && tail -n 8 \$f || echo missing; \
      done"
  fi
}

usage() {
  cat <<'EOH'
Usage:
  ./scripts/backend10_lifecycle.sh mode_wired
  ./scripts/backend10_lifecycle.sh mode_wireless
  ./scripts/backend10_lifecycle.sh mode_status
  ./scripts/backend10_lifecycle.sh safe_down
  ./scripts/backend10_lifecycle.sh up_container
  ./scripts/backend10_lifecycle.sh up_container_build
  ./scripts/backend10_lifecycle.sh restart_container
  ./scripts/backend10_lifecycle.sh wired_on
  ./scripts/backend10_lifecycle.sh wired_off
  ./scripts/backend10_lifecycle.sh wired_status
  ./scripts/backend10_lifecycle.sh build_ws
  ./scripts/backend10_lifecycle.sh build_receiver
  ./scripts/backend10_lifecycle.sh bringup_all
  ./scripts/backend10_lifecycle.sh bringup_wired
  ./scripts/backend10_lifecycle.sh stop_nodes
  ./scripts/backend10_lifecycle.sh start_receiver
  ./scripts/backend10_lifecycle.sh start_part23
  ./scripts/backend10_lifecycle.sh start_part4
  ./scripts/backend10_lifecycle.sh start_sim
  ./scripts/backend10_lifecycle.sh start_servo
  ./scripts/backend10_lifecycle.sh keyboard
  ./scripts/backend10_lifecycle.sh status
EOH
}

cmd="${1:-}"
case "${cmd}" in
  mode_wired) mode_wired ;;
  mode_wireless) mode_wireless ;;
  mode_status) mode_status ;;
  safe_down) safe_down ;;
  up_container) up_container ;;
  up_container_build) up_container_build ;;
  restart_container) safe_down; up_container ;;
  wired_on) wired_on ;;
  wired_off) wired_off ;;
  wired_status) wired_status ;;
  build_ws) build_ws ;;
  build_receiver) build_receiver ;;
  bringup_all) bringup_all ;;
  bringup_wired) bringup_wired ;;
  stop_nodes) stop_nodes ;;
  start_receiver) start_receiver ;;
  start_part23) start_part23 ;;
  start_part4) start_part4 ;;
  start_sim) start_sim ;;
  start_servo) start_servo ;;
  keyboard) keyboard ;;
  status) status ;;
  *) usage; exit 1 ;;
esac
