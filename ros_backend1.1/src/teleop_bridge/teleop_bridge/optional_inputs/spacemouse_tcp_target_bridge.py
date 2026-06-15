import json
import socket
import time

import numpy as np
import rclpy
from rclpy.executors import ExternalShutdownException
from rclpy.node import Node
from teleop_bridge_msgs.msg import TargetTwistStates


class SpaceMouseTcpTargetBridge(Node):
    """Receive host-side SpaceMouse packets and publish TargetTwistStates."""

    def __init__(self):
        super().__init__("spacemouse_tcp_target_bridge")

        self.declare_parameter("listen_host", "0.0.0.0")
        self.declare_parameter("listen_port", 5036)
        self.declare_parameter("output_topic", "/target_twist_states")
        self.declare_parameter("frame_id", "base_link")
        self.declare_parameter("publish_rate_hz", 60.0)
        self.declare_parameter("stale_timeout_sec", 0.25)

        self.listen_host = str(self.get_parameter("listen_host").value)
        self.listen_port = int(self.get_parameter("listen_port").value)
        self.output_topic = str(self.get_parameter("output_topic").value)
        self.frame_id = str(self.get_parameter("frame_id").value)
        publish_rate_hz = max(1.0, float(self.get_parameter("publish_rate_hz").value))
        self.stale_timeout_sec = max(0.05, float(self.get_parameter("stale_timeout_sec").value))

        self.pub = self.create_publisher(TargetTwistStates, self.output_topic, 20)
        self.create_timer(0.005, self._poll_socket)
        self.create_timer(1.0 / publish_rate_hz, self._publish_loop)

        self._server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self._server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self._server.bind((self.listen_host, self.listen_port))
        self._server.listen(1)
        self._server.setblocking(False)

        self._client = None
        self._client_addr = None
        self._rx_buffer = b""
        self._last_packet_time = 0.0
        self._linear = np.zeros(3, dtype=float)
        self._angular = np.zeros(3, dtype=float)
        self._tracked = False
        self._gripper_cmd = 0
        self._packet_count = 0
        self._decode_errors = 0
        self._last_status_log = 0.0

        self.get_logger().info(
            f"SpaceMouse TCP target bridge listening on {self.listen_host}:{self.listen_port}, "
            f"publishing {self.output_topic}, frame_id={self.frame_id}"
        )

    @staticmethod
    def _parse_vec3(value, field_name):
        vec = np.array(value, dtype=float)
        if vec.shape != (3,):
            raise ValueError(f"{field_name} must have exactly 3 numeric values")
        return vec

    @staticmethod
    def _clamp_gripper_cmd(value):
        try:
            cmd = int(value)
        except (TypeError, ValueError):
            return 0
        if cmd > 0:
            return 1
        if cmd < 0:
            return -1
        return 0

    def _poll_socket(self):
        if self._client is None:
            try:
                client, addr = self._server.accept()
            except BlockingIOError:
                return
            client.setblocking(False)
            self._client = client
            self._client_addr = addr
            self._rx_buffer = b""
            self._last_packet_time = 0.0
            self.get_logger().info(f"SpaceMouse host connected from {addr[0]}:{addr[1]}")

        while self._client is not None:
            try:
                chunk = self._client.recv(4096)
            except BlockingIOError:
                return
            except OSError as exc:
                self.get_logger().warn(f"SpaceMouse host socket error: {exc}")
                self._close_client()
                return

            if not chunk:
                self.get_logger().warn("SpaceMouse host disconnected.")
                self._close_client()
                return

            self._rx_buffer += chunk
            while b"\n" in self._rx_buffer:
                line, self._rx_buffer = self._rx_buffer.split(b"\n", 1)
                self._handle_line(line)

    def _handle_line(self, line):
        if not line.strip():
            return
        try:
            packet = json.loads(line.decode("utf-8"))
            self._linear = self._parse_vec3(packet.get("linear", [0.0, 0.0, 0.0]), "linear")
            self._angular = self._parse_vec3(packet.get("angular", [0.0, 0.0, 0.0]), "angular")
            self._tracked = bool(packet.get("tracked", True))
            self._gripper_cmd = self._clamp_gripper_cmd(packet.get("gripper_cmd", 0))
            self._last_packet_time = time.monotonic()
            self._packet_count += 1
        except (UnicodeDecodeError, json.JSONDecodeError, TypeError, ValueError) as exc:
            self._decode_errors += 1
            if self._decode_errors <= 5 or self._decode_errors % 50 == 0:
                self.get_logger().warn(f"Bad SpaceMouse TCP packet #{self._decode_errors}: {exc}")

    def _close_client(self):
        if self._client is not None:
            try:
                self._client.close()
            except OSError:
                pass
        self._client = None
        self._client_addr = None
        self._rx_buffer = b""
        self._tracked = False
        self._linear[:] = 0.0
        self._angular[:] = 0.0
        self._gripper_cmd = 0

    def _publish_loop(self):
        now = time.monotonic()
        stale = (now - self._last_packet_time) > self.stale_timeout_sec
        tracked = self._client is not None and self._tracked and not stale
        linear = self._linear if tracked else np.zeros(3, dtype=float)
        angular = self._angular if tracked else np.zeros(3, dtype=float)

        msg = TargetTwistStates()
        msg.header.stamp = self.get_clock().now().to_msg()
        msg.header.frame_id = self.frame_id
        msg.twist.linear.x = float(linear[0])
        msg.twist.linear.y = float(linear[1])
        msg.twist.linear.z = float(linear[2])
        msg.twist.angular.x = float(angular[0])
        msg.twist.angular.y = float(angular[1])
        msg.twist.angular.z = float(angular[2])
        msg.tracked = bool(tracked)
        msg.gripper_cmd = int(self._gripper_cmd if tracked else 0)
        self.pub.publish(msg)

        if now - self._last_status_log > 2.0:
            self._last_status_log = now
            self.get_logger().info(
                f"packets={self._packet_count}, connected={self._client is not None}, "
                f"tracked={tracked}, stale={stale}, lin=({linear[0]:.3f},{linear[1]:.3f},{linear[2]:.3f}), "
                f"ang=({angular[0]:.3f},{angular[1]:.3f},{angular[2]:.3f}), gripper_cmd={msg.gripper_cmd}"
            )

    def destroy_node(self):
        self._close_client()
        try:
            self._server.close()
        except OSError:
            pass
        super().destroy_node()


def main(args=None):
    rclpy.init(args=args)
    node = SpaceMouseTcpTargetBridge()
    try:
        rclpy.spin(node)
    except (KeyboardInterrupt, ExternalShutdownException):
        pass
    finally:
        try:
            node.destroy_node()
        finally:
            if rclpy.ok():
                rclpy.shutdown()


if __name__ == "__main__":
    main()
