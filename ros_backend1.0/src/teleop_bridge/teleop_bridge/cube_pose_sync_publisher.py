import time
from dataclasses import dataclass

import rclpy
from geometry_msgs.msg import PoseStamped
from rclpy.executors import ExternalShutdownException
from rclpy.node import Node
from tf2_msgs.msg import TFMessage
from tf2_ros import Buffer, TransformListener

from .scene_layout import load_scene_object_poses_from_sdf


@dataclass(frozen=True)
class TrackedObjectSpec:
    name: str
    output_topic: str
    frames: tuple
    default_pose_xyz: tuple
    default_pose_xyzw: tuple
    tf_fallback_enabled: bool = False


class CubePoseSyncPublisher(Node):
    def __init__(self):
        super().__init__("cube_pose_sync_publisher")

        self.declare_parameter("target_frame", "base_link")
        self.declare_parameter("publish_rate_hz", 20.0)
        self.declare_parameter("use_gz_dynamic_pose_topic", True)
        self.declare_parameter("gz_dynamic_pose_topic", "/world/ur_hande_tabletop/dynamic_pose/info")
        self.declare_parameter("gz_pose_timeout_sec", 0.6)
        self.declare_parameter("publish_default_when_unavailable", True)
        self.declare_parameter("warn_interval_sec", 2.0)
        self.declare_parameter("sync_scene_objects", True)
        self.declare_parameter(
            "scene_layout_sdf_path",
            "/home/noah/ws_moveit/simulation/worlds/ur_hande_tabletop.sdf",
        )

        self.target_frame = str(self.get_parameter("target_frame").value)
        self.publish_rate_hz = max(1.0, float(self.get_parameter("publish_rate_hz").value))
        self.use_gz_dynamic_pose_topic = bool(self.get_parameter("use_gz_dynamic_pose_topic").value)
        self.gz_dynamic_pose_topic = str(self.get_parameter("gz_dynamic_pose_topic").value)
        self.gz_pose_timeout_sec = max(0.05, float(self.get_parameter("gz_pose_timeout_sec").value))
        self.publish_default_when_unavailable = bool(
            self.get_parameter("publish_default_when_unavailable").value
        )
        self.warn_interval_sec = max(0.1, float(self.get_parameter("warn_interval_sec").value))
        self.sync_scene_objects = bool(self.get_parameter("sync_scene_objects").value)
        self.scene_layout_sdf_path = str(self.get_parameter("scene_layout_sdf_path").value)

        self._specs = []

        if self.sync_scene_objects:
            self._specs.extend(self._build_scene_object_specs())

        self._publishers = {spec.name: self.create_publisher(PoseStamped, spec.output_topic, 10) for spec in self._specs}
        self._tf_buffer = Buffer()
        self._tf_listener = TransformListener(self._tf_buffer, self)
        self._gz_latest = {}
        self._gz_last_rx_time = 0.0
        if self.use_gz_dynamic_pose_topic:
            self.create_subscription(TFMessage, self.gz_dynamic_pose_topic, self._on_gz_dynamic_pose, 50)
        self.create_timer(1.0 / self.publish_rate_hz, self._publish_loop)

        self._active_frame_by_object = {}
        self._last_warn_time_by_object = {}
        self._last_info_time_by_object = {}

        self.get_logger().info(
            f"CubePoseSyncPublisher started: target_frame={self.target_frame}, "
            f"objects={[spec.name for spec in self._specs]}, "
            f"use_gz_dynamic_pose_topic={self.use_gz_dynamic_pose_topic}, "
            f"gz_dynamic_pose_topic={self.gz_dynamic_pose_topic}, "
            f"publish_default_when_unavailable={self.publish_default_when_unavailable}"
        )

    def _build_scene_object_specs(self):
        scene_layout = load_scene_object_poses_from_sdf(self.scene_layout_sdf_path, name_prefix="Sync_")
        bindings = [
            ("Sync_RedCube", "/unity_sync/Sync_RedCube_pose", ("Sync_RedCube/body_link", "Sync_RedCube::body_link", "Sync_RedCube")),
            ("Sync_GreenCube", "/unity_sync/Sync_GreenCube_pose", ("Sync_GreenCube/body_link", "Sync_GreenCube::body_link", "Sync_GreenCube")),
            ("Sync_RedCylinder", "/unity_sync/Sync_RedCylinder_pose", ("Sync_RedCylinder/body_link", "Sync_RedCylinder::body_link", "Sync_RedCylinder")),
            ("Sync_GreenCylinder", "/unity_sync/Sync_GreenCylinder_pose", ("Sync_GreenCylinder/body_link", "Sync_GreenCylinder::body_link", "Sync_GreenCylinder")),
            ("Sync_Plate_A", "/unity_sync/Sync_Plate_A_pose", ("Sync_Plate_A/plate_link", "Sync_Plate_A::plate_link", "Sync_Plate_A")),
            ("Sync_Plate_B", "/unity_sync/Sync_Plate_B_pose", ("Sync_Plate_B/plate_link", "Sync_Plate_B::plate_link", "Sync_Plate_B")),
        ]

        specs = []
        for name, topic, frames in bindings:
            scene_pose = scene_layout.get(name)
            if scene_pose is None:
                self.get_logger().warn(
                    f"{name} was not found in {self.scene_layout_sdf_path}; using origin as fallback pose."
                )
                default_pose_xyz = (0.0, 0.0, 0.0)
                default_pose_xyzw = (0.0, 0.0, 0.0, 1.0)
            else:
                default_pose_xyz = scene_pose.pose_xyz
                default_pose_xyzw = scene_pose.pose_xyzw

            specs.append(
                TrackedObjectSpec(
                    name=name,
                    output_topic=topic,
                    frames=frames,
                    default_pose_xyz=default_pose_xyz,
                    default_pose_xyzw=default_pose_xyzw,
                )
            )
        return specs

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

    def _on_gz_dynamic_pose(self, msg: TFMessage):
        latest = {}
        for tf in msg.transforms:
            child = str(tf.child_frame_id).strip()
            if not child:
                continue
            latest[child] = (
                (
                    float(tf.transform.translation.x),
                    float(tf.transform.translation.y),
                    float(tf.transform.translation.z),
                ),
                self._normalize_quat(
                    (
                        float(tf.transform.rotation.x),
                        float(tf.transform.rotation.y),
                        float(tf.transform.rotation.z),
                        float(tf.transform.rotation.w),
                    )
                ),
            )

        if latest:
            self._gz_latest = latest
            self._gz_last_rx_time = time.monotonic()

    @staticmethod
    def _quat_multiply(q1, q2):
        x1, y1, z1, w1 = q1
        x2, y2, z2, w2 = q2
        return (
            w1 * x2 + x1 * w2 + y1 * z2 - z1 * y2,
            w1 * y2 - x1 * z2 + y1 * w2 + z1 * x2,
            w1 * z2 + x1 * y2 - y1 * x2 + z1 * w2,
            w1 * w2 - x1 * x2 - y1 * y2 - z1 * z2,
        )

    @staticmethod
    def _quat_conjugate(q):
        return (-q[0], -q[1], -q[2], q[3])

    @staticmethod
    def _normalize_quat(q):
        x, y, z, w = q
        n = (x * x + y * y + z * z + w * w) ** 0.5
        if n < 1e-9:
            return (0.0, 0.0, 0.0, 1.0)
        inv = 1.0 / n
        return (x * inv, y * inv, z * inv, w * inv)

    def _rotate_vec_by_quat(self, v, q):
        qv = (v[0], v[1], v[2], 0.0)
        qr = self._quat_multiply(self._quat_multiply(q, qv), self._quat_conjugate(q))
        return (qr[0], qr[1], qr[2])

    def _lookup_from_gz_topic(self, spec: TrackedObjectSpec):
        if not self.use_gz_dynamic_pose_topic or not self._gz_latest:
            return False, None
        if (time.monotonic() - self._gz_last_rx_time) > self.gz_pose_timeout_sec:
            return False, None

        world_pose = None
        active_frame = None
        for frame in spec.frames:
            if frame in self._gz_latest:
                world_pose = self._gz_latest[frame]
                active_frame = frame
                break
        if world_pose is None:
            return False, None

        if active_frame != self._active_frame_by_object.get(spec.name):
            self._active_frame_by_object[spec.name] = active_frame
            self.get_logger().info(f"{spec.name} source active: {active_frame}")

        if self.target_frame in self._gz_latest:
            target_pose_world = self._gz_latest[self.target_frame]
        else:
            target_pose_world = ((0.0, 0.0, 0.0), (0.0, 0.0, 0.0, 1.0))

        obj_pos_w, obj_quat_w = world_pose
        tgt_pos_w, tgt_quat_w = target_pose_world
        inv_tgt_q = self._quat_conjugate(tgt_quat_w)
        delta_pos_w = (
            obj_pos_w[0] - tgt_pos_w[0],
            obj_pos_w[1] - tgt_pos_w[1],
            obj_pos_w[2] - tgt_pos_w[2],
        )
        rel_pos = self._rotate_vec_by_quat(delta_pos_w, inv_tgt_q)
        rel_quat = self._normalize_quat(self._quat_multiply(inv_tgt_q, obj_quat_w))
        return True, (rel_pos, rel_quat)

    def _lookup_from_tf(self, spec: TrackedObjectSpec):
        active = self._active_frame_by_object.get(spec.name, "")

        if active:
            try:
                tf = self._tf_buffer.lookup_transform(self.target_frame, active, rclpy.time.Time())
                return True, tf
            except Exception:
                self._active_frame_by_object[spec.name] = ""

        for frame in spec.frames:
            try:
                tf = self._tf_buffer.lookup_transform(self.target_frame, frame, rclpy.time.Time())
                self._active_frame_by_object[spec.name] = frame
                self.get_logger().info(f"{spec.name} TF source active: {self.target_frame} <- {frame}")
                return True, tf
            except Exception:
                continue

        return False, None

    def _publish_default(self, spec: TrackedObjectSpec):
        msg = PoseStamped()
        msg.header.stamp = self.get_clock().now().to_msg()
        msg.header.frame_id = self.target_frame
        msg.pose.position.x = float(spec.default_pose_xyz[0])
        msg.pose.position.y = float(spec.default_pose_xyz[1])
        msg.pose.position.z = float(spec.default_pose_xyz[2])
        msg.pose.orientation.x = float(spec.default_pose_xyzw[0])
        msg.pose.orientation.y = float(spec.default_pose_xyzw[1])
        msg.pose.orientation.z = float(spec.default_pose_xyzw[2])
        msg.pose.orientation.w = float(spec.default_pose_xyzw[3])
        self._publishers[spec.name].publish(msg)

    def _publish_loop(self):
        now = time.monotonic()

        for spec in self._specs:
            msg = PoseStamped()
            msg.header.stamp = self.get_clock().now().to_msg()
            msg.header.frame_id = self.target_frame

            gz_ok, gz_pose = self._lookup_from_gz_topic(spec)
            if gz_ok and gz_pose is not None:
                pos, quat = gz_pose
                msg.pose.position.x = float(pos[0])
                msg.pose.position.y = float(pos[1])
                msg.pose.position.z = float(pos[2])
                msg.pose.orientation.x = float(quat[0])
                msg.pose.orientation.y = float(quat[1])
                msg.pose.orientation.z = float(quat[2])
                msg.pose.orientation.w = float(quat[3])
                self._publishers[spec.name].publish(msg)
                continue

            if spec.tf_fallback_enabled:
                tf_ok, tf = self._lookup_from_tf(spec)
                if tf_ok and tf is not None:
                    msg.pose.position.x = float(tf.transform.translation.x)
                    msg.pose.position.y = float(tf.transform.translation.y)
                    msg.pose.position.z = float(tf.transform.translation.z)
                    msg.pose.orientation.x = float(tf.transform.rotation.x)
                    msg.pose.orientation.y = float(tf.transform.rotation.y)
                    msg.pose.orientation.z = float(tf.transform.rotation.z)
                    msg.pose.orientation.w = float(tf.transform.rotation.w)
                    self._publishers[spec.name].publish(msg)
                    continue

            if not self.publish_default_when_unavailable:
                if now - self._last_warn_time_by_object.get(spec.name, 0.0) > self.warn_interval_sec:
                    self.get_logger().warn(f"No live pose found for {spec.name}; not publishing.")
                    self._last_warn_time_by_object[spec.name] = now
                continue

            self._publish_default(spec)
            if now - self._last_info_time_by_object.get(spec.name, 0.0) > 5.0:
                self.get_logger().info(
                    f"Publishing fallback pose for {spec.name}; update frames or provide live Gazebo pose."
                )
                self._last_info_time_by_object[spec.name] = now


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
