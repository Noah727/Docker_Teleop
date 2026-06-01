# Coupled Hand-E Gripper Controller

The default dual-arm setup uses the smoother position-controller gripper path.
Use the coupled controller only when testing Gazebo contact behavior.

## Default Smooth Gripper

```bash
cd /Users/noahli/ros_unity_project/ros_backend1.1
./scripts/backend11_lifecycle.sh build_ws
./scripts/backend11_lifecycle.sh safe_down
./scripts/backend11_lifecycle.sh bringup_dual
```

This starts `target_twist_to_gripper_cmd`, which publishes the same position target to both Hand-E finger joints.

## Experimental Coupled Gripper

```bash
cd /Users/noahli/ros_unity_project/ros_backend1.1
./scripts/backend11_lifecycle.sh build_ws
DUAL_GRIPPER_CONTROLLER=coupled ./scripts/backend11_lifecycle.sh safe_down
DUAL_GRIPPER_CONTROLLER=coupled ./scripts/backend11_lifecycle.sh bringup_dual
```

If Gazebo is already running and you only want to switch the Part 2/3 bridge nodes:

```bash
DUAL_GRIPPER_CONTROLLER=coupled ./scripts/backend11_lifecycle.sh start_dual_part23
```

Switch back to the default gripper bridge:

```bash
DUAL_GRIPPER_CONTROLLER=position ./scripts/backend11_lifecycle.sh start_dual_part23
```

## How It Works

The experimental node is `coupled_hande_gripper_controller`.
It treats the two Hand-E finger joints as one shared aperture:

- One scalar target is used for both fingers.
- Closing moves the shared target toward `min_pos`.
- Opening moves the shared target toward `max_pos`.
- If one finger is blocked by contact, the shared target is clamped near the most-open measured finger.
- The other finger backs off to the same target instead of continuing to fight independently.

This is not a compiled Gazebo physics plugin yet. It is a ROS-side controller that emulates the mechanical coupling behavior well enough for testing.

## Tuning

The parameters live in:

- `src/teleop_bridge/config/teleop_tuning_dual_left.yaml`
- `src/teleop_bridge/config/teleop_tuning_dual_right.yaml`

Relevant section:

```yaml
/left_arm/coupled_hande_gripper_controller:
  ros__parameters:
    speed_m_per_s: 0.03
    close_speed_m_per_s: 0.06
    open_speed_m_per_s: 0.05
    min_pos: -0.025
    max_pos: 0.0
    initial_pos: 0.0
    squeeze_margin_m: 0.003
```

Increase `close_speed_m_per_s` if closing feels too slow. Increase `open_speed_m_per_s` if opening feels too slow.

Increase `squeeze_margin_m` if objects slip. Decrease it if the fingers visibly press too far into objects.
