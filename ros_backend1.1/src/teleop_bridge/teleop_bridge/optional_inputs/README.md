# Optional Single-Arm Input Nodes

These nodes are right-arm trial inputs by default. They publish `TargetTwistStates`
so the existing Servo bridge, coupled gripper controller, reset manager, and
haptic/contact observers stay in the normal backend data path.

Current scripts:

- `keyboard_single_arm_controller.py`: terminal keyboard controller for pick/place baseline trials.
- `spacemouse_single_arm_controller.py`: HID-based 3Dconnexion SpaceMouse controller.
- `spacemouse_tcp_target_bridge.py`: ROS/container TCP receiver for macOS SpaceMouse packets.
- `mac_spacemouse_host_bridge.py`: macOS host-side HID reader that forwards SpaceMouse packets to the container bridge.

Quest gamepad/thumbstick mode is not a separate script. It is implemented inside
`teleop_bridge/mapping/hand_pose_mapper.py` because it uses fields already sent
by the Quest controller packet.

## Keyboard Right-Arm Baseline

Start it with:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh right_keyboard
```

Current key layout:

- `W/S`: forward/back.
- `A/D`: left/right.
- `Q/E`: up/down.
- `U/J`: roll +/-.
- `I/K`: pitch +/-.
- `O/L`: yaw +/-.
- `G`: toggle gripper close/open.
- `Space`: stop arm motion.
- `X` or `Ctrl-C`: quit.

This command uses `keyboard_single_arm_controller`, not the older
`keyboard_servo_override`. The old direct-Servo keyboard path is still available
as `./scripts/backend11_lifecycle.sh keyboard`, but it should not be used for
right-arm baseline trials because it bypasses the normal `TargetTwistStates`
pipeline.

One right-arm gamepad mode is available from the lifecycle script:

- `right_gamepad_on`: rotation-capable mode. Normal layer uses the sticks for translation; hold right `A` or left `X` to map left stick X to roll, right stick Y to pitch, and right stick X to yaw.

The rotation-capable mode is controlled by mapper parameters:

```text
gamepad_rotation_mode: stick_modifier
gamepad_linear_speed_xyz: [0.30, 0.30, 0.30]
gamepad_angular_speed_xyz: [0.55, 0.70, 0.70]
gamepad_angular_sign_xyz: [1.0, 1.0, -1.0]
```

## SpaceMouse On macOS Docker Desktop

Docker Desktop for macOS does not expose USB HID devices to the Linux container
the same way native Ubuntu does. Use the two-part bridge:

1. Start the ROS/container receiver and temporarily replace only the right-arm
   hand-pose mapper:

   ```bash
   cd ros_backend1.1
   ./scripts/backend11_lifecycle.sh right_spacemouse_host_bridge
   ```

2. Install the host-side Python dependency once:

   ```bash
   cd /Users/noahli/ros_unity_project
   python3 -m venv ros_backend1.1/.venv_spacemouse_host
   ros_backend1.1/.venv_spacemouse_host/bin/python -m pip install hidapi
   ```

3. Confirm macOS can see the SpaceMouse:

   ```bash
   ros_backend1.1/.venv_spacemouse_host/bin/python \
     ros_backend1.1/src/teleop_bridge/teleop_bridge/optional_inputs/mac_spacemouse_host_bridge.py \
     --detect-only
   ```

   If macOS lists multiple matching SpaceMouse interfaces, the bridge uses
   `MATCH[0]` by default. If the bridge connects but motion stays zero, retry
   with `--device-index 1`.

4. Run the host bridge:

   ```bash
   ros_backend1.1/.venv_spacemouse_host/bin/python \
     ros_backend1.1/src/teleop_bridge/teleop_bridge/optional_inputs/mac_spacemouse_host_bridge.py \
     --host 127.0.0.1 \
     --port 5036 \
     --device-index 1 \
     --linear-sign-xyz -1.0,-1.0,-1.0 \
     --angular-speed-xyz 1.4,1.4,1.4
   ```

   The default SpaceMouse mapping uses cap translation for right-arm XYZ
   velocity and cap twist/rotation for right-arm roll/pitch/yaw.
   `linear_sign_xyz=[-1,-1,-1]` keeps the corrected Z direction and restores
   the original X/Y feel. If a rotation axis feels inverted, flip the
   corresponding entry in `--angular-sign-xyz`.

   Rotation support handles both common SpaceMouse HID formats: separate
   translation/rotation reports and combined 12-byte motion reports. If the ROS
   target bridge still logs `ang=(0,0,0)` while you twist the cap, rerun the
   host bridge with `--detect-only` and try the other `--device-index`.

5. Restore normal right-controller headset teleop:

   ```bash
   cd ros_backend1.1
   ./scripts/backend11_lifecycle.sh right_spacemouse_host_bridge_off
   ```

The direct `spacemouse_single_arm_controller.py` path is still useful on Linux
when `/dev/hidraw*` is available inside the container.

## Synthetic TCP Test

The macOS host bridge can test the TCP path without the physical SpaceMouse:

```bash
python3 ros_backend1.1/src/teleop_bridge/teleop_bridge/optional_inputs/mac_spacemouse_host_bridge.py \
  --synthetic \
  --host 127.0.0.1 \
  --port 5036 \
  --synthetic-duration-sec 3
```
