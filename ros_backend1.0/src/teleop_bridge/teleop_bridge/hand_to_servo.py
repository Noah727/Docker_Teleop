import json
import math
import socket
import threading
import time

import numpy as np
import rclpy
from geometry_msgs.msg import Pose, PoseStamped, Vector3Stamped
from rcl_interfaces.msg import ParameterDescriptor
from rclpy.node import Node
from std_msgs.msg import Int8

# UDP Configuration (Internal within container)
UDP_IP = "0.0.0.0"
UDP_PORT = 5005


class HandToServo(Node):
    def __init__(self):
        super().__init__("hand_to_servo")

        numeric_array_param = ParameterDescriptor(dynamic_typing=True)

        self.declare_parameter("target_frame", "base_link")
        self.declare_parameter("map_axes", ["x", "y", "z"])
        self.declare_parameter("map_signs", [-1.0, 1.0, 1.0], numeric_array_param)
        self.declare_parameter("scale_xyz", [1.0, 1.0, 1.0], numeric_array_param)
        self.declare_parameter("offset_xyz", [0.0, 0.0, 0.0], numeric_array_param)
        self.declare_parameter("min_xyz", [0.15, -0.50, 0.05], numeric_array_param)
        self.declare_parameter("max_xyz", [0.75, 0.50, 0.70], numeric_array_param)
        self.declare_parameter("angular_cmd_topic", "/teleop_bridge/angular_twist_cmd")
        self.declare_parameter("gripper_cmd_topic", "/teleop_bridge/gripper_hold_cmd")
        self.declare_parameter("max_angular_speed", 1.5)
        self.declare_parameter("angular_deadband", 0.02)

        # Publisher for Servo pose target commands (pose-tracking mode)
        self.target_pub_stamped = self.create_publisher(PoseStamped, "/servo_node/target_pose", 10)

        # Publisher for debugging/Unity visualization of target
        self.target_pub = self.create_publisher(Pose, "/hand_target_pose", 10)

        # Publisher for angular command (consumed by pose_to_servo_twist)
        angular_topic = str(self.get_parameter("angular_cmd_topic").value)
        self.angular_pub = self.create_publisher(Vector3Stamped, angular_topic, 10)

        # Publisher for hold-to-move gripper command (-1 open, 0 stop, +1 close)
        gripper_topic = str(self.get_parameter("gripper_cmd_topic").value)
        self.gripper_pub = self.create_publisher(Int8, gripper_topic, 10)

        self.get_logger().info("HandToServo node started. Listening for UDP data...")
        self.get_logger().info("Input priority: right_hand (tracked) -> hand.position -> root.position")

        # UDP setup
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.sock.bind((UDP_IP, UDP_PORT))

        # State
        self.latest_hand_pos = None  # [x, y, z] in ROS mapped space
        self.latest_angular_cmd = np.array([0.0, 0.0, 0.0], dtype=float)
        self.latest_gripper_cmd = 0
        self.last_input_source = "none"

        self.prev_unity_rot = None
        self.prev_unity_rot_time = None

        self._last_debug_log_time = 0.0
        self._rx_window_start = time.monotonic()
        self._rx_packet_count = 0
        self._last_raw_packet = ""
        self._last_sender = ""

        # Parameters
        self.target_frame = str(self.get_parameter("target_frame").value)

        self.map_axes = [str(v).lower() for v in self.get_parameter("map_axes").value]
        if len(self.map_axes) != 3 or any(axis not in ("x", "y", "z") for axis in self.map_axes):
            self.get_logger().warn("Invalid map_axes; expected 3 values from {x,y,z}. Falling back to ['x','y','z'].")
            self.map_axes = ["x", "y", "z"]

        self.map_signs = np.array(self.get_parameter("map_signs").value, dtype=float)
        if self.map_signs.shape != (3,):
            self.get_logger().warn("Invalid map_signs; expected 3 values. Falling back to [-1,1,1].")
            self.map_signs = np.array([-1.0, 1.0, 1.0], dtype=float)

        self.scale_xyz = np.array(self.get_parameter("scale_xyz").value, dtype=float)
        if self.scale_xyz.shape != (3,):
            self.get_logger().warn("Invalid scale_xyz; expected 3 values. Falling back to [1,1,1].")
            self.scale_xyz = np.array([1.0, 1.0, 1.0], dtype=float)

        self.offset_xyz = np.array(self.get_parameter("offset_xyz").value, dtype=float)
        if self.offset_xyz.shape != (3,):
            self.get_logger().warn("Invalid offset_xyz; expected 3 values. Falling back to [0,0,0].")
            self.offset_xyz = np.array([0.0, 0.0, 0.0], dtype=float)

        self.min_xyz = np.array(self.get_parameter("min_xyz").value, dtype=float)
        self.max_xyz = np.array(self.get_parameter("max_xyz").value, dtype=float)

        self.max_angular_speed = max(0.0, float(self.get_parameter("max_angular_speed").value))
        self.angular_deadband = max(0.0, float(self.get_parameter("angular_deadband").value))

        self.get_logger().info(
            f"target_frame={self.target_frame}, map_axes={self.map_axes}, map_signs={self.map_signs.tolist()}, "
            f"scale_xyz={self.scale_xyz.tolist()}, offset_xyz={self.offset_xyz.tolist()}, "
            f"min_xyz={self.min_xyz.tolist()}, max_xyz={self.max_xyz.tolist()}, "
            f"max_angular_speed={self.max_angular_speed:.3f}, angular_deadband={self.angular_deadband:.3f}"
        )

        # Start UDP thread
        self.packet_thread = threading.Thread(target=self.receive_udp)
        self.packet_thread.daemon = True
        self.packet_thread.start()

        # Timer for control loop (30Hz)
        self.create_timer(1.0 / 30.0, self.control_loop)

    def receive_udp(self):
        while rclpy.ok():
            try:
                data, addr = self.sock.recvfrom(2048)
                text = data.decode("utf-8")
                self._rx_packet_count += 1
                self._last_raw_packet = text
                self._last_sender = f"{addr[0]}:{addr[1]}"

                try:
                    msg = json.loads(text)

                    rotate_held, close_held, open_held, unity_rot = self._parse_controls(msg)
                    self.latest_gripper_cmd = self._arbitrate_gripper(close_held, open_held)
                    self.latest_angular_cmd = self._compute_angular_cmd(unity_rot, rotate_held)

                    unity_pos = None
                    input_source = None

                    right_hand = msg.get("right_hand")
                    if isinstance(right_hand, dict) and right_hand.get("isTracked", False):
                        pos = right_hand.get("pos")
                        if isinstance(pos, dict) and all(k in pos for k in ("x", "y", "z")):
                            unity_pos = pos
                            input_source = "right_hand"

                    if unity_pos is None and "hand" in msg and isinstance(msg["hand"], dict):
                        hand_pos = msg["hand"].get("position")
                        if isinstance(hand_pos, dict) and all(k in hand_pos for k in ("x", "y", "z")):
                            unity_pos = hand_pos
                            input_source = "hand.position"

                    if unity_pos is None and "position" in msg and isinstance(msg["position"], dict):
                        if all(k in msg["position"] for k in ("x", "y", "z")):
                            unity_pos = msg["position"]
                            input_source = "root.position"

                    if unity_pos is not None:
                        ux = float(unity_pos["x"])
                        uy = float(unity_pos["y"])
                        uz = float(unity_pos["z"])

                        unity_xyz = np.array([ux, uy, uz], dtype=float)
                        mapped_ros = self._map_unity_vector(unity_xyz)

                        self.latest_hand_pos = (mapped_ros * self.scale_xyz) + self.offset_xyz
                        self.last_input_source = input_source if input_source is not None else "unknown"

                    now = time.monotonic()
                    if now - self._last_debug_log_time > 2.0:
                        dt = max(now - self._rx_window_start, 1e-6)
                        rx_hz = self._rx_packet_count / dt
                        raw_preview = self._last_raw_packet.replace("\n", " ")[:180]
                        self.get_logger().info(
                            f"RX {rx_hz:.1f} Hz from {self._last_sender} | raw='{raw_preview}'"
                        )
                        if self.latest_hand_pos is not None:
                            self.get_logger().info(
                                f"Input={self.last_input_source} target=({self.latest_hand_pos[0]:.3f}, "
                                f"{self.latest_hand_pos[1]:.3f}, {self.latest_hand_pos[2]:.3f}) "
                                f"ang=({self.latest_angular_cmd[0]:.3f}, {self.latest_angular_cmd[1]:.3f}, {self.latest_angular_cmd[2]:.3f}) "
                                f"gripper={self.latest_gripper_cmd}"
                            )
                        self._rx_window_start = now
                        self._rx_packet_count = 0
                        self._last_debug_log_time = now

                except json.JSONDecodeError:
                    continue

            except Exception as e:
                self.get_logger().error(f"UDP error: {e}")

    def _parse_controls(self, msg):
        controls = msg.get("controls") if isinstance(msg, dict) else None
        right_hand = msg.get("right_hand") if isinstance(msg, dict) else None

        rotate_held = False
        close_held = False
        open_held = False

        if isinstance(controls, dict):
            rotate_held = self._bool_from_any(controls.get("rotate_held", False))
            close_held = self._bool_from_any(controls.get("close_held", False))
            open_held = self._bool_from_any(controls.get("open_held", False))

        unity_rot = None
        if isinstance(controls, dict):
            unity_rot = self._quat_from_dict(controls.get("right_controller_rot"))

        if unity_rot is None and isinstance(right_hand, dict) and right_hand.get("isTracked", False):
            unity_rot = self._quat_from_dict(right_hand.get("rot"))

        return rotate_held, close_held, open_held, unity_rot

    @staticmethod
    def _bool_from_any(value):
        if isinstance(value, bool):
            return value
        if isinstance(value, (int, float)):
            return value != 0
        if isinstance(value, str):
            return value.strip().lower() in ("1", "true", "yes", "on")
        return False

    @staticmethod
    def _quat_from_dict(obj):
        if not isinstance(obj, dict):
            return None
        if not all(k in obj for k in ("x", "y", "z", "w")):
            return None
        q = np.array([float(obj["x"]), float(obj["y"]), float(obj["z"]), float(obj["w"])], dtype=float)
        n = float(np.linalg.norm(q))
        if n < 1e-9:
            return None
        return q / n

    def _compute_angular_cmd(self, unity_rot, rotate_held):
        now = time.monotonic()

        if unity_rot is None:
            self.prev_unity_rot = None
            self.prev_unity_rot_time = None
            return np.array([0.0, 0.0, 0.0], dtype=float)

        if self.prev_unity_rot is None or self.prev_unity_rot_time is None:
            self.prev_unity_rot = unity_rot
            self.prev_unity_rot_time = now
            return np.array([0.0, 0.0, 0.0], dtype=float)

        dt = max(1e-4, now - self.prev_unity_rot_time)
        omega_unity = self._quat_delta_to_angular_velocity(self.prev_unity_rot, unity_rot, dt)

        # Always advance the baseline to avoid jumps after button toggles.
        self.prev_unity_rot = unity_rot
        self.prev_unity_rot_time = now

        if not rotate_held:
            return np.array([0.0, 0.0, 0.0], dtype=float)

        mapped = self._map_unity_vector(omega_unity)

        mag = float(np.linalg.norm(mapped))
        if mag < self.angular_deadband:
            return np.array([0.0, 0.0, 0.0], dtype=float)

        if self.max_angular_speed > 0.0 and mag > self.max_angular_speed:
            mapped = mapped * (self.max_angular_speed / mag)

        return mapped

    @staticmethod
    def _quat_delta_to_angular_velocity(q_prev, q_curr, dt):
        q_delta = HandToServo._quat_multiply(q_curr, HandToServo._quat_conjugate(q_prev))
        if q_delta[3] < 0.0:
            q_delta = -q_delta

        xyz = q_delta[:3]
        w = max(-1.0, min(1.0, float(q_delta[3])))
        xyz_norm = float(np.linalg.norm(xyz))
        if xyz_norm < 1e-9:
            return np.array([0.0, 0.0, 0.0], dtype=float)

        angle = 2.0 * math.atan2(xyz_norm, w)
        axis = xyz / xyz_norm
        return axis * (angle / max(dt, 1e-4))

    @staticmethod
    def _quat_conjugate(q):
        return np.array([-q[0], -q[1], -q[2], q[3]], dtype=float)

    @staticmethod
    def _quat_multiply(a, b):
        ax, ay, az, aw = a
        bx, by, bz, bw = b
        return np.array(
            [
                aw * bx + ax * bw + ay * bz - az * by,
                aw * by - ax * bz + ay * bw + az * bx,
                aw * bz + ax * by - ay * bx + az * bw,
                aw * bw - ax * bx - ay * by - az * bz,
            ],
            dtype=float,
        )

    def _map_unity_vector(self, unity_xyz):
        lookup = {
            "x": float(unity_xyz[0]),
            "y": float(unity_xyz[1]),
            "z": float(unity_xyz[2]),
        }
        return np.array(
            [
                lookup[self.map_axes[0]],
                lookup[self.map_axes[1]],
                lookup[self.map_axes[2]],
            ],
            dtype=float,
        ) * self.map_signs

    @staticmethod
    def _arbitrate_gripper(close_held, open_held):
        if close_held and not open_held:
            return 1
        if open_held and not close_held:
            return -1
        return 0

    def control_loop(self):
        now = self.get_clock().now().to_msg()

        if self.latest_hand_pos is not None:
            target_xyz = np.clip(self.latest_hand_pos, self.min_xyz, self.max_xyz)

            target = PoseStamped()
            target.header.stamp = now
            target.header.frame_id = self.target_frame
            target.pose.position.x = float(target_xyz[0])
            target.pose.position.y = float(target_xyz[1])
            target.pose.position.z = float(target_xyz[2])
            target.pose.orientation.w = 1.0
            self.target_pub_stamped.publish(target)

            debug_pose = Pose()
            debug_pose.position.x = float(target_xyz[0])
            debug_pose.position.y = float(target_xyz[1])
            debug_pose.position.z = float(target_xyz[2])
            debug_pose.orientation.w = 1.0
            self.target_pub.publish(debug_pose)

        ang = Vector3Stamped()
        ang.header.stamp = now
        ang.header.frame_id = self.target_frame
        ang.vector.x = float(self.latest_angular_cmd[0])
        ang.vector.y = float(self.latest_angular_cmd[1])
        ang.vector.z = float(self.latest_angular_cmd[2])
        self.angular_pub.publish(ang)

        grip = Int8()
        grip.data = int(self.latest_gripper_cmd)
        self.gripper_pub.publish(grip)


def main(args=None):
    rclpy.init(args=args)
    node = HandToServo()
    rclpy.spin(node)
    node.destroy_node()
    rclpy.shutdown()


if __name__ == "__main__":
    main()
