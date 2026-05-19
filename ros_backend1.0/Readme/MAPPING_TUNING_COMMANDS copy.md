# Mapping + Reset Tuning Commands (ros_backend1.0)

This file contains the commands to tune mapping and reset behavior.

## 0) Setup

```bash
cd /Users/noahli/ros_unity_project/ros_backend1.0
CONTAINER=motion_planner_10
ROS_ENV='source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash'
```

## 1) Ensure nodes are up

```bash
./scripts/backend10_lifecycle.sh status
```

If mapper/reset nodes are missing:

```bash
./scripts/backend10_lifecycle.sh start_part23
```

## 2) Read current mapper/reset parameters

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist map_axes"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist map_signs"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist position_mapping_mode"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist rot_map_axes"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist rot_map_signs"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist rot_scale_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist control_mode"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist start_in_gamepad_mode"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist gamepad_deadband"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist gamepad_linear_speed_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist gamepad_linear_sign_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist gamepad_angular_speed_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist gamepad_angular_sign_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /keyboard_servo_cmd linear_speed_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /keyboard_servo_cmd linear_sign_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /keyboard_servo_cmd angular_speed_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /keyboard_servo_cmd angular_sign_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /keyboard_servo_cmd key_timeout_sec"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist scale_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist offset_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist min_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist max_xyz"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist kp_linear"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /received_pose_to_target_twist kp_angular"

docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /target_twist_reset_manager home_joint_positions"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /target_twist_reset_manager kick_velocities"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /target_twist_reset_manager kick_duration_sec"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param get /target_twist_reset_manager scene_reset_lift_z"
```

## 3) Live mapper tuning (no restart)

Position mapping mode:

```bash
# New 1.0 behavior: hand/controller movement is interpreted as a world-frame displacement.
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist position_mapping_mode world_delta"

# Legacy 0.8 behavior: incoming hand/controller position maps directly to an absolute robot target.
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist position_mapping_mode absolute"
```

World-delta mode requires Unity to send world pose:

- In `Ur5e_Working 1`, `HandPoseSender.sendRelativeToHeadset` should be unchecked/false.
- When world-delta starts, the mapper captures the current controller pose and current `tool0` pose.
- The target is then `EE_ref + mapped(controller_world - controller_ref) * scale_xyz + offset_xyz`.
- Pressing right-controller `B` requests a recenter. While reset is active, the pending hand reference follows the current tracked hand pose, so the hand's current position becomes the new zero point when hand-pose control resumes.
- Holding the right thumbstick acts like a clutch: arm motion is paused while held, and the hand pose at release becomes the new position reference. This does not toggle robot/object reset.
- Keep `offset_xyz` at `[0.0,0.0,0.0]` unless you intentionally want a constant bias.

Axis map/sign:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist map_axes \"['z','x','y']\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist map_signs \"[1.0,-1.0,1.0]\""
```

Rotation axis map/sign/scale:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist rot_map_axes \"['z','x','y']\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist rot_map_signs \"[1.0,-1.0,1.0]\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist rot_scale_xyz \"[1.0,1.0,1.0]\""
```

Control mode:

```bash
# Hand/controller pose drives EE position.
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist control_mode hand_pose"

# Left stick + left trigger/grip drives EE position.
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist control_mode gamepad"
```

Gamepad mode defaults/tuning:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist start_in_gamepad_mode false"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist gamepad_deadband 0.15"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist gamepad_linear_speed_xyz \"[0.20,0.20,0.20]\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist gamepad_linear_sign_xyz \"[1.0,-1.0,1.0]\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist gamepad_angular_speed_xyz \"[0.0,0.75,0.75]\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist gamepad_angular_sign_xyz \"[1.0,1.0,-1.0]\""
```

Control-mode notes:

- Left controller `Y` toggles between `hand_pose` and `gamepad` mode.
- In `gamepad` mode:
  - left stick `Y` drives robot `+/-X` (forward/back)
  - left stick `X` drives robot `-/+Y` (right/left with the default sign setting)
  - left trigger drives robot `+Z` (up)
  - left grip drives robot `-Z` (down)
  - right stick `Y` drives robot angular `Y`
  - right stick `X` drives robot angular `Z`
- Right controller `A` still enables rotate mode.
- If `A` is held in gamepad mode, the existing controller-orientation delta rotation takes priority over right-stick rotation.
- Right controller `B` still triggers reset.
- Hold right grip to engage teleop. The arm only follows hand/gamepad motion while right grip is held; releasing right grip pauses arm motion so you can move your hand freely.
- Hold right thumbstick to pause arm motion and move your hand freely; release it to recenter the hand reference at the current hand pose. This does not home the robot or reset objects.
- Right trigger toggles the gripper command. First press closes, next press opens, then alternates.

After changing controller fields such as right-thumbstick recenter or right-grip teleop engage, rebuild the backend once so the ROS message definition is regenerated:

```bash
./scripts/backend10_lifecycle.sh up_container
./scripts/backend10_lifecycle.sh build_ws
```

Keyboard controller tuning:

```bash
# Start keyboard mode from a real terminal first:
./scripts/backend10_lifecycle.sh keyboard

# Then, from another terminal:
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /keyboard_servo_cmd linear_speed_xyz \"[0.15,0.15,0.15]\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /keyboard_servo_cmd linear_sign_xyz \"[1.0,1.0,1.0]\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /keyboard_servo_cmd angular_speed_xyz \"[0.50,0.50,0.50]\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /keyboard_servo_cmd angular_sign_xyz \"[1.0,1.0,1.0]\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /keyboard_servo_cmd key_timeout_sec 0.25"
```

Keyboard notes:
- `W/S`: robot `+/-X`
- `A/D`: robot `+/-Y`
- `Q/E`: robot `+/-Z`
- `U/J`: angular `+/-X`
- `I/K`: angular `+/-Y`
- `O/L`: angular `+/-Z`
- `Space`: stop immediately
- `X` or `Ctrl-C`: quit keyboard mode
- `keyboard` publishes directly to `/servo_node/delta_twist_cmds`, so the lifecycle command stops `target_twist_to_servo_cmd` first to avoid fighting the headset path.
- After quitting keyboard mode, run `./scripts/backend10_lifecycle.sh start_part23` to restore headset control.
- If holding a key feels pulsed, increase `key_timeout_sec` slightly, for example `0.35`.

Scale/offset/workspace:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist scale_xyz \"[1.0,1.0,1.0]\""

docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist offset_xyz \"[0.0,0.0,0.0]\""

docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist min_xyz \"[-1.0,-1.0,-1.0]\""
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist max_xyz \"[1.0,1.0,1.0]\""
```

Gains/limits/deadbands:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist kp_linear 2.0"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist max_linear_speed 0.30"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist linear_deadband 0.005"

docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist kp_angular 4.0"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist max_angular_speed 1.50"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /received_pose_to_target_twist angular_deadband 0.02"
```

## 4) Live reset tuning (no restart)

These apply to the next reset cycle. If a reset is currently running, the node rejects param changes; toggle reset OFF and retry.

Home and kick:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /target_twist_reset_manager home_joint_positions \"[-1.5,-1.5,1.5,0.0,0.0,0.0]\""

docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /target_twist_reset_manager kick_velocities \"[0.35,-0.18,-0.28,0.18,-0.28,0.0]\""

docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /target_twist_reset_manager kick_duration_sec 1.5"
```

Other reset knobs:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /target_twist_reset_manager home_kp 1.2"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /target_twist_reset_manager home_max_vel 0.60"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /target_twist_reset_manager home_tolerance 0.03"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /target_twist_reset_manager home_timeout_sec 8.0"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /target_twist_reset_manager min_reset_interval_sec 2.5"

# Dynamic scene objects reset to setup_z + scene_reset_lift_z.
# Default is 0.01 m, so objects are placed 1 cm above the table and settle down.
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 param set /target_twist_reset_manager scene_reset_lift_z 0.01"
```

## 5) Verify while tuning

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /target_twist_states"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && tail -f /tmp/part2_mapper.log"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && tail -f /tmp/part3_reset_manager.log"
```

## 6) Live scene-object position/orientation modifier

In `1.0`, the legacy single-cube reset path is disabled.  
For live scene-object adjustment, move the actual Gazebo model directly.

```bash
docker exec "$CONTAINER" bash -lc "ign service -s /world/ur_hande_tabletop/set_pose --reqtype ignition.msgs.Pose --reptype ignition.msgs.Boolean --timeout 2000 --req 'name: \"Sync_RedCube\" position: {x: 0.50538 y: 0.45121 z: 0.020} orientation: {x: 0 y: 0 z: 0 w: 1}'"
```

Check the mirrored Unity sync topic:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /unity_sync/Sync_RedCube_pose --once"
```

## 7) Persist tuning to file

Live `ros2 param set` is runtime-only. To keep values after restart, update:

`src/teleop_bridge/config/teleop_tuning.yaml`

Then restart Part2/Part3 nodes:

```bash
./scripts/backend10_lifecycle.sh start_part23
```

If you modified Python source code (node logic), rebuild workspace and restart Part2/Part3:

```bash
./scripts/backend10_lifecycle.sh build_ws
./scripts/backend10_lifecycle.sh start_part23
```

If you modified cube size/mass in world SDF, restart simulation:

```bash
./scripts/backend10_lifecycle.sh start_sim
```

## 8) Live spawn/remove another debug object

Spawn another debug cube now (example name `debug_cube_1`):

```bash
docker exec "$CONTAINER" bash -lc "cat > /tmp/debug_cube_1.sdf <<'EOF'
<?xml version='1.0'?>
<sdf version='1.8'>
  <model name='debug_cube_1'>
    <static>false</static>
    <link name='cube_link'>
      <inertial><mass>0.08</mass><inertia><ixx>0.00003</ixx><iyy>0.00003</iyy><izz>0.00003</izz><ixy>0</ixy><ixz>0</ixz><iyz>0</iyz></inertia></inertial>
      <collision name='collision'><geometry><box><size>0.045 0.045 0.045</size></box></geometry></collision>
      <visual name='visual'><geometry><box><size>0.045 0.045 0.045</size></box></geometry></visual>
    </link>
  </model>
</sdf>
EOF
source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash && ros2 run ros_gz_sim create -world ur_hande_tabletop -name debug_cube_1 -file /tmp/debug_cube_1.sdf -x 0.62 -y 0.05 -z 0.0225"
```

Move it live:

```bash
docker exec "$CONTAINER" bash -lc "ign service -s /world/ur_hande_tabletop/set_pose --reqtype ignition.msgs.Pose --reptype ignition.msgs.Boolean --timeout 2000 --req 'name: \"debug_cube_1\" position: {x: 0.62 y: 0.05 z: 0.023} orientation: {x: 0 y: 0 z: 0 w: 1}'"
```

Remove it:

```bash
docker exec "$CONTAINER" bash -lc "ign service -s /world/ur_hande_tabletop/remove --reqtype ignition.msgs.Entity --reptype ignition.msgs.Boolean --timeout 2000 --req 'name: \"debug_cube_1\" type: MODEL'"
```

## 9) Live resize + friction tune a synced scene object

Gazebo does not expose a direct \"resize collision geometry\" service.  
Use remove + respawn for true physics size/friction changes (live, no container restart).

Example: respawn `Sync_RedCube` with a custom size/friction while keeping the same synced object name.

```bash
docker exec "$CONTAINER" bash -lc "cat > /tmp/sync_red_cube_live.sdf <<'EOF'
<?xml version='1.0'?>
<sdf version='1.8'>
  <model name='Sync_RedCube'>
    <static>false</static>
    <link name='body_link'>
      <inertial>
        <mass>0.08</mass>
        <inertia>
          <ixx>0.00003</ixx><iyy>0.00003</iyy><izz>0.00003</izz>
          <ixy>0</ixy><ixz>0</ixz><iyz>0</iyz>
        </inertia>
      </inertial>
      <collision name='cube_collision'>
        <geometry><box><size>0.040 0.040 0.040</size></box></geometry>
        <surface>
          <friction><ode><mu>1.4</mu><mu2>1.4</mu2></ode></friction>
          <contact><ode><kp>200000</kp><kd>30</kd><max_vel>0.1</max_vel><min_depth>0.001</min_depth></ode></contact>
        </surface>
      </collision>
      <visual name='cube_visual'>
        <geometry><box><size>0.040 0.040 0.040</size></box></geometry>
        <material>
          <ambient>0.85 0.12 0.12 1</ambient>
          <diffuse>0.85 0.12 0.12 1</diffuse>
          <specular>0.1 0.1 0.1 1</specular>
        </material>
      </visual>
    </link>
  </model>
</sdf>
EOF
ign service -s /world/ur_hande_tabletop/remove --reqtype ignition.msgs.Entity --reptype ignition.msgs.Boolean --timeout 2000 --req 'name: \"Sync_RedCube\" type: MODEL' || true
source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash
ros2 run ros_gz_sim create -world ur_hande_tabletop -name Sync_RedCube -file /tmp/sync_red_cube_live.sdf -x 0.50538 -y 0.45121 -z 0.020"
```

Verify synced pose after respawn:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic echo /unity_sync/Sync_RedCube_pose --once"
```
