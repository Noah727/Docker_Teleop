#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONTAINER="${CONTAINER:-motion_planner_11}"
RECEIVER_PACKAGE="receiver"
BUILD_PACKAGES="robotiq_hande_description ur_hande_description ur_moveit_config servo_test_config teleop_bridge_msgs teleop_bridge ros_tcp_endpoint receiver"
TELEOP_TUNING_FILE="/home/noah/ws_moveit/src/teleop_bridge/config/teleop_tuning.yaml"
DUAL_LEFT_TUNING_FILE="/home/noah/ws_moveit/src/teleop_bridge/config/teleop_tuning_dual_left.yaml"
DUAL_RIGHT_TUNING_FILE="/home/noah/ws_moveit/src/teleop_bridge/config/teleop_tuning_dual_right.yaml"
ENV_FILE="${ROOT_DIR}/.env"

TELEOP_PATTERN="quest_controller_receiver|received_pose_to_target_twist|target_twist_to_servo_cmd|target_twist_to_gripper_cmd|coupled_hande_gripper_controller|target_twist_reset_manager|keyboard_servo_cmd|cube_pose_sync_publisher|gazebo_contact_haptic_publisher|rubik2x2_mechanism_controller|ros_tcp_endpoint.*endpoint.py|default_server_endpoint|run_tabletop_sim.sh|run_dual_arm_tabletop_sim.sh|servo_gz.launch.py|servo_dual_gz.launch.py|servo_node_main|joint_states_filter|ros_gz_bridge.* /clock@|ros_gz_bridge.*dynamic_pose/info"

dc() {
  (cd "${ROOT_DIR}" && docker compose "$@")
}

container_running() {
  docker ps --format '{{.Names}}' | grep -qx "${CONTAINER}"
}

require_running() {
  if ! container_running; then
    echo "[error] Container ${CONTAINER} is not running."
    echo "        Start it with: ./scripts/backend11_lifecycle.sh up_container"
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

quest_tcp_unity_port() {
  local port
  port="$(awk -F= '/^QUEST_TCP_UNITY_PORT=/{print $2}' "${ENV_FILE}" | tail -n 1)"
  echo "${port:-5026}"
}

ros_tcp_host_port() {
  local port
  port="$(awk -F= '/^ROS_TCP_HOST_PORT=/{print $2}' "${ENV_FILE}" | tail -n 1)"
  echo "${port:-10001}"
}

ros_tcp_unity_port() {
  local port
  port="$(awk -F= '/^ROS_TCP_UNITY_PORT=/{print $2}' "${ENV_FILE}" | tail -n 1)"
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
  set_env_var "ROS_TCP_UNITY_PORT" "$(ros_tcp_unity_port)"
  set_env_var "QUEST_TCP_UNITY_PORT" "$(quest_tcp_unity_port)"
  echo "[ok] Backend network mode set to WIRED in ${ENV_FILE}."
  echo "     ROS-TCP: Quest 127.0.0.1:$(ros_tcp_unity_port) -> host 127.0.0.1:$(ros_tcp_host_port) -> container 10000"
  echo "     Hand TCP: Quest 127.0.0.1:$(quest_tcp_unity_port) -> host 127.0.0.1:$(quest_tcp_host_port) -> container 5005"
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
  local quest_unity_port quest_host_port ros_unity_port ros_host_port
  quest_unity_port="$(quest_tcp_unity_port)"
  quest_host_port="$(quest_tcp_host_port)"
  ros_unity_port="$(ros_tcp_unity_port)"
  ros_host_port="$(ros_tcp_host_port)"

  if ! command -v adb >/dev/null 2>&1; then
    echo "[error] adb is not installed or not in PATH."
    exit 1
  fi

  if ! adb_device_connected; then
    echo "[error] No ADB device is connected."
    echo "        Connect the Quest by USB, allow USB debugging in the headset, then retry."
    exit 1
  fi

  adb reverse "tcp:${quest_unity_port}" "tcp:${quest_host_port}"
  adb reverse "tcp:${ros_unity_port}" "tcp:${ros_host_port}"
  echo "[ok] Wired TCP tunnels enabled:"
  echo "     HandPoseSender: Quest 127.0.0.1:${quest_unity_port} -> Mac 127.0.0.1:${quest_host_port}"
  echo "     ROS-TCP:        Quest 127.0.0.1:${ros_unity_port} -> Mac 127.0.0.1:${ros_host_port}"
  echo "     Unity HandPoseSender should use targetIP=127.0.0.1 targetPort=${quest_unity_port}"
  echo "     Unity ROS Settings should use ROS IP Address=127.0.0.1 ROS Port=${ros_unity_port}"
}

wired_off() {
  local quest_unity_port ros_unity_port
  quest_unity_port="$(quest_tcp_unity_port)"
  ros_unity_port="$(ros_tcp_unity_port)"

  if ! command -v adb >/dev/null 2>&1; then
    echo "[error] adb is not installed or not in PATH."
    exit 1
  fi

  adb reverse --remove "tcp:${quest_unity_port}" || true
  adb reverse --remove "tcp:${ros_unity_port}" || true
  echo "[ok] Wired TCP tunnels removed for Quest tcp:${quest_unity_port} and tcp:${ros_unity_port}."
}

wired_status() {
  local quest_host_port quest_unity_port quest_bind ros_host_port ros_unity_port ros_bind
  quest_host_port="$(quest_tcp_host_port)"
  quest_unity_port="$(quest_tcp_unity_port)"
  quest_bind="$(quest_tcp_host_bind)"
  ros_host_port="$(ros_tcp_host_port)"
  ros_unity_port="$(ros_tcp_unity_port)"
  ros_bind="$(ros_tcp_host_bind)"

  echo "--- wired defaults"
  echo "quest hand host bind: ${quest_bind}"
  echo "quest hand host port: ${quest_host_port}"
  echo "quest hand app port:  ${quest_unity_port}"
  echo "quest hand tunnel:    Quest 127.0.0.1:${quest_unity_port} -> host 127.0.0.1:${quest_host_port}"
  echo "ros tcp host bind:    ${ros_bind}"
  echo "ros tcp host port:    ${ros_host_port}"
  echo "ros tcp app port:     ${ros_unity_port}"
  echo "ros tcp tunnel:       Quest 127.0.0.1:${ros_unity_port} -> host 127.0.0.1:${ros_host_port}"

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

maybe_wired_on() {
  if [ "$(quest_tcp_host_bind)" != "127.0.0.1" ] && [ "$(ros_tcp_host_bind)" != "127.0.0.1" ]; then
    return 0
  fi

  if ! command -v adb >/dev/null 2>&1; then
    echo "[warn] adb not found; skipping wired TCP tunnels."
    return 0
  fi

  if adb_device_connected; then
    wired_on
  else
    echo "[warn] No ADB device connected; skipping wired TCP tunnels."
  fi
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
    for p in \$(pgrep -f 'received_pose_to_target_twist|target_twist_to_servo_cmd|target_twist_to_gripper_cmd|coupled_hande_gripper_controller|target_twist_reset_manager' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill -2 \"\$p\" 2>/dev/null || true; \
    done; \
    sleep 0.4; \
    for p in \$(pgrep -f 'received_pose_to_target_twist|target_twist_to_servo_cmd|target_twist_to_gripper_cmd|coupled_hande_gripper_controller|target_twist_reset_manager' || true); do \
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
    for p in \$(pgrep -f 'cube_pose_sync_publisher|gazebo_contact_haptic_publisher|rubik2x2_mechanism_controller|ros_tcp_endpoint.*endpoint.py|default_server_endpoint|ros_gz_bridge.*dynamic_pose/info' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill -2 \"\$p\" 2>/dev/null || true; \
    done; \
    sleep 0.6; \
    for p in \$(pgrep -f 'cube_pose_sync_publisher|gazebo_contact_haptic_publisher|rubik2x2_mechanism_controller|ros_tcp_endpoint.*endpoint.py|default_server_endpoint|ros_gz_bridge.*dynamic_pose/info' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill \"\$p\" 2>/dev/null || true; \
    done; \
    nohup /opt/ros/humble/bin/ros2 launch ros_tcp_endpoint endpoint.py >/tmp/part4_tcp_endpoint.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run ros_gz_bridge parameter_bridge '/world/ur_hande_tabletop/dynamic_pose/info@tf2_msgs/msg/TFMessage[ignition.msgs.Pose_V' >/tmp/part4_gz_tf_bridge.log 2>&1 < /dev/null & \
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

generate_dual_world_from_profiles() {
  local generator="${ROOT_DIR}/simulation/tools/generate_dual_arm_world_from_profiles.py"
  local scene_profile="${DUAL_SCENE_PROFILE:-${ROOT_DIR}/profiles/scenes/dual_arm_tabletop/scene.yaml}"
  if [[ "${scene_profile}" != /* ]]; then
    scene_profile="${ROOT_DIR}/${scene_profile}"
  fi
  local world_output="${ROOT_DIR}/simulation/worlds/ur_hande_dual_arm_tabletop.sdf"
  if [[ ! -x "${generator}" ]]; then
    echo "[error] Missing world generator: ${generator}"
    exit 1
  fi
  echo "[info] Generating dual-arm world from ${scene_profile}"
  python3 "${generator}" \
    --repo-root "${ROOT_DIR}" \
    --scene-profile "${scene_profile}" \
    --output "${world_output}"
}

start_dual_sim() {
  require_running
  ensure_ws_built
  generate_dual_world_from_profiles
  dexec "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && \
    self=\$\$; \
    for p in \$(pgrep -f 'run_dual_arm_tabletop_sim.sh|run_tabletop_sim.sh' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill -2 \"\$p\" 2>/dev/null || true; \
    done; \
    sleep 0.4; \
    for p in \$(pgrep -f 'run_dual_arm_tabletop_sim.sh|run_tabletop_sim.sh' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill -9 \"\$p\" 2>/dev/null || true; \
    done; \
    nohup env SIM_HEADLESS=0 /home/noah/ws_moveit/simulation/launch/run_dual_arm_tabletop_sim.sh >/tmp/run_dual_arm_tabletop_sim.log 2>&1 < /dev/null &"
  echo "[ok] Started dual-arm Gazebo tabletop scaffold."
}

start_dual_servo() {
  require_running
  ensure_ws_built
  dexec "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && \
    self=\$\$; \
    for p in \$(pgrep -f 'servo_gz.launch.py|servo_dual_gz.launch.py|servo_node_main|joint_states_filter' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill -2 \"\$p\" 2>/dev/null || true; \
    done; \
    sleep 0.4; \
    for p in \$(pgrep -f 'servo_gz.launch.py|servo_dual_gz.launch.py|servo_node_main|joint_states_filter' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill -9 \"\$p\" 2>/dev/null || true; \
    done; \
    nohup /opt/ros/humble/bin/ros2 launch servo_test_config servo_dual_gz.launch.py >/tmp/servo_dual_gz.log 2>&1 < /dev/null &"
  echo "[ok] Started dual-arm servo launch."
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

start_dual_part23() {
  require_running
  ensure_ws_built
  local dual_gripper_controller="${DUAL_GRIPPER_CONTROLLER:-position}"
  local dual_gripper_node="target_twist_to_gripper_cmd"
  if [[ "${dual_gripper_controller}" == "coupled" ]]; then
    dual_gripper_node="coupled_hande_gripper_controller"
  elif [[ "${dual_gripper_controller}" != "position" ]]; then
    echo "[warn] Unknown DUAL_GRIPPER_CONTROLLER=${dual_gripper_controller}; using position."
    dual_gripper_controller="position"
  fi
  dexec "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && \
    self=\$\$; \
    for p in \$(pgrep -f 'received_pose_to_target_twist|target_twist_to_servo_cmd|target_twist_to_gripper_cmd|coupled_hande_gripper_controller|target_twist_reset_manager' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill -2 \"\$p\" 2>/dev/null || true; \
    done; \
    sleep 0.4; \
    for p in \$(pgrep -f 'received_pose_to_target_twist|target_twist_to_servo_cmd|target_twist_to_gripper_cmd|coupled_hande_gripper_controller|target_twist_reset_manager' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill -9 \"\$p\" 2>/dev/null || true; \
    done; \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge received_pose_to_target_twist --ros-args -r __ns:=/left_arm --params-file ${DUAL_LEFT_TUNING_FILE} >/tmp/dual_left_mapper.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge target_twist_to_servo_cmd --ros-args -r __ns:=/left_arm --params-file ${DUAL_LEFT_TUNING_FILE} >/tmp/dual_left_target_to_servo.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge ${dual_gripper_node} --ros-args -r __ns:=/left_arm --params-file ${DUAL_LEFT_TUNING_FILE} >/tmp/dual_left_target_to_gripper.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge target_twist_reset_manager --ros-args -r __ns:=/left_arm --params-file ${DUAL_LEFT_TUNING_FILE} >/tmp/dual_left_reset_manager.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge received_pose_to_target_twist --ros-args -r __ns:=/right_arm --params-file ${DUAL_RIGHT_TUNING_FILE} >/tmp/dual_right_mapper.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge target_twist_to_servo_cmd --ros-args -r __ns:=/right_arm --params-file ${DUAL_RIGHT_TUNING_FILE} >/tmp/dual_right_target_to_servo.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge ${dual_gripper_node} --ros-args -r __ns:=/right_arm --params-file ${DUAL_RIGHT_TUNING_FILE} >/tmp/dual_right_target_to_gripper.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge target_twist_reset_manager --ros-args -r __ns:=/right_arm --params-file ${DUAL_RIGHT_TUNING_FILE} >/tmp/dual_right_reset_manager.log 2>&1 < /dev/null &"
  echo "[ok] Started dual-arm Part2/Part3 mapper/bridges (gripper=${dual_gripper_controller})."
}

start_dual_part4() {
  require_running
  ensure_ws_built
  dexec "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && \
    self=\$\$; \
    for p in \$(pgrep -f 'cube_pose_sync_publisher|gazebo_contact_haptic_publisher|rubik2x2_mechanism_controller|ros_tcp_endpoint.*endpoint.py|default_server_endpoint|ros_gz_bridge.*dynamic_pose/info' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill -2 \"\$p\" 2>/dev/null || true; \
    done; \
    sleep 0.6; \
    for p in \$(pgrep -f 'cube_pose_sync_publisher|gazebo_contact_haptic_publisher|rubik2x2_mechanism_controller|ros_tcp_endpoint.*endpoint.py|default_server_endpoint|ros_gz_bridge.*dynamic_pose/info' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill \"\$p\" 2>/dev/null || true; \
    done; \
    nohup /opt/ros/humble/bin/ros2 launch ros_tcp_endpoint endpoint.py >/tmp/dual_part4_tcp_endpoint.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run ros_gz_bridge parameter_bridge '/world/ur_hande_dual_arm_tabletop/dynamic_pose/info@tf2_msgs/msg/TFMessage[ignition.msgs.Pose_V' >/tmp/dual_part4_gz_tf_bridge.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge cube_pose_sync_publisher --ros-args -p target_frame:=world -p gz_dynamic_pose_topic:=/world/ur_hande_dual_arm_tabletop/dynamic_pose/info -p scene_layout_sdf_path:=/home/noah/ws_moveit/simulation/worlds/ur_hande_dual_arm_tabletop.sdf -p publish_rate_hz:=60.0 >/tmp/dual_part4_cube_pose.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge rubik2x2_mechanism_controller --ros-args -p world_name:=ur_hande_dual_arm_tabletop -p scene_layout_sdf_path:=/home/noah/ws_moveit/simulation/worlds/ur_hande_dual_arm_tabletop.sdf -p root_name:=Sync_Rubik2x2 >/tmp/dual_part4_rubik.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge gazebo_contact_haptic_publisher --ros-args -p gz_dynamic_pose_topic:=/world/ur_hande_dual_arm_tabletop/dynamic_pose/info -p scene_layout_sdf_path:=/home/noah/ws_moveit/simulation/worlds/ur_hande_dual_arm_tabletop.sdf -p publish_rate_hz:=60.0 -p continuous_contact_amplitude:=0.18 -p proximity_amplitude:=0.06 >/tmp/dual_part4_haptics.log 2>&1 < /dev/null &"
  echo "[ok] Started dual-arm Part4 sync services."
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

bringup_dual() {
  up_container
  maybe_wired_on
  ensure_ws_built
  start_dual_sim
  sleep 2
  start_dual_servo
  sleep 2
  start_receiver
  start_dual_part23
  start_dual_part4
  status
}

status() {
  docker ps --filter "name=^/${CONTAINER}$" --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
  if container_running; then
    dexec "echo '--- processes'; pgrep -fa '${TELEOP_PATTERN}' || true"
    dexec "echo '--- tcp listen'; netstat -ant 2>/dev/null | grep LISTEN | grep ':5005 ' || true"
    dexec "echo '--- recent logs'; \
      for f in /tmp/qcr.log /tmp/part2_mapper.log /tmp/part3_target_to_servo.log /tmp/part3_target_to_gripper.log /tmp/part3_reset_manager.log /tmp/part4_tcp_endpoint.log /tmp/part4_gz_tf_bridge.log /tmp/part4_cube_pose.log /tmp/run_tabletop_sim.log /tmp/run_dual_arm_tabletop_sim.log /tmp/gz_clock_bridge.log /tmp/gz_dual_arm_clock_bridge.log /tmp/servo_gz.log /tmp/servo_dual_gz.log /tmp/dual_left_mapper.log /tmp/dual_left_target_to_servo.log /tmp/dual_left_target_to_gripper.log /tmp/dual_left_reset_manager.log /tmp/dual_right_mapper.log /tmp/dual_right_target_to_servo.log /tmp/dual_right_target_to_gripper.log /tmp/dual_right_reset_manager.log /tmp/dual_part4_tcp_endpoint.log /tmp/dual_part4_gz_tf_bridge.log /tmp/dual_part4_cube_pose.log /tmp/dual_part4_rubik.log /tmp/dual_part4_haptics.log; do \
        echo '===== ' \$f; [ -f \$f ] && tail -n 8 \$f || echo missing; \
      done"
  fi
}

usage() {
  cat <<'EOH'
Usage:
  ./scripts/backend11_lifecycle.sh mode_wired
  ./scripts/backend11_lifecycle.sh mode_wireless
  ./scripts/backend11_lifecycle.sh mode_status
  ./scripts/backend11_lifecycle.sh safe_down
  ./scripts/backend11_lifecycle.sh up_container
  ./scripts/backend11_lifecycle.sh up_container_build
  ./scripts/backend11_lifecycle.sh restart_container
  ./scripts/backend11_lifecycle.sh wired_on
  ./scripts/backend11_lifecycle.sh wired_off
  ./scripts/backend11_lifecycle.sh wired_status
  ./scripts/backend11_lifecycle.sh build_ws
  ./scripts/backend11_lifecycle.sh build_receiver
  ./scripts/backend11_lifecycle.sh bringup_all
  ./scripts/backend11_lifecycle.sh bringup_wired
  ./scripts/backend11_lifecycle.sh bringup_dual
  ./scripts/backend11_lifecycle.sh stop_nodes
  ./scripts/backend11_lifecycle.sh start_receiver
  ./scripts/backend11_lifecycle.sh start_part23
  ./scripts/backend11_lifecycle.sh start_dual_part23
  ./scripts/backend11_lifecycle.sh start_part4
  ./scripts/backend11_lifecycle.sh start_dual_part4
  ./scripts/backend11_lifecycle.sh start_sim
  ./scripts/backend11_lifecycle.sh generate_dual_world
  DUAL_SCENE_PROFILE=profiles/scenes/dual_arm_cable_insertion/scene.yaml ./scripts/backend11_lifecycle.sh generate_dual_world
  ./scripts/backend11_lifecycle.sh start_dual_sim
  ./scripts/backend11_lifecycle.sh start_servo
  ./scripts/backend11_lifecycle.sh start_dual_servo
  ./scripts/backend11_lifecycle.sh keyboard
  ./scripts/backend11_lifecycle.sh status
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
  bringup_dual) bringup_dual ;;
  stop_nodes) stop_nodes ;;
  start_receiver) start_receiver ;;
  start_part23) start_part23 ;;
  start_dual_part23) start_dual_part23 ;;
  start_part4) start_part4 ;;
  start_dual_part4) start_dual_part4 ;;
  start_sim) start_sim ;;
  generate_dual_world) generate_dual_world_from_profiles ;;
  start_dual_sim) start_dual_sim ;;
  start_servo) start_servo ;;
  start_dual_servo) start_dual_servo ;;
  keyboard) keyboard ;;
  status) status ;;
  *) usage; exit 1 ;;
esac
