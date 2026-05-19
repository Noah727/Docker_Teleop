#!/usr/bin/env bash
set -euo pipefail

CONTAINER="${CONTAINER:-motion_planner_10}"
RESTART_RECEIVER="${RESTART_RECEIVER:-1}"

echo "[info] Stopping quest_controller_receiver so scripted input owns /received_pose_states..."
docker exec "${CONTAINER}" bash -lc "pkill -f '[q]uest_controller_receiver' || true"

echo "[info] Running hard-coded pick sequence (approach -> descend -> close -> lift -> retreat)..."
docker exec "${CONTAINER}" bash -lc "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && python3 - <<'PY'
import time
import rclpy
from teleop_bridge_msgs.msg import ReceivedPoseStates

PHASES = [
    # (name, duration_sec, target_xyz_in_base_link, close_enable, open_enable)
    ('open_home', 2.0, (0.45, 0.00, 0.25), False, True),
    ('approach_above_cube', 4.0, (0.60, 0.25, 0.22), False, False),
    ('descend_to_cube', 3.0, (0.60, 0.25, 0.07), False, False),
    ('close_gripper', 2.0, (0.60, 0.25, 0.07), True, False),
    ('lift_cube', 4.0, (0.60, 0.25, 0.24), True, False),
    ('retreat', 3.0, (0.50, 0.00, 0.24), True, False),
]

# Mapper inverse for axis_map=['x','z','y'] with sign=[1,-1,1]:
# base target (tx,ty,tz) = (ux, -uz, uy)
# so unity pose to publish is:
#   ux = tx, uy = tz, uz = -ty
def base_to_unity(tx, ty, tz):
    return tx, tz, -ty

rclpy.init()
node = rclpy.create_node('hardcoded_pick_driver')
pub = node.create_publisher(ReceivedPoseStates, '/received_pose_states', 10)

msg = ReceivedPoseStates()
msg.header.frame_id = 'unity_world'
msg.tracked = True
msg.rotate_enable = False
msg.reset_enable = False
msg.pose.orientation.w = 1.0
msg.source = 'hardcoded_pick'

dt = 1.0 / 60.0

for name, duration, (tx, ty, tz), close_enable, open_enable in PHASES:
    ux, uy, uz = base_to_unity(tx, ty, tz)
    msg.pose.position.x = float(ux)
    msg.pose.position.y = float(uy)
    msg.pose.position.z = float(uz)
    msg.close_enable = bool(close_enable)
    msg.open_enable = bool(open_enable)
    msg.grip_value = 1.0 if close_enable else 0.0
    msg.trigger_value = 1.0 if open_enable else 0.0

    print(f'phase={name:>18} target_base=({tx:.3f},{ty:.3f},{tz:.3f}) close={close_enable} open={open_enable}')
    end_t = time.monotonic() + duration
    while time.monotonic() < end_t:
        msg.header.stamp = node.get_clock().now().to_msg()
        pub.publish(msg)
        rclpy.spin_once(node, timeout_sec=0.0)
        time.sleep(dt)

# Release buttons while keeping last pose briefly.
msg.close_enable = False
msg.open_enable = False
msg.grip_value = 0.0
msg.trigger_value = 0.0
end_t = time.monotonic() + 1.0
while time.monotonic() < end_t:
    msg.header.stamp = node.get_clock().now().to_msg()
    pub.publish(msg)
    rclpy.spin_once(node, timeout_sec=0.0)
    time.sleep(dt)

print('hardcoded_pick_sequence_done')
node.destroy_node()
rclpy.shutdown()
PY"

if [[ "${RESTART_RECEIVER}" == "1" ]]; then
  echo "[info] Restarting quest_controller_receiver..."
  docker exec "${CONTAINER}" bash -lc "source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && nohup /opt/ros/humble/bin/ros2 run receiver quest_controller_receiver >/tmp/qcr.log 2>&1 < /dev/null &"
fi

echo "[ok] Hard-coded pick sequence finished."
