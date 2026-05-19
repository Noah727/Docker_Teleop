# Architecture

## Data Flow

```text
Meta Quest / Unity
  HandPoseSender.cs
    TCP JSON packets
      |
      v
ROS backend container
  quest_controller_receiver
    /received_pose_states
      |
      v
  received_pose_to_target_twist
    /target_twist_states
      |
      +--> target_twist_to_servo_cmd --> MoveIt Servo --> Gazebo robot arm
      |
      +--> target_twist_to_gripper_cmd --> Hand-E position controller
      |
      +--> target_twist_reset_manager --> reset / home / scene object reset

Gazebo / ROS state
  /joint_states, /tf, object poses, camera topics
      |
      v
Unity ROS-TCP-Connector
  Ur5eTrajectorySubscriber.cs
  SceneObjectPoseSyncManager.cs
  GazeboPoseStampedSubscriber.cs
```

## Unity Responsibilities

- Read headset/controller pose and controller buttons.
- Send controller state to ROS over TCP.
- Visualize robot joint states from ROS.
- Visualize synced table objects from Gazebo poses.
- Provide a wrist-camera preview and frame recording interface.
- Provide user-facing control panel and scene decorations.

Unity should be treated mostly as a visualizer and UI layer. Robot/object physics should come from Gazebo.

## ROS Backend Responsibilities

- Run Gazebo simulation.
- Run UR5e + Hand-E robot description and controllers.
- Receive Unity TCP controller data.
- Map controller input to target twist commands.
- Run MoveIt Servo and gripper command bridges.
- Publish Gazebo object poses back to Unity.
- Reset robot and scene objects.

## Main Runtime Nodes

- `quest_controller_receiver`: TCP JSON receiver from Unity.
- `received_pose_to_target_twist`: hand/gamepad mapping and mode logic.
- `target_twist_to_servo_cmd`: converts target twist state into servo commands.
- `target_twist_to_gripper_cmd`: converts gripper intent into Hand-E position commands.
- `target_twist_reset_manager`: B-button reset, home motion, object reset.
- `ros_tcp_endpoint`: Unity ROS-TCP bridge server.

## Network Ports

- Unity controller TCP to backend: host `127.0.0.1:5026` in wired mode.
- Unity ROS-TCP-Connector to backend: host `127.0.0.1:10001` in wired mode.
- Container controller listener: `5005`.
- Container ROS-TCP endpoint: `10000`.
