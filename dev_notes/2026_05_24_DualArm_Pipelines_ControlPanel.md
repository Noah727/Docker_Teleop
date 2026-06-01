# Dual-Arm Pipelines And Control Panel Facing

## Goal

Complete the first pass of the `ros_backend1.1` dual-arm control pipeline and make the MR control panel easier to move by keeping it facing the headset during and after dragging.

## Implemented

- Quest TCP receiver now keeps the original `/received_pose_states` output and also publishes per-arm streams:
  - `/left_arm/received_pose_states`
  - `/right_arm/received_pose_states`
- Added per-arm teleop tuning files:
  - `ros_backend1.1/src/teleop_bridge/config/teleop_tuning_dual_left.yaml`
  - `ros_backend1.1/src/teleop_bridge/config/teleop_tuning_dual_right.yaml`
- Added a dual Servo launch file:
  - `ros_backend1.1/src/servo_test_config/launch/servo_dual_gz.launch.py`
- Updated the dual Gazebo controller names so both arms do not fight over the same global controller topics.
- Added backend lifecycle commands:
  - `start_dual_sim`
  - `start_dual_servo`
  - `start_dual_part23`
  - `start_dual_part4`
  - `bringup_dual`
- Added attachment-mode support in the mapper. When a per-arm attachment flag is enabled, the mapper uses direct absolute target behavior instead of the usual hand-delta behavior.
- Updated `VRDraggableWindow` so the control panel can face the headset while dragging and after release.

## Validation

- Python syntax check passed for modified backend files.
- Shell syntax check passed for the lifecycle and dual Gazebo launch scripts.
- YAML parsing passed for new dual tuning/controller files.
- Unity C# compile passed with existing Cinemachine sample warnings only.

## Runtime TODO

- Rebuild `ros_backend1.1` in the container and test `./scripts/backend11_lifecycle.sh bringup_dual`.
- Dual Gazebo robot spawning is validated: `left_ur5e_hande` and `right_ur5e_hande` appear in `ign model --list`.
- Dual Gazebo controllers are validated active:
  - `left_joint_state_broadcaster`
  - `left_joint_group_velocity_controller`
  - `left_hande_position_controller`
  - `right_joint_state_broadcaster`
  - `right_joint_group_velocity_controller`
  - `right_hande_position_controller`
- Fixes required during validation:
  - `gz_ros2_control` needed `robot_param_node` pointed at each prefixed robot-state-publisher.
  - This container's plugin expected `controller_manager_name`, not only `controller_manager_node_name`.
  - Controller YAML root keys had to match the real manager node names: `left_controller_manager` and `right_controller_manager`.
- If Servo fails, inspect `/tmp/servo_dual_gz.log`; the riskiest part is namespaced MoveIt Servo with two prefixed robot models.
- Tune left/right workspace offsets after the dual Gazebo scene is visually confirmed.
