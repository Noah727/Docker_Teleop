import json
import socket
import threading
import time

import rclpy
from rclpy.executors import ExternalShutdownException
from geometry_msgs.msg import Pose
from rclpy.node import Node
from teleop_bridge_msgs.msg import ReceivedPoseStates


class QuestControllerReceiver(Node):
    def __init__(self):
        super().__init__("quest_controller_receiver")

        self.declare_parameter("listen_ip", "0.0.0.0")
        self.declare_parameter("listen_port", 5005)
        self.declare_parameter("output_topic", "/received_pose_states")
        self.declare_parameter("frame_id", "unity_world")
        self.declare_parameter("publish_rate_hz", 60.0)
        self.declare_parameter("stale_timeout_sec", 0.25)
        self.declare_parameter("analog_press_threshold", 0.55)

        listen_ip = str(self.get_parameter("listen_ip").value)
        listen_port = int(self.get_parameter("listen_port").value)
        output_topic = str(self.get_parameter("output_topic").value)
        self.frame_id = str(self.get_parameter("frame_id").value)
        publish_rate_hz = max(1.0, float(self.get_parameter("publish_rate_hz").value))
        self.stale_timeout_sec = max(0.05, float(self.get_parameter("stale_timeout_sec").value))
        self.analog_press_threshold = float(self.get_parameter("analog_press_threshold").value)

        self.pub = self.create_publisher(ReceivedPoseStates, output_topic, 20)

        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.sock.bind((listen_ip, listen_port))
        self.sock.settimeout(0.5)

        self._state_lock = threading.Lock()
        self._latest_state = self._neutral_state(source="startup")
        self._last_packet_time_monotonic = 0.0
        self._last_sender = "none"

        self._last_log_time = time.monotonic()
        self._packet_count_window = 0
        self._window_start = time.monotonic()

        self.rx_thread = threading.Thread(target=self._rx_loop, daemon=True)
        self.rx_thread.start()

        self.create_timer(1.0 / publish_rate_hz, self._publish_loop)

        self.get_logger().info(
            f"QuestControllerReceiver listening on UDP {listen_ip}:{listen_port}, "
            f"publishing {output_topic} @ {publish_rate_hz:.1f} Hz"
        )
        self.get_logger().info(
            f"stale_timeout_sec={self.stale_timeout_sec:.3f}, "
            f"analog_press_threshold={self.analog_press_threshold:.2f}, frame_id={self.frame_id}"
        )

    def _rx_loop(self):
        while rclpy.ok():
            try:
                data, addr = self.sock.recvfrom(4096)
            except socket.timeout:
                continue
            except OSError:
                return
            except Exception as exc:
                self.get_logger().error(f"UDP recv error: {exc}")
                continue

            payload_text = data.decode("utf-8", errors="ignore")
            try:
                payload = json.loads(payload_text)
            except json.JSONDecodeError:
                continue

            parsed_state = self._parse_payload(payload)
            now = time.monotonic()

            with self._state_lock:
                self._latest_state = parsed_state
                self._last_packet_time_monotonic = now
                self._last_sender = f"{addr[0]}:{addr[1]}"
                self._packet_count_window += 1

    def _parse_payload(self, payload):
        if not isinstance(payload, dict):
            return self._neutral_state(source="invalid_packet")

        controls = payload.get("controls")
        if not isinstance(controls, dict):
            controls = {}

        right_hand = payload.get("right_hand")
        if not isinstance(right_hand, dict):
            right_hand = {}

        tracked = self._as_bool(right_hand.get("isTracked", False))

        pose = Pose()
        pose.orientation.w = 1.0

        position = right_hand.get("pos")
        if isinstance(position, dict):
            pose.position.x = self._as_float(position.get("x"), 0.0)
            pose.position.y = self._as_float(position.get("y"), 0.0)
            pose.position.z = self._as_float(position.get("z"), 0.0)

        rotation = right_hand.get("rot")
        if isinstance(rotation, dict):
            pose.orientation.x = self._as_float(rotation.get("x"), 0.0)
            pose.orientation.y = self._as_float(rotation.get("y"), 0.0)
            pose.orientation.z = self._as_float(rotation.get("z"), 0.0)
            pose.orientation.w = self._as_float(rotation.get("w"), 1.0)

        close_flag = self._as_optional_bool(
            controls.get("close_enable", controls.get("close_held"))
        )
        open_flag = self._as_optional_bool(
            controls.get("open_enable", controls.get("open_held"))
        )
        reset_flag = self._as_optional_bool(
            controls.get("reset_enable", controls.get("reset_held"))
        )

        grip_value = self._as_float(
            controls.get("grip_value"),
            1.0 if close_flag else 0.0,
        )
        trigger_value = self._as_float(
            controls.get("trigger_value"),
            1.0 if open_flag else 0.0,
        )

        close_enable = close_flag if close_flag is not None else grip_value >= self.analog_press_threshold
        open_enable = open_flag if open_flag is not None else trigger_value >= self.analog_press_threshold

        rotate_enable = self._as_bool(
            controls.get("rotate_enable", controls.get("rotate_held", False))
        )
        reset_enable = reset_flag if reset_flag is not None else False

        source = controls.get("source", "quest_right_controller")
        source = str(source) if source is not None else "quest_right_controller"

        return {
            "tracked": tracked,
            "pose": pose,
            "grip_value": grip_value,
            "trigger_value": trigger_value,
            "rotate_enable": rotate_enable,
            "close_enable": close_enable,
            "open_enable": open_enable,
            "reset_enable": reset_enable,
            "source": source,
        }

    def _publish_loop(self):
        now_monotonic = time.monotonic()
        now_msg = self.get_clock().now().to_msg()

        with self._state_lock:
            age = now_monotonic - self._last_packet_time_monotonic if self._last_packet_time_monotonic > 0.0 else float("inf")
            stale = age > self.stale_timeout_sec
            state = self._latest_state if not stale else self._neutral_state(source="stale_timeout")
            sender = self._last_sender
            packet_count = self._packet_count_window
            window_dt = max(now_monotonic - self._window_start, 1e-6)
            if now_monotonic - self._last_log_time > 2.0:
                rx_hz = packet_count / window_dt
                self.get_logger().info(
                    f"RX {rx_hz:.1f} Hz from {sender}, stale={stale}, tracked={state['tracked']}, "
                    f"rotate={state['rotate_enable']}, close={state['close_enable']}, open={state['open_enable']}, reset={state['reset_enable']}, "
                    f"grip={state['grip_value']:.2f}, trigger={state['trigger_value']:.2f}"
                )
                self._packet_count_window = 0
                self._window_start = now_monotonic
                self._last_log_time = now_monotonic

        msg = ReceivedPoseStates()
        msg.header.stamp = now_msg
        msg.header.frame_id = self.frame_id
        msg.tracked = bool(state["tracked"])
        msg.pose = state["pose"]
        msg.grip_value = float(state["grip_value"])
        msg.trigger_value = float(state["trigger_value"])
        msg.rotate_enable = bool(state["rotate_enable"])
        msg.close_enable = bool(state["close_enable"])
        msg.open_enable = bool(state["open_enable"])
        msg.reset_enable = bool(state["reset_enable"])
        msg.source = str(state["source"])
        self.pub.publish(msg)

    @staticmethod
    def _as_float(value, default):
        try:
            return float(value)
        except (TypeError, ValueError):
            return float(default)

    @staticmethod
    def _as_bool(value):
        if isinstance(value, bool):
            return value
        if isinstance(value, (int, float)):
            return value != 0
        if isinstance(value, str):
            return value.strip().lower() in ("1", "true", "yes", "on")
        return False

    @staticmethod
    def _as_optional_bool(value):
        if value is None:
            return None
        return QuestControllerReceiver._as_bool(value)

    @staticmethod
    def _neutral_state(source):
        pose = Pose()
        pose.orientation.w = 1.0
        return {
            "tracked": False,
            "pose": pose,
            "grip_value": 0.0,
            "trigger_value": 0.0,
            "rotate_enable": False,
            "close_enable": False,
            "open_enable": False,
            "reset_enable": False,
            "source": source,
        }

    def destroy_node(self):
        try:
            self.sock.close()
        except Exception:
            pass
        super().destroy_node()


def main(args=None):
    rclpy.init(args=args)
    node = QuestControllerReceiver()
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
