# Coupled Hand-E Gripper Controller

The default dual-arm setup uses the coupled Hand-E controller. This is the recommended path for Gazebo contact because it treats the two finger joints like one mechanical gripper instead of two unrelated sliders.

## Default Coupled Gripper

```bash
cd /Users/noahli/ros_unity_project/ros_backend1.1
./scripts/backend11_lifecycle.sh build_ws
./scripts/backend11_lifecycle.sh safe_down
./scripts/backend11_lifecycle.sh bringup_dual
```

This starts `coupled_gripper_controller` for both arms.

If Gazebo is already running and you only want to restart the Part 2/3 bridge nodes:

```bash
./scripts/backend11_lifecycle.sh start_dual_part23
```

## Optional Simple Position Gripper

Use this only if you intentionally want the old simple position bridge:

```bash
DUAL_GRIPPER_CONTROLLER=position ./scripts/backend11_lifecycle.sh bringup_dual
```

Or switch only Part 2/3 while Gazebo is running:

```bash
DUAL_GRIPPER_CONTROLLER=position ./scripts/backend11_lifecycle.sh start_dual_part23
```

## How It Works

The controller node is `coupled_gripper_controller`.
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
/left_arm/coupled_gripper_controller:
  ros__parameters:
    publish_rate_hz: 60.0
    speed_m_per_s: 0.22
    close_speed_m_per_s: 0.40
    open_speed_m_per_s: 0.22
    min_pos: -0.025
    max_pos: 0.0
    initial_pos: 0.0
    squeeze_margin_m: 0.003
```

Increase `close_speed_m_per_s` if closing feels too slow. Increase `open_speed_m_per_s` if opening feels too slow.

Increase `squeeze_margin_m` if objects slip. Decrease it if the fingers visibly press too far into objects.
