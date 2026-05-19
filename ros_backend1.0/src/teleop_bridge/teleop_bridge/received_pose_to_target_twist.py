import math
import time

import numpy as np
import rclpy
from rcl_interfaces.msg import SetParametersResult
from rclpy.executors import ExternalShutdownException
from rclpy.node import Node
from teleop_bridge_msgs.msg import ReceivedPoseStates, TargetTwistStates
from tf2_ros import Buffer, TransformListener


class ReceivedPoseToTargetTwist(Node):
    def __init__(self):
        super().__init__("received_pose_to_target_twist")

        self.declare_parameter("input_topic", "/received_pose_states")
        self.declare_parameter("output_topic", "/target_twist_states")
        self.declare_parameter("target_frame", "base_link")
        self.declare_parameter("ee_frame", "tool0")
        self.declare_parameter("publish_rate_hz", 60.0)
        self.declare_parameter("stale_timeout_sec", 0.25)
        self.declare_parameter("reset_hold_sec", 8.0)
        self.declare_parameter("position_mapping_mode", "world_delta")

        self.declare_parameter("kp_linear", 2.0)
        self.declare_parameter("max_linear_speed", 0.30)
        self.declare_parameter("linear_deadband", 0.005)

        self.declare_parameter("kp_angular", 4.0)
        self.declare_parameter("max_angular_speed", 1.50)
        self.declare_parameter("angular_deadband", 0.02)
        self.declare_parameter("start_in_gamepad_mode", False)
        self.declare_parameter("control_mode", "hand_pose")
        self.declare_parameter("gamepad_deadband", 0.15)
        self.declare_parameter("gamepad_linear_speed_xyz", [0.20, 0.20, 0.20])
        self.declare_parameter("gamepad_linear_sign_xyz", [1.0, -1.0, 1.0])
        self.declare_parameter("gamepad_angular_speed_xyz", [0.0, 0.75, 0.75])
        self.declare_parameter("gamepad_angular_sign_xyz", [1.0, 1.0, 1.0])

        # Calibrated default for this project setup:
        # Unity/controller: x=forward/back, y=up/down, z=left/right
        # Robot base_link:   x=forward/back, y=left/right, z=up/down
        self.declare_parameter("map_axes", ["x", "z", "y"])
        self.declare_parameter("map_signs", [1.0, -1.0, 1.0])
        self.declare_parameter("rot_map_axes", ["x", "z", "y"])
        self.declare_parameter("rot_map_signs", [1.0, -1.0, 1.0])
        self.declare_parameter("rot_scale_xyz", [1.0, 1.0, 1.0])
        self.declare_parameter("scale_xyz", [1.0, 1.0, 1.0])
        self.declare_parameter("offset_xyz", [0.0, 0.0, 0.0])
        self.declare_parameter("min_xyz", [0.15, -0.50, 0.05])
        self.declare_parameter("max_xyz", [0.75, 0.50, 0.70])

        input_topic = str(self.get_parameter("input_topic").value)
        output_topic = str(self.get_parameter("output_topic").value)
        self.target_frame = str(self.get_parameter("target_frame").value)
        self.ee_frame = str(self.get_parameter("ee_frame").value)
        publish_rate_hz = max(1.0, float(self.get_parameter("publish_rate_hz").value))
        self.stale_timeout_sec = max(0.05, float(self.get_parameter("stale_timeout_sec").value))
        self.reset_hold_sec = max(0.05, float(self.get_parameter("reset_hold_sec").value))
        try:
            self.position_mapping_mode = self._parse_position_mapping_mode(
                self.get_parameter("position_mapping_mode").value
            )
        except ValueError as exc:
            self.get_logger().warn(f"{exc}; falling back to world_delta.")
            self.position_mapping_mode = "world_delta"

        self.kp_linear = max(0.0, float(self.get_parameter("kp_linear").value))
        self.max_linear_speed = max(0.0, float(self.get_parameter("max_linear_speed").value))
        self.linear_deadband = max(0.0, float(self.get_parameter("linear_deadband").value))

        self.kp_angular = max(0.0, float(self.get_parameter("kp_angular").value))
        self.max_angular_speed = max(0.0, float(self.get_parameter("max_angular_speed").value))
        self.angular_deadband = max(0.0, float(self.get_parameter("angular_deadband").value))
        self.start_in_gamepad_mode = bool(self.get_parameter("start_in_gamepad_mode").value)
        try:
            self.control_mode = self._parse_control_mode(self.get_parameter("control_mode").value)
        except ValueError as exc:
            self.get_logger().warn(f"{exc}; falling back to start_in_gamepad_mode.")
            self.control_mode = "gamepad" if self.start_in_gamepad_mode else "hand_pose"
        self.gamepad_deadband = max(0.0, float(self.get_parameter("gamepad_deadband").value))
        self.gamepad_linear_speed_xyz = np.array(self.get_parameter("gamepad_linear_speed_xyz").value, dtype=float)
        if self.gamepad_linear_speed_xyz.shape != (3,):
            self.get_logger().warn("Invalid gamepad_linear_speed_xyz; falling back to [0.20,0.20,0.20].")
            self.gamepad_linear_speed_xyz = np.array([0.20, 0.20, 0.20], dtype=float)
        self.gamepad_linear_sign_xyz = np.array(self.get_parameter("gamepad_linear_sign_xyz").value, dtype=float)
        if self.gamepad_linear_sign_xyz.shape != (3,):
            self.get_logger().warn("Invalid gamepad_linear_sign_xyz; falling back to [1,-1,1].")
            self.gamepad_linear_sign_xyz = np.array([1.0, -1.0, 1.0], dtype=float)
        self.gamepad_angular_speed_xyz = np.array(self.get_parameter("gamepad_angular_speed_xyz").value, dtype=float)
        if self.gamepad_angular_speed_xyz.shape != (3,):
            self.get_logger().warn("Invalid gamepad_angular_speed_xyz; falling back to [0,0.75,0.75].")
            self.gamepad_angular_speed_xyz = np.array([0.0, 0.75, 0.75], dtype=float)
        self.gamepad_angular_sign_xyz = np.array(self.get_parameter("gamepad_angular_sign_xyz").value, dtype=float)
        if self.gamepad_angular_sign_xyz.shape != (3,):
            self.get_logger().warn("Invalid gamepad_angular_sign_xyz; falling back to [1,1,1].")
            self.gamepad_angular_sign_xyz = np.array([1.0, 1.0, 1.0], dtype=float)

        self.map_axes = [str(v).lower() for v in self.get_parameter("map_axes").value]
        if len(self.map_axes) != 3 or any(axis not in ("x", "y", "z") for axis in self.map_axes):
            self.get_logger().warn("Invalid map_axes; falling back to ['x','z','y'].")
            self.map_axes = ["x", "z", "y"]

        self.map_signs = np.array(self.get_parameter("map_signs").value, dtype=float)
        if self.map_signs.shape != (3,):
            self.get_logger().warn("Invalid map_signs; falling back to [1,-1,1].")
            self.map_signs = np.array([1.0, -1.0, 1.0], dtype=float)

        self.rot_map_axes = [str(v).lower() for v in self.get_parameter("rot_map_axes").value]
        if len(self.rot_map_axes) != 3 or any(axis not in ("x", "y", "z") for axis in self.rot_map_axes):
            self.get_logger().warn("Invalid rot_map_axes; falling back to ['x','z','y'].")
            self.rot_map_axes = ["x", "z", "y"]

        self.rot_map_signs = np.array(self.get_parameter("rot_map_signs").value, dtype=float)
        if self.rot_map_signs.shape != (3,):
            self.get_logger().warn("Invalid rot_map_signs; falling back to [1,-1,1].")
            self.rot_map_signs = np.array([1.0, -1.0, 1.0], dtype=float)

        self.rot_scale_xyz = np.array(self.get_parameter("rot_scale_xyz").value, dtype=float)
        if self.rot_scale_xyz.shape != (3,):
            self.get_logger().warn("Invalid rot_scale_xyz; falling back to [1,1,1].")
            self.rot_scale_xyz = np.array([1.0, 1.0, 1.0], dtype=float)

        self.scale_xyz = np.array(self.get_parameter("scale_xyz").value, dtype=float)
        if self.scale_xyz.shape != (3,):
            self.get_logger().warn("Invalid scale_xyz; falling back to [1,1,1].")
            self.scale_xyz = np.array([1.0, 1.0, 1.0], dtype=float)

        self.offset_xyz = np.array(self.get_parameter("offset_xyz").value, dtype=float)
        if self.offset_xyz.shape != (3,):
            self.get_logger().warn("Invalid offset_xyz; falling back to [0,0,0].")
            self.offset_xyz = np.array([0.0, 0.0, 0.0], dtype=float)

        self.min_xyz = np.array(self.get_parameter("min_xyz").value, dtype=float)
        if self.min_xyz.shape != (3,):
            self.get_logger().warn("Invalid min_xyz; falling back to [0.15,-0.50,0.05].")
            self.min_xyz = np.array([0.15, -0.50, 0.05], dtype=float)

        self.max_xyz = np.array(self.get_parameter("max_xyz").value, dtype=float)
        if self.max_xyz.shape != (3,):
            self.get_logger().warn("Invalid max_xyz; falling back to [0.75,0.50,0.70].")
            self.max_xyz = np.array([0.75, 0.50, 0.70], dtype=float)
        if np.any(self.min_xyz > self.max_xyz):
            self.get_logger().warn("Invalid workspace bounds (min_xyz > max_xyz); using defaults.")
            self.min_xyz = np.array([0.15, -0.50, 0.05], dtype=float)
            self.max_xyz = np.array([0.75, 0.50, 0.70], dtype=float)

        self.sub = self.create_subscription(ReceivedPoseStates, input_topic, self._on_pose_states, 20)
        self.pub = self.create_publisher(TargetTwistStates, output_topic, 20)
        self.create_timer(1.0 / publish_rate_hz, self._publish_loop)
        self._tf_buffer = Buffer()
        self._tf_listener = TransformListener(self._tf_buffer, self)

        self._last_rx_time = 0.0
        self._tracked = False
        self._rotate_enable = False
        self._gripper_cmd = 0
        self._reset_button = False
        self._reset_button_last = False
        self._reset_latched = False
        self._reset_latch_until = 0.0
        self._recenter_button_last = False
        self._recenter_hold_active = False
        self._teleop_enable = False
        self._teleop_enable_last = False
        self._mode_switch_last = False
        self._gamepad_mode = self.control_mode == "gamepad"
        self._left_stick = np.zeros(2, dtype=float)
        self._right_stick = np.zeros(2, dtype=float)
        self._left_grip_value = 0.0
        self._left_trigger_value = 0.0

        self._target_pos = np.zeros(3, dtype=float)
        self._latest_hand_pos = np.zeros(3, dtype=float)
        self._position_session_active = False
        self._position_recenter_pending = False
        self._position_recenter_hand_ref_valid = False
        self._position_recenter_hand_ref = np.zeros(3, dtype=float)
        self._position_hand_ref = np.zeros(3, dtype=float)
        self._position_ee_ref = np.zeros(3, dtype=float)
        self._hand_rot = np.array([0.0, 0.0, 0.0, 1.0], dtype=float)
        self._have_target = False

        self._rotate_session_active = False
        self._rotate_hand_ref = np.array([0.0, 0.0, 0.0, 1.0], dtype=float)
        self._rotate_ee_ref = np.array([0.0, 0.0, 0.0, 1.0], dtype=float)

        self._rx_count = 0
        self._rx_window_start = time.monotonic()
        self._last_log_time = time.monotonic()
        self._last_tf_warn_time = 0.0

        self.get_logger().info(
            f"Mapping {input_topic} -> {output_topic}, target_frame={self.target_frame}, "
            f"ee_frame={self.ee_frame}, stale_timeout_sec={self.stale_timeout_sec:.3f}"
        )
        self.get_logger().info(
            f"linear: kp={self.kp_linear:.3f}, max={self.max_linear_speed:.3f}, deadband={self.linear_deadband:.3f} | "
            f"angular: kp={self.kp_angular:.3f}, max={self.max_angular_speed:.3f}, deadband={self.angular_deadband:.3f}"
        )
        self.get_logger().info(
            f"control_mode_default={'gamepad' if self._gamepad_mode else 'hand_pose'}, "
            f"gamepad_deadband={self.gamepad_deadband:.3f}, "
            f"gamepad_linear_speed_xyz={self.gamepad_linear_speed_xyz.tolist()}, "
            f"gamepad_linear_sign_xyz={self.gamepad_linear_sign_xyz.tolist()}, "
            f"gamepad_angular_speed_xyz={self.gamepad_angular_speed_xyz.tolist()}, "
            f"gamepad_angular_sign_xyz={self.gamepad_angular_sign_xyz.tolist()}"
        )
        self.get_logger().info(
            f"pos_map_axes={self.map_axes}, pos_map_signs={self.map_signs.tolist()}, "
            f"position_mapping_mode={self.position_mapping_mode}, "
            f"rot_map_axes={self.rot_map_axes}, rot_map_signs={self.rot_map_signs.tolist()}, rot_scale_xyz={self.rot_scale_xyz.tolist()}, "
            f"scale_xyz={self.scale_xyz.tolist()}, offset_xyz={self.offset_xyz.tolist()}, "
            f"min_xyz={self.min_xyz.tolist()}, max_xyz={self.max_xyz.tolist()}"
        )
        self.add_on_set_parameters_callback(self._on_parameter_change)

    @staticmethod
    def _parse_position_mapping_mode(value) -> str:
        mode = str(value).strip().lower()
        aliases = {
            "delta": "world_delta",
            "unity_world_delta": "world_delta",
            "world": "world_delta",
            "old": "absolute",
            "legacy": "absolute",
        }
        mode = aliases.get(mode, mode)
        if mode not in ("absolute", "world_delta"):
            raise ValueError("position_mapping_mode must be 'world_delta' or 'absolute'")
        return mode

    @staticmethod
    def _parse_control_mode(value) -> str:
        mode = str(value).strip().lower()
        aliases = {
            "hand": "hand_pose",
            "pose": "hand_pose",
            "controller": "hand_pose",
            "controller_pose": "hand_pose",
            "game": "gamepad",
            "thumbstick": "gamepad",
        }
        mode = aliases.get(mode, mode)
        if mode not in ("hand_pose", "gamepad"):
            raise ValueError("control_mode must be 'hand_pose' or 'gamepad'")
        return mode

    @staticmethod
    def _parse_map_axes(value):
        axes = [str(v).lower() for v in value]
        if len(axes) != 3 or any(axis not in ("x", "y", "z") for axis in axes):
            raise ValueError("map_axes must be a 3-item list using only x/y/z.")
        return axes

    @staticmethod
    def _parse_vec3(value, field_name: str) -> np.ndarray:
        vec = np.array(value, dtype=float)
        if vec.shape != (3,):
            raise ValueError(f"{field_name} must be a 3-item numeric list.")
        return vec

    def _on_parameter_change(self, params):
        new_target_frame = self.target_frame
        new_ee_frame = self.ee_frame
        new_stale_timeout_sec = self.stale_timeout_sec
        new_reset_hold_sec = self.reset_hold_sec
        new_position_mapping_mode = self.position_mapping_mode
        new_kp_linear = self.kp_linear
        new_max_linear_speed = self.max_linear_speed
        new_linear_deadband = self.linear_deadband
        new_kp_angular = self.kp_angular
        new_max_angular_speed = self.max_angular_speed
        new_angular_deadband = self.angular_deadband
        new_start_in_gamepad_mode = self.start_in_gamepad_mode
        new_control_mode = self.control_mode
        new_gamepad_deadband = self.gamepad_deadband
        new_gamepad_linear_speed_xyz = self.gamepad_linear_speed_xyz
        new_gamepad_linear_sign_xyz = self.gamepad_linear_sign_xyz
        new_gamepad_angular_speed_xyz = self.gamepad_angular_speed_xyz
        new_gamepad_angular_sign_xyz = self.gamepad_angular_sign_xyz
        new_map_axes = self.map_axes
        new_map_signs = self.map_signs
        new_rot_map_axes = self.rot_map_axes
        new_rot_map_signs = self.rot_map_signs
        new_rot_scale_xyz = self.rot_scale_xyz
        new_scale_xyz = self.scale_xyz
        new_offset_xyz = self.offset_xyz
        new_min_xyz = self.min_xyz
        new_max_xyz = self.max_xyz
        touched = []

        try:
            for param in params:
                if param.name == "map_axes":
                    new_map_axes = self._parse_map_axes(param.value)
                    touched.append("map_axes")
                elif param.name == "map_signs":
                    new_map_signs = self._parse_vec3(param.value, "map_signs")
                    touched.append("map_signs")
                elif param.name == "position_mapping_mode":
                    new_position_mapping_mode = self._parse_position_mapping_mode(param.value)
                    touched.append("position_mapping_mode")
                elif param.name == "rot_map_axes":
                    new_rot_map_axes = self._parse_map_axes(param.value)
                    touched.append("rot_map_axes")
                elif param.name == "rot_map_signs":
                    new_rot_map_signs = self._parse_vec3(param.value, "rot_map_signs")
                    touched.append("rot_map_signs")
                elif param.name == "rot_scale_xyz":
                    new_rot_scale_xyz = self._parse_vec3(param.value, "rot_scale_xyz")
                    touched.append("rot_scale_xyz")
                elif param.name == "scale_xyz":
                    new_scale_xyz = self._parse_vec3(param.value, "scale_xyz")
                    touched.append("scale_xyz")
                elif param.name == "offset_xyz":
                    new_offset_xyz = self._parse_vec3(param.value, "offset_xyz")
                    touched.append("offset_xyz")
                elif param.name == "min_xyz":
                    new_min_xyz = self._parse_vec3(param.value, "min_xyz")
                    touched.append("min_xyz")
                elif param.name == "max_xyz":
                    new_max_xyz = self._parse_vec3(param.value, "max_xyz")
                    touched.append("max_xyz")
                elif param.name == "target_frame":
                    new_target_frame = str(param.value)
                    touched.append("target_frame")
                elif param.name == "ee_frame":
                    new_ee_frame = str(param.value)
                    touched.append("ee_frame")
                elif param.name == "stale_timeout_sec":
                    new_stale_timeout_sec = max(0.05, float(param.value))
                    touched.append("stale_timeout_sec")
                elif param.name == "reset_hold_sec":
                    new_reset_hold_sec = max(0.05, float(param.value))
                    touched.append("reset_hold_sec")
                elif param.name == "kp_linear":
                    new_kp_linear = max(0.0, float(param.value))
                    touched.append("kp_linear")
                elif param.name == "max_linear_speed":
                    new_max_linear_speed = max(0.0, float(param.value))
                    touched.append("max_linear_speed")
                elif param.name == "linear_deadband":
                    new_linear_deadband = max(0.0, float(param.value))
                    touched.append("linear_deadband")
                elif param.name == "kp_angular":
                    new_kp_angular = max(0.0, float(param.value))
                    touched.append("kp_angular")
                elif param.name == "max_angular_speed":
                    new_max_angular_speed = max(0.0, float(param.value))
                    touched.append("max_angular_speed")
                elif param.name == "angular_deadband":
                    new_angular_deadband = max(0.0, float(param.value))
                    touched.append("angular_deadband")
                elif param.name == "start_in_gamepad_mode":
                    new_start_in_gamepad_mode = bool(param.value)
                    new_control_mode = "gamepad" if new_start_in_gamepad_mode else "hand_pose"
                    touched.append("start_in_gamepad_mode")
                elif param.name == "control_mode":
                    new_control_mode = self._parse_control_mode(param.value)
                    new_start_in_gamepad_mode = new_control_mode == "gamepad"
                    touched.append("control_mode")
                elif param.name == "gamepad_deadband":
                    new_gamepad_deadband = max(0.0, float(param.value))
                    touched.append("gamepad_deadband")
                elif param.name == "gamepad_linear_speed_xyz":
                    new_gamepad_linear_speed_xyz = self._parse_vec3(param.value, "gamepad_linear_speed_xyz")
                    touched.append("gamepad_linear_speed_xyz")
                elif param.name == "gamepad_linear_sign_xyz":
                    new_gamepad_linear_sign_xyz = self._parse_vec3(param.value, "gamepad_linear_sign_xyz")
                    touched.append("gamepad_linear_sign_xyz")
                elif param.name == "gamepad_angular_speed_xyz":
                    new_gamepad_angular_speed_xyz = self._parse_vec3(param.value, "gamepad_angular_speed_xyz")
                    touched.append("gamepad_angular_speed_xyz")
                elif param.name == "gamepad_angular_sign_xyz":
                    new_gamepad_angular_sign_xyz = self._parse_vec3(param.value, "gamepad_angular_sign_xyz")
                    touched.append("gamepad_angular_sign_xyz")
        except ValueError as exc:
            return SetParametersResult(successful=False, reason=str(exc))

        if np.any(new_min_xyz > new_max_xyz):
            return SetParametersResult(
                successful=False, reason="Invalid workspace bounds: min_xyz must be <= max_xyz."
            )

        self.target_frame = new_target_frame
        self.ee_frame = new_ee_frame
        self.stale_timeout_sec = new_stale_timeout_sec
        self.reset_hold_sec = new_reset_hold_sec
        old_position_mapping_mode = self.position_mapping_mode
        self.position_mapping_mode = new_position_mapping_mode
        self.kp_linear = new_kp_linear
        self.max_linear_speed = new_max_linear_speed
        self.linear_deadband = new_linear_deadband
        self.kp_angular = new_kp_angular
        self.max_angular_speed = new_max_angular_speed
        self.angular_deadband = new_angular_deadband
        self.start_in_gamepad_mode = new_start_in_gamepad_mode
        self.control_mode = new_control_mode
        self.gamepad_deadband = new_gamepad_deadband
        self.gamepad_linear_speed_xyz = new_gamepad_linear_speed_xyz
        self.gamepad_linear_sign_xyz = new_gamepad_linear_sign_xyz
        self.gamepad_angular_speed_xyz = new_gamepad_angular_speed_xyz
        self.gamepad_angular_sign_xyz = new_gamepad_angular_sign_xyz
        self.map_axes = new_map_axes
        self.map_signs = new_map_signs
        self.rot_map_axes = new_rot_map_axes
        self.rot_map_signs = new_rot_map_signs
        self.rot_scale_xyz = new_rot_scale_xyz
        self.scale_xyz = new_scale_xyz
        self.offset_xyz = new_offset_xyz
        self.min_xyz = new_min_xyz
        self.max_xyz = new_max_xyz

        if (
            old_position_mapping_mode != self.position_mapping_mode
            or any(
                name in touched
                for name in (
                    "target_frame",
                    "ee_frame",
                    "map_axes",
                    "map_signs",
                    "scale_xyz",
                    "offset_xyz",
                )
            )
        ):
            self._position_session_active = False

        if touched:
            if "control_mode" in touched or "start_in_gamepad_mode" in touched:
                self._gamepad_mode = self.control_mode == "gamepad"
                self._request_position_recenter()
                self.get_logger().warn(f"Control mode set to {self.control_mode.upper()} by parameter update.")

            self.get_logger().info(
                f"Updated mapper params ({', '.join(touched)}): "
                f"target_frame={self.target_frame}, ee_frame={self.ee_frame}, "
                f"position_mapping_mode={self.position_mapping_mode}, control_mode={self.control_mode}, "
                f"kp_linear={self.kp_linear:.3f}, max_linear_speed={self.max_linear_speed:.3f}, linear_deadband={self.linear_deadband:.4f}, "
                f"kp_angular={self.kp_angular:.3f}, max_angular_speed={self.max_angular_speed:.3f}, angular_deadband={self.angular_deadband:.4f}, "
                f"gamepad_deadband={self.gamepad_deadband:.3f}, "
                f"gamepad_linear_speed_xyz={self.gamepad_linear_speed_xyz.tolist()}, "
                f"gamepad_linear_sign_xyz={self.gamepad_linear_sign_xyz.tolist()}, "
                f"gamepad_angular_speed_xyz={self.gamepad_angular_speed_xyz.tolist()}, "
                f"gamepad_angular_sign_xyz={self.gamepad_angular_sign_xyz.tolist()}, "
                f"pos_map_axes={self.map_axes}, pos_map_signs={self.map_signs.tolist()}, "
                f"rot_map_axes={self.rot_map_axes}, rot_map_signs={self.rot_map_signs.tolist()}, rot_scale_xyz={self.rot_scale_xyz.tolist()}, "
                f"scale_xyz={self.scale_xyz.tolist()}, offset_xyz={self.offset_xyz.tolist()}, "
                f"min_xyz={self.min_xyz.tolist()}, max_xyz={self.max_xyz.tolist()}"
            )

        return SetParametersResult(successful=True)

    def _on_pose_states(self, msg: ReceivedPoseStates):
        self._last_rx_time = time.monotonic()
        self._rx_count += 1

        self._tracked = bool(msg.tracked)
        self._rotate_enable = bool(msg.rotate_enable)
        self._gripper_cmd = self._arbitrate_gripper(msg.close_enable, msg.open_enable)
        reset_button = bool(msg.reset_enable)
        if reset_button and (not self._reset_button_last):
            self._reset_latched = True
            self._reset_latch_until = time.monotonic() + self.reset_hold_sec
            self._request_position_recenter()
            self.get_logger().warn(
                f"Reset requested by B-button edge; mapper will pause teleop for "
                f"{self.reset_hold_sec:.1f}s while reset manager runs."
            )
        recenter_button = bool(msg.recenter_enable)
        if recenter_button and (not self._recenter_button_last):
            self._request_position_recenter()
            self.get_logger().warn("Right thumbstick clutch held: arm motion paused until release.")
        elif (not recenter_button) and self._recenter_button_last:
            self.get_logger().warn("Right thumbstick clutch released: current hand pose will become the new position reference.")
        teleop_enable = bool(msg.teleop_enable)
        if teleop_enable and (not self._teleop_enable_last):
            self._request_position_recenter()
            self.get_logger().warn("Teleop engaged by right grip: current hand pose will become the new position reference.")
        elif (not teleop_enable) and self._teleop_enable_last:
            self._position_session_active = False
            self._rotate_session_active = False
            self.get_logger().warn("Teleop disengaged by right grip release.")
        mode_switch = bool(msg.mode_switch_enable)
        if mode_switch and (not self._mode_switch_last):
            self._gamepad_mode = not self._gamepad_mode
            self._request_position_recenter()
            mode_name = "GAMEPAD" if self._gamepad_mode else "HAND_POSE"
            self.get_logger().warn(f"Control mode toggled to {mode_name} by left Y-button edge.")
        self._reset_button = reset_button
        self._reset_button_last = reset_button
        self._recenter_button_last = recenter_button
        self._recenter_hold_active = recenter_button
        self._teleop_enable = teleop_enable
        self._teleop_enable_last = teleop_enable
        self._mode_switch_last = mode_switch

        if not self._tracked:
            self._have_target = False
            self._position_session_active = False
            self._rotate_session_active = False
            return

        unity_pos = np.array(
            [float(msg.pose.position.x), float(msg.pose.position.y), float(msg.pose.position.z)],
            dtype=float,
        )
        mapped_pos = self._map_unity_vector(unity_pos)
        self._latest_hand_pos = mapped_pos
        if self._position_recenter_pending:
            self._position_recenter_hand_ref = mapped_pos.copy()
            self._position_recenter_hand_ref_valid = True
        if self.position_mapping_mode == "absolute":
            scaled_pos = (mapped_pos * self.scale_xyz) + self.offset_xyz
            self._target_pos = np.clip(scaled_pos, self.min_xyz, self.max_xyz)
        self._have_target = True

        q = np.array(
            [
                float(msg.pose.orientation.x),
                float(msg.pose.orientation.y),
                float(msg.pose.orientation.z),
                float(msg.pose.orientation.w),
            ],
            dtype=float,
        )
        self._hand_rot = self._normalize_quat(q)
        self._left_stick = np.array([float(msg.left_stick_x), float(msg.left_stick_y)], dtype=float)
        self._right_stick = np.array([float(msg.right_stick_x), float(msg.right_stick_y)], dtype=float)
        self._left_grip_value = float(msg.left_grip_value)
        self._left_trigger_value = float(msg.left_trigger_value)

        if not self._rotate_enable:
            self._rotate_session_active = False

    def _publish_loop(self):
        now = time.monotonic()
        stale = (now - self._last_rx_time) > self.stale_timeout_sec
        input_active = self._tracked and self._have_target and not stale
        arm_input_active = input_active and self._teleop_enable

        linear = np.zeros(3, dtype=float)
        angular = np.zeros(3, dtype=float)
        gripper_cmd = self._gripper_cmd if input_active else 0
        rotate_enable = False
        tracked_active = False
        reset_enable = False
        tf_ok = False

        if self._reset_latched and now >= self._reset_latch_until:
            self._reset_latched = False
            self._reset_latch_until = 0.0
            self.get_logger().warn("Reset pause auto-released; teleop can resume.")

        # Reset is held only briefly after a B-button edge. This gives the reset
        # manager a clean rising edge without leaving teleop blocked forever.
        reset_enable = self._reset_latched

        if reset_enable:
            tracked_active = True
            self._position_session_active = False
            self._rotate_session_active = False
        elif self._recenter_hold_active:
            tracked_active = input_active
            self._position_session_active = False
            self._rotate_session_active = False
        elif arm_input_active:
            if self._gamepad_mode:
                tracked_active = True
                self._position_session_active = False
                linear = self._compute_gamepad_linear()
                if self._rotate_enable:
                    tf_ok, _, ee_rot = self._lookup_ee_pose(now)
                    if tf_ok:
                        if not self._rotate_session_active:
                            self._rotate_hand_ref = self._hand_rot.copy()
                            self._rotate_ee_ref = ee_rot.copy()
                            self._rotate_session_active = True

                        hand_delta_rotvec = self._quat_delta_to_rotvec(self._rotate_hand_ref, self._hand_rot)
                        mapped_delta_rotvec = self._map_rotation_delta(hand_delta_rotvec)
                        target_delta_quat = self._rotvec_to_quat(mapped_delta_rotvec)
                        target_ee_rot = self._normalize_quat(self._quat_multiply(target_delta_quat, self._rotate_ee_ref))

                        angular_error = self._quat_delta_to_rotvec(ee_rot, target_ee_rot)
                        angular = self.kp_angular * angular_error
                        angular = self._apply_speed_limits(angular, self.max_angular_speed, self.angular_deadband)
                        rotate_enable = True
                    else:
                        self._rotate_session_active = False
                else:
                    self._rotate_session_active = False
                    angular = self._compute_gamepad_angular()
                    if np.linalg.norm(angular) > 0.0:
                        rotate_enable = True
            else:
                tf_ok, ee_pos, ee_rot = self._lookup_ee_pose(now)
                if tf_ok:
                    tracked_active = True

                    target_pos = self._compute_position_target(ee_pos)
                    linear_error = target_pos - ee_pos
                    linear = self.kp_linear * linear_error
                    linear = self._apply_speed_limits(linear, self.max_linear_speed, self.linear_deadband)

                    if self._rotate_enable:
                        if not self._rotate_session_active:
                            self._rotate_hand_ref = self._hand_rot.copy()
                            self._rotate_ee_ref = ee_rot.copy()
                            self._rotate_session_active = True

                        hand_delta_rotvec = self._quat_delta_to_rotvec(self._rotate_hand_ref, self._hand_rot)
                        mapped_delta_rotvec = self._map_rotation_delta(hand_delta_rotvec)
                        target_delta_quat = self._rotvec_to_quat(mapped_delta_rotvec)
                        target_ee_rot = self._normalize_quat(self._quat_multiply(target_delta_quat, self._rotate_ee_ref))

                        angular_error = self._quat_delta_to_rotvec(ee_rot, target_ee_rot)
                        angular = self.kp_angular * angular_error
                        angular = self._apply_speed_limits(angular, self.max_angular_speed, self.angular_deadband)
                        rotate_enable = True
                    else:
                        self._rotate_session_active = False

                else:
                    self._rotate_session_active = False

        else:
            self._position_session_active = False
            self._rotate_session_active = False

        msg = TargetTwistStates()
        msg.header.stamp = self.get_clock().now().to_msg()
        msg.header.frame_id = self.target_frame

        msg.twist.linear.x = float(linear[0])
        msg.twist.linear.y = float(linear[1])
        msg.twist.linear.z = float(linear[2])
        msg.twist.angular.x = float(angular[0])
        msg.twist.angular.y = float(angular[1])
        msg.twist.angular.z = float(angular[2])
        msg.gripper_cmd = int(gripper_cmd)
        msg.rotate_enable = bool(rotate_enable)
        msg.tracked = bool(tracked_active or (input_active and gripper_cmd != 0))
        msg.reset_enable = bool(reset_enable)
        self.pub.publish(msg)

        if now - self._last_log_time > 2.0:
            dt = max(now - self._rx_window_start, 1e-6)
            rx_hz = self._rx_count / dt
            self.get_logger().info(
                f"RX {rx_hz:.1f} Hz, mode={'gamepad' if self._gamepad_mode else 'hand_pose'}, "
                f"stale={stale}, tracked={tracked_active}, tf_ok={tf_ok}, rotate={rotate_enable}, "
                f"reset={reset_enable}, teleop={self._teleop_enable}, clutch={self._recenter_hold_active}, gripper_cmd={gripper_cmd}, lin=({linear[0]:.3f},{linear[1]:.3f},{linear[2]:.3f}), "
                f"ang=({angular[0]:.3f},{angular[1]:.3f},{angular[2]:.3f})"
            )
            self._rx_count = 0
            self._rx_window_start = now
            self._last_log_time = now

    @staticmethod
    def _arbitrate_gripper(close_enable: bool, open_enable: bool) -> int:
        if close_enable and not open_enable:
            return 1
        if open_enable and not close_enable:
            return -1
        return 0

    @staticmethod
    def _remap_vector(unity_xyz: np.ndarray, axes, signs: np.ndarray) -> np.ndarray:
        lookup = {
            "x": float(unity_xyz[0]),
            "y": float(unity_xyz[1]),
            "z": float(unity_xyz[2]),
        }
        return np.array(
            [
                lookup[axes[0]],
                lookup[axes[1]],
                lookup[axes[2]],
            ],
            dtype=float,
        ) * signs

    def _map_unity_vector(self, unity_xyz: np.ndarray) -> np.ndarray:
        return self._remap_vector(unity_xyz, self.map_axes, self.map_signs)

    def _map_rotation_delta(self, unity_rotvec: np.ndarray) -> np.ndarray:
        return self._remap_vector(unity_rotvec, self.rot_map_axes, self.rot_map_signs) * self.rot_scale_xyz

    def _request_position_recenter(self):
        self._position_session_active = False
        self._position_recenter_pending = True
        self._position_recenter_hand_ref_valid = False

    def _compute_position_target(self, ee_pos: np.ndarray) -> np.ndarray:
        if self.position_mapping_mode == "absolute":
            return self._target_pos

        if (not self._position_session_active) or self._position_recenter_pending:
            if self._position_recenter_hand_ref_valid:
                self._position_hand_ref = self._position_recenter_hand_ref.copy()
            else:
                self._position_hand_ref = self._latest_hand_pos.copy()
            self._position_ee_ref = ee_pos.copy()
            self._position_session_active = True
            self._position_recenter_pending = False
            self._position_recenter_hand_ref_valid = False
            self.get_logger().info(
                "Captured world_delta position reference: "
                f"hand={self._position_hand_ref.tolist()}, ee={self._position_ee_ref.tolist()}"
            )

        hand_delta = self._latest_hand_pos - self._position_hand_ref
        target_pos = self._position_ee_ref + (hand_delta * self.scale_xyz) + self.offset_xyz
        self._target_pos = np.clip(target_pos, self.min_xyz, self.max_xyz)
        return self._target_pos

    def _compute_gamepad_linear(self) -> np.ndarray:
        raw = np.array(
            [
                float(self._left_stick[1]),
                float(self._left_stick[0]),
                float(self._left_trigger_value - self._left_grip_value),
            ],
            dtype=float,
        )
        processed = np.zeros(3, dtype=float)
        for i, value in enumerate(raw):
            if abs(value) >= self.gamepad_deadband:
                processed[i] = value
        return processed * self.gamepad_linear_speed_xyz * self.gamepad_linear_sign_xyz

    def _compute_gamepad_angular(self) -> np.ndarray:
        raw = np.array(
            [
                0.0,
                float(self._right_stick[1]),
                float(self._right_stick[0]),
            ],
            dtype=float,
        )
        processed = np.zeros(3, dtype=float)
        for i, value in enumerate(raw):
            if abs(value) >= self.gamepad_deadband:
                processed[i] = value
        return processed * self.gamepad_angular_speed_xyz * self.gamepad_angular_sign_xyz

    @staticmethod
    def _normalize_quat(q: np.ndarray) -> np.ndarray:
        if q.shape != (4,):
            return np.array([0.0, 0.0, 0.0, 1.0], dtype=float)
        n = float(np.linalg.norm(q))
        if n < 1e-9:
            return np.array([0.0, 0.0, 0.0, 1.0], dtype=float)
        return q / n

    @staticmethod
    def _quat_conjugate(q: np.ndarray) -> np.ndarray:
        return np.array([-q[0], -q[1], -q[2], q[3]], dtype=float)

    @staticmethod
    def _quat_multiply(a: np.ndarray, b: np.ndarray) -> np.ndarray:
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

    def _quat_delta_to_rotvec(self, q_ref: np.ndarray, q_curr: np.ndarray) -> np.ndarray:
        q_delta = self._quat_multiply(q_curr, self._quat_conjugate(q_ref))
        q_delta = self._normalize_quat(q_delta)

        if q_delta[3] < 0.0:
            q_delta = -q_delta

        xyz = q_delta[:3]
        xyz_norm = float(np.linalg.norm(xyz))
        if xyz_norm < 1e-9:
            return np.zeros(3, dtype=float)

        w = max(-1.0, min(1.0, float(q_delta[3])))
        angle = 2.0 * math.atan2(xyz_norm, w)
        axis = xyz / xyz_norm
        return axis * angle

    @staticmethod
    def _rotvec_to_quat(rotvec: np.ndarray) -> np.ndarray:
        angle = float(np.linalg.norm(rotvec))
        if angle < 1e-9:
            return np.array([0.0, 0.0, 0.0, 1.0], dtype=float)

        axis = rotvec / angle
        half = 0.5 * angle
        s = math.sin(half)
        c = math.cos(half)
        return np.array([axis[0] * s, axis[1] * s, axis[2] * s, c], dtype=float)

    def _lookup_ee_pose(self, now: float):
        try:
            tf = self._tf_buffer.lookup_transform(self.target_frame, self.ee_frame, rclpy.time.Time())
        except Exception as exc:
            if now - self._last_tf_warn_time > 2.0:
                self.get_logger().warn(
                    f"TF lookup failed for {self.target_frame} <- {self.ee_frame}: {exc}"
                )
                self._last_tf_warn_time = now
            return False, np.zeros(3, dtype=float), np.array([0.0, 0.0, 0.0, 1.0], dtype=float)

        ee_pos = np.array(
            [
                float(tf.transform.translation.x),
                float(tf.transform.translation.y),
                float(tf.transform.translation.z),
            ],
            dtype=float,
        )
        ee_rot = self._normalize_quat(
            np.array(
                [
                    float(tf.transform.rotation.x),
                    float(tf.transform.rotation.y),
                    float(tf.transform.rotation.z),
                    float(tf.transform.rotation.w),
                ],
                dtype=float,
            )
        )
        return True, ee_pos, ee_rot

    @staticmethod
    def _apply_speed_limits(vec: np.ndarray, max_speed: float, deadband: float) -> np.ndarray:
        mag = float(np.linalg.norm(vec))
        if mag < deadband:
            return np.zeros(3, dtype=float)
        if max_speed > 0.0 and mag > max_speed:
            vec = vec * (max_speed / mag)
        return vec


def main(args=None):
    rclpy.init(args=args)
    node = ReceivedPoseToTargetTwist()
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
