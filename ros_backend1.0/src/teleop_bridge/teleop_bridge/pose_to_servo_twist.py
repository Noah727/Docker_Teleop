import math

import numpy as np
import rclpy
from geometry_msgs.msg import PoseStamped, TwistStamped, Vector3Stamped
from rclpy.node import Node
from tf2_ros import Buffer, TransformListener


class PoseToServoTwist(Node):
    def __init__(self):
        super().__init__("pose_to_servo_twist")

        self.declare_parameter("input_pose_topic", "/servo_node/target_pose")
        self.declare_parameter("output_twist_topic", "/servo_node/delta_twist_cmds")
        self.declare_parameter("angular_cmd_topic", "/teleop_bridge/angular_twist_cmd")
        self.declare_parameter("frame_id", "base_link")
        self.declare_parameter("ee_frame", "tool0")
        self.declare_parameter("kp_linear", 1.0)
        self.declare_parameter("max_linear_speed", 0.15)
        self.declare_parameter("kp_angular", 1.0)
        self.declare_parameter("max_angular_speed", 1.0)
        self.declare_parameter("angular_deadband", 0.02)
        self.declare_parameter("angular_cmd_timeout", 0.25)
        self.declare_parameter("rate_hz", 30.0)

        self._in = self.get_parameter("input_pose_topic").value
        self._out = self.get_parameter("output_twist_topic").value
        self._angular_in = self.get_parameter("angular_cmd_topic").value
        self._frame = self.get_parameter("frame_id").value
        self._ee = self.get_parameter("ee_frame").value
        self._kp = float(self.get_parameter("kp_linear").value)
        self._vmax = float(self.get_parameter("max_linear_speed").value)
        self._kp_ang = float(self.get_parameter("kp_angular").value)
        self._wmax = float(self.get_parameter("max_angular_speed").value)
        self._ang_deadband = float(self.get_parameter("angular_deadband").value)
        self._ang_timeout = float(self.get_parameter("angular_cmd_timeout").value)
        self._rate = float(self.get_parameter("rate_hz").value)

        self._latest_pose = None
        self._latest_angular = np.array([0.0, 0.0, 0.0], dtype=float)
        self._latest_angular_stamp = None

        self._pub = self.create_publisher(TwistStamped, self._out, 10)
        self.create_subscription(PoseStamped, self._in, self._on_pose, 10)
        self.create_subscription(Vector3Stamped, self._angular_in, self._on_angular, 10)

        self._tf_buffer = Buffer()
        self._tf_listener = TransformListener(self._tf_buffer, self)
        self.create_timer(1.0 / max(self._rate, 1.0), self._tick)

        self.get_logger().info(
            f"Pose->Twist: {self._in} + {self._angular_in} -> {self._out}, frame={self._frame}, ee={self._ee}"
        )

    def _on_pose(self, msg: PoseStamped):
        self._latest_pose = msg

    def _on_angular(self, msg: Vector3Stamped):
        if msg.header.frame_id and msg.header.frame_id != self._frame:
            # Drop frame-mismatched angular command to avoid unsafe interpretation.
            return

        self._latest_angular = np.array([msg.vector.x, msg.vector.y, msg.vector.z], dtype=float)
        self._latest_angular_stamp = self.get_clock().now().nanoseconds

    def _tick(self):
        if self._latest_pose is None:
            return

        tgt = self._latest_pose
        if tgt.header.frame_id and tgt.header.frame_id != self._frame:
            return

        try:
            tf = self._tf_buffer.lookup_transform(self._frame, self._ee, rclpy.time.Time())
        except Exception:
            return

        ex = tgt.pose.position.x - tf.transform.translation.x
        ey = tgt.pose.position.y - tf.transform.translation.y
        ez = tgt.pose.position.z - tf.transform.translation.z

        vx = self._kp * ex
        vy = self._kp * ey
        vz = self._kp * ez

        speed = math.sqrt(vx * vx + vy * vy + vz * vz)
        if speed > self._vmax and speed > 1e-9:
            scale = self._vmax / speed
            vx *= scale
            vy *= scale
            vz *= scale

        wx, wy, wz = self._compute_angular_output()

        out = TwistStamped()
        out.header.stamp = self.get_clock().now().to_msg()
        out.header.frame_id = self._frame
        out.twist.linear.x = float(vx)
        out.twist.linear.y = float(vy)
        out.twist.linear.z = float(vz)
        out.twist.angular.x = float(wx)
        out.twist.angular.y = float(wy)
        out.twist.angular.z = float(wz)
        self._pub.publish(out)

    def _compute_angular_output(self):
        now_ns = self.get_clock().now().nanoseconds
        if self._latest_angular_stamp is None:
            return 0.0, 0.0, 0.0

        age_s = (now_ns - self._latest_angular_stamp) / 1e9
        if age_s > self._ang_timeout:
            return 0.0, 0.0, 0.0

        w = self._kp_ang * self._latest_angular
        w_norm = float(np.linalg.norm(w))

        if w_norm < self._ang_deadband:
            return 0.0, 0.0, 0.0

        if self._wmax > 0.0 and w_norm > self._wmax:
            w = w * (self._wmax / w_norm)

        return float(w[0]), float(w[1]), float(w[2])


def main(args=None):
    rclpy.init(args=args)
    node = PoseToServoTwist()
    try:
        rclpy.spin(node)
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()
