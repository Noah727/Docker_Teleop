import time

import rclpy
from rclpy.node import Node
from std_msgs.msg import Float64MultiArray, Int8


class GripperHoldToPosition(Node):
    def __init__(self):
        super().__init__("gripper_hold_to_position")

        self.declare_parameter("input_topic", "/teleop_bridge/gripper_hold_cmd")
        self.declare_parameter("output_topic", "/hande_position_controller/commands")
        self.declare_parameter("rate_hz", 30.0)
        self.declare_parameter("speed_m_per_s", 0.03)
        self.declare_parameter("min_pos", 0.0)
        self.declare_parameter("max_pos", 0.025)
        self.declare_parameter("initial_pos", 0.025)

        self._input_topic = str(self.get_parameter("input_topic").value)
        self._output_topic = str(self.get_parameter("output_topic").value)
        self._rate_hz = max(1.0, float(self.get_parameter("rate_hz").value))
        self._speed = max(0.0, float(self.get_parameter("speed_m_per_s").value))
        self._min_pos = float(self.get_parameter("min_pos").value)
        self._max_pos = float(self.get_parameter("max_pos").value)
        self._target_pos = float(self.get_parameter("initial_pos").value)

        if self._min_pos > self._max_pos:
            self._min_pos, self._max_pos = self._max_pos, self._min_pos
        self._target_pos = max(self._min_pos, min(self._max_pos, self._target_pos))

        self._hold_cmd = 0
        self._last_tick = time.monotonic()

        self._pub = self.create_publisher(Float64MultiArray, self._output_topic, 10)
        self.create_subscription(Int8, self._input_topic, self._on_hold_cmd, 10)
        self.create_timer(1.0 / self._rate_hz, self._tick)

        self.get_logger().info(
            "Gripper hold->position node started: "
            f"{self._input_topic} -> {self._output_topic}, "
            f"range=[{self._min_pos:.4f}, {self._max_pos:.4f}], speed={self._speed:.4f}"
        )

    def _on_hold_cmd(self, msg: Int8):
        if msg.data > 0:
            self._hold_cmd = 1
        elif msg.data < 0:
            self._hold_cmd = -1
        else:
            self._hold_cmd = 0

    def _tick(self):
        now = time.monotonic()
        dt = max(0.0, now - self._last_tick)
        self._last_tick = now

        if self._hold_cmd != 0 and self._speed > 0.0:
            # +1 closes (towards 0.0), -1 opens (towards max).
            direction = -1.0 if self._hold_cmd > 0 else 1.0
            self._target_pos += direction * self._speed * dt
            self._target_pos = max(self._min_pos, min(self._max_pos, self._target_pos))

        out = Float64MultiArray()
        out.data = [float(self._target_pos)]
        self._pub.publish(out)


def main(args=None):
    rclpy.init(args=args)
    node = GripperHoldToPosition()
    try:
        rclpy.spin(node)
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()
