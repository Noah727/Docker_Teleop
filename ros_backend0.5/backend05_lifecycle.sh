#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONTAINER="${CONTAINER:-motion_planner_05}"

TELEOP_PATTERN="quest_controller_receiver|received_pose_to_target_twist|target_twist_to_servo_cmd|target_twist_to_gripper_cmd|target_twist_reset_manager|cube_pose_sync_publisher|ros_tcp_endpoint.*endpoint.py|default_server_endpoint|run_tabletop_sim.sh|servo_gz.launch.py"

dc() {
  (cd "${ROOT_DIR}" && docker compose "$@")
}

container_running() {
  docker ps --format '{{.Names}}' | grep -qx "${CONTAINER}"
}

require_running() {
  if ! container_running; then
    echo "[error] Container ${CONTAINER} is not running."
    echo "        Start it with: ./backend05_lifecycle.sh up_container"
    exit 1
  fi
}

dexec() {
  docker exec "${CONTAINER}" bash -lc "$1"
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

  # Remove stale same-name containers that sometimes linger in Created/Exited state.
  while IFS= read -r cid; do
    [ -z "${cid}" ] && continue
    docker rm -f "${cid}" >/dev/null 2>&1 || true
  done < <(docker ps -a --format '{{.ID}} {{.Names}}' | awk '$2 ~ /motion_planner_05$/ {print $1}')

  echo "[ok] Compose stack is down and stale containers are cleaned."
}

up_container() {
  dc up -d --build
  docker ps --filter "name=^/${CONTAINER}$" --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
  docker port "${CONTAINER}" || true
}

start_receiver() {
  require_running
  dexec "source /opt/ros/humble/setup.bash && \
    source /home/noah/ws_moveit/install/setup.bash && \
    pkill -f '[q]uest_controller_receiver' || true; \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge quest_controller_receiver >/tmp/qcr.log 2>&1 < /dev/null &"
  echo "[ok] Started quest_controller_receiver (log: /tmp/qcr.log)."
}

start_part23() {
  require_running
  dexec "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && \
    pkill -f '[/]teleop_bridge/received_pose_to_target_twist' || true; \
    pkill -f '[/]teleop_bridge/target_twist_to_servo_cmd' || true; \
    pkill -f '[/]teleop_bridge/target_twist_to_gripper_cmd' || true; \
    pkill -f '[/]teleop_bridge/target_twist_reset_manager' || true; \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge received_pose_to_target_twist >/tmp/part2_mapper.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge target_twist_to_servo_cmd >/tmp/part3_target_to_servo.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge target_twist_to_gripper_cmd >/tmp/part3_target_to_gripper.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge target_twist_reset_manager >/tmp/part3_reset_manager.log 2>&1 < /dev/null &"
  echo "[ok] Started Part2/Part3 mapper/bridges."
}

start_part4() {
  require_running
  dexec "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && \
    self=\$\$; \
    for p in \$(pgrep -f 'cube_pose_sync_publisher|ros_tcp_endpoint.*endpoint.py|default_server_endpoint' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill -2 \"\$p\" 2>/dev/null || true; \
    done; \
    sleep 0.6; \
    for p in \$(pgrep -f 'cube_pose_sync_publisher|ros_tcp_endpoint.*endpoint.py|default_server_endpoint' || true); do \
      [ \"\$p\" = \"\$self\" ] && continue; kill \"\$p\" 2>/dev/null || true; \
    done; \
    nohup /opt/ros/humble/bin/ros2 launch ros_tcp_endpoint endpoint.py >/tmp/part4_tcp_endpoint.log 2>&1 < /dev/null & \
    nohup /opt/ros/humble/bin/ros2 run teleop_bridge cube_pose_sync_publisher >/tmp/part4_cube_pose.log 2>&1 < /dev/null &"
  echo "[ok] Started Part4 sync services."
}

start_sim() {
  require_running
  dexec "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && \
    pkill -f '[r]un_tabletop_sim.sh' || true; \
    nohup env SIM_HEADLESS=1 /home/noah/ws_moveit/simulation/launch/run_tabletop_sim.sh >/tmp/run_tabletop_sim.log 2>&1 < /dev/null &"
  echo "[ok] Started Gazebo tabletop simulation (headless)."
}

start_servo() {
  require_running
  dexec "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && \
    pkill -f '[s]ervo_gz.launch.py' || true; \
    nohup /opt/ros/humble/bin/ros2 launch servo_test_config servo_gz.launch.py >/tmp/servo_gz.log 2>&1 < /dev/null &"
  echo "[ok] Started servo launch."
}

bringup_all() {
  up_container
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
  docker ps --filter "name=motion_planner_05" --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
  if container_running; then
    dexec "echo '--- processes'; pgrep -fa '${TELEOP_PATTERN}' || true"
    dexec "echo '--- udp bind'; netstat -anup 2>/dev/null | grep ':5005 ' || true"
    dexec "echo '--- recent logs'; \
      for f in /tmp/qcr.log /tmp/part2_mapper.log /tmp/part3_target_to_servo.log /tmp/part3_target_to_gripper.log /tmp/part3_reset_manager.log /tmp/part4_tcp_endpoint.log /tmp/part4_cube_pose.log /tmp/run_tabletop_sim.log /tmp/servo_gz.log; do \
        echo '===== ' \$f; [ -f \$f ] && tail -n 8 \$f || echo missing; \
      done"
  fi
}

usage() {
  cat <<'EOF'
Usage:
  ./backend05_lifecycle.sh safe_down
  ./backend05_lifecycle.sh up_container
  ./backend05_lifecycle.sh restart_container
  ./backend05_lifecycle.sh bringup_all
  ./backend05_lifecycle.sh stop_nodes
  ./backend05_lifecycle.sh start_receiver
  ./backend05_lifecycle.sh start_part23
  ./backend05_lifecycle.sh start_part4
  ./backend05_lifecycle.sh start_sim
  ./backend05_lifecycle.sh start_servo
  ./backend05_lifecycle.sh status
EOF
}

cmd="${1:-}"
case "${cmd}" in
  safe_down) safe_down ;;
  up_container) up_container ;;
  restart_container) safe_down; up_container ;;
  bringup_all) bringup_all ;;
  stop_nodes) stop_nodes ;;
  start_receiver) start_receiver ;;
  start_part23) start_part23 ;;
  start_part4) start_part4 ;;
  start_sim) start_sim ;;
  start_servo) start_servo ;;
  status) status ;;
  *) usage; exit 1 ;;
esac
