import time

import rclpy
from geometry_msgs.msg import PoseStamped
from rclpy.executors import ExternalShutdownException
from rclpy.node import Node
from tf2_ros import Buffer, TransformListener


class CubePoseSyncPublisher(Node):
    def __init__(self):
        super().__init__("cube_pose_sync_publisher")

        self.declare_parameter("output_topic", "/unity_sync/target_cube_pose")
        self.declare_parameter("target_frame", "base_link")
        self.declare_parameter("publish_rate_hz", 20.0)
        self.declare_parameter(
            "cube_frames",
            ["target_cube/cube_link", "target_cube::cube_link", "target_cube", "cube_link"],
        )
        self.declare_parameter("publish_default_when_unavailable", True)
        self.declare_parameter("default_pose_xyz", [0.60, 0.25, 0.03])
        self.declare_parameter("default_pose_xyzw", [0.0, 0.0, 0.0, 1.0])
        self.declare_parameter("warn_interval_sec", 2.0)

        self.output_topic = str(self.get_parameter("output_topic").value)
        self.target_frame = str(self.get_parameter("target_frame").value)
        self.publish_rate_hz = max(1.0, float(self.get_parameter("publish_rate_hz").value))
        self.cube_frames = [str(v) for v in self.get_parameter("cube_frames").value]
        self.publish_default_when_unavailable = bool(
            self.get_parameter("publish_default_when_unavailable").value
        )
        self.default_pose_xyz = self._parse_vec3(
            self.get_parameter("default_pose_xyz").value, [0.60, 0.25, 0.03]
        )
        self.default_pose_xyzw = self._parse_vec4(
            self.get_parameter("default_pose_xyzw").value, [0.0, 0.0, 0.0, 1.0]
        )
        self.warn_interval_sec = max(0.1, float(self.get_parameter("warn_interval_sec").value))

        self.pub = self.create_publisher(PoseStamped, self.output_topic, 10)
        self._tf_buffer = Buffer()
        self._tf_listener = TransformListener(self._tf_buffer, self)
        self.create_timer(1.0 / self.publish_rate_hz, self._publish_loop)

        self._active_cube_frame = ""
        self._last_warn_time = 0.0
        self._last_info_time = 0.0

        self.get_logger().info(
            f"CubePoseSyncPublisher started: output={self.output_topic}, "
            f"target_frame={self.target_frame}, cube_frames={self.cube_frames}, "
            f"publish_default_when_unavailable={self.publish_default_when_unavailable}"
        )

    @staticmethod
    def _parse_vec3(value, fallback):
        try:
            out = [float(value[0]), float(value[1]), float(value[2])]
            return out
        except Exception:
            return [float(fallback[0]), float(fallback[1]), float(fallback[2])]

    @staticmethod
    def _parse_vec4(value, fallback):
        try:
            out = [float(value[0]), float(value[1]), float(value[2]), float(value[3])]
            return out
        except Exception:
            return [float(fallback[0]), float(fallback[1]), float(fallback[2]), float(fallback[3])]

    def _lookup_cube_tf(self):
        now = time.monotonic()

        if self._active_cube_frame:
            try:
                tf = self._tf_buffer.lookup_transform(
                    self.target_frame, self._active_cube_frame, rclpy.time.Time()
                )
                return True, tf
            except Exception:
                self._active_cube_frame = ""

        for frame in self.cube_frames:
            try:
                tf = self._tf_buffer.lookup_transform(self.target_frame, frame, rclpy.time.Time())
                if frame != self._active_cube_frame:
                    self._active_cube_frame = frame
                    self.get_logger().info(
                        f"Cube TF source active: {self.target_frame} <- {self._active_cube_frame}"
                    )
                return True, tf
            except Exception:
                continue

        if now - self._last_warn_time > self.warn_interval_sec:
            self.get_logger().warn(
                f"No cube TF found for {self.target_frame} <- any({self.cube_frames}). "
                "Publishing default pose."
                if self.publish_default_when_unavailable
                else "No pose published."
            )
            self._last_warn_time = now
        return False, None

    def _publish_loop(self):
        msg = PoseStamped()
        msg.header.stamp = self.get_clock().now().to_msg()
        msg.header.frame_id = self.target_frame

        tf_ok, tf = self._lookup_cube_tf()
        if tf_ok and tf is not None:
            msg.pose.position.x = float(tf.transform.translation.x)
            msg.pose.position.y = float(tf.transform.translation.y)
            msg.pose.position.z = float(tf.transform.translation.z)
            msg.pose.orientation.x = float(tf.transform.rotation.x)
            msg.pose.orientation.y = float(tf.transform.rotation.y)
            msg.pose.orientation.z = float(tf.transform.rotation.z)
            msg.pose.orientation.w = float(tf.transform.rotation.w)
            self.pub.publish(msg)
            return

        if not self.publish_default_when_unavailable:
            return

        msg.pose.position.x = float(self.default_pose_xyz[0])
        msg.pose.position.y = float(self.default_pose_xyz[1])
        msg.pose.position.z = float(self.default_pose_xyz[2])
        msg.pose.orientation.x = float(self.default_pose_xyzw[0])
        msg.pose.orientation.y = float(self.default_pose_xyzw[1])
        msg.pose.orientation.z = float(self.default_pose_xyzw[2])
        msg.pose.orientation.w = float(self.default_pose_xyzw[3])
        self.pub.publish(msg)

        now = time.monotonic()
        if now - self._last_info_time > 5.0:
            self.get_logger().info(
                "Publishing fallback cube pose; update cube_frames or provide cube TF for live sync."
            )
            self._last_info_time = now


def main(args=None):
    rclpy.init(args=args)
    node = CubePoseSyncPublisher()
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
