# 2026-05-24 Platform / Robot-Scene Interface Plan

## Goal

Turn the current UR5e + Hand-E + tabletop setup into a reusable platform where a user can swap robot arms and task scenes without rewriting the teleop loop.

## Important Reality Check

This is doable, but not with only an arbitrary Gazebo scene file and an arbitrary robot xacro.

A xacro usually defines links, joints, visuals, collisions, and maybe gazebo tags. It often does not fully define:

- MoveIt planning groups.
- Servo command topics.
- ROS 2 controller names and interfaces.
- End-effector frame naming.
- Gripper command semantics.
- Joint limits tuned for teleop.
- Object reset poses.
- Unity visual/sync bindings.
- Dual-arm namespaces and frame prefixes.

So the scalable interface should be: user provides robot/scene assets plus a small manifest that tells the platform how to use them.

## Recommended Project Model

Use three profiles:

- `RobotProfile`: describes one robot arm and its end effector.
- `SceneProfile`: describes the Gazebo world, Unity replica, task objects, and reset poses.
- `ControlProfile`: describes hand-pose, gamepad, rotation, gripper, and safety tuning.

The current UR5e setup becomes the first profile instead of being the hardcoded default.

## RobotProfile

Each robot profile should define:

- `robot_id`: for example `ur5e_hande`.
- `namespace`: for example `/arm_1`.
- `xacro_path`: robot description entry point.
- `moveit_config_package`: MoveIt config package.
- `base_frame`: for example `base_link`.
- `ee_frame`: for example `tool0`.
- `arm_joint_names`: ordered arm joints.
- `gripper_joint_names`: ordered gripper joints.
- `servo_command_topic`: target twist input.
- `joint_state_topic`: robot joint state output.
- `gripper_command_topic`: gripper controller command input.
- `home_joint_positions`: reset/home pose.
- `workspace_bounds`: safe teleop min/max.
- `unity_robot_prefab_or_urdf`: visual import path.

## SceneProfile

Each scene profile should define:

- `scene_id`: for example `tabletop_pick_place`.
- `gazebo_world_or_sdf`: Gazebo scene entry point.
- `spawn_pose`: where the robot is placed in the world.
- `unity_scene`: Unity scene to load.
- `workspace_root_name`: Unity workspace root, currently `GazeboWorkspace`.
- `objects`: object IDs, types, colors, sizes, initial poses, reset heights, and sync topics.
- `static_geometry`: tables, bins, plates, walls, props.
- `recording_views`: camera names, poses, resolution, frame rate.

## ControlProfile

Each control profile should define:

- Position mapping mode: `world_delta` or another mapping plugin.
- Position axis map/sign/scale/offset.
- Rotation axis map/sign/scale.
- Gamepad mode speeds, signs, and deadbands.
- Gripper open/close command values.
- Reset behavior.
- Safety limits and rate limits.

## Dual-Arm Requirement

Design as multi-robot from the start:

- Every robot gets a namespace: `/left_arm`, `/right_arm`, `/arm_1`, `/arm_2`.
- Every TF tree uses a prefix or clean namespaces to avoid duplicate frame names.
- Controller state includes an `arm_id` or routing rule.
- Mapping nodes run per arm or as one multi-arm coordinator.
- Reset managers run per arm and per scene.
- Unity uses one workspace root with multiple robot visual roots.

The first implementation can still run one UR5e, but the interfaces should not assume there is only one robot.

## Practical Migration Plan

1. Wrap the current UR5e + Hand-E setup in a `RobotProfile`.
2. Wrap the current Gazebo replica/tabletop setup in a `SceneProfile`.
3. Move teleop tuning YAML into a named `ControlProfile`.
4. Replace hardcoded Unity object names with a `ScenarioManager`.
5. Replace hardcoded backend launch assumptions with profile arguments.
6. Add a second robot namespace after the single-arm profile works cleanly.
7. Add a validator that checks frames, joints, controllers, and object sync topics before launch.

## Future Folder Shape

```text
profiles/
  robots/
    ur5e_hande/
      robot.yaml
      description/
      moveit_config/
      unity/
  scenes/
    tabletop_pick_place/
      scene.yaml
      gazebo/
      unity/
  controls/
    quest_world_delta.yaml
    keyboard_debug.yaml
ros_backend1.0/
  src/
  scripts/
UnityApp/
  Assets/
    Scripts/Scenario/
    Scenes/
    Robots/
```

## Near-Term Rule

Do not let the teleop mapping code know about `ur5e`, `Hand-E`, or a specific cube name. It should know only about profile fields like `base_frame`, `ee_frame`, `gripper_joint_names`, and `object_id`.

## 1.1 Scaffold Created

- Copied `ros_backend1.0` to `ros_backend1.1`.
- Renamed the lifecycle entrypoint to `scripts/backend11_lifecycle.sh`.
- Gave the new backend a separate Docker identity: `motion_planner_11`, compose project `ros_backend11`.
- Gave the new backend separate default TCP ports: ROS-TCP `10011`, Quest hand/control TCP `5031`.
- Added `simulation/worlds/ur_hande_dual_arm_tabletop.sdf`.
- Added `simulation/launch/run_dual_arm_tabletop_sim.sh`.
- Added left/right prefixed Gazebo controller YAML files.
- Added profile manifests under `ros_backend1.1/profiles`.
- Added Unity scene `Assets/Scenes/GazeboReplica_DualArm_MR.unity`.
- Added Unity scaffold builder `GazeboReplicaDualArmSceneBuilder`.

## Window Rotation Decision

Floating MR panels should not inherit controller rotation. The best default is:

- Drag changes position only.
- Keep the panel upright relative to the room/world.
- Keep panel yaw stable while dragging, or optionally re-face it toward the headset on release.
- Do not allow pitch/roll unless the user explicitly grabs a rotation handle.

This keeps UI readable and avoids the nauseating behavior where a panel tumbles with the controller.
