# World-Frame Hand Mapping (ros_backend1.0)

## Goal

`ros_backend1.0` changes hand-pose teleop from absolute pose mapping to world-delta mapping.

Old `0.8` behavior:

```text
controller pose relative to headset -> axis map/scale/offset -> absolute EE target in base_link
```

New `1.0` default behavior:

```text
controller pose in Unity world -> mapped controller displacement -> EE displacement in base_link
```

This makes the robot move in the same direction as the controller motion, as long as the Unity world/table axes are mapped correctly to the robot base axes.

## Important Unity Setting

In the active Unity scene `Ur5e_Working 1`, `HandPoseSender.sendRelativeToHeadset` should be disabled.

The scene file currently has:

```yaml
sendRelativeToHeadset: 0
```

That means `HandPoseSender` sends the controller pose in Unity world coordinates instead of headset-local coordinates.

If this is accidentally enabled, the backend will receive headset-relative motion again, and moving your head can change the controller frame. That is exactly what `0.9` is trying to avoid.

## Runtime Formula

When hand-pose mode starts and valid TF is available, the mapper captures:

```text
hand_ref = mapped current controller world position
EE_ref   = current tool0 position in base_link
```

Then every frame:

```text
hand_delta = mapped_current_hand - hand_ref
EE_target  = EE_ref + hand_delta * scale_xyz + offset_xyz
EE_target  = clamp(EE_target, min_xyz, max_xyz)
twist      = kp_linear * (EE_target - current_EE)
```

The node still publishes velocity, not direct joint positions. MoveIt Servo receives a twist command and moves the arm toward that target.

## Why Delta Instead Of Absolute

Absolute mapping only works cleanly if Unity world coordinates are numerically calibrated into the robot workspace. That requires a carefully tuned offset and can jump when tracking starts.

World-delta mapping is more practical for headset control:

- It does not require the controller's absolute Unity position to equal a robot workspace coordinate.
- It captures the robot's current EE position when control starts.
- It maps hand movement direction to robot movement direction.
- `scale_xyz` becomes an intuitive motion gain.

## Parameters

Config file:

```bash
ros_backend1.0/src/teleop_bridge/config/teleop_tuning.yaml
```

Default mode:

```yaml
position_mapping_mode: world_delta
```

Useful live commands:

```bash
cd ros_backend1.0
CONTAINER=motion_planner_10
ROS_ENV='source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash'

docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist position_mapping_mode"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist position_mapping_mode world_delta"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist position_mapping_mode absolute"
```

Tune direction mapping:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist map_axes \"['z','x','y']\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist map_signs \"[1.0,-1.0,1.0]\""
```

Tune movement scale:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist scale_xyz \"[1.0,1.0,1.0]\""
```

For world-delta mode, keep offset zero unless you intentionally want a constant target bias:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist offset_xyz \"[0.0,0.0,0.0]\""
```

## When The Reference Resets

The world-delta reference is recaptured when:

- tracking is lost and regained
- teleop is engaged with right grip
- you switch between hand-pose mode and gamepad mode with left `Y`
- reset mode is toggled with right `B`; while reset is active, the pending hand reference keeps refreshing from the current tracked hand pose
- the right thumbstick is held and released; arm motion pauses while held, then the release pose becomes the new hand reference without homing the robot or resetting scene objects
- mapper frame/mapping/scale/offset parameters are changed live
- the node restarts

This avoids sudden jumps after mode changes.

## Rotation

Rotation control remains delta-based. Holding right-controller `A` captures the current controller orientation and current EE orientation, then maps controller delta rotation into EE delta rotation using:

```yaml
rot_map_axes
rot_map_signs
rot_scale_xyz
```

So position and rotation now have separate tuning parameters, but both are still delta-style controls.
