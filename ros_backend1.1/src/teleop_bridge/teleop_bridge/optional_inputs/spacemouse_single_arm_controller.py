import time

import numpy as np
import rclpy
from rcl_interfaces.msg import SetParametersResult
from rclpy.executors import ExternalShutdownException
from rclpy.node import Node
from teleop_bridge_msgs.msg import TargetTwistStates

try:
    import hid  # type: ignore
except ImportError:  # pragma: no cover - depends on host/container image
    hid = None


class SpaceMouseSingleArmController(Node):
    """Read a 3Dconnexion SpaceMouse through HID and publish TargetTwistStates."""

    def __init__(self):
        super().__init__("spacemouse_single_arm_controller")

        self.declare_parameter("output_topic", "/target_twist_states")
        self.declare_parameter("frame_id", "base_link")
        self.declare_parameter("publish_rate_hz", 60.0)
        self.declare_parameter("device_vendor_id", 0x256F)
        self.declare_parameter("device_product_id", 0)
        self.declare_parameter("device_name_filter", "SpaceMouse")
        self.declare_parameter("raw_full_scale", 350.0)
        self.declare_parameter("deadband", 0.06)
        self.declare_parameter("stale_timeout_sec", 0.25)
        self.declare_parameter("linear_speed_xyz", [0.35, 0.35, 0.30])
        self.declare_parameter("linear_sign_xyz", [-1.0, -1.0, -1.0])
        self.declare_parameter("linear_axis_map", ["x", "y", "z"])
        self.declare_parameter("angular_speed_xyz", [1.4, 1.4, 1.4])
        self.declare_parameter("angular_sign_xyz", [1.0, 1.0, 1.0])
        self.declare_parameter("angular_axis_map", ["x", "y", "z"])
        self.declare_parameter("gripper_button_index", 0)

        self.output_topic = str(self.get_parameter("output_topic").value)
        self.frame_id = str(self.get_parameter("frame_id").value)
        self.publish_rate_hz = max(1.0, float(self.get_parameter("publish_rate_hz").value))
        self.vendor_id = int(self.get_parameter("device_vendor_id").value)
        self.product_id = int(self.get_parameter("device_product_id").value)
        self.name_filter = str(self.get_parameter("device_name_filter").value).lower().strip()
        self.raw_full_scale = max(1.0, float(self.get_parameter("raw_full_scale").value))
        self.deadband = max(0.0, float(self.get_parameter("deadband").value))
        self.stale_timeout_sec = max(0.05, float(self.get_parameter("stale_timeout_sec").value))
        self.linear_speed_xyz = self._parse_vec3(
            self.get_parameter("linear_speed_xyz").value,
            np.array([0.35, 0.35, 0.30], dtype=float),
            "linear_speed_xyz",
        )
        self.linear_sign_xyz = self._parse_vec3(
            self.get_parameter("linear_sign_xyz").value,
            np.array([-1.0, -1.0, -1.0], dtype=float),
            "linear_sign_xyz",
        )
        self.linear_axis_map = self._parse_axis_map(self.get_parameter("linear_axis_map").value)
        self.angular_speed_xyz = self._parse_vec3(
            self.get_parameter("angular_speed_xyz").value,
            np.array([1.4, 1.4, 1.4], dtype=float),
            "angular_speed_xyz",
        )
        self.angular_sign_xyz = self._parse_vec3(
            self.get_parameter("angular_sign_xyz").value,
            np.array([1.0, 1.0, 1.0], dtype=float),
            "angular_sign_xyz",
        )
        self.angular_axis_map = self._parse_axis_map(self.get_parameter("angular_axis_map").value)
        self.gripper_button_index = max(0, int(self.get_parameter("gripper_button_index").value))

        self.pub = self.create_publisher(TargetTwistStates, self.output_topic, 20)
        self.add_on_set_parameters_callback(self._on_params)
        self.create_timer(1.0 / self.publish_rate_hz, self._publish_loop)
        self.create_timer(0.005, self._poll_device)

        self._device = None
        self._translation_raw = np.zeros(3, dtype=float)
        self._rotation_raw = np.zeros(3, dtype=float)
        self._last_motion_time = 0.0
        self._buttons = 0
        self._last_buttons = 0
        self._gripper_closed = False
        self._gripper_has_command = False
        self._last_device_log = 0.0
        self._report_counts = {}

        self._open_device()
        self.get_logger().info(
            f"SpaceMouse controller started: output_topic={self.output_topic}, frame_id={self.frame_id}, "
            f"vendor=0x{self.vendor_id:04x}, product=0x{self.product_id:04x}, name_filter={self.name_filter!r}, "
            f"linear_axis_map={self.linear_axis_map}, linear_speed_xyz={self.linear_speed_xyz.tolist()}, "
            f"linear_sign_xyz={self.linear_sign_xyz.tolist()}, angular_axis_map={self.angular_axis_map}, "
            f"angular_speed_xyz={self.angular_speed_xyz.tolist()}, angular_sign_xyz={self.angular_sign_xyz.tolist()}"
        )

    @staticmethod
    def _parse_vec3(value, default: np.ndarray, name: str) -> np.ndarray:
        try:
            vec = np.array(value, dtype=float)
        except (TypeError, ValueError):
            return default
        if vec.shape != (3,):
            return default
        return vec

    @staticmethod
    def _require_vec3(value, name: str) -> np.ndarray:
        try:
            vec = np.array(value, dtype=float)
        except (TypeError, ValueError) as exc:
            raise ValueError(f"{name} must be a numeric array with 3 values") from exc
        if vec.shape != (3,):
            raise ValueError(f"{name} must be a numeric array with 3 values")
        return vec

    @staticmethod
    def _parse_axis_map(value):
        axes = [str(v).lower().strip() for v in value]
        if len(axes) != 3 or any(axis not in ("x", "y", "z") for axis in axes):
            return ["x", "y", "z"]
        return axes

    @staticmethod
    def _map_axes(raw: np.ndarray, axes) -> np.ndarray:
        idx = {"x": 0, "y": 1, "z": 2}
        return np.array([raw[idx[axis]] for axis in axes], dtype=float)

    def _on_params(self, params):
        restart_only = {"output_topic", "publish_rate_hz", "device_vendor_id", "device_product_id", "device_name_filter"}
        try:
            for param in params:
                if param.name in restart_only:
                    return SetParametersResult(successful=False, reason=f"{param.name} requires restarting SpaceMouse node")
                if param.name == "frame_id":
                    self.frame_id = str(param.value)
                elif param.name == "raw_full_scale":
                    self.raw_full_scale = max(1.0, float(param.value))
                elif param.name == "deadband":
                    self.deadband = max(0.0, float(param.value))
                elif param.name == "stale_timeout_sec":
                    self.stale_timeout_sec = max(0.05, float(param.value))
                elif param.name == "linear_speed_xyz":
                    self.linear_speed_xyz = self._require_vec3(param.value, param.name)
                elif param.name == "linear_sign_xyz":
                    self.linear_sign_xyz = self._require_vec3(param.value, param.name)
                elif param.name == "linear_axis_map":
                    self.linear_axis_map = self._parse_axis_map(param.value)
                elif param.name == "angular_speed_xyz":
                    self.angular_speed_xyz = self._require_vec3(param.value, param.name)
                elif param.name == "angular_sign_xyz":
                    self.angular_sign_xyz = self._require_vec3(param.value, param.name)
                elif param.name == "angular_axis_map":
                    self.angular_axis_map = self._parse_axis_map(param.value)
                elif param.name == "gripper_button_index":
                    self.gripper_button_index = max(0, int(param.value))
        except ValueError as exc:
            return SetParametersResult(successful=False, reason=str(exc))
        return SetParametersResult(successful=True)

    def _open_device(self):
        if hid is None:
            self.get_logger().error(
                "Python hidapi module is not installed. Rebuild the backend image or install python hidapi."
            )
            return

        matches = []
        for info in hid.enumerate():
            vendor = int(info.get("vendor_id", 0))
            product = int(info.get("product_id", 0))
            product_name = str(info.get("product_string") or "")
            manufacturer = str(info.get("manufacturer_string") or "")
            if self.vendor_id and vendor != self.vendor_id:
                continue
            if self.product_id and product != self.product_id:
                continue
            haystack = f"{manufacturer} {product_name}".lower()
            if self.name_filter and self.name_filter not in haystack and "3dconnexion" not in haystack:
                continue
            matches.append(info)

        if not matches:
            devices = []
            for info in hid.enumerate():
                vendor = int(info.get("vendor_id", 0))
                product = int(info.get("product_id", 0))
                product_name = str(info.get("product_string") or "")
                manufacturer = str(info.get("manufacturer_string") or "")
                devices.append(f"0x{vendor:04x}:0x{product:04x} {manufacturer} {product_name}".strip())
            self.get_logger().error(
                "No SpaceMouse HID device found. Visible HID devices: " + ("; ".join(devices) if devices else "none")
            )
            return

        info = matches[0]
        try:
            dev = hid.device()
            dev.open_path(info["path"])
            dev.set_nonblocking(True)
            self._device = dev
            self.get_logger().info(
                "Opened SpaceMouse HID device: "
                f"0x{int(info.get('vendor_id', 0)):04x}:0x{int(info.get('product_id', 0)):04x} "
                f"{info.get('manufacturer_string', '')} {info.get('product_string', '')}"
            )
        except Exception as exc:
            self.get_logger().error(f"Failed to open SpaceMouse HID device: {exc}")

    @staticmethod
    def _i16(lo: int, hi: int) -> int:
        return int.from_bytes(bytes([lo & 0xFF, hi & 0xFF]), byteorder="little", signed=True)

    def _unpack_vec3(self, payload, offset=0):
        if len(payload) < offset + 6:
            return None
        return np.array(
            [
                self._i16(payload[offset + 0], payload[offset + 1]),
                self._i16(payload[offset + 2], payload[offset + 3]),
                self._i16(payload[offset + 4], payload[offset + 5]),
            ],
            dtype=float,
        )

    def _poll_device(self):
        if self._device is None:
            now = time.monotonic()
            if now - self._last_device_log > 5.0:
                self._last_device_log = now
                self.get_logger().warn("SpaceMouse device is not open; waiting for restart after device/permission fix.")
            return

        while True:
            try:
                data = self._device.read(64)
            except Exception as exc:
                self.get_logger().error(f"SpaceMouse read failed: {exc}")
                self._device = None
                return
            if not data:
                return
            self._handle_report(data)

    def _handle_report(self, data):
        if len(data) < 2:
            return
        report_id = int(data[0])
        payload = data[1:]
        self._report_counts[report_id] = self._report_counts.get(report_id, 0) + 1
        if report_id == 1 and len(payload) >= 12:
            # Some SpaceMouse HID interfaces combine translation and rotation in
            # one motion report instead of separate report IDs 1 and 2.
            translation = self._unpack_vec3(payload, 0)
            rotation = self._unpack_vec3(payload, 6)
            if translation is not None:
                self._translation_raw = translation
            if rotation is not None:
                self._rotation_raw = rotation
            self._last_motion_time = time.monotonic()
        elif report_id in (1, 2) and len(payload) >= 6:
            vals = self._unpack_vec3(payload, 0)
            if vals is None:
                return
            if report_id == 1:
                self._translation_raw = vals
            elif report_id == 2:
                self._rotation_raw = vals
            self._last_motion_time = time.monotonic()
        elif report_id == 3 and payload:
            buttons = 0
            for i, value in enumerate(payload[:4]):
                buttons |= int(value) << (8 * i)
            self._buttons = buttons
            mask = 1 << self.gripper_button_index
            if (self._buttons & mask) and not (self._last_buttons & mask):
                self._gripper_closed = not self._gripper_closed
                self._gripper_has_command = True
                state = "closing" if self._gripper_closed else "opening"
                self.get_logger().info(f"SpaceMouse gripper toggle from button {self.gripper_button_index}: {state}")
            self._last_buttons = self._buttons

    def _normalize_raw(self, raw: np.ndarray) -> np.ndarray:
        values = np.clip(raw / self.raw_full_scale, -1.0, 1.0)
        values[np.abs(values) < self.deadband] = 0.0
        return values

    def _publish_loop(self):
        now = time.monotonic()
        stale = (now - self._last_motion_time) > self.stale_timeout_sec
        translation = np.zeros(3, dtype=float) if stale else self._normalize_raw(self._translation_raw)
        rotation = np.zeros(3, dtype=float) if stale else self._normalize_raw(self._rotation_raw)
        linear = self._map_axes(translation, self.linear_axis_map) * self.linear_speed_xyz * self.linear_sign_xyz
        angular = self._map_axes(rotation, self.angular_axis_map) * self.angular_speed_xyz * self.angular_sign_xyz

        msg = TargetTwistStates()
        msg.header.stamp = self.get_clock().now().to_msg()
        msg.header.frame_id = self.frame_id
        msg.twist.linear.x = float(linear[0])
        msg.twist.linear.y = float(linear[1])
        msg.twist.linear.z = float(linear[2])
        msg.twist.angular.x = float(angular[0])
        msg.twist.angular.y = float(angular[1])
        msg.twist.angular.z = float(angular[2])
        msg.tracked = self._device is not None
        if self._gripper_has_command:
            msg.gripper_cmd = 1 if self._gripper_closed else -1
        else:
            msg.gripper_cmd = 0
        self.pub.publish(msg)

    def destroy_node(self):
        if self._device is not None:
            try:
                self._device.close()
            except Exception:
                pass
        super().destroy_node()


def main(args=None):
    rclpy.init(args=args)
    node = SpaceMouseSingleArmController()
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
