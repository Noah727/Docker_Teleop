# Troubleshooting

## Robot Does Not Move

Check backend state:

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh status
```

Check mapper output:

```bash
CONTAINER=motion_planner_10
ROS_ENV='source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash'
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /target_twist_states --once"
```

Common causes:

- Right grip is not held. Arm motion is gated by right grip.
- B reset is active. In `1.0`, reset should auto-release after `reset_hold_sec`.
- Part 2/3 nodes are stale. Restart them:

```bash
./scripts/backend10_lifecycle.sh start_part23
```

## Y Button Seems To Break Motion

`Y` toggles hand-pose mode vs thumbstick/gamepad mode.

- In `hand_pose` mode, right hand/controller motion moves the robot while right grip is held.
- In `gamepad` mode, the robot ignores hand-position motion and uses sticks/triggers instead.
- Press `Y` again to return to hand-pose mode.

Live check:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist control_mode"
```

## Floating Control Panel Has No Camera Preview

Check that `GripperCameraRecorder` is enabled on `GripperDataCamera` in Unity.

The preview depends on a runtime RenderTexture. If the panel exists but is blank:

- Stop Play mode.
- Select `GripperDataCamera`.
- Confirm `Gripper Camera Recorder` is enabled.
- Confirm `Assign Runtime Render Texture` is enabled.
- Press Play again.

## Recording Causes Lag

Recording currently uses GPU readback + image encoding + synchronous file writes.

Reduce load by changing `GripperDataCamera > GripperCameraRecorder`:

- Increase `Capture Every N Frames` from `5` to `10` or `15`.
- Reduce `Width/Height` from `1280x720` to `640x360`.
- Keep `Force Render Before Capture` disabled unless needed.

Future improvement: async GPU readback and JPEG/video encoding.

## Gripper Visual Moves Opposite Direction

Unity uses visual-only gripper finger motion from `/joint_states`.

Current intended settings in `Ur5e_Working 1`:

```yaml
useAbsoluteGripperJointForVisuals: 1
gripperClosedPositionMeters: 0
gripperOpenPositionMeters: 0.025
leftFingerLocalAxis: {x: 0, y: 0, z: 1}
rightFingerLocalAxis: {x: 0, y: 0, z: 1}
leftFingerDirectionSign: 1
rightFingerDirectionSign: -1
```

If a reimported robot has different mesh orientation, only adjust:

- `leftFingerLocalAxis`
- `rightFingerLocalAxis`
- `leftFingerDirectionSign`
- `rightFingerDirectionSign`

Do not re-enable gripper ArticulationBodies unless Unity physics is intentionally being used.

## Docker On macOS Uses CPU Heavily

Gazebo camera rendering inside Docker on macOS generally does not get native GPU acceleration like a Linux workstation. Camera rendering and PNG recording can lower real-time factor.

Useful mitigations:

- Lower camera resolution.
- Record fewer frames.
- Run headless when not visually debugging Gazebo.
- Use a Linux workstation with proper GPU passthrough for heavier simulation work.
